import { apiFetch } from './client'

export interface AiStatus {
  habilitado: boolean
  modelo: string | null
}

export interface ToolCall {
  ferramenta: string
  argumentos: Record<string, unknown>
  erro: boolean
}

export interface AssistantAnswer {
  resposta: string
  consultas: ToolCall[]
}

/** O modelo pode fazer várias consultas antes de responder. */
const ASK_TIMEOUT_MS = 90_000

export function getAiStatus(signal?: AbortSignal) {
  return apiFetch<AiStatus>('/ai/status', { signal })
}

export function askAboutOrders(pergunta: string) {
  return apiFetch<AssistantAnswer>('/ai/ask', {
    method: 'POST',
    body: JSON.stringify({ pergunta }),
    timeoutMs: ASK_TIMEOUT_MS,
  })
}
