import { useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { dataBr, hojeIso, somarDias } from '../financeiro/tipos'
import { inicioDoMes } from '../vendas/tipos'
import { BotaoExportar } from './BotaoExportar'

interface Parametro {
  nome: string
  rotulo: string
  tipo: 'Periodo' | 'Data' | 'Inteiro' | 'Opcao'
  padrao: string | null
  opcoes: { valor: string; rotulo: string }[] | null
}

interface ItemDoCatalogo {
  codigo: string
  titulo: string
  area: string
  descricao: string | null
  parametros: Parametro[]
}

type TipoColuna = 'Texto' | 'Inteiro' | 'Numero' | 'Moeda' | 'Data' | 'DataHora'

interface Dados {
  titulo: string
  colunas: { titulo: string; tipo: TipoColuna; totalizar: boolean }[]
  linhas: (string | number | null)[][]
  filtros: { nome: string; valor: string }[]
  totalDeLinhas: number
}

const ORDEM_DAS_AREAS = ['Vendas', 'Estoque', 'Financeiro', 'Gestão', 'Cadastros', 'Sistema']

/** Valores iniciais dos filtros (o período vira "de" e "ate"). */
function valoresPadrao(r: ItemDoCatalogo): Record<string, string> {
  const hoje = hojeIso()
  const valores: Record<string, string> = {}
  for (const p of r.parametros) {
    if (p.tipo === 'Periodo') {
      const [de, ate] = p.padrao === 'hoje' ? [hoje, hoje] : p.padrao === 'proximos30' ? [hoje, somarDias(hoje, 30)] : [inicioDoMes(hoje), hoje]
      valores.de = de
      valores.ate = ate
    } else valores[p.nome] = p.padrao ?? ''
  }
  return valores
}

const numero = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 })

function formatar(valor: string | number | null, tipo: TipoColuna) {
  if (valor === null || valor === '') return '—'
  switch (tipo) {
    case 'Moeda':
      return moeda(Number(valor))
    case 'Numero':
    case 'Inteiro':
      return numero.format(Number(valor))
    case 'Data':
      return dataBr(String(valor).slice(0, 10))
    case 'DataHora':
      return new Date(String(valor)).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' })
    default:
      return String(valor)
  }
}

const alinhadoADireita = (tipo: TipoColuna) => tipo === 'Moeda' || tipo === 'Numero' || tipo === 'Inteiro'

