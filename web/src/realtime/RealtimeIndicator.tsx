import { FALLBACK_POLL_INTERVAL_MS } from '@/features/orders/queries'
import { cn } from '@/lib/utils'
import { useRealtimeStatus, type RealtimeStatus } from './RealtimeContext'

const fallbackSeconds = FALLBACK_POLL_INTERVAL_MS / 1000

const variants: Record<RealtimeStatus, { label: string; title: string; dot: string; text: string }> = {
  connected: {
    label: 'Tempo real',
    title: 'Conectado: os status são atualizados assim que mudam.',
    dot: 'bg-emerald-500',
    text: 'text-emerald-700 dark:text-emerald-400',
  },
  connecting: {
    label: 'Conectando…',
    title: 'Estabelecendo conexão em tempo real.',
    dot: 'bg-slate-400 animate-pulse',
    text: 'text-slate-500',
  },
  reconnecting: {
    label: 'Reconectando…',
    title: `Conexão em tempo real perdida. Atualizando a cada ${fallbackSeconds} s enquanto reconecta.`,
    dot: 'bg-amber-500 animate-pulse',
    text: 'text-amber-700 dark:text-amber-400',
  },
  disconnected: {
    label: 'Atualização periódica',
    title: `Sem conexão em tempo real. Atualizando a cada ${fallbackSeconds} s; tentando reconectar.`,
    dot: 'bg-amber-500',
    text: 'text-amber-700 dark:text-amber-400',
  },
}

/** Indica se as atualizações chegam em tempo real ou via polling (fallback). */
export function RealtimeIndicator() {
  const status = useRealtimeStatus()
  const variant = variants[status]

  return (
    <span
      role="status"
      title={variant.title}
      data-status={status}
      className={cn('inline-flex items-center gap-2 text-xs font-medium', variant.text)}
    >
      <span className="relative flex size-2">
        {status === 'connected' && (
          <span aria-hidden className="absolute inline-flex size-full animate-ping rounded-full bg-emerald-400 opacity-60" />
        )}
        <span aria-hidden className={cn('relative inline-flex size-2 rounded-full', variant.dot)} />
      </span>
      <span>{variant.label}</span>
      <span className="sr-only">{variant.title}</span>
    </span>
  )
}
