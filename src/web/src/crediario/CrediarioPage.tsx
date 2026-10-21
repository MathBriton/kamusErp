import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { decimal, lerNumero, moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade } from '../components/grade/useGrade'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { SeloDeSituacao } from '../financeiro/Selo'
import { dataBr } from '../financeiro/tipos'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { salvarArquivo } from '../relatorios/salvarArquivo'
import {
  PERFIS_DE_GESTAO, PERFIS_DO_CREDIARIO,
  type Carteira, type ClienteDoCrediario, type LinhaDaCarteira, type ParcelaDoCrediario, type RegrasDoCrediario,
} from './tipos'

const FILTROS = { busca: '' }

/** Como o useListaPaginada, mas a carteira traz `clientes` e `registros` (o `total` dela é o saldo em aberto). */
function useCarteira(url: string | null, versao: number, atraso: number) {
  const { getJson } = useApi()
  const [estado, setEstado] = useState<{ carteira: Carteira | null; erro: string | null }>({ carteira: null, erro: null })
  useEffect(() => {
    if (!url) return
    const controle = new AbortController()
    const timer = window.setTimeout(() => {
      getJson<Carteira>(url, controle.signal)
        .then((carteira) => setEstado({ carteira, erro: null }))
        .catch((e: unknown) => {
          if (!controle.signal.aborted) setEstado({ carteira: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar a carteira.' })
        })
    }, atraso)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, url, versao, atraso])
  return estado
}

/**
 * Crediário próprio: faixas de atraso de toda a carteira, grade de clientes (busca, seleção para exportar a inadimplência),
 * ficha do cliente com parcelas, recebimento, carnê/recibo, limite e regras.
 */
export function CrediarioPage() {
  const { baixar } = useApi()
  const { perfilAtual } = useEmpresa()
  const permitido = PERFIS_DO_CREDIARIO.includes(perfilAtual)
  const gestao = PERFIS_DE_GESTAO.includes(perfilAtual)
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { carteira, erro } = useCarteira(permitido ? `/api/financeiro/crediario/carteira?${grade.consulta}` : null, versao, grade.filtros.busca ? 250 : 0)
  const [aberto, setAberto] = useState<string | null>(null)
  const [novo, setNovo] = useState(false)

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Crediário</h1>
        <p className={`mt-4 ${estilos.erro}`}>Seu perfil não acessa o crediário.</p>
      </div>
    )

  const recarregar = () => setVersao((v) => v + 1)
  const baixarCarne = async (clienteId: string) => {
    const r = await baixar(`/api/financeiro/crediario/clientes/${clienteId}/carne.pdf`)
    if (r.tipo === 'arquivo') salvarArquivo(r.blob, r.nome)
  }
  const faixas: [string, number][] = carteira
    ? [['A vencer', carteira.aVencer], ['1 a 30 dias', carteira.ate30], ['31 a 60 dias', carteira.ate60], ['61 a 90 dias', carteira.ate90], ['Mais de 90 dias', carteira.acima90]]
    : []

  const colunas: Coluna<LinhaDaCarteira>[] = [
    {
      titulo: 'Cliente',
      celula: (c) => <button type="button" onClick={() => setAberto(c.clienteId)} className="font-semibold text-primary hover:underline">{c.clienteNome}</button>,
    },
    { titulo: 'Limite', celula: (c) => moeda(c.limite), alinhar: 'direita' },
    { titulo: 'Em aberto', celula: (c) => moeda(c.emAberto), alinhar: 'direita' },
    { titulo: 'Atrasado', celula: (c) => <span className={c.atrasado > 0 ? 'font-semibold text-danger' : ''}>{moeda(c.atrasado)}</span>, alinhar: 'direita' },
    { titulo: 'Atraso', celula: (c) => (c.diasDeAtraso > 0 ? `${c.diasDeAtraso} dias` : '—'), alinhar: 'direita' },
    { titulo: 'Disponível', celula: (c) => moeda(c.disponivel), alinhar: 'direita' },
    {
      titulo: 'Situação',
      celula: (c) =>
        c.bloqueado ? (
          <span className="rounded-full bg-danger-soft px-2 py-0.5 text-xs font-semibold text-danger">Bloqueado</span>
        ) : (
          <span className="rounded-full bg-success-soft px-2 py-0.5 text-xs font-semibold text-success">Liberado</span>
        ),
    },
  ]

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Vendas"
        titulo="Crediário"
        descricao="Venda a prazo para clientes com limite aprovado. Parcela atrasada além do prazo das regras bloqueia novas compras."
        acoes={
          <>
            <BotaoExportar relatorio="inadimplencia" parametros={grade.parametros} selecionados={[...grade.selecionados]} />
            {gestao && (
              <button type="button" onClick={() => setNovo(true)} className={estilos.botaoPrimario}>
                <Icone nome="plus" />
                Liberar crediário para cliente
              </button>
            )}
          </>
        }
      />

      {carteira && (
        <dl className="mt-6 grid gap-3 sm:grid-cols-3 lg:grid-cols-6">
          {faixas.map(([rotulo, valor]) => (
            <div key={rotulo} className="rounded-2xl border border-border bg-surface p-3 shadow-sm">
              <dt className="text-xs text-muted">{rotulo}</dt>
              <dd className={`text-lg font-bold tabular-nums ${rotulo !== 'A vencer' && valor > 0 ? 'text-danger' : ''}`}>{moeda(valor)}</dd>
            </div>
          ))}
          <div className="rounded-2xl border border-border bg-surface-2 p-3 shadow-sm">
            <dt className="text-xs text-muted">Total a receber</dt>
            <dd className="text-lg font-bold tabular-nums">{moeda(carteira.total)}</dd>
          </div>
        </dl>
      )}

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <Campo rotulo="Buscar cliente" className="min-w-60 flex-1">
          <input value={grade.filtros.busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Nome do cliente" className={estilos.campo} />
        </Campo>
      </BarraDeFiltros>

      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      <GradeDeRegistros
        rotulo="Carteira do crediário"
        colunas={colunas}
        itens={carteira?.clientes ?? (erro ? [] : null)}
        chave={(c) => c.clienteId}
        nome={(c) => c.clienteNome}
        total={carteira?.registros ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        vazio={grade.ativos ? 'Nenhum cliente com essa busca.' : 'Nenhum cliente no crediário ainda.'}
        acoes={(c) => (
          <>
            <AcaoDaLinha rotulo={`Ver crediário de ${c.clienteNome}`} icone="eye" onClick={() => setAberto(c.clienteId)} />
            {c.emAberto > 0 && <AcaoDaLinha rotulo={`Carnê de ${c.clienteNome}`} icone="printer" onClick={() => void baixarCarne(c.clienteId)} />}
          </>
        )}
      />

      {gestao && <Regras />}

      {aberto && <FichaDoCrediario clienteId={aberto} gestao={gestao} onFechar={() => setAberto(null)} onMudou={recarregar} />}
      {novo && (
        <NovoCliente
          onFechar={() => setNovo(false)}
          onSalvo={(id) => {
            setNovo(false)
            recarregar()
            setAberto(id)
          }}
        />
      )}
    </div>
  )
}

function FichaDoCrediario({ clienteId, gestao, onFechar, onMudou }: { clienteId: string; gestao: boolean; onFechar: () => void; onMudou: () => void }) {
  const { getJson, baixar } = useApi()
  const [cliente, setCliente] = useState<ClienteDoCrediario | null>(null)
  const [recebendo, setRecebendo] = useState<ParcelaDoCrediario | null>(null)
  const [editando, setEditando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  const carregar = useCallback(
    (sinal?: AbortSignal) =>
      getJson<ClienteDoCrediario>(`/api/financeiro/crediario/clientes/${clienteId}`, sinal)
        .then(setCliente)
        .catch((e: unknown) => {
          if (!sinal?.aborted) setErro(e instanceof ErroDeApi ? e.message : 'Não foi possível abrir o crediário do cliente.')
        }),
    [getJson, clienteId],
  )

  useEffect(() => {
    const controle = new AbortController()
    void carregar(controle.signal)
    return () => controle.abort()
  }, [carregar])

  const carne = async () => {
    setErro(null)
    try {
      const r = await baixar(`/api/financeiro/crediario/clientes/${clienteId}/carne.pdf`)
      if (r.tipo === 'arquivo') salvarArquivo(r.blob, r.nome)
    } catch (e) {
      setErro(e instanceof ErroDeApi ? e.message : 'Não foi possível gerar o carnê.')
    }
  }

  const abertas = cliente?.parcelas.filter((p) => p.saldo > 0) ?? []
  const pagas = cliente?.parcelas.filter((p) => p.saldo === 0) ?? []

  return (
    <Dialogo titulo={cliente ? `Crediário de ${cliente.clienteNome}` : 'Crediário'} onFechar={onFechar} largo>
      {erro && <p role="alert" className={`mb-3 ${estilos.erro}`}>{erro}</p>}
      {cliente && (
        <div className="space-y-4 text-sm">
          <dl className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {[
              ['Limite', moeda(cliente.limite)],
              ['Em aberto', moeda(cliente.emAberto)],
              ['Disponível', moeda(cliente.disponivel)],
              ['Atraso', cliente.diasDeAtraso > 0 ? `${cliente.diasDeAtraso} dias` : 'em dia'],
            ].map(([rotulo, valor]) => (
              <div key={rotulo} className="rounded-xl bg-surface-2 p-3">
                <dt className="text-xs text-muted">{rotulo}</dt>
                <dd className="font-bold tabular-nums">{valor}</dd>
              </div>
            ))}
          </dl>
          {cliente.motivo && <p className={estilos.erro}>{cliente.motivo}</p>}

          <div className="flex flex-wrap gap-2">
            {abertas.length > 0 && <button type="button" onClick={() => void carne()} className={estilos.botao}>Carnê (PDF)</button>}
            {gestao && !editando && <button type="button" onClick={() => setEditando(true)} className={estilos.botao}>Alterar limite</button>}
          </div>

          {editando && (
            <FormularioDeLimite
              clienteId={cliente.clienteId}
              clienteNome={cliente.clienteNome}
              inicial={{ limite: cliente.limite, bloqueado: cliente.bloqueadoManualmente, observacao: cliente.observacao ?? '' }}
              onCancelar={() => setEditando(false)}
              onSalvo={() => {
                setEditando(false)
                void carregar()
                onMudou()
              }}
            />
          )}

          <table aria-label="Parcelas em aberto" className="w-full">
            <thead className="text-left text-xs text-muted uppercase">
              <tr>
                <th className="py-1">Parcela</th>
                <th className="py-1">Vencimento</th>
                <th className="py-1 text-right">Saldo</th>
                <th className="py-1 text-right">Encargos hoje</th>
                <th className="py-1">Situação</th>
                <th />
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {abertas.length === 0 && (
                <tr><td colSpan={6} className="py-3 text-center text-muted">Nenhuma parcela em aberto.</td></tr>
              )}
              {abertas.map((p) => (
                <tr key={p.parcelaId}>
                  <td className="py-1.5">
                    {`${p.numero}/${p.parcelas}`}
                    <span className="block text-xs text-muted">{p.descricao}</span>
                  </td>
                  <td className="py-1.5">{dataBr(p.vencimento)}</td>
                  <td className="py-1.5 text-right tabular-nums">{moeda(p.saldo)}</td>
                  <td className="py-1.5 text-right tabular-nums">{p.multa + p.juros > 0 ? moeda(p.multa + p.juros) : '—'}</td>
                  <td className="py-1.5"><SeloDeSituacao situacao={p.situacao} /></td>
                  <td className="py-1.5 text-right">
                    <button type="button" onClick={() => setRecebendo(p)} className={estilos.botao} aria-label={`Receber parcela ${p.numero}`}>Receber</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {pagas.length > 0 && <p className="text-xs text-muted">{`${pagas.length} parcela(s) já paga(s).`}</p>}
        </div>
      )}
      {recebendo && (
        <ReceberParcela
          parcela={recebendo}
          onFechar={() => setRecebendo(null)}
          onRecebido={() => {
            void carregar()
            onMudou()
          }}
        />
      )}
    </Dialogo>
  )
}

function ReceberParcela({ parcela, onFechar, onRecebido }: { parcela: ParcelaDoCrediario; onFechar: () => void; onRecebido: () => void }) {
  const { getJson, enviar, baixar } = useApi()
  const [contas, setContas] = useState<{ id: string; nome: string }[]>([])
  const [contaId, setContaId] = useState('')
  const [valor, setValor] = useState(decimal(parcela.saldo))
  const [multa, setMulta] = useState(decimal(parcela.multa))
  const [juros, setJuros] = useState(decimal(parcela.juros))
  const [desconto, setDesconto] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [recebido, setRecebido] = useState<{ baixaId: string; total: number } | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<{ id: string; nome: string }[]>('/api/financeiro/crediario/contas', controle.signal).then(setContas).catch(() => {})
    return () => controle.abort()
  }, [getJson])

  const conta = contaId || contas[0]?.id || ''
  const n = (t: string) => Math.max(0, lerNumero(t) ?? 0)
  const total = Math.round((n(valor) + n(multa) + n(juros) - n(desconto)) * 100) / 100

  async function receber(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    try {
      const r = await enviar<{ baixaId: string; total: number }>('POST', `/api/financeiro/crediario/parcelas/${parcela.parcelaId}/receber`, {
        contaId: conta, valor: n(valor), multa: n(multa), juros: n(juros), desconto: n(desconto),
      })
      setRecebido(r)
      onRecebido()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível receber.')
    }
  }

  const recibo = async () => {
    if (!recebido) return
    const r = await baixar(`/api/financeiro/crediario/baixas/${recebido.baixaId}/recibo.pdf`)
    if (r.tipo === 'arquivo') salvarArquivo(r.blob, r.nome)
  }

  return (
    <Dialogo titulo={`Receber parcela ${parcela.numero} de ${parcela.parcelas}`} onFechar={onFechar}>
      {recebido ? (
        <div className="space-y-4 text-sm">
          <p className="rounded-lg bg-success-soft px-3 py-2.5 font-semibold text-success">{`Recebido: ${moeda(recebido.total)}`}</p>
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => void recibo()} className={estilos.botao}>Recibo (PDF)</button>
            <button type="button" onClick={onFechar} className={estilos.botaoPrimario}>Concluir</button>
          </div>
        </div>
      ) : (
        <form onSubmit={receber} className="grid gap-3 text-sm sm:grid-cols-2">
          <p className="text-muted sm:col-span-2">
            {`${parcela.descricao} · vencimento ${dataBr(parcela.vencimento)}${parcela.diasDeAtraso > 0 ? ` · ${parcela.diasDeAtraso} dias de atraso` : ''}`}
          </p>
          <Campo rotulo="Conta" className="sm:col-span-2">
            <select value={conta} onChange={(e) => setContaId(e.target.value)} className={estilos.campo}>
              {contas.map((c) => (
                <option key={c.id} value={c.id}>{c.nome}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Valor (R$)">
            <input value={valor} onChange={(e) => setValor(e.target.value)} inputMode="decimal" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Desconto (R$)">
            <input value={desconto} onChange={(e) => setDesconto(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Multa (R$)">
            <input value={multa} onChange={(e) => setMulta(e.target.value)} inputMode="decimal" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Juros (R$)">
            <input value={juros} onChange={(e) => setJuros(e.target.value)} inputMode="decimal" className={estilos.campo} />
          </Campo>
          <p className="text-base font-bold sm:col-span-2">{`Total: ${moeda(total)}`}</p>
          {erro && <p role="alert" className={`sm:col-span-2 ${estilos.erro}`}>{erro}</p>}
          <div className="flex justify-end gap-2 sm:col-span-2">
            <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
            <button type="submit" disabled={!conta || n(valor) <= 0} className={estilos.botaoPrimario}>Confirmar recebimento</button>
          </div>
        </form>
      )}
    </Dialogo>
  )
}

function FormularioDeLimite({ clienteId, clienteNome, inicial, onCancelar, onSalvo }: {
  clienteId: string
  clienteNome: string
  inicial: { limite: number; bloqueado: boolean; observacao: string }
  onCancelar: () => void
  onSalvo: () => void
}) {
  const { enviar } = useApi()
  const [limite, setLimite] = useState(decimal(inicial.limite))
  const [bloqueado, setBloqueado] = useState(inicial.bloqueado)
  const [observacao, setObservacao] = useState(inicial.observacao)
  const [erro, setErro] = useState<string | null>(null)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    try {
      await enviar('PUT', `/api/financeiro/crediario/clientes/${clienteId}/limite`, {
        clienteNome, limite: lerNumero(limite) ?? 0, bloqueado, observacao: observacao.trim() || null,
      })
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.')
    }
  }

  return (
    <form onSubmit={salvar} className="grid gap-3 rounded-xl border border-border p-4 sm:grid-cols-2">
      <Campo rotulo="Limite (R$)">
        <input value={limite} onChange={(e) => setLimite(e.target.value)} inputMode="decimal" className={estilos.campo} />
      </Campo>
      <label className="flex items-center gap-2 self-end pb-2">
        <input type="checkbox" checked={bloqueado} onChange={(e) => setBloqueado(e.target.checked)} className="h-4 w-4" />
        Bloquear crediário (bloqueio manual)
      </label>
      <Campo rotulo="Observação" className="sm:col-span-2">
        <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={300} className={estilos.campo} />
      </Campo>
      {erro && <p role="alert" className={`sm:col-span-2 ${estilos.erro}`}>{erro}</p>}
      <div className="flex justify-end gap-2 sm:col-span-2">
        <button type="button" onClick={onCancelar} className={estilos.botao}>Cancelar</button>
        <button type="submit" className={estilos.botaoPrimario}>Salvar limite</button>
      </div>
    </form>
  )
}

function NovoCliente({ onFechar, onSalvo }: { onFechar: () => void; onSalvo: (clienteId: string) => void }) {
  const { getJson } = useApi()
  const [busca, setBusca] = useState('')
  const [sugestoes, setSugestoes] = useState<{ id: string; nome: string }[]>([])
  const [escolhido, setEscolhido] = useState<{ id: string; nome: string } | null>(null)

  useEffect(() => {
    const termo = busca.trim()
    if (escolhido || termo.length < 2) return
    const controle = new AbortController()
    const timer = window.setTimeout(() => {
      getJson<{ itens: { id: string; nome: string }[] }>(`/api/pessoas?${new URLSearchParams({ papel: 'cliente', situacao: 'ativos', busca: termo })}`, controle.signal)
        .then((r) => setSugestoes(r.itens.slice(0, 6)))
        .catch(() => {})
    }, 250)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, busca, escolhido])

  return (
    <Dialogo titulo="Liberar crediário" onFechar={onFechar}>
      {!escolhido ? (
        <div className="relative text-sm">
          <Campo rotulo="Cliente cadastrado">
            <input value={busca} onChange={(e) => setBusca(e.target.value)} autoFocus placeholder="Buscar pelo nome" className={estilos.campo} />
          </Campo>
          {sugestoes.length > 0 && (
            <ul className="mt-1 overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
              {sugestoes.map((p) => (
                <li key={p.id}>
                  <button type="button" onClick={() => setEscolhido(p)} className="w-full px-3 py-2 text-left hover:bg-surface-2">{p.nome}</button>
                </li>
              ))}
            </ul>
          )}
        </div>
      ) : (
        <div className="space-y-3 text-sm">
          <p className="font-semibold">{escolhido.nome}</p>
          <FormularioDeLimite
            clienteId={escolhido.id}
            clienteNome={escolhido.nome}
            inicial={{ limite: 0, bloqueado: false, observacao: '' }}
            onCancelar={onFechar}
            onSalvo={() => onSalvo(escolhido.id)}
          />
        </div>
      )}
    </Dialogo>
  )
}

function Regras() {
  const { getJson, enviar } = useApi()
  const [multa, setMulta] = useState('')
  const [juros, setJuros] = useState('')
  const [dias, setDias] = useState('')
  const [carregado, setCarregado] = useState(false)
  const [aviso, setAviso] = useState<{ tipo: 'ok' | 'erro'; texto: string } | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<RegrasDoCrediario>('/api/financeiro/crediario/configuracao', controle.signal)
      .then((r) => {
        setMulta(decimal(r.multaPercentual))
        setJuros(decimal(r.jurosMensalPercentual))
        setDias(String(r.diasParaBloquear))
        setCarregado(true)
      })
      .catch(() => {})
    return () => controle.abort()
  }, [getJson])

  if (!carregado) return null

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setAviso(null)
    try {
      await enviar('PUT', '/api/financeiro/crediario/configuracao', {
        multaPercentual: lerNumero(multa) ?? 0, jurosMensalPercentual: lerNumero(juros) ?? 0, diasParaBloquear: Number.parseInt(dias, 10) || 0,
      })
      setAviso({ tipo: 'ok', texto: 'Regras salvas.' })
    } catch (ex) {
      setAviso({ tipo: 'erro', texto: ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.' })
    }
  }

  return (
    <section className="mt-8 rounded-2xl border border-border bg-surface p-5 shadow-sm">
      <h2 className="font-semibold">Regras do crediário</h2>
      <form onSubmit={salvar} className="mt-3 flex flex-wrap items-end gap-3">
        <Campo rotulo="Multa por atraso (%)" className="w-44">
          <input value={multa} onChange={(e) => setMulta(e.target.value)} inputMode="decimal" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Juros ao mês (%)" className="w-40">
          <input value={juros} onChange={(e) => setJuros(e.target.value)} inputMode="decimal" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Dias de atraso para bloquear" className="w-56">
          <input value={dias} onChange={(e) => setDias(e.target.value.replace(/\D/g, ''))} inputMode="numeric" className={estilos.campo} />
        </Campo>
        <button type="submit" className={estilos.botao}>Salvar regras</button>
      </form>
      <p className="mt-2 text-xs text-muted">Multa limitada a 2% (CDC); juros cobrados por dia de atraso. Valide os percentuais com o seu contador.</p>
      {aviso && <p role={aviso.tipo === 'erro' ? 'alert' : 'status'} className={`mt-3 ${aviso.tipo === 'erro' ? estilos.erro : 'text-sm text-success'}`}>{aviso.texto}</p>}
    </section>
  )
}
