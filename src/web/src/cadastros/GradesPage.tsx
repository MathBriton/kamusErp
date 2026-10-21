import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade } from '../components/grade/useGrade'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { Amostra } from './Amostra'
import { PERFIS_DO_CATALOGO, type Cor, type Grade } from './catalogo'

type Edicao = { tipo: 'grade'; grade: Grade | null } | { tipo: 'cor'; cor: Cor | null } | null

const FILTROS = { busca: '' }

/** Normaliza para a busca local: sem acento e sem caixa. */
const normalizar = (texto: string) => texto.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim()

/** Página local de uma lista pequena (grades e cores cabem inteiras na memória): busca, recorte e total. */
function pagina<T>(itens: T[] | null, termo: string, texto: (item: T) => string, estado: { pagina: number; tamanho: number }) {
  if (!itens) return { itens: null, total: 0 }
  const busca = normalizar(termo)
  const filtrados = busca ? itens.filter((i) => normalizar(texto(i)).includes(busca)) : itens
  const inicio = (estado.pagina - 1) * estado.tamanho
  return { itens: filtrados.slice(inicio, inicio + estado.tamanho), total: filtrados.length }
}

/**
 * Grades e cores no padrão das telas de cadastro: busca, grade com seleção, total e paginação, editar por linha e exportar.
 * Não há inativação no domínio (grade em uso fica travada), então não há ações em lote além de exportar os selecionados.
 */
