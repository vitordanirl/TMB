import { QueryClient } from '@tanstack/react-query'
import { describe, expect, it } from 'vitest'
import type { Order, OrderDetails, PagedResponse } from '@/api/types'
import type { OrderStatusChangedEvent } from '@/realtime/events'
import { orderKeys, pollInterval } from './queries'
import { applyStatusChange, applyStatusToDetails } from './realtimeCache'

const order: OrderDetails = {
  id: 'a1',
  cliente: 'Ana',
  produto: 'Teclado',
  valor: 250,
  status: 'Pendente',
  data_criacao: '2026-10-08T02:00:00.000Z',
  data_atualizacao: '2026-10-08T02:00:00.000Z',
  historico: [{ status_anterior: null, status_novo: 'Pendente', ocorrido_em: '2026-10-08T02:00:00.000Z' }],
}

const toProcessing: OrderStatusChangedEvent = {
  order_id: 'a1',
  status_anterior: 'Pendente',
  status_novo: 'Processando',
  ocorrido_em: '2026-10-08T02:00:01.000Z',
}

const toFinished: OrderStatusChangedEvent = {
  order_id: 'a1',
  status_anterior: 'Processando',
  status_novo: 'Finalizado',
  ocorrido_em: '2026-10-08T02:00:06.000Z',
}

describe('applyStatusToDetails', () => {
  it('avança o status e registra o histórico', () => {
    const updated = applyStatusToDetails(order, toProcessing)

    expect(updated.status).toBe('Processando')
    expect(updated.data_atualizacao).toBe(toProcessing.ocorrido_em)
    expect(updated.historico.map((h) => h.status_novo)).toEqual(['Pendente', 'Processando'])
  })

  it('ignora evento duplicado', () => {
    const once = applyStatusToDetails(order, toProcessing)

    expect(applyStatusToDetails(once, toProcessing)).toBe(once)
  })

  it('ignora evento fora de ordem (não regride o status)', () => {
    const finished = applyStatusToDetails(applyStatusToDetails(order, toProcessing), toFinished)

    expect(applyStatusToDetails(finished, toProcessing)).toBe(finished)
  })

  it('ignora evento de outro pedido', () => {
    expect(applyStatusToDetails(order, { ...toProcessing, order_id: 'outro' })).toBe(order)
  })
})

describe('applyStatusChange', () => {
  it('atualiza listas e detalhe em cache', () => {
    const queryClient = new QueryClient()
    const listKey = orderKeys.list({ page: 1, pageSize: 10 })
    const { historico: _historico, ...summary } = order
    const otherOrder: Order = { ...summary, id: 'b2' }

    queryClient.setQueryData<PagedResponse<Order>>(listKey, {
      items: [summary, otherOrder],
      page: 1,
      page_size: 10,
      total_count: 2,
    })
    queryClient.setQueryData(orderKeys.detail('a1'), order)

    applyStatusChange(queryClient, toProcessing)

    const list = queryClient.getQueryData<PagedResponse<Order>>(listKey)!
    expect(list.items.map((o) => [o.id, o.status])).toEqual([
      ['a1', 'Processando'],
      ['b2', 'Pendente'],
    ])
    expect(queryClient.getQueryData<OrderDetails>(orderKeys.detail('a1'))?.status).toBe('Processando')
  })
})

describe('pollInterval', () => {
  it('não faz polling com tempo real conectado', () => {
    expect(pollInterval('connected', true, true)).toBe(false)
  })

  it.each(['reconnecting', 'disconnected', 'connecting'] as const)(
    'faz polling rápido sem tempo real (%s) havendo pedidos em andamento',
    (status) => {
      expect(pollInterval(status, true, true)).toBe(3_000)
    },
  )

  it('faz polling lento sem tempo real e sem pedidos em andamento, quando habilitado', () => {
    expect(pollInterval('disconnected', false, true)).toBe(15_000)
    expect(pollInterval('disconnected', false, false)).toBe(false)
  })
})
