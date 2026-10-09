import { describe, expect, it } from 'vitest'
import { describeToolCall } from './toolLabels'

describe('describeToolCall', () => {
  it.each([
    [{ ferramenta: 'contar_pedidos', argumentos: {} }, 'Contagem de pedidos'],
    [{ ferramenta: 'contar_pedidos', argumentos: { status: 'Pendente' } }, 'Contagem de pedidos · Pendente'],
    [
      { ferramenta: 'somar_valor_pedidos', argumentos: { status: 'Finalizado', data_inicio: '2026-10-01', data_fim: '2026-10-31' } },
      'Soma dos valores · Finalizado · 01/10/2026 a 31/10/2026',
    ],
    [{ ferramenta: 'tempo_medio_processamento', argumentos: { data_inicio: '2026-10-08', data_fim: '2026-10-08' } }, 'Tempo médio de processamento · 08/10/2026'],
    [{ ferramenta: 'listar_pedidos', argumentos: { limite: 5 } }, 'Lista de pedidos · até 5 itens'],
    [{ ferramenta: 'outra', argumentos: { data_inicio: '2026-10-01' } }, 'outra · desde 01/10/2026'],
  ])('descreve %j', (call, expected) => {
    expect(describeToolCall({ ...call, erro: false })).toBe(expected)
  })
})
