import { useId, type Ref } from 'react'
import { inputClasses } from '@/components/ui/styles'
import { formatCurrency, parseCurrencyInput } from '@/lib/format'
import { cn } from '@/lib/utils'

interface MoneyInputProps {
  label: string
  value: number | undefined
  onChange: (value: number | undefined) => void
  onBlur?: () => void
  error?: string
  name?: string
  ref?: Ref<HTMLInputElement>
}

/** Campo monetário em BRL: os dígitos digitados são tratados como centavos ("4599" → R$ 45,99). */
export function MoneyInput({ label, value, onChange, onBlur, error, name, ref }: MoneyInputProps) {
  const id = useId()
  const errorId = `${id}-error`

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-sm font-medium text-slate-700 dark:text-slate-300">
        {label}
      </label>
      <input
        ref={ref}
        id={id}
        name={name}
        inputMode="numeric"
        autoComplete="off"
        placeholder="R$ 0,00"
        value={value === undefined ? '' : formatCurrency(value)}
        onChange={(event) => onChange(parseCurrencyInput(event.target.value))}
        onBlur={onBlur}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className={cn(inputClasses, 'tabular-nums')}
      />
      {error && (
        <p id={errorId} role="alert" className="text-sm text-rose-600 dark:text-rose-400">
          {error}
        </p>
      )}
    </div>
  )
}
