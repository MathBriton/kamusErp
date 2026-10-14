import { useCallback } from 'react'
import { chamar } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { useEmpresa } from '../empresa/EmpresaContext'

/** Cliente da API para telas autenticadas: envia o token e a empresa ativa (X-Empresa-Id). */
export function useApi() {
  const { estado } = useAuth()
  const { selecao } = useEmpresa()
  const token = estado.status === 'autenticado' ? estado.sessao.accessToken : ''

  const getJson = useCallback(
    async <T,>(caminho: string, signal?: AbortSignal): Promise<T> => {
      const resposta = await chamar(caminho, {
        signal,
        headers: { Authorization: `Bearer ${token}`, 'X-Empresa-Id': selecao },
      })
      return (await resposta.json()) as T
    },
    [token, selecao],
  )

  const post = useCallback(
    async (caminho: string, signal?: AbortSignal): Promise<void> => {
      await chamar(caminho, {
        method: 'POST',
        signal,
        headers: { Authorization: `Bearer ${token}`, 'X-Empresa-Id': selecao },
      })
    },
    [token, selecao],
  )

  return { getJson, post }
}
