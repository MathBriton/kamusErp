import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { lerNumero, moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { Confirmacao } from '../components/grade/Confirmacao'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade, useListaPaginada } from '../components/grade/useGrade'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { Indicador } from './Indicador'
import { SeloDeSituacao } from './Selo'
import {
  dataBr, hojeIso, PERFIS_DO_FINANCEIRO, somarMeses, type LinhaDeParcela, type ListaDeParcelas, type ResultadoDaBaixaEmLote, type TipoDeTitulo, type Titulo,
} from './tipos'
import { useCategorias, useContas } from './useFinanceiro'

const TEXTOS: Record<TipoDeTitulo, {
  titulo: string; subtitulo: string; pessoa: string; papel: string; novo: string; baixar: string; baixadas: [string, string]; categoria: 'Despesa' | 'Receita'
}> = {
  pagar: {
    titulo: 'Contas a pagar',
    subtitulo: 'Fornecedores, despesas fixas e impostos. Notas de entrada lançadas no estoque entram aqui sozinhas.',
    pessoa: 'Fornecedor',
    papel: 'fornecedor',
    novo: 'Nova conta a pagar',
    baixar: 'Pagar',
    baixadas: ['paga', 'pagas'],
    categoria: 'Despesa',
  },
  receber: {
    titulo: 'Contas a receber',
    subtitulo: 'Valores a receber de clientes e outras receitas.',
    pessoa: 'Cliente',
    papel: 'cliente',
    novo: 'Nova conta a receber',
    baixar: 'Receber',
    baixadas: ['recebida', 'recebidas'],
    categoria: 'Receita',
  },
}

type Janela =
  | { tipo: 'novo' }
  | { tipo: 'baixar'; linha: LinhaDeParcela }
  | { tipo: 'detalhe'; tituloId: string }
  | { tipo: 'cancelar'; linha: LinhaDeParcela }
  | { tipo: 'baixarSelecionadas'; linhas: LinhaDeParcela[] }
  | null

const FILTROS = { situacao: 'abertas', busca: '', de: '', ate: '' }

/** "Reforma da vitrine 1/2": nome da linha (uma parcela) nas ações e na seleção. */
const nomeDaLinha = (l: LinhaDeParcela) => `${l.descricao} ${l.numero}/${l.parcelas}`
const temSaldo = (l: LinhaDeParcela) => l.saldo > 0 && l.situacao !== 'Cancelada'
const quantasParcelas = (n: number) => `${n} ${n === 1 ? 'parcela' : 'parcelas'}`

/**
 * Contas a pagar ou a receber no padrão de grade: uma linha por parcela, com filtros, seleção, baixa (total ou parcial),
 * detalhe com estorno, cancelamento, baixa das selecionadas e exportação (das selecionadas ou dos filtros).
 */
