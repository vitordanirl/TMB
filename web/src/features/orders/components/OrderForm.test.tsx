import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/api/client'
import { OrderForm } from './OrderForm'

function setup(onSubmit = vi.fn().mockResolvedValue(undefined)) {
  const user = userEvent.setup()
  render(<OrderForm onSubmit={onSubmit} />)

  return {
    user,
    onSubmit,
    cliente: screen.getByLabelText('Cliente'),
    produto: screen.getByLabelText('Produto'),
    valor: screen.getByLabelText('Valor'),
    submit: screen.getByRole('button', { name: 'Criar pedido' }),
  }
}

describe('OrderForm', () => {
  it('exibe erros de validação e não envia com campos vazios', async () => {
    const { user, onSubmit, submit } = setup()

    await user.click(submit)

    expect(await screen.findByText('Informe o cliente.')).toBeInTheDocument()
    expect(screen.getByText('Informe o produto.')).toBeInTheDocument()
    expect(screen.getByText('Informe o valor.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('envia os valores normalizados, com o valor digitado em centavos', async () => {
    const { user, onSubmit, cliente, produto, valor, submit } = setup()

    await user.type(cliente, '  Maria Silva ')
    await user.type(produto, 'Notebook')
    await user.type(valor, '459990')
    expect(valor).toHaveValue('R$ 4.599,90')

    await user.click(submit)

    expect(onSubmit).toHaveBeenCalledWith({ cliente: 'Maria Silva', produto: 'Notebook', valor: 4599.9 })
  })

  it('mostra erros de validação retornados pela API nos campos', async () => {
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiError(400, { title: 'Inválido', errors: { cliente: ['Cliente bloqueado.'] } }),
    )
    const { user, cliente, produto, valor, submit } = setup(onSubmit)

    await user.type(cliente, 'Fulano')
    await user.type(produto, 'Mouse')
    await user.type(valor, '1000')
    await user.click(submit)

    expect(await screen.findByText('Cliente bloqueado.')).toBeInTheDocument()
    expect(cliente).toHaveAttribute('aria-invalid', 'true')
  })

  it('mostra erro geral quando a API falha sem detalhes por campo', async () => {
    const { user, cliente, produto, valor, submit } = setup(
      vi.fn().mockRejectedValue(new ApiError(0, { title: 'Não foi possível conectar à API.' })),
    )

    await user.type(cliente, 'Fulano')
    await user.type(produto, 'Mouse')
    await user.type(valor, '1000')
    await user.click(submit)

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível conectar à API.')
  })
})
