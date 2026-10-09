import { createContext, useContext } from 'react'

/**
 * - connecting: primeira conexão em andamento;
 * - connected: recebendo eventos em tempo real;
 * - reconnecting / disconnected: sem tempo real; as telas usam polling como fallback.
 */
export type RealtimeStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export const RealtimeContext = createContext<RealtimeStatus>('disconnected')

export function useRealtimeStatus(): RealtimeStatus {
  return useContext(RealtimeContext)
}
