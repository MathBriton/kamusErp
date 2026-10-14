import { createContext, useContext } from 'react'

export type EstadoLicenca = 'Trial' | 'Ativa' | 'Carencia' | 'Suspensa' | 'Cancelada'

/** Resposta de GET /api/licenca. Datas no formato AAAA-MM-DD. */
export interface SituacaoDaLicenca {
  plano: string
  estado: EstadoLicenca
  vencimento: string | null
  fimDaCarencia: string | null
  diasParaVencer: number | null
  valorMensal: number
  mensagem: string
  limiteDeCnpjs: number | null
  limiteDeLojas: number | null
  limiteDeUsuarios: number | null
}

export interface LicencaContextValue {
  /** null enquanto carrega ou quando o cliente não tem licença cadastrada. */
  situacao: SituacaoDaLicenca | null
  bloqueada: boolean
  /** Consulta a API de novo e devolve a situação atualizada. */
  atualizar: () => Promise<SituacaoDaLicenca | null>
}

export const LicencaContext = createContext<LicencaContextValue | null>(null)

export function useLicenca(): LicencaContextValue {
  const ctx = useContext(LicencaContext)
  if (!ctx) throw new Error('useLicenca deve ser usado dentro de <LicencaProvider>.')
  return ctx
}

export const estaBloqueada = (s: SituacaoDaLicenca | null) => s?.estado === 'Suspensa' || s?.estado === 'Cancelada'

/** "2026-10-15" → "15/10/2026" (sem passar por Date, para não sofrer com fuso). */
export const formatarDia = (dia: string | null) => (dia ? dia.split('-').reverse().join('/') : '—')

export const formatarMoeda = (valor: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor)
