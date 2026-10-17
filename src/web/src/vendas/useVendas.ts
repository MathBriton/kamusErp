import { useEffect, useState } from 'react'
import { useApi } from '../api/useApi'
import type { FormaDePagamento, VendedorResumo } from './tipos'

function useLista<T>(caminho: string, versao: number) {
  const { getJson } = useApi()
  const [dados, setDados] = useState<T[] | null>(null)
  useEffect(() => {
    const controle = new AbortController()
    getJson<T[]>(caminho, controle.signal)
      .then(setDados)
      .catch(() => {
        if (!controle.signal.aborted) setDados([])
      })
    return () => controle.abort()
  }, [getJson, caminho, versao])
  return dados
}

export const useFormas = (versao = 0) => useLista<FormaDePagamento>('/api/vendas/formas', versao)

export const useVendedores = () => useLista<VendedorResumo>('/api/vendedores', 0)
