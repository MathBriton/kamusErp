import { useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { Confirmacao } from '../components/grade/Confirmacao'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade, useListaPaginada } from '../components/grade/useGrade'
import { Icone } from '../components/Icone'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { CredenciaisProvisorias } from './CredenciaisProvisorias'
import { PERFIS, type EmpresaDoCliente, type SenhaProvisoria, type UsuarioDoCliente } from './tipos'

type Janela =
  | { tipo: 'novo' }
  | { tipo: 'editar'; usuario: UsuarioDoCliente }
  | { tipo: 'senha'; credenciais: SenhaProvisoria }
  | { tipo: 'mudarSituacao'; usuario: UsuarioDoCliente }
  | { tipo: 'mudarSelecionados'; ativo: boolean }
  | null

const FILTROS = { situacao: 'todos', busca: '', empresaId: '', perfil: '' }

function Situacao({ usuario }: { usuario: UsuarioDoCliente }) {
  if (!usuario.ativo) return <span className="rounded-full bg-surface-2 px-2 py-0.5 text-xs font-semibold text-muted">Inativo</span>
  if (usuario.deveTrocarSenha)
    return <span className="rounded-full bg-warning-soft px-2 py-0.5 text-xs font-semibold whitespace-nowrap text-warning">Aguardando 1º acesso</span>
  return <span className="rounded-full bg-success-soft px-2 py-0.5 text-xs font-semibold text-success">Ativo</span>
}

