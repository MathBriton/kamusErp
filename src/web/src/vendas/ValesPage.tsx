import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { dataBr } from '../financeiro/tipos'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { PERFIS_DE_GESTAO, PERFIS_QUE_CONSULTAM, PERFIS_QUE_VENDEM, type ListaDeVales, type ValeTroca } from './tipos'

const TOM: Record<ValeTroca['situacao'], string> = {
  Ativo: 'bg-success-soft text-success',
  Usado: 'bg-surface-2 text-muted',
  Vencido: 'bg-warning-soft text-warning',
  Cancelado: 'bg-danger-soft text-danger',
}

/** Vales-troca emitidos nas trocas: saldo, validade e situação; Dono e Gerente cancelam. */
export function ValesPage() {
  const { getJson } = useApi()
  const { perfilAtual } = useEmpresa()
  const [situacao, setSituacao] = useState('ativos')
  const [busca, setBusca] = useState('')
  const [versao, setVersao] = useState(0)
  const [lista, setLista] = useState<ListaDeVales | null>(null)
  const [cancelando, setCancelando] = useState<ValeTroca | null>(null)
  const permitido = PERFIS_QUE_VENDEM.includes(perfilAtual) || PERFIS_QUE_CONSULTAM.includes(perfilAtual)
  const filtros = useMemo(() => {
    const f: Record<string, string> = { situacao }
    if (busca.trim()) f.busca = busca.trim()
    return f
  }, [situacao, busca])

  useEffect(() => {
    if (!permitido) return
    const controle = new AbortController()
    getJson<ListaDeVales>(`/api/vendas/vales?${new URLSearchParams(filtros)}`, controle.signal)
      .then(setLista)
      .catch(() => {
        if (!controle.signal.aborted) setLista({ itens: [], saldoAtivo: 0, saldoVencido: 0 })
      })
    return () => controle.abort()
  }, [getJson, filtros, versao, permitido])

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Vales-troca</h1>
        <p className={`mt-4 ${estilos.erro}`}>Seu perfil não acessa os vales-troca.</p>
      </div>
    )

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Vendas</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Vales-troca</h1>
          <p className="mt-1 text-sm text-muted">Crédito do cliente gerado nas trocas. É aceito como forma de pagamento, em uma ou mais compras, até a validade.</p>
        </div>
        <BotaoExportar relatorio="vales-troca" parametros={filtros} />
      </div>

      <div className="mt-6 grid gap-3 sm:grid-cols-4">
        <Campo rotulo="Situação">
          <select value={situacao} onChange={(e) => setSituacao(e.target.value)} className={estilos.campo}>
            <option value="ativos">Ativos</option>
            <option value="usados">Usados</option>
            <option value="vencidos">Vencidos</option>
            <option value="cancelados">Cancelados</option>
            <option value="todos">Todos</option>
          </select>
        </Campo>
        <Campo rotulo="Busca" className="sm:col-span-1">
          <input value={busca} onChange={(e) => setBusca(e.target.value)} placeholder="Código ou cliente" className={estilos.campo} />
        </Campo>
        {lista && (
          <dl className="contents">
            <div className="rounded-2xl border border-border bg-surface p-3 shadow-sm">
              <dt className="text-xs text-muted">Saldo em vales ativos</dt>
              <dd className="text-lg font-bold tabular-nums">{moeda(lista.saldoAtivo)}</dd>
            </div>
            <div className="rounded-2xl border border-border bg-surface p-3 shadow-sm">
              <dt className="text-xs text-muted">Saldo vencido (não usado)</dt>
              <dd className="text-lg font-bold tabular-nums">{moeda(lista.saldoVencido)}</dd>
            </div>
          </dl>
        )}
      </div>

      {lista && (
        <div className="mt-4 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          {lista.itens.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted">Nenhum vale nesta situação.</p>
          ) : (
            <table aria-label="Vales-troca" className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-4 py-2">Código</th>
                  <th className="px-4 py-2">Cliente</th>
                  <th className="px-4 py-2">Emitido</th>
                  <th className="px-4 py-2">Válido até</th>
                  <th className="px-4 py-2 text-right">Valor</th>
                  <th className="px-4 py-2 text-right">Saldo</th>
                  <th className="px-4 py-2">Situação</th>
                  <th />
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {lista.itens.map((v) => (
                  <tr key={v.id}>
                    <td className="px-4 py-2 font-mono font-semibold">{v.codigo}</td>
                    <td className="px-4 py-2">{v.clienteNome ?? '—'}</td>
                    <td className="px-4 py-2">{dataBr(v.emitidoEm)}</td>
                    <td className="px-4 py-2">{dataBr(v.validoAte)}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{moeda(v.valor)}</td>
                    <td className="px-4 py-2 text-right font-semibold tabular-nums">{moeda(v.saldo)}</td>
                    <td className="px-4 py-2">
                      <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${TOM[v.situacao]}`}>{v.situacao}</span>
                    </td>
                    <td className="px-4 py-2 text-right">
                      {PERFIS_DE_GESTAO.includes(perfilAtual) && v.situacao !== 'Cancelado' && v.situacao !== 'Usado' && (
                        <button type="button" onClick={() => setCancelando(v)} className={`${estilos.botao} text-danger`} aria-label={`Cancelar ${v.codigo}`}>
                          Cancelar
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {cancelando && (
        <CancelarVale
          vale={cancelando}
          onFechar={() => setCancelando(null)}
          onCancelado={() => {
            setCancelando(null)
            setVersao((x) => x + 1)
          }}
        />
      )}
    </div>
  )
}

function CancelarVale({ vale, onFechar, onCancelado }: { vale: ValeTroca; onFechar: () => void; onCancelado: () => void }) {
  const { enviar } = useApi()
  const [motivo, setMotivo] = useState('')
  const [erro, setErro] = useState<string | null>(null)

  async function cancelar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    try {
      await enviar('POST', `/api/vendas/vales/${vale.id}/cancelar`, { motivo })
      onCancelado()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível cancelar.')
    }
  }

  return (
    <Dialogo titulo={`Cancelar vale ${vale.codigo}`} onFechar={onFechar}>
      <form onSubmit={cancelar} className="space-y-4 text-sm">
        <p className="text-muted">{`O saldo de ${moeda(vale.saldo)} deixa de valer como crédito do cliente.`}</p>
        <Campo rotulo="Motivo">
          <input value={motivo} onChange={(e) => setMotivo(e.target.value)} maxLength={300} autoFocus className={estilos.campo} />
        </Campo>
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Voltar</button>
          <button type="submit" disabled={!motivo.trim()} className={estilos.botaoPrimario}>Cancelar vale</button>
        </div>
      </form>
    </Dialogo>
  )
}
