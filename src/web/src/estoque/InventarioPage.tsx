import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade, useListaPaginada } from '../components/grade/useGrade'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { BuscaDeProduto } from './BuscaDeProduto'
import { MatrizDeEstoque } from './MatrizDeEstoque'
import { pecas, PERFIS_QUE_MOVIMENTAM, type CorDaMatriz, type Matriz } from './tipos'
import { useLojas } from './useLojas'

interface Inventario {
  id: string
  lojaId: string
  loja: string
  status: 'Aberto' | 'Concluido' | 'Cancelado'
  observacao: string | null
  abertoEm: string
  abertoPor: string | null
  encerradoEm: string | null
  concluidoPor: string | null
  skusContados: number
  pecasContadas: number
}

interface Contagem {
  produtoId: string
  referencia: string
  descricao: string
  tamanhos: string[]
  cores: CorDaMatriz[]
  celulas: { skuId: string; corId: string; tamanho: string; ativo: boolean; contado: number | null }[]
}

interface Divergencia {
  skuId: string
  referencia: string
  descricao: string
  cor: string
  tamanho: string
  saldoNaAbertura: number
  contado: number | null
  diferenca: number
  valorDaDiferenca: number | null
}

interface Divergencias {
  itens: Divergencia[]
  sobras: number
  faltas: number
  valor: number | null
  naoContados: number
}

const STATUS: Record<Inventario['status'], { rotulo: string; classe: string }> = {
  Aberto: { rotulo: 'Em contagem', classe: 'bg-warning-soft text-warning' },
  Concluido: { rotulo: 'Concluído', classe: 'bg-success-soft text-success' },
  Cancelado: { rotulo: 'Cancelado', classe: 'bg-surface-2 text-muted' },
}

const dataHora = (iso: string) => new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

