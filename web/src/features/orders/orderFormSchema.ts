import { z } from 'zod'

const MAX_TEXT_LENGTH = 200
const MAX_VALUE = 999_999_999.99

/** Regras espelham as da API (cliente/produto até 200 caracteres, valor > 0). */
export const orderFormSchema = z.object({
  cliente: z
    .string()
    .trim()
    .min(1, 'Informe o cliente.')
    .max(MAX_TEXT_LENGTH, `Máximo de ${MAX_TEXT_LENGTH} caracteres.`),
  produto: z
    .string()
    .trim()
    .min(1, 'Informe o produto.')
    .max(MAX_TEXT_LENGTH, `Máximo de ${MAX_TEXT_LENGTH} caracteres.`),
  valor: z
    .number({ error: 'Informe o valor.' })
    .positive('O valor deve ser maior que zero.')
    .max(MAX_VALUE, 'Valor acima do limite permitido.'),
})

export type OrderFormInput = z.input<typeof orderFormSchema>

export type OrderFormValues = z.infer<typeof orderFormSchema>
