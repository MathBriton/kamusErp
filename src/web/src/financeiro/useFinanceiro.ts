import { useEffect, useState } from 'react'
import { useApi } from '../api/useApi'
import type { Categoria, ContaFinanceira } from './tipos'

export function useContas(versao = 0) {
  const { getJson } = useApi()
  const [contas, setContas] = useState<ContaFinanceira[] | null>(null)
  useEffect(() => {
    const controle = new AbortController()
    getJson<ContaFinanceira[]>('/api/financeiro/contas', controle.signal)
      .then(setContas)
      .catch(() => {
        if (!controle.signal.aborted) setContas([])
      })
    return () => controle.abort()
  }, [getJson, versao])
  return contas
}

export function useCategorias(versao = 0) {
  const { getJson } = useApi()
  const [categorias, setCategorias] = useState<Categoria[] | null>(null)
  useEffect(() => {
    const controle = new AbortController()
    getJson<Categoria[]>('/api/financeiro/categorias', controle.signal)
      .then(setCategorias)
      .catch(() => {
        if (!controle.signal.aborted) setCategorias([])
      })
    return () => controle.abort()
  }, [getJson, versao])
  return categorias
}
