import { useEffect, useState } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'

interface Alteracao {
  campo: string
  antes: string | null
  depois: string | null
}

interface Registro {
  id: string
  ocorridoEm: string
  tipo: 'Inclusao' | 'Alteracao' | 'Exclusao' | 'Evento'
  evento: string | null
  entidade: string | null
  descricao: string
  usuarioNome: string
  ip: string | null
  alteracoes: Alteracao[]
}

interface Pagina {
  itens: Registro[]
  total: number
}

const TIPOS = [
  { valor: '', rotulo: 'Todos' },
  { valor: 'Inclusao', rotulo: 'Inclusões' },
  { valor: 'Alteracao', rotulo: 'Alterações' },
  { valor: 'Exclusao', rotulo: 'Exclusões' },
  { valor: 'Evento', rotulo: 'Eventos (login, segurança)' },
]

const ESTILO_TIPO: Record<Registro['tipo'], { rotulo: string; classe: string }> = {
  Inclusao: { rotulo: 'Inclusão', classe: 'bg-success-soft text-success' },
  Alteracao: { rotulo: 'Alteração', classe: 'bg-info-soft text-info' },
  Exclusao: { rotulo: 'Exclusão', classe: 'bg-danger-soft text-danger' },
  Evento: { rotulo: 'Evento', classe: 'bg-surface-2 text-muted' },
}

const formatarData = (iso: string) =>
  new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(iso))

export function HistoricoPage() {
  const { getJson } = useApi()
  const { selecao } = useEmpresa()
  const [tipo, setTipo] = useState('')
  const [{ pagina, erro }, setResultado] = useState<{ pagina: Pagina | null; erro: string | null }>({ pagina: null, erro: null })
  const [abertos, setAbertos] = useState<Set<string>>(new Set())

  useEffect(() => {
    const controle = new AbortController()
    const params = new URLSearchParams({ pagina: '1', tamanho: '50' })
    if (tipo) params.set('tipo', tipo)
    getJson<Pagina>(`/api/auditoria?${params}`, controle.signal)
      .then((resposta) => setResultado({ pagina: resposta, erro: null }))
      .catch((e: unknown) => {
        if (controle.signal.aborted) return
        setResultado({ pagina: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar o histórico.' })
      })
    return () => controle.abort()
  }, [getJson, tipo, selecao])

  const alternar = (id: string) =>
    setAbertos((atual) => {
      const novo = new Set(atual)
      if (novo.has(id)) novo.delete(id)
      else novo.add(id)
      return novo
    })

  return (
    <div className="mx-auto max-w-6xl">
      <p className="text-xs text-muted">Configurações</p>
      <h1 className="text-2xl font-bold tracking-tight">Histórico de alterações</h1>
      <p className="mt-1 text-sm text-muted">
        Quem incluiu, alterou ou excluiu registros, além de logins e eventos de segurança. O histórico não pode ser editado nem apagado.
      </p>

      <div className="mt-6 flex flex-wrap items-end gap-3">
        <label className="text-sm">
          <span className="mb-1 block font-semibold">Tipo</span>
          <select
            value={tipo}
            onChange={(e) => setTipo(e.target.value)}
            className="h-9 rounded-lg border border-border bg-surface px-2.5 text-sm"
          >
            {TIPOS.map((t) => (
              <option key={t.valor} value={t.valor}>
                {t.rotulo}
              </option>
            ))}
          </select>
        </label>
        {pagina && (
          <span className="ml-auto text-sm text-muted">
            {pagina.total} {pagina.total === 1 ? 'registro' : 'registros'}
          </span>
        )}
      </div>

      {erro && (
        <p role="alert" className="mt-5 flex items-center gap-2 rounded-lg bg-danger-soft px-4 py-3 text-sm text-danger">
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {pagina && (
        <section className="mt-4 rounded-2xl border border-border bg-surface shadow-sm">
          {pagina.itens.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted">Nenhum registro encontrado.</p>
          ) : (
            <ul className="divide-y divide-border">
              {pagina.itens.map((r) => (
                <li key={r.id} className="px-4 py-3">
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
                    <span className={`w-20 rounded-full px-2 py-0.5 text-center text-xs font-semibold ${ESTILO_TIPO[r.tipo].classe}`}>
                      {ESTILO_TIPO[r.tipo].rotulo}
                    </span>
                    <span className="min-w-56 flex-1 text-sm font-medium">{r.descricao}</span>
                    <span className="text-sm text-muted">{r.usuarioNome}</span>
                    <span className="w-40 text-sm text-muted tabular-nums">{formatarData(r.ocorridoEm)}</span>
                    {r.alteracoes.length > 0 && (
                      <button
                        type="button"
                        onClick={() => alternar(r.id)}
                        aria-expanded={abertos.has(r.id)}
                        className="text-sm font-semibold text-primary hover:underline"
                      >
                        {abertos.has(r.id) ? 'Ocultar' : 'Ver detalhes'}
                      </button>
                    )}
                  </div>
                  {abertos.has(r.id) && (
                    <table className="mt-3 w-full overflow-hidden rounded-lg border border-border text-sm">
                      <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                        <tr>
                          <th className="px-3 py-2">Campo</th>
                          <th className="px-3 py-2">Antes</th>
                          <th className="px-3 py-2">Depois</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-border">
                        {r.alteracoes.map((a) => (
                          <tr key={a.campo}>
                            <td className="px-3 py-2 font-medium">{a.campo}</td>
                            <td className="px-3 py-2 text-muted">{a.antes ?? '—'}</td>
                            <td className="px-3 py-2">{a.depois ?? '—'}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                  {r.ip && <p className="mt-1 text-xs text-muted">IP {r.ip}</p>}
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  )
}
