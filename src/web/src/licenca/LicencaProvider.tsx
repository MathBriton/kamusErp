import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useApi } from '../api/useApi'
import { EVENTO_LICENCA_BLOQUEADA } from '../auth/api'
import { estaBloqueada, LicencaContext, type SituacaoDaLicenca } from './LicencaContext'

const RECONSULTA_MS = 5 * 60_000

/** Mantém a situação da licença do cliente: ao entrar, a cada 5 minutos e sempre que alguma chamada recebe 402. */
export function LicencaProvider({ children }: { children: ReactNode }) {
  const { getJson } = useApi()
  const [situacao, setSituacao] = useState<SituacaoDaLicenca | null>(null)

  const atualizar = useCallback(async () => {
    try {
      const nova = await getJson<SituacaoDaLicenca>('/api/licenca')
      setSituacao(nova)
      return nova
    } catch {
      // Sem licença cadastrada (404) ou API fora: não bloqueia nem avisa.
      return null
    }
  }, [getJson])

  useEffect(() => {
    const consultar = () => void atualizar()
    const timer = window.setTimeout(consultar, 0)
    const intervalo = window.setInterval(consultar, RECONSULTA_MS)
    window.addEventListener(EVENTO_LICENCA_BLOQUEADA, consultar)
    return () => {
      window.clearTimeout(timer)
      window.clearInterval(intervalo)
      window.removeEventListener(EVENTO_LICENCA_BLOQUEADA, consultar)
    }
  }, [atualizar])

  const valor = useMemo(() => ({ situacao, bloqueada: estaBloqueada(situacao), atualizar }), [situacao, atualizar])
  return <LicencaContext value={valor}>{children}</LicencaContext>
}
