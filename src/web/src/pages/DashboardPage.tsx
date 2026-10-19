import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { moeda } from '../cadastros/catalogo'
import { Icone, type NomeIcone } from '../components/Icone'
import { ResumoDiarioDialogo } from '../dashboard/ResumoDiarioDialogo'
import { PERIODOS, variacao, type Cartao, type CodigoDoPeriodo, type Compromissos, type Dashboard, type VendasDoDia } from '../dashboard/tipos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { dataBr } from '../financeiro/tipos'
import { filtrarMenu, MENU, todasAsRotas } from '../navegacao/menu'

interface EmpresaAtivaResposta {
  consolidado: boolean
  empresas: { id: string; nomeFantasia: string; cnpj: string }[]
}

const PRIMEIROS_PASSOS = [
  { titulo: 'Cadastrar produtos', descricao: 'Com grade de cor × tamanho, ou importando sua planilha.', rota: '/cadastros/produtos' },
  { titulo: 'Cadastrar clientes', descricao: 'Ficha com tamanhos preferidos e WhatsApp em 1 clique.', rota: '/cadastros/clientes' },
  { titulo: 'Dar entrada no estoque', descricao: 'Digite na matriz de grade ou importe o XML da nota.', rota: '/estoque/entradas' },
  { titulo: 'Lançar contas a pagar', descricao: 'Fornecedores, aluguel e despesas fixas.', rota: '/financeiro/contas-a-pagar' },
  { titulo: 'Registrar a primeira venda', descricao: 'Baixa o estoque e lança no financeiro automaticamente.', rota: '/vendas/registro' },
]

const pecas = (n: number) => `${n} ${n === 1 ? 'peça' : 'peças'}`
const vendasTexto = (n: number) => `${n} ${n === 1 ? 'venda' : 'vendas'}`

