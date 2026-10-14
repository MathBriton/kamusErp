import { useEffect, useState } from 'react'
import { useApi } from '../api/useApi'
import type { Loja } from './tipos'

/** Lojas da empresa ativa (a API cria a primeira no primeiro acesso). */
export function useLojas(versao = 0) {
  const { getJson } = useApi()
  const [lojas, setLojas] = useState<Loja[] | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<Loja[]>('/api/estoque/lojas', controle.signal)
      .then(setLojas)
      .catch(() => {
        if (!controle.signal.aborted) setLojas([])
      })
    return () => controle.abort()
  }, [getJson, versao])

  return lojas
}
