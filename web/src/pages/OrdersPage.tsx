import { ChevronLeft, ChevronRight, Inbox, Plus } from 'lucide-react'
import { Link, useSearchParams } from 'react-router'
import { ORDER_STATUSES, type OrderStatus } from '@/api/types'
import { ErrorState } from '@/components/ErrorState'
import { Button } from '@/components/ui/Button'
import { buttonClasses } from '@/components/ui/styles'
import { Card } from '@/components/ui/Card'
import { Skeleton } from '@/components/ui/Skeleton'
import { OrdersList } from '@/features/orders/components/OrdersList'
import { useOrdersList } from '@/features/orders/queries'
import { cn } from '@/lib/utils'

const PAGE_SIZE = 10

export function OrdersPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const status = parseStatus(searchParams.get('status'))
  const page = Math.max(1, Number(searchParams.get('page')) || 1)

  const { data, isPending, isError, error, refetch, isPlaceholderData } = useOrdersList({
    page,
    pageSize: PAGE_SIZE,
    status,
  })

  const totalPages = data ? Math.max(1, Math.ceil(data.total_count / PAGE_SIZE)) : 1

  const updateParams = (next: { status?: OrderStatus; page?: number }) => {
    const params = new URLSearchParams()
    if (next.status) params.set('status', next.status)
    if (next.page && next.page > 1) params.set('page', String(next.page))
    setSearchParams(params)
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Pedidos</h1>
          <p className="mt-1 text-sm text-slate-500">
            Acompanhe os pedidos e o status de processamento em tempo real.
          </p>
        </div>
        <Link to="/pedidos/novo" className={buttonClasses('primary', 'md', 'w-full sm:w-auto')}>
          <Plus aria-hidden className="size-4" />
          Novo pedido
        </Link>
      </div>

      <StatusFilter value={status} onChange={(next) => updateParams({ status: next })} />

      <Card className="overflow-hidden">
        {isPending ? (
          <ListSkeleton />
        ) : isError ? (
          <ErrorState title="Não foi possível carregar os pedidos" description={error.message} onRetry={() => refetch()} />
        ) : data.items.length === 0 ? (
          <EmptyState filtered={Boolean(status)} />
        ) : (
          <div className={cn('transition-opacity', isPlaceholderData && 'opacity-60')}>
            <OrdersList orders={data.items} />
          </div>
        )}

        {data && data.total_count > 0 && (
          <nav
            aria-label="Paginação"
            className="flex items-center justify-between gap-4 border-t border-slate-200 px-4 py-3 text-sm dark:border-slate-800"
          >
            <p className="text-slate-500">
              {data.total_count} {data.total_count === 1 ? 'pedido' : 'pedidos'} · página {page} de {totalPages}
            </p>
            <div className="flex gap-2">
              <Button
                variant="secondary"
                size="sm"
                disabled={page <= 1}
                onClick={() => updateParams({ status, page: page - 1 })}
                aria-label="Página anterior"
              >
                <ChevronLeft aria-hidden className="size-4" />
              </Button>
              <Button
                variant="secondary"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => updateParams({ status, page: page + 1 })}
                aria-label="Próxima página"
              >
                <ChevronRight aria-hidden className="size-4" />
              </Button>
            </div>
          </nav>
        )}
      </Card>
    </div>
  )
}

function StatusFilter({ value, onChange }: { value?: OrderStatus; onChange: (status?: OrderStatus) => void }) {
  const options: { label: string; value?: OrderStatus }[] = [
    { label: 'Todos' },
    ...ORDER_STATUSES.map((status) => ({ label: status, value: status })),
  ]

  return (
    <div role="tablist" aria-label="Filtrar por status" className="flex gap-1 overflow-x-auto [scrollbar-width:none]">
      {options.map((option) => {
        const selected = option.value === value
        return (
          <button
            key={option.label}
            type="button"
            role="tab"
            aria-selected={selected}
            onClick={() => onChange(option.value)}
            className={cn(
              'rounded-lg px-3 py-1.5 text-sm font-medium whitespace-nowrap transition-colors',
              selected
                ? 'bg-white text-slate-900 shadow-sm ring-1 ring-slate-200 dark:bg-slate-800 dark:text-white dark:ring-slate-700'
                : 'text-slate-500 hover:text-slate-800 dark:hover:text-slate-200',
            )}
          >
            {option.label}
          </button>
        )
      })}
    </div>
  )
}

function ListSkeleton() {
  return (
    <div className="space-y-4 p-4" aria-label="Carregando pedidos">
      {Array.from({ length: 5 }, (_, index) => (
        <div key={index} className="flex items-center gap-4">
          <Skeleton className="h-4 w-16" />
          <Skeleton className="h-4 flex-1" />
          <Skeleton className="hidden h-4 w-24 sm:block" />
          <Skeleton className="h-6 w-24 rounded-full" />
        </div>
      ))}
    </div>
  )
}

function EmptyState({ filtered }: { filtered: boolean }) {
  return (
    <div className="flex flex-col items-center px-6 py-14 text-center">
      <span className="flex size-12 items-center justify-center rounded-full bg-slate-100 text-slate-400 dark:bg-slate-800">
        <Inbox aria-hidden className="size-6" />
      </span>
      <h2 className="mt-4 font-semibold">{filtered ? 'Nenhum pedido com esse status' : 'Nenhum pedido ainda'}</h2>
      <p className="mt-1 text-sm text-slate-500">
        {filtered ? 'Tente outro filtro.' : 'Crie o primeiro pedido para vê-lo ser processado.'}
      </p>
      {!filtered && (
        <Link to="/pedidos/novo" className={buttonClasses('primary', 'sm', 'mt-5')}>
          <Plus aria-hidden className="size-4" />
          Novo pedido
        </Link>
      )}
    </div>
  )
}

function parseStatus(value: string | null): OrderStatus | undefined {
  return ORDER_STATUSES.find((status) => status === value)
}
