import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import * as api from './api'
import { AuthContext, type EstadoAuth } from './AuthContext'

/** Renova o token de acesso 1 minuto antes de expirar. */
const ANTECEDENCIA_MS = 60_000

/**
 * O token de acesso fica só em memória (nunca em localStorage). Ao abrir a página,
 * a sessão é restaurada pelo cookie HttpOnly via /api/auth/refresh.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [estado, setEstado] = useState<EstadoAuth>({ status: 'carregando' })

  const aplicar = useCallback((sessao: api.Sessao | null) => {
    setEstado(sessao ? { status: 'autenticado', sessao } : { status: 'anonimo' })
  }, [])

  useEffect(() => {
    let ativo = true
    api
      .renovar()
      .catch(() => null)
      .then((sessao) => ativo && aplicar(sessao))
    return () => {
      ativo = false
    }
  }, [aplicar])

  const expiraEm = estado.status === 'autenticado' ? estado.sessao.expiraEm : null
  useEffect(() => {
    if (!expiraEm) return
    const espera = Math.max(5_000, new Date(expiraEm).getTime() - Date.now() - ANTECEDENCIA_MS)
    const timer = setTimeout(() => {
      api.renovar().catch(() => null).then(aplicar)
    }, espera)
    return () => clearTimeout(timer)
  }, [expiraEm, aplicar])

  const entrar = useCallback(
    async (email: string, senha: string, manterConectado: boolean) => {
      aplicar(await api.entrar(email, senha, manterConectado))
    },
    [aplicar],
  )

  const sair = useCallback(async () => {
    try {
      await api.sair()
    } finally {
      aplicar(null)
    }
  }, [aplicar])

  const value = useMemo(() => ({ estado, entrar, sair }), [estado, entrar, sair])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
