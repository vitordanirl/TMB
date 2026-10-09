import { ArrowLeft } from 'lucide-react'
import { Link, useNavigate } from 'react-router'
import { toast } from 'sonner'
import { Card } from '@/components/ui/Card'
import { OrderForm } from '@/features/orders/components/OrderForm'
import { useCreateOrder } from '@/features/orders/queries'
import { shortId } from '@/lib/format'

export function NewOrderPage() {
  const navigate = useNavigate()
  const createOrder = useCreateOrder()

  return (
    <div className="mx-auto max-w-xl space-y-6">
      <Link
        to="/pedidos"
        className="inline-flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-800 dark:hover:text-slate-200"
      >
        <ArrowLeft aria-hidden className="size-4" />
        Voltar para pedidos
      </Link>

      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Novo pedido</h1>
        <p className="mt-1 text-sm text-slate-500">
          Após criado, o pedido é processado de forma assíncrona: Pendente → Processando → Finalizado.
        </p>
      </div>

      <Card className="p-6">
        <OrderForm
          onCancel={() => navigate('/pedidos')}
          onSubmit={async (values) => {
            const order = await createOrder.mutateAsync(values)
            toast.success('Pedido criado', { description: `#${shortId(order.id)} · ${order.cliente}` })
            navigate(`/pedidos/${order.id}`)
          }}
        />
      </Card>
    </div>
  )
}
