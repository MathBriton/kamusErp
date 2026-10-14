export interface Grade {
  id: string
  nome: string
  tamanhos: string[]
  emUso: boolean
}

export interface Cor {
  id: string
  nome: string
  hex: string | null
}

export interface Sku {
  id: string
  corId: string
  tamanho: string
  ordemTamanho: number
  codigoDeBarras: string
  precoVenda: number | null
  preco: number
  ativo: boolean
}

export interface Produto {
  id: string
  referencia: string
  descricao: string
  marca: string | null
  categoria: string | null
  colecao: string | null
  referenciaDoFornecedor: string | null
  fornecedorId: string | null
  precoVenda: number
  precoCusto: number | null
  markup: number | null
  ativo: boolean
  grade: Grade
  cores: Cor[]
  skus: Sku[]
}

export interface ProdutoResumo {
  id: string
  referencia: string
  descricao: string
  marca: string | null
  categoria: string | null
  colecao: string | null
  precoVenda: number
  markup: number | null
  skus: number
  cores: string[]
  ativo: boolean
}

export const PERFIS_DO_CATALOGO = ['Dono', 'Gerente', 'Estoquista']

export const moeda = (valor: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor)

export const decimal = (valor: number) => new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(valor)

/** "59,90", "1.234,56" ou "59.90" → número; vazio ou inválido → null. */
export function lerNumero(texto: string): number | null {
  const t = texto.trim()
  if (!t) return null
  const normalizado = t.includes(',') ? t.replace(/\./g, '').replace(',', '.') : t
  const n = Number(normalizado)
  return Number.isFinite(n) ? n : null
}
