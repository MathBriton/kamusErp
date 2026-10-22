import { useEffect, useRef, type ReactNode } from 'react'

const FOCAVEIS = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
const CAMPOS = 'input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled])'
/** Diálogos abertos, do mais antigo ao mais recente: só o de cima responde a Tab e Esc. */
const abertos: object[] = []

/**
 * Janela sobreposta centralizada. Esc ou clique fora fecham.
 * Acessível pelo teclado: ao abrir, o foco vai para o primeiro campo; Tab fica preso dentro da janela; ao fechar,
 * o foco volta para quem a abriu.
 */
export function Dialogo({ titulo, onFechar, largo = false, children }: { titulo: string; onFechar: () => void; largo?: boolean; children: ReactNode }) {
  const janela = useRef<HTMLElement>(null)
  const fechar = useRef(onFechar)

  useEffect(() => {
    fechar.current = onFechar
  }, [onFechar])

  useEffect(() => {
    const anterior = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const raiz = janela.current
    const primeiro = raiz?.querySelector<HTMLElement>(CAMPOS) ?? raiz?.querySelector<HTMLElement>(FOCAVEIS)
    primeiro?.focus()
    const eu = {}
    abertos.push(eu)

    const aoTeclar = (e: KeyboardEvent) => {
      if (abertos.at(-1) !== eu) return
      if (e.key === 'Escape') {
        fechar.current()
        return
      }
      if (e.key !== 'Tab' || !raiz) return
      const focaveis = [...raiz.querySelectorAll<HTMLElement>(FOCAVEIS)]
      if (focaveis.length === 0) return
      const ativo = document.activeElement
      const [inicio, fim] = [focaveis[0]!, focaveis[focaveis.length - 1]!]
      if (!raiz.contains(ativo)) {
        e.preventDefault()
        inicio.focus()
      } else if (e.shiftKey && ativo === inicio) {
        e.preventDefault()
        fim.focus()
      } else if (!e.shiftKey && ativo === fim) {
        e.preventDefault()
        inicio.focus()
      }
    }
    window.addEventListener('keydown', aoTeclar)
    return () => {
      window.removeEventListener('keydown', aoTeclar)
      abertos.splice(abertos.indexOf(eu), 1)
      if (anterior?.isConnected) anterior.focus()
    }
  }, [])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button type="button" tabIndex={-1} aria-label="Fechar" onClick={onFechar} className="absolute inset-0 bg-black/40" />
      <section
        ref={janela}
        role="dialog"
        aria-modal="true"
        aria-label={titulo}
        className={`relative max-h-[90dvh] w-full ${largo ? 'max-w-2xl' : 'max-w-lg'} overflow-y-auto rounded-2xl border border-border bg-surface p-6 shadow-xl`}
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
