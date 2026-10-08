import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/** Combina classes condicionalmente, resolvendo conflitos do Tailwind (ex.: `px-2` + `px-4`). */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs))
}
