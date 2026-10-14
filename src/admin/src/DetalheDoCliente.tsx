import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { chamar, ErroDeApi, type Detalhe } from './api'
import { Campo } from './Campo'
import { Dialogo } from './Dialogo'
import { botao, botaoPrimario, campo, dataHora, dia, ESTADOS, moeda } from './formatos'

const numeroOuNulo = (valor: string) => (valor.trim() === '' ? null : Number(valor))

/** Painel lateral: situação, ajuste de valor/carência/limites, pagamentos, ações e cancelar/reativar. */
export function DetalheDoCliente({ tenantId, nome, token, onFechar, onAlterado }: {
  tenantId: string
  nome: string
  token: string
  onFechar: () => void
  onAlterado: () => void
}) {
  const [detalhe, setDetalhe] = useState<Detalhe | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [confirmando, setConfirmando] = useState(false)
  const [form, setForm] = useState({ valorMensal: '', diasDeCarencia: '', limiteDeCnpjs: '', limiteDeLojas: '', limiteDeUsuarios: '' })

  const carregar = useCallback(async () => {
    try {
      const d = await chamar<Detalhe>(`/admin-api/clientes/${tenantId}`, { token })
      setDetalhe(d)
      const texto = (n: number | null) => (n === null ? '' : String(n))
      setForm({
        valorMensal: String(d.cliente.valorMensal), diasDeCarencia: String(d.cliente.diasDeCarencia),
        limiteDeCnpjs: texto(d.cliente.limiteDeCnpjs), limiteDeLojas: texto(d.cliente.limiteDeLojas), limiteDeUsuarios: texto(d.cliente.limiteDeUsuarios),
      })
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível carregar o cliente.')
    }
  }, [tenantId, token])

  useEffect(() => {
    const timer = window.setTimeout(() => void carregar(), 0)
    return () => window.clearTimeout(timer)
  }, [carregar])

  async function executar(acao: () => Promise<unknown>) {
    setErro(null)
    try {
      await acao()
      setConfirmando(false)
      onAlterado()
      await carregar()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível concluir.')
    }
  }

  const salvar = (e: FormEvent) => {
    e.preventDefault()
    void executar(() =>
      chamar(`/admin-api/clientes/${tenantId}/licenca`, {
        metodo: 'PUT',
        token,
        corpo: {
          valorMensal: Number(form.valorMensal), diasDeCarencia: Number(form.diasDeCarencia),
          limiteDeCnpjs: numeroOuNulo(form.limiteDeCnpjs), limiteDeLojas: numeroOuNulo(form.limiteDeLojas), limiteDeUsuarios: numeroOuNulo(form.limiteDeUsuarios),
        },
      }),
    )
  }
  const mudar = (acao: 'cancelar' | 'reativar') => void executar(() => chamar(`/admin-api/clientes/${tenantId}/${acao}`, { metodo: 'POST', token }))
  const alterar = (c: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [c]: e.target.value })

  const cliente = detalhe?.cliente
  return (
    <Dialogo titulo={nome} onFechar={onFechar} lateral>
      {erro && (
        <p role="alert" className="mb-4 rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
          {erro}
        </p>
      )}
      {!cliente ? (
        !erro && <p className="text-sm text-muted">Carregando...</p>
      ) : (
        <div className="space-y-6">
          <div className="flex flex-wrap items-center gap-3 text-sm">
            <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${ESTADOS[cliente.estado].classe}`}>{ESTADOS[cliente.estado].rotulo}</span>
            <span>
              Vence em <b>{dia(cliente.vencimento)}</b> · carência até <b>{dia(cliente.fimDaCarencia)}</b>
            </span>
            <span className="text-muted">
              Dono {cliente.emailDoDono ?? '—'} · {cliente.usuarios} usuário(s)
            </span>
          </div>

          {cliente.estado !== 'SemLicenca' && (
            <form onSubmit={salvar} className="rounded-xl border border-border p-4">
              <h3 className="mb-3 font-semibold">Licença</h3>
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
                <Campo rotulo="Mensalidade">
                  <input type="number" min="0" step="0.01" value={form.valorMensal} onChange={alterar('valorMensal')} className={campo} />
                </Campo>
                <Campo rotulo="Carência (dias)">
                  <input type="number" min="0" max="60" value={form.diasDeCarencia} onChange={alterar('diasDeCarencia')} className={campo} />
                </Campo>
                <Campo rotulo="Máx. CNPJs">
                  <input type="number" min="1" placeholder="—" value={form.limiteDeCnpjs} onChange={alterar('limiteDeCnpjs')} className={campo} />
                </Campo>
                <Campo rotulo="Máx. lojas">
                  <input type="number" min="1" placeholder="—" value={form.limiteDeLojas} onChange={alterar('limiteDeLojas')} className={campo} />
                </Campo>
                <Campo rotulo="Máx. usuários">
                  <input type="number" min="1" placeholder="—" value={form.limiteDeUsuarios} onChange={alterar('limiteDeUsuarios')} className={campo} />
                </Campo>
              </div>
              <p className="mt-2 text-xs text-muted">Limites vazios = sem limite. Ainda não são aplicados no sistema do cliente (só informativos).</p>
              <div className="mt-3 flex flex-wrap justify-between gap-2">
                {cliente.estado === 'Cancelada' ? (
                  <button type="button" onClick={() => mudar('reativar')} className={botao}>
                    Reativar licença
                  </button>
                ) : confirmando ? (
                  <span className="flex flex-wrap items-center gap-2 text-sm">
                    O cliente perde o acesso na hora.
                    <button type="button" onClick={() => mudar('cancelar')} className={`${botao} border-danger text-danger`}>
                      Confirmar cancelamento
                    </button>
                    <button type="button" onClick={() => setConfirmando(false)} className="text-muted">
                      Voltar
                    </button>
                  </span>
                ) : (
                  <button type="button" onClick={() => setConfirmando(true)} className={`${botao} text-danger`}>
                    Cancelar licença
                  </button>
                )}
                <button type="submit" className={botaoPrimario}>
                  Salvar
                </button>
              </div>
            </form>
          )}

          <div>
            <h3 className="mb-2 font-semibold">Pagamentos</h3>
            {detalhe.pagamentos.length === 0 ? (
              <p className="text-sm text-muted">Nenhum pagamento registrado.</p>
            ) : (
              <table aria-label="Pagamentos" className="w-full text-sm">
                <thead className="text-left text-xs text-muted uppercase">
                  <tr>
                    <th className="py-1">Pago em</th>
                    <th className="py-1">Valor</th>
                    <th className="py-1">Meses</th>
                    <th className="py-1">Forma</th>
                    <th className="py-1">Cobre até</th>
                    <th className="py-1">Por</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {detalhe.pagamentos.map((p) => (
                    <tr key={p.id} title={p.observacao ?? undefined}>
                      <td className="py-1.5 tabular-nums">{dia(p.pagoEm)}</td>
                      <td className="py-1.5 tabular-nums">{moeda(p.valor)}</td>
                      <td className="py-1.5">{p.meses}</td>
                      <td className="py-1.5">{p.forma ?? '—'}</td>
                      <td className="py-1.5 tabular-nums">{dia(p.estendeuAte)}</td>
                      <td className="py-1.5 text-muted">{p.registradoPor}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          <div>
            <h3 className="mb-2 font-semibold">Ações administrativas</h3>
            {detalhe.acoes.length === 0 ? (
              <p className="text-sm text-muted">Nenhuma ação registrada.</p>
            ) : (
              <ul className="space-y-1.5 text-sm">
                {detalhe.acoes.map((a) => (
                  <li key={a.id}>
                    <span className="text-muted tabular-nums">{dataHora(a.ocorridoEm)}</span> · <span>{a.descricao}</span>{' '}
                    <span className="text-muted">({a.administradorEmail})</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </Dialogo>
  )
}
