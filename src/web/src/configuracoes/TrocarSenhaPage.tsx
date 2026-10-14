import { useAuth } from '../auth/AuthContext'
import { Logo } from '../components/Logo'
import { FormularioDeSenha } from './FormularioDeSenha'

/** Primeiro acesso com senha provisória: nada do sistema aparece antes da troca. */
export function TrocarSenhaPage({ nome }: { nome: string }) {
  const { recarregar, sair } = useAuth()

  return (
    <main className="grid min-h-dvh place-items-center bg-background p-4">
      <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-8 shadow-lg">
        <Logo />
        <h1 className="mt-6 text-2xl font-bold tracking-tight">Crie sua senha</h1>
        <p className="mt-1.5 mb-6 text-sm text-muted">
          Olá, {nome.split(' ')[0]}! Você entrou com uma senha provisória. Defina uma senha só sua para continuar.
        </p>
        <FormularioDeSenha rotuloAtual="Senha atual (provisória)" onTrocada={recarregar} />
        <button type="button" onClick={() => void sair()} className="mt-4 w-full text-center text-sm text-muted hover:text-foreground">
          Sair
        </button>
      </div>
    </main>
  )
}
