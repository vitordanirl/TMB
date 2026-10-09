/**
 * Backoff exponencial (1 s, 2 s, 4 s, 8 s) limitado a 10 s; nunca desiste. Enquanto isso o polling
 * cobre o período sem tempo real, e ao reconectar as queries são revalidadas.
 */
export const retryDelayMs = (attempt: number) => Math.min(10_000, 1_000 * 2 ** attempt)
