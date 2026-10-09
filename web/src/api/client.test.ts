import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiFetch } from './client'

afterEach(() => {
  vi.unstubAllGlobals()
})

const jsonResponse = (status: number, body: unknown, contentType = 'application/json') =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': contentType } })

describe('apiFetch', () => {
  it('retorna o JSON em respostas de sucesso', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(200, { ok: true })))

    await expect(apiFetch('/orders')).resolves.toEqual({ ok: true })
  })

  it('converte ProblemDetails em ApiError com erros por campo', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        jsonResponse(400, { title: 'Inválido', errors: { cliente: ['Campo obrigatório.'] } }, 'application/problem+json'),
      ),
    )

    const error = await apiFetch('/orders').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(400)
    expect((error as ApiError).fieldErrors).toEqual({ cliente: ['Campo obrigatório.'] })
  })

  it('identifica 404', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 404 })))

    const error = (await apiFetch('/orders/x').catch((e: unknown) => e)) as ApiError

    expect(error.isNotFound).toBe(true)
  })

  it('converte falha de rede em ApiError com status 0', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    const error = (await apiFetch('/orders').catch((e: unknown) => e)) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(0)
    expect(error.message).toMatch(/conectar à API/)
  })

  it('propaga o cancelamento solicitado pelo chamador sem convertê-lo', async () => {
    const controller = new AbortController()
    const abortError = new DOMException('Aborted', 'AbortError')
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(abortError))
    controller.abort()

    await expect(apiFetch('/orders', { signal: controller.signal })).rejects.toBe(abortError)
  })

  it('envia um sinal de timeout em toda requisição', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, {}))
    vi.stubGlobal('fetch', fetchMock)

    await apiFetch('/orders')

    expect((fetchMock.mock.calls[0]![1] as RequestInit).signal).toBeInstanceOf(AbortSignal)
  })
})
