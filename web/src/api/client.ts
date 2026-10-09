import type { ProblemDetails } from './types'

/** Base da API. Em produção o nginx encaminha /api para o backend; em dev, o proxy do Vite. */
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

/** Tempo máximo de uma chamada à API antes de desistir. */
export const REQUEST_TIMEOUT_MS = 10_000

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

export interface ApiFetchInit extends RequestInit {
  /** Sobrescreve o timeout padrão (ex.: respostas da IA, que levam mais tempo). */
  timeoutMs?: number
}

export async function apiFetch<T>(path: string, { timeoutMs = REQUEST_TIMEOUT_MS, ...init }: ApiFetchInit = {}): Promise<T> {
  let response: Response

  // Sem timeout, uma requisição pendurada (ex.: backend fora do ar) bloquearia o polling,
  // já que o TanStack Query não dispara um novo fetch enquanto o anterior não termina.
  const timeout = AbortSignal.timeout(timeoutMs)
  const signal = init?.signal ? AbortSignal.any([init.signal, timeout]) : timeout

  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      signal,
      headers: {
        Accept: 'application/json',
        ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
        ...init?.headers,
      },
    })
  } catch (error) {
    if (init?.signal?.aborted) {
      throw error // cancelamento solicitado pelo chamador (ex.: TanStack Query): propaga como está
    }

    throw new ApiError(0, {
      title: timeout.aborted
        ? 'A API demorou demais para responder. Tente novamente.'
        : 'Não foi possível conectar à API. Verifique sua conexão.',
    })
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
