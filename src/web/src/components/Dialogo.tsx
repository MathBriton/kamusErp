import { useEffect, type ReactNode } from 'react'

/** Janela sobreposta centralizada. Esc ou clique fora fecham. */
export function Dialogo({ titulo, onFechar, children }: { titulo: string; onFechar: () => void; children: ReactNode }) {
  useEffect(() => {
    const aoTeclar = (e: KeyboardEvent) => e.key === 'Escape' && onFechar()
    window.addEventListener('keydown', aoTeclar)
    return () => window.removeEventListener('keydown', aoTeclar)
  }, [onFechar])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button type="button" aria-label="Fechar" onClick={onFechar} className="absolute inset-0 bg-black/40" />
      <section
        role="dialog"
        aria-modal="true"
        aria-label={titulo}
        className="relative max-h-[90dvh] w-full max-w-lg overflow-y-auto rounded-2xl border border-border bg-surface p-6 shadow-xl"
      >
        <header className="mb-4 flex items-start justify-between gap-4">
          <h2 className="text-lg font-bold">{titulo}</h2>
          <button type="button" onClick={onFechar} className="text-sm text-muted hover:text-foreground">
            Fechar
          </button>
        </header>
        {children}
      </section>
    </div>
  )
}
