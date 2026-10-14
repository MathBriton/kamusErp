import { useAuth } from '../auth/AuthContext'
import type { Sessao } from '../auth/api'
import { ApiStatus } from '../components/ApiStatus'
import { Icone } from '../components/Icone'
import { Logo } from '../components/Logo'
import { ThemeToggle } from '../components/ThemeToggle'

/** Página provisória pós-login (v0.3.0). O shell completo — sidebar, topbar, troca de CNPJ — chega na v0.4.0. */
export function InicioPage({ sessao }: { sessao: Sessao }) {
  const { sair } = useAuth()

  return (
    <div className="min-h-dvh">
      <header className="flex h-16 items-center gap-4 border-b border-border bg-surface px-6">
        <Logo />
        <span className="ml-auto hidden sm:inline">
          <ApiStatus />
        </span>
        <ThemeToggle />
        <button
          type="button"
          onClick={() => void sair()}
          className="flex h-9 items-center gap-2 rounded-lg border border-border px-3 text-sm font-medium hover:bg-surface-2"
        >
          <Icone nome="logOut" />
          Sair
        </button>
      </header>

      <main className="mx-auto max-w-3xl p-6">
        <h1 className="text-2xl font-bold tracking-tight">Olá, {sessao.usuario.nome} 👋</h1>
        <p className="mt-1 text-muted">{sessao.usuario.email}</p>

        <section className="mt-8 rounded-2xl border border-border bg-surface p-6 shadow-sm">
          <h2 className="font-semibold">Empresas que você acessa</h2>
          <ul className="mt-4 divide-y divide-border">
            {sessao.empresas.map((empresa) => (
              <li key={empresa.id} className="flex items-center gap-3 py-3">
                <span className="grid size-9 place-items-center rounded-lg bg-primary-soft text-primary">
                  <Icone nome="building" />
                </span>
                <span className="flex-1">
                  <span className="block font-medium">{empresa.nomeFantasia}</span>
                  <span className="text-sm text-muted">{empresa.cnpj}</span>
                </span>
                <span className="rounded-full bg-info-soft px-2.5 py-0.5 text-xs font-semibold text-info">{empresa.perfil}</span>
              </li>
            ))}
          </ul>
        </section>

        <p className="mt-6 text-sm text-muted">
          Página provisória da v0.3.0. Na v0.4.0 entram o menu lateral, a barra superior com pesquisa e notificações e a troca de CNPJ.
        </p>
      </main>
    </div>
  )
}
