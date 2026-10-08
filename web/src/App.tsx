import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router'
import { ApiError } from '@/api/client'
import { Layout } from '@/components/Layout'
import { NewOrderPage } from '@/pages/NewOrderPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { OrderDetailsPage } from '@/pages/OrderDetailsPage'
import { OrdersPage } from '@/pages/OrdersPage'
import { RealtimeProvider } from '@/realtime/RealtimeProvider'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5_000,
      // Não insiste em erros do cliente (ex.: 404); tenta de novo em falhas de rede/servidor.
      retry: (failureCount, error) =>
        !(error instanceof ApiError && error.status >= 400 && error.status < 500) && failureCount < 2,
    },
  },
})

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { index: true, element: <Navigate to="/pedidos" replace /> },
      { path: 'pedidos', element: <OrdersPage /> },
      { path: 'pedidos/novo', element: <NewOrderPage /> },
      { path: 'pedidos/:id', element: <OrderDetailsPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RealtimeProvider>
        <RouterProvider router={router} />
      </RealtimeProvider>
    </QueryClientProvider>
  )
}
