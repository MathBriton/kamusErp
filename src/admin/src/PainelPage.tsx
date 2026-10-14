import { useCallback, useEffect, useMemo, useState } from 'react'
import { chamar, ErroDeApi, type Acao, type Cliente, type Estado, type Resumo, type Sessao } from './api'
import { DetalheDoCliente } from './DetalheDoCliente'
import { botao, botaoPrimario, campo, dataHora, dia, ESTADOS, moeda } from './formatos'
import { Marca } from './Marca'
import { NovoCliente } from './NovoCliente'
import { RegistrarPagamento } from './RegistrarPagamento'
import { AlternarTema } from './tema'

type Janela = { tipo: 'pagamento'; cliente: Cliente } | { tipo: 'novo' } | { tipo: 'detalhe'; cliente: Cliente } | null

export function PainelPage({ sessao, onSair }: { sessao: Sessao; onSair: () => void }) {
  const [resumo, setResumo] = useState<Resumo | null>(null)
  const [clientes, setClientes] = useState<Cliente[] | null>(null)
  const [acoes, setAcoes] = useState<Acao[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)
  const [filtro, setFiltro] = useState<Estado | ''>('')
  const [busca, setBusca] = useState('')
  const [janela, setJanela] = useState<Janela>(null)
  const token = sessao.token

  const carregar = useCallback(async () => {
    try {
      const [r, c, a] = await Promise.all([
        chamar<Resumo>('/admin-api/resumo', { token }),
        chamar<Cliente[]>('/admin-api/clientes', { token }),
        chamar<Acao[]>('/admin-api/acoes', { token }),
      ])
      setResumo(r)
      setClientes(c)
      setAcoes(a)
      setErro(null)
    } catch (falha) {
      if (falha instanceof ErroDeApi && falha.status === 401) return
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível carregar o painel.')
    }
  }, [token])

  useEffect(() => {
    const timer = window.setTimeout(() => void carregar(), 0)
    return () => window.clearTimeout(timer)
  }, [carregar])

  const visiveis = useMemo(() => {
    const termo = busca.trim().toLowerCase()
    return (clientes ?? []).filter(
      (c) => (!filtro || c.estado === filtro) && (!termo || c.nome.toLowerCase().includes(termo) || (c.emailDoDono ?? '').includes(termo)),
    )
  }, [clientes, filtro, busca])

  const indicadores: [string, string, string][] = resumo
    ? [
        ['Ativas', String(resumo.ativas), 'bg-success'],
        ['Trial', String(resumo.trial), 'bg-info'],
        ['Em carência', String(resumo.emCarencia), 'bg-warning'],
        ['Suspensas', String(resumo.suspensas), 'bg-danger'],
        ['Receita mensal', moeda(resumo.receitaMensal), 'bg-primary'],
      ]
    : []

  return (
    <div className="min-h-dvh bg-background">
      <header className="flex h-16 items-center gap-3 border-b border-border bg-surface px-4 lg:px-8">
        <Marca />
        <span className="ml-auto hidden text-sm text-muted sm:inline">{sessao.email}</span>
        <span className="text-sm font-semibold">{sessao.nome}</span>
        <AlternarTema />
        <button type="button" onClick={onSair} className={botao}>
          Sair
        </button>
      </header>

      <main className="mx-auto max-w-7xl space-y-6 p-4 lg:p-8">
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex-1">
            <h1 className="text-2xl font-bold tracking-tight">Clientes e licenças</h1>
            <p className="text-sm text-muted">Registrar um pagamento libera o cliente na hora.</p>
          </div>
          <button type="button" onClick={() => setJanela({ tipo: 'novo' })} className={botaoPrimario}>
            Novo cliente
          </button>
        </div>

        {aviso && (
          <p role="status" className="flex items-center justify-between rounded-lg bg-success-soft px-4 py-3 text-sm text-success">
            {aviso}
            <button type="button" onClick={() => setAviso(null)} className="font-semibold">
              OK
            </button>
          </p>
        )}
        {erro && (
          <p role="alert" className="rounded-lg bg-danger-soft px-4 py-3 text-sm text-danger">
            {erro}
          </p>
        )}

        {resumo && (
          <section aria-label="Indicadores" className="grid grid-cols-2 gap-3 md:grid-cols-5">
            {indicadores.map(([rotulo, valor, cor]) => (
              <div key={rotulo} className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
                <p className="flex items-center gap-2 text-xs text-muted">
                  <span className={`size-2 rounded-full ${cor}`} />
                  {rotulo}
                </p>
                <p className="mt-1 text-2xl font-bold tabular-nums">{valor}</p>
              </div>
            ))}
          </section>
        )}

        <section className="rounded-2xl border border-border bg-surface shadow-sm">
          <div className="flex flex-wrap items-end gap-3 border-b border-border p-4">
            <label className="min-w-60 flex-1 text-sm">
              <span className="mb-1 block font-semibold">Buscar cliente</span>
              <input value={busca} onChange={(e) => setBusca(e.target.value)} placeholder="Nome ou e-mail do dono" className={campo} />
            </label>
            <label className="text-sm">
              <span className="mb-1 block font-semibold">Situação</span>
              <select value={filtro} onChange={(e) => setFiltro(e.target.value as Estado | '')} className={campo}>
                <option value="">Todas</option>
                {(Object.keys(ESTADOS) as Estado[]).map((e) => (
                  <option key={e} value={e}>
                    {ESTADOS[e].rotulo}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-4 py-2">Cliente</th>
                  <th className="px-4 py-2">Dono</th>
                  <th className="px-4 py-2">Situação</th>
                  <th className="px-4 py-2">Vencimento</th>
                  <th className="px-4 py-2">Mensalidade</th>
                  <th className="px-4 py-2">Último pagamento</th>
                  <th className="px-4 py-2 text-right">Ações</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {clientes && visiveis.length === 0 && (
                  <tr>
                    <td colSpan={7} className="p-8 text-center text-muted">
                      Nenhum cliente encontrado.
                    </td>
                  </tr>
                )}
                {visiveis.map((c) => (
                  <tr key={c.tenantId}>
                    <td className="px-4 py-2.5 font-semibold">{c.nome}</td>
                    <td className="px-4 py-2.5 text-muted">{c.emailDoDono ?? '—'}</td>
                    <td className="px-4 py-2.5">
                      <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${ESTADOS[c.estado].classe}`}>{ESTADOS[c.estado].rotulo}</span>
                    </td>
                    <td className="px-4 py-2.5 tabular-nums">{dia(c.vencimento)}</td>
                    <td className="px-4 py-2.5 tabular-nums">{moeda(c.valorMensal)}</td>
                    <td className="px-4 py-2.5 tabular-nums">{dia(c.ultimoPagamentoEm)}</td>
                    <td className="px-4 py-2.5">
                      <div className="flex justify-end gap-2">
                        <button type="button" onClick={() => setJanela({ tipo: 'detalhe', cliente: c })} aria-label={`Detalhes de ${c.nome}`} className={botao}>
                          Detalhes
                        </button>
                        {c.estado !== 'SemLicenca' && c.estado !== 'Cancelada' && (
                          <button type="button" onClick={() => setJanela({ tipo: 'pagamento', cliente: c })} className={botaoPrimario}>
                            Registrar pagamento
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
          <h2 className="mb-3 font-semibold">Ações recentes</h2>
          {acoes.length === 0 ? (
            <p className="text-sm text-muted">Nenhuma ação registrada.</p>
          ) : (
            <ul className="space-y-1.5 text-sm">
              {acoes.map((a) => (
                <li key={a.id}>
                  <span className="text-muted tabular-nums">{dataHora(a.ocorridoEm)}</span> · <span>{a.descricao}</span>{' '}
                  <span className="text-muted">({a.administradorEmail})</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </main>

      {janela?.tipo === 'pagamento' && (
        <RegistrarPagamento
          cliente={janela.cliente}
          token={token}
          onFechar={() => setJanela(null)}
          onRegistrado={(mensagem) => {
            setJanela(null)
            setAviso(mensagem)
            void carregar()
          }}
        />
      )}
      {janela?.tipo === 'novo' && <NovoCliente token={token} onFechar={() => setJanela(null)} onCriado={() => void carregar()} />}
      {janela?.tipo === 'detalhe' && (
        <DetalheDoCliente
          tenantId={janela.cliente.tenantId}
          nome={janela.cliente.nome}
          token={token}
          onFechar={() => setJanela(null)}
          onAlterado={() => void carregar()}
        />
      )}
    </div>
  )
}
