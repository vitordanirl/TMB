import { Link } from 'react-router'
import { ErrorState } from '@/components/ErrorState'
import { buttonClasses } from '@/components/ui/styles'
import { Card } from '@/components/ui/Card'

export function NotFoundPage() {
  return (
    <Card className="mx-auto max-w-xl">
      <ErrorState
        title="Página não encontrada"
        description="O endereço acessado não existe."
        action={
          <Link to="/pedidos" className={buttonClasses('secondary', 'sm')}>
            Ir para pedidos
          </Link>
        }
      />
    </Card>
  )
}
