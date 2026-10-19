import { useCallback, useEffect, useState } from 'react'
import { ErroDeApi, useSessao } from '../auth/Sessao'

/** Busca na API quando o caminho ou a empresa ativa mudam; recarregar() para puxar de novo (pull-to-refresh). */
export function useDados<T>(caminho: string) {
  const { getJson, estado } = useSessao()
  const empresaId = estado.status === 'logado' ? estado.empresaId : ''
  const [versao, setVersao] = useState(0)
  const chave = `${empresaId}|${caminho}|${versao}`
  // Cada resposta guarda a chave que a pediu: enquanto a chave atual não chega, está carregando (e os dados anteriores continuam na tela).
  const [resultado, setResultado] = useState<{ chave: string; dados: T | null; erro: string | null }>({ chave: '', dados: null, erro: null })

  useEffect(() => {
    let vivo = true
    getJson<T>(caminho)
      .then((dados) => vivo && setResultado({ chave, dados, erro: null }))
      .catch((e: unknown) => vivo && setResultado({ chave, dados: null, erro: e instanceof ErroDeApi ? e.message : 'Sem conexão com o servidor.' }))
    return () => {
      vivo = false
    }
  }, [getJson, caminho, chave])

  const recarregar = useCallback(() => setVersao((v) => v + 1), [])
  return { dados: resultado.dados, erro: resultado.erro, carregando: resultado.chave !== chave, recarregar }
}
