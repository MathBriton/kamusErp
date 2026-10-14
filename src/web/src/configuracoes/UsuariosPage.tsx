import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { CredenciaisProvisorias } from './CredenciaisProvisorias'
import { PERFIS, type EmpresaDoCliente, type SenhaProvisoria, type UsuarioDoCliente } from './tipos'

type Janela = { tipo: 'novo' } | { tipo: 'editar'; usuario: UsuarioDoCliente } | { tipo: 'senha'; credenciais: SenhaProvisoria } | null

export function UsuariosPage() {
  const { getJson, enviar } = useApi()
  const { estado } = useAuth()
  const euId = estado.status === 'autenticado' ? estado.sessao.usuario.id : ''
  const [usuarios, setUsuarios] = useState<UsuarioDoCliente[] | null>(null)
  const [empresas, setEmpresas] = useState<EmpresaDoCliente[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [janela, setJanela] = useState<Janela>(null)
  const [confirmando, setConfirmando] = useState<string | null>(null)

  const carregar = useCallback(async () => {
    try {
      const [u, e] = await Promise.all([getJson<UsuarioDoCliente[]>('/api/usuarios'), getJson<EmpresaDoCliente[]>('/api/empresas')])
      setUsuarios(u)
      setEmpresas(e)
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível carregar os usuários.')
    }
  }, [getJson])

  useEffect(() => {
    const timer = window.setTimeout(() => void carregar(), 0)
    return () => window.clearTimeout(timer)
  }, [carregar])

  async function executar(acao: () => Promise<unknown>) {
    setErro(null)
    setConfirmando(null)
    try {
      await acao()
      await carregar()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível concluir.')
    }
  }

  const redefinir = (u: UsuarioDoCliente) =>
    void executar(async () => setJanela({ tipo: 'senha', credenciais: await enviar<SenhaProvisoria>('POST', `/api/usuarios/${u.id}/redefinir-senha`) }))

  return (
    <div className="mx-auto max-w-6xl">
      <p className="text-xs text-muted">Configurações</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Usuários e permissões</h1>
          <p className="mt-1 text-sm text-muted">Cada pessoa pode ter um perfil diferente em cada empresa. Usuário novo recebe senha provisória.</p>
        </div>
        <button type="button" onClick={() => setJanela({ tipo: 'novo' })} className={estilos.botaoPrimario}>
          Novo usuário
        </button>
      </div>

      {erro && (
        <p role="alert" className={`mt-5 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {usuarios && (
        <div className="mt-5 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
              <tr>
                <th className="px-4 py-2">Nome</th>
                <th className="px-4 py-2">E-mail</th>
                <th className="px-4 py-2">Perfis</th>
                <th className="px-4 py-2">Situação</th>
                <th className="px-4 py-2 text-right">Ações</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {usuarios.map((u) => (
                <tr key={u.id} className={u.ativo ? '' : 'text-muted'}>
                  <td className="px-4 py-2.5 font-semibold">{u.nome}</td>
                  <td className="px-4 py-2.5">{u.email}</td>
                  <td className="px-4 py-2.5">
                    <ul className="space-y-0.5">
                      {u.acessos.map((a) => (
                        <li key={a.empresaId}>{`${a.empresa}: ${a.perfil}`}</li>
                      ))}
                    </ul>
                  </td>
                  <td className="px-4 py-2.5">
                    {!u.ativo ? (
                      <span className="rounded-full bg-surface-2 px-2 py-0.5 text-xs font-semibold text-muted">Inativo</span>
                    ) : u.deveTrocarSenha ? (
                      <span className="rounded-full bg-warning-soft px-2 py-0.5 text-xs font-semibold text-warning">Aguardando 1º acesso</span>
                    ) : (
                      <span className="rounded-full bg-success-soft px-2 py-0.5 text-xs font-semibold text-success">Ativo</span>
                    )}
                  </td>
                  <td className="px-4 py-2.5">
                    <div className="flex flex-wrap justify-end gap-2">
                      {confirmando === u.id ? (
                        <>
                          <span className="self-center text-xs">Encerra as sessões dele.</span>
                          <button type="button" onClick={() => void executar(() => enviar('POST', `/api/usuarios/${u.id}/desativar`))} className={`${estilos.botao} text-danger`}>
                            Confirmar
                          </button>
                          <button type="button" onClick={() => setConfirmando(null)} className={estilos.botao}>
                            Voltar
                          </button>
                        </>
                      ) : (
                        <>
                          <button type="button" onClick={() => setJanela({ tipo: 'editar', usuario: u })} className={estilos.botao}>
                            Editar
                          </button>
                          {u.id !== euId && u.ativo && (
                            <>
                              <button type="button" onClick={() => redefinir(u)} className={estilos.botao}>
                                Redefinir senha
                              </button>
                              <button type="button" onClick={() => setConfirmando(u.id)} className={`${estilos.botao} text-danger`}>
                                Desativar
                              </button>
                            </>
                          )}
                          {!u.ativo && (
                            <button type="button" onClick={() => void executar(() => enviar('POST', `/api/usuarios/${u.id}/reativar`))} className={estilos.botao}>
                              Reativar
                            </button>
                          )}
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {(janela?.tipo === 'novo' || janela?.tipo === 'editar') && (
        <FormularioDeUsuario
          usuario={janela.tipo === 'editar' ? janela.usuario : null}
          empresas={empresas}
          onFechar={() => setJanela(null)}
          onSalvo={(credenciais) => {
            setJanela(credenciais ? { tipo: 'senha', credenciais } : null)
            void carregar()
          }}
        />
      )}
      {janela?.tipo === 'senha' && (
        <Dialogo titulo="Senha provisória" onFechar={() => setJanela(null)}>
          <CredenciaisProvisorias email={janela.credenciais.email} senha={janela.credenciais.senhaProvisoria} onConcluir={() => setJanela(null)} />
        </Dialogo>
      )}
    </div>
  )
}

function FormularioDeUsuario({ usuario, empresas, onFechar, onSalvo }: {
  usuario: UsuarioDoCliente | null
  empresas: EmpresaDoCliente[]
  onFechar: () => void
  onSalvo: (credenciais: SenhaProvisoria | null) => void
}) {
  const { enviar } = useApi()
  const [nome, setNome] = useState(usuario?.nome ?? '')
  const [email, setEmail] = useState(usuario?.email ?? '')
  const [perfis, setPerfis] = useState<Record<string, string>>(() =>
    Object.fromEntries((usuario?.acessos ?? []).map((a) => [a.empresaId, a.perfil])),
  )
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    const acessos = empresas.filter((emp) => perfis[emp.id]).map((emp) => ({ empresaId: emp.id, perfil: perfis[emp.id]! }))
    setEnviando(true)
    setErro(null)
    try {
      if (usuario) {
        await enviar('PUT', `/api/usuarios/${usuario.id}`, { nome, acessos })
        onSalvo(null)
      } else {
        onSalvo(await enviar<SenhaProvisoria>('POST', '/api/usuarios', { nome, email, acessos }))
      }
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível salvar.')
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={usuario ? 'Editar usuário' : 'Novo usuário'} onFechar={onFechar}>
      <form onSubmit={(e) => void salvar(e)} className="space-y-4">
        <Campo rotulo="Nome">
          <input value={nome} onChange={(e) => setNome(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="E-mail">
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} disabled={!!usuario} className={estilos.campo} />
        </Campo>
        <fieldset>
          <legend className="mb-1.5 text-sm font-semibold">Acesso por empresa</legend>
          <div className="space-y-2">
            {empresas.map((emp) => (
              <label key={emp.id} className="flex items-center gap-3 text-sm">
                <span className="flex-1">{emp.nomeFantasia}</span>
                <select
                  aria-label={`Perfil em ${emp.nomeFantasia}`}
                  value={perfis[emp.id] ?? ''}
                  onChange={(e) => setPerfis({ ...perfis, [emp.id]: e.target.value })}
                  className="h-9 w-40 rounded-lg border border-border bg-surface px-2 text-sm"
                >
                  <option value="">Sem acesso</option>
                  {PERFIS.map((p) => (
                    <option key={p} value={p}>
                      {p}
                    </option>
                  ))}
                </select>
              </label>
            ))}
          </div>
        </fieldset>
        {erro && (
          <p role="alert" className={estilos.erro}>
            {erro}
          </p>
        )}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>
            Cancelar
          </button>
          <button type="submit" disabled={enviando} className={estilos.botaoPrimario}>
            {enviando ? 'Salvando...' : 'Salvar'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
