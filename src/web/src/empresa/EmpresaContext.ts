import { createContext, useContext } from 'react'
import type { Empresa } from '../auth/api'

export const CONSOLIDADO = 'todas'

export interface EmpresaContextValue {
  empresas: Empresa[]
  /** Valor enviado no header X-Empresa-Id: id da empresa ou "todas". */
  selecao: string
  empresaAtual: Empresa | null
  consolidado: boolean
  podeConsolidar: boolean
  /** Perfil do usuário na empresa ativa ("Dono" na visão consolidada). */
  perfilAtual: string
  selecionar: (selecao: string) => void
}

export const EmpresaContext = createContext<EmpresaContextValue | null>(null)

export function useEmpresa(): EmpresaContextValue {
  const ctx = useContext(EmpresaContext)
  if (!ctx) throw new Error('useEmpresa deve ser usado dentro de <EmpresaProvider>.')
  return ctx
}
