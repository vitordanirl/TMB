import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AssistantPage } from './AssistantPage'

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })

function setup(routes: Record<string, () => Response>) {
  const fetchMock = vi.fn((url: string) => {
    const route = Object.keys(routes).find((path) => url.endsWith(path))
    return Promise.resolve(route ? routes[route]!() : json(404, {}))
  })
  vi.stubGlobal('fetch', fetchMock)

  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
      <AssistantPage />
    </QueryClientProvider>,
  )

  return { user: userEvent.setup(), fetchMock }
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('AssistantPage', () => {
  it('orienta a configurar a chave quando o módulo está desabilitado', async () => {
    setup({ '/ai/status': () => json(200, { habilitado: false, modelo: null }) })

    expect(await screen.findByText('Módulo de IA não configurado')).toBeInTheDocument()
    expect(screen.queryByLabelText('Sua pergunta')).not.toBeInTheDocument()
  })

  it('envia uma pergunta de exemplo e mostra a resposta com as consultas realizadas', async () => {
    const { user, fetchMock } = setup({
      '/ai/status': () => json(200, { habilitado: true, modelo: 'claude-opus-5-5' }),
      '/ai/ask': () =>
        json(200, {
          resposta: 'Há 3 pedidos pendentes.',
          consultas: [{ ferramenta: 'contar_pedidos', argumentos: { status: 'Pendente' }, erro: false }],
        }),
    })

    await user.click(await screen.findByRole('button', { name: 'Quantos pedidos estão pendentes?' }))

    expect(await screen.findByText('Há 3 pedidos pendentes.')).toBeInTheDocument()
    expect(screen.getByText('Quantos pedidos estão pendentes?')).toBeInTheDocument()
    expect(screen.getByText('1 consulta realizada')).toBeInTheDocument()
    expect(screen.getByText('Contagem de pedidos · Pendente')).toBeInTheDocument()

    const [, init] = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/ai/ask'))! as unknown as [string, RequestInit]
    expect(JSON.parse(init.body as string)).toEqual({ pergunta: 'Quantos pedidos estão pendentes?' })
  })

  it('mostra mensagem amigável quando o limite de perguntas é atingido', async () => {
    const { user } = setup({
      '/ai/status': () => json(200, { habilitado: true, modelo: 'claude-opus-5-5' }),
      '/ai/ask': () => json(429, { title: 'Too Many Requests', status: 429 }),
    })

    await user.type(await screen.findByLabelText('Sua pergunta'), 'Quantos pedidos hoje?{Enter}')

    expect(await screen.findByRole('alert')).toHaveTextContent('Muitas perguntas em pouco tempo')
  })
})
