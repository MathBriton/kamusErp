import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { estilos } from '../components/estilos'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { useLojas } from '../estoque/useLojas'
import { hojeIso } from '../financeiro/tipos'
import { EditorDePagamentos } from './EditorDePagamentos'
import { EntradaDeItens } from './EntradaDeItens'
import { calcularPagamentos, corpoDosPagamentos, inteiro, novaLinha, somaDosItens, valorDe, type ItemDigitado, type PagamentoDigitado } from './itens'
import { centavos, PERFIS_DE_GESTAO, PERFIS_QUE_VENDEM, porcentagem, precisaDeConta, prontaParaVender, type Venda } from './tipos'
import { useFormas, useVendedores } from './useVendas'

interface PessoaResumo {
  id: string
  nome: string
}

/** O que está sendo digitado: fica guardado para a venda não se perder ao sair da tela (ex.: para configurar o pagamento). */
interface Rascunho {
  lojaId: string
  vendedorId: string | null
  cliente: { id: string | null; nome: string }
  data: string
  notaExterna: string
  observacao: string
  itens: ItemDigitado[]
  descontoGeral: string
  pagamentos: PagamentoDigitado[]
}

const lerRascunho = (chave: string): Rascunho | null => {
  try {
    const texto = localStorage.getItem(chave)
    return texto ? (JSON.parse(texto) as Rascunho) : null
  } catch {
    return null
  }
}

