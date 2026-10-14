import { useCallback, useEffect, useState } from 'react'
import { EVENTO_SESSAO_EXPIRADA, guardarSessao, lerSessao, type Sessao } from './api'
import { LoginPage } from './LoginPage'
import { PainelPage } from './PainelPage'

export function App() {
  const [sessao, setSessao] = useState<Sessao | null>(lerSessao)
  const [expirou, setExpirou] = useState(false)

  const definir = useCallback((nova: Sessao | null) => {
    guardarSessao(nova)
    setSessao(nova)
  }, [])

  useEffect(() => {
    const aoExpirar = () => {
      definir(null)
      setExpirou(true)
    }
    window.addEventListener(EVENTO_SESSAO_EXPIRADA, aoExpirar)
    return () => window.removeEventListener(EVENTO_SESSAO_EXPIRADA, aoExpirar)
  }, [definir])

  if (!sessao)
    return (
      <LoginPage
        expirou={expirou}
        onEntrar={(nova) => {
          setExpirou(false)
          definir(nova)
        }}
      />
    )
  return <PainelPage sessao={sessao} onSair={() => definir(null)} />
}
