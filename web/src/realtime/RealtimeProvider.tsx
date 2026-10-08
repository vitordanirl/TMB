import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useState, type ReactNode } from 'react'
import { orderKeys } from '@/features/orders/queries'
import { applyStatusChange } from '@/features/orders/realtimeCache'
import { HubEvents, type OrderStatusChangedEvent } from './events'
import { RealtimeContext, type RealtimeStatus } from './RealtimeContext'
import { retryDelayMs } from './retry'

const HUB_URL = import.meta.env.VITE_HUB_URL ?? '/hubs/orders'

/**
 * Mantém a conexão SignalR com o hub de pedidos e aplica os eventos no cache do TanStack Query.
 * Ao (re)conectar, invalida as queries de pedidos para recuperar eventos perdidos enquanto offline.
 */
export function RealtimeProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<RealtimeStatus>('connecting')

  useEffect(() => {
    let disposed = false
    let retryTimer: ReturnType<typeof setTimeout> | undefined

    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: ({ previousRetryCount }) => retryDelayMs(previousRetryCount) })
      .configureLogging(LogLevel.Warning)
      .build()

    const resync = () => queryClient.invalidateQueries({ queryKey: orderKeys.all })

    connection.on(HubEvents.OrderStatusChanged, (event: OrderStatusChangedEvent) => applyStatusChange(queryClient, event))
    connection.on(HubEvents.OrderCreated, () => queryClient.invalidateQueries({ queryKey: orderKeys.lists() }))

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(() => {
      setStatus('connected')
      void resync()
    })
    connection.onclose(() => {
      if (!disposed) {
        setStatus('disconnected')
        scheduleStart(0)
      }
    })

    // A reconexão automática do SignalR só cobre quedas após conectar; a primeira conexão
    // (ex.: API ainda subindo) é tentada aqui com o mesmo backoff.
    const start = async (attempt: number) => {
      try {
        await connection.start()
        if (disposed) {
          return
        }
        setStatus('connected')
        if (attempt > 0) {
          void resync()
        }
      } catch {
        if (!disposed) {
          setStatus('disconnected')
          scheduleStart(attempt + 1)
        }
      }
    }

    function scheduleStart(attempt: number) {
      clearTimeout(retryTimer)
      retryTimer = setTimeout(() => void start(attempt), retryDelayMs(attempt))
    }

    void start(0)

    return () => {
      disposed = true
      clearTimeout(retryTimer)
      void connection.stop()
    }
  }, [queryClient])

  return <RealtimeContext.Provider value={status}>{children}</RealtimeContext.Provider>
}
