import { useEffect, useMemo, useState } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { dataBr, hojeIso, PERFIS_DO_FINANCEIRO, somarDias } from './tipos'
import { useContas } from './useFinanceiro'

interface Periodo {
  data: string
  entradasRealizadas: number
  saidasRealizadas: number
  entradasPrevistas: number
  saidasPrevistas: number
  saldo: number
}

interface Fluxo {
  agrupamento: 'dia' | 'mes'
  saldoInicial: number
  periodos: Periodo[]
  aReceberEmAtraso: number
  aPagarEmAtraso: number
  saldoFinal: number
}

const MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']

/** Fluxo de caixa: realizado até hoje e previsto (parcelas em aberto) daqui em diante, com saldo acumulado. */
export function FluxoDeCaixaPage() {
  const { getJson } = useApi()
  const { perfilAtual } = useEmpresa()
  const contas = useContas()
  const [de, setDe] = useState(hojeIso())
  const [ate, setAte] = useState(somarDias(hojeIso(), 30))
  const [contaId, setContaId] = useState('')
  const [{ fluxo, erro }, setEstado] = useState<{ fluxo: Fluxo | null; erro: string | null }>({ fluxo: null, erro: null })
  const permitido = PERFIS_DO_FINANCEIRO.includes(perfilAtual)

  const filtros = useMemo(() => {
    const f: Record<string, string> = { de, ate }
    if (contaId) f.contaId = contaId
    return f
  }, [de, ate, contaId])

  useEffect(() => {
    if (!permitido || !de || !ate) return
    const controle = new AbortController()
    getJson<Fluxo>(`/api/financeiro/fluxo?${new URLSearchParams(filtros)}`, controle.signal)
      .then((f) => setEstado({ fluxo: f, erro: null }))
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setEstado({ fluxo: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar.' })
      })
    return () => controle.abort()
  }, [getJson, filtros, permitido, de, ate])

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Fluxo de caixa</h1>
        <p className={`mt-4 ${estilos.erro}`}>Só Dono, Gerente e Financeiro acessam o financeiro.</p>
      </div>
    )

  const rotulo = (p: Periodo) => (fluxo?.agrupamento === 'mes' ? `${MESES[Number(p.data.slice(5, 7)) - 1]}/${p.data.slice(0, 4)}` : dataBr(p.data))
  const comMovimento = fluxo?.periodos.filter((p) => p.entradasRealizadas || p.saidasRealizadas || p.entradasPrevistas || p.saidasPrevistas) ?? []

  return (
    <div className="mx-auto max-w-6xl">
      <p className="text-xs text-muted">Financeiro</p>
      <h1 className="text-2xl font-bold tracking-tight">Fluxo de caixa</h1>
      <p className="mt-1 text-sm text-muted">Até hoje, o que de fato entrou e saiu das contas; de hoje em diante, as contas a pagar e a receber em aberto.</p>

      <div className="mt-6 flex flex-wrap items-end gap-3">
        <label className="text-sm">
          <span className="mb-1 block font-semibold">De</span>
          <input type="date" value={de} onChange={(e) => setDe(e.target.value)} className={estilos.campo} />
        </label>
        <label className="text-sm">
          <span className="mb-1 block font-semibold">Até</span>
          <input type="date" value={ate} onChange={(e) => setAte(e.target.value)} className={estilos.campo} />
        </label>
        <label className="text-sm">
          <span className="mb-1 block font-semibold">Conta</span>
          <select value={contaId} onChange={(e) => setContaId(e.target.value)} className={estilos.campo}>
            <option value="">Todas as contas</option>
            {contas?.map((c) => (
              <option key={c.id} value={c.id}>{c.nome}</option>
            ))}
          </select>
        </label>
        <span className="flex-1" />
        <BotaoExportar relatorio="fluxo-de-caixa" parametros={filtros} />
      </div>

      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {fluxo && (
        <>
          <dl className="mt-4 grid gap-3 sm:grid-cols-4">
            <Indicador rotulo="Saldo no início" valor={moeda(fluxo.saldoInicial)} />
            <Indicador rotulo="Saldo projetado no fim" valor={moeda(fluxo.saldoFinal)} destaque={fluxo.saldoFinal < 0} />
            <Indicador rotulo="A receber em atraso" valor={moeda(fluxo.aReceberEmAtraso)} />
            <Indicador rotulo="A pagar em atraso" valor={moeda(fluxo.aPagarEmAtraso)} destaque={fluxo.aPagarEmAtraso > 0} />
          </dl>
          <p className="mt-2 text-xs text-muted">Valores em atraso ficam fora da projeção até serem pagos ou renegociados.</p>
          <div className="mt-4 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
            {comMovimento.length === 0 ? (
              <p className="p-8 text-center text-sm text-muted">Nenhum movimento nem conta prevista no período.</p>
            ) : (
              <table aria-label="Fluxo de caixa" className="w-full text-sm">
                <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                  <tr>
                    <th className="px-4 py-2">{fluxo.agrupamento === 'mes' ? 'Mês' : 'Data'}</th>
                    <th className="px-4 py-2 text-right">Entradas</th>
                    <th className="px-4 py-2 text-right">Saídas</th>
                    <th className="px-4 py-2 text-right">Saldo</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {comMovimento.map((p) => {
                    const previsto = p.entradasPrevistas > 0 || p.saidasPrevistas > 0
                    return (
                      <tr key={p.data}>
                        <td className="px-4 py-2.5 whitespace-nowrap tabular-nums">
                          {rotulo(p)}
                          {previsto && <span className="ml-2 text-xs text-muted">previsto</span>}
                        </td>
                        <td className="px-4 py-2.5 text-right text-success tabular-nums">{moeda(p.entradasRealizadas + p.entradasPrevistas)}</td>
                        <td className="px-4 py-2.5 text-right text-danger tabular-nums">{moeda(p.saidasRealizadas + p.saidasPrevistas)}</td>
                        <td className={`px-4 py-2.5 text-right font-semibold tabular-nums ${p.saldo < 0 ? 'text-danger' : ''}`}>{moeda(p.saldo)}</td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
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