export function ContasPage({ tipo }: { tipo: TipoDeTitulo }) {
  const { enviar } = useApi()
  const { perfilAtual, consolidado } = useEmpresa()
  const textos = TEXTOS[tipo]
  const permitido = PERFIS_DO_FINANCEIRO.includes(perfilAtual)
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { dados, erro } = useListaPaginada<ListaDeParcelas>(
    permitido ? `/api/financeiro/titulos?tipo=${tipo}&${grade.consulta}` : null,
    versao,
    grade.filtros.busca ? 250 : 0,
  )
  const [janela, setJanela] = useState<Janela>(null)
  // Linhas já vistas (de qualquer página): a seleção sobrevive à troca de página e a baixa em lote precisa do saldo delas.
  const vistas = useRef(new Map<string, LinhaDeParcela>())
  useEffect(() => {
    for (const l of dados?.itens ?? []) vistas.current.set(l.parcelaId, l)
  }, [dados])

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">{textos.titulo}</h1>
        <p className={`mt-4 ${estilos.erro}`}>Só Dono, Gerente e Financeiro acessam o financeiro.</p>
      </div>
    )

  const recarregar = () => setVersao((v) => v + 1)
  const fecharERecarregar = () => {
    setJanela(null)
    recarregar()
  }

  const colunas: Coluna<LinhaDeParcela>[] = [
    { titulo: 'Vencimento', celula: (l) => <span className="whitespace-nowrap tabular-nums">{dataBr(l.vencimento)}</span> },
    {
      titulo: 'Descrição',
      celula: (l) => (
        <>
          <span className="font-semibold">{l.descricao}</span>
          {l.parcelas > 1 && <span className="ml-1 text-xs text-muted">{`${l.numero}/${l.parcelas}`}</span>}
          <span className="block text-xs text-muted">{[l.pessoaNome, l.documento].filter(Boolean).join(' · ')}</span>
        </>
      ),
    },
    { titulo: 'Categoria', celula: (l) => <span className="text-muted">{l.categoria ?? '—'}</span> },
    { titulo: 'Valor', celula: (l) => moeda(l.valor), alinhar: 'direita' },
    { titulo: 'Saldo', celula: (l) => <span className="font-semibold">{moeda(l.saldo)}</span>, alinhar: 'direita' },
    { titulo: 'Situação', celula: (l) => <SeloDeSituacao situacao={l.situacao} /> },
  ]

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Financeiro"
        titulo={textos.titulo}
        descricao={textos.subtitulo}
        acoes={
          <>
            <BotaoExportar relatorio="titulos" parametros={{ tipo, ...grade.parametros }} selecionados={[...grade.selecionados]} />
            {!consolidado && (
              <button type="button" onClick={() => setJanela({ tipo: 'novo' })} className={estilos.botaoPrimario}>
                <Icone nome="plus" />
                {textos.novo}
              </button>
            )}
          </>
        }
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="min-w-56 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input
            value={grade.filtros.busca}
            onChange={(e) => grade.filtrar({ busca: e.target.value })}
            placeholder={`Descrição, ${textos.pessoa.toLowerCase()} ou documento`}
            className={estilos.campo}
          />
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">Situação</span>
          <select value={grade.filtros.situacao} onChange={(e) => grade.filtrar({ situacao: e.target.value })} className={estilos.campo}>
            <option value="abertas">Em aberto</option>
            <option value="vencidas">Vencidas</option>
            <option value="pagas">{tipo === 'pagar' ? 'Pagas' : 'Recebidas'}</option>
            <option value="canceladas">Canceladas</option>
            <option value="todas">Todas</option>
          </select>
        </label>
        <label className="text-sm">
          <span className="mb-1 block font-semibold">Vencimento de</span>
          <input type="date" value={grade.filtros.de} onChange={(e) => grade.filtrar({ de: e.target.value })} className={estilos.campo} />
        </label>
        <label className="text-sm">
          <span className="mb-1 block font-semibold">até</span>
          <input type="date" value={grade.filtros.ate} onChange={(e) => grade.filtrar({ ate: e.target.value })} className={estilos.campo} />
        </label>
      </BarraDeFiltros>

      {erro && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {dados && (
        <dl className="mt-4 grid gap-3 sm:grid-cols-3">
          <Indicador rotulo="Saldo em aberto" valor={moeda(dados.saldo)} />
          <Indicador rotulo="Vencido" valor={moeda(dados.vencido)} destaque={dados.vencido > 0} />
          <Indicador rotulo={tipo === 'pagar' ? 'Já pago' : 'Já recebido'} valor={moeda(dados.pago)} />
        </dl>
      )}

      <GradeDeRegistros
        rotulo={textos.titulo}
        colunas={colunas}
        itens={dados?.itens ?? (erro ? [] : null)}
        chave={(l) => l.parcelaId}
        nome={nomeDaLinha}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        apagada={(l) => l.situacao === 'Cancelada'}
        vazio="Nada por aqui com esses filtros."
        acoes={(l) => (
          <>
            <AcaoDaLinha rotulo={`Ver ${nomeDaLinha(l)}`} icone="eye" onClick={() => setJanela({ tipo: 'detalhe', tituloId: l.tituloId })} />
            {!consolidado && temSaldo(l) && (
              <AcaoDaLinha rotulo={`${textos.baixar} ${nomeDaLinha(l)}`} icone="dollar" onClick={() => setJanela({ tipo: 'baixar', linha: l })} />
            )}
            {!consolidado && l.situacao !== 'Cancelada' && (
              <AcaoDaLinha rotulo={`Cancelar título ${nomeDaLinha(l)}`} icone="x" perigo onClick={() => setJanela({ tipo: 'cancelar', linha: l })} />
            )}
          </>
        )}
        acoesEmLote={
          !consolidado && (
            <button
              type="button"
              onClick={() => setJanela({ tipo: 'baixarSelecionadas', linhas: [...grade.selecionados].flatMap((id) => vistas.current.get(id) ?? []) })}
              className={estilos.botao}
            >
              {`${textos.baixar} selecionadas`}
            </button>
          )
        }
      />

      {janela?.tipo === 'novo' && <NovoTitulo tipo={tipo} onFechar={() => setJanela(null)} onSalvo={fecharERecarregar} />}
      {janela?.tipo === 'baixar' && <BaixarParcela tipo={tipo} linha={janela.linha} onFechar={() => setJanela(null)} onSalvo={fecharERecarregar} />}
      {janela?.tipo === 'detalhe' && <DetalheDoTitulo id={janela.tituloId} onFechar={() => setJanela(null)} onMudou={recarregar} />}
      {janela?.tipo === 'cancelar' && (
        <Confirmacao
          titulo="Cancelar título"
          rotulo="Cancelar título"
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', `/api/financeiro/titulos/${janela.linha.tituloId}/cancelar`)
            fecharERecarregar()
          }}
        >
          <p>
            {`Cancelar "${janela.linha.descricao}"${janela.linha.parcelas > 1 ? ` e as suas ${janela.linha.parcelas} parcelas` : ''}? `}
            Só dá para cancelar sem pagamentos ativos — estorne as baixas antes, se houver.
          </p>
        </Confirmacao>
      )}
      {janela?.tipo === 'baixarSelecionadas' && (
        <BaixarSelecionadas
          tipo={tipo}
          linhas={janela.linhas}
          onFechar={() => setJanela(null)}
          onConcluido={() => {
            grade.limparSelecao()
            recarregar()
          }}
        />
      )}
    </div>
  )
}

