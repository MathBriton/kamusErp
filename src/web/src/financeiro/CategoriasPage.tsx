import { useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { PERFIS_DO_FINANCEIRO, type Categoria } from './tipos'
import { useCategorias } from './useFinanceiro'

/** Plano de contas simples: categorias de receita e despesa agrupadas (base do fluxo por categoria e do DRE). */
export function CategoriasPage() {
  const { post, enviar } = useApi()
  const { perfilAtual } = useEmpresa()
  const [versao, setVersao] = useState(0)
  const categorias = useCategorias(versao)
  const [edicao, setEdicao] = useState<Categoria | 'nova' | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  if (!PERFIS_DO_FINANCEIRO.includes(perfilAtual))
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Categorias</h1>
        <p className={`mt-4 ${estilos.erro}`}>Só Dono, Gerente e Financeiro acessam o financeiro.</p>
      </div>
    )

  const recarregar = () => setVersao((v) => v + 1)
  async function alternar(c: Categoria) {
    setErro(null)
    try {
      await enviar('POST', `/api/financeiro/categorias/${c.id}/${c.ativa ? 'desativar' : 'reativar'}`)
      recarregar()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível alterar.')
    }
  }

  const grupos = (tipo: Categoria['tipo']) => {
    const lista = categorias?.filter((c) => c.tipo === tipo) ?? []
    return [...new Set(lista.map((c) => c.grupo))].map((g) => ({ grupo: g, itens: lista.filter((c) => c.grupo === g) }))
  }

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Financeiro</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Categorias</h1>
          <p className="mt-1 text-sm text-muted">Organize receitas e despesas em grupos (ex.: Ocupação › Aluguel). Valem para todas as empresas.</p>
        </div>
        <button type="button" onClick={() => void post('/api/financeiro/categorias/padrao').then(recarregar)} className={estilos.botao}>
          Adicionar categorias padrão
        </button>
        <button type="button" onClick={() => setEdicao('nova')} className={estilos.botaoPrimario}>Nova categoria</button>
      </div>
      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {categorias && categorias.length > 0 && (
        <div className="mt-6 grid gap-6 md:grid-cols-2">
          {(['Receita', 'Despesa'] as const).map((tipo) => (
            <section key={tipo} aria-label={tipo === 'Receita' ? 'Receitas' : 'Despesas'} className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
              <h2 className={`font-semibold ${tipo === 'Receita' ? 'text-success' : 'text-danger'}`}>{tipo === 'Receita' ? 'Receitas' : 'Despesas'}</h2>
              {grupos(tipo).map((g) => (
                <div key={g.grupo} className="mt-3">
                  <p className="text-xs font-semibold text-muted uppercase">{g.grupo}</p>
                  <ul className="mt-1 divide-y divide-border">
                    {g.itens.map((c) => (
                      <li key={c.id} className="flex items-center gap-2 py-1.5 text-sm">
                        <span className={`flex-1 ${c.ativa ? '' : 'text-muted line-through'}`}>{c.nome}</span>
                        <button type="button" onClick={() => setEdicao(c)} className="text-xs text-primary hover:underline" aria-label={`Editar ${c.nome}`}>Editar</button>
                        <button type="button" onClick={() => void alternar(c)} className="text-xs text-muted hover:underline">{c.ativa ? 'Desativar' : 'Reativar'}</button>
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </section>
          ))}
        </div>
      )}

      {edicao && (
        <FormularioDeCategoria
          categoria={edicao === 'nova' ? null : edicao}
          onFechar={() => setEdicao(null)}
          onSalvo={() => {
            setEdicao(null)
            recarregar()
          }}
        />
      )}
    </div>
  )
}

function FormularioDeCategoria({ categoria, onFechar, onSalvo }: { categoria: Categoria | null; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [nome, setNome] = useState(categoria?.nome ?? '')
  const [grupo, setGrupo] = useState(categoria?.grupo ?? '')
  const [tipo, setTipo] = useState<Categoria['tipo']>(categoria?.tipo ?? 'Despesa')
  const [erro, setErro] = useState<string | null>(null)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    try {
      await enviar(categoria ? 'PUT' : 'POST', categoria ? `/api/financeiro/categorias/${categoria.id}` : '/api/financeiro/categorias', { nome, grupo, tipo })
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.')
    }
  }

  return (
    <Dialogo titulo={categoria ? 'Editar categoria' : 'Nova categoria'} onFechar={onFechar}>
      <form onSubmit={salvar} className="space-y-4">
        <Campo rotulo="Tipo">
          <select value={tipo} onChange={(e) => setTipo(e.target.value as Categoria['tipo'])} className={estilos.campo}>
            <option value="Despesa">Despesa</option>
            <option value="Receita">Receita</option>
          </select>
        </Campo>
        <Campo rotulo="Grupo">
          <input value={grupo} onChange={(e) => setGrupo(e.target.value)} maxLength={60} placeholder="Ex.: Ocupação" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Nome">
          <input value={nome} onChange={(e) => setNome(e.target.value)} maxLength={60} placeholder="Ex.: Aluguel" className={estilos.campo} />
        </Campo>
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" className={estilos.botaoPrimario}>Salvar</button>
        </div>
      </form>
    </Dialogo>
  )
}
