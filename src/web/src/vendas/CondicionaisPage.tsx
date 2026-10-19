import { useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { useLojas } from '../estoque/useLojas'
import { dataBr, hojeIso, somarDias } from '../financeiro/tipos'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { salvarArquivo } from '../relatorios/salvarArquivo'
import { EditorDePagamentos } from './EditorDePagamentos'
import { EntradaDeItens } from './EntradaDeItens'
import { calcularPagamentos, corpoDosPagamentos, inteiro, novaLinha, valorDe, type ItemDigitado, type PagamentoDigitado } from './itens'
import {
  centavos,
  inicioDoMes,
  PERFIS_DE_GESTAO,
  PERFIS_QUE_CONSULTAM,
  PERFIS_QUE_VENDEM,
  pecas,
  porcentagem,
  type Condicional,
  type ConversaoDoVendedor,
  type LinhaDeCondicional,
  type ListaDeCondicionais,
  type RegrasDeVenda,
  type SituacaoDaCondicional,
} from './tipos'
import { useFormas, useVendedores } from './useVendas'

type Filtro = 'abertas' | 'atrasadas' | 'fechadas' | 'todas'

const FILTROS: { valor: Filtro; rotulo: string }[] = [
  { valor: 'abertas', rotulo: 'Abertas (inclui atrasadas)' },
  { valor: 'atrasadas', rotulo: 'Atrasadas' },
  { valor: 'fechadas', rotulo: 'Fechadas' },
  { valor: 'todas', rotulo: 'Todas' },
]

const SELO: Record<SituacaoDaCondicional, string> = {
  Aberta: 'bg-info-soft text-info',
  Atrasada: 'bg-danger-soft text-danger',
  Fechada: 'bg-success-soft text-success',
}

interface PessoaResumo {
  id: string
  nome: string
}

interface Fechamento {
  condicional: Condicional
  vendaNumero: number | null
  vendaTotal: number | null
  avisos: string[]
}

type Resultado = { tipo: 'aberta'; condicional: Condicional } | { tipo: 'fechada'; fechamento: Fechamento }

/** Condicional (malinha): peças que o cliente leva para provar, com prazo; no retorno, o que ficou vira venda. */
export function CondicionaisPage() {
  const { getJson, baixar } = useApi()
  const { perfilAtual, consolidado } = useEmpresa()
  const [situacao, setSituacao] = useState<Filtro>('abertas')
  const [busca, setBusca] = useState('')
  const [{ lista, erro }, setEstado] = useState<{ lista: ListaDeCondicionais | null; erro: string | null }>({ lista: null, erro: null })
  const [regras, setRegras] = useState<RegrasDeVenda | null>(null)
  const [versao, setVersao] = useState(0)
  const [abrindo, setAbrindo] = useState(false)
  const [fechando, setFechando] = useState<LinhaDeCondicional | null>(null)
  const [resultado, setResultado] = useState<Resultado | null>(null)
  const permitido = PERFIS_QUE_VENDEM.includes(perfilAtual) || PERFIS_QUE_CONSULTAM.includes(perfilAtual)
  const podeMovimentar = PERFIS_QUE_VENDEM.includes(perfilAtual) && !consolidado

  useEffect(() => {
    if (!permitido) return
    const controle = new AbortController()
    const filtros = new URLSearchParams({ situacao })
    if (busca.trim()) filtros.set('busca', busca.trim())
    getJson<ListaDeCondicionais>(`/api/vendas/condicionais?${filtros}`, controle.signal)
      .then((l) => setEstado({ lista: l, erro: null }))
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setEstado({ lista: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar as condicionais.' })
      })
    return () => controle.abort()
  }, [getJson, permitido, situacao, busca, versao])

  useEffect(() => {
    if (!podeMovimentar) return
    const controle = new AbortController()
    getJson<RegrasDeVenda>('/api/vendas/configuracao', controle.signal)
      .then(setRegras)
      .catch(() => {
        if (!controle.signal.aborted)
          setRegras({ permitirEstoqueNegativo: true, prazoDeTrocaDias: 30, validadeDoValeDias: 90, prazoCondicionalDias: 2, limiteCondicionalPecas: 0, limiteCondicionalValor: 0 })
      })
    return () => controle.abort()
  }, [getJson, podeMovimentar])

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Condicional</h1>
        <p className={`mt-4 ${estilos.erro}`}>Seu perfil não acessa as condicionais.</p>
      </div>
    )

  const baixarTermo = async (id: string) => {
    const r = await baixar(`/api/vendas/condicionais/${id}/termo.pdf`)
    if (r.tipo === 'arquivo') salvarArquivo(r.blob, r.nome)
  }

  const concluido = (r: Resultado) => {
    setResultado(r)
    setAbrindo(false)
    setFechando(null)
    setVersao((v) => v + 1)
  }

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Vendas</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Condicional</h1>
          <p className="mt-1 text-sm text-muted">
            O cliente leva as peças para provar e devolve até a data combinada. As peças saem do estoque disponível; no retorno, as devolvidas voltam e as que ele ficou viram venda.
          </p>
        </div>
        <BotaoExportar relatorio="condicionais" parametros={{ situacao }} />
        {podeMovimentar && regras && (
          <button type="button" onClick={() => setAbrindo(true)} className={estilos.botaoPrimario}>
            Nova condicional
          </button>
        )}
      </div>

      {resultado && (
        <div role="status" className="mt-4 space-y-1 rounded-lg bg-success-soft px-3 py-2.5 text-sm text-success">
          {resultado.tipo === 'aberta' ? (
            <p className="flex flex-wrap items-center gap-2 font-semibold">
              <Icone nome="check" />
              <span>{`Condicional nº ${resultado.condicional.numero} aberta: ${pecas(resultado.condicional.pecas)} (${moeda(resultado.condicional.valor)})`}</span>
              <button type="button" onClick={() => void baixarTermo(resultado.condicional.id)} className="font-normal underline">
                Termo (PDF)
              </button>
            </p>
          ) : (
            <>
              <p className="flex flex-wrap items-center gap-2 font-semibold">
                <Icone nome="check" />
                <span>
                  {resultado.fechamento.vendaNumero !== null
                    ? `Condicional nº ${resultado.fechamento.condicional.numero} fechada · venda nº ${resultado.fechamento.vendaNumero} de ${moeda(resultado.fechamento.vendaTotal ?? 0)}`
                    : `Condicional nº ${resultado.fechamento.condicional.numero} fechada: todas as peças voltaram ao estoque`}
                </span>
              </p>
              {resultado.fechamento.avisos.map((a) => (
                <p key={a} className="text-warning">{a}</p>
              ))}
            </>
          )}
        </div>
      )}

      {lista && (
        <dl className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Cartao rotulo="Abertas" valor={String(lista.abertas)} />
          <Cartao rotulo="Atrasadas" valor={String(lista.atrasadas)} destaque={lista.atrasadas > 0} />
          <Cartao rotulo="Peças na rua" valor={String(lista.pecasNaRua)} />
          <Cartao rotulo="Valor na rua" valor={moeda(lista.valorNaRua)} />
        </dl>
      )}

      <div className="mt-6 grid gap-3 sm:grid-cols-[14rem_minmax(0,1fr)]">
        <Campo rotulo="Situação">
          <select value={situacao} onChange={(e) => setSituacao(e.target.value as Filtro)} className={estilos.campo}>
            {FILTROS.map((f) => (
              <option key={f.valor} value={f.valor}>{f.rotulo}</option>
            ))}
          </select>
        </Campo>
        <Campo rotulo="Buscar">
          <input value={busca} onChange={(e) => setBusca(e.target.value)} placeholder="Cliente ou número" className={estilos.campo} />
        </Campo>
      </div>

      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {lista && (
        <div className="mt-4 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          {lista.itens.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted">Nenhuma condicional nesta situação.</p>
          ) : (
            <table aria-label="Condicionais" className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-4 py-2">Nº</th>
                  <th className="px-4 py-2">Saída</th>
                  <th className="px-4 py-2">Cliente</th>
                  <th className="px-4 py-2">Vendedor</th>
                  <th className="px-4 py-2 text-right">Peças</th>
                  <th className="px-4 py-2 text-right">Valor</th>
                  <th className="px-4 py-2">Devolver até</th>
                  <th className="px-4 py-2">Situação</th>
                  <th />
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {lista.itens.map((k) => (
                  <tr key={k.id}>
                    <td className="px-4 py-2 tabular-nums">{k.numero}</td>
                    <td className="px-4 py-2">{dataBr(k.data)}</td>
                    <td className="px-4 py-2 font-semibold">{k.clienteNome}</td>
                    <td className="px-4 py-2 text-muted">{k.vendedorNome ?? '—'}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{k.pecas}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{moeda(k.valor)}</td>
                    <td className="px-4 py-2">{dataBr(k.prazo)}</td>
                    <td className="px-4 py-2">
                      <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${SELO[k.situacao]}`}>{k.situacao}</span>
                      {k.vendaNumero !== null && <span className="ml-2 text-xs text-muted">{`venda nº ${k.vendaNumero}`}</span>}
                    </td>
                    <td className="px-4 py-2">
                      <div className="flex justify-end gap-2">
                        <button type="button" aria-label={`Termo da condicional ${k.numero}`} onClick={() => void baixarTermo(k.id)} className="text-xs text-primary hover:underline">
                          Termo
                        </button>
                        {podeMovimentar && k.situacao !== 'Fechada' && (
                          <button type="button" aria-label={`Fechar condicional ${k.numero}`} onClick={() => setFechando(k)} className="text-xs font-semibold text-primary hover:underline">
                            Fechar
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {PERFIS_QUE_CONSULTAM.includes(perfilAtual) && <ConversaoPorVendedor versao={versao} />}

      {abrindo && regras && (
        <NovaCondicional regras={regras} gestao={PERFIS_DE_GESTAO.includes(perfilAtual)} onFechar={() => setAbrindo(false)} onAberta={(c) => concluido({ tipo: 'aberta', condicional: c })} />
      )}
      {fechando && <FecharCondicional linha={fechando} onFechar={() => setFechando(null)} onFechada={(f) => concluido({ tipo: 'fechada', fechamento: f })} />}
    </div>
  )
}

function Cartao({ rotulo, valor, destaque = false }: { rotulo: string; valor: string; destaque?: boolean }) {
  return (
    <div className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
      <dt className="text-xs text-muted">{rotulo}</dt>
      <dd className={`mt-1 text-xl font-bold tabular-nums ${destaque ? 'text-danger' : ''}`}>{valor}</dd>
    </div>
  )
}

function ConversaoPorVendedor({ versao }: { versao: number }) {
  const { getJson } = useApi()
  const [de, setDe] = useState(() => inicioDoMes(hojeIso()))
  const [ate, setAte] = useState(hojeIso())
  const [linhas, setLinhas] = useState<ConversaoDoVendedor[] | null>(null)

  useEffect(() => {
    if (!de || !ate) return
    const controle = new AbortController()
    getJson<ConversaoDoVendedor[]>(`/api/vendas/condicionais/conversao?${new URLSearchParams({ de, ate })}`, controle.signal)
      .then(setLinhas)
      .catch(() => {
        if (!controle.signal.aborted) setLinhas(null)
      })
    return () => controle.abort()
  }, [getJson, de, ate, versao])

  return (
    <section className="mt-8 rounded-2xl border border-border bg-surface p-5 shadow-sm">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h2 className="font-semibold">Conversão por vendedor</h2>
          <p className="text-sm text-muted">Quanto das condicionais fechadas no período virou venda.</p>
        </div>
        <Campo rotulo="De" className="w-40">
          <input type="date" value={de} onChange={(e) => setDe(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Até" className="w-40">
          <input type="date" value={ate} onChange={(e) => setAte(e.target.value)} className={estilos.campo} />
        </Campo>
      </div>
      {linhas && linhas.length === 0 && <p className="mt-4 text-sm text-muted">Nenhuma condicional fechada no período.</p>}
      {linhas && linhas.length > 0 && (
        <div className="mt-4 overflow-x-auto">
          <table aria-label="Conversão por vendedor" className="w-full text-sm">
            <thead className="text-left text-xs text-muted uppercase">
              <tr>
                <th className="py-2">Vendedor</th>
                <th className="py-2 text-right">Condicionais</th>
                <th className="py-2 text-right">Peças levadas</th>
                <th className="py-2 text-right">Peças vendidas</th>
                <th className="py-2 text-right">Valor levado</th>
                <th className="py-2 text-right">Valor vendido</th>
                <th className="py-2 text-right">Conversão</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {linhas.map((l) => (
                <tr key={l.vendedorId ?? 'sem'}>
                  <td className="py-2 font-semibold">{l.vendedor}</td>
                  <td className="py-2 text-right tabular-nums">{l.condicionais}</td>
                  <td className="py-2 text-right tabular-nums">{l.pecasLevadas}</td>
                  <td className="py-2 text-right tabular-nums">{l.pecasVendidas}</td>
                  <td className="py-2 text-right tabular-nums">{moeda(l.valorLevado)}</td>
                  <td className="py-2 text-right tabular-nums">{moeda(l.valorVendido)}</td>
                  <td className="py-2 text-right font-semibold tabular-nums">{porcentagem(l.conversao)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function NovaCondicional({ regras, gestao, onFechar, onAberta }: { regras: RegrasDeVenda; gestao: boolean; onFechar: () => void; onAberta: (c: Condicional) => void }) {
  const { getJson, enviar } = useApi()
  const { estado } = useAuth()
  const usuarioId = estado.status === 'autenticado' ? estado.sessao.usuario.id : null
  const ativas = useLojas()?.filter((l) => l.ativa) ?? []
  const vendedores = useVendedores()?.filter((v) => v.ativo) ?? []
  const [lojaId, setLojaId] = useState('')
  const [vendedorId, setVendedorId] = useState<string | null>(null)
  const [cliente, setCliente] = useState<{ id: string | null; nome: string }>({ id: null, nome: '' })
  const [sugestoes, setSugestoes] = useState<PessoaResumo[]>([])
  const [prazo, setPrazo] = useState(() => somarDias(hojeIso(), regras.prazoCondicionalDias))
  const [observacao, setObservacao] = useState('')
  const [itens, setItens] = useState<ItemDigitado[]>([])
  const [liberar, setLiberar] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  const lojaEscolhida = lojaId || (ativas.length === 1 ? ativas[0]!.id : '')
  const vendedorEscolhido = vendedorId ?? vendedores.find((v) => v.usuarioId === usuarioId)?.id ?? ''

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

  const totalDePecas = itens.reduce((s, i) => s + inteiro(i.quantidade), 0)
  const valor = centavos(itens.reduce((s, i) => s + inteiro(i.quantidade) * i.preco, 0))
  const podeAbrir = !!cliente.id && !!lojaEscolhida && !!prazo && itens.length > 0 && itens.every((i) => inteiro(i.quantidade) > 0)

  async function abrir(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      const condicional = await enviar<Condicional>('POST', '/api/vendas/condicionais', {
        lojaId: lojaEscolhida,
        clienteId: cliente.id,
        clienteNome: cliente.nome,
        vendedorId: vendedorEscolhido || null,
        prazo,
        observacao: observacao.trim() || null,
        liberar,
        itens: itens.map((i) => ({ skuId: i.skuId, quantidade: inteiro(i.quantidade) })),
      })
      onAberta(condicional)
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível abrir a condicional. Tente de novo.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo="Nova condicional" onFechar={onFechar} largo>
      <form onSubmit={abrir} className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-3">
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
                <option key={v.id} value={v.id}>{v.nome}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Devolver até">
            <input type="date" value={prazo} min={hojeIso()} onChange={(e) => setPrazo(e.target.value)} className={estilos.campo} />
          </Campo>
        </div>
        <div className="relative">
          <Campo rotulo="Cliente">
            <input
              value={cliente.nome}
              onChange={(e) => {
                setCliente({ id: null, nome: e.target.value })
                if (e.target.value.trim().length < 2) setSugestoes([])
              }}
              placeholder="Buscar no cadastro de clientes"
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
          {!cliente.id && <p className="mt-1 text-xs text-muted">A condicional exige cliente cadastrado.</p>}
        </div>

        <div>
          <h3 className="text-sm font-semibold">Peças</h3>
          <EntradaDeItens lojaId={lojaEscolhida} itens={itens} onItens={setItens} onErro={setErro} rotulo="Peças da condicional" botaoDaGrade="Adicionar à condicional" semDesconto />
          {itens.length > 0 && <p className="mt-2 text-right text-sm font-semibold">{`${pecas(totalDePecas)} · ${moeda(valor)}`}</p>}
        </div>

        <Campo rotulo="Observação (opcional)">
          <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={500} className={estilos.campo} />
        </Campo>

        {gestao && (
          <label className="flex items-start gap-3 text-sm">
            <input type="checkbox" checked={liberar} onChange={(e) => setLiberar(e.target.checked)} className="mt-1 h-4 w-4" />
            <span>
              <span className="font-semibold">Liberar acima do limite ou com condicional atrasada</span>
              <span className="block text-muted">{`Limites por cliente: ${regras.limiteCondicionalPecas > 0 ? pecas(regras.limiteCondicionalPecas) : 'sem limite de peças'} · ${regras.limiteCondicionalValor > 0 ? moeda(regras.limiteCondicionalValor) : 'sem limite de valor'}.`}</span>
            </span>
          </label>
        )}

        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}

        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" disabled={!podeAbrir || enviando} className={estilos.botaoPrimario}>
            {enviando ? 'Abrindo...' : 'Abrir condicional'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}

function FecharCondicional({ linha, onFechar, onFechada }: { linha: LinhaDeCondicional; onFechar: () => void; onFechada: (f: Fechamento) => void }) {
  const { getJson, enviar } = useApi()
  const formas = useFormas()?.filter((f) => f.ativa) ?? []
  const [condicional, setCondicional] = useState<Condicional | null>(null)
  const [devolvidas, setDevolvidas] = useState<Record<string, string>>({})
  const [descontoGeral, setDescontoGeral] = useState('')
  const [notaExterna, setNotaExterna] = useState('')
  const [pagamentos, setPagamentos] = useState<PagamentoDigitado[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    const controle = new AbortController()
    getJson<Condicional>(`/api/vendas/condicionais/${linha.id}`, controle.signal)
      .then((c) => {
        setCondicional(c)
        setDevolvidas(Object.fromEntries(c.itens.map((i) => [i.skuId, String(i.quantidade)])))
      })
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setErro(e instanceof ErroDeApi ? e.message : 'Não foi possível abrir a condicional.')
      })
    return () => controle.abort()
  }, [getJson, linha.id])

  const itens = condicional?.itens ?? []
  const devolvidasDe = (skuId: string) => inteiro(devolvidas[skuId] ?? '')
  const ficou = itens.map((i) => ({ ...i, ficou: Math.max(0, i.quantidade - devolvidasDe(i.skuId)) }))
  const pecasQueFicaram = ficou.reduce((s, i) => s + i.ficou, 0)
  const valorQueFicou = centavos(ficou.reduce((s, i) => s + i.ficou * i.precoUnitario, 0))
  const desconto = valorDe(descontoGeral)
  const total = centavos(valorQueFicou - desconto)
  const formaPadrao = formas.find((f) => f.tipo === 'Dinheiro') ?? formas[0]
  const linhasDePagamento: PagamentoDigitado[] = pagamentos.length > 0 || !formaPadrao ? pagamentos : [novaLinha(formaPadrao)]
  const { valores, falta } = calcularPagamentos(linhasDePagamento, total)
  const excede = itens.some((i) => devolvidasDe(i.skuId) > i.quantidade)
  const vende = pecasQueFicaram > 0
  const podeFechar = !!condicional && !excede && (!vende || (falta <= 0 && total >= 0 && linhasDePagamento.length > 0))

  async function fechar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      const r = await enviar<Fechamento>('POST', `/api/vendas/condicionais/${linha.id}/fechar`, {
        devolvidos: itens.map((i) => ({ skuId: i.skuId, quantidade: devolvidasDe(i.skuId) })),
        pagamentos: vende ? corpoDosPagamentos(linhasDePagamento, valores) : [],
        descontoGeral: vende ? desconto : 0,
        notaExterna: (vende && notaExterna.trim()) || null,
      })
      onFechada(r)
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível fechar a condicional. Tente de novo.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={`Fechar condicional nº ${linha.numero}`} onFechar={onFechar} largo>
      {!condicional && !erro && <p className="text-sm text-muted">Carregando...</p>}
      {condicional && (
        <form onSubmit={fechar} className="space-y-4">
          <p className="text-sm text-muted">
            {`${condicional.clienteNome} · saída em ${dataBr(condicional.data)} · devolver até ${dataBr(condicional.prazo)}`}
            {condicional.situacao === 'Atrasada' && <span className="ml-2 font-semibold text-danger">Atrasada</span>}
          </p>
          <table aria-label="Peças devolvidas" className="w-full text-sm">
            <thead className="text-left text-xs text-muted uppercase">
              <tr>
                <th className="py-2">Peça</th>
                <th className="py-2 text-right">Levou</th>
                <th className="w-24 py-2 text-right">Devolveu</th>
                <th className="py-2 text-right">Ficou</th>
                <th className="py-2 text-right">Valor</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {ficou.map((i) => (
                <tr key={i.skuId}>
                  <td className="py-2 font-semibold">{i.descricao}</td>
                  <td className="py-2 text-right tabular-nums">{i.quantidade}</td>
                  <td className="py-2 pl-2">
                    <input
                      aria-label={`Devolveu ${i.descricao}`}
                      value={devolvidas[i.skuId] ?? ''}
                      onChange={(e) => setDevolvidas((d) => ({ ...d, [i.skuId]: e.target.value.replace(/\D/g, '') }))}
                      inputMode="numeric"
                      className="h-9 w-full rounded-lg border border-border bg-surface px-2 text-right tabular-nums"
                    />
                  </td>
                  <td className="py-2 text-right tabular-nums">{i.ficou}</td>
                  <td className="py-2 text-right tabular-nums">{moeda(i.ficou * i.precoUnitario)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {excede && <p className="text-sm font-semibold text-danger">A devolução não pode passar do que o cliente levou.</p>}

          {vende ? (
            <div className="grid gap-4 rounded-xl bg-surface-2 p-4 md:grid-cols-2">
              <div className="space-y-3">
                <p className="font-semibold">{`O cliente ficou com ${pecas(pecasQueFicaram)}: ${moeda(valorQueFicou)}`}</p>
                <EditorDePagamentos formas={formas} total={total} linhas={linhasDePagamento} onLinhas={setPagamentos} clienteId={condicional.clienteId} />
              </div>
              <div className="space-y-3">
                <Campo rotulo="Desconto na venda (R$)">
                  <input value={descontoGeral} onChange={(e) => setDescontoGeral(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
                </Campo>
                <Campo rotulo="Nota externa (opcional)">
                  <input value={notaExterna} onChange={(e) => setNotaExterna(e.target.value)} maxLength={60} placeholder="Ex.: NFC-e 1234" className={estilos.campo} />
                </Campo>
                <p className="text-lg font-bold">{`Total da venda: ${moeda(total)}`}</p>
                {falta > 0 && <p className="font-semibold text-danger">{`Falta: ${moeda(falta)}`}</p>}
                {falta < 0 && <p className="font-semibold text-success">{`Troco: ${moeda(-falta)}`}</p>}
              </div>
            </div>
          ) : (
            <p className="rounded-xl bg-surface-2 p-4 text-sm">Todas as peças voltam ao estoque; nenhuma venda será registrada.</p>
          )}

          {erro && <p role="alert" className={estilos.erro}>{erro}</p>}

          <div className="flex justify-end gap-2">
            <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
            <button type="submit" disabled={!podeFechar || enviando} className={estilos.botaoPrimario}>
              {enviando ? 'Fechando...' : 'Fechar condicional'}
            </button>
          </div>
        </form>
      )}
      {!condicional && erro && <p role="alert" className={estilos.erro}>{erro}</p>}
    </Dialogo>
  )
}
