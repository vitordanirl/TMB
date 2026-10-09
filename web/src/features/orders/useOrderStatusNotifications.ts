import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useNavigate } from 'react-router'
import { toast } from 'sonner'
import type { Order, OrderStatus } from '@/api/types'
import { shortId } from '@/lib/format'
import { orderKeys } from './queries'

/**
 * Observa o cache do TanStack Query e notifica (toast) quando um pedido muda de status,
 * independentemente da origem da atualização (polling, refetch ou eventos em tempo real)
 * e da página aberta. O primeiro status conhecido de cada pedido é registrado sem notificar.
 */
export function useOrderStatusNotifications() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  useEffect(() => {
    const cache = queryClient.getQueryCache()
    const knownStatuses = new Map<string, OrderStatus>()

    const track = (data: unknown, notify: boolean) => {
      for (const order of extractOrders(data)) {
        const previous = knownStatuses.get(order.id)
        knownStatuses.set(order.id, order.status)

        if (notify && previous && previous !== order.status) {
          showStatusToast(order, () => navigate(`/pedidos/${order.id}`))
        }
      }
    }

    for (const query of cache.findAll({ queryKey: orderKeys.all })) {
      track(query.state.data, false)
    }

    return cache.subscribe((event) => {
      if (event.type === 'updated' && event.action.type === 'success' && event.query.queryKey[0] === orderKeys.all[0]) {
        track(event.query.state.data, true)
      }
    })
  }, [queryClient, navigate])
}

function showStatusToast(order: Order, onView: () => void) {
  const description = `#${shortId(order.id)} · ${order.cliente} — ${order.produto}`
  const options = {
    id: order.id, // um toast por pedido: "Processando" é substituído por "Finalizado"
    description,
    action: { label: 'Ver', onClick: onView },
  }

  if (order.status === 'Finalizado') {
    toast.success('Pedido finalizado', options)
  } else {
    toast.info(`Pedido ${order.status.toLowerCase()}`, options)
  }
}

function extractOrders(data: unknown): Order[] {
  if (!data || typeof data !== 'object') {
    return []
  }

  if ('items' in data && Array.isArray(data.items)) {
    return data.items as Order[]
  }

  return 'id' in data && 'status' in data ? [data as Order] : []
}
