import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StatusBadge } from './StatusBadge'

describe('StatusBadge', () => {
  it('não destaca na primeira renderização', () => {
    render(<StatusBadge status="Pendente" />)

    expect(screen.getByText('Pendente')).not.toHaveAttribute('data-changed')
  })

  it('destaca quando o status muda', () => {
    const { rerender } = render(<StatusBadge status="Pendente" />)

    rerender(<StatusBadge status="Processando" />)

    expect(screen.getByText('Processando')).toHaveAttribute('data-changed', 'true')
  })
})
