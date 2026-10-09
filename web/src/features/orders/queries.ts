import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createOrder, getOrder, listOrders, type ListOrdersParams } from '@/api/orders'
import type { OrderStatus } from '@/api/types'
import { useRealtimeStatus, type RealtimeStatus } from '@/realtime/RealtimeContext'

export const orderKeys = {
  all: ['orders'] as const,
  lists: () => [...orderKeys.all, 'list'] as const,
  list: (params: ListOrdersParams) => [...orderKeys.lists(), params] as const,
  details: () => [...orderKeys.all, 'detail'] as const,
  detail: (id: string) => [...orderKeys.details(), id] as const,
}

/** Fallback sem tempo real: polling rápido enquanto houver pedidos em andamento. */
export const FALLBACK_POLL_INTERVAL_MS = 3_000

/** Fallback sem tempo real e sem pedidos em andamento: polling lento para descobrir pedidos novos. */
export const IDLE_FALLBACK_POLL_INTERVAL_MS = 15_000

const isInProgress = (status: OrderStatus) => status !== 'Finalizado'

/**
 * Com o SignalR conectado, as atualizações chegam por evento e não há polling.
 * Sem conexão (ou reconectando), o polling assume como fallback.
 */
export function pollInterval(realtime: RealtimeStatus, hasActiveOrders: boolean, pollWhenIdle: boolean): number | false {
  if (realtime === 'connected') {
    return false
  }

  if (hasActiveOrders) {
    return FALLBACK_POLL_INTERVAL_MS
  }

  return pollWhenIdle ? IDLE_FALLBACK_POLL_INTERVAL_MS : false
}

export function useOrdersList(params: ListOrdersParams) {
  const realtime = useRealtimeStatus()

  return useQuery({
    queryKey: orderKeys.list(params),
    queryFn: ({ signal }) => listOrders(params, signal),
    placeholderData: keepPreviousData,
    refetchInterval: (query) =>
      pollInterval(realtime, Boolean(query.state.data?.items.some((order) => isInProgress(order.status))), true),
  })
}

export function useOrder(id: string) {
  const realtime = useRealtimeStatus()

  return useQuery({
    queryKey: orderKeys.detail(id),
    queryFn: ({ signal }) => getOrder(id, signal),
    refetchInterval: (query) =>
      pollInterval(realtime, Boolean(query.state.data && isInProgress(query.state.data.status)), false),
  })
}

export function useCreateOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: createOrder,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: orderKeys.lists() }),
  })
}
