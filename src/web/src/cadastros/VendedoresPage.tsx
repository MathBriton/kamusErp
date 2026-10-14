import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'

interface Vendedor {
  id: string
  nome: string
  comissaoPercentual: number
  usuarioId: string | null
  usuarioNome: string | null
  telefone: string | null
  ativo: boolean
}

interface Usuario {
  id: string
  nome: string
  email: string
}

const percentual = (valor: number) => `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(valor)}%`

export function VendedoresPage() {
  const { getJson, enviar } = useApi()
  const [vendedores, setVendedores] = useState<Vendedor[] | null>(null)
  const [usuarios, setUsuarios] = useState<Usuario[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [edicao, setEdicao] = useState<{ vendedor: Vendedor | null } | null>(null)

  const carregar = useCallback(async () => {
    try {
      const [v, u] = await Promise.all([getJson<Vendedor[]>('/api/vendedores'), getJson<Usuario[]>('/api/vendedores/usuarios')])
      setVendedores(v)
      setUsuarios(u)
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível carregar os vendedores.')
    }
  }, [getJson])

  useEffect(() => {
    const timer = window.setTimeout(() => void carregar(), 0)
    return () => window.clearTimeout(timer)
  }, [carregar])

  async function mudar(v: Vendedor) {
    setErro(null)
    try {
      await enviar('POST', `/api/vendedores/${v.id}/${v.ativo ? 'inativar' : 'reativar'}`)
      await carregar()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível concluir.')
    }
  }

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Cadastros</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Vendedores</h1>
          <p className="mt-1 text-sm text-muted">Comissão padrão de cada vendedor. Vincule a um usuário para ele ver as próprias vendas.</p>
        </div>
        <button type="button" onClick={() => setEdicao({ vendedor: null })} className={estilos.botaoPrimario}>
          Novo vendedor
        </button>
      </div>

      {erro && (
        <p role="alert" className={`mt-5 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {vendedores && (
        <div className="mt-5 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          {vendedores.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted">Nenhum vendedor cadastrado.</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-4 py-2">Nome</th>
                  <th className="px-4 py-2">Comissão</th>
                  <th className="px-4 py-2">Login</th>
                  <th className="px-4 py-2">Situação</th>
                  <th className="px-4 py-2 text-right">Ações</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {vendedores.map((v) => (
                  <tr key={v.id} className={v.ativo ? '' : 'text-muted'}>
                    <td className="px-4 py-2.5 font-semibold">{v.nome}</td>
                    <td className="px-4 py-2.5 tabular-nums">{percentual(v.comissaoPercentual)}</td>
                    <td className="px-4 py-2.5">{v.usuarioNome ? `vinculado: ${v.usuarioNome}` : 'sem login'}</td>
                    <td className="px-4 py-2.5">{v.ativo ? 'Ativo' : 'Inativo'}</td>
                    <td className="px-4 py-2.5">
                      <div className="flex justify-end gap-2">
                        <button type="button" onClick={() => setEdicao({ vendedor: v })} className={estilos.botao}>
                          Editar
                        </button>
                        <button type="button" onClick={() => void mudar(v)} className={estilos.botao}>
                          {v.ativo ? 'Inativar' : 'Reativar'}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {edicao && (
        <FormularioDeVendedor
          vendedor={edicao.vendedor}
          usuarios={usuarios}
          onFechar={() => setEdicao(null)}
          onSalvo={() => {
            setEdicao(null)
            void carregar()
          }}
        />
      )}
    </div>
  )
}

function FormularioDeVendedor({ vendedor, usuarios, onFechar, onSalvo }: { vendedor: Vendedor | null; usuarios: Usuario[]; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [nome, setNome] = useState(vendedor?.nome ?? '')
  const [comissao, setComissao] = useState(vendedor ? String(vendedor.comissaoPercentual).replace('.', ',') : '3')
  const [usuarioId, setUsuarioId] = useState(vendedor?.usuarioId ?? '')
  const [telefone, setTelefone] = useState(vendedor?.telefone ?? '')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setErro(null)
    const corpo = { nome, comissaoPercentual: Number(comissao.replace(',', '.')), usuarioId: usuarioId || null, telefone: telefone || null }
    try {
      if (vendedor) await enviar('PUT', `/api/vendedores/${vendedor.id}`, corpo)
      else await enviar('POST', '/api/vendedores', corpo)
      onSalvo()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível salvar.')
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={vendedor ? 'Editar vendedor' : 'Novo vendedor'} onFechar={onFechar}>
      <form onSubmit={(e) => void salvar(e)} className="space-y-4">
        <Campo rotulo="Nome">
          <input value={nome} onChange={(e) => setNome(e.target.value)} className={estilos.campo} />
        </Campo>
        <div className="grid grid-cols-2 gap-3">
          <Campo rotulo="Comissão (%)">
            <input value={comissao} onChange={(e) => setComissao(e.target.value)} inputMode="decimal" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Telefone">
            <input value={telefone} onChange={(e) => setTelefone(e.target.value)} inputMode="tel" className={estilos.campo} />
          </Campo>
        </div>
        <Campo rotulo="Usuário do sistema">
          <select value={usuarioId} onChange={(e) => setUsuarioId(e.target.value)} className={estilos.campo}>
            <option value="">Sem login</option>
            {usuarios.map((u) => (
              <option key={u.id} value={u.id}>
                {`${u.nome} (${u.email})`}
              </option>
            ))}
          </select>
        </Campo>
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
