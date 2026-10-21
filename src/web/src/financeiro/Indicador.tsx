/** Cartão de total acima da grade (soma de todos os registros filtrados, não só da página). */
export function Indicador({ rotulo, valor, destaque = false }: { rotulo: string; valor: string; destaque?: boolean }) {
  return (
    <div className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
      <dt className="text-xs text-muted uppercase">{rotulo}</dt>
      <dd className={`mt-1 text-xl font-bold tabular-nums ${destaque ? 'text-danger' : ''}`}>{valor}</dd>
    </div>
  )
}
