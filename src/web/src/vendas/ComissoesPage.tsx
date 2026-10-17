import { useEffect, useMemo, useState } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { hojeIso } from '../financeiro/tipos'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { inicioDoMes, PERFIS_QUE_CONSULTAM, type ComissaoDoVendedor } from './tipos'

/** Comissão por vendedor sobre as vendas concluídas no período (o fechamento com pagamento vem na v0.15.0). */
export function ComissoesPage() {
  const { getJson } = useApi()
  const { perfilAtual } = useEmpresa()
  const [de, setDe] = useState(() => inicioDoMes(hojeIso()))
  const [ate, setAte] = useState(hojeIso())
  const [{ linhas, erro }, setEstado] = useState<{ linhas: ComissaoDoVendedor[] | null; erro: string | null }>({ linhas: null, erro: null })
  const permitido = PERFIS_QUE_CONSULTAM.includes(perfilAtual)
  const filtros = useMemo(() => ({ de, ate }), [de, ate])

  useEffect(() => {
    if (!permitido || !de || !ate) return
    const controle = new AbortController()
    getJson<ComissaoDoVendedor[]>(`/api/vendas/comissoes?${new URLSearchParams(filtros)}`, controle.signal)
      .then((l) => setEstado({ linhas: l, erro: null }))
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setEstado({ linhas: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar as comissões.' })
      })
    return () => controle.abort()
  }, [getJson, filtros, permitido, de, ate])

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Comissões</h1>
        <p className={`mt-4 ${estilos.erro}`}>Só Dono, Gerente e Financeiro veem as comissões.</p>
      </div>
    )

  const soma = (f: (c: ComissaoDoVendedor) => number) => (linhas ?? []).reduce((s, c) => s + f(c), 0)

  return (
    <div className="mx-auto max-w-4xl">
      <p className="text-xs text-muted">Vendas</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Comissões</h1>
          <p className="mt-1 text-sm text-muted">Percentual do vendedor sobre o total de cada venda concluída (vendas canceladas não contam).</p>
        </div>
        <BotaoExportar relatorio="comissoes" parametros={filtros} />
      </div>

      <div className="mt-6 grid max-w-md grid-cols-2 gap-3">
        <Campo rotulo="De">
          <input type="date" value={de} onChange={(e) => setDe(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Até">
          <input type="date" value={ate} onChange={(e) => setAte(e.target.value)} className={estilos.campo} />
        </Campo>
      </div>

      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {linhas && (
        <div className="mt-6 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          {linhas.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted">Nenhuma venda no período.</p>
          ) : (
            <table aria-label="Comissões" className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-4 py-2">Vendedor</th>
                  <th className="px-4 py-2 text-right">Vendas</th>
                  <th className="px-4 py-2 text-right">Total vendido</th>
                  <th className="px-4 py-2 text-right">Comissão</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {linhas.map((c) => (
                  <tr key={c.vendedorId ?? 'sem'}>
                    <td className={`px-4 py-2 font-semibold ${c.vendedorId ? '' : 'text-muted'}`}>{c.vendedor}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{c.vendas}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{moeda(c.total)}</td>
                    <td className="px-4 py-2 text-right font-semibold tabular-nums">{moeda(c.comissao)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot className="bg-surface-2 font-semibold">
                <tr>
                  <td className="px-4 py-2">Total</td>
                  <td className="px-4 py-2 text-right tabular-nums">{soma((c) => c.vendas)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{moeda(soma((c) => c.total))}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{moeda(soma((c) => c.comissao))}</td>
                </tr>
              </tfoot>
            </table>
          )}
        </div>
      )}
    </div>
  )
}
