import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { dataBr, hojeIso } from '../financeiro/tipos'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { inicioDoMes, PERFIS_DE_GESTAO, porcentagem, PERFIS_QUE_CONSULTAM, PERFIS_QUE_VENDEM, type ListaDeVendas, type Venda } from './tipos'
import { useVendedores } from './useVendas'

function SeloDaVenda({ status }: { status: Venda['status'] }) {
  return status === 'Cancelada' ? (
    <span className="rounded-full bg-danger-soft px-2 py-0.5 text-xs font-semibold text-danger">Cancelada</span>
  ) : (
    <span className="rounded-full bg-success-soft px-2 py-0.5 text-xs font-semibold text-success">Concluída</span>
  )
}

/** Vendas do período com totais (o Vendedor vê só as suas), detalhe e cancelamento. */
export function VendasPage() {
  const { getJson } = useApi()
  const { perfilAtual } = useEmpresa()
  const veTudo = PERFIS_QUE_CONSULTAM.includes(perfilAtual)
  const vendedores = useVendedores()
  const [de, setDe] = useState(() => inicioDoMes(hojeIso()))
  const [ate, setAte] = useState(hojeIso())
  const [status, setStatus] = useState('todas')
  const [vendedorId, setVendedorId] = useState('')
  const [busca, setBusca] = useState('')
  const [versao, setVersao] = useState(0)
  const [{ lista, erro }, setEstado] = useState<{ lista: ListaDeVendas | null; erro: string | null }>({ lista: null, erro: null })
  const [detalhe, setDetalhe] = useState<string | null>(null)
  const permitido = PERFIS_QUE_VENDEM.includes(perfilAtual) || veTudo

  const filtros = useMemo(() => {
    const f: Record<string, string> = {}
    if (de) f.de = de
    if (ate) f.ate = ate
    f.status = status
    if (vendedorId) f.vendedorId = vendedorId
    if (busca.trim()) f.busca = busca.trim()
    return f
  }, [de, ate, status, vendedorId, busca])

  useEffect(() => {
    if (!permitido) return
    const controle = new AbortController()
    getJson<ListaDeVendas>(`/api/vendas?${new URLSearchParams(filtros)}`, controle.signal)
      .then((l) => setEstado({ lista: l, erro: null }))
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setEstado({ lista: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar as vendas.' })
      })
    return () => controle.abort()
  }, [getJson, filtros, versao, permitido])

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Vendas realizadas</h1>
        <p className={`mt-4 ${estilos.erro}`}>Seu perfil não acessa as vendas.</p>
      </div>
    )

  return (
    <div className="mx-auto max-w-6xl">
      <p className="text-xs text-muted">Vendas</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Vendas realizadas</h1>
          <p className="mt-1 text-sm text-muted">{veTudo ? 'Todas as vendas da empresa no período.' : 'As vendas que você registrou.'}</p>
        </div>
        <BotaoExportar relatorio="vendas" parametros={filtros} />
      </div>

      <div className="mt-6 grid gap-3 rounded-2xl border border-border bg-surface p-4 shadow-sm sm:grid-cols-5">
        <Campo rotulo="De">
          <input type="date" value={de} onChange={(e) => setDe(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Até">
          <input type="date" value={ate} onChange={(e) => setAte(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Situação">
          <select value={status} onChange={(e) => setStatus(e.target.value)} className={estilos.campo}>
            <option value="todas">Todas</option>
            <option value="concluidas">Concluídas</option>
            <option value="canceladas">Canceladas</option>
          </select>
        </Campo>
        {veTudo ? (
          <Campo rotulo="Vendedor">
            <select value={vendedorId} onChange={(e) => setVendedorId(e.target.value)} className={estilos.campo}>
              <option value="">Todos</option>
              {vendedores?.map((v) => (
                <option key={v.id} value={v.id}>{v.nome}</option>
              ))}
            </select>
          </Campo>
        ) : (
          <div />
        )}
        <Campo rotulo="Busca">
          <input value={busca} onChange={(e) => setBusca(e.target.value)} placeholder="Nº, cliente ou nota" className={estilos.campo} />
        </Campo>
      </div>

      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {lista && (
        <>
          <dl className="mt-4 grid gap-3 sm:grid-cols-4">
            {[
              ['Vendas', String(lista.quantidade)],
              ['Peças', String(lista.pecas)],
              ['Total vendido', moeda(lista.total)],
              ['Ticket médio', moeda(lista.ticketMedio)],
            ].map(([rotulo, valor]) => (
              <div key={rotulo} className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
                <dt className="text-xs text-muted">{rotulo}</dt>
                <dd className="text-xl font-bold tabular-nums">{valor}</dd>
              </div>
            ))}
          </dl>
          <div className="mt-4 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
            {lista.itens.length === 0 ? (
              <p className="p-8 text-center text-sm text-muted">Nenhuma venda no período.</p>
            ) : (
              <table aria-label="Vendas" className="w-full text-sm">
                <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                  <tr>
                    <th className="px-4 py-2">Nº</th>
                    <th className="px-4 py-2">Data</th>
                    <th className="px-4 py-2">Cliente</th>
                    <th className="px-4 py-2">Vendedor</th>
                    <th className="px-4 py-2 text-right">Peças</th>
                    <th className="px-4 py-2">Pagamento</th>
                    <th className="px-4 py-2 text-right">Total</th>
                    <th className="px-4 py-2">Situação</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {lista.itens.map((v) => (
                    <tr key={v.id} className={v.status === 'Cancelada' ? 'text-muted' : ''}>
                      <td className="px-4 py-2">
                        <button type="button" onClick={() => setDetalhe(v.id)} className="font-semibold text-primary hover:underline">{String(v.numero)}</button>
                      </td>
                      <td className="px-4 py-2 whitespace-nowrap">{dataBr(v.data)}</td>
                      <td className="px-4 py-2">{v.clienteNome ?? '—'}</td>
                      <td className="px-4 py-2">{v.vendedorNome ?? '—'}</td>
                      <td className="px-4 py-2 text-right tabular-nums">{v.pecas}</td>
                      <td className="px-4 py-2">{v.formas}</td>
                      <td className={`px-4 py-2 text-right font-semibold tabular-nums ${v.status === 'Cancelada' ? 'line-through' : ''}`}>{moeda(v.total)}</td>
                      <td className="px-4 py-2"><SeloDaVenda status={v.status} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}

      {detalhe && <DetalheDaVenda id={detalhe} onFechar={() => setDetalhe(null)} onMudou={() => setVersao((x) => x + 1)} />}
    </div>
  )
}

function DetalheDaVenda({ id, onFechar, onMudou }: { id: string; onFechar: () => void; onMudou: () => void }) {
  const { getJson, enviar } = useApi()
  const { perfilAtual } = useEmpresa()
  const [venda, setVenda] = useState<Venda | null>(null)
  const [cancelando, setCancelando] = useState(false)
  const [motivo, setMotivo] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    const controle = new AbortController()
    getJson<Venda>(`/api/vendas/${id}`, controle.signal)
      .then(setVenda)
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setErro(e instanceof ErroDeApi ? e.message : 'Não foi possível abrir a venda.')
      })
    return () => controle.abort()
  }, [getJson, id])

  async function cancelar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      setVenda(await enviar<Venda>('POST', `/api/vendas/${id}/cancelar`, { motivo }))
      setCancelando(false)
      onMudou()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível cancelar.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={venda ? `Venda nº ${venda.numero}` : 'Venda'} onFechar={onFechar} largo>
      {erro && <p role="alert" className={`mb-3 ${estilos.erro}`}>{erro}</p>}
      {venda && (
        <div className="space-y-4 text-sm">
          <div className="flex flex-wrap items-center gap-2">
            <SeloDaVenda status={venda.status} />
            <span className="text-muted">{`${dataBr(venda.data)} · ${venda.vendedorNome ?? 'sem vendedor'}${venda.clienteNome ? ` · ${venda.clienteNome}` : ''}`}</span>
            {venda.notaExterna && <span className="text-muted">{`· ${venda.notaExterna}`}</span>}
          </div>
          {venda.status === 'Cancelada' && (
            <p className={estilos.erro}>{`Cancelada por ${venda.canceladaPor ?? 'Sistema'}: ${venda.motivoDoCancelamento ?? ''}`}</p>
          )}
          <table className="w-full">
            <thead className="text-left text-xs text-muted uppercase">
              <tr>
                <th className="py-1">Item</th>
                <th className="py-1 text-right">Qtd.</th>
                <th className="py-1 text-right">Preço</th>
                <th className="py-1 text-right">Desconto</th>
                <th className="py-1 text-right">Total</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {venda.itens.map((i) => (
                <tr key={i.skuId}>
                  <td className="py-1">{i.descricao}</td>
                  <td className="py-1 text-right tabular-nums">{i.quantidade}</td>
                  <td className="py-1 text-right tabular-nums">{moeda(i.precoUnitario)}</td>
                  <td className="py-1 text-right tabular-nums">{i.desconto ? moeda(i.desconto) : '—'}</td>
                  <td className="py-1 text-right tabular-nums">{moeda(i.total)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-1 rounded-xl bg-surface-2 p-4">
            <dt className="text-muted">Subtotal</dt><dd className="text-right tabular-nums">{moeda(venda.subtotal)}</dd>
            {venda.descontoGeral > 0 && (<><dt className="text-muted">Desconto na venda</dt><dd className="text-right tabular-nums">{`− ${moeda(venda.descontoGeral)}`}</dd></>)}
            <dt className="font-bold">Total</dt><dd className="text-right font-bold tabular-nums">{moeda(venda.total)}</dd>
            {venda.troco > 0 && (<><dt className="text-muted">Troco</dt><dd className="text-right tabular-nums">{moeda(venda.troco)}</dd></>)}
            <dt className="text-muted">{`Comissão (${porcentagem(venda.comissaoPercentual)})`}</dt><dd className="text-right tabular-nums">{moeda(venda.comissao)}</dd>
            {venda.taxaTotal > 0 && (<><dt className="text-muted">Taxas de cartão</dt><dd className="text-right tabular-nums">{moeda(venda.taxaTotal)}</dd></>)}
            {venda.lucroBruto !== null && (<><dt className="text-muted">Custo das peças</dt><dd className="text-right tabular-nums">{moeda(venda.custoTotal ?? 0)}</dd></>)}
            {venda.lucroBruto !== null && (<><dt className="text-muted">Lucro bruto</dt><dd className="text-right tabular-nums">{moeda(venda.lucroBruto)}</dd></>)}
          </dl>
          <div>
            <h3 className="mb-1 font-semibold">Pagamento</h3>
            <ul className="space-y-0.5">
              {venda.pagamentos.map((p, n) => (
                <li key={n} className="flex justify-between">
                  <span>{`${p.formaNome}${p.parcelas > 1 ? ` em ${p.parcelas}x` : ''}${p.taxaPercentual ? ` (taxa ${porcentagem(p.taxaPercentual)})` : ''}`}</span>
                  <span className="tabular-nums">{moeda(p.valor)}</span>
                </li>
              ))}
            </ul>
          </div>
          <p className="text-xs text-muted">{`Registrada por ${venda.registradaPor ?? 'Sistema'} em ${new Date(venda.registradaEm).toLocaleString('pt-BR')}`}</p>

          {venda.status === 'Concluida' && PERFIS_DE_GESTAO.includes(perfilAtual) && !cancelando && (
            <div className="flex justify-end">
              <button type="button" onClick={() => setCancelando(true)} className={`${estilos.botao} text-danger`}>Cancelar venda</button>
            </div>
          )}
          {cancelando && (
            <form onSubmit={cancelar} className="space-y-3 rounded-xl border border-danger p-4">
              <p className="text-muted">As peças voltam ao estoque e o que entrou no caixa ou ficou a receber é estornado.</p>
              <Campo rotulo="Motivo do cancelamento">
                <input value={motivo} onChange={(e) => setMotivo(e.target.value)} maxLength={300} autoFocus className={estilos.campo} />
              </Campo>
              <div className="flex justify-end gap-2">
                <button type="button" onClick={() => setCancelando(false)} className={estilos.botao}>Voltar</button>
                <button type="submit" disabled={!motivo.trim() || enviando} className={estilos.botaoPrimario}>Confirmar cancelamento</button>
              </div>
            </form>
          )}
        </div>
      )}
    </Dialogo>
  )
}