export function GradesPage() {
  const { getJson, enviar } = useApi()
  const { perfilAtual } = useEmpresa()
  const podeEditar = PERFIS_DO_CATALOGO.includes(perfilAtual)
  const [grades, setGrades] = useState<Grade[] | null>(null)
  const [cores, setCores] = useState<Cor[] | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [edicao, setEdicao] = useState<Edicao>(null)
  const gradeDeGrades = useGrade(FILTROS)
  const gradeDeCores = useGrade(FILTROS)
  const busca = gradeDeGrades.filtros.busca

  const carregar = useCallback(async () => {
    try {
      const [g, c] = await Promise.all([getJson<Grade[]>('/api/grades'), getJson<Cor[]>('/api/cores')])
      setGrades(g)
      setCores(c)
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível carregar.')
    }
  }, [getJson])

  useEffect(() => {
    const timer = window.setTimeout(() => void carregar(), 0)
    return () => window.clearTimeout(timer)
  }, [carregar])

  async function padroes(caminho: string) {
    setErro(null)
    try {
      await enviar('POST', caminho)
      await carregar()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível concluir.')
    }
  }

  const salvo = () => {
    setEdicao(null)
    void carregar()
  }

  const paginaDeGrades = pagina(grades, busca, (g) => `${g.nome} ${g.tamanhos.join(' ')}`, gradeDeGrades.paginacao)
  const paginaDeCores = pagina(cores, busca, (c) => c.nome, gradeDeCores.paginacao)

  const colunasDeGrades: Coluna<Grade>[] = [
    { titulo: 'Grade', celula: (g) => <span className="font-semibold">{g.nome}</span> },
    { titulo: 'Tamanhos', celula: (g) => <span className="text-muted">{g.tamanhos.join(' · ')}</span> },
    {
      titulo: 'Situação',
      celula: (g) => (
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${g.emUso ? 'bg-surface-2 text-muted' : 'bg-success-soft text-success'}`}>
          {g.emUso ? 'Em uso' : 'Livre'}
        </span>
      ),
    },
  ]
  const colunasDeCores: Coluna<Cor>[] = [
    {
      titulo: 'Cor',
      celula: (c) => (
        <span className="flex items-center gap-2 font-semibold">
          <Amostra hex={c.hex} />
          {c.nome}
        </span>
      ),
    },
    { titulo: 'Amostra', celula: (c) => <span className="font-mono text-xs text-muted">{c.hex ?? '—'}</span> },
  ]

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Cadastros"
        titulo="Grades e cores"
        descricao="Grades de tamanho reutilizáveis (a ordem é a da etiqueta: PP antes de M) e as cores dos produtos."
      />

      <BarraDeFiltros
        ativos={gradeDeGrades.ativos}
        onLimpar={() => {
          gradeDeGrades.limpar()
          gradeDeCores.limpar()
        }}
      >
        <label className="min-w-60 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input
            value={busca}
            onChange={(e) => {
              gradeDeGrades.filtrar({ busca: e.target.value })
              gradeDeCores.filtrar({ busca: e.target.value })
            }}
            placeholder="Nome da grade, tamanho ou cor"
            className={estilos.campo}
          />
        </label>
      </BarraDeFiltros>

      {erro && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      <div className="mt-6 grid gap-6 xl:grid-cols-2">
        <Secao
          titulo="Grades de tamanho"
          acoes={
            <>
              <BotaoExportar relatorio="grades" parametros={gradeDeGrades.parametros} selecionados={[...gradeDeGrades.selecionados]} />
              {podeEditar && (
                <>
                  <button type="button" onClick={() => void padroes('/api/grades/padrao')} className={estilos.botao}>Adicionar grades padrão</button>
                  <button type="button" onClick={() => setEdicao({ tipo: 'grade', grade: null })} className={estilos.botaoPrimario}>
                    <Icone nome="plus" />
                    Nova grade
                  </button>
                </>
              )}
            </>
          }
        >
          <GradeDeRegistros
            rotulo="Grades de tamanho"
            colunas={colunasDeGrades}
            itens={paginaDeGrades.itens ?? (erro ? [] : null)}
            chave={(g) => g.id}
            nome={(g) => g.nome}
            total={paginaDeGrades.total}
            {...gradeDeGrades.paginacao}
            selecao={gradeDeGrades.selecao}
            vazio={busca ? 'Nenhuma grade encontrada.' : 'Nenhuma grade ainda. Comece pelas padrão (PP ao GG, 34 ao 48, infantil, único...).'}
            acoes={podeEditar ? (g) => !g.emUso && <AcaoDaLinha rotulo={`Editar ${g.nome}`} icone="pencil" onClick={() => setEdicao({ tipo: 'grade', grade: g })} /> : undefined}
          />
          <p className="mt-2 px-1 text-xs text-muted">Grade em uso (com produtos) não muda: crie uma grade nova.</p>
        </Secao>

        <Secao
          titulo="Cores"
          acoes={
            <>
              <BotaoExportar relatorio="cores" parametros={gradeDeCores.parametros} selecionados={[...gradeDeCores.selecionados]} />
              {podeEditar && (
                <>
                  <button type="button" onClick={() => void padroes('/api/cores/padrao')} className={estilos.botao}>Adicionar cores padrão</button>
                  <button type="button" onClick={() => setEdicao({ tipo: 'cor', cor: null })} className={estilos.botaoPrimario}>
                    <Icone nome="plus" />
                    Nova cor
                  </button>
                </>
              )}
            </>
          }
        >
          <GradeDeRegistros
            rotulo="Cores"
            colunas={colunasDeCores}
            itens={paginaDeCores.itens ?? (erro ? [] : null)}
            chave={(c) => c.id}
            nome={(c) => c.nome}
            total={paginaDeCores.total}
            {...gradeDeCores.paginacao}
            selecao={gradeDeCores.selecao}
            vazio={busca ? 'Nenhuma cor encontrada.' : 'Nenhuma cor ainda.'}
            acoes={podeEditar ? (c) => <AcaoDaLinha rotulo={`Editar ${c.nome}`} icone="pencil" onClick={() => setEdicao({ tipo: 'cor', cor: c })} /> : undefined}
          />
        </Secao>
      </div>

      {edicao?.tipo === 'grade' && <FormularioDeGrade grade={edicao.grade} onFechar={() => setEdicao(null)} onSalvo={salvo} />}
      {edicao?.tipo === 'cor' && <FormularioDeCor cor={edicao.cor} onFechar={() => setEdicao(null)} onSalvo={salvo} />}
    </div>
  )
}

function Secao({ titulo, acoes, children }: { titulo: string; acoes: ReactNode; children: ReactNode }) {
  return (
    <section aria-label={titulo} className="min-w-0">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="flex-1 font-semibold">{titulo}</h2>
        {acoes}
      </div>
      {children}
    </section>
  )
}

function FormularioDeGrade({ grade, onFechar, onSalvo }: { grade: Grade | null; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [nome, setNome] = useState(grade?.nome ?? '')
  const [tamanhos, setTamanhos] = useState(grade?.tamanhos.join(', ') ?? '')
  const [erro, setErro] = useState<string | null>(null)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    const corpo = { nome, tamanhos: tamanhos.split(',').map((t) => t.trim()).filter(Boolean) }
    try {
      if (grade) await enviar('PUT', `/api/grades/${grade.id}`, corpo)
      else await enviar('POST', '/api/grades', corpo)
      onSalvo()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível salvar.')
    }
  }

  return (
    <Dialogo titulo={grade ? 'Editar grade' : 'Nova grade'} onFechar={onFechar}>
      <form onSubmit={(e) => void salvar(e)} className="space-y-4">
        <Campo rotulo="Nome">
          <input value={nome} onChange={(e) => setNome(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Tamanhos (na ordem, separados por vírgula)">
          <input value={tamanhos} onChange={(e) => setTamanhos(e.target.value)} placeholder="PP, P, M, G, GG" className={estilos.campo} />
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

function FormularioDeCor({ cor, onFechar, onSalvo }: { cor: Cor | null; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [nome, setNome] = useState(cor?.nome ?? '')
  const [hex, setHex] = useState(cor?.hex ?? '#888888')
  const [erro, setErro] = useState<string | null>(null)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    try {
      if (cor) await enviar('PUT', `/api/cores/${cor.id}`, { nome, hex })
      else await enviar('POST', '/api/cores', { nome, hex })
      onSalvo()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível salvar.')
    }
  }

  return (
    <Dialogo titulo={cor ? 'Editar cor' : 'Nova cor'} onFechar={onFechar}>
      <form onSubmit={(e) => void salvar(e)} className="space-y-4">
        <Campo rotulo="Nome">
          <input value={nome} onChange={(e) => setNome(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Amostra">
          <input type="color" value={hex} onChange={(e) => setHex(e.target.value)} className="h-10 w-20 rounded-lg border border-border bg-surface" />
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
