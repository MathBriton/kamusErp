import { useState, type FormEvent } from 'react'
import { chamar, ErroDeApi, type Sessao } from './api'
import { Campo } from './Campo'
import { botaoPrimario, campo } from './formatos'
import { Marca } from './Marca'

export function LoginPage({ expirou, onEntrar }: { expirou: boolean; onEntrar: (sessao: Sessao) => void }) {
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function entrar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setErro(null)
    try {
      onEntrar(await chamar<Sessao>('/admin-api/login', { metodo: 'POST', corpo: { email, senha } }))
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível entrar.')
      setEnviando(false)
    }
  }

  return (
    <main className="grid min-h-dvh place-items-center bg-background p-4">
      <form onSubmit={(e) => void entrar(e)} className="w-full max-w-sm space-y-4 rounded-2xl border border-border bg-surface p-8 shadow-lg">
        <Marca />
        <div>
          <h1 className="text-xl font-bold">Painel Administrativo</h1>
          <p className="text-sm text-muted">Acesso restrito ao operador do Kamus.</p>
        </div>
        {expirou && (
          <p role="status" className="rounded-lg bg-warning-soft px-3 py-2 text-sm text-warning">
            Sua sessão expirou. Entre novamente.
          </p>
        )}
        <Campo rotulo="E-mail">
          <input type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} className={campo} />
        </Campo>
        <Campo rotulo="Senha">
          <input type="password" autoComplete="current-password" required value={senha} onChange={(e) => setSenha(e.target.value)} className={campo} />
        </Campo>
        {erro && (
          <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
            {erro}
          </p>
        )}
        <button type="submit" disabled={enviando} className={`${botaoPrimario} w-full`}>
          {enviando ? 'Entrando...' : 'Entrar'}
        </button>
      </form>
    </main>
  )
}
