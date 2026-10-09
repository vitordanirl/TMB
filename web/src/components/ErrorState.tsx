import { AlertTriangle, RotateCw } from 'lucide-react'
import type { ReactNode } from 'react'
import { Button } from './ui/Button'

interface ErrorStateProps {
  title: string
  description?: string
  onRetry?: () => void
  action?: ReactNode
}

export function ErrorState({ title, description, onRetry, action }: ErrorStateProps) {
  return (
    <div role="alert" className="flex flex-col items-center px-6 py-14 text-center">
      <span className="flex size-12 items-center justify-center rounded-full bg-rose-50 text-rose-600 dark:bg-rose-400/10 dark:text-rose-400">
        <AlertTriangle aria-hidden className="size-6" />
      </span>
      <h2 className="mt-4 font-semibold">{title}</h2>
      {description && <p className="mt-1 max-w-sm text-sm text-slate-500">{description}</p>}
      <div className="mt-5 flex gap-3">
        {onRetry && (
          <Button variant="secondary" size="sm" onClick={onRetry}>
            <RotateCw aria-hidden className="size-4" />
            Tentar novamente
          </Button>
        )}
        {action}
      </div>
    </div>
  )
}
