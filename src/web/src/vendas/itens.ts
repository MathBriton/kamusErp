import { lerNumero } from '../cadastros/catalogo'
import { centavos, type FormaDePagamento } from './tipos'

/** Linha de item sendo digitada (venda ou peças levadas na troca). Quantidade e desconto ficam como texto digitado. */
export interface ItemDigitado {
  skuId: string
  descricao: string
  preco: number
  quantidade: string
  desconto: string
}

/** Valor nulo = automático: cobre o que falta para fechar o total. */
export interface PagamentoDigitado {
  formaId: string
  valor: string | null
  parcelas: number
  /** Só para a forma vale-troca. */
  valeCodigo?: string
}

export const inteiro = (texto: string) => Math.max(0, Number.parseInt(texto, 10) || 0)
export const valorDe = (texto: string) => Math.max(0, lerNumero(texto) ?? 0)
export const totalDoItem = (i: ItemDigitado) => centavos(inteiro(i.quantidade) * i.preco - valorDe(i.desconto))
export const somaDosItens = (itens: ItemDigitado[]) => centavos(itens.reduce((s, i) => s + totalDoItem(i), 0))

/** Junta itens novos aos existentes (o mesmo SKU soma a quantidade). */
export function somarItens(atual: ItemDigitado[], novos: ItemDigitado[]) {
  const lista = [...atual]
  for (const n of novos) {
    const existente = lista.findIndex((i) => i.skuId === n.skuId)
    if (existente >= 0) lista[existente] = { ...lista[existente]!, quantidade: String(inteiro(lista[existente]!.quantidade) + inteiro(n.quantidade)) }
    else lista.push(n)
  }
  return lista
}

/** Valor efetivo de cada linha (a automática cobre o que falta) e quanto falta (negativo = troco). */
export function calcularPagamentos(linhas: PagamentoDigitado[], total: number) {
  const informado = (p: PagamentoDigitado) => (p.valor === null ? 0 : valorDe(p.valor))
  const fixos = linhas.reduce((s, p) => s + informado(p), 0)
  const valores = linhas.map((p) => (p.valor === null ? Math.max(0, centavos(total - fixos)) : informado(p)))
  const falta = centavos(total - valores.reduce((s, v) => s + v, 0))
  return { valores, falta }
}

export const corpoDosPagamentos = (linhas: PagamentoDigitado[], valores: number[]) =>
  linhas.map((p, i) => ({ formaId: p.formaId, valor: valores[i] ?? 0, parcelas: p.parcelas, ...(p.valeCodigo !== undefined ? { valeCodigo: p.valeCodigo.trim() } : {}) }))

export const novaLinha = (forma: FormaDePagamento): PagamentoDigitado => ({
  formaId: forma.id,
  valor: null,
  parcelas: 1,
  ...(forma.tipo === 'ValeTroca' ? { valeCodigo: '' } : {}),
})
