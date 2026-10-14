import { useCallback, useMemo, useState, type ReactNode } from 'react'
import type { Sessao } from '../auth/api'
import { CONSOLIDADO, EmpresaContext } from './EmpresaContext'

const chave = (usuarioId: string) => `kamus.empresa.${usuarioId}`

function lerEscolhaSalva(usuarioId: string): string | null {
  try {
    return localStorage.getItem(chave(usuarioId))
  } catch {
    return null
  }
}

/**
 * Empresa (CNPJ) ativa do usuário logado. A escolha é salva por usuário e enviada à API em toda
 * requisição; o backend valida o acesso (o frontend nunca é a fonte da verdade).
 */
export function EmpresaProvider({ sessao, children }: { sessao: Sessao; children: ReactNode }) {
  const { empresas, usuario } = sessao
  const podeConsolidar = empresas.length > 1 && empresas.every((e) => e.perfil === 'Dono')

  const valida = useCallback(
    (selecao: string | null) =>
      selecao !== null && (empresas.some((e) => e.id === selecao) || (selecao === CONSOLIDADO && podeConsolidar)),
    [empresas, podeConsolidar],
  )

  const [selecao, setSelecao] = useState(() => {
    const salva = lerEscolhaSalva(usuario.id)
    return valida(salva) ? salva! : (empresas[0]?.id ?? '')
  })

  const selecionar = useCallback(
    (nova: string) => {
      if (!valida(nova)) return
      try {
        localStorage.setItem(chave(usuario.id), nova)
      } catch {
        // sem armazenamento: vale só para esta sessão
      }
      setSelecao(nova)
    },
    [valida, usuario.id],
  )

  const value = useMemo(() => {
    const consolidado = selecao === CONSOLIDADO
    const empresaAtual = consolidado ? null : (empresas.find((e) => e.id === selecao) ?? null)
    return {
      empresas,
      selecao,
      empresaAtual,
      consolidado,
      podeConsolidar,
      perfilAtual: consolidado ? 'Dono' : (empresaAtual?.perfil ?? ''),
      selecionar,
    }
  }, [empresas, selecao, podeConsolidar, selecionar])

  return <EmpresaContext.Provider value={value}>{children}</EmpresaContext.Provider>
}