const extenso = (iso: string) =>
  new Intl.DateTimeFormat('pt-BR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${iso}T12:00:00Z`))

/** Venda no backoffice: itens pelo leitor ou pela grade, descontos, vendedor, cliente e uma ou mais formas de pagamento (com troco). */
export function RegistroDeVendaPage() {
  const { getJson, enviar } = useApi()
  const { estado } = useAuth()
  const { perfilAtual, consolidado, selecao } = useEmpresa()
  const usuarioId = estado.status === 'autenticado' ? estado.sessao.usuario.id : null
  const chaveDoRascunho = `kamus.rascunho-venda.${usuarioId}.${selecao}`
  const [rascunho] = useState(() => lerRascunho(chaveDoRascunho))
  const lojas = useLojas()
  const ativas = lojas?.filter((l) => l.ativa) ?? []
  const vendedores = useVendedores()?.filter((v) => v.ativo) ?? []
  const todasAsFormas = useFormas()
  const formas = todasAsFormas?.filter((f) => f.ativa) ?? []
  const prontas = formas.filter(prontaParaVender)
  const [lojaId, setLojaId] = useState(rascunho?.lojaId ?? '')
  const [vendedorId, setVendedorId] = useState<string | null>(rascunho?.vendedorId ?? null)
  const [cliente, setCliente] = useState<{ id: string | null; nome: string }>(rascunho?.cliente ?? { id: null, nome: '' })
  const [sugestoes, setSugestoes] = useState<PessoaResumo[]>([])
  const [data, setData] = useState(rascunho?.data ?? hojeIso())
  const [notaExterna, setNotaExterna] = useState(rascunho?.notaExterna ?? '')
  const [observacao, setObservacao] = useState(rascunho?.observacao ?? '')
  const [itens, setItens] = useState<ItemDigitado[]>(rascunho?.itens ?? [])
  const [descontoGeral, setDescontoGeral] = useState(rascunho?.descontoGeral ?? '')
  const [pagamentos, setPagamentos] = useState<PagamentoDigitado[]>(rascunho?.pagamentos ?? [])
  const [recuperado, setRecuperado] = useState((rascunho?.itens.length ?? 0) > 0)
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [registrada, setRegistrada] = useState<Venda | null>(null)

  const lojaEscolhida = lojaId || (ativas.length === 1 ? ativas[0]!.id : '')
  const vendedorDoUsuario = vendedores.find((v) => v.usuarioId === usuarioId)?.id ?? ''
  const vendedorEscolhido = vendedorId ?? vendedorDoUsuario
  const formaPadrao = prontas.find((f) => f.tipo === 'Dinheiro') ?? prontas[0]
  const linhasDePagamento: PagamentoDigitado[] = pagamentos.length > 0 || !formaPadrao ? pagamentos : [novaLinha(formaPadrao)]

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

  useEffect(() => {
    try {
      if (itens.length === 0 && !notaExterna && !cliente.nome) localStorage.removeItem(chaveDoRascunho)
      else
        localStorage.setItem(
          chaveDoRascunho,
          JSON.stringify({ lojaId, vendedorId, cliente, data, notaExterna, observacao, itens, descontoGeral, pagamentos } satisfies Rascunho),
        )
    } catch {
      // Sem armazenamento (modo privado): a venda só não sobrevive à troca de tela.
    }
  }, [chaveDoRascunho, lojaId, vendedorId, cliente, data, notaExterna, observacao, itens, descontoGeral, pagamentos])

  const limparVenda = () => {
    setItens([])
    setPagamentos([])
    setDescontoGeral('')
    setCliente({ id: null, nome: '' })
    setNotaExterna('')
    setObservacao('')
    setData(hojeIso())
    setRecuperado(false)
  }

  const subtotal = somaDosItens(itens)
  const desconto = valorDe(descontoGeral)
  const total = centavos(subtotal - desconto)
  const { valores, falta } = calcularPagamentos(linhasDePagamento, total)

  if (!PERFIS_QUE_VENDEM.includes(perfilAtual) || consolidado)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Registrar venda</h1>
        <p className={`mt-4 ${estilos.erro}`}>
          {consolidado ? 'Escolha uma empresa no seletor do topo para registrar vendas.' : 'Só Dono, Gerente e Vendedor registram vendas.'}
        </p>
      </div>
    )

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
        pagamentos: corpoDosPagamentos(linhasDePagamento, valores),
      })
      setRegistrada(venda)
      limparVenda()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível registrar a venda. Tente de novo.')
    } finally {
      setEnviando(false)
    }
  }

  const podeRegistrar = itens.length > 0 && itens.every((i) => inteiro(i.quantidade) > 0) && !!lojaEscolhida && falta <= 0 && total >= 0 && linhasDePagamento.length > 0

  return (
    <div className="mx-auto max-w-5xl">
      <CabecalhoDaPagina
        secao="Vendas"
        titulo="Registrar venda"
        descricao="A nota fiscal continua saindo no sistema atual da loja: aqui ficam o estoque, o caixa, os recebíveis e a comissão."
        acoes={<Link to="/vendas/realizadas" className={estilos.botao}>Vendas realizadas</Link>}
      />

      {todasAsFormas && prontas.length === 0 && (
        <section aria-label="Antes de vender" className="mt-4 rounded-2xl border border-warning bg-warning-soft p-4 text-sm text-warning">
          <p className="font-semibold">Antes de vender, deixe pelo menos uma forma de pagamento pronta.</p>
          <ul className="mt-1 list-disc pl-5">
            {formas.length === 0 && <li>Nenhuma forma de pagamento ativa.</li>}
            {formas.filter((f) => precisaDeConta(f) && !f.contaId).map((f) => (
              <li key={f.id}>{`${f.nome} precisa de uma conta financeira para receber (ex.: Caixa da loja).`}</li>
            ))}
          </ul>
          {PERFIS_DE_GESTAO.includes(perfilAtual) ? (
            <Link to="/vendas/formas-de-pagamento" className="mt-2 inline-flex items-center gap-1 font-semibold underline">
              Configurar formas de pagamento
              <Icone nome="arrowRight" className="size-4" />
            </Link>
          ) : (
            <p className="mt-2">Peça ao Dono ou ao Gerente para configurar em Vendas → Formas de pagamento.</p>
          )}
        </section>
      )}

      {recuperado && itens.length > 0 && (
        <div role="status" aria-label="Rascunho" className="mt-4 flex flex-wrap items-center gap-3 rounded-lg bg-info-soft px-3 py-2.5 text-sm text-info">
          <span>Recuperamos a venda que estava em andamento.</span>
          <button type="button" onClick={limparVenda} className="font-semibold underline">Descartar rascunho</button>
        </div>
      )}

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
                <option key={v.id} value={v.id}>{`${v.nome} (${porcentagem(v.comissaoPercentual)})`}</option>
              ))}
            </select>
          </Campo>
          <div>
            <Campo rotulo="Data">
              <input type="date" value={data} max={hojeIso()} onChange={(e) => setData(e.target.value)} className={estilos.campo} />
            </Campo>
            {data && <p className="mt-1 text-xs text-muted first-letter:uppercase">{extenso(data)}</p>}
          </div>
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
          <EntradaDeItens lojaId={lojaEscolhida} itens={itens} onItens={setItens} onErro={setErro} rotulo="Itens da venda" botaoDaGrade="Adicionar à venda" />
        </section>

        {itens.length > 0 && (
          <section className="grid gap-6 rounded-2xl border border-border bg-surface p-5 shadow-sm md:grid-cols-2">
            <div className="space-y-3">
              <h2 className="font-semibold">Pagamento</h2>
              <EditorDePagamentos formas={formas} total={total} linhas={linhasDePagamento} onLinhas={setPagamentos} clienteId={cliente.id} />
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