function Selo({ status }: { status: Inventario['status'] }) {
  return <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${STATUS[status].classe}`}>{STATUS[status].rotulo}</span>
}

const FILTROS = { situacao: '', lojaId: '', de: '', ate: '' }

/** Inventário: abrir (foto do saldo), contar sem ver o saldo (grade ou leitor), conferir divergências e concluir. A lista fica numa grade com filtros. */
export function InventarioPage() {
  const { perfilAtual } = useEmpresa()
  const [params, setParams] = useSearchParams()
  const id = params.get('id')

  if (!PERFIS_QUE_MOVIMENTAM.includes(perfilAtual))
    return (
      <div className="mx-auto max-w-3xl">
        <CabecalhoDaPagina secao="Estoque" titulo="Inventário" />
        <p className={`mt-4 ${estilos.erro}`}>Só Dono, Gerente e Estoquista fazem inventário.</p>
      </div>
    )

  if (id) return <DetalheDoInventario id={id} onVoltar={() => setParams({})} />
  return <ListaDeInventarios onAbrir={(novo) => setParams({ id: novo })} />
}

function ListaDeInventarios({ onAbrir }: { onAbrir: (id: string) => void }) {
  const { enviar } = useApi()
  const { consolidado } = useEmpresa()
  const grade = useGrade(FILTROS)
  const { dados, erro: erroDaLista } = useListaPaginada<{ itens: Inventario[]; total: number }>(`/api/estoque/inventarios?${grade.consulta}`)
  const lojas = useLojas()
  const ativas = lojas?.filter((l) => l.ativa) ?? []
  const [lojaId, setLojaId] = useState('')
  const [observacao, setObservacao] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const lojaEscolhida = lojaId || (ativas.length === 1 ? ativas[0]!.id : '')

  async function abrir(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    try {
      const novo = await enviar<Inventario>('POST', '/api/estoque/inventarios', { lojaId: lojaEscolhida, observacao })
      onAbrir(novo.id)
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível abrir o inventário.')
    }
  }

  const colunas: Coluna<Inventario>[] = [
    {
      titulo: 'Loja',
      celula: (i) => (
        <>
          <span className="font-semibold">{i.loja}</span>
          {i.observacao && <span className="block text-xs text-muted">{i.observacao}</span>}
        </>
      ),
    },
    { titulo: 'Situação', celula: (i) => <Selo status={i.status} /> },
    { titulo: 'Aberto', celula: (i) => <span className="text-muted">{`${dataHora(i.abertoEm)}${i.abertoPor ? ` · ${i.abertoPor}` : ''}`}</span> },
    { titulo: 'Contado', celula: (i) => `${i.skusContados} SKU(s) · ${pecas(i.pecasContadas)}`, alinhar: 'direita' },
  ]

  return (
    <div className="mx-auto max-w-6xl">
      <CabecalhoDaPagina
        secao="Estoque"
        titulo="Inventário"
        descricao="Ao abrir, o sistema guarda a foto do saldo. A contagem é cega (quem conta não vê o saldo) e, ao concluir, o estoque é ajustado pela diferença."
        acoes={<BotaoExportar relatorio="inventarios" parametros={grade.parametros} selecionados={[...grade.selecionados]} />}
      />

      {consolidado ? (
        <p className={`mt-6 ${estilos.erro}`}>Escolha uma empresa no seletor do topo para abrir um inventário.</p>
      ) : (
        <form onSubmit={abrir} className="mt-6 flex flex-wrap items-end gap-3 rounded-2xl border border-border bg-surface p-5 shadow-sm">
          <Campo rotulo="Loja" className="min-w-52">
            <select value={lojaEscolhida} onChange={(e) => setLojaId(e.target.value)} className={estilos.campo}>
              <option value="">Escolha...</option>
              {ativas.map((l) => (
                <option key={l.id} value={l.id}>{l.nome}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Observação (opcional)" className="min-w-60 flex-1">
            <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={500} placeholder="Ex.: balanço de fim de mês" className={estilos.campo} />
          </Campo>
          <button type="submit" disabled={!lojaEscolhida} className={estilos.botaoPrimario}>Abrir inventário</button>
        </form>
      )}
      {(erro ?? erroDaLista) && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro ?? erroDaLista}</p>}

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="w-44 text-sm">
          <span className="mb-1 block font-semibold">Situação</span>
          <select value={grade.filtros.situacao} onChange={(e) => grade.filtrar({ situacao: e.target.value })} className={estilos.campo}>
            <option value="">Todas</option>
            <option value="Aberto">Em contagem</option>
            <option value="Concluido">Concluídos</option>
            <option value="Cancelado">Cancelados</option>
          </select>
        </label>
        <label className="w-52 text-sm">
          <span className="mb-1 block font-semibold">Loja do inventário</span>
          <select value={grade.filtros.lojaId} onChange={(e) => grade.filtrar({ lojaId: e.target.value })} className={estilos.campo}>
            <option value="">Todas as lojas</option>
            {lojas?.map((l) => (
              <option key={l.id} value={l.id}>{l.nome}</option>
            ))}
          </select>
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">Aberto de</span>
          <input type="date" value={grade.filtros.de} onChange={(e) => grade.filtrar({ de: e.target.value })} className={estilos.campo} />
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">Até</span>
          <input type="date" value={grade.filtros.ate} onChange={(e) => grade.filtrar({ ate: e.target.value })} className={estilos.campo} />
        </label>
      </BarraDeFiltros>

      <GradeDeRegistros
        rotulo="Inventários"
        colunas={colunas}
        itens={dados?.itens ?? (erroDaLista ? [] : null)}
        chave={(i) => i.id}
        nome={(i) => `${i.loja} ${dataHora(i.abertoEm)}`}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        apagada={(i) => i.status === 'Cancelado'}
        vazio={grade.ativos ? 'Nenhum inventário com esses filtros.' : 'Nenhum inventário ainda.'}
        acoes={(i) => <AcaoDaLinha rotulo={`Abrir inventário de ${i.loja}`} icone="eye" onClick={() => onAbrir(i.id)} />}
      />
    </div>
  )
}

type Aba = 'grade' | 'leitor' | 'divergencias'

function DetalheDoInventario({ id, onVoltar }: { id: string; onVoltar: () => void }) {
  const { getJson, enviar } = useApi()
  const { perfilAtual } = useEmpresa()
  const aprova = perfilAtual === 'Dono' || perfilAtual === 'Gerente'
  const [inventario, setInventario] = useState<Inventario | null>(null)
  const [aba, setAba] = useState<Aba>('grade')
  const [versao, setVersao] = useState(0)
  const [confirmar, setConfirmar] = useState(false)
  const [zerar, setZerar] = useState(false)
  const [aviso, setAviso] = useState<string | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  const recarregar = useCallback(() => setVersao((v) => v + 1), [])

  useEffect(() => {
    const controle = new AbortController()
    getJson<Inventario>(`/api/estoque/inventarios/${id}`, controle.signal)
      .then(setInventario)
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, id, versao])

  if (!inventario) return <p className="text-sm text-muted">Carregando...</p>
  const aberto = inventario.status === 'Aberto'

  async function concluir() {
    setErro(null)
    try {
      const r = await enviar<{ ajustes: number; sobras: number; faltas: number }>('POST', `/api/estoque/inventarios/${id}/concluir`, { zerarNaoContados: zerar })
      setAviso(`Inventário concluído: ${r.ajustes} SKU(s) ajustados — sobras de ${pecas(r.sobras)}, faltas de ${pecas(r.faltas)}.`)
      setConfirmar(false)
      recarregar()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível concluir.')
    }
  }

  async function cancelar() {
    setErro(null)
    try {
      await enviar('POST', `/api/estoque/inventarios/${id}/cancelar`)
      setAviso('Inventário cancelado. Nada foi ajustado.')
      recarregar()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível cancelar.')
    }
  }

  return (
    <div className="mx-auto max-w-5xl">
      <button type="button" onClick={onVoltar} className="text-sm text-primary hover:underline">← Inventários</button>
      <div className="mt-2 flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="flex items-center gap-3 text-2xl font-bold tracking-tight">
            {`Inventário — ${inventario.loja}`}
            <Selo status={inventario.status} />
          </h1>
          <p className="mt-1 text-sm text-muted">
            {`Aberto em ${dataHora(inventario.abertoEm)}${inventario.abertoPor ? ` por ${inventario.abertoPor}` : ''} · ${inventario.skusContados} SKU(s) contados, ${pecas(inventario.pecasContadas)}`}
            {inventario.observacao ? ` · ${inventario.observacao}` : ''}
          </p>
        </div>
        {aberto && aprova && (
          <>
            <button type="button" onClick={() => void cancelar()} className={estilos.botao}>Cancelar inventário</button>
            <button type="button" onClick={() => setConfirmar(true)} className={estilos.botaoPrimario}>Concluir e ajustar</button>
          </>
        )}
      </div>

      {aviso && <p role="status" className="mt-4 rounded-lg bg-success-soft px-3 py-2.5 text-sm text-success">{aviso}</p>}
      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      <div role="tablist" className="mt-6 flex gap-1 border-b border-border">
        {(
          [
            ['grade', 'Contar pela grade'],
            ['leitor', 'Leitor de código'],
            ['divergencias', 'Divergências'],
          ] as [Aba, string][]
        ).map(([valor, rotulo]) => (
          <button
            key={valor}
            type="button"
            role="tab"
            aria-selected={aba === valor}
            onClick={() => setAba(valor)}
            className={`-mb-px border-b-2 px-4 py-2 text-sm font-semibold ${aba === valor ? 'border-primary text-primary' : 'border-transparent text-muted hover:text-foreground'}`}
          >
            {rotulo}
          </button>
        ))}
      </div>

      <div className="mt-4">
        {aba === 'grade' && <ContarPelaGrade id={id} aberto={aberto} onSalvo={recarregar} />}
        {aba === 'leitor' && <Leitor id={id} aberto={aberto} onLido={recarregar} />}
        {aba === 'divergencias' && <ListaDeDivergencias id={id} versao={versao} zerar={zerar} onZerar={setZerar} />}
      </div>

      {confirmar && (
        <Dialogo titulo="Concluir inventário" onFechar={() => setConfirmar(false)}>
          <div className="space-y-4 text-sm">
            <p>O estoque de {inventario.loja} será ajustado pela diferença entre o contado e a foto tirada na abertura. Isso não pode ser desfeito.</p>
            <label className="flex items-start gap-2">
              <input type="checkbox" checked={zerar} onChange={(e) => setZerar(e.target.checked)} className="mt-0.5 accent-primary" />
              <span>
                <span className="font-semibold">Zerar o que não foi contado</span>
                <span className="block text-muted">Marque se a loja inteira foi contada. Sem marcar, os SKUs não contados ficam como estão.</span>
              </span>
            </label>
            <div className="flex justify-end gap-2">
              <button type="button" onClick={() => setConfirmar(false)} className={estilos.botao}>Voltar</button>
              <button type="button" onClick={() => void concluir()} className={estilos.botaoPrimario}>Concluir e ajustar</button>
            </div>
          </div>
        </Dialogo>
      )}
    </div>
  )
}

function ContarPelaGrade({ id, aberto, onSalvo }: { id: string; aberto: boolean; onSalvo: () => void }) {
  const { getJson, enviar } = useApi()
  const [contagem, setContagem] = useState<Contagem | null>(null)
  const [valores, setValores] = useState<Record<string, string>>({})
  const [mensagem, setMensagem] = useState<{ tipo: 'ok' | 'erro'; texto: string } | null>(null)

  const escolher = (produtoId: string) => {
    setMensagem(null)
    getJson<Contagem>(`/api/estoque/inventarios/${id}/produtos/${produtoId}`)
      .then((c) => {
        setContagem(c)
        setValores(Object.fromEntries(c.celulas.filter((x) => x.contado !== null).map((x) => [x.skuId, String(x.contado)])))
      })
      .catch(() => setMensagem({ tipo: 'erro', texto: 'Não foi possível abrir o produto.' }))
  }

  async function salvar() {
    if (!contagem) return
    const itens = contagem.celulas.flatMap((c) => {
      const texto = valores[c.skuId] ?? ''
      const novo = texto === '' ? null : Number(texto)
      return novo === c.contado ? [] : [{ skuId: c.skuId, quantidade: novo }]
    })
    if (itens.length === 0) {
      setMensagem({ tipo: 'erro', texto: 'Nada mudou nesta grade.' })
      return
    }
    try {
      await enviar('PUT', `/api/estoque/inventarios/${id}/contagens`, { itens })
      setMensagem({ tipo: 'ok', texto: `Contagem de ${contagem.referencia} salva.` })
      setContagem(null)
      onSalvo()
    } catch (ex) {
      setMensagem({ tipo: 'erro', texto: ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.' })
    }
  }

  const matriz: Matriz | null = contagem && {
    produtoId: contagem.produtoId,
    referencia: contagem.referencia,
    descricao: contagem.descricao,
    precoVenda: 0,
    precoCusto: null,
    tamanhos: contagem.tamanhos,
    cores: contagem.cores,
    celulas: contagem.celulas.map((c) => ({ ...c, codigoDeBarras: '', quantidade: c.contado ?? 0, custoMedio: null })),
    lojas: [],
    total: 0,
  }

  return (
    <div className="space-y-4">
      {mensagem && <p role={mensagem.tipo === 'ok' ? 'status' : 'alert'} className={mensagem.tipo === 'ok' ? 'text-sm text-success' : estilos.erro}>{mensagem.texto}</p>}
      {!matriz && <BuscaDeProduto onEscolher={escolher} desabilitado={!aberto} placeholder={aberto ? undefined : 'Inventário encerrado'} />}
      {matriz && (
        <div className="space-y-3 rounded-xl border border-primary p-4">
          <p className="font-semibold">
            <span className="font-mono text-xs text-muted">{matriz.referencia}</span> {matriz.descricao}
          </p>
          <MatrizDeEstoque matriz={matriz} digitacao={{ valores, onMudar: (skuId, v) => setValores((x) => ({ ...x, [skuId]: v })), ocultarSaldo: true }} />
          <p className="text-xs text-muted">Digite quantas peças foram contadas. Deixe vazio o que não foi contado.</p>
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setContagem(null)} className={estilos.botao}>Cancelar</button>
            <button type="button" onClick={() => void salvar()} className={estilos.botaoPrimario}>Salvar contagem</button>
          </div>
        </div>
      )}
    </div>
  )
}

interface Leitura {
  skuId: string
  referencia: string
  descricao: string
  cor: string
  tamanho: string
  contado: number
}

function Leitor({ id, aberto, onLido }: { id: string; aberto: boolean; onLido: () => void }) {
  const { enviar } = useApi()
  const [codigo, setCodigo] = useState('')
  const [lidas, setLidas] = useState<Leitura[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const campo = useRef<HTMLInputElement>(null)

  async function ler(e: FormEvent) {
    e.preventDefault()
    const valor = codigo.trim()
    if (!valor) return
    setCodigo('')
    setErro(null)
    try {
      const r = await enviar<Leitura>('POST', `/api/estoque/inventarios/${id}/leituras`, { codigo: valor })
      setLidas((atual) => [r, ...atual].slice(0, 15))
      onLido()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível registrar a leitura.')
    } finally {
      campo.current?.focus()
    }
  }

  return (
    <div className="space-y-4">
      <form onSubmit={ler} className="flex gap-2">
        <input
          ref={campo}
          aria-label="Código de barras"
          autoFocus
          value={codigo}
          onChange={(e) => setCodigo(e.target.value)}
          disabled={!aberto}
          inputMode="numeric"
          placeholder="Passe o leitor ou digite o código e tecle Enter (cada leitura soma 1)"
          className={estilos.campo}
        />
        <button type="submit" disabled={!aberto} className={estilos.botaoPrimario}>Registrar</button>
      </form>
      {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
      {lidas.length > 0 && (
        <ul aria-label="Últimas leituras" className="divide-y divide-border rounded-xl border border-border">
          {lidas.map((l, i) => (
            <li key={`${l.skuId}-${i}`} className="flex items-center justify-between px-3 py-2 text-sm">
              <span>
                <span className="font-mono text-xs text-muted">{l.referencia}</span> {`${l.descricao} · ${l.cor} ${l.tamanho}`}
              </span>
              <span className="font-semibold tabular-nums">{`contado: ${l.contado}`}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function ListaDeDivergencias({ id, versao, zerar, onZerar }: { id: string; versao: number; zerar: boolean; onZerar: (v: boolean) => void }) {
  const { getJson } = useApi()
  const [dados, setDados] = useState<Divergencias | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<Divergencias>(`/api/estoque/inventarios/${id}/divergencias?zerarNaoContados=${zerar}`, controle.signal)
      .then(setDados)
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, id, zerar, versao])

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-4">
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={zerar} onChange={(e) => onZerar(e.target.checked)} className="accent-primary" />
          Considerar como zero o que não foi contado
        </label>
        <span className="flex-1" />
        <BotaoExportar relatorio="inventario" parametros={{ inventarioId: id, zerarNaoContados: String(zerar) }} />
      </div>
      {dados && (
        <>
          <p className="text-sm">
            <span className="font-semibold text-success">{`Sobras: ${pecas(dados.sobras)}`}</span>
            {' · '}
            <span className="font-semibold text-danger">{`Faltas: ${pecas(dados.faltas)}`}</span>
            {dados.valor !== null && ` · Valor (custo): ${moeda(dados.valor)}`}
            {!zerar && dados.naoContados > 0 && <span className="text-muted">{` · ${dados.naoContados} SKU(s) com saldo ainda não contados`}</span>}
          </p>
          {dados.itens.length === 0 ? (
            <p className="rounded-xl border border-border p-6 text-center text-sm text-muted">Nenhuma divergência até agora.</p>
          ) : (
            <div className="overflow-x-auto rounded-xl border border-border">
              <table aria-label="Divergências" className="w-full text-sm">
                <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                  <tr>
                    <th className="px-3 py-2">Produto</th>
                    <th className="px-3 py-2">Cor / tam.</th>
                    <th className="px-3 py-2 text-right">Na abertura</th>
                    <th className="px-3 py-2 text-right">Contado</th>
                    <th className="px-3 py-2 text-right">Diferença</th>
                    <th className="px-3 py-2 text-right">Valor</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {dados.itens.map((d) => (
                    <tr key={d.skuId}>
                      <td className="px-3 py-2">
                        <span className="font-mono text-xs text-muted">{d.referencia}</span> {d.descricao}
                      </td>
                      <td className="px-3 py-2">{`${d.cor} ${d.tamanho}`}</td>
                      <td className="px-3 py-2 text-right tabular-nums">{d.saldoNaAbertura}</td>
                      <td className="px-3 py-2 text-right tabular-nums">{d.contado ?? '—'}</td>
                      <td className={`px-3 py-2 text-right font-semibold tabular-nums ${d.diferenca < 0 ? 'text-danger' : 'text-success'}`}>
                        {d.diferenca > 0 ? `+${d.diferenca}` : d.diferenca}
                      </td>
                      <td className="px-3 py-2 text-right tabular-nums">{d.valorDaDiferenca === null ? '—' : moeda(d.valorDaDiferenca)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  )
}
