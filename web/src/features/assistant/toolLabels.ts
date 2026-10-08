import type { ToolCall } from '@/api/ai'

const toolNames: Record<string, string> = {
  contar_pedidos: 'Contagem de pedidos',
  somar_valor_pedidos: 'Soma dos valores',
  tempo_medio_processamento: 'Tempo médio de processamento',
  listar_pedidos: 'Lista de pedidos',
}

const formatDate = (value: unknown) =>
  typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value.split('-').reverse().join('/') : String(value)

/** Descrição legível de uma consulta feita pela IA (ex.: "Contagem de pedidos · Pendente · 01/10/2026 a 31/10/2026"). */
export function describeToolCall({ ferramenta, argumentos }: ToolCall): string {
  const parts = [toolNames[ferramenta] ?? ferramenta]
  const { status, data_inicio: start, data_fim: end, limite } = argumentos

  if (typeof status === 'string') {
    parts.push(status)
  }

  if (start && end) {
    parts.push(start === end ? formatDate(start) : `${formatDate(start)} a ${formatDate(end)}`)
  } else if (start) {
    parts.push(`desde ${formatDate(start)}`)
  } else if (end) {
    parts.push(`até ${formatDate(end)}`)
  }

  if (typeof limite === 'number') {
    parts.push(`até ${limite} itens`)
  }

  return parts.join(' · ')
}
