import type { ReactNode } from 'react'

/** Linha de filtros acima da grade; "Limpar filtros" aparece quando algum filtro está diferente do padrão. */
export function BarraDeFiltros({ children, ativos, onLimpar, acoes }: { children: ReactNode; ativos: boolean; onLimpar: () => void; acoes?: ReactNode }) {
  return (
    <div role="search" aria-label="Filtros" className="mt-6 flex flex-wrap items-end gap-3 rounded-2xl border border-border bg-surface p-4 shadow-sm">
      {children}
      {ativos && (
        <button type="button" onClick={onLimpar} className="h-10 px-2 text-sm font-medium text-primary hover:underline">
          Limpar filtros
        </button>
      )}
      {acoes && <div className="ml-auto flex flex-wrap items-end gap-2">{acoes}</div>}
    </div>
  )
}
