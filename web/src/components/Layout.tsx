import { Package, Sparkles } from 'lucide-react'
import { Link, NavLink, Outlet } from 'react-router'
import { Toaster } from 'sonner'
import { useOrderStatusNotifications } from '@/features/orders/useOrderStatusNotifications'
import { cn } from '@/lib/utils'
import { RealtimeIndicator } from '@/realtime/RealtimeIndicator'

const navLinkClasses = ({ isActive }: { isActive: boolean }) =>
  cn(
    'inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
    isActive
      ? 'bg-slate-100 text-slate-900 dark:bg-slate-800 dark:text-white'
      : 'text-slate-500 hover:text-slate-900 dark:hover:text-white',
  )

export function Layout() {
  useOrderStatusNotifications()

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/80 backdrop-blur dark:border-slate-800 dark:bg-slate-950/80">
        <div className="mx-auto flex h-14 max-w-6xl items-center gap-4 px-4 sm:px-6">
          <Link to="/pedidos" className="flex items-center gap-2 font-semibold" aria-label="Gestão de Pedidos">
            <span className="flex size-7 items-center justify-center rounded-lg bg-indigo-600 text-white">
              <Package aria-hidden className="size-4" />
            </span>
            <span className="hidden sm:inline">Gestão de Pedidos</span>
          </Link>

          <nav aria-label="Principal" className="flex items-center gap-1">
            <NavLink to="/pedidos" className={navLinkClasses}>
              Pedidos
            </NavLink>
            <NavLink to="/assistente" className={navLinkClasses}>
              <Sparkles aria-hidden className="size-4" />
              <span>
                Pergunte <span className="hidden sm:inline">à IA</span>
              </span>
            </NavLink>
          </nav>

          <div className="ml-auto">
            <RealtimeIndicator />
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
        <Outlet />
      </main>

      <Toaster richColors closeButton position="bottom-right" />
    </div>
  )
}
