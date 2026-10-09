import { CheckCircle2, Clock, Loader2 } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import type { OrderStatus } from '@/api/types'
import { cn } from '@/lib/utils'

const styles: Record<OrderStatus, { className: string; icon: typeof Clock }> = {
  Pendente: {
    className: 'bg-amber-50 text-amber-800 ring-amber-600/25 dark:bg-amber-400/10 dark:text-amber-300 dark:ring-amber-400/30',
    icon: Clock,
  },
  Processando: {
    className: 'bg-sky-50 text-sky-800 ring-sky-600/25 dark:bg-sky-400/10 dark:text-sky-300 dark:ring-sky-400/30',
    icon: Loader2,
  },
  Finalizado: {
    className:
      'bg-emerald-50 text-emerald-800 ring-emerald-600/25 dark:bg-emerald-400/10 dark:text-emerald-300 dark:ring-emerald-400/30',
    icon: CheckCircle2,
  },
}

interface StatusBadgeProps {
  status: OrderStatus
  size?: 'sm' | 'lg'
  className?: string
}

/** Badge de status. Pisca/destaca quando o status muda, como feedback visual da transição. */
export function StatusBadge({ status, size = 'sm', className }: StatusBadgeProps) {
  const changed = useStatusChanged(status)
  const { className: tone, icon: Icon } = styles[status]

  return (
    <span
      data-status={status}
      data-changed={changed || undefined}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full font-medium whitespace-nowrap ring-1 ring-inset',
        size === 'sm' ? 'px-2.5 py-0.5 text-xs' : 'px-3 py-1 text-sm',
        tone,
        changed && 'animate-status-flash',
        className,
      )}
    >
      <Icon aria-hidden className={cn(size === 'sm' ? 'size-3.5' : 'size-4', status === 'Processando' && 'animate-spin')} />
      {status}
    </span>
  )
}

/** true por ~1,6 s após o status mudar (não na primeira renderização). */
function useStatusChanged(status: OrderStatus): boolean {
  const previous = useRef(status)
  const [changed, setChanged] = useState(false)

  useEffect(() => {
    if (previous.current === status) {
      return
    }

    previous.current = status
    setChanged(true)
    const timeout = setTimeout(() => setChanged(false), 1600)
    return () => clearTimeout(timeout)
  }, [status])

  return changed
}
