import { ArrowLeft } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '@/api/client'
import { ErrorState } from '@/components/ErrorState'
import { buttonClasses } from '@/components/ui/styles'
import { Card } from '@/components/ui/Card'
import { Skeleton } from '@/components/ui/Skeleton'
import { StatusBadge } from '@/features/orders/components/StatusBadge'
import { StatusTimeline } from '@/features/orders/components/StatusTimeline'
import { useOrder } from '@/features/orders/queries'
import { formatCurrency, formatDateTime, formatDuration, shortId } from '@/lib/format'

export function OrderDetailsPage() {
  const { id = '' } = useParams()
  const { data: order, isPending, isError, error, refetch } = useOrder(id)

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <Link
        to="/pedidos"
        className="inline-flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-800 dark:hover:text-slate-200"
      >
        <ArrowLeft aria-hidden className="size-4" />
        Voltar para pedidos
      </Link>

      {isPending ? (
        <DetailsSkeleton />
      ) : isError ? (
        <Card>
          {error instanceof ApiError && error.isNotFound ? (
            <ErrorState
              title="Pedido não encontrado"
              description="Verifique o endereço ou volte para a lista de pedidos."
              action={
                <Link to="/pedidos" className={buttonClasses('secondary', 'sm')}>
                  Ver pedidos
                </Link>
              }
            />
          ) : (
            <ErrorState title="Não foi possível carregar o pedido" description={error.message} onRetry={() => refetch()} />
          )}
        </Card>
      ) : (
        <>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0">
              <p className="font-mono text-xs text-slate-500">Pedido #{shortId(order.id)}</p>
              <h1 className="truncate text-2xl font-semibold tracking-tight">{order.produto}</h1>
            </div>
            <StatusBadge status={order.status} size="lg" className="self-start sm:self-auto" />
          </div>

          <div className="grid gap-6 md:grid-cols-5">
            <Card className="p-6 md:col-span-2">
              <h2 className="text-sm font-semibold">Dados do pedido</h2>
              <dl className="mt-4 space-y-4 text-sm">
                <Detail label="Cliente">{order.cliente}</Detail>
                <Detail label="Produto">{order.produto}</Detail>
                <Detail label="Valor">
                  <span className="text-base font-semibold tabular-nums">{formatCurrency(order.valor)}</span>
                </Detail>
                <Detail label="Criado em">{formatDateTime(order.data_criacao)}</Detail>
                <Detail label="Atualizado em">{formatDateTime(order.data_atualizacao)}</Detail>
                {order.status === 'Finalizado' && (
                  <Detail label="Tempo total">{formatDuration(order.data_criacao, order.data_atualizacao)}</Detail>
                )}
                <Detail label="ID">
                  <span className="font-mono text-xs break-all text-slate-500">{order.id}</span>
                </Detail>
              </dl>
            </Card>

            <Card className="p-6 md:col-span-3">
              <h2 className="text-sm font-semibold">Linha do tempo</h2>
              <div className="mt-5">
                <StatusTimeline order={order} />
              </div>
            </Card>
          </div>
        </>
      )}
    </div>
  )
}

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="mt-0.5 break-words">{children}</dd>
    </div>
  )
}

function DetailsSkeleton() {
  return (
    <div className="space-y-6" aria-label="Carregando pedido">
      <div className="space-y-2">
        <Skeleton className="h-3 w-24" />
        <Skeleton className="h-7 w-64" />
      </div>
      <div className="grid gap-6 md:grid-cols-5">
        <Skeleton className="h-72 md:col-span-2" />
        <Skeleton className="h-72 md:col-span-3" />
      </div>
    </div>
  )
}
