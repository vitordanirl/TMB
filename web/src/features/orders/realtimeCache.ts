import type { QueryClient } from '@tanstack/react-query'
import { ORDER_STATUSES, type Order, type OrderDetails, type OrderStatus, type PagedResponse } from '@/api/types'
import type { OrderStatusChangedEvent } from '@/realtime/events'
import { orderKeys } from './queries'

const statusRank = (status: OrderStatus) => ORDER_STATUSES.indexOf(status)

/**
 * Só avança o status: eventos duplicados ou fora de ordem (ex.: "Processando" chegando depois de
 * "Finalizado") não fazem a tela regredir.
 */
const advances = (current: OrderStatus, next: OrderStatus) => statusRank(next) > statusRank(current)

export function applyStatusToOrder<T extends Order>(order: T, event: OrderStatusChangedEvent): T {
  if (order.id !== event.order_id || !advances(order.status, event.status_novo)) {
    return order
  }

  return { ...order, status: event.status_novo, data_atualizacao: event.ocorrido_em }
}

export function applyStatusToDetails(order: OrderDetails, event: OrderStatusChangedEvent): OrderDetails {
  const updated = applyStatusToOrder(order, event)
  if (updated === order) {
    return order
  }

  const alreadyRecorded = order.historico.some((entry) => entry.status_novo === event.status_novo)

  return {
    ...updated,
    historico: alreadyRecorded
      ? order.historico
      : [
          ...order.historico,
          { status_anterior: event.status_anterior, status_novo: event.status_novo, ocorrido_em: event.ocorrido_em },
        ],
  }
}

/** Atualiza listas e detalhe em cache imediatamente a partir de um evento em tempo real. */
export function applyStatusChange(queryClient: QueryClient, event: OrderStatusChangedEvent) {
  queryClient.setQueriesData<PagedResponse<Order>>({ queryKey: orderKeys.lists() }, (data) => {
    if (!data?.items.some((order) => order.id === event.order_id)) {
      return data
    }

    return { ...data, items: data.items.map((order) => applyStatusToOrder(order, event)) }
  })

  queryClient.setQueryData<OrderDetails>(orderKeys.detail(event.order_id), (data) =>
    data ? applyStatusToDetails(data, event) : data,
  )
}
