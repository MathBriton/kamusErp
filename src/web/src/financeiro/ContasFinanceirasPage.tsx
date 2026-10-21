import { useEffect, useState, type FormEvent } from 'react'
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
import { dataBr, hojeIso, PERFIS_DO_FINANCEIRO, somarDias, type ContaFinanceira, type ListaDeContas } from './tipos'
import { useContas } from './useFinanceiro'

interface Extrato {
  conta: string
  de: string
  ate: string
  saldoAnterior: number
  movimentos: { id: string; data: string; descricao: string; valor: number; saldo: number; usuario: string | null }[]
  saldoFinal: number
}

const TIPOS: Record<ContaFinanceira['tipo'], string> = { Caixa: 'Caixa da loja', Banco: 'Conta bancária', Carteira: 'Carteira digital' }

type Janela =
  | { tipo: 'nova' }
  | { tipo: 'editar'; conta: ContaFinanceira }
  | { tipo: 'transferir' }
  | { tipo: 'extrato'; conta: ContaFinanceira }
  | { tipo: 'mudarSituacao'; conta: ContaFinanceira }
  | { tipo: 'mudarSelecionadas'; ativa: boolean }
  | null

const FILTROS = { situacao: 'ativas', tipo: '', busca: '' }

/** Contas financeiras no padrão de grade: saldo atual (e o total das filtradas), extrato, transferência, edição e desativação. */
export function ContasFinanceirasPage() {
  const { enviar } = useApi()
  const { perfilAtual, consolidado } = useEmpresa()
  const permitido = PERFIS_DO_FINANCEIRO.includes(perfilAtual)
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { dados, erro } = useListaPaginada<ListaDeContas>(permitido ? `/api/financeiro/contas?${grade.consulta}` : null, versao, grade.filtros.busca ? 250 : 0)
  const ativas = useContas(versao)?.filter((c) => c.ativa) ?? []
  const [janela, setJanela] = useState<Janela>(null)

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Contas financeiras</h1>
        <p className={`mt-4 ${estilos.erro}`}>Só Dono, Gerente e Financeiro acessam o financeiro.</p>
      </div>
    )

  const recarregar = () => {
    setJanela(null)
    setVersao((v) => v + 1)
  }

  const colunas: Coluna<ContaFinanceira>[] = [
    {
      titulo: 'Conta',
      celula: (c) => (
        <>
          <span className="font-semibold">{c.nome}</span>
          <span className="block text-xs text-muted">{TIPOS[c.tipo]}</span>
        </>
      ),
    },
    {
      titulo: 'Saldo inicial',
      celula: (c) => (
        <>
          {moeda(c.saldoInicial)}
          <span className="block text-xs text-muted">{`em ${dataBr(c.saldoInicialEm)}`}</span>
        </>
      ),
      alinhar: 'direita',
    },
    { titulo: 'Saldo atual', celula: (c) => <span className={`font-semibold ${c.saldo < 0 ? 'text-danger' : ''}`}>{moeda(c.saldo)}</span>, alinhar: 'direita' },
    {
      titulo: 'Situação',
      celula: (c) => (
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${c.ativa ? 'bg-success-soft text-success' : 'bg-surface-2 text-muted'}`}>
          {c.ativa ? 'Ativa' : 'Inativa'}
        </span>
      ),
    },
  ]

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Financeiro"
        titulo="Contas financeiras"
        descricao="Onde o dinheiro está. Cada pagamento ou recebimento movimenta uma conta."
        acoes={
          <>
            <BotaoExportar relatorio="contas-financeiras" parametros={grade.parametros} selecionados={[...grade.selecionados]} />
            {!consolidado && (
              <>
                {ativas.length > 1 && (
                  <button type="button" onClick={() => setJanela({ tipo: 'transferir' })} className={estilos.botao}>Transferir entre contas</button>
                )}
                <button type="button" onClick={() => setJanela({ tipo: 'nova' })} className={estilos.botaoPrimario}>
                  <Icone nome="plus" />
                  Nova conta
                </button>
              </>
            )}
          </>
        }
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="min-w-56 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input value={grade.filtros.busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Nome da conta" className={estilos.campo} />
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">Situação</span>
          <select value={grade.filtros.situacao} onChange={(e) => grade.filtrar({ situacao: e.target.value })} className={estilos.campo}>
            <option value="ativas">Ativas</option>
            <option value="inativas">Inativas</option>
            <option value="todas">Todas</option>
          </select>
        </label>
        <label className="w-48 text-sm">
          <span className="mb-1 block font-semibold">Tipo</span>
          <select value={grade.filtros.tipo} onChange={(e) => grade.filtrar({ tipo: e.target.value })} className={estilos.campo}>
            <option value="">Todos</option>
            {Object.entries(TIPOS).map(([v, r]) => (
              <option key={v} value={v}>{r}</option>
            ))}
          </select>
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
          <Indicador rotulo="Saldo total" valor={moeda(dados.saldo)} destaque={dados.saldo < 0} />
        </dl>
      )}

      <GradeDeRegistros
        rotulo="Contas financeiras"
        colunas={colunas}
        itens={dados?.itens ?? (erro ? [] : null)}
        chave={(c) => c.id}
        nome={(c) => c.nome}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        apagada={(c) => !c.ativa}
        vazio="Nenhuma conta com esses filtros. Comece pelo caixa da loja e pela conta do banco."
        acoes={(c) => (
          <>
            <AcaoDaLinha rotulo={`Extrato de ${c.nome}`} icone="file" onClick={() => setJanela({ tipo: 'extrato', conta: c })} />
            {!consolidado && (
              <>
                <AcaoDaLinha rotulo={`Editar ${c.nome}`} icone="pencil" onClick={() => setJanela({ tipo: 'editar', conta: c })} />
                <AcaoDaLinha
                  rotulo={`${c.ativa ? 'Desativar' : 'Reativar'} ${c.nome}`}
                  icone="power"
                  perigo={c.ativa}
                  onClick={() => setJanela({ tipo: 'mudarSituacao', conta: c })}
                />
              </>
            )}
          </>
        )}
        acoesEmLote={
          !consolidado && (
            <>
              <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionadas', ativa: false })} className={estilos.botao}>Desativar selecionadas</button>
              <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionadas', ativa: true })} className={estilos.botao}>Reativar selecionadas</button>
            </>
          )
        }
      />

      {janela?.tipo === 'nova' && <FormularioDeConta conta={null} onFechar={() => setJanela(null)} onSalvo={recarregar} />}
      {janela?.tipo === 'editar' && <FormularioDeConta conta={janela.conta} onFechar={() => setJanela(null)} onSalvo={recarregar} />}
      {janela?.tipo === 'transferir' && <Transferencia contas={ativas} onFechar={() => setJanela(null)} onSalvo={recarregar} />}
      {janela?.tipo === 'extrato' && <ExtratoDaConta conta={janela.conta} onFechar={() => setJanela(null)} />}
      {janela?.tipo === 'mudarSituacao' && (
        <Confirmacao
          titulo={janela.conta.ativa ? 'Desativar conta' : 'Reativar conta'}
          rotulo={janela.conta.ativa ? 'Desativar' : 'Reativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', `/api/financeiro/contas/${janela.conta.id}/${janela.conta.ativa ? 'desativar' : 'reativar'}`)
            recarregar()
          }}
        >
          {janela.conta.ativa
            ? <p>{`"${janela.conta.nome}" deixa de aparecer nas baixas, vendas e transferências. Só dá para desativar com saldo zero; o extrato continua guardado.`}</p>
            : <p>{`"${janela.conta.nome}" volta a aparecer nas baixas, vendas e transferências.`}</p>}
        </Confirmacao>
      )}
      {janela?.tipo === 'mudarSelecionadas' && (
        <Confirmacao
          titulo={janela.ativa ? 'Reativar contas' : 'Desativar contas'}
          rotulo={janela.ativa ? 'Reativar' : 'Desativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', '/api/financeiro/contas/situacao', { ids: [...grade.selecionados], ativo: janela.ativa })
            grade.limparSelecao()
            recarregar()
          }}
        >
          <p>{`${janela.ativa ? 'Reativar' : 'Desativar'} ${grade.selecionados.size === 1 ? 'a conta selecionada' : `as ${grade.selecionados.size} contas selecionadas`}?`}</p>
          {!janela.ativa && <p className="mt-2 text-muted">Se alguma ainda tiver saldo, nenhuma é desativada: transfira o saldo antes.</p>}
        </Confirmacao>
      )}
    </div>
  )
}

function FormularioDeConta({ conta, onFechar, onSalvo }: { conta: ContaFinanceira | null; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [nome, setNome] = useState(conta?.nome ?? '')
  const [tipo, setTipo] = useState<ContaFinanceira['tipo']>(conta?.tipo ?? 'Caixa')
  const [saldo, setSaldo] = useState(conta ? String(conta.saldoInicial).replace('.', ',') : '')
  const [desde, setDesde] = useState(conta?.saldoInicialEm ?? hojeIso())
  const [erro, setErro] = useState<string | null>(null)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    const corpo = { nome, tipo, saldoInicial: lerNumero(saldo) ?? 0, saldoInicialEm: desde }
    try {
      await enviar(conta ? 'PUT' : 'POST', conta ? `/api/financeiro/contas/${conta.id}` : '/api/financeiro/contas', corpo)
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.')
    }
  }

  return (
    <Dialogo titulo={conta ? `Editar ${conta.nome}` : 'Nova conta'} onFechar={onFechar}>
      <form onSubmit={salvar} className="space-y-4">
        <Campo rotulo="Nome">
          <input value={nome} onChange={(e) => setNome(e.target.value)} maxLength={60} autoFocus placeholder="Ex.: Caixa da loja, Banco do Brasil" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Tipo">
          <select value={tipo} onChange={(e) => setTipo(e.target.value as ContaFinanceira['tipo'])} className={estilos.campo}>
            {Object.entries(TIPOS).map(([v, r]) => (
              <option key={v} value={v}>{r}</option>
            ))}
          </select>
        </Campo>
        <div className="grid grid-cols-2 gap-3">
          <Campo rotulo="Saldo inicial (R$)">
            <input value={saldo} onChange={(e) => setSaldo(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Em">
            <input type="date" value={desde} onChange={(e) => setDesde(e.target.value)} className={estilos.campo} />
          </Campo>
        </div>
        {conta && <p className="text-xs text-muted">Mudar o saldo inicial recalcula o saldo atual e o extrato desde aquela data.</p>}
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" className={estilos.botaoPrimario}>Salvar</button>
        </div>
      </form>
    </Dialogo>
  )
}

function Transferencia({ contas, onFechar, onSalvo }: { contas: ContaFinanceira[]; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [origemId, setOrigemId] = useState('')
  const [destinoId, setDestinoId] = useState('')
  const [valor, setValor] = useState('')
  const [data, setData] = useState(hojeIso())
  const [observacao, setObservacao] = useState('')
  const [erro, setErro] = useState<string | null>(null)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    try {
      await enviar('POST', '/api/financeiro/transferencias', { origemId, destinoId, data, valor: lerNumero(valor) ?? 0, observacao: observacao || null })
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível transferir.')
    }
  }

  return (
    <Dialogo titulo="Transferir entre contas" onFechar={onFechar}>
      <form onSubmit={salvar} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <Campo rotulo="De">
            <select value={origemId} onChange={(e) => setOrigemId(e.target.value)} className={estilos.campo}>
              <option value="">Escolha...</option>
              {contas.map((c) => (
                <option key={c.id} value={c.id}>{c.nome}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Para">
            <select value={destinoId} onChange={(e) => setDestinoId(e.target.value)} className={estilos.campo}>
              <option value="">Escolha...</option>
              {contas.filter((c) => c.id !== origemId).map((c) => (
                <option key={c.id} value={c.id}>{c.nome}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Valor (R$)">
            <input value={valor} onChange={(e) => setValor(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Data">
            <input type="date" value={data} onChange={(e) => setData(e.target.value)} className={estilos.campo} />
          </Campo>
        </div>
        <Campo rotulo="Observação (opcional)">
          <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={100} placeholder="Ex.: depósito do caixa" className={estilos.campo} />
        </Campo>
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" disabled={!origemId || !destinoId} className={estilos.botaoPrimario}>Transferir</button>
        </div>
      </form>
    </Dialogo>
  )
}

function ExtratoDaConta({ conta, onFechar }: { conta: ContaFinanceira; onFechar: () => void }) {
  const { getJson } = useApi()
  const [de, setDe] = useState(somarDias(hojeIso(), -30))
  const [ate, setAte] = useState(hojeIso())
  const [extrato, setExtrato] = useState<Extrato | null>(null)

  useEffect(() => {
    if (!de || !ate) return
    const controle = new AbortController()
    getJson<Extrato>(`/api/financeiro/contas/${conta.id}/extrato?de=${de}&ate=${ate}`, controle.signal)
      .then(setExtrato)
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, conta.id, de, ate])

  return (
    <Dialogo titulo={`Extrato — ${conta.nome}`} onFechar={onFechar} largo>
      <div className="space-y-4 text-sm">
        <div className="flex gap-3">
          <Campo rotulo="De">
            <input type="date" value={de} onChange={(e) => setDe(e.target.value)} className={estilos.campo} />
          </Campo>
          <Campo rotulo="Até">
            <input type="date" value={ate} onChange={(e) => setAte(e.target.value)} className={estilos.campo} />
          </Campo>
        </div>
        {extrato && (
          <div className="overflow-x-auto rounded-xl border border-border">
            <table aria-label="Extrato" className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-3 py-2">Data</th>
                  <th className="px-3 py-2">Descrição</th>
                  <th className="px-3 py-2 text-right">Valor</th>
                  <th className="px-3 py-2 text-right">Saldo</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                <tr className="text-muted">
                  <td className="px-3 py-2" colSpan={3}>Saldo anterior</td>
                  <td className="px-3 py-2 text-right tabular-nums">{moeda(extrato.saldoAnterior)}</td>
                </tr>
                {extrato.movimentos.map((m) => (
                  <tr key={m.id}>
                    <td className="px-3 py-2 whitespace-nowrap tabular-nums">{dataBr(m.data)}</td>
                    <td className="px-3 py-2">
                      {m.descricao}
                      {m.usuario && <span className="block text-xs text-muted">{m.usuario}</span>}
                    </td>
                    <td className={`px-3 py-2 text-right font-semibold tabular-nums ${m.valor < 0 ? 'text-danger' : 'text-success'}`}>{moeda(m.valor)}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{moeda(m.saldo)}</td>
                  </tr>
                ))}
                <tr className="bg-surface-2 font-semibold">
                  <td className="px-3 py-2" colSpan={3}>Saldo final</td>
                  <td className="px-3 py-2 text-right tabular-nums">{moeda(extrato.saldoFinal)}</td>
                </tr>
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Dialogo>
  )
}
