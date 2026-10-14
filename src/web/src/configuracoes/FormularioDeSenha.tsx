import { useState, type FormEvent } from 'react'
import { ErroDeApi, trocarSenha } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { Campo } from '../components/Campo'
import { estilos } from '../components/estilos'

/** Senha atual + nova (com confirmação). Regras conferidas pela API: 8+ caracteres, letras e números. */
export function FormularioDeSenha({ rotuloAtual, onTrocada }: { rotuloAtual: string; onTrocada: () => void | Promise<void> }) {
  const { estado } = useAuth()
  const [atual, setAtual] = useState('')
  const [nova, setNova] = useState('')
  const [repetida, setRepetida] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    if (nova !== repetida) {
      setErro('As senhas não conferem.')
      return
    }
    if (estado.status !== 'autenticado') return
    setEnviando(true)
    setErro(null)
    try {
      await trocarSenha(estado.sessao.accessToken, atual, nova)
      await onTrocada()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível trocar a senha.')
      setEnviando(false)
    }
  }

  return (
    <form onSubmit={(e) => void salvar(e)} className="space-y-4">
      <Campo rotulo={rotuloAtual}>
        <input type="password" autoComplete="current-password" value={atual} onChange={(e) => setAtual(e.target.value)} className={estilos.campo} />
      </Campo>
      <Campo rotulo="Nova senha">
        <input type="password" autoComplete="new-password" value={nova} onChange={(e) => setNova(e.target.value)} className={estilos.campo} />
      </Campo>
      <Campo rotulo="Repita a nova senha">
        <input type="password" autoComplete="new-password" value={repetida} onChange={(e) => setRepetida(e.target.value)} className={estilos.campo} />
      </Campo>
      <p className="text-xs text-muted">Pelo menos 8 caracteres, com letras e números.</p>
      {erro && (
        <p role="alert" className={estilos.erro}>
          {erro}
        </p>
      )}
      <button type="submit" disabled={enviando} className={`${estilos.botaoPrimario} w-full`}>
        {enviando ? 'Salvando...' : 'Salvar senha'}
      </button>
    </form>
  )
}
