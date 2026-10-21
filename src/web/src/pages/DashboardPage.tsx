import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { moeda } from '../cadastros/catalogo'
import { Icone, type NomeIcone } from '../components/Icone'
import { useFecharAoClicarFora } from '../components/useFecharAoClicarFora'
import { AtendimentoWhatsApp } from '../dashboard/AtendimentoWhatsApp'
import { GraficoDeVendas } from '../dashboard/Grafico'
import { PrimeirosPassos } from '../dashboard/PrimeirosPassos'
import { ResumoDiarioDialogo } from '../dashboard/ResumoDiarioDialogo'
import { linkDoWhatsApp } from '../dashboard/suporte'
import { PERIODOS, variacao, type Cartao, type CodigoDoPeriodo, type Compromissos, type Dashboard } from '../dashboard/tipos'
import { UltimosMarcos } from '../dashboard/UltimosMarcos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { filtrarMenu, MENU, todasAsRotas } from '../navegacao/menu'

interface EmpresaAtivaResposta {
  consolidado: boolean
  empresas: { id: string; nomeFantasia: string; cnpj: string }[]
}

/** O dashboard se atualiza sozinho nesse intervalo, sem recarregar a página. */
const ATUALIZAR_A_CADA_MS = 60_000

const ACOES_RAPIDAS: { rotulo: string; rota: string; icone: NomeIcone }[] = [
  { rotulo: 'Novo produto', rota: '/cadastros/produtos', icone: 'shirt' },
  { rotulo: 'Novo cliente', rota: '/cadastros/clientes', icone: 'users' },
  { rotulo: 'Entrada de mercadoria', rota: '/estoque/entradas', icone: 'box' },
  { rotulo: 'Entrada por XML da NF-e', rota: '/estoque/xml', icone: 'file' },
  { rotulo: 'Lançar conta a pagar', rota: '/financeiro/contas-a-pagar', icone: 'wallet' },
  { rotulo: 'Importar planilha', rota: '/cadastros/importacao', icone: 'layers' },
]

const ATALHOS_DE_ESTOQUE = [
  { rotulo: 'Grades e cores', rota: '/cadastros/grades' },
  { rotulo: 'Etiquetas', rota: '/estoque/etiquetas' },
  { rotulo: 'Ajustes', rota: '/estoque/ajustes' },
]

const PERFIS_COM_HISTORICO = ['Dono', 'Gerente']

const pecas = (n: number) => `${n} ${n === 1 ? 'peça' : 'peças'}`
const vendasTexto = (n: number) => `${n} ${n === 1 ? 'venda' : 'vendas'}`
const hora = (d: Date) => d.toLocaleTimeString('pt-BR', { timeZone: 'America/Sao_Paulo', hour: '2-digit', minute: '2-digit' })

