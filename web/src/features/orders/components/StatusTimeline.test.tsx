import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { OrderDetails } from '@/api/types'
import { StatusTimeline } from './StatusTimeline'

const baseOrder: OrderDetails = {
  id: '01a11965-7ea4-729b-b590-4f3f5fc49019',
  cliente: 'Ana',
  produto: 'Teclado',
  valor: 250,
  status: 'Processando',
  data_criacao: '2026-10-08T02:00:00.000Z',
  data_atualizacao: '2026-10-08T02:00:02.000Z',
  historico: [
    { status_anterior: null, status_novo: 'Pendente', ocorrido_em: '2026-10-08T02:00:00.000Z' },
    { status_anterior: 'Pendente', status_novo: 'Processando', ocorrido_em: '2026-10-08T02:00:02.000Z' },
  ],
}

const steps = () => within(screen.getByRole('list', { name: 'Histórico de status' })).getAllByRole('listitem')

describe('StatusTimeline', () => {
  it('marca etapas concluídas, atual e futuras', () => {
    render(<StatusTimeline order={baseOrder} />)

    expect(steps().map((step) => step.dataset.state)).toEqual(['done', 'current', 'upcoming'])
    expect(screen.getByText('2,0 s após "Pendente"')).toBeInTheDocument()
  })

  it('marca todas as etapas como concluídas quando finalizado', () => {
    render(
      <StatusTimeline
        order={{
          ...baseOrder,
          status: 'Finalizado',
          historico: [
            ...baseOrder.historico,
            { status_anterior: 'Processando', status_novo: 'Finalizado', ocorrido_em: '2026-10-08T02:00:07.000Z' },
          ],
        }}
      />,
    )

    expect(steps().map((step) => step.dataset.state)).toEqual(['done', 'done', 'done'])
    expect(screen.getByText('5,0 s após "Processando"')).toBeInTheDocument()
  })
})
