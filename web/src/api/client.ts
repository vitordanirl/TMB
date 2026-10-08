import type { ProblemDetails } from './types'

/** Base da API. Em produção o nginx encaminha /api para o backend; em dev, o proxy do Vite. */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '/api'

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | undefined

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.detail ?? problem?.title ?? `Erro ${status} ao chamar a API.`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  get isNotFound(): boolean {
    return this.status === 404
  }

  /** Erros de validação por campo (chaves no formato do contrato, ex.: "cliente"). */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {}
  }
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response

  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: {
        Accept: 'application/json',
        ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
        ...init?.headers,
      },
    })
  } catch {
    throw new ApiError(0, { title: 'Não foi possível conectar à API. Verifique sua conexão.' })
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }

  return (await response.json()) as T
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('json')) {
    return undefined
  }

  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return undefined
  }
}
