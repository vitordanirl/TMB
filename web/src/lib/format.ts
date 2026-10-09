const currencyFormatter = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

const dateTimeFormatter = new Intl.DateTimeFormat('pt-BR', {
  dateStyle: 'short',
  timeStyle: 'medium',
})

export function formatCurrency(value: number): string {
  return currencyFormatter.format(value)
}

export function formatDateTime(iso: string): string {
  return dateTimeFormatter.format(new Date(iso))
}

/** Duração legível entre dois instantes ISO (ex.: "5,0 s", "2 min 3 s"). */
export function formatDuration(fromIso: string, toIso: string): string {
  const ms = Math.max(0, new Date(toIso).getTime() - new Date(fromIso).getTime())

  if (ms < 60_000) {
    return `${(ms / 1000).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} s`
  }

  const totalSeconds = Math.round(ms / 1000)
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60

  return [hours && `${hours} h`, minutes && `${minutes} min`, seconds && `${seconds} s`].filter(Boolean).join(' ')
}

/** Identificador curto para exibição (primeiros 8 caracteres do UUID). */
export function shortId(id: string): string {
  return id.slice(0, 8)
}

/**
 * Converte o texto digitado num campo monetário em valor numérico, tratando os dígitos como centavos.
 * Ex.: "4599" → 45.99; "R$ 1.234,56" → 1234.56. Retorna undefined se não houver dígitos.
 */
export function parseCurrencyInput(text: string): number | undefined {
  const digits = text.replace(/\D/g, '').replace(/^0+(?=\d)/, '')
  if (digits.length === 0) {
    return undefined
  }

  return Number(digits) / 100
}
