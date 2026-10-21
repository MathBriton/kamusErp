export type TipoDeTitulo = 'pagar' | 'receber'

export interface ContaFinanceira {
  id: string
  empresaId: string
  nome: string
  tipo: 'Caixa' | 'Banco' | 'Carteira'
  saldoInicial: number
  saldoInicialEm: string
  saldo: number
  ativa: boolean
}

export interface Categoria {
  id: string
  nome: string
  grupo: string
  tipo: 'Receita' | 'Despesa'
  ativa: boolean
}

export type Situacao = 'Aberta' | 'Vencida' | 'Parcial' | 'Paga' | 'Cancelada'

export interface LinhaDeParcela {
  tituloId: string
  parcelaId: string
  numero: number
  parcelas: number
  vencimento: string
  descricao: string
  pessoaNome: string | null
  documento: string | null
  categoria: string | null
  valor: number
  valorPago: number
  saldo: number
  situacao: Situacao
  origem: string
}

export interface ListaDeParcelas {
  itens: LinhaDeParcela[]
  valor: number
  pago: number
  saldo: number
  vencido: number
  /** Linhas com os filtros (os valores acima somam todas elas; `itens` é só a página). */
  total: number
  pagina: number
  tamanho: number
}

export interface ListaDeContas {
  itens: ContaFinanceira[]
  total: number
  pagina: number
  tamanho: number
  /** Saldo somado de todas as contas filtradas. */
  saldo: number
}

export interface ResultadoDaBaixaEmLote {
  baixadas: number
  total: number
  falhas: { parcelaId: string; descricao: string; motivo: string }[]
}

export interface Baixa {
  id: string
  data: string
  contaId: string
  conta: string
  valor: number
  juros: number
  multa: number
  desconto: number
  total: number
  usuario: string | null
  estornada: boolean
  estornadaPor: string | null
}

export interface Parcela {
  id: string
  numero: number
  vencimento: string
  valor: number
  valorPago: number
  saldo: number
  situacao: Situacao
  baixas: Baixa[]
}

export interface Titulo {
  id: string
  tipo: 'Pagar' | 'Receber'
  descricao: string
  pessoaId: string | null
  pessoaNome: string | null
  categoriaId: string | null
  categoria: string | null
  documento: string | null
  competencia: string
  observacao: string | null
  valorTotal: number
  cancelado: boolean
  origem: string
  parcelas: Parcela[]
}

export const PERFIS_DO_FINANCEIRO = ['Dono', 'Gerente', 'Financeiro']

export const SITUACAO: Record<Situacao, { rotulo: string; classe: string }> = {
  Aberta: { rotulo: 'Em aberto', classe: 'bg-info-soft text-info' },
  Vencida: { rotulo: 'Vencida', classe: 'bg-danger-soft text-danger' },
  Parcial: { rotulo: 'Parcial', classe: 'bg-warning-soft text-warning' },
  Paga: { rotulo: 'Paga', classe: 'bg-success-soft text-success' },
  Cancelada: { rotulo: 'Cancelada', classe: 'bg-surface-2 text-muted' },
}

/** "2026-10-15" → "15/10/2026" (sem passar por fuso horário). */
export const dataBr = (dia: string) => dia.split('-').reverse().join('/')

/** Hoje (aaaa-mm-dd) pelo calendário de Brasília (UTC−3), o mesmo da API — independe do fuso do navegador. */
export function hojeIso() {
  return new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString().slice(0, 10)
}

export function somarDias(dia: string, dias: number) {
  const [a, m, d] = dia.split('-').map(Number)
  const data = new Date(a!, m! - 1, d! + dias)
  return `${data.getFullYear()}-${String(data.getMonth() + 1).padStart(2, '0')}-${String(data.getDate()).padStart(2, '0')}`
}

/** Mesmo dia do mês seguinte (ou o último dia, se o mês for mais curto). */
export function somarMeses(dia: string, meses: number) {
  const [a, m, d] = dia.split('-').map(Number)
  const ultimo = new Date(a!, m! - 1 + meses + 1, 0).getDate()
  const data = new Date(a!, m! - 1 + meses, Math.min(d!, ultimo))
  return `${data.getFullYear()}-${String(data.getMonth() + 1).padStart(2, '0')}-${String(data.getDate()).padStart(2, '0')}`
}
