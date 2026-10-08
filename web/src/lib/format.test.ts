import { describe, expect, it } from 'vitest'
import { formatCurrency, formatDuration, parseCurrencyInput, shortId } from './format'

// Intl usa espaço não separável entre "R$" e o número.
const normalize = (text: string) => text.replace(/\s/g, ' ')

describe('formatCurrency', () => {
  it('formata em reais', () => {
    expect(normalize(formatCurrency(4599.9))).toBe('R$ 4.599,90')
  })
})

describe('parseCurrencyInput', () => {
  it.each([
    ['4599', 45.99],
    ['R$ 1.234,56', 1234.56],
    ['0', 0],
    ['0005', 0.05],
  ])('trata "%s" como centavos → %d', (input, expected) => {
    expect(parseCurrencyInput(input)).toBe(expected)
  })

  it('retorna undefined sem dígitos', () => {
    expect(parseCurrencyInput('R$ ')).toBeUndefined()
  })
})

describe('formatDuration', () => {
  it('usa segundos com uma casa abaixo de 1 minuto', () => {
    expect(formatDuration('2026-10-08T00:00:00.000Z', '2026-10-08T00:00:05.004Z')).toBe('5,0 s')
  })

  it('usa minutos e segundos acima de 1 minuto', () => {
    expect(formatDuration('2026-10-08T00:00:00Z', '2026-10-08T00:02:03Z')).toBe('2 min 3 s')
  })
})

describe('shortId', () => {
  it('retorna os 8 primeiros caracteres', () => {
    expect(shortId('01a11965-7ea4-729b-b590-4f3f5fc49019')).toBe('01a11965')
  })
})
