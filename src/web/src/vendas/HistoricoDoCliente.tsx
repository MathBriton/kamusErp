import { useEffect, useState } from 'react'
import { useApi } from '../api/useApi'
import { moeda } from '../cadastros/catalogo'
import { dataBr } from '../financeiro/tipos'
import type { ListaDeVales, ListaDeVendas } from './tipos'

/** Na ficha do cliente: compras (concluídas) e vales-troca. Some se o perfil não acessa vendas. */
export function HistoricoDoCliente({ clienteId }: { clienteId: string }) {
  const { getJson } = useApi()
  const [vendas, setVendas] = useState<ListaDeVendas | null>(null)
  const [vales, setVales] = useState<ListaDeVales | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<ListaDeVendas>(`/api/vendas?${new URLSearchParams({ status: 'todas', clienteId })}`, controle.signal).then(setVendas).catch(() => {})
    getJson<ListaDeVales>(`/api/vendas/vales?${new URLSearchParams({ situacao: 'todos', clienteId })}`, controle.signal).then(setVales).catch(() => {})
    return () => controle.abort()
  }, [getJson, clienteId])

  if (!vendas && !vales) return null
  const concluidas = vendas?.itens.filter((v) => v.status === 'Concluida') ?? []
  const ativos = vales?.itens.filter((v) => v.situacao === 'Ativo') ?? []

  return (
    <section aria-label="Compras e vale-troca" className="space-y-2 rounded-xl border border-border bg-surface-2 p-4 text-sm">
      <h3 className="font-semibold">Compras e vale-troca</h3>
      {vendas && (
        <>
          <p>{`${vendas.quantidade} ${vendas.quantidade === 1 ? 'compra' : 'compras'} · ${moeda(vendas.total)}`}</p>
          {concluidas.length > 0 && (
            <ul className="space-y-0.5 text-muted">
              {concluidas.slice(0, 5).map((v) => (
                <li key={v.id}>{`${dataBr(v.data)} · venda nº ${v.numero} · ${v.pecas} ${v.pecas === 1 ? 'peça' : 'peças'} · ${moeda(v.total)}`}</li>
              ))}
            </ul>
          )}
        </>
      )}
      {ativos.length > 0 && (
        <ul className="space-y-0.5">
          {ativos.map((v) => (
            <li key={v.id} className="text-success">{`Vale ${v.codigo} · saldo ${moeda(v.saldo)} · válido até ${dataBr(v.validoAte)}`}</li>
          ))}
        </ul>
      )}
    </section>
  )
}
