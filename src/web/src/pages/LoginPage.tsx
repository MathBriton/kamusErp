import { useState, type FormEvent } from 'react'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { Icone } from '../components/Icone'
import { Logo } from '../components/Logo'
import { ThemeToggle } from '../components/ThemeToggle'

export function LoginPage() {
  const { entrar } = useAuth()
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [manterConectado, setManterConectado] = useState(false)
  const [mostrarSenha, setMostrarSenha] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (!email.trim() || !senha) {
      setErro('Informe e-mail e senha.')
      return
    }
    setErro(null)
    setEnviando(true)
    try {
      await entrar(email.trim(), senha, manterConectado)
    } catch (err) {
      setErro(err instanceof ErroDeApi ? err.message : 'Algo deu errado. Tente novamente.')
      setEnviando(false)
    }
  }

  return (
    <main className="grid min-h-dvh lg:grid-cols-2">
      {/* Painel da marca (cores fixas de propósito: é a identidade visual, igual nos dois temas) */}
      <section className="relative hidden flex-col overflow-hidden bg-gradient-to-br from-[#0d5c3a] via-[#004328] to-[#1e293b] p-14 text-white lg:flex">
        <Logo invertido />
        <div className="mt-auto max-w-md">
          <h2 className="text-4xl leading-tight font-bold tracking-tight">
            Sua loja de roupas,
            <br />
            organizada.
          </h2>
          <p className="mt-4 text-base leading-relaxed text-white/80">
            Estoque em grade (cor × tamanho), vendas, financeiro e indicadores atualizados a cada minuto — na web e no celular.
          </p>
          <p className="mt-3 text-sm leading-relaxed text-white/70">A nota fiscal continua no emissor que a loja já usa.</p>
        </div>
        <p className="mt-10 text-xs text-white/60">© 2026 Kamus</p>
      </section>

      <section className="flex flex-col bg-surface p-6 sm:p-10">
        <div className="flex items-center justify-between">
          <span className="lg:hidden">
            <Logo />
          </span>
          <span className="ml-auto">
            <ThemeToggle />
          </span>
        </div>

        <form onSubmit={onSubmit} noValidate className="m-auto w-full max-w-sm py-10">
          <h1 className="text-3xl font-bold tracking-tight">Entrar</h1>
          <p className="mt-1.5 mb-7 text-muted">Acesse o painel da sua loja.</p>
          {/* No celular o painel da marca some: o resumo do produto vem aqui. */}
          <div className="-mt-4 mb-6 rounded-xl bg-surface-2 p-3 text-xs leading-relaxed text-muted lg:hidden">
            <p>Estoque em grade (cor × tamanho), vendas e financeiro da sua loja de roupas, com indicadores atualizados a cada minuto.</p>
            <p className="mt-1">A nota fiscal continua no emissor que a loja já usa.</p>
          </div>

          <label htmlFor="email" className="mb-1.5 block text-sm font-semibold">
            E-mail
          </label>
          <div className="mb-4 flex items-center gap-2.5 rounded-lg border border-border bg-surface px-3 focus-within:border-primary focus-within:ring-3 focus-within:ring-primary-soft">
            <Icone nome="mail" className="size-4 text-muted" />
            <input
              id="email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="h-10 w-full bg-transparent outline-none"
            />
          </div>

          <label htmlFor="senha" className="mb-1.5 block text-sm font-semibold">
            Senha
          </label>
          <div className="mb-3 flex items-center gap-2.5 rounded-lg border border-border bg-surface px-3 focus-within:border-primary focus-within:ring-3 focus-within:ring-primary-soft">
            <Icone nome="lock" className="size-4 text-muted" />
            <input
              id="senha"
              type={mostrarSenha ? 'text' : 'password'}
              autoComplete="current-password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              className="h-10 w-full bg-transparent outline-none"
            />
            <button
              type="button"
              onClick={() => setMostrarSenha((v) => !v)}
              aria-label={mostrarSenha ? 'Ocultar senha' : 'Mostrar senha'}
              className="text-muted hover:text-foreground"
            >
              <Icone nome={mostrarSenha ? 'eyeOff' : 'eye'} />
            </button>
          </div>

          <label className="mb-5 flex w-fit cursor-pointer items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={manterConectado}
              onChange={(e) => setManterConectado(e.target.checked)}
              className="size-4 accent-primary"
            />
            Manter conectado
          </label>

          {erro && (
            <p role="alert" className="mb-4 flex items-start gap-2 rounded-lg bg-danger-soft px-3 py-2.5 text-sm text-danger">
              <Icone nome="alert" className="mt-0.5 size-4" />
              {erro}
            </p>
          )}

          <button
            type="submit"
            disabled={enviando}
            className="flex h-11 w-full items-center justify-center gap-2 rounded-lg bg-primary font-medium text-primary-foreground transition-opacity disabled:opacity-60"
          >
            {enviando ? 'Entrando...' : 'Entrar'}
            {!enviando && <Icone nome="arrowRight" />}
          </button>

          <p className="mt-5 text-center text-sm text-muted">
            Ainda não tem acesso? Peça ao responsável pela loja para criar seu usuário em Administrativo → Usuários e senhas.
          </p>

          {import.meta.env.DEV && (
            <div className="mt-7 rounded-lg border border-dashed border-warning bg-warning-soft px-3.5 py-3 text-xs leading-relaxed text-warning">
              <b>Ambiente de desenvolvimento</b>
              <br />
              Usuário de teste: <b>admin@kamus.local</b> · senha <b>Kamus@Dev123</b>
              <br />
              (definida em <code>appsettings.Development.json</code>; dados em memória somem ao reiniciar a API)
            </div>
          )}
        </form>

        <p className="flex items-center justify-center gap-1.5 text-xs text-muted">
          <Icone nome="shield" className="size-3.5" />
          Conexão segura · bloqueio após 5 tentativas
        </p>
      </section>
    </main>
  )
}
