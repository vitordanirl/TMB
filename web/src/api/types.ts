// Contratos da API (JSON em snake_case, conforme o backend).

export const ORDER_STATUSES = ['Pendente', 'Processando', 'Finalizado'] as const

export type OrderStatus = (typeof ORDER_STATUSES)[number]

export interface Order {
  id: string
  cliente: string
  produto: string
  valor: number
  status: OrderStatus
  data_criacao: string
  data_atualizacao: string
}

export interface OrderStatusHistoryEntry {
  status_anterior: OrderStatus | null
  status_novo: OrderStatus
  ocorrido_em: string
}

export interface OrderDetails extends Order {
  historico: OrderStatusHistoryEntry[]
}

export interface PagedResponse<T> {
  items: T[]
  page: number
  page_size: number
  total_count: number
}

export interface CreateOrderInput {
  cliente: string
  produto: string
  valor: number
}

/** RFC 9457 Problem Details (com `errors` em respostas de validação). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}