/** Central de relatórios: todos os relatórios do perfil por área, com filtros, visualização na tela e exportação. */
export function CentralDeRelatoriosPage() {
  const { getJson } = useApi()
  const { selecao } = useEmpresa()
  const [{ catalogo, erro }, setCatalogo] = useState<{ catalogo: ItemDoCatalogo[] | null; erro: string | null }>({ catalogo: null, erro: null })
  const [escolhido, setEscolhido] = useState<ItemDoCatalogo | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<ItemDoCatalogo[]>('/api/relatorios', controle.signal)
      .then((c) => setCatalogo({ catalogo: c, erro: null }))
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setCatalogo({ catalogo: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar os relatórios.' })
      })
    return () => controle.abort()
  }, [getJson, selecao])

  const areas = catalogo
    ? [...new Set(catalogo.map((r) => r.area))].sort((a, b) => (ORDEM_DAS_AREAS.indexOf(a) + 99) % 99 - (ORDEM_DAS_AREAS.indexOf(b) + 99) % 99 || a.localeCompare(b))
    : []

  return (
    <div className="mx-auto max-w-7xl">
      <h1 className="text-2xl font-bold tracking-tight">Relatórios</h1>
      <p className="mt-1 text-sm text-muted">Escolha um relatório, ajuste os filtros e veja na tela ou exporte em PDF, Excel ou CSV.</p>

      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}
      {catalogo?.length === 0 && <p className="mt-6 text-sm text-muted">Nenhum relatório disponível para o seu perfil nesta empresa.</p>}

      {catalogo && catalogo.length > 0 && (
        <div className="mt-6 grid gap-6 lg:grid-cols-[18rem_minmax(0,1fr)]">
          <nav className="space-y-5">
            {areas.map((area) => (
              <section key={area} aria-label={area}>
                <h2 className="mb-2 text-xs font-semibold text-muted uppercase">{area}</h2>
                <ul className="space-y-1">
                  {catalogo.filter((r) => r.area === area).map((r) => (
                    <li key={r.codigo}>
                      <button
                        type="button"
                        onClick={() => setEscolhido(r)}
                        aria-current={escolhido?.codigo === r.codigo}
                        className={`w-full rounded-lg px-3 py-2 text-left text-sm ${escolhido?.codigo === r.codigo ? 'bg-primary-soft text-primary' : 'hover:bg-surface-2'}`}
                      >
                        <span className="block font-semibold">{r.titulo}</span>
                        {r.descricao && <span className="block text-xs text-muted">{r.descricao}</span>}
                      </button>
                    </li>
                  ))}
                </ul>
              </section>
            ))}
          </nav>

          <div>
            {escolhido ? (
              <Relatorio key={escolhido.codigo} relatorio={escolhido} />
            ) : (
              <p className="rounded-2xl border border-dashed border-border p-8 text-center text-sm text-muted">Escolha um relatório na lista.</p>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

function Relatorio({ relatorio }: { relatorio: ItemDoCatalogo }) {
  const { getJson } = useApi()
  const [valores, setValores] = useState(() => valoresPadrao(relatorio))
  const [dados, setDados] = useState<Dados | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [carregando, setCarregando] = useState(false)
  const filtros = Object.fromEntries(Object.entries(valores).filter(([, v]) => v !== ''))
  const mudar = (nome: string, valor: string) => setValores((v) => ({ ...v, [nome]: valor }))

  async function visualizar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    setCarregando(true)
    try {
      const query = new URLSearchParams(filtros)
      setDados(await getJson<Dados>(`/api/relatorios/${relatorio.codigo}/dados${query.size ? `?${query}` : ''}`))
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível gerar o relatório.')
    } finally {
      setCarregando(false)
    }
  }

  const totais = dados?.colunas.map((c, i) => (c.totalizar ? dados.linhas.reduce((s, l) => s + Number(l[i] ?? 0), 0) : null))

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-start gap-3">
        <div className="flex-1">
          <h2 className="text-lg font-bold">{relatorio.titulo}</h2>
          {relatorio.descricao && <p className="text-sm text-muted">{relatorio.descricao}</p>}
        </div>
        <BotaoExportar relatorio={relatorio.codigo} parametros={filtros} />
      </div>

      <form aria-label={`Filtros de ${relatorio.titulo}`} onSubmit={visualizar} className="flex flex-wrap items-end gap-3 rounded-2xl border border-border bg-surface p-4 shadow-sm">
        {relatorio.parametros.map((p) =>
          p.tipo === 'Periodo' ? (
            <div key={p.nome} className="flex gap-3">
              <Campo rotulo="De" className="w-40">
                <input type="date" value={valores.de ?? ''} onChange={(e) => mudar('de', e.target.value)} className={estilos.campo} />
              </Campo>
              <Campo rotulo="Até" className="w-40">
                <input type="date" value={valores.ate ?? ''} onChange={(e) => mudar('ate', e.target.value)} className={estilos.campo} />
              </Campo>
            </div>
          ) : p.tipo === 'Opcao' ? (
            <Campo key={p.nome} rotulo={p.rotulo} className="w-52">
              <select value={valores[p.nome] ?? ''} onChange={(e) => mudar(p.nome, e.target.value)} className={estilos.campo}>
                {(p.opcoes ?? []).map((o) => (
                  <option key={o.valor} value={o.valor}>{o.rotulo}</option>
                ))}
              </select>
            </Campo>
          ) : (
            <Campo key={p.nome} rotulo={p.rotulo} className="w-60">
              <input
                type={p.tipo === 'Data' ? 'date' : 'text'}
                inputMode={p.tipo === 'Inteiro' ? 'numeric' : undefined}
                value={valores[p.nome] ?? ''}
                onChange={(e) => mudar(p.nome, p.tipo === 'Inteiro' ? e.target.value.replace(/\D/g, '') : e.target.value)}
                className={estilos.campo}
              />
            </Campo>
          ),
        )}
        <button type="submit" disabled={carregando} className={estilos.botaoPrimario}>{carregando ? 'Gerando...' : 'Visualizar'}</button>
      </form>

      {erro && <p role="alert" className={estilos.erro}>{erro}</p>}

      {dados && (
        <div className="space-y-2">
          {dados.filtros.length > 0 && <p className="text-xs text-muted">{dados.filtros.map((f) => `${f.nome}: ${f.valor}`).join(' · ')}</p>}
          {dados.totalDeLinhas > dados.linhas.length && (
            <p className="text-sm text-warning">{`Mostrando ${dados.linhas.length} de ${dados.totalDeLinhas} linhas. Exporte para ver todas.`}</p>
          )}
          <div className="overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
            {dados.linhas.length === 0 ? (
              <p className="p-8 text-center text-sm text-muted">Nada encontrado com esses filtros.</p>
            ) : (
              <table aria-label={dados.titulo} className="w-full text-sm">
                <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                  <tr>
                    {dados.colunas.map((c) => (
                      <th key={c.titulo} className={`px-3 py-2 ${alinhadoADireita(c.tipo) ? 'text-right' : ''}`}>{c.titulo}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {dados.linhas.map((l, i) => (
                    <tr key={i}>
                      {l.map((v, j) => (
                        <td key={j} className={`px-3 py-2 ${alinhadoADireita(dados.colunas[j]!.tipo) ? 'text-right tabular-nums' : ''}`}>{formatar(v, dados.colunas[j]!.tipo)}</td>
                      ))}
                    </tr>
                  ))}
                </tbody>
                {totais?.some((t) => t !== null) && (
                  <tfoot className="border-t border-border bg-surface-2 font-semibold">
                    <tr>
                      {dados.colunas.map((c, j) => (
                        <td key={c.titulo} className={`px-3 py-2 ${alinhadoADireita(c.tipo) ? 'text-right tabular-nums' : ''}`}>
                          {j === 0 ? 'Total' : totais[j] === null ? '' : formatar(Math.round(totais[j]! * 100) / 100, c.tipo)}
                        </td>
                      ))}
                    </tr>
                  </tfoot>
                )}
              </table>
            )}
          </div>
        </div>
      )}
    </section>
  )
}
