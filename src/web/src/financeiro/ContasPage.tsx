import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { lerNumero, moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { SeloDeSituacao } from './Selo'
import { dataBr, hojeIso, PERFIS_DO_FINANCEIRO, somarMeses, type LinhaDeParcela, type ListaDeParcelas, type TipoDeTitulo, type Titulo } from './tipos'
import { useCategorias, useContas } from './useFinanceiro'

const TEXTOS: Record<TipoDeTitulo, { titulo: string; subtitulo: string; pessoa: string; papel: string; novo: string; baixar: string; categoria: 'Despesa' | 'Receita' }> = {
  pagar: {
    titulo: 'Contas a pagar',
    subtitulo: 'Fornecedores, despesas fixas e impostos. Notas de entrada lançadas no estoque entram aqui sozinhas.',
    pessoa: 'Fornecedor',
    papel: 'fornecedor',
    novo: 'Nova conta a pagar',
    baixar: 'Pagar',
    categoria: 'Despesa',
  },
  receber: {
    titulo: 'Contas a receber',
    subtitulo: 'Valores a receber de clientes e outras receitas.',
    pessoa: 'Cliente',
    papel: 'cliente',
    novo: 'Nova conta a receber',
    baixar: 'Receber',
    categoria: 'Receita',
  },
}

/** Contas a pagar ou a receber: uma linha por parcela, com baixa (total ou parcial), detalhe, estorno e cancelamento. */
export function ContasPage({ tipo }: { tipo: TipoDeTitulo }) {
  const { getJson } = useApi()
  const { perfilAtual, consolidado } = useEmpresa()
  const textos = TEXTOS[tipo]
  const [situacao, setSituacao] = useState('abertas')
  const [de, setDe] = useState('')
  const [ate, setAte] = useState('')
  const [busca, setBusca] = useState('')
  const [versao, setVersao] = useState(0)
  const [{ lista, erro }, setEstado] = useState<{ lista: ListaDeParcelas | null; erro: string | null }>({ lista: null, erro: null })
  const [novo, setNovo] = useState(false)
  const [baixando, setBaixando] = useState<LinhaDeParcela | null>(null)
  const [detalhe, setDetalhe] = useState<string | null>(null)
  const permitido = PERFIS_DO_FINANCEIRO.includes(perfilAtual)

  const filtros = useMemo(() => {
    const f: Record<string, string> = { tipo, situacao }
    if (de) f.de = de
    if (ate) f.ate = ate
    if (busca.trim()) f.busca = busca.trim()
    return f
  }, [tipo, situacao, de, ate, busca])

  useEffect(() => {
    if (!permitido) return
    const controle = new AbortController()
    const timer = window.setTimeout(() => {
      getJson<ListaDeParcelas>(`/api/financeiro/titulos?${new URLSearchParams(filtros)}`, controle.signal)
        .then((l) => setEstado({ lista: l, erro: null }))
        .catch((e: unknown) => {
          if (!controle.signal.aborted) setEstado({ lista: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar.' })
        })
    }, busca ? 250 : 0)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, filtros, busca, versao, permitido])

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">{textos.titulo}</h1>
        <p className={`mt-4 ${estilos.erro}`}>Só Dono, Gerente e Financeiro acessam o financeiro.</p>
      </div>
    )

  const recarregar = () => setVersao((v) => v + 1)

  return (
    <div className="mx-auto max-w-7xl">
      <p className="text-xs text-muted">Financeiro</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">{textos.titulo}</h1>
          <p className="mt-1 text-sm text-muted">{textos.subtitulo}</p>
        </div>
        {!consolidado && (
          <button type="button" onClick={() => setNovo(true)} className={estilos.botaoPrimario}>
            {textos.novo}
          </button>
        )}
      </div>

      <div className="mt-6 flex flex-wrap items-end gap-3">
        <label className="min-w-56 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input value={busca} onChange={(e) => setBusca(e.target.value)} placeholder={`Descrição, ${textos.pessoa.toLowerCase()} ou documento`} className={estilos.campo} />
        </label>
        <label className="text-sm">
          <span className="mb-1 block font-semibold">Situação</span>
          <select value={situacao} onChange={(e) => setSituacao(e.target.value)} className={estilos.campo}>
            <option value="abertas">Em aberto</option>
            <option value="vencidas">Vencidas</option>
            <option value="pagas">{tipo === 'pagar' ? 'Pagas' : 'Recebidas'}</option>
            <option value="canceladas">Canceladas</option>
            <option value="todas">Todas</option>
          </select>
        </label>
        <label className="text-sm">
          <span className="mb-1 block font-semibold">Vencimento de</span>
          <input type="date" value={de} onChange={(e) => setDe(e.target.value)} className={estilos.campo} />
        </label>
        <label className="text-sm">
          <span className="mb-1 block font-semibold">até</span>
          <input type="date" value={ate} onChange={(e) => setAte(e.target.value)} className={estilos.campo} />
        </label>
        <BotaoExportar relatorio="titulos" parametros={filtros} />
      </div>

      {erro && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {lista && (
        <>
          <dl className="mt-4 grid gap-3 sm:grid-cols-3">
            <Indicador rotulo="Saldo em aberto" valor={moeda(lista.saldo)} />
            <Indicador rotulo="Vencido" valor={moeda(lista.vencido)} destaque={lista.vencido > 0} />
            <Indicador rotulo={tipo === 'pagar' ? 'Já pago' : 'Já recebido'} valor={moeda(lista.pago)} />
          </dl>
          <div className="mt-4 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
            {lista.itens.length === 0 ? (
              <p className="p-8 text-center text-sm text-muted">Nada por aqui com esses filtros.</p>
            ) : (
              <table aria-label={textos.titulo} className="w-full text-sm">
                <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                  <tr>
                    <th className="px-4 py-2">Vencimento</th>
                    <th className="px-4 py-2">Descrição</th>
                    <th className="px-4 py-2">Categoria</th>
                    <th className="px-4 py-2 text-right">Valor</th>
                    <th className="px-4 py-2 text-right">Saldo</th>
                    <th className="px-4 py-2">Situação</th>
                    <th className="px-4 py-2" />
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {lista.itens.map((l) => (
                    <tr key={l.parcelaId}>
                      <td className="px-4 py-2.5 whitespace-nowrap tabular-nums">{dataBr(l.vencimento)}</td>
                      <td className="px-4 py-2.5">
                        <button type="button" onClick={() => setDetalhe(l.tituloId)} className="text-left font-semibold hover:text-primary hover:underline">
                          {l.descricao}
                        </button>
                        {l.parcelas > 1 && <span className="ml-1 text-xs text-muted">{`${l.numero}/${l.parcelas}`}</span>}
                        <span className="block text-xs text-muted">{[l.pessoaNome, l.documento].filter(Boolean).join(' · ')}</span>
                      </td>
                      <td className="px-4 py-2.5 text-muted">{l.categoria ?? '—'}</td>
                      <td className="px-4 py-2.5 text-right tabular-nums">{moeda(l.valor)}</td>
                      <td className="px-4 py-2.5 text-right font-semibold tabular-nums">{moeda(l.saldo)}</td>
                      <td className="px-4 py-2.5"><SeloDeSituacao situacao={l.situacao} /></td>
                      <td className="px-4 py-2.5 text-right">
                        {l.saldo > 0 && l.situacao !== 'Cancelada' && !consolidado && (
                          <button type="button" onClick={() => setBaixando(l)} className={estilos.botao} aria-label={`${textos.baixar} ${l.descricao} ${l.numero}/${l.parcelas}`}>
                            {textos.baixar}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}

      {novo && (
        <NovoTitulo
          tipo={tipo}
          onFechar={() => setNovo(false)}
          onSalvo={() => {
            setNovo(false)
            recarregar()
          }}
        />
      )}
      {baixando && (
        <BaixarParcela
          tipo={tipo}
          linha={baixando}
          onFechar={() => setBaixando(null)}
          onSalvo={() => {
            setBaixando(null)
            recarregar()
          }}
        />
      )}
      {detalhe && <DetalheDoTitulo id={detalhe} onFechar={() => setDetalhe(null)} onMudou={recarregar} />}
    </div>
  )
}

function Indicador({ rotulo, valor, destaque = false }: { rotulo: string; valor: string; destaque?: boolean }) {
  return (
    <div className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
      <dt className="text-xs text-muted uppercase">{rotulo}</dt>
      <dd className={`mt-1 text-xl font-bold tabular-nums ${destaque ? 'text-danger' : ''}`}>{valor}</dd>
    </div>
  )
}

interface PessoaResumo {
  id: string
  nome: string
}

function NovoTitulo({ tipo, onFechar, onSalvo }: { tipo: TipoDeTitulo; onFechar: () => void; onSalvo: () => void }) {
  const { getJson, enviar } = useApi()
  const textos = TEXTOS[tipo]
  const categorias = useCategorias()?.filter((c) => c.tipo === textos.categoria && c.ativa) ?? []
  const [descricao, setDescricao] = useState('')
  const [pessoa, setPessoa] = useState<{ id: string | null; nome: string }>({ id: null, nome: '' })
  const [sugestoes, setSugestoes] = useState<PessoaResumo[]>([])
  const [categoriaId, setCategoriaId] = useState('')
  const [documento, setDocumento] = useState('')
  const [competencia, setCompetencia] = useState(hojeIso())
  const [total, setTotal] = useState('')
  const [quantidade, setQuantidade] = useState('1')
  const [primeiro, setPrimeiro] = useState(hojeIso())
  const [repetir, setRepetir] = useState('1')
  const [observacao, setObservacao] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    const termo = pessoa.nome.trim()
    if (pessoa.id || termo.length < 2) return
    const controle = new AbortController()
    const timer = window.setTimeout(() => {
      getJson<{ itens: PessoaResumo[] }>(`/api/pessoas?${new URLSearchParams({ papel: textos.papel, situacao: 'ativos', busca: termo })}`, controle.signal)
        .then((r) => setSugestoes(r.itens.slice(0, 6)))
        .catch(() => {})
    }, 250)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, pessoa, textos.papel])

  const parcelas = useMemo(() => {
    const valor = lerNumero(total)
    const n = Math.max(1, Math.min(120, Number.parseInt(quantidade, 10) || 1))
    if (valor === null || valor <= 0 || !primeiro) return []
    const centavos = Math.round(valor * 100)
    const base = Math.floor(centavos / n)
    return Array.from({ length: n }, (_, i) => ({ vencimento: somarMeses(primeiro, i), valor: (i === 0 ? base + (centavos - base * n) : base) / 100 }))
  }, [total, quantidade, primeiro])

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    if (parcelas.length === 0) {
      setErro('Informe o valor e o primeiro vencimento.')
      return
    }
    setEnviando(true)
    try {
      await enviar('POST', '/api/financeiro/titulos', {
        tipo,
        descricao,
        pessoaId: pessoa.id,
        pessoaNome: pessoa.nome.trim() || null,
        categoriaId: categoriaId || null,
        documento: documento || null,
        competencia,
        observacao: observacao || null,
        parcelas,
        repetirMeses: parcelas.length === 1 ? Number.parseInt(repetir, 10) || 1 : 1,
      })
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={textos.novo} onFechar={onFechar} largo>
      <form onSubmit={salvar} className="grid gap-4 sm:grid-cols-2">
        <Campo rotulo="Descrição" className="sm:col-span-2">
          <input value={descricao} onChange={(e) => setDescricao(e.target.value)} maxLength={150} autoFocus className={estilos.campo} />
        </Campo>
        <div className="relative">
          <Campo rotulo={`${textos.pessoa} (opcional)`}>
            <input
              value={pessoa.nome}
              onChange={(e) => {
                setPessoa({ id: null, nome: e.target.value })
                if (e.target.value.trim().length < 2) setSugestoes([])
              }}
              maxLength={150}
              className={estilos.campo}
            />
          </Campo>
          {sugestoes.length > 0 && !pessoa.id && (
            <ul className="absolute z-10 mt-1 w-full overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
              {sugestoes.map((p) => (
                <li key={p.id}>
                  <button
                    type="button"
                    onClick={() => {
                      setPessoa({ id: p.id, nome: p.nome })
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
        <Campo rotulo="Categoria">
          <select value={categoriaId} onChange={(e) => setCategoriaId(e.target.value)} className={estilos.campo}>
            <option value="">Sem categoria</option>
            {categorias.map((c) => (
              <option key={c.id} value={c.id}>{`${c.grupo} › ${c.nome}`}</option>
            ))}
          </select>
        </Campo>
        <Campo rotulo="Documento (opcional)">
          <input value={documento} onChange={(e) => setDocumento(e.target.value)} maxLength={60} placeholder="Ex.: boleto, NF, contrato" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Competência">
          <input type="date" value={competencia} onChange={(e) => setCompetencia(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Valor total (R$)">
          <input value={total} onChange={(e) => setTotal(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
        </Campo>
        <div className="grid grid-cols-2 gap-3">
          <Campo rotulo="Parcelas">
            <input value={quantidade} onChange={(e) => setQuantidade(e.target.value.replace(/\D/g, ''))} inputMode="numeric" className={estilos.campo} />
          </Campo>
          <Campo rotulo="1º vencimento">
            <input type="date" value={primeiro} onChange={(e) => setPrimeiro(e.target.value)} className={estilos.campo} />
          </Campo>
        </div>
        {parcelas.length > 1 && (
          <p className="text-xs text-muted sm:col-span-2">{parcelas.map((p, i) => `${i + 1}ª ${dataBr(p.vencimento)}: ${moeda(p.valor)}`).join(' · ')}</p>
        )}
        {parcelas.length <= 1 && (
          <Campo rotulo="Repetir todo mês (despesa/receita fixa)">
            <select value={repetir} onChange={(e) => setRepetir(e.target.value)} className={estilos.campo}>
              <option value="1">Não repetir</option>
              {[3, 6, 12, 24].map((m) => (
                <option key={m} value={m}>{`Por ${m} meses`}</option>
              ))}
            </select>
          </Campo>
        )}
        <Campo rotulo="Observação (opcional)" className="sm:col-span-2">
          <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={1000} className={estilos.campo} />
        </Campo>
        {erro && <p role="alert" className={`sm:col-span-2 ${estilos.erro}`}>{erro}</p>}
        <div className="flex justify-end gap-2 sm:col-span-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" disabled={enviando} className={estilos.botaoPrimario}>{enviando ? 'Salvando...' : 'Salvar'}</button>
        </div>
      </form>
    </Dialogo>
  )
}

function BaixarParcela({ tipo, linha, onFechar, onSalvo }: { tipo: TipoDeTitulo; linha: LinhaDeParcela; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const contas = useContas()?.filter((c) => c.ativa) ?? []
  const [contaId, setContaId] = useState('')
  const [data, setData] = useState(hojeIso())
  const [valor, setValor] = useState(String(linha.saldo).replace('.', ','))
  const [juros, setJuros] = useState('')
  const [multa, setMulta] = useState('')
  const [desconto, setDesconto] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const contaEscolhida = contaId || (contas.length === 1 ? contas[0]!.id : '')
  const numero = (t: string) => lerNumero(t) ?? 0
  const totalPago = numero(valor) + numero(juros) + numero(multa) - numero(desconto)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    try {
      await enviar('POST', `/api/financeiro/parcelas/${linha.parcelaId}/baixas`, {
        data,
        contaId: contaEscolhida,
        valor: numero(valor),
        juros: numero(juros),
        multa: numero(multa),
        desconto: numero(desconto),
      })
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível registrar.')
    }
  }

  return (
    <Dialogo titulo={`${TEXTOS[tipo].baixar}: ${linha.descricao}`} onFechar={onFechar}>
      <form onSubmit={salvar} className="space-y-4">
        <p className="text-sm text-muted">{`Parcela ${linha.numero}/${linha.parcelas} · vence ${dataBr(linha.vencimento)} · saldo ${moeda(linha.saldo)}`}</p>
        {contas.length === 0 && (
          <p className={estilos.erro}>Cadastre uma conta financeira (caixa ou banco) em Financeiro → Contas financeiras.</p>
        )}
        <div className="grid gap-3 sm:grid-cols-2">
          <Campo rotulo="Conta">
            <select value={contaEscolhida} onChange={(e) => setContaId(e.target.value)} className={estilos.campo}>
              <option value="">Escolha...</option>
              {contas.map((c) => (
                <option key={c.id} value={c.id}>{c.nome}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Data">
            <input type="date" value={data} onChange={(e) => setData(e.target.value)} className={estilos.campo} />
          </Campo>
          <Campo rotulo="Valor da parcela (R$)">
            <input value={valor} onChange={(e) => setValor(e.target.value)} inputMode="decimal" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Juros (R$)">
            <input value={juros} onChange={(e) => setJuros(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Multa (R$)">
            <input value={multa} onChange={(e) => setMulta(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Desconto (R$)">
            <input value={desconto} onChange={(e) => setDesconto(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
          </Campo>
        </div>
        <p className="text-sm">{`Total ${tipo === 'pagar' ? 'pago' : 'recebido'}: `}<span className="font-bold tabular-nums">{moeda(totalPago)}</span></p>
        {numero(valor) < linha.saldo && numero(valor) > 0 && <p className="text-xs text-muted">Baixa parcial: o restante continua em aberto.</p>}
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" disabled={!contaEscolhida} className={estilos.botaoPrimario}>Confirmar</button>
        </div>
      </form>
    </Dialogo>
  )
}

function DetalheDoTitulo({ id, onFechar, onMudou }: { id: string; onFechar: () => void; onMudou: () => void }) {
  const { getJson, enviar } = useApi()
  const [titulo, setTitulo] = useState<Titulo | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<Titulo>(`/api/financeiro/titulos/${id}`, controle.signal)
      .then(setTitulo)
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, id])

  async function agir(caminho: string) {
    setErro(null)
    try {
      setTitulo(await enviar<Titulo>('POST', caminho))
      onMudou()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível concluir.')
    }
  }

  return (
    <Dialogo titulo={titulo?.descricao ?? 'Título'} onFechar={onFechar} largo>
      {!titulo ? (
        <p className="text-sm text-muted">Carregando...</p>
      ) : (
        <div className="space-y-4 text-sm">
          <p className="text-muted">
            {[titulo.pessoaNome, titulo.categoria, titulo.documento, `competência ${dataBr(titulo.competencia)}`, titulo.origem === 'NotaDeEntrada' ? 'gerado pela nota de entrada' : null]
              .filter(Boolean)
              .join(' · ')}
          </p>
          {titulo.observacao && <p>{titulo.observacao}</p>}
          <ul className="divide-y divide-border rounded-xl border border-border">
            {titulo.parcelas.map((p) => (
              <li key={p.id} className="px-3 py-2.5">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-semibold">{`${p.numero}/${titulo.parcelas.length} · ${dataBr(p.vencimento)}`}</span>
                  <SeloDeSituacao situacao={p.situacao} />
                  <span className="flex-1" />
                  <span className="tabular-nums">{`${moeda(p.valor)} · saldo ${moeda(p.saldo)}`}</span>
                </div>
                {p.baixas.map((b) => (
                  <div key={b.id} className={`mt-1 flex flex-wrap items-center gap-2 text-xs ${b.estornada ? 'text-muted line-through' : ''}`}>
                    <span>{`${dataBr(b.data)} · ${b.conta} · ${moeda(b.total)}`}</span>
                    {(b.juros > 0 || b.multa > 0 || b.desconto > 0) && <span className="text-muted">{`(juros ${moeda(b.juros)}, multa ${moeda(b.multa)}, desconto ${moeda(b.desconto)})`}</span>}
                    {b.usuario && <span className="text-muted">{b.usuario}</span>}
                    {!b.estornada && (
                      <button type="button" onClick={() => void agir(`/api/financeiro/baixas/${b.id}/estornar`)} className="text-danger hover:underline">
                        Estornar
                      </button>
                    )}
                  </div>
                ))}
              </li>
            ))}
          </ul>
          <p className="text-right font-semibold">{`Total: ${moeda(titulo.valorTotal)}`}</p>
          {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
          {!titulo.cancelado && (
            <div className="flex justify-end">
              <button type="button" onClick={() => void agir(`/api/financeiro/titulos/${id}/cancelar`)} className={estilos.botao}>Cancelar título</button>
            </div>
          )}
        </div>
      )}
    </Dialogo>
  )
}
