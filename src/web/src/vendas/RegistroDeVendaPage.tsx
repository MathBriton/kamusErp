import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { decimal, lerNumero, moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BuscaDeProduto } from '../estoque/BuscaDeProduto'
import { MatrizDeEstoque } from '../estoque/MatrizDeEstoque'
import type { Matriz } from '../estoque/tipos'
import { useLojas } from '../estoque/useLojas'
import { hojeIso } from '../financeiro/tipos'
import { centavos, PERFIS_QUE_VENDEM, type FormaDePagamento, type Venda } from './tipos'
import { useFormas, useVendedores } from './useVendas'

interface Item {
  skuId: string
  descricao: string
  preco: number
  quantidade: string
  desconto: string
}

/** Valor nulo = automático: cobre o que falta para fechar o total. */
interface Pagamento {
  formaId: string
  valor: string | null
  parcelas: number
}

interface PorCodigo {
  produto: { descricao: string }
  sku: { id: string; tamanho: string; preco: number; ativo: boolean }
  cor: string
}

interface PessoaResumo {
  id: string
  nome: string
}

const inteiro = (texto: string) => Math.max(0, Number.parseInt(texto, 10) || 0)
const valorDe = (texto: string) => Math.max(0, lerNumero(texto) ?? 0)

/** Venda no backoffice: itens pelo leitor ou pela grade, descontos, vendedor, cliente e uma ou mais formas de pagamento (com troco). */
export function RegistroDeVendaPage() {
  const { getJson, enviar } = useApi()
  const { estado } = useAuth()
  const { perfilAtual, consolidado } = useEmpresa()
  const usuarioId = estado.status === 'autenticado' ? estado.sessao.usuario.id : null
  const lojas = useLojas()
  const ativas = lojas?.filter((l) => l.ativa) ?? []
  const vendedores = useVendedores()?.filter((v) => v.ativo) ?? []
  const formas = useFormas()?.filter((f) => f.ativa) ?? []
  const [lojaId, setLojaId] = useState('')
  const [vendedorId, setVendedorId] = useState<string | null>(null)
  const [cliente, setCliente] = useState<{ id: string | null; nome: string }>({ id: null, nome: '' })
  const [sugestoes, setSugestoes] = useState<PessoaResumo[]>([])
  const [data, setData] = useState(hojeIso())
  const [notaExterna, setNotaExterna] = useState('')
  const [observacao, setObservacao] = useState('')
  const [codigo, setCodigo] = useState('')
  const [matriz, setMatriz] = useState<Matriz | null>(null)
  const [digitado, setDigitado] = useState<Record<string, string>>({})
  const [itens, setItens] = useState<Item[]>([])
  const [descontoGeral, setDescontoGeral] = useState('')
  const [pagamentos, setPagamentos] = useState<Pagamento[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [registrada, setRegistrada] = useState<Venda | null>(null)
  const leitor = useRef<HTMLInputElement>(null)

  const lojaEscolhida = lojaId || (ativas.length === 1 ? ativas[0]!.id : '')
  const vendedorDoUsuario = vendedores.find((v) => v.usuarioId === usuarioId)?.id ?? ''
  const vendedorEscolhido = vendedorId ?? vendedorDoUsuario
  const formaPadrao = formas.find((f) => f.tipo === 'Dinheiro') ?? formas[0]
  const linhasDePagamento: Pagamento[] = pagamentos.length > 0 || !formaPadrao ? pagamentos : [{ formaId: formaPadrao.id, valor: null, parcelas: 1 }]

  useEffect(() => {
    const termo = cliente.nome.trim()
    if (cliente.id || termo.length < 2) return
    const controle = new AbortController()
    const timer = window.setTimeout(() => {
      getJson<{ itens: PessoaResumo[] }>(`/api/pessoas?${new URLSearchParams({ papel: 'cliente', situacao: 'ativos', busca: termo })}`, controle.signal)
        .then((r) => setSugestoes(r.itens.slice(0, 6)))
        .catch(() => {})
    }, 250)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, cliente])

  const totalDoItem = (i: Item) => centavos(inteiro(i.quantidade) * i.preco - valorDe(i.desconto))
  const subtotal = centavos(itens.reduce((s, i) => s + totalDoItem(i), 0))
  const desconto = valorDe(descontoGeral)
  const total = centavos(subtotal - desconto)
  const informado = (p: Pagamento) => (p.valor === null ? 0 : valorDe(p.valor))
  const fixos = linhasDePagamento.reduce((s, p) => s + informado(p), 0)
  const valores = linhasDePagamento.map((p) => (p.valor === null ? Math.max(0, centavos(total - fixos)) : informado(p)))
  const pago = centavos(valores.reduce((s, v) => s + v, 0))
  const falta = centavos(total - pago)

  if (!PERFIS_QUE_VENDEM.includes(perfilAtual) || consolidado)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Registrar venda</h1>
        <p className={`mt-4 ${estilos.erro}`}>
          {consolidado ? 'Escolha uma empresa no seletor do topo para registrar vendas.' : 'Só Dono, Gerente e Vendedor registram vendas.'}
        </p>
      </div>
    )

  const somar = (novos: Item[]) =>
    setItens((atual) => {
      const lista = [...atual]
      for (const n of novos) {
        const existente = lista.findIndex((i) => i.skuId === n.skuId)
        if (existente >= 0) lista[existente] = { ...lista[existente]!, quantidade: String(inteiro(lista[existente]!.quantidade) + inteiro(n.quantidade)) }
        else lista.push(n)
      }
      return lista
    })

  async function ler(e: { preventDefault: () => void }) {
    e.preventDefault()
    const valor = codigo.trim()
    if (!valor) return
    setCodigo('')
    setErro(null)
    try {
      const r = await getJson<PorCodigo>(`/api/produtos/por-codigo/${encodeURIComponent(valor)}`)
      if (!r.sku.ativo) setErro(`${r.produto.descricao} ${r.cor} ${r.sku.tamanho} está inativo no catálogo.`)
      else somar([{ skuId: r.sku.id, descricao: `${r.produto.descricao} · ${r.cor} ${r.sku.tamanho}`, preco: r.sku.preco, quantidade: '1', desconto: '' }])
    } catch {
      setErro(`Nenhum produto com o código ${valor}.`)
    } finally {
      leitor.current?.focus()
    }
  }

  const escolher = (produtoId: string) => {
    setErro(null)
    getJson<Matriz>(`/api/estoque/posicao/${produtoId}?lojaId=${lojaEscolhida}`)
      .then((m) => {
        setMatriz(m)
        setDigitado({})
      })
      .catch((ex: unknown) => setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível abrir o produto.'))
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
      setErro('Digite ao menos uma quantidade na grade.')
      return
    }
    somar(novos)
    setMatriz(null)
    setErro(null)
  }

  const mudarItem = (skuId: string, campo: 'quantidade' | 'desconto', valor: string) =>
    setItens((atual) => atual.map((i) => (i.skuId === skuId ? { ...i, [campo]: campo === 'quantidade' ? valor.replace(/\D/g, '') : valor } : i)))

  const mudarPagamento = (indice: number, mudanca: Partial<Pagamento>) =>
    setPagamentos(linhasDePagamento.map((p, i) => (i === indice ? { ...p, ...mudanca } : p)))

  const adicionarPagamento = () => {
    const outra = formas.find((f) => !linhasDePagamento.some((p) => p.formaId === f.id)) ?? formas[0]
    if (!outra) return
    // Congela os valores atuais e deixa a nova forma cobrir o que falta.
    setPagamentos([...linhasDePagamento.map((p, i) => ({ ...p, valor: decimal(valores[i] ?? 0) })), { formaId: outra.id, valor: null, parcelas: 1 }])
  }

  const forma = (id: string): FormaDePagamento | undefined => formas.find((f) => f.id === id)

  async function registrar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      const venda = await enviar<Venda>('POST', '/api/vendas', {
        lojaId: lojaEscolhida,
        data,
        clienteId: cliente.id,
        clienteNome: cliente.nome.trim() || null,
        vendedorId: vendedorEscolhido || null,
        descontoGeral: desconto,
        notaExterna: notaExterna.trim() || null,
        observacao: observacao.trim() || null,
        itens: itens.map((i) => ({ skuId: i.skuId, quantidade: inteiro(i.quantidade), desconto: valorDe(i.desconto) })),
        pagamentos: linhasDePagamento.map((p, i) => ({ formaId: p.formaId, valor: valores[i] ?? 0, parcelas: p.parcelas })),
      })
      setRegistrada(venda)
      setItens([])
      setPagamentos([])
      setDescontoGeral('')
      setCliente({ id: null, nome: '' })
      setNotaExterna('')
      setObservacao('')
      setData(hojeIso())
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível registrar a venda. Tente de novo.')
    } finally {
      setEnviando(false)
    }
  }

  const podeRegistrar = itens.length > 0 && itens.every((i) => inteiro(i.quantidade) > 0) && !!lojaEscolhida && falta <= 0 && total >= 0 && linhasDePagamento.length > 0

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Vendas</p>
      <h1 className="text-2xl font-bold tracking-tight">Registrar venda</h1>
      <p className="mt-1 text-sm text-muted">A nota fiscal continua saindo no sistema atual da loja: aqui ficam o estoque, o caixa, os recebíveis e a comissão.</p>

      {registrada && (
        <div role="status" className="mt-4 space-y-1 rounded-lg bg-success-soft px-3 py-2.5 text-sm text-success">
          <p className="flex flex-wrap items-center gap-2 font-semibold">
            <Icone nome="check" />
            {`Venda nº ${registrada.numero} registrada: ${moeda(registrada.total)}`}
            {registrada.troco > 0 && <span>{`· Troco: ${moeda(registrada.troco)}`}</span>}
            <Link to="/vendas/realizadas" className="font-normal underline">Ver vendas realizadas</Link>
          </p>
          {registrada.avisos.map((a) => (
            <p key={a} className="text-warning">{a}</p>
          ))}
        </div>
      )}

      <form onSubmit={registrar} className="mt-6 space-y-6">
        <section className="grid gap-4 rounded-2xl border border-border bg-surface p-5 shadow-sm sm:grid-cols-3">
          <Campo rotulo="Loja">
            <select value={lojaEscolhida} onChange={(e) => setLojaId(e.target.value)} disabled={itens.length > 0} className={estilos.campo}>
              <option value="">Escolha...</option>
              {ativas.map((l) => (
                <option key={l.id} value={l.id}>{l.nome}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Vendedor">
            <select value={vendedorEscolhido} onChange={(e) => setVendedorId(e.target.value)} className={estilos.campo}>
              <option value="">Sem vendedor</option>
              {vendedores.map((v) => (
                <option key={v.id} value={v.id}>{`${v.nome} (${decimal(v.comissaoPercentual).replace(',00', '')}%)`}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Data">
            <input type="date" value={data} max={hojeIso()} onChange={(e) => setData(e.target.value)} className={estilos.campo} />
          </Campo>
          <div className="relative sm:col-span-2">
            <Campo rotulo="Cliente (opcional)">
              <input
                value={cliente.nome}
                onChange={(e) => {
                  setCliente({ id: null, nome: e.target.value })
                  if (e.target.value.trim().length < 2) setSugestoes([])
                }}
                maxLength={150}
                placeholder="Buscar no cadastro ou digitar o nome"
                className={estilos.campo}
              />
            </Campo>
            {sugestoes.length > 0 && !cliente.id && (
              <ul className="absolute z-10 mt-1 w-full overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
                {sugestoes.map((p) => (
                  <li key={p.id}>
                    <button
                      type="button"
                      onClick={() => {
                        setCliente({ id: p.id, nome: p.nome })
                        setSugestoes([])
                      }}
                      className="w-full px-3 py-2 text-left text-sm hover:bg-surface-2"
                    >
                      {p.nome}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
          <Campo rotulo="Nota externa (opcional)">
            <input value={notaExterna} onChange={(e) => setNotaExterna(e.target.value)} maxLength={60} placeholder="Ex.: NFC-e 1234" className={estilos.campo} />
          </Campo>
        </section>

        <section className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
          <h2 className="font-semibold">Itens</h2>
          {!matriz && (
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <input
                  ref={leitor}
                  aria-label="Código de barras"
                  autoFocus
                  value={codigo}
                  onChange={(e) => setCodigo(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') void ler(e)
                  }}
                  disabled={!lojaEscolhida}
                  inputMode="numeric"
                  placeholder="Passe o leitor (cada leitura soma 1)"
                  className={estilos.campo}
                />
              <BuscaDeProduto onEscolher={escolher} desabilitado={!lojaEscolhida} placeholder="Ou busque o produto e escolha na grade" />
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
                <button type="button" onClick={adicionarDaGrade} className={estilos.botaoPrimario}>Adicionar à venda</button>
              </div>
            </div>
          )}

          {itens.length > 0 && (
            <table aria-label="Itens da venda" className="mt-4 w-full text-sm">
              <thead className="text-left text-xs text-muted uppercase">
                <tr>
                  <th className="py-2">Produto</th>
                  <th className="w-20 py-2 text-right">Qtd.</th>
                  <th className="py-2 text-right">Preço</th>
                  <th className="w-28 py-2 text-right">Desconto (R$)</th>
                  <th className="py-2 text-right">Total</th>
                  <th />
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {itens.map((i) => (
                  <tr key={i.skuId}>
                    <td className="py-2 font-semibold">{i.descricao}</td>
                    <td className="py-2">
                      <input
                        aria-label={`Quantidade de ${i.descricao}`}
                        value={i.quantidade}
                        onChange={(e) => mudarItem(i.skuId, 'quantidade', e.target.value)}
                        inputMode="numeric"
                        className="h-9 w-full rounded-lg border border-border bg-surface px-2 text-right tabular-nums"
                      />
                    </td>
                    <td className="py-2 text-right tabular-nums">{moeda(i.preco)}</td>
                    <td className="py-2 pl-2">
                      <input
                        aria-label={`Desconto de ${i.descricao}`}
                        value={i.desconto}
                        onChange={(e) => mudarItem(i.skuId, 'desconto', e.target.value)}
                        inputMode="decimal"
                        placeholder="0,00"
                        className="h-9 w-full rounded-lg border border-border bg-surface px-2 text-right tabular-nums"
                      />
                    </td>
                    <td className="py-2 text-right font-semibold tabular-nums">{moeda(totalDoItem(i))}</td>
                    <td className="py-2 text-right">
                      <button type="button" onClick={() => setItens((atual) => atual.filter((x) => x.skuId !== i.skuId))} className="text-xs text-danger hover:underline">
                        Remover
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        {itens.length > 0 && (
          <section className="grid gap-6 rounded-2xl border border-border bg-surface p-5 shadow-sm md:grid-cols-2">
            <div className="space-y-3">
              <h2 className="font-semibold">Pagamento</h2>
              {linhasDePagamento.map((p, i) => {
                const f = forma(p.formaId)
                return (
                  <div key={i} className="flex flex-wrap items-end gap-2">
                    <select
                      aria-label={`Forma do pagamento ${i + 1}`}
                      value={p.formaId}
                      onChange={(e) => mudarPagamento(i, { formaId: e.target.value, parcelas: 1 })}
                      className={`${estilos.campo} min-w-40 flex-1`}
                    >
                      {formas.map((x) => (
                        <option key={x.id} value={x.id}>{x.nome}</option>
                      ))}
                    </select>
                    <input
                      aria-label={`Valor do pagamento ${i + 1}`}
                      value={p.valor ?? decimal(valores[i] ?? 0)}
                      onChange={(e) => mudarPagamento(i, { valor: e.target.value })}
                      inputMode="decimal"
                      className={`${estilos.campo} w-28 text-right`}
                    />
                    {f && f.maxParcelas > 1 && (
                      <select
                        aria-label={`Parcelas do pagamento ${i + 1}`}
                        value={p.parcelas}
                        onChange={(e) => mudarPagamento(i, { parcelas: Number(e.target.value) })}
                        className={`${estilos.campo} w-20`}
                      >
                        {Array.from({ length: f.maxParcelas }, (_, n) => n + 1).map((n) => (
                          <option key={n} value={n}>{`${n}x`}</option>
                        ))}
                      </select>
                    )}
                    {linhasDePagamento.length > 1 && (
                      <button type="button" onClick={() => setPagamentos(linhasDePagamento.filter((_, x) => x !== i))} className="h-10 text-xs text-danger hover:underline">
                        Remover
                      </button>
                    )}
                  </div>
                )
              })}
              {formas.length === 0 && (
                <p className="text-sm text-muted">Nenhuma forma de pagamento ativa. Dono ou Gerente cadastram em Vendas → Formas de pagamento.</p>
              )}
              {formas.length > 0 && (
                <button type="button" onClick={adicionarPagamento} className={estilos.botao}>Adicionar forma de pagamento</button>
              )}
              <Campo rotulo="Observação (opcional)">
                <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={500} className={estilos.campo} />
              </Campo>
            </div>

            <div className="space-y-3">
              <Campo rotulo="Desconto na venda (R$)" className="max-w-48">
                <input value={descontoGeral} onChange={(e) => setDescontoGeral(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
              </Campo>
              <dl className="space-y-1 rounded-xl bg-surface-2 p-4 text-sm">
                {subtotal !== total && (
                  <>
                    <div className="flex justify-between"><dt className="text-muted">Subtotal</dt><dd className="tabular-nums">{moeda(subtotal)}</dd></div>
                    <div className="flex justify-between"><dt className="text-muted">Desconto</dt><dd className="tabular-nums">{`− ${moeda(desconto)}`}</dd></div>
                  </>
                )}
                <div className="flex justify-between text-lg font-bold"><dt>Total</dt><dd className="tabular-nums">{moeda(total)}</dd></div>
              </dl>
              {falta > 0 && <p className="font-semibold text-danger">{`Falta: ${moeda(falta)}`}</p>}
              {falta < 0 && <p className="font-semibold text-success">{`Troco: ${moeda(-falta)}`}</p>}
            </div>
          </section>
        )}

        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}

        <div className="flex justify-end">
          <button type="submit" disabled={!podeRegistrar || enviando} className={estilos.botaoPrimario}>
            {enviando ? 'Registrando...' : 'Registrar venda'}
          </button>
        </div>
      </form>
    </div>
  )
}
