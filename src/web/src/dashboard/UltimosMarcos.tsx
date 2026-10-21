import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'

interface Registro {
  id: string
  ocorridoEm: string
  tipo: string
  evento: string | null
  entidade: string | null
  descricao: string
  usuarioNome: string
}

/** Eventos técnicos que não são marcos de operação. */
const IGNORADOS = new Set(['login', 'login_falhou', 'logout', 'exportacao'])
/** Registros "filhos" que acompanham outro (o título já conta; a parcela não precisa aparecer). */
const ENTIDADES_INTERNAS = /^(Parcela|Baixa|Sku|Item|Pagamento|Distribuicao|AcessoEmpresa|Saldo|Movimento)/

type Marco = Registro & { vezes: number }

/** Tira o ruído e junta repetições seguidas (mesma descrição e mesmo usuário) em uma linha "(3×)". */
function marcosDe(registros: Registro[], quantos = 10): Marco[] {
  const marcos: Marco[] = []
  for (const r of registros) {
    if (IGNORADOS.has(r.evento ?? '') || ENTIDADES_INTERNAS.test(r.entidade ?? '')) continue
    const anterior = marcos.at(-1)
    if (anterior && anterior.descricao === r.descricao && anterior.usuarioNome === r.usuarioNome) anterior.vezes++
    else marcos.push({ ...r, vezes: 1 })
  }
  return marcos.slice(0, quantos)
}

const ETIQUETAS: { padrao: RegExp; rotulo: string; tom: string }[] = [
  { padrao: /comiss/i, rotulo: 'COMISSÕES', tom: 'bg-accent-soft text-accent' },
  { padrao: /produto|sku|grade|^cor$/i, rotulo: 'CATÁLOGO', tom: 'bg-primary-soft text-primary' },
  { padrao: /estoque|movimento|saldo|loja|nota|invent|ajuste|transfer|entrada/i, rotulo: 'ESTOQUE', tom: 'bg-success-soft text-success' },
  { padrao: /titulo|parcela|baixa|conta|categoria|credito|crediario/i, rotulo: 'FINANCEIRO', tom: 'bg-info-soft text-info' },
  { padrao: /venda|troca|vale|condicional|forma/i, rotulo: 'VENDAS', tom: 'bg-warning-soft text-warning' },
  { padrao: /pessoa|vendedor|cliente|fornecedor/i, rotulo: 'CADASTROS', tom: 'bg-info-soft text-info' },
  { padrao: /usuario|empresa|acesso|senha|licen/i, rotulo: 'GESTÃO', tom: 'bg-surface-2 text-muted' },
]

function etiqueta(r: Registro) {
  const chave = r.evento ?? r.entidade ?? ''
  return ETIQUETAS.find((e) => e.padrao.test(chave)) ?? { rotulo: 'SISTEMA', tom: 'bg-surface-2 text-muted' }
}

function quando(iso: string) {
  const minutos = Math.round((Date.now() - new Date(iso).getTime()) / 60_000)
  if (minutos < 1) return 'agora'
  if (minutos < 60) return `há ${minutos} min`
  if (minutos < 24 * 60) return `há ${Math.round(minutos / 60)} h`
  return new Date(iso).toLocaleDateString('pt-BR', { timeZone: 'America/Sao_Paulo', day: '2-digit', month: '2-digit' })
}

/** Os 10 últimos marcos de operação (histórico de alterações sem logins/exportações). Só Dono e Gerente. */
export function UltimosMarcos({ versao }: { versao: number }) {
  const { getJson } = useApi()
  const [registros, setRegistros] = useState<Marco[] | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<{ itens: Registro[] }>('/api/auditoria?pagina=1&tamanho=60', controle.signal)
      .then((r) => setRegistros(marcosDe(r.itens)))
      .catch(() => {
        if (!controle.signal.aborted) setRegistros([])
      })
    return () => controle.abort()
  }, [getJson, versao])

  return (
    <section aria-label="Últimos marcos e atualizações" className="rounded-2xl border border-border bg-surface p-6 shadow-sm">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="text-lg font-semibold">Últimos marcos e atualizações</h2>
        <Link to="/configuracoes/historico" className="text-sm font-semibold text-primary hover:underline">Ver histórico completo →</Link>
      </div>
      {registros === null ? (
        <p className="mt-4 text-sm text-muted">Carregando...</p>
      ) : registros.length === 0 ? (
        <p className="mt-4 text-sm text-muted">Nada registrado ainda. Cadastros, entradas, vendas e lançamentos aparecem aqui.</p>
      ) : (
        <ol className="mt-4 space-y-0">
          {registros.map((r, i) => {
            const e = etiqueta(r)
            return (
              <li key={r.id} className="relative flex gap-3 pb-4 last:pb-0">
                {i < registros.length - 1 && <span aria-hidden className="absolute top-3 bottom-0 left-[5px] w-px bg-border" />}
                <span aria-hidden className="relative mt-1.5 size-[11px] shrink-0 rounded-full border-2 border-primary bg-surface" />
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold tracking-wider ${e.tom}`}>{e.rotulo}</span>
                    <span className="text-sm font-semibold">{r.vezes > 1 ? `${r.descricao} (${r.vezes}×)` : r.descricao}</span>
                  </div>
                  <p className="mt-0.5 text-xs text-muted">{`${r.usuarioNome} · ${quando(r.ocorridoEm)}`}</p>
                </div>
              </li>
            )
          })}
        </ol>
      )}
    </section>
  )
}
