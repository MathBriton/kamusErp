import { useEffect, useState } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { decimal, moeda } from '../cadastros/catalogo'
import { estilos } from '../components/estilos'
import { dataBr } from '../financeiro/tipos'
import { calcularPagamentos, novaLinha, type PagamentoDigitado } from './itens'
import { prontaParaVender, type FormaDePagamento, type ValeTroca } from './tipos'

interface Props {
  formas: FormaDePagamento[]
  total: number
  linhas: PagamentoDigitado[]
  onLinhas: (linhas: PagamentoDigitado[]) => void
  /** Cliente da venda (para mostrar o limite do crediário). Indefinido = não mostra. */
  clienteId?: string | null
}

/** Uma ou mais formas de pagamento; a linha sem valor digitado cobre o que falta. Vale-troca pede o código e mostra o saldo. */
export function EditorDePagamentos({ formas, total, linhas, onLinhas, clienteId }: Props) {
  const { valores } = calcularPagamentos(linhas, total)
  const forma = (id: string) => formas.find((f) => f.id === id)
  const mudar = (indice: number, mudanca: Partial<PagamentoDigitado>) => onLinhas(linhas.map((p, i) => (i === indice ? { ...p, ...mudanca } : p)))

  const adicionar = () => {
    const prontas = formas.filter(prontaParaVender)
    const outra = prontas.find((f) => !linhas.some((p) => p.formaId === f.id)) ?? prontas[0]
    if (!outra) return
    // Congela os valores atuais e deixa a nova forma cobrir o que falta.
    onLinhas([...linhas.map((p, i) => ({ ...p, valor: decimal(valores[i] ?? 0) })), novaLinha(outra)])
  }

  return (
    <div className="space-y-3">
      {linhas.map((p, i) => {
        const f = forma(p.formaId)
        return (
          <div key={i} className="space-y-2">
            {/* No celular a forma ocupa a linha inteira e valor/parcelas/remover vêm embaixo: o nome da forma nunca some. */}
            <div className="flex flex-wrap items-center gap-2">
              <select
                aria-label={`Forma do pagamento ${i + 1}`}
                value={p.formaId}
                onChange={(e) => {
                  const nova = forma(e.target.value)
                  mudar(i, { formaId: e.target.value, parcelas: 1, valeCodigo: nova?.tipo === 'ValeTroca' ? '' : undefined })
                }}
                className={`${estilos.campo} min-w-0 basis-full sm:flex-1 sm:basis-48`}
              >
                {formas.map((x) => (
                  <option key={x.id} value={x.id} disabled={!prontaParaVender(x)}>
                    {prontaParaVender(x) ? x.nome : `${x.nome} — falta configurar a conta`}
                  </option>
                ))}
              </select>
              <input
                aria-label={`Valor do pagamento ${i + 1}`}
                value={p.valor ?? decimal(valores[i] ?? 0)}
                onChange={(e) => mudar(i, { valor: e.target.value })}
                inputMode="decimal"
                className={`${estilos.campo} w-28 flex-1 text-right sm:flex-none`}
              />
              {f && f.maxParcelas > 1 ? (
                <select
                  aria-label={`Parcelas do pagamento ${i + 1}`}
                  value={p.parcelas}
                  onChange={(e) => mudar(i, { parcelas: Number(e.target.value) })}
                  className={`${estilos.campo} w-20`}
                >
                  {Array.from({ length: f.maxParcelas }, (_, n) => n + 1).map((n) => (
                    <option key={n} value={n}>{`${n}x`}</option>
                  ))}
                </select>
              ) : (
                <span className="w-10 text-center text-xs text-muted">1x</span>
              )}
              {linhas.length > 1 ? (
                <button
                  type="button"
                  aria-label={`Remover pagamento ${i + 1}`}
                  onClick={() => onLinhas(linhas.filter((_, x) => x !== i))}
                  className="h-10 rounded-lg px-3 text-sm font-medium text-danger hover:bg-danger-soft"
                >
                  Remover
                </button>
              ) : null}
            </div>
            {f?.tipo === 'ValeTroca' && <CodigoDoVale indice={i + 1} codigo={p.valeCodigo ?? ''} onCodigo={(c) => mudar(i, { valeCodigo: c })} />}
            {f?.tipo === 'Crediario' && clienteId !== undefined && <SituacaoDoCrediario clienteId={clienteId} />}
          </div>
        )
      })}
      {formas.length === 0 && <p className="text-sm text-muted">Nenhuma forma de pagamento ativa. Dono ou Gerente cadastram em Vendas → Formas de pagamento.</p>}
      {formas.length > 0 && (
        <button type="button" onClick={adicionar} className={estilos.botao}>Adicionar forma de pagamento</button>
      )}
    </div>
  )
}

function CodigoDoVale({ indice, codigo, onCodigo }: { indice: number; codigo: string; onCodigo: (codigo: string) => void }) {
  const { getJson } = useApi()
  const [vale, setVale] = useState<ValeTroca | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  const consultar = async () => {
    setErro(null)
    setVale(null)
    if (!codigo.trim()) return
    try {
      setVale(await getJson<ValeTroca>(`/api/vendas/vales/por-codigo/${encodeURIComponent(codigo.trim())}`))
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Vale não encontrado.')
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-2 pl-1 text-sm">
      <input
        aria-label={`Código do vale do pagamento ${indice}`}
        value={codigo}
        onChange={(e) => onCodigo(e.target.value.toUpperCase())}
        onBlur={() => void consultar()}
        placeholder="Código do vale (ex.: VT7K2M9Q)"
        className={`${estilos.campo} max-w-56 font-mono`}
      />
      {vale && (
        <span className={vale.situacao === 'Ativo' ? 'text-success' : 'text-danger'}>
          {vale.situacao === 'Ativo' ? `Saldo ${moeda(vale.saldo)} · válido até ${dataBr(vale.validoAte)}` : `Vale ${vale.situacao.toLowerCase()}`}
        </span>
      )}
      {erro && <span className="text-danger">{erro}</span>}
    </div>
  )
}

function SituacaoDoCrediario({ clienteId }: { clienteId: string | null }) {
  const { getJson } = useApi()
  const [situacao, setSituacao] = useState<{ id: string; disponivel: number; bloqueado: boolean; motivo: string | null } | null>(null)

  useEffect(() => {
    if (!clienteId) return
    const controle = new AbortController()
    getJson<{ disponivel: number; bloqueado: boolean; motivo: string | null }>(`/api/financeiro/crediario/clientes/${clienteId}`, controle.signal)
      .then((s) => setSituacao({ id: clienteId, ...s }))
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, clienteId])

  if (!clienteId) return <p className="pl-1 text-sm text-warning">Escolha um cliente cadastrado para vender no crediário.</p>
  if (!situacao || situacao.id !== clienteId) return null
  return situacao.bloqueado ? (
    <p className="pl-1 text-sm text-danger">{situacao.motivo}</p>
  ) : (
    <p className="pl-1 text-sm text-success">{`Crediário disponível: ${moeda(situacao.disponivel)}`}</p>
  )
}
