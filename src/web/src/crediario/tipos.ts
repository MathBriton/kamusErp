export interface ParcelaDoCrediario {
  parcelaId: string
  tituloId: string
  descricao: string
  numero: number
  parcelas: number
  vencimento: string
  valor: number
  saldo: number
  situacao: 'Aberta' | 'Vencida' | 'Parcial' | 'Paga' | 'Cancelada'
  diasDeAtraso: number
  multa: number
  juros: number
}

export interface ClienteDoCrediario {
  clienteId: string
  clienteNome: string
  limite: number
  emAberto: number
  disponivel: number
  diasDeAtraso: number
  bloqueado: boolean
  motivo: string | null
  bloqueadoManualmente: boolean
  observacao: string | null
  parcelas: ParcelaDoCrediario[]
}

export interface LinhaDaCarteira {
  clienteId: string
  clienteNome: string
  limite: number
  emAberto: number
  atrasado: number
  diasDeAtraso: number
  disponivel: number
  bloqueado: boolean
}

export interface Carteira {
  aVencer: number
  ate30: number
  ate60: number
  ate90: number
  acima90: number
  total: number
  clientes: LinhaDaCarteira[]
}

export interface RegrasDoCrediario {
  multaPercentual: number
  jurosMensalPercentual: number
  diasParaBloquear: number
}

export const PERFIS_DO_CREDIARIO = ['Dono', 'Gerente', 'Financeiro', 'Vendedor']
export const PERFIS_DE_GESTAO = ['Dono', 'Gerente']
