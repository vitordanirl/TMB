import type { OrderStatus } from '@/api/types'

// Eventos emitidos pelo hub /hubs/orders (payload em snake_case, como a API REST).

export interface OrderCreatedEvent {
  order_id: string
  cliente: string
  produto: string
  valor: number
  data_criacao: string
}

export interface OrderStatusChangedEvent {
  order_id: string
  status_anterior: OrderStatus | null
  status_novo: OrderStatus
  ocorrido_em: string
}

export const HubEvents = {
  OrderCreated: 'OrderCreated',
  OrderStatusChanged: 'OrderStatusChanged',
} as const
