import { ApiStatus } from './components/ApiStatus'
import { ThemeToggle } from './components/ThemeToggle'

const TOKENS = ['primary', 'success', 'warning', 'danger', 'info'] as const
const SWATCH: Record<(typeof TOKENS)[number], string> = {
  primary: 'bg-primary-soft text-primary',
  success: 'bg-success-soft text-success',
  warning: 'bg-warning-soft text-warning',
  danger: 'bg-danger-soft text-danger',
  info: 'bg-info-soft text-info',
}

/** Página provisória da Fase 0a. O shell (sidebar/topbar) e o login chegam na Fase 0b. */
export function App() {
  return (
    <main className="grid min-h-dvh place-items-center p-6">
      <section className="w-full max-w-lg rounded-2xl border border-border bg-surface p-8 shadow-sm">
        <div className="flex items-center gap-3">
          <div className="grid size-9 place-items-center rounded-lg bg-primary font-extrabold text-primary-foreground">K</div>
          <span className="text-lg font-bold tracking-tight">kamus</span>
          <span className="ml-auto">
            <ApiStatus />
          </span>
        </div>

        <h1 className="mt-8 text-2xl font-bold tracking-tight">Fundação do projeto</h1>
        <p className="mt-2 text-muted">
          Fase 0a: backend .NET, PostgreSQL com isolamento por tenant, Docker Compose, CI e tema claro/escuro.
        </p>

        <div className="mt-6 flex flex-wrap gap-2">
          {TOKENS.map((token) => (
            <span key={token} className={`rounded-full px-3 py-1 text-xs font-semibold ${SWATCH[token]}`}>
              {token}
            </span>
          ))}
        </div>

        <div className="mt-8 flex items-center justify-between border-t border-border pt-6">
          <span className="text-sm font-medium">Tema</span>
          <ThemeToggle />
        </div>
      </section>
    </main>
  )
}
