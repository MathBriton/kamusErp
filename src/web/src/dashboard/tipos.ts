export type CodigoDoPeriodo = 'hoje' | '7dias' | 'mes'

export const PERIODOS: { valor: CodigoDoPeriodo; rotulo: string; titulo: string; anterior: string }[] = [
  { valor: 'hoje', rotulo: 'Hoje', titulo: 'hoje', anterior: 'ontem' },
  { valor: '7dias', rotulo: '7 dias', titulo: 'em 7 dias', anterior: '7 dias anteriores' },
  { valor: 'mes', rotulo: 'Mês', titulo: 'no mês', anterior: 'mês anterior' },
]

export interface Cartao {
  total: number
  vendas: number
  pecas: number
  ticketMedio: number
  totalAnterior: number
  /** Em %, contra o período anterior; nulo quando o anterior foi zero. */
  variacao: number | null
}

export interface VendasDoDia {
  data: string
  vendas: number
  pecas: number
  total: number
}

export interface TotalPorNome {
  nome: string
  quantidade: number
  total: number
}

export interface BlocoDeVendas {
  periodo: Cartao
  mes: Cartao
  ultimos30Dias: VendasDoDia[]
  porForma: TotalPorNome[]
  porVendedor: TotalPorNome[]
  maisVendidos: { skuId: string; descricao: string; pecas: number; total: number }[]
  somenteAsMinhas: boolean
}

export interface Compromissos {
  hoje: number
  amanha: number
  proximosSeteDias: number
  vencido: number
  parcelasVencidas: number
  /** Parcelas em aberto que vencem de hoje até daqui a 6 dias. */
  parcelasProximosSeteDias: number
}

export interface ResumoFinanceiro {
  contas: { id: string; nome: string; tipo: string; saldo: number }[]
  saldoTotal: number
  aReceber: Compromissos
  aPagar: Compromissos
  crediarioEmAtraso: number
  clientesEmAtraso: number
}

export interface ResumoDeEstoque {
  pecas: number
  valorDeCusto: number
  valorDeVenda: number
  skusNegativos: number
  quebras: { produtoId: string; descricao: string; esgotados: string[] }[]
  totalDeQuebras: number
}

export interface Alerta {
  tipo: string
  gravidade: 'perigo' | 'aviso'
  titulo: string
  detalhe: string
  link: string
}

/** Blocos nulos = o perfil não vê. */
export interface Dashboard {
  periodo: CodigoDoPeriodo
  de: string
  ate: string
  vendas: BlocoDeVendas | null
  financeiro: ResumoFinanceiro | null
  estoque: ResumoDeEstoque | null
  condicionais: { abertas: number; atrasadas: number; pecas: number; valor: number } | null
  alertas: Alerta[]
  /** Etapas da implantação já concluídas: produtos, clientes, entrada_de_estoque, contas_a_pagar, primeira_venda. */
  primeirosPassos: string[]
}

export interface PreferenciasDoResumo {
  ativo: boolean
  hora: number
  /** 0 = domingo … 6 = sábado. */
  dias: number[]
  vendas: boolean
  financeiro: boolean
  estoque: boolean
  condicionais: boolean
  crediario: boolean
  enviarSemMovimento: boolean
  ultimoEnvio: string | null
}

export interface PreviaDoResumo {
  titulo: string
  linhas: string[]
  temMovimento: boolean
}

/** 200 → "▲ 200%", -12.5 → "▼ 12,5%". */
export function variacao(v: number) {
  const texto = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 }).format(Math.abs(v))
  return `${v >= 0 ? '▲' : '▼'} ${texto}%`
}
