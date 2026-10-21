import type { ReactNode } from 'react'

/** Seção (migalha), título, descrição e as ações principais da tela (ex.: "Novo", "Exportar"). */
export function CabecalhoDaPagina({ secao, titulo, descricao, acoes }: { secao: string; titulo: string; descricao?: string; acoes?: ReactNode }) {
  return (
    <>
      <p className="text-xs text-muted">{secao}</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">{titulo}</h1>
          {descricao && <p className="mt-1 text-sm text-muted">{descricao}</p>}
        </div>
        {acoes && <div className="flex flex-wrap items-center gap-2">{acoes}</div>}
      </div>
    </>
  )
}
