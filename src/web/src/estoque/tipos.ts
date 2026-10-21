export interface Loja {
  id: string
  empresaId: string
  nome: string
  ativa: boolean
  quantidade: number
}

export interface CorDaMatriz {
  id: string
  nome: string
  hex: string | null
}

export interface Celula {
  skuId: string
  corId: string
  tamanho: string
  codigoDeBarras: string
  ativo: boolean
  quantidade: number
  /** Nulo para quem não vê custos (ou SKU sem movimento). */
  custoMedio: number | null
  /** Preço de venda efetivo do SKU (o próprio ou o do produto). */
  preco?: number
}

export interface Matriz {
  produtoId: string
  referencia: string
  descricao: string
  precoVenda: number
  precoCusto: number | null
  tamanhos: string[]
  cores: CorDaMatriz[]
  celulas: Celula[]
  lojas: { lojaId: string; nome: string; quantidade: number }[]
  total: number
}

export interface ItemDaPosicao {
  produtoId: string
  referencia: string
  descricao: string
  quantidade: number
  valorEmCusto: number | null
  valorEmVenda: number
}

/** Uma página da posição; os totais somam todas as linhas filtradas. */
export interface Posicao {
  itens: ItemDaPosicao[]
  total: number
  pagina: number
  tamanho: number
  totalDePecas: number
  valorEmCusto: number | null
  valorEmVenda: number
}

export type TipoDeMovimento = 'Entrada' | 'Ajuste' | 'TransferenciaSaida' | 'TransferenciaEntrada' | 'EstoqueInicial' | 'Inventario' | 'Venda' | 'EstornoDeVenda' | 'Devolucao' | 'SaidaParaCondicional' | 'RetornoDeCondicional'

export interface Movimento {
  id: string
  ocorridoEm: string
  tipo: TipoDeMovimento
  lojaId: string
  loja: string
  lojaRelacionada: string | null
  referencia: string
  cor: string
  tamanho: string
  quantidade: number
  custoUnitario: number | null
  saldoApos: number
  custoMedioApos: number | null
  motivo: string | null
  documento: string | null
  observacao: string | null
  usuario: string | null
}

export const PERFIS_QUE_MOVIMENTAM = ['Dono', 'Gerente', 'Estoquista']
export const PERFIS_QUE_VEEM_CUSTO = ['Dono', 'Gerente', 'Estoquista', 'Financeiro']

export const ROTULO_DO_TIPO: Record<TipoDeMovimento, string> = {
  Entrada: 'Entrada',
  Ajuste: 'Ajuste',
  TransferenciaSaida: 'Transferência (saída)',
  TransferenciaEntrada: 'Transferência (entrada)',
  EstoqueInicial: 'Estoque inicial',
  Inventario: 'Inventário',
  Venda: 'Venda',
  EstornoDeVenda: 'Cancelamento de venda',
  Devolucao: 'Devolução (troca)',
  SaidaParaCondicional: 'Saída para condicional',
  RetornoDeCondicional: 'Retorno de condicional',
}

export const MOTIVOS_DE_AJUSTE = [
  { valor: 'Perda', rotulo: 'Perda' },
  { valor: 'Avaria', rotulo: 'Avaria' },
  { valor: 'Furto', rotulo: 'Furto' },
  { valor: 'Correcao', rotulo: 'Correção de contagem' },
  { valor: 'Outro', rotulo: 'Outro (descreva)' },
]

export const pecas = (n: number) => `${n.toLocaleString('pt-BR')} ${Math.abs(n) === 1 ? 'peça' : 'peças'}`
