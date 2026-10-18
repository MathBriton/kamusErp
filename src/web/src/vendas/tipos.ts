export type TipoDePagamento = 'Dinheiro' | 'Pix' | 'Debito' | 'Credito' | 'Outro' | 'ValeTroca'

export interface FormaDePagamento {
  id: string
  nome: string
  tipo: TipoDePagamento
  contaId: string | null
  taxaAVista: number
  taxaParcelado: number
  maxParcelas: number
  prazoDias: number
  ativa: boolean
}

export interface ItemDaVenda {
  skuId: string
  produtoId: string
  descricao: string
  quantidade: number
  precoUnitario: number
  desconto: number
  total: number
  custoUnitario: number | null
}

export interface PagamentoDaVenda {
  formaId: string
  formaNome: string
  tipo: TipoDePagamento
  valor: number
  parcelas: number
  taxaPercentual: number
  valorTaxa: number
}

export interface Venda {
  id: string
  empresaId: string
  lojaId: string
  numero: number
  data: string
  clienteId: string | null
  clienteNome: string | null
  vendedorId: string | null
  vendedorNome: string | null
  comissaoPercentual: number
  comissao: number
  subtotal: number
  descontoGeral: number
  total: number
  troco: number
  pecas: number
  taxaTotal: number
  custoTotal: number | null
  lucroBruto: number | null
  status: 'Concluida' | 'Cancelada'
  notaExterna: string | null
  observacao: string | null
  registradaPor: string | null
  registradaEm: string
  motivoDoCancelamento: string | null
  canceladaPor: string | null
  canceladaEm: string | null
  itens: ItemDaVenda[]
  pagamentos: PagamentoDaVenda[]
  avisos: string[]
}

export interface LinhaDeVenda {
  id: string
  empresaId: string
  numero: number
  data: string
  clienteNome: string | null
  vendedorId: string | null
  vendedorNome: string | null
  pecas: number
  total: number
  formas: string
  status: 'Concluida' | 'Cancelada'
  notaExterna: string | null
}

export interface ValeTroca {
  id: string
  codigo: string
  clienteId: string | null
  clienteNome: string | null
  valor: number
  saldo: number
  emitidoEm: string
  validoAte: string
  situacao: 'Ativo' | 'Usado' | 'Vencido' | 'Cancelado'
  trocaId: string | null
  motivoDoCancelamento: string | null
}

export interface ListaDeVales {
  itens: ValeTroca[]
  saldoAtivo: number
  saldoVencido: number
}

export type MotivoDaTroca = 'Tamanho' | 'Cor' | 'Defeito' | 'Desistencia' | 'Outro'

export const MOTIVOS_DA_TROCA: { valor: MotivoDaTroca; rotulo: string }[] = [
  { valor: 'Tamanho', rotulo: 'Tamanho' },
  { valor: 'Cor', rotulo: 'Cor' },
  { valor: 'Defeito', rotulo: 'Defeito' },
  { valor: 'Desistencia', rotulo: 'Desistência' },
  { valor: 'Outro', rotulo: 'Outro' },
]

export interface Devolvivel {
  skuId: string
  descricao: string
  vendidas: number
  jaDevolvidas: number
  disponiveis: number
  valorPorPeca: number
}

export interface Devolviveis {
  vendaId: string
  lojaId: string
  numero: number
  data: string
  clienteNome: string | null
  vendedorNome: string | null
  diasDesdeAVenda: number
  prazoDeTrocaDias: number
  dentroDoPrazo: boolean
  itens: Devolvivel[]
}

export interface Troca {
  id: string
  numero: number
  data: string
  vendaId: string
  vendaNumero: number
  clienteNome: string | null
  vendedorNome: string | null
  motivo: MotivoDaTroca
  observacao: string | null
  valorDevolvido: number
  valorLevado: number
  diferenca: number
  troco: number
  comissao: number
  registradaPor: string | null
  devolvidos: { skuId: string; descricao: string; quantidade: number; valor: number; destino: 'Estoque' | 'Avariado' }[]
  levados: { skuId: string; descricao: string; quantidade: number; precoUnitario: number; desconto: number; total: number }[]
  pagamentos: PagamentoDaVenda[]
  vale: ValeTroca | null
  avisos: string[]
}

export interface LinhaDeTroca {
  id: string
  numero: number
  data: string
  vendaNumero: number
  clienteNome: string | null
  motivo: MotivoDaTroca
  pecasDevolvidas: number
  valorDevolvido: number
  valorLevado: number
  diferenca: number
  valeCodigo: string | null
}

export interface Fechamento {
  id: string
  vendedorId: string
  vendedorNome: string
  de: string
  ate: string
  vendas: number
  trocas: number
  totalVendido: number
  comissao: number
  fechadoPor: string | null
  fechadoEm: string
  contaAPagarGerada: boolean
}

export interface ListaDeVendas {
  itens: LinhaDeVenda[]
  quantidade: number
  pecas: number
  total: number
  ticketMedio: number
}

export interface ComissaoDoVendedor {
  vendedorId: string | null
  vendedor: string
  vendas: number
  total: number
  comissao: number
  comissaoPendente: number
}

export interface VendedorResumo {
  id: string
  nome: string
  comissaoPercentual: number
  usuarioId: string | null
  ativo: boolean
}

export const PERFIS_QUE_VENDEM = ['Dono', 'Gerente', 'Vendedor']
export const PERFIS_DE_GESTAO = ['Dono', 'Gerente']
export const PERFIS_QUE_CONSULTAM = ['Dono', 'Gerente', 'Financeiro']

export const ROTULO_DO_TIPO: Record<TipoDePagamento, string> = {
  Dinheiro: 'Dinheiro',
  Pix: 'PIX',
  Debito: 'Cartão de débito',
  Credito: 'Cartão de crédito',
  Outro: 'Outro',
  ValeTroca: 'Vale-troca',
}

/** Dinheiro e PIX entram na hora numa conta; os demais viram contas a receber. */
export const entraNaHora = (tipo: TipoDePagamento) => tipo === 'Dinheiro' || tipo === 'Pix'

/** 5 → "5%", 1.5 → "1,5%". */
export const porcentagem = (n: number) => `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(n)}%`

/** Arredonda para centavos (evita 0,1 + 0,2). */
export const centavos = (valor: number) => Math.round(valor * 100) / 100

/** Primeiro dia do mês corrente (AAAA-MM-DD), no fuso do navegador. */
export function inicioDoMes(hoje: string) {
  return `${hoje.slice(0, 7)}-01`
}
