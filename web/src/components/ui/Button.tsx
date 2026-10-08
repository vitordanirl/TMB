import type { ButtonHTMLAttributes } from 'react'
import { buttonClasses, type ButtonSize, type ButtonVariant } from './styles'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
}

export function Button({ variant, size, className, type = 'button', ...props }: ButtonProps) {
  return <button type={type} className={buttonClasses(variant, size, className)} {...props} />
}
