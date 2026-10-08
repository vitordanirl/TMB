import { ChevronRight } from 'lucide-react'
import { Link, useNavigate } from 'react-router'
import type { Order } from '@/api/types'
import { formatCurrency, formatDateTime, shortId } from '@/lib/format'
import { StatusBadge } from './StatusBadge'

/** Tabela em telas médias/grandes; lista de cards no mobile. */
export function OrdersList({ orders }: { orders: Order[] }) {
  return (
    <>
      <OrdersTable orders={orders} />
      <OrdersCards orders={orders} />
    </>
  )
}

function OrdersTable({ orders }: { orders: Order[] }) {
  const navigate = useNavigate()

  return (
    <div className="hidden overflow-x-auto md:block">
      <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
        <thead>
          <tr className="text-left text-xs font-medium tracking-wide text-slate-500 uppercase">
            <th scope="col" className="px-4 py-3">Pedido</th>
            <th scope="col" className="px-4 py-3">Cliente</th>
            <th scope="col" className="px-4 py-3">Produto</th>
            <th scope="col" className="px-4 py-3 text-right">Valor</th>
            <th scope="col" className="px-4 py-3">Status</th>
            <th scope="col" className="px-4 py-3">Criado em</th>
            <th scope="col" className="px-4 py-3"><span className="sr-only">Abrir</span></th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800/70">
          {orders.map((order) => (
            <tr
              key={order.id}
              onClick={() => navigate(`/pedidos/${order.id}`)}
              className="animate-fade-in cursor-pointer transition-colors hover:bg-slate-50 dark:hover:bg-slate-800/50"
            >
              <td className="px-4 py-3 font-mono text-xs text-slate-500">#{shortId(order.id)}</td>
              <td className="max-w-48 truncate px-4 py-3 font-medium">{order.cliente}</td>
              <td className="max-w-56 truncate px-4 py-3 text-slate-600 dark:text-slate-300">{order.produto}</td>
              <td className="px-4 py-3 text-right tabular-nums">{formatCurrency(order.valor)}</td>
              <td className="px-4 py-3"><StatusBadge status={order.status} /></td>
              <td className="px-4 py-3 whitespace-nowrap text-slate-500 tabular-nums">{formatDateTime(order.data_criacao)}</td>
              <td className="px-4 py-3 text-right">
                <Link
                  to={`/pedidos/${order.id}`}
                  onClick={(event) => event.stopPropagation()}
                  className="inline-flex rounded p-1 text-slate-400 hover:text-indigo-600"
                  aria-label={`Ver pedido de ${order.cliente}`}
                >
                  <ChevronRight className="size-4" />
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function OrdersCards({ orders }: { orders: Order[] }) {
  return (
    <ul className="divide-y divide-slate-100 md:hidden dark:divide-slate-800">
      {orders.map((order) => (
        <li key={order.id} className="animate-fade-in">
          <Link
            to={`/pedidos/${order.id}`}
            className="flex items-start justify-between gap-3 px-4 py-4 active:bg-slate-50 dark:active:bg-slate-800/50"
          >
            <div className="min-w-0 space-y-1">
              <p className="truncate font-medium">{order.cliente}</p>
              <p className="truncate text-sm text-slate-600 dark:text-slate-300">{order.produto}</p>
              <p className="text-xs text-slate-500 tabular-nums">
                #{shortId(order.id)} · {formatDateTime(order.data_criacao)}
              </p>
            </div>
            <div className="flex shrink-0 flex-col items-end gap-2">
              <span className="font-medium tabular-nums">{formatCurrency(order.valor)}</span>
              <StatusBadge status={order.status} />
            </div>
          </Link>
        </li>
      ))}
    </ul>
  )
}
