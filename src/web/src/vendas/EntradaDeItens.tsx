import { useRef, useState } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { estilos } from '../components/estilos'
import { BuscaDeProduto } from '../estoque/BuscaDeProduto'
import { MatrizDeEstoque } from '../estoque/MatrizDeEstoque'
import type { Matriz } from '../estoque/tipos'
import { inteiro, somarItens, totalDoItem, type ItemDigitado } from './itens'

interface PorCodigo {
  produto: { descricao: string }
  sku: { id: string; tamanho: string; preco: number; ativo: boolean }
  cor: string
}

interface Props {
  lojaId: string
  itens: ItemDigitado[]
  onItens: (atualizar: (atual: ItemDigitado[]) => ItemDigitado[]) => void
  onErro: (mensagem: string | null) => void
  /** Rótulo da tabela (acessível) e do botão da grade. */
  rotulo: string
  botaoDaGrade: string
  /** Sem a coluna de desconto (ex.: peças da condicional). */
  semDesconto?: boolean
}

/** Itens pelo leitor de código de barras (cada leitura soma 1) ou pela busca + grade, com quantidade e desconto editáveis. */
export function EntradaDeItens({ lojaId, itens, onItens, onErro, rotulo, botaoDaGrade, semDesconto = false }: Props) {
  const { getJson } = useApi()
  const [codigo, setCodigo] = useState('')
  const [matriz, setMatriz] = useState<Matriz | null>(null)
  const [digitado, setDigitado] = useState<Record<string, string>>({})
  const leitor = useRef<HTMLInputElement>(null)

  async function ler() {
    const valor = codigo.trim()
    if (!valor) return
    setCodigo('')
    onErro(null)
    try {
      const r = await getJson<PorCodigo>(`/api/produtos/por-codigo/${encodeURIComponent(valor)}`)
      if (!r.sku.ativo) onErro(`${r.produto.descricao} ${r.cor} ${r.sku.tamanho} está inativo no catálogo.`)
      else onItens((atual) => somarItens(atual, [{ skuId: r.sku.id, descricao: `${r.produto.descricao} · ${r.cor} ${r.sku.tamanho}`, preco: r.sku.preco, quantidade: '1', desconto: '' }]))
    } catch {
      onErro(`Nenhum produto com o código ${valor}.`)
    } finally {
      leitor.current?.focus()
    }
  }

  const escolher = (produtoId: string) => {
    onErro(null)
    getJson<Matriz>(`/api/estoque/posicao/${produtoId}?lojaId=${lojaId}`)
      .then((m) => {
        setMatriz(m)
        setDigitado({})
      })
      .catch((ex: unknown) => onErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível abrir o produto.'))
  }

  const adicionarDaGrade = () => {
    if (!matriz) return
    const novos = matriz.celulas.flatMap((c) => {
      const q = inteiro(digitado[c.skuId] ?? '')
      if (q === 0) return []
      const cor = matriz.cores.find((x) => x.id === c.corId)?.nome ?? ''
      return [{ skuId: c.skuId, descricao: `${matriz.descricao} · ${cor} ${c.tamanho}`, preco: c.preco ?? matriz.precoVenda, quantidade: String(q), desconto: '' }]
    })
    if (novos.length === 0) {
      onErro('Digite ao menos uma quantidade na grade.')
      return
    }
    onItens((atual) => somarItens(atual, novos))
    setMatriz(null)
    onErro(null)
  }

  const mudar = (skuId: string, campo: 'quantidade' | 'desconto', valor: string) =>
    onItens((atual) => atual.map((i) => (i.skuId === skuId ? { ...i, [campo]: campo === 'quantidade' ? valor.replace(/\D/g, '') : valor } : i)))

  return (
    <>
      {!matriz && (
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <input
            ref={leitor}
            aria-label="Código de barras"
            autoFocus
            value={codigo}
            onChange={(e) => setCodigo(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault()
                void ler()
              }
            }}
            disabled={!lojaId}
            inputMode="numeric"
            placeholder="Passe o leitor (cada leitura soma 1)"
            className={estilos.campo}
          />
          <BuscaDeProduto onEscolher={escolher} desabilitado={!lojaId} placeholder="Ou busque o produto e escolha na grade" />
        </div>
      )}

      {matriz && (
        <div className="mt-3 space-y-3 rounded-xl border border-primary p-4">
          <p className="font-semibold">
            <span className="font-mono text-xs text-muted">{matriz.referencia}</span> {matriz.descricao} <span className="text-sm text-muted">{moeda(matriz.precoVenda)}</span>
          </p>
          <MatrizDeEstoque
            matriz={matriz}
            digitacao={{ valores: digitado, onMudar: (skuId, valor) => setDigitado((v) => ({ ...v, [skuId]: valor })), somenteAtivos: true }}
          />
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setMatriz(null)} className={estilos.botao}>Cancelar</button>
            <button type="button" onClick={adicionarDaGrade} className={estilos.botaoPrimario}>{botaoDaGrade}</button>
          </div>
        </div>
      )}

      {itens.length > 0 && (
        // No celular a tabela rola dentro do próprio quadro (nada escapa da borda nem fica espremido).
        <div className="mt-4 -mx-1 overflow-x-auto px-1">
          <table aria-label={rotulo} className="w-full min-w-[34rem] text-sm">
            <thead className="text-left text-xs text-muted uppercase">
              <tr>
                <th className="py-2">Produto</th>
                <th className="w-20 py-2 text-right">Qtd.</th>
                <th className="py-2 text-right">Preço</th>
                {!semDesconto && <th className="w-28 py-2 text-right">Desconto (R$)</th>}
                <th className="py-2 text-right">Total</th>
                <th />
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {itens.map((i) => (
                <tr key={i.skuId}>
                  <td className="py-2 pr-2 font-semibold">{i.descricao}</td>
                  <td className="py-2">
                    <input
                      aria-label={`Quantidade de ${i.descricao}`}
                      value={i.quantidade}
                      onChange={(e) => mudar(i.skuId, 'quantidade', e.target.value)}
                      inputMode="numeric"
                      className="h-9 w-full rounded-lg border border-border bg-surface px-2 text-right tabular-nums"
                    />
                  </td>
                  <td className="py-2 text-right tabular-nums">{moeda(i.preco)}</td>
                  {!semDesconto && (
                    <td className="py-2 pl-2">
                      <input
                        aria-label={`Desconto de ${i.descricao}`}
                        value={i.desconto}
                        onChange={(e) => mudar(i.skuId, 'desconto', e.target.value)}
                        inputMode="decimal"
                        placeholder="0,00"
                        className="h-9 w-full rounded-lg border border-border bg-surface px-2 text-right tabular-nums"
                      />
                    </td>
                  )}
                  <td className="py-2 text-right font-semibold tabular-nums">{moeda(totalDoItem(i))}</td>
                  <td className="py-2 text-right">
                    <button
                      type="button"
                      aria-label={`Remover ${i.descricao}`}
                      onClick={() => onItens((atual) => atual.filter((x) => x.skuId !== i.skuId))}
                      className="ml-1 h-10 rounded-lg px-3 text-sm font-medium text-danger hover:bg-danger-soft"
                    >
                      Remover
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