export function DashboardPage() {
  const { estado } = useAuth()
  const { selecao, perfilAtual } = useEmpresa()
  const { getJson } = useApi()
  const [{ contexto, erro }, setResultado] = useState<{ contexto: EmpresaAtivaResposta | null; erro: string | null }>({ contexto: null, erro: null })
  const [periodo, setPeriodo] = useState<CodigoDoPeriodo>('hoje')
  const [{ dados, carregado, atualizadoEm }, setDados] = useState<{ dados: Dashboard | null; carregado: boolean; atualizadoEm: Date | null }>({
    dados: null,
    carregado: false,
    atualizadoEm: null,
  })
  const [versao, setVersao] = useState(0)
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
    const timer = window.setInterval(() => setVersao((v) => v + 1), ATUALIZAR_A_CADA_MS)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    const controle = new AbortController()
    getJson<Dashboard>(`/api/indicadores?periodo=${periodo}`, controle.signal)
      .then((d) => setDados({ dados: d, carregado: true, atualizadoEm: new Date() }))
      .catch(() => {
        // Numa atualização automática que falhe, os números anteriores continuam na tela.
        if (!controle.signal.aborted) setDados((atual) => ({ ...atual, carregado: true }))
      })
    return () => controle.abort()
  }, [getJson, selecao, periodo, versao])

  const rotasPermitidas = useMemo(() => new Set(todasAsRotas(filtrarMenu(MENU, perfilAtual)).map((r) => r.rota)), [perfilAtual])
  const verHistorico = PERFIS_COM_HISTORICO.includes(perfilAtual)

  const primeiroNome = estado.status === 'autenticado' ? estado.sessao.usuario.nome.split(' ')[0] : ''
  const data = new Intl.DateTimeFormat('pt-BR', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'America/Sao_Paulo' }).format(new Date())
  const hoje = data.charAt(0).toUpperCase() + data.slice(1) // "Quarta-feira, 14 de outubro"
  const acoes = ACOES_RAPIDAS.filter((a) => rotasPermitidas.has(a.rota))

  return (
    <div className="mx-auto max-w-[1600px] pb-24">
      <div className="mb-8 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-semibold tracking-wider text-muted uppercase">Início</p>
          <h1 className="mt-1 text-[28px] leading-9 font-semibold tracking-tight">Olá, {primeiroNome} 👋</h1>
          {contexto && (
            <p className="mt-1 text-sm text-muted">
              {contexto.consolidado
                ? `Visão consolidada · ${contexto.empresas.length} empresas (somente leitura)`
                : `${contexto.empresas[0]?.nomeFantasia} / CNPJ: ${contexto.empresas[0]?.cnpj}`}
            </p>
          )}
          <div className="mt-2 flex flex-wrap items-center gap-2 text-xs">
            <span className="rounded-full border border-border bg-surface px-2.5 py-1 text-muted">{hoje}</span>
            {atualizadoEm && (
              <span className="flex items-center gap-1.5 rounded-full bg-primary-soft px-2.5 py-1 font-semibold text-primary">
                <span aria-hidden className="size-1.5 rounded-full bg-primary" />
                {`Atualizado às ${hora(atualizadoEm)}`}
              </span>
            )}
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button type="button" onClick={() => setResumoAberto(true)} className="flex h-10 items-center gap-2 rounded-lg border border-border bg-surface px-3 text-sm font-semibold hover:bg-surface-2">
            <Icone nome="bell" />
            Resumo diário
          </button>
          {acoes.length > 0 && <AcoesRapidas acoes={acoes} />}
          {rotasPermitidas.has('/vendas/registro') && (
            <Link to="/vendas/registro" className="flex h-10 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground transition-colors hover:bg-primary-hover">
              <Icone nome="bag" />
              Nova venda
            </Link>
          )}
        </div>
      </div>

      {erro && (
        <p role="alert" className="mb-5 flex items-center gap-2 rounded-lg bg-danger-soft px-4 py-3 text-sm text-danger">
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      <section aria-label="Resumo" className="mb-6">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-lg font-semibold">Resumo</h2>
          <div role="radiogroup" aria-label="Período" className="inline-flex rounded-full border border-border bg-surface p-1 text-sm">
            {PERIODOS.map((p) => (
              <button
                key={p.valor}
                type="button"
                role="radio"
                aria-checked={periodo === p.valor}
                onClick={() => setPeriodo(p.valor)}
                className={`rounded-full px-4 py-1 font-semibold transition-colors ${periodo === p.valor ? 'bg-primary text-primary-foreground' : 'text-muted hover:text-foreground'}`}
              >
                {p.rotulo}
              </button>
            ))}
          </div>
        </div>
        {dados && <Cartoes dados={dados} />}
      </section>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,2fr)_minmax(320px,1fr)]">
        <div className="min-w-0 space-y-6">
          {dados?.vendas && <GraficoDeVendas dias={dados.vendas.ultimos30Dias} pecasEmCondicional={dados.condicionais?.pecas ?? null} />}
          {carregado && dados && <PrimeirosPassos concluidos={dados.primeirosPassos} rotasPermitidas={rotasPermitidas} />}
          {verHistorico && <UltimosMarcos versao={versao} />}
          {dados?.vendas && (dados.vendas.maisVendidos.length > 0 || dados.vendas.porForma.length > 0) && (
            <div className="grid gap-6 md:grid-cols-2">
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
                    <h3 className="mt-4 mb-2 text-xs font-bold tracking-wider text-muted uppercase">Por vendedor</h3>
                    <Lista itens={dados.vendas.porVendedor} />
                  </>
                )}
              </Bloco>
            </div>
          )}
        </div>

        <div className="min-w-0 space-y-6">
          <section aria-label="Alertas" className="rounded-2xl border border-border bg-surface p-6 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-lg font-semibold">Alertas</h2>
              {dados && dados.alertas.length > 0 && (
                <span className="rounded-full bg-danger-soft px-2 py-0.5 text-xs font-bold text-danger tabular-nums">{dados.alertas.length}</span>
              )}
            </div>
            {dados && dados.alertas.length > 0 ? (
              <ul className="mt-4 space-y-2">
                {dados.alertas.map((a) => (
                  <li key={a.tipo}>
                    <Link
                      to={a.link}
                      className={`flex items-start gap-3 rounded-xl px-3 py-2.5 text-sm transition-opacity hover:opacity-90 ${a.gravidade === 'perigo' ? 'bg-danger-soft text-danger' : 'bg-warning-soft text-warning'}`}
                    >
                      <Icone nome="alert" className="mt-0.5 size-4" />
                      <span>
                        <span className="block font-semibold">{a.titulo}</span>
                        <span className="block text-xs">{a.detalhe}</span>
                      </span>
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

          {dados?.estoque && (
            <section aria-label="Estoque" className="rounded-2xl border border-border bg-surface p-6 shadow-sm">
              <h2 className="text-lg font-semibold">Estoque</h2>
              <p className="mt-3 text-xs text-muted">A preço de venda</p>
              <p className="text-[28px] leading-9 font-bold tracking-tight tabular-nums">{moeda(dados.estoque.valorDeVenda)}</p>
              <dl className="mt-3 grid grid-cols-2 gap-3 text-sm">
                <div className="rounded-xl bg-surface-2 px-3 py-2">
                  <dt className="text-xs text-muted">A custo</dt>
                  <dd className="font-semibold tabular-nums">{moeda(dados.estoque.valorDeCusto)}</dd>
                </div>
                <div className="rounded-xl bg-surface-2 px-3 py-2">
                  <dt className="text-xs text-muted">Em estoque</dt>
                  <dd className="font-semibold tabular-nums">{pecas(dados.estoque.pecas)}</dd>
                </div>
              </dl>
              <p className={`mt-3 text-xs font-semibold ${dados.estoque.totalDeQuebras > 0 ? 'text-warning' : 'text-muted'}`}>
                {dados.estoque.totalDeQuebras > 0
                  ? `${dados.estoque.totalDeQuebras} ${dados.estoque.totalDeQuebras === 1 ? 'produto' : 'produtos'} com quebra de grade`
                  : 'Nenhuma quebra de grade'}
              </p>
              <div className="mt-4 grid gap-2">
                {ATALHOS_DE_ESTOQUE.filter((a) => rotasPermitidas.has(a.rota)).map((a) => (
                  <Link key={a.rota} to={a.rota} className="flex items-center justify-between rounded-lg border border-border px-3 py-2 text-sm font-medium hover:border-primary hover:text-primary">
                    {a.rotulo}
                    <Icone nome="arrowRight" className="size-4" />
                  </Link>
                ))}
              </div>
            </section>
          )}

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

          <Consultoria />
        </div>
      </div>

      <AtendimentoWhatsApp />
      {resumoAberto && <ResumoDiarioDialogo onFechar={() => setResumoAberto(false)} />}
    </div>
  )
}

function AcoesRapidas({ acoes }: { acoes: typeof ACOES_RAPIDAS }) {
  const [aberto, setAberto] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  useFecharAoClicarFora(ref, aberto, () => setAberto(false))
  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={aberto}
        onClick={() => setAberto((v) => !v)}
        className="flex h-10 items-center gap-2 rounded-lg border border-border bg-surface px-3 text-sm font-semibold hover:bg-surface-2"
      >
        Ações rápidas
        <Icone nome="chevronDown" className="size-4 text-muted" />
      </button>
      {aberto && (
        <div role="menu" aria-label="Ações rápidas" className="absolute right-0 z-30 mt-2 w-64 rounded-xl border border-border bg-surface p-1.5 shadow-lg">
          {acoes.map((a) => (
            <Link key={a.rota} role="menuitem" to={a.rota} onClick={() => setAberto(false)} className="flex items-center gap-3 rounded-lg px-2.5 py-2 text-sm hover:bg-surface-2">
              <span className="grid size-8 place-items-center rounded-lg bg-surface-2 text-muted">
                <Icone nome={a.icone} />
              </span>
              {a.rotulo}
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}

function Consultoria() {
  const link = linkDoWhatsApp('Olá! Quero ajuda com a implantação do Kamus na minha loja.')
  return (
    <section aria-label="Consultoria e implantação" className="overflow-hidden rounded-2xl border border-primary bg-primary-soft p-6">
      <span className="grid size-10 place-items-center rounded-xl bg-primary text-primary-foreground">
        <Icone nome="users" />
      </span>
      <h2 className="mt-4 text-lg font-semibold">Implantação assistida</h2>
      <p className="mt-1 text-sm text-muted">
        Ajuda para montar as grades, importar planilhas, cadastrar o estoque inicial e treinar a equipe da loja.
      </p>
      {link ? (
        <a href={link} target="_blank" rel="noreferrer" className="mt-4 inline-flex h-10 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground hover:bg-primary-hover">
          Falar com um consultor
          <Icone nome="arrowRight" className="size-4" />
        </a>
      ) : (
        <p className="mt-4 text-xs font-semibold text-primary">Fale com a equipe Kamus pelo canal de atendimento da sua assinatura.</p>
      )}
    </section>
  )
}

function Cartoes({ dados }: { dados: Dashboard }) {
  // O rótulo vem da resposta (e não do botão escolhido) para nunca misturar o título novo com os números do período anterior.
  const periodo = dados.periodo
  const info = PERIODOS.find((p) => p.valor === periodo) ?? PERIODOS[0]!
  const cartoes: ReactNode[] = []
  const v = dados.vendas
  if (v) {
    const prefixo = v.somenteAsMinhas ? 'Minhas vendas' : 'Vendas'
    cartoes.push(<CartaoDeVendas key="periodo" titulo={`${prefixo} ${info.titulo}`} c={v.periodo} anterior={info.anterior} icone="bag" />)
    cartoes.push(
      periodo === 'mes' ? (
        <Indicador key="ticket" titulo="Ticket médio no mês" icone="trendingUp" valor={moeda(v.mes.ticketMedio)} linhas={[`${pecas(v.mes.pecas)} vendidas`]} />
      ) : (
        <CartaoDeVendas key="mes" titulo={`${prefixo} no mês`} c={v.mes} anterior="mês anterior" icone="trendingUp" />
      ),
    )
  }
  const f = dados.financeiro
  if (f) {
    cartoes.push(<AReceber key="receber" c={f.aReceber} />)
    cartoes.push(<APagar key="pagar" c={f.aPagar} />)
  }
  if (!v && !f && dados.estoque)
    cartoes.push(
      <Indicador key="pecas" titulo="Peças em estoque" icone="box" valor={String(dados.estoque.pecas)} linhas={[`${dados.estoque.totalDeQuebras} produtos com quebra de grade`]} />,
    )
  if (cartoes.length === 0) return null
  return <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">{cartoes}</div>
}

function Indicador({ titulo, icone, valor, linhas, destaque, selo }: {
  titulo: string
  icone: NomeIcone
  valor: string
  linhas: string[]
  destaque?: ReactNode
  selo?: ReactNode
}) {
  return (
    <article className="rounded-2xl border border-border bg-surface p-6 shadow-sm transition-shadow hover:shadow-md">
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-bold tracking-wider text-muted uppercase">{titulo}</span>
        <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-surface-2 text-primary">
          <Icone nome={icone} />
        </span>
      </div>
      <p className="mt-3 text-[28px] leading-9 font-bold tracking-tight tabular-nums">{valor}</p>
      {selo && <p className="mt-1">{selo}</p>}
      {destaque}
      {linhas.map((l) => (
        <p key={l} className="mt-1 text-xs text-muted">{l}</p>
      ))}
    </article>
  )
}

function Selo({ texto, tom }: { texto: string; tom: string }) {
  return <span className={`rounded-full px-2.5 py-1 text-[11px] font-bold ${tom}`}>{texto}</span>
}

function CartaoDeVendas({ titulo, c, anterior, icone }: { titulo: string; c: Cartao; anterior: string; icone: NomeIcone }) {
  const comparacao =
    c.variacao === null ? (
      <p className="mt-1 text-xs text-muted">{c.totalAnterior === 0 ? `Sem vendas no ${anterior === 'ontem' ? 'dia anterior' : 'período anterior'}` : ''}</p>
    ) : (
      <p className="mt-1">
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${c.variacao >= 0 ? 'bg-success-soft text-success' : 'bg-danger-soft text-danger'}`}>
          {`${variacao(c.variacao)} vs ${anterior}`}
        </span>
      </p>
    )
  return <Indicador titulo={titulo} icone={icone} valor={moeda(c.total)} destaque={comparacao} linhas={[`${vendasTexto(c.vendas)} · ticket ${moeda(c.ticketMedio)}`]} />
}

const vencido = (c: Compromissos) => (c.vencido > 0 ? <p className="mt-1 text-xs font-semibold text-danger">{`${moeda(c.vencido)} vencido`}</p> : undefined)

function AReceber({ c }: { c: Compromissos }) {
  return (
    <Indicador
      titulo="A receber (7 dias)"
      icone="wallet"
      valor={moeda(c.proximosSeteDias)}
      selo={<Selo texto="Fluxo previsto" tom="bg-success-soft text-success" />}
      destaque={vencido(c)}
      linhas={[`Hoje ${moeda(c.hoje)} · ${c.parcelasProximosSeteDias} ${c.parcelasProximosSeteDias === 1 ? 'parcela' : 'parcelas'}`]}
    />
  )
}

function APagar({ c }: { c: Compromissos }) {
  const titulos = `${c.parcelasProximosSeteDias} ${c.parcelasProximosSeteDias === 1 ? 'título' : 'títulos'} a vencer`
  return (
    <Indicador
      titulo="A pagar (7 dias)"
      icone="alert"
      valor={moeda(c.proximosSeteDias)}
      selo={
        c.parcelasVencidas > 0 ? (
          <Selo texto={`${c.parcelasVencidas} ${c.parcelasVencidas === 1 ? 'vencida' : 'vencidas'}`} tom="bg-danger-soft text-danger" />
        ) : (
          <Selo texto="Em dia" tom="bg-warning-soft text-warning" />
        )
      }
      destaque={vencido(c)}
      linhas={[`Hoje ${moeda(c.hoje)} · ${titulos}`]}
    />
  )
}

function Bloco({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <section aria-label={titulo} className="rounded-2xl border border-border bg-surface p-6 shadow-sm">
      <h2 className="mb-3 text-lg font-semibold">{titulo}</h2>
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
