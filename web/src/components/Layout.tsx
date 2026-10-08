import { Package } from 'lucide-react'
import { Link, Outlet } from 'react-router'
import { Toaster } from 'sonner'
import { useOrderStatusNotifications } from '@/features/orders/useOrderStatusNotifications'

export function Layout() {
  useOrderStatusNotifications()

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/80 backdrop-blur dark:border-slate-800 dark:bg-slate-950/80">
        <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4 sm:px-6">
          <Link to="/pedidos" className="flex items-center gap-2 font-semibold">
            <span className="flex size-7 items-center justify-center rounded-lg bg-indigo-600 text-white">
              <Package aria-hidden className="size-4" />
            </span>
            Gestão de Pedidos
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
        <Outlet />
      </main>

      <Toaster richColors closeButton position="bottom-right" />
    </div>
  )
}
