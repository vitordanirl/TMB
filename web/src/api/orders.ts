import { apiFetch } from './client'
import type { CreateOrderInput, Order, OrderDetails, OrderStatus, PagedResponse } from './types'

export interface ListOrdersParams {
  page: number
  pageSize: number
  status?: OrderStatus
}

export function listOrders({ page, pageSize, status }: ListOrdersParams, signal?: AbortSignal) {
  const query = new URLSearchParams({ page: String(page), page_size: String(pageSize) })
  if (status) {
    query.set('status', status)
  }

  return apiFetch<PagedResponse<Order>>(`/orders?${query}`, { signal })
}

export function getOrder(id: string, signal?: AbortSignal) {
  return apiFetch<OrderDetails>(`/orders/${encodeURIComponent(id)}`, { signal })
}

export function createOrder(input: CreateOrderInput) {
  return apiFetch<Order>('/orders', { method: 'POST', body: JSON.stringify(input) })
}