export function DashboardPage() {
  const { estado } = useAuth()
  const { selecao, perfilAtual } = useEmpresa()
  const { getJson } = useApi()
  const [{ contexto, erro }, setResultado] = useState<{ contexto: EmpresaAtivaResposta | null; erro: string | null }>({ contexto: null, erro: null })
  const [periodo, setPeriodo] = useState<CodigoDoPeriodo>('hoje')
  const [{ dados, carregado }, setDados] = useState<{ dados: Dashboard | null; carregado: boolean }>({ dados: null, carregado: false })
  const [resumoAberto, setResumoAberto] = useState(false)

  useEffect(() => {
    const controle = new AbortController()
    getJson<EmpresaAtivaResposta>('/api/empresas/ativa', controle.signal)
      .then((resposta) => setResultado({ contexto: resposta, erro: null }))
      .catch((e: unknown) => {
        if (controle.signal.aborted) return
        setResultado({ contexto: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar a empresa ativa.' })
      })
    return () => controle.abort()
  }, [getJson, selecao])

  useEffect(() => {
    const controle = new AbortController()
    getJson<Dashboard>(`/api/indicadores?periodo=${periodo}`, controle.signal)
      .then((d) => setDados({ dados: d, carregado: true }))
      .catch(() => {
        if (!controle.signal.aborted) setDados({ dados: null, carregado: true })
      })
    return () => controle.abort()
  }, [getJson, selecao, periodo])

  const rotasPermitidas = useMemo(() => new Set(todasAsRotas(filtrarMenu(MENU, perfilAtual)).map((r) => r.rota)), [perfilAtual])
  const passos = PRIMEIROS_PASSOS.filter((p) => rotasPermitidas.has(p.rota))

  const primeiroNome = estado.status === 'autenticado' ? estado.sessao.usuario.nome.split(' ')[0] : ''
  const data = new Intl.DateTimeFormat('pt-BR', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date())
  const hoje = data.charAt(0).toUpperCase() + data.slice(1) // "Quarta-feira, 14 de outubro"

  const semDados =
    carregado && (!dados || ((dados.vendas?.ultimos30Dias.every((d) => d.vendas === 0) ?? true) && (dados.estoque?.pecas ?? 0) === 0))

  return (
    <div className="mx-auto max-w-7xl">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs text-muted">Início</p>
          <h1 className="text-2xl font-bold tracking-tight">Olá, {primeiroNome} 👋</h1>
          {contexto && (
            <p className="mt-1 text-sm text-muted">
              {contexto.consolidado
                ? `Visão consolidada · ${contexto.empresas.length} empresas (somente leitura)`
                : `${contexto.empresas[0]?.nomeFantasia} · ${contexto.empresas[0]?.cnpj}`}
            </p>
          )}
          <p className="text-xs text-muted">{hoje}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button type="button" onClick={() => setResumoAberto(true)} className="flex h-9 items-center gap-2 rounded-lg border border-border bg-surface px-3 text-sm font-semibold hover:bg-surface-2">
            <Icone nome="bell" />
            Resumo diário
          </button>
          <div role="radiogroup" aria-label="Período" className="inline-flex rounded-lg border border-border bg-surface-2 p-0.5 text-sm">
            {PERIODOS.map((p) => (
              <button
                key={p.valor}
                type="button"
                role="radio"
                aria-checked={periodo === p.valor}
                onClick={() => setPeriodo(p.valor)}
                className={`rounded-md px-3 py-1 font-medium ${periodo === p.valor ? 'bg-surface text-foreground shadow-sm' : 'text-muted hover:text-foreground'}`}
              >
                {p.rotulo}
              </button>
            ))}
          </div>
        </div>
      </div>

      {erro && (
        <p role="alert" className="mb-5 flex items-center gap-2 rounded-lg bg-danger-soft px-4 py-3 text-sm text-danger">
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {dados && <Cartoes dados={dados} periodo={periodo} />}

      <div className="grid gap-5 lg:grid-cols-3">
        <div className="space-y-5 lg:col-span-2">
          {dados?.vendas && <Grafico dias={dados.vendas.ultimos30Dias} />}
          {semDados && (
            <section aria-label="Primeiros passos" className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
              <h2 className="font-semibold">Primeiros passos</h2>
              <p className="mt-1 text-sm text-muted">Configure sua loja nesta ordem. Os indicadores acendem conforme os dados entram.</p>
              <ol className="mt-4 divide-y divide-border">
                {passos.map((passo, i) => (
                  <li key={passo.rota}>
                    <Link to={passo.rota} className="group flex items-center gap-3 rounded-lg py-3 hover:bg-surface-2">
                      <span className="grid size-8 shrink-0 place-items-center rounded-full border border-border text-sm font-semibold text-muted">{i + 1}</span>
                      <span className="flex-1">
                        <span className="block text-sm font-semibold">{passo.titulo}</span>
                        <span className="block text-xs text-muted">{passo.descricao}</span>
                      </span>
                      <Icone nome="arrowRight" className="size-4 text-muted group-hover:text-primary" />
                    </Link>
                  </li>
                ))}
              </ol>
            </section>
          )}
          {dados?.vendas && (dados.vendas.maisVendidos.length > 0 || dados.vendas.porForma.length > 0) && (
            <div className="grid gap-5 md:grid-cols-2">
              <Bloco titulo="Mais vendidos">
                {dados.vendas.maisVendidos.length === 0 ? (
                  <p className="text-sm text-muted">Nenhuma venda no período.</p>
                ) : (
                  <ol className="space-y-2 text-sm">
                    {dados.vendas.maisVendidos.map((m) => (
                      <li key={m.skuId} className="flex items-baseline justify-between gap-3">
                        <span className="font-medium">{m.descricao}</span>
                        <span className="shrink-0 text-muted tabular-nums">{`${pecas(m.pecas)} · ${moeda(m.total)}`}</span>
                      </li>
                    ))}
                  </ol>
                )}
              </Bloco>
              <Bloco titulo="Formas de pagamento">
                <Lista itens={dados.vendas.porForma} />
                {!dados.vendas.somenteAsMinhas && dados.vendas.porVendedor.length > 0 && (
                  <>
                    <h3 className="mt-4 mb-2 text-xs font-semibold text-muted uppercase">Por vendedor</h3>
                    <Lista itens={dados.vendas.porVendedor} />
                  </>
                )}
              </Bloco>
            </div>
          )}
        </div>

        <div className="space-y-5">
          <section aria-label="Alertas" className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
            <h2 className="font-semibold">Alertas</h2>
            {dados && dados.alertas.length > 0 ? (
              <ul className="mt-3 space-y-2">
                {dados.alertas.map((a) => (
                  <li key={a.tipo}>
                    <Link to={a.link} className={`block rounded-lg px-3 py-2 text-sm hover:opacity-90 ${a.gravidade === 'perigo' ? 'bg-danger-soft text-danger' : 'bg-warning-soft text-warning'}`}>
                      <span className="block font-semibold">{a.titulo}</span>
                      <span className="block text-xs">{a.detalhe}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            ) : (
              <div className="mt-6 flex flex-col items-center text-center">
                <span className="grid size-12 place-items-center rounded-full bg-success-soft text-success">
                  <Icone nome="check" className="size-5" />
                </span>
                <p className="mt-3 text-sm font-medium">Sem alertas por enquanto</p>
                <p className="mt-1 text-xs text-muted">Contas vencidas, condicionais atrasadas e quebra de grade aparecem aqui.</p>
              </div>
            )}
          </section>
          {dados?.financeiro && dados.financeiro.contas.length > 0 && (
            <Bloco titulo="Saldo das contas">
              <ul className="space-y-1.5 text-sm">
                {dados.financeiro.contas.map((c) => (
                  <li key={c.id} className="flex justify-between gap-3">
                    <span>{c.nome}</span>
                    <span className="font-semibold tabular-nums">{moeda(c.saldo)}</span>
                  </li>
                ))}
              </ul>
              {dados.financeiro.contas.length > 1 && (
                <p className="mt-3 flex justify-between border-t border-border pt-2 text-sm font-bold">
                  <span>Total</span>
                  <span className="tabular-nums">{moeda(dados.financeiro.saldoTotal)}</span>
                </p>
              )}
            </Bloco>
          )}
          {dados?.condicionais && dados.condicionais.abertas > 0 && (
            <Bloco titulo="Condicionais na rua">
              <p className="text-2xl font-bold tabular-nums">{moeda(dados.condicionais.valor)}</p>
              <p className="text-sm text-muted">{`${dados.condicionais.abertas} abertas · ${pecas(dados.condicionais.pecas)}`}</p>
            </Bloco>
          )}
          {dados?.estoque && (
            <Bloco titulo="Estoque">
              <p className="text-2xl font-bold tabular-nums">{moeda(dados.estoque.valorDeCusto)}</p>
              <p className="text-sm text-muted">{`${pecas(dados.estoque.pecas)} a custo · ${moeda(dados.estoque.valorDeVenda)} a preço de venda`}</p>
            </Bloco>
          )}
        </div>
      </div>

      {resumoAberto && <ResumoDiarioDialogo onFechar={() => setResumoAberto(false)} />}
    </div>
  )
}

function Cartoes({ dados, periodo }: { dados: Dashboard; periodo: CodigoDoPeriodo }) {
  const info = PERIODOS.find((p) => p.valor === periodo)!
  const cartoes: ReactNode[] = []
  const v = dados.vendas
  if (v) {
    const prefixo = v.somenteAsMinhas ? 'Minhas vendas' : 'Vendas'
    cartoes.push(<CartaoDeVendas key="periodo" titulo={`${prefixo} ${info.titulo}`} c={v.periodo} anterior={info.anterior} icone="bag" tom="bg-primary-soft text-primary" />)
    cartoes.push(
      periodo === 'mes' ? (
        <Indicador key="ticket" titulo="Ticket médio no mês" icone="trendingUp" tom="bg-success-soft text-success" valor={moeda(v.mes.ticketMedio)} linhas={[`${pecas(v.mes.pecas)} vendidas`]} />
      ) : (
        <CartaoDeVendas key="mes" titulo={`${prefixo} no mês`} c={v.mes} anterior="mês anterior" icone="trendingUp" tom="bg-success-soft text-success" />
      ),
    )
  }
  const f = dados.financeiro
  if (f) {
    cartoes.push(<Compromisso key="receber" titulo="A receber" c={f.aReceber} icone="wallet" tom="bg-info-soft text-info" />)
    cartoes.push(<Compromisso key="pagar" titulo="A pagar" c={f.aPagar} icone="alert" tom="bg-warning-soft text-warning" />)
  }
  if (!v && !f && dados.estoque)
    cartoes.push(
      <Indicador key="pecas" titulo="Peças em estoque" icone="box" tom="bg-primary-soft text-primary" valor={String(dados.estoque.pecas)}
        linhas={[`${dados.estoque.totalDeQuebras} produtos com quebra de grade`]} />,
    )
  if (cartoes.length === 0) return null
  return <div className="mb-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">{cartoes}</div>
}

function Indicador({ titulo, icone, tom, valor, linhas, destaque }: { titulo: string; icone: NomeIcone; tom: string; valor: string; linhas: string[]; destaque?: ReactNode }) {
  return (
    <article className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
      <div className="flex items-center gap-2 text-[13px] font-medium text-muted">
        <span className={`grid size-8 place-items-center rounded-lg ${tom}`}>
          <Icone nome={icone} />
        </span>
        {titulo}
      </div>
      <p className="mt-3 text-2xl font-bold tabular-nums">{valor}</p>
      {destaque}
      {linhas.map((l) => (
        <p key={l} className="mt-1 text-xs text-muted">{l}</p>
      ))}
    </article>
  )
}

function CartaoDeVendas({ titulo, c, anterior, icone, tom }: { titulo: string; c: Cartao; anterior: string; icone: NomeIcone; tom: string }) {
  const comparacao =
    c.variacao === null ? (
      <p className="mt-1 text-xs text-muted">{c.totalAnterior === 0 ? `Sem vendas no ${anterior === 'ontem' ? 'dia anterior' : 'período anterior'}` : ''}</p>
    ) : (
      <p className={`mt-1 text-xs font-semibold ${c.variacao >= 0 ? 'text-success' : 'text-danger'}`}>{`${variacao(c.variacao)} vs ${anterior}`}</p>
    )
  return <Indicador titulo={titulo} icone={icone} tom={tom} valor={moeda(c.total)} destaque={comparacao} linhas={[`${vendasTexto(c.vendas)} · ticket ${moeda(c.ticketMedio)}`]} />
}

function Compromisso({ titulo, c, icone, tom }: { titulo: string; c: Compromissos; icone: NomeIcone; tom: string }) {
  return (
    <Indicador
      titulo={titulo}
      icone={icone}
      tom={tom}
      valor={moeda(c.proximosSeteDias)}
      destaque={c.vencido > 0 ? <p className="mt-1 text-xs font-semibold text-danger">{`${moeda(c.vencido)} vencido`}</p> : undefined}
      linhas={['Próximos 7 dias', `Hoje ${moeda(c.hoje)}`]}
    />
  )
}

function Bloco({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <section aria-label={titulo} className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
      <h2 className="mb-3 font-semibold">{titulo}</h2>
      {children}
    </section>
  )
}

function Lista({ itens }: { itens: { nome: string; quantidade: number; total: number }[] }) {
  if (itens.length === 0) return <p className="text-sm text-muted">Nenhuma venda no período.</p>
  const maior = Math.max(...itens.map((i) => i.total), 1)
  return (
    <ul className="space-y-2 text-sm">
      {itens.map((i) => (
        <li key={i.nome}>
          <div className="flex justify-between gap-3">
            <span>{i.nome}</span>
            <span className="font-semibold tabular-nums">{moeda(i.total)}</span>
          </div>
          <div className="mt-1 h-1.5 rounded-full bg-surface-2">
            <div className="h-1.5 rounded-full bg-primary" style={{ width: `${(i.total / maior) * 100}%` }} />
          </div>
        </li>
      ))}
    </ul>
  )
}

/** Barras dos últimos 30 dias (SVG simples, sem biblioteca). */
function Grafico({ dias }: { dias: VendasDoDia[] }) {
  const total = dias.reduce((s, d) => s + d.total, 0)
  const maior = Math.max(...dias.map((d) => d.total), 1)
  const largura = 100 / dias.length
  return (
    <section aria-label="Vendas — últimos 30 dias" className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
      <div className="mb-3 flex items-baseline justify-between gap-3">
        <h2 className="font-semibold">Vendas — últimos 30 dias</h2>
        <span className="text-sm text-muted tabular-nums">{moeda(total)}</span>
      </div>
      <svg role="img" aria-label={`Vendas dos últimos 30 dias: ${moeda(total)}`} viewBox="0 0 100 40" preserveAspectRatio="none" className="h-40 w-full">
        {dias.map((d, i) => {
          const altura = (d.total / maior) * 38
          return (
            <rect key={d.data} x={i * largura + largura * 0.15} y={40 - altura} width={largura * 0.7} height={Math.max(altura, d.total > 0 ? 0.6 : 0.2)} className={d.total > 0 ? 'fill-primary' : 'fill-border'}>
              <title>{`${dataBr(d.data)}: ${moeda(d.total)} (${vendasTexto(d.vendas)})`}</title>
            </rect>
          )
        })}
      </svg>
      <div className="mt-1 flex justify-between text-xs text-muted">
        <span>{dataBr(dias[0]!.data)}</span>
        <span>{dataBr(dias[dias.length - 1]!.data)}</span>
      </div>
    </section>
  )
}
