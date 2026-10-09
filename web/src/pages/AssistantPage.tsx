import { useMutation, useQuery } from '@tanstack/react-query'
import { AlertCircle, Database, KeyRound, Loader2, SendHorizonal, Sparkles } from 'lucide-react'
import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { askAboutOrders, getAiStatus, type ToolCall } from '@/api/ai'
import { ApiError } from '@/api/client'
import { Card } from '@/components/ui/Card'
import { Skeleton } from '@/components/ui/Skeleton'
import { buttonClasses } from '@/components/ui/styles'
import { describeToolCall } from '@/features/assistant/toolLabels'
import { cn } from '@/lib/utils'

const MAX_QUESTION_LENGTH = 500

const EXAMPLE_QUESTIONS = [
  'Quantos pedidos temos hoje?',
  'Qual o tempo médio para aprovar os pedidos?',
  'Quantos pedidos estão pendentes?',
  'Qual o valor total de pedidos finalizados este mês?',
]

type ChatMessage =
  | { id: number; role: 'user'; text: string }
  | { id: number; role: 'assistant'; text: string; consultas: ToolCall[] }
  | { id: number; role: 'error'; text: string }

// Omit distributivo: remove o "id" de cada variante, preservando a união discriminada.
type DistributiveOmit<T, K extends PropertyKey> = T extends unknown ? Omit<T, K> : never

type NewChatMessage = DistributiveOmit<ChatMessage, 'id'>

export function AssistantPage() {
  const status = useQuery({ queryKey: ['ai', 'status'], queryFn: ({ signal }) => getAiStatus(signal), staleTime: 60_000 })

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-semibold tracking-tight">
          <Sparkles aria-hidden className="size-6 text-indigo-500" />
          Pergunte sobre os pedidos
        </h1>
        <p className="mt-1 text-sm text-slate-500">
          Faça perguntas em linguagem natural. A IA consulta os dados reais antes de responder.
        </p>
      </div>

      {status.isPending ? (
        <Skeleton className="h-64" />
      ) : status.data?.habilitado ? (
        <Chat model={status.data.modelo} />
      ) : (
        <NotConfigured />
      )}
    </div>
  )
}

function Chat({ model }: { model: string | null }) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [question, setQuestion] = useState('')
  const nextId = useRef(0)
  const endRef = useRef<HTMLDivElement>(null)

  const ask = useMutation({
    mutationFn: askAboutOrders,
    onSuccess: (answer) =>
      append({ role: 'assistant', text: answer.resposta, consultas: answer.consultas }),
    onError: (error) => append({ role: 'error', text: errorMessage(error) }),
  })

  function append(message: NewChatMessage) {
    setMessages((current) => [...current, { ...message, id: nextId.current++ }])
  }

  function send(text: string) {
    const trimmed = text.trim()
    if (!trimmed || ask.isPending) {
      return
    }

    append({ role: 'user', text: trimmed })
    setQuestion('')
    ask.mutate(trimmed)
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    send(question)
  }

  function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      send(question)
    }
  }

  useEffect(() => {
    endRef.current?.scrollIntoView?.({ behavior: 'smooth', block: 'end' })
  }, [messages.length, ask.isPending])

  return (
    <Card className="flex flex-col">
      <div className="min-h-72 space-y-4 p-4 sm:p-6" aria-live="polite">
        {messages.length === 0 && !ask.isPending ? (
          <div className="py-6 text-center">
            <p className="text-sm text-slate-500">Experimente uma destas perguntas:</p>
            <div className="mt-4 flex flex-wrap justify-center gap-2">
              {EXAMPLE_QUESTIONS.map((example) => (
                <button
                  key={example}
                  type="button"
                  onClick={() => send(example)}
                  className="rounded-full bg-indigo-50 px-3 py-1.5 text-sm text-indigo-700 ring-1 ring-indigo-200 transition-colors hover:bg-indigo-100 dark:bg-indigo-400/10 dark:text-indigo-300 dark:ring-indigo-400/30 dark:hover:bg-indigo-400/20"
                >
                  {example}
                </button>
              ))}
            </div>
          </div>
        ) : (
          messages.map((message) => <MessageBubble key={message.id} message={message} />)
        )}

        {ask.isPending && (
          <div className="flex items-center gap-2 text-sm text-slate-500" role="status">
            <Loader2 aria-hidden className="size-4 animate-spin" />
            Consultando os pedidos…
          </div>
        )}
        <div ref={endRef} />
      </div>

      <form onSubmit={onSubmit} className="border-t border-slate-200 p-3 sm:p-4 dark:border-slate-800">
        <div className="flex items-end gap-2">
          <label htmlFor="question" className="sr-only">
            Sua pergunta
          </label>
          <textarea
            id="question"
            rows={1}
            value={question}
            maxLength={MAX_QUESTION_LENGTH}
            onChange={(event) => setQuestion(event.target.value)}
            onKeyDown={onKeyDown}
            placeholder="Ex.: Quantos pedidos foram finalizados esta semana?"
            className="block max-h-40 min-h-10 w-full resize-y rounded-lg border-0 bg-white px-3 py-2 text-sm shadow-sm ring-1 ring-slate-300 ring-inset placeholder:text-slate-400 focus:ring-2 focus:ring-indigo-600 focus:outline-none dark:bg-slate-950 dark:ring-slate-700"
          />
          <button
            type="submit"
            disabled={!question.trim() || ask.isPending}
            className={buttonClasses('primary', 'md', 'shrink-0')}
            aria-label="Enviar pergunta"
          >
            <SendHorizonal aria-hidden className="size-4" />
          </button>
        </div>
        <p className="mt-2 text-xs text-slate-400">
          Respostas geradas por IA{model ? ` (${model})` : ''} a partir dos dados do sistema. Enter envia, Shift+Enter quebra a linha.
        </p>
      </form>
    </Card>
  )
}