/** Usuários e senhas em grade: filtros, perfil por empresa, ações por linha (editar, redefinir senha, desativar) e em lote. */
export function UsuariosPage() {
  const { getJson, enviar } = useApi()
  const { estado } = useAuth()
  const euId = estado.status === 'autenticado' ? estado.sessao.usuario.id : ''
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { dados, erro: erroDaLista } = useListaPaginada<{ itens: UsuarioDoCliente[]; total: number }>(`/api/usuarios?${grade.consulta}`, versao, grade.filtros.busca ? 250 : 0)
  const [empresas, setEmpresas] = useState<EmpresaDoCliente[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [janela, setJanela] = useState<Janela>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<EmpresaDoCliente[]>('/api/empresas', controle.signal).then(setEmpresas).catch(() => {})
    return () => controle.abort()
  }, [getJson])

  const recarregar = () => setVersao((v) => v + 1)

  async function redefinir(u: UsuarioDoCliente) {
    setErro(null)
    try {
      setJanela({ tipo: 'senha', credenciais: await enviar<SenhaProvisoria>('POST', `/api/usuarios/${u.id}/redefinir-senha`) })
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível redefinir a senha.')
    }
  }

  const colunas: Coluna<UsuarioDoCliente>[] = [
    { titulo: 'Nome', celula: (u) => <span className="font-semibold">{u.nome}</span> },
    { titulo: 'E-mail', celula: (u) => u.email },
    {
      titulo: 'Perfis',
      celula: (u) => (
        <ul className="space-y-0.5">
          {u.acessos.map((a) => (
            <li key={a.empresaId}>{`${a.empresa}: ${a.perfil}`}</li>
          ))}
        </ul>
      ),
    },
    { titulo: 'Situação', celula: (u) => <Situacao usuario={u} /> },
  ]

  const mensagem = erro ?? erroDaLista

  return (
    <div className="mx-auto max-w-6xl">
      <CabecalhoDaPagina
        secao="Administrativo"
        titulo="Usuários e senhas"
        descricao="Crie o acesso de cada pessoa: perfil por empresa e senha (gerada pelo sistema ou definida por você). No primeiro login ela cria uma senha própria."
        acoes={
          <>
            <BotaoExportar relatorio="usuarios" parametros={grade.parametros} selecionados={[...grade.selecionados]} />
            <button type="button" onClick={() => setJanela({ tipo: 'novo' })} className={estilos.botaoPrimario}>
              <Icone nome="plus" />
              Novo usuário
            </button>
          </>
        }
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="min-w-60 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input value={grade.filtros.busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Nome ou e-mail" className={estilos.campo} />
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">Situação</span>
          <select value={grade.filtros.situacao} onChange={(e) => grade.filtrar({ situacao: e.target.value })} className={estilos.campo}>
            <option value="todos">Todos</option>
            <option value="ativos">Ativos</option>
            <option value="inativos">Inativos</option>
          </select>
        </label>
        <label className="w-52 text-sm">
          <span className="mb-1 block font-semibold">Empresa</span>
          <select value={grade.filtros.empresaId} onChange={(e) => grade.filtrar({ empresaId: e.target.value })} className={estilos.campo}>
            <option value="">Todas</option>
            {empresas.map((e) => (
              <option key={e.id} value={e.id}>{e.nomeFantasia}</option>
            ))}
          </select>
        </label>
        <label className="w-44 text-sm">
          <span className="mb-1 block font-semibold">Perfil</span>
          <select value={grade.filtros.perfil} onChange={(e) => grade.filtrar({ perfil: e.target.value })} className={estilos.campo}>
            <option value="">Todos</option>
            {PERFIS.map((p) => (
              <option key={p} value={p}>{p}</option>
            ))}
          </select>
        </label>
      </BarraDeFiltros>

      {mensagem && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {mensagem}
        </p>
      )}

      <GradeDeRegistros
        rotulo="Usuários"
        colunas={colunas}
        itens={dados?.itens ?? (erroDaLista ? [] : null)}
        chave={(u) => u.id}
        nome={(u) => u.nome}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        apagada={(u) => !u.ativo}
        vazio="Nenhum usuário encontrado com esses filtros."
        acoes={(u) => (
          <>
            <AcaoDaLinha rotulo={`Editar ${u.nome}`} icone="pencil" onClick={() => setJanela({ tipo: 'editar', usuario: u })} />
            {u.id !== euId && u.ativo && <AcaoDaLinha rotulo={`Redefinir senha de ${u.nome}`} icone="lock" onClick={() => void redefinir(u)} />}
            {u.id !== euId && (
              <AcaoDaLinha
                rotulo={`${u.ativo ? 'Desativar' : 'Reativar'} ${u.nome}`}
                icone="power"
                perigo={u.ativo}
                onClick={() => setJanela({ tipo: 'mudarSituacao', usuario: u })}
              />
            )}
          </>
        )}
        acoesEmLote={
          <>
            <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionados', ativo: false })} className={estilos.botao}>Desativar selecionados</button>
            <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionados', ativo: true })} className={estilos.botao}>Reativar selecionados</button>
          </>
        }
      />

      {(janela?.tipo === 'novo' || janela?.tipo === 'editar') && (
        <FormularioDeUsuario
          usuario={janela.tipo === 'editar' ? janela.usuario : null}
          empresas={empresas}
          onFechar={() => setJanela(null)}
          onSalvo={(credenciais) => {
            setJanela(credenciais ? { tipo: 'senha', credenciais } : null)
            recarregar()
          }}
        />
      )}
      {janela?.tipo === 'senha' && (
        <Dialogo titulo="Senha provisória" onFechar={() => setJanela(null)}>
          <CredenciaisProvisorias email={janela.credenciais.email} senha={janela.credenciais.senhaProvisoria} onConcluir={() => setJanela(null)} />
        </Dialogo>
      )}
      {janela?.tipo === 'mudarSituacao' && (
        <Confirmacao
          titulo={janela.usuario.ativo ? 'Desativar usuário' : 'Reativar usuário'}
          rotulo={janela.usuario.ativo ? 'Desativar' : 'Reativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', `/api/usuarios/${janela.usuario.id}/${janela.usuario.ativo ? 'desativar' : 'reativar'}`)
            setJanela(null)
            recarregar()
          }}
        >
          {janela.usuario.ativo
            ? <p>{`${janela.usuario.nome} não entra mais no sistema e as sessões abertas são encerradas. Dá para reativar depois.`}</p>
            : <p>{`${janela.usuario.nome} volta a entrar com a senha atual (conta no limite de usuários do plano).`}</p>}
        </Confirmacao>
      )}
      {janela?.tipo === 'mudarSelecionados' && (
        <Confirmacao
          titulo={janela.ativo ? 'Reativar usuários' : 'Desativar usuários'}
          rotulo={janela.ativo ? 'Reativar' : 'Desativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', '/api/usuarios/situacao', { ids: [...grade.selecionados], ativo: janela.ativo })
            grade.limparSelecao()
            setJanela(null)
            recarregar()
          }}
        >
          <p>
            {`${janela.ativo ? 'Reativar' : 'Desativar'} ${grade.selecionados.size === 1 ? 'o usuário selecionado' : `os ${grade.selecionados.size} usuários selecionados`}?`}
            {!janela.ativo && ' As sessões abertas são encerradas.'}
          </p>
        </Confirmacao>
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
  const [definirSenha, setDefinirSenha] = useState(false)
  const [senha, setSenha] = useState('')
  const [repeticao, setRepeticao] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    if (!usuario && definirSenha && senha !== repeticao) {
      setErro('As senhas não conferem.')
      return
    }
    const acessos = empresas.filter((emp) => perfis[emp.id]).map((emp) => ({ empresaId: emp.id, perfil: perfis[emp.id]! }))
    setEnviando(true)
    setErro(null)
    try {
      if (usuario) {
        await enviar('PUT', `/api/usuarios/${usuario.id}`, { nome, acessos })
        onSalvo(null)
      } else {
        onSalvo(await enviar<SenhaProvisoria>('POST', '/api/usuarios', definirSenha ? { nome, email, senha, acessos } : { nome, email, acessos }))
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
        {!usuario && (
          <fieldset className="space-y-2">
            <legend className="mb-1.5 text-sm font-semibold">Senha de acesso</legend>
            <label className="flex items-center gap-2 text-sm">
              <input type="radio" name="senha" checked={!definirSenha} onChange={() => setDefinirSenha(false)} className="h-4 w-4" />
              Gerar uma senha provisória
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input type="radio" name="senha" checked={definirSenha} onChange={() => setDefinirSenha(true)} className="h-4 w-4" />
              Definir a senha agora
            </label>
            {definirSenha && (
              <div className="grid gap-3 sm:grid-cols-2">
                <Campo rotulo="Senha">
                  <input type="password" autoComplete="new-password" value={senha} onChange={(e) => setSenha(e.target.value)} className={estilos.campo} />
                </Campo>
                <Campo rotulo="Repita a senha">
                  <input type="password" autoComplete="new-password" value={repeticao} onChange={(e) => setRepeticao(e.target.value)} className={estilos.campo} />
                </Campo>
                <p className="text-xs text-muted sm:col-span-2">Pelo menos 8 caracteres, com letras e números. No primeiro login a pessoa troca por uma senha só dela.</p>
              </div>
            )}
          </fieldset>
        )}
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
