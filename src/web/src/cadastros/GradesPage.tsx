import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { Amostra } from './Amostra'
import type { Cor, Grade } from './catalogo'

type Edicao = { tipo: 'grade'; grade: Grade | null } | { tipo: 'cor'; cor: Cor | null } | null

export function GradesPage() {
  const { getJson, enviar } = useApi()
  const [grades, setGrades] = useState<Grade[] | null>(null)
  const [cores, setCores] = useState<Cor[] | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [edicao, setEdicao] = useState<Edicao>(null)

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

  return (
    <div className="mx-auto max-w-6xl">
      <p className="text-xs text-muted">Cadastros</p>
      <h1 className="text-2xl font-bold tracking-tight">Grades e cores</h1>
      <p className="mt-1 text-sm text-muted">Grades de tamanho reutilizáveis (a ordem é a da etiqueta: PP antes de M) e as cores dos produtos.</p>

      {erro && (
        <p role="alert" className={`mt-5 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      <div className="mt-6 grid gap-5 lg:grid-cols-2">
        <section className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <h2 className="flex-1 font-semibold">Grades de tamanho</h2>
            <button type="button" onClick={() => void padroes('/api/grades/padrao')} className={estilos.botao}>
              Adicionar grades padrão
            </button>
            <button type="button" onClick={() => setEdicao({ tipo: 'grade', grade: null })} className={estilos.botaoPrimario}>
              Nova grade
            </button>
          </div>
          {grades?.length === 0 && <p className="text-sm text-muted">Nenhuma grade ainda. Comece pelas padrão (PP ao GG, 34 ao 48, infantil, único...).</p>}
          <ul className="divide-y divide-border">
            {grades?.map((g) => (
              <li key={g.id} className="flex items-center gap-3 py-2.5">
                <div className="flex-1">
                  <p className="font-semibold">{g.nome}</p>
                  <p className="text-sm text-muted">{g.tamanhos.join(' · ')}</p>
                </div>
                {g.emUso ? (
                  <span className="rounded-full bg-surface-2 px-2 py-0.5 text-xs font-semibold text-muted">Em uso</span>
                ) : (
                  <button type="button" onClick={() => setEdicao({ tipo: 'grade', grade: g })} className={estilos.botao}>
                    Editar
                  </button>
                )}
              </li>
            ))}
          </ul>
        </section>

        <section className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <h2 className="flex-1 font-semibold">Cores</h2>
            <button type="button" onClick={() => void padroes('/api/cores/padrao')} className={estilos.botao}>
              Adicionar cores padrão
            </button>
            <button type="button" onClick={() => setEdicao({ tipo: 'cor', cor: null })} className={estilos.botaoPrimario}>
              Nova cor
            </button>
          </div>
          {cores?.length === 0 && <p className="text-sm text-muted">Nenhuma cor ainda.</p>}
          <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            {cores?.map((c) => (
              <li key={c.id}>
                <button type="button" onClick={() => setEdicao({ tipo: 'cor', cor: c })} className="flex w-full items-center gap-2 rounded-lg border border-border px-3 py-2 text-left text-sm hover:bg-surface-2">
                  <Amostra hex={c.hex} />
                  {c.nome}
                </button>
              </li>
            ))}
          </ul>
        </section>
      </div>

      {edicao?.tipo === 'grade' && (
        <FormularioDeGrade grade={edicao.grade} onFechar={() => setEdicao(null)} onSalvo={() => { setEdicao(null); void carregar() }} />
      )}
      {edicao?.tipo === 'cor' && <FormularioDeCor cor={edicao.cor} onFechar={() => setEdicao(null)} onSalvo={() => { setEdicao(null); void carregar() }} />}
    </div>
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