function MessageBubble({ message }: { message: ChatMessage }) {
  if (message.role === 'user') {
    return (
      <div className="animate-fade-in flex justify-end">
        <p className="max-w-[85%] rounded-2xl rounded-br-sm bg-indigo-600 px-4 py-2 text-sm whitespace-pre-wrap text-white">
          {message.text}
        </p>
      </div>
    )
  }

  if (message.role === 'error') {
    return (
      <div role="alert" className="animate-fade-in flex items-start gap-2 rounded-lg bg-rose-50 p-3 text-sm text-rose-700 dark:bg-rose-400/10 dark:text-rose-300">
        <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
        {message.text}
      </div>
    )
  }

  return (
    <div className="animate-fade-in flex gap-3">
      <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-indigo-100 text-indigo-600 dark:bg-indigo-400/15 dark:text-indigo-300">
        <Sparkles aria-hidden className="size-4" />
      </span>
      <div className="min-w-0 flex-1 space-y-2">
        <p className="text-sm leading-relaxed whitespace-pre-wrap">{message.text}</p>
        {message.consultas.length > 0 && (
          <details className="group text-xs text-slate-500">
            <summary className="inline-flex cursor-pointer list-none items-center gap-1.5 hover:text-slate-700 dark:hover:text-slate-300">
              <Database aria-hidden className="size-3.5" />
              {message.consultas.length === 1 ? '1 consulta realizada' : `${message.consultas.length} consultas realizadas`}
            </summary>
            <ul className="mt-2 space-y-1 border-l-2 border-slate-200 pl-3 dark:border-slate-700">
              {message.consultas.map((call, index) => (
                <li key={index} className={cn(call.erro && 'text-rose-600 dark:text-rose-400')}>
                  {describeToolCall(call)}
                  {call.erro && ' (parâmetros inválidos, corrigido pela IA)'}
                </li>
              ))}
            </ul>
          </details>
        )}
      </div>
    </div>
  )
}

function NotConfigured() {
  return (
    <Card className="p-6">
      <div className="flex gap-4">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-amber-50 text-amber-600 dark:bg-amber-400/10 dark:text-amber-400">
          <KeyRound aria-hidden className="size-5" />
        </span>
        <div className="space-y-2 text-sm">
          <h2 className="font-semibold">Módulo de IA não configurado</h2>
          <p className="text-slate-600 dark:text-slate-300">
            Para habilitar as perguntas, defina a chave da API da Anthropic no arquivo <code>.env</code> e reinicie a API:
          </p>
          <pre className="overflow-x-auto rounded-lg bg-slate-100 p-3 text-xs dark:bg-slate-800">
            {'ANTHROPIC_API_KEY=sk-ant-...\ndocker compose up -d api'}
          </pre>
        </div>
      </div>
    </Card>
  )
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 429) {
      return 'Muitas perguntas em pouco tempo. Aguarde um minuto e tente novamente.'
    }
    return error.problem?.detail ?? error.message
  }

  return 'Não foi possível obter uma resposta. Tente novamente.'
}
