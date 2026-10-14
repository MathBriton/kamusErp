import { useEffect, useState } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { estilos } from '../components/estilos'
import { moeda } from '../cadastros/catalogo'
import { ROTULO_DO_TIPO, type Movimento } from './tipos'

const dataHora = (iso: string) => new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

/** Extrato de movimentos de um SKU (mais recentes primeiro): cada linha explica o saldo. */
export function Kardex({ skuId, lojaId }: { skuId: string; lojaId: string }) {
  const { getJson } = useApi()
  const [{ movimentos, erro }, setEstado] = useState<{ movimentos: Movimento[] | null; erro: string | null }>({ movimentos: null, erro: null })

  useEffect(() => {
    const controle = new AbortController()
    const consulta = new URLSearchParams({ skuId })
    if (lojaId) consulta.set('lojaId', lojaId)
    getJson<Movimento[]>(`/api/estoque/kardex?${consulta}`, controle.signal)
      .then((m) => setEstado({ movimentos: m, erro: null }))
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setEstado({ movimentos: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar o kardex.' })
      })
    return () => controle.abort()
  }, [getJson, skuId, lojaId])

  if (erro) return <p role="alert" className={estilos.erro}>{erro}</p>
  if (!movimentos) return <p className="text-sm text-muted">Carregando...</p>
  if (movimentos.length === 0) return <p className="text-sm text-muted">Nenhum movimento ainda.</p>

  return (
    <div className="overflow-x-auto rounded-xl border border-border">
      <table aria-label="Kardex" className="w-full text-sm">
        <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
          <tr>
            <th className="px-3 py-2">Quando</th>
            <th className="px-3 py-2">Movimento</th>
            <th className="px-3 py-2">Loja</th>
            <th className="px-3 py-2 text-right">Qtd.</th>
            <th className="px-3 py-2 text-right">Saldo</th>
            <th className="px-3 py-2 text-right">Custo médio</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {movimentos.map((m) => (
            <tr key={m.id}>
              <td className="px-3 py-2 whitespace-nowrap text-muted">{dataHora(m.ocorridoEm)}</td>
              <td className="px-3 py-2">
                <span className="font-semibold">{ROTULO_DO_TIPO[m.tipo]}</span>
                <span className="block text-xs text-muted">
                  {[m.motivo, m.documento, m.lojaRelacionada && (m.quantidade < 0 ? `para ${m.lojaRelacionada}` : `de ${m.lojaRelacionada}`), m.observacao, m.usuario]
                    .filter(Boolean)
                    .join(' · ')}
                </span>
              </td>
              <td className="px-3 py-2">{m.loja}</td>
              <td className={`px-3 py-2 text-right font-semibold tabular-nums ${m.quantidade < 0 ? 'text-danger' : 'text-success'}`}>
                {m.quantidade > 0 ? `+${m.quantidade}` : m.quantidade}
              </td>
              <td className="px-3 py-2 text-right tabular-nums">{m.saldoApos}</td>
              <td className="px-3 py-2 text-right tabular-nums">{m.custoMedioApos === null ? '—' : moeda(m.custoMedioApos)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
