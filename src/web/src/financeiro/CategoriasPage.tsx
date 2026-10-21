import { useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { Confirmacao } from '../components/grade/Confirmacao'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade, useListaPaginada } from '../components/grade/useGrade'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { PERFIS_DO_FINANCEIRO, type Categoria } from './tipos'

type Janela =
  | { tipo: 'nova' }
  | { tipo: 'editar'; categoria: Categoria }
  | { tipo: 'mudarSituacao'; categoria: Categoria }
  | { tipo: 'mudarSelecionadas'; ativa: boolean }
  | null

const FILTROS = { situacao: 'ativas', tipo: '', busca: '' }

/** Plano de contas simples no padrão de grade: categorias de receita e despesa por grupo (base do fluxo por categoria e do DRE). */
export function CategoriasPage() {
  const { post, enviar } = useApi()
  const { perfilAtual } = useEmpresa()
  const permitido = PERFIS_DO_FINANCEIRO.includes(perfilAtual)
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { dados, erro } = useListaPaginada<{ itens: Categoria[]; total: number }>(
    permitido ? `/api/financeiro/categorias?${grade.consulta}` : null,
    versao,
    grade.filtros.busca ? 250 : 0,
  )
  const [janela, setJanela] = useState<Janela>(null)
  const [erroDoPadrao, setErroDoPadrao] = useState<string | null>(null)

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Categorias</h1>
        <p className={`mt-4 ${estilos.erro}`}>Só Dono, Gerente e Financeiro acessam o financeiro.</p>
      </div>
    )

  const recarregar = () => {
    setJanela(null)
    setVersao((v) => v + 1)
  }
  async function adicionarPadrao() {
    setErroDoPadrao(null)
    try {
      await post('/api/financeiro/categorias/padrao')
      recarregar()
    } catch (ex) {
      setErroDoPadrao(ex instanceof ErroDeApi ? ex.message : 'Não foi possível adicionar.')
    }
  }

  const colunas: Coluna<Categoria>[] = [
    { titulo: 'Tipo', celula: (c) => <span className={`font-semibold ${c.tipo === 'Receita' ? 'text-success' : 'text-danger'}`}>{c.tipo}</span> },
    { titulo: 'Grupo', celula: (c) => <span className="text-muted">{c.grupo}</span> },
    { titulo: 'Categoria', celula: (c) => <span className="font-semibold">{c.nome}</span> },
    {
      titulo: 'Situação',
      celula: (c) => (
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${c.ativa ? 'bg-success-soft text-success' : 'bg-surface-2 text-muted'}`}>
          {c.ativa ? 'Ativa' : 'Inativa'}
        </span>
      ),
    },
  ]

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Financeiro"
        titulo="Categorias"
        descricao="Organize receitas e despesas em grupos (ex.: Ocupação › Aluguel). Valem para todas as empresas."
        acoes={
          <>
            <BotaoExportar relatorio="categorias-financeiras" parametros={grade.parametros} selecionados={[...grade.selecionados]} />
            <button type="button" onClick={() => void adicionarPadrao()} className={estilos.botao}>Adicionar categorias padrão</button>
            <button type="button" onClick={() => setJanela({ tipo: 'nova' })} className={estilos.botaoPrimario}>
              <Icone nome="plus" />
              Nova categoria
            </button>
          </>
        }
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="min-w-56 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input value={grade.filtros.busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Nome ou grupo" className={estilos.campo} />
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">Situação</span>
          <select value={grade.filtros.situacao} onChange={(e) => grade.filtrar({ situacao: e.target.value })} className={estilos.campo}>
            <option value="ativas">Ativas</option>
            <option value="inativas">Inativas</option>
            <option value="todas">Todas</option>
          </select>
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">Tipo</span>
          <select value={grade.filtros.tipo} onChange={(e) => grade.filtrar({ tipo: e.target.value })} className={estilos.campo}>
            <option value="">Todos</option>
            <option value="Receita">Receitas</option>
            <option value="Despesa">Despesas</option>
          </select>
        </label>
      </BarraDeFiltros>

      {(erro || erroDoPadrao) && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro ?? erroDoPadrao}
        </p>
      )}

      <GradeDeRegistros
        rotulo="Categorias"
        colunas={colunas}
        itens={dados?.itens ?? (erro ? [] : null)}
        chave={(c) => c.id}
        nome={(c) => c.nome}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        apagada={(c) => !c.ativa}
        vazio="Nenhuma categoria com esses filtros. Use “Adicionar categorias padrão” para começar."
        acoes={(c) => (
          <>
            <AcaoDaLinha rotulo={`Editar ${c.nome}`} icone="pencil" onClick={() => setJanela({ tipo: 'editar', categoria: c })} />
            <AcaoDaLinha
              rotulo={`${c.ativa ? 'Desativar' : 'Reativar'} ${c.nome}`}
              icone="power"
              perigo={c.ativa}
              onClick={() => setJanela({ tipo: 'mudarSituacao', categoria: c })}
            />
          </>
        )}
        acoesEmLote={
          <>
            <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionadas', ativa: false })} className={estilos.botao}>Desativar selecionadas</button>
            <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionadas', ativa: true })} className={estilos.botao}>Reativar selecionadas</button>
          </>
        }
      />

      {janela?.tipo === 'nova' && <FormularioDeCategoria categoria={null} onFechar={() => setJanela(null)} onSalvo={recarregar} />}
      {janela?.tipo === 'editar' && <FormularioDeCategoria categoria={janela.categoria} onFechar={() => setJanela(null)} onSalvo={recarregar} />}
      {janela?.tipo === 'mudarSituacao' && (
        <Confirmacao
          titulo={janela.categoria.ativa ? 'Desativar categoria' : 'Reativar categoria'}
          rotulo={janela.categoria.ativa ? 'Desativar' : 'Reativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', `/api/financeiro/categorias/${janela.categoria.id}/${janela.categoria.ativa ? 'desativar' : 'reativar'}`)
            recarregar()
          }}
        >
          {janela.categoria.ativa
            ? <p>{`"${janela.categoria.nome}" deixa de aparecer nos lançamentos novos. As contas já lançadas continuam com ela.`}</p>
            : <p>{`"${janela.categoria.nome}" volta a aparecer nos lançamentos.`}</p>}
        </Confirmacao>
      )}
      {janela?.tipo === 'mudarSelecionadas' && (
        <Confirmacao
          titulo={janela.ativa ? 'Reativar categorias' : 'Desativar categorias'}
          rotulo={janela.ativa ? 'Reativar' : 'Desativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', '/api/financeiro/categorias/situacao', { ids: [...grade.selecionados], ativo: janela.ativa })
            grade.limparSelecao()
            recarregar()
          }}
        >
          <p>{`${janela.ativa ? 'Reativar' : 'Desativar'} ${grade.selecionados.size === 1 ? 'a categoria selecionada' : `as ${grade.selecionados.size} categorias selecionadas`}?`}</p>
        </Confirmacao>
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
