import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createOrder, getOrder, listOrders, type ListOrdersParams } from '@/api/orders'
import type { OrderStatus } from '@/api/types'

export const orderKeys = {
  all: ['orders'] as const,
  lists: () => [...orderKeys.all, 'list'] as const,
  list: (params: ListOrdersParams) => [...orderKeys.lists(), params] as const,
  details: () => [...orderKeys.all, 'detail'] as const,
  detail: (id: string) => [...orderKeys.details(), id] as const,
}

/** Intervalo de polling enquanto houver pedidos ainda em andamento. */
export const ACTIVE_ORDERS_POLL_INTERVAL_MS = 2_000

const isInProgress = (status: OrderStatus) => status !== 'Finalizado'

export function useOrdersList(params: ListOrdersParams) {
  return useQuery({
    queryKey: orderKeys.list(params),
    queryFn: ({ signal }) => listOrders(params, signal),
    placeholderData: keepPreviousData,
    refetchInterval: (query) =>
      query.state.data?.items.some((order) => isInProgress(order.status)) ? ACTIVE_ORDERS_POLL_INTERVAL_MS : false,
  })
}

export function useOrder(id: string) {
  return useQuery({
    queryKey: orderKeys.detail(id),
    queryFn: ({ signal }) => getOrder(id, signal),
    refetchInterval: (query) =>
      query.state.data && isInProgress(query.state.data.status) ? ACTIVE_ORDERS_POLL_INTERVAL_MS : false,
  })
}

export function useCreateOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: createOrder,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: orderKeys.lists() }),
  })
}
