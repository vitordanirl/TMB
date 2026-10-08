import { useId, type InputHTMLAttributes, type ReactNode, type Ref } from 'react'
import { cn } from '@/lib/utils'
import { inputClasses } from './styles'

interface FormFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
  hint?: ReactNode
  ref?: Ref<HTMLInputElement>
}

/** Input com label, mensagem de erro e atributos de acessibilidade ligados. */
export function FormField({ label, error, hint, className, id, ...props }: FormFieldProps) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const errorId = `${inputId}-error`

  return (
    <div className="space-y-1.5">
      <label htmlFor={inputId} className="block text-sm font-medium text-slate-700 dark:text-slate-300">
        {label}
      </label>
      <input
        id={inputId}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className={cn(inputClasses, className)}
        {...props}
      />
      {error ? (
        <p id={errorId} role="alert" className="text-sm text-rose-600 dark:text-rose-400">
          {error}
        </p>
      ) : (
        hint && <p className="text-xs text-slate-500">{hint}</p>
      )}
    </div>
  )
}
