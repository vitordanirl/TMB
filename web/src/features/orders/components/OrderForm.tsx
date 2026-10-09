import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, Loader2 } from 'lucide-react'
import { Controller, useForm } from 'react-hook-form'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/Button'
import { FormField } from '@/components/ui/FormField'
import { orderFormSchema, type OrderFormInput, type OrderFormValues } from '../orderFormSchema'
import { MoneyInput } from './MoneyInput'

interface OrderFormProps {
  onSubmit: (values: OrderFormValues) => Promise<unknown>
  onCancel?: () => void
}

export function OrderForm({ onSubmit, onCancel }: OrderFormProps) {
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<OrderFormInput, unknown, OrderFormValues>({
    resolver: zodResolver(orderFormSchema),
    defaultValues: { cliente: '', produto: '', valor: undefined },
  })

  const submit = handleSubmit(async (values) => {
    try {
      await onSubmit(values)
    } catch (error) {
      // Erros de validação do servidor (ProblemDetails) são exibidos nos respectivos campos.
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          if (field in orderFormSchema.shape) {
            setError(field as keyof OrderFormValues, { message: messages[0] })
          }
        }
        return
      }

      setError('root', { message: error instanceof Error ? error.message : 'Não foi possível criar o pedido.' })
    }
  })

  return (
    <form onSubmit={submit} noValidate className="space-y-5">
      <FormField
        label="Cliente"
        placeholder="Nome do cliente"
        autoComplete="off"
        autoFocus
        error={errors.cliente?.message}
        {...register('cliente')}
      />

      <FormField
        label="Produto"
        placeholder="Descrição do produto"
        autoComplete="off"
        error={errors.produto?.message}
        {...register('produto')}
      />

      <Controller
        control={control}
        name="valor"
        render={({ field, fieldState }) => (
          <MoneyInput
            label="Valor"
            name={field.name}
            ref={field.ref}
            value={field.value}
            onChange={field.onChange}
            onBlur={field.onBlur}
            error={fieldState.error?.message}
          />
        )}
      />

      {errors.root?.message && (
        <div
          role="alert"
          className="flex items-start gap-2 rounded-lg bg-rose-50 p-3 text-sm text-rose-700 dark:bg-rose-400/10 dark:text-rose-300"
        >
          <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
          {errors.root.message}
        </div>
      )}

      <div className="flex flex-col-reverse gap-3 pt-2 sm:flex-row sm:justify-end">
        {onCancel && (
          <Button variant="secondary" onClick={onCancel} disabled={isSubmitting}>
            Cancelar
          </Button>
        )}
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting && <Loader2 aria-hidden className="size-4 animate-spin" />}
          {isSubmitting ? 'Criando…' : 'Criar pedido'}
        </Button>
      </div>
    </form>
  )
}