/** Quita as parcelas selecionadas de uma vez: saldo inteiro, hoje, numa conta; mostra quantas deram e o motivo das que não deram. */
function BaixarSelecionadas({ tipo, linhas, onFechar, onConcluido }: {
  tipo: TipoDeTitulo
  linhas: LinhaDeParcela[]
  onFechar: () => void
  onConcluido: () => void
}) {
  const { enviar } = useApi()
  const textos = TEXTOS[tipo]
  const contas = useContas()?.filter((c) => c.ativa) ?? []
  const [contaId, setContaId] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [resultado, setResultado] = useState<ResultadoDaBaixaEmLote | null>(null)
  const elegiveis = linhas.filter(temSaldo)
  const total = elegiveis.reduce((s, l) => s + l.saldo, 0)
  const contaEscolhida = contaId || (contas.length === 1 ? contas[0]!.id : '')
  const hoje = hojeIso()

  async function confirmar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setErro(null)
    try {
      setResultado(await enviar<ResultadoDaBaixaEmLote>('POST', '/api/financeiro/parcelas/baixas-em-lote', {
        parcelaIds: elegiveis.map((l) => l.parcelaId),
        contaId: contaEscolhida,
        data: hoje,
      }))
      onConcluido()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível registrar.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={`${textos.baixar} selecionadas`} onFechar={onFechar}>
      {resultado ? (
        <div className="space-y-4 text-sm">
          <p role="status" className="rounded-lg bg-success-soft px-3 py-2.5 text-success">
            {`${quantasParcelas(resultado.baixadas)} ${resultado.baixadas === 1 ? textos.baixadas[0] : textos.baixadas[1]}: ${moeda(resultado.total)}`}
          </p>
          {resultado.falhas.length > 0 && (
            <div role="alert" className={estilos.erro}>
              <p className="font-semibold">{`Não deu para ${textos.baixar.toLowerCase()} ${quantasParcelas(resultado.falhas.length)}:`}</p>
              <ul className="mt-1 list-disc pl-5">
                {resultado.falhas.map((f) => (
                  <li key={f.parcelaId}>{`${f.descricao}: ${f.motivo}`}</li>
                ))}
              </ul>
            </div>
          )}
          <div className="flex justify-end">
            <button type="button" onClick={onFechar} className={estilos.botaoPrimario}>Concluir</button>
          </div>
        </div>
      ) : (
        <form onSubmit={(e) => void confirmar(e)} className="space-y-4 text-sm">
          <p>
            <span className="font-semibold">{quantasParcelas(elegiveis.length)}</span>
            {' · total '}
            <span className="font-semibold tabular-nums">{moeda(total)}</span>
            {` · em ${dataBr(hoje)}`}
          </p>
          <p className="text-muted">
            O saldo inteiro de cada parcela é baixado hoje, sem juros, multa ou desconto. Para baixa parcial ou com encargos, use a ação da linha.
          </p>
          {linhas.length > elegiveis.length && (
            <p className="text-muted">{`${quantasParcelas(linhas.length - elegiveis.length)} já quitada(s) ou cancelada(s) fica(m) de fora.`}</p>
          )}
          {contas.length === 0 && <p className={estilos.erro}>Cadastre uma conta financeira (caixa ou banco) em Financeiro → Contas financeiras.</p>}
          <Campo rotulo="Conta">
            <select value={contaEscolhida} onChange={(e) => setContaId(e.target.value)} className={estilos.campo}>
              <option value="">Escolha...</option>
              {contas.map((c) => (
                <option key={c.id} value={c.id}>{c.nome}</option>
              ))}
            </select>
          </Campo>
          {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
          <div className="flex justify-end gap-2">
            <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
            <button type="submit" disabled={enviando || !contaEscolhida || elegiveis.length === 0} className={estilos.botaoPrimario}>
              {enviando ? 'Aguarde...' : `${textos.baixar} ${quantasParcelas(elegiveis.length)}`}
            </button>
          </div>
        </form>
      )}
    </Dialogo>
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
