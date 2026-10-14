import { createContext, useContext } from 'react'
import type { Sessao } from './api'

export type EstadoAuth =
  | { status: 'carregando' }
  | { status: 'anonimo' }
  | { status: 'autenticado'; sessao: Sessao }

export interface AuthContextValue {
  estado: EstadoAuth
  entrar: (email: string, senha: string, manterConectado: boolean) => Promise<void>
  sair: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth deve ser usado dentro de <AuthProvider>.')
  return ctx
}
