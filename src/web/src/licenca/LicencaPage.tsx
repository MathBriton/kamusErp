import { useEffect, useState } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { Icone } from '../components/Icone'
import { formatarDia, formatarMoeda, useLicenca, type EstadoLicenca } from './LicencaContext'

interface Pagamento {
  id: string
  valor: number
  meses: number
  pagoEm: string
  estendeuAte: string
  forma: string | null
}

const ESTADOS: Record<EstadoLicenca, { rotulo: string; classe: string }> = {
  Trial: { rotulo: 'Avaliação', classe: 'bg-info-soft text-info' },
  Ativa: { rotulo: 'Em dia', classe: 'bg-success-soft text-success' },
  Carencia: { rotulo: 'Em carência', classe: 'bg-warning-soft text-warning' },
  Suspensa: { rotulo: 'Suspensa', classe: 'bg-danger-soft text-danger' },
  Cancelada: { rotulo: 'Cancelada', classe: 'bg-surface-2 text-muted' },
}

const limite = (valor: number | null) => (valor === null ? 'Sem limite' : String(valor))

export function LicencaPage() {
  const { situacao } = useLicenca()
  const { getJson } = useApi()
  const [{ pagamentos, erro }, setPagamentos] = useState<{ pagamentos: Pagamento[] | null; erro: string | null }>({ pagamentos: null, erro: null })

  useEffect(() => {
    const controle = new AbortController()
    getJson<Pagamento[]>('/api/licenca/pagamentos', controle.signal)
      .then((lista) => setPagamentos({ pagamentos: lista, erro: null }))
      .catch((e: unknown) => {
        if (!controle.signal.aborted)
          setPagamentos({ pagamentos: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar os pagamentos.' })
      })
    return () => controle.abort()
  }, [getJson])

  return (
    <div className="mx-auto max-w-4xl">
      <CabecalhoDaPagina secao="Administrativo" titulo="Licença e assinatura" descricao="Plano, vencimento e situação da mensalidade do Kamus." />

      {!situacao ? (
        <p className="mt-6 text-sm text-muted">Carregando...</p>
      ) : (
        <section className="mt-6 rounded-2xl border border-border bg-surface p-5 shadow-sm">
          <div className="flex flex-wrap items-center gap-3">
            <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${ESTADOS[situacao.estado].classe}`}>{ESTADOS[situacao.estado].rotulo}</span>
            <p className="min-w-60 flex-1 text-sm">{situacao.mensagem}</p>
          </div>
          <dl className="mt-5 grid gap-4 sm:grid-cols-3">
            {[
              ['Plano', situacao.plano],
              ['Vencimento', formatarDia(situacao.vencimento)],
              ['Valor', `${formatarMoeda(situacao.valorMensal)} / mês`],
              ['Carência até', formatarDia(situacao.fimDaCarencia)],
              ['CNPJs', limite(situacao.limiteDeCnpjs)],
              ['Usuários', limite(situacao.limiteDeUsuarios)],
            ].map(([rotulo, valor]) => (
              <div key={rotulo}>
                <dt className="text-xs text-muted">{rotulo}</dt>
                <dd className="mt-0.5 font-semibold tabular-nums">{valor}</dd>
              </div>
            ))}
          </dl>
        </section>
      )}

      <h2 className="mt-8 text-lg font-bold">Pagamentos</h2>
      {erro && (
        <p role="alert" className="mt-3 flex items-center gap-2 rounded-lg bg-danger-soft px-4 py-3 text-sm text-danger">
          <Icone nome="alert" />
          {erro}
        </p>
      )}
      {pagamentos && pagamentos.length === 0 && <p className="mt-3 text-sm text-muted">Nenhum pagamento registrado ainda.</p>}
      {pagamentos && pagamentos.length > 0 && (
        <div className="mt-3 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          <table aria-label="Pagamentos" className="w-full text-sm">
            <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
              <tr>
                <th className="px-4 py-2">Pago em</th>
                <th className="px-4 py-2">Valor</th>
                <th className="px-4 py-2">Meses</th>
                <th className="px-4 py-2">Forma</th>
                <th className="px-4 py-2">Cobre até</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {pagamentos.map((p) => (
                <tr key={p.id}>
                  <td className="px-4 py-2 tabular-nums">{formatarDia(p.pagoEm)}</td>
                  <td className="px-4 py-2 tabular-nums">{formatarMoeda(p.valor)}</td>
                  <td className="px-4 py-2 tabular-nums">{p.meses}</td>
                  <td className="px-4 py-2">{p.forma ?? '—'}</td>
                  <td className="px-4 py-2 tabular-nums">{formatarDia(p.estendeuAte)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
