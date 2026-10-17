export type TipoDePagamento = 'Dinheiro' | 'Pix' | 'Debito' | 'Credito' | 'Outro'

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
}

/** Dinheiro e PIX entram na hora numa conta; os demais viram contas a receber. */
export const entraNaHora = (tipo: TipoDePagamento) => tipo === 'Dinheiro' || tipo === 'Pix'

/** Arredonda para centavos (evita 0,1 + 0,2). */
export const centavos = (valor: number) => Math.round(valor * 100) / 100

/** Primeiro dia do mês corrente (AAAA-MM-DD), no fuso do navegador. */
export function inicioDoMes(hoje: string) {
  return `${hoje.slice(0, 7)}-01`
}
