import { Check, Loader2 } from 'lucide-react'
import { ORDER_STATUSES, type OrderDetails, type OrderStatus } from '@/api/types'
import { formatDateTime, formatDuration } from '@/lib/format'
import { cn } from '@/lib/utils'

const descriptions: Record<OrderStatus, string> = {
  Pendente: 'Pedido recebido e aguardando processamento.',
  Processando: 'O worker está processando o pedido.',
  Finalizado: 'Processamento concluído.',
}

type StepState = 'done' | 'current' | 'upcoming'

/** Linha do tempo Pendente → Processando → Finalizado, baseada no histórico de status. */
export function StatusTimeline({ order }: { order: OrderDetails }) {
  const currentIndex = ORDER_STATUSES.indexOf(order.status)

  return (
    <ol className="space-y-0" aria-label="Histórico de status">
      {ORDER_STATUSES.map((status, index) => {
        const entry = order.historico.find((h) => h.status_novo === status)
        const previous = index > 0 ? order.historico.find((h) => h.status_novo === ORDER_STATUSES[index - 1]) : undefined
        const state: StepState =
          index < currentIndex || order.status === 'Finalizado' ? 'done' : index === currentIndex ? 'current' : 'upcoming'
        const isLast = index === ORDER_STATUSES.length - 1

        return (
          <li key={status} className="relative flex gap-4 pb-8 last:pb-0" data-state={state}>
            {!isLast && (
              <span
                aria-hidden
                className={cn(
                  'absolute top-8 left-4 -ml-px h-[calc(100%-2rem)] w-0.5 transition-colors duration-500',
                  state === 'done' ? 'bg-emerald-500' : 'bg-slate-200 dark:bg-slate-800',
                )}
              />
            )}

            <StepIcon state={state} />

            <div className="min-w-0 flex-1 pt-1">
              <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
                <p
                  className={cn(
                    'font-medium',
                    state === 'upcoming' ? 'text-slate-400 dark:text-slate-500' : 'text-slate-900 dark:text-slate-100',
                  )}
                >
                  {status}
                </p>
                {entry && (
                  <time dateTime={entry.ocorrido_em} className="text-xs text-slate-500 tabular-nums">
                    {formatDateTime(entry.ocorrido_em)}
                  </time>
                )}
              </div>
              <p className="mt-0.5 text-sm text-slate-500 dark:text-slate-400">{descriptions[status]}</p>
              {entry && previous && (
                <p className="mt-1 text-xs text-slate-500">
                  {formatDuration(previous.ocorrido_em, entry.ocorrido_em)} após "{previous.status_novo}"
                </p>
              )}
            </div>
          </li>
        )
      })}
    </ol>
  )
}

function StepIcon({ state }: { state: StepState }) {
  return (
    <span
      className={cn(
        'relative z-10 flex size-8 shrink-0 items-center justify-center rounded-full ring-4 ring-white transition-colors duration-500 dark:ring-slate-900',
        state === 'done' && 'bg-emerald-500 text-white',
        state === 'current' && 'bg-sky-500 text-white',
        state === 'upcoming' && 'bg-slate-100 text-slate-400 dark:bg-slate-800',
      )}
    >
      {state === 'done' && <Check aria-hidden className="size-4" />}
      {state === 'current' && <Loader2 aria-hidden className="size-4 animate-spin" />}
      {state === 'upcoming' && <span aria-hidden className="size-2 rounded-full bg-current" />}
    </span>
  )
}
