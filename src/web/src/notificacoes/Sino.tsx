import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { useApi } from '../api/useApi'
import { useAuth } from '../auth/AuthContext'
import { Icone, type NomeIcone } from '../components/Icone'
import { useFecharAoClicarFora } from '../components/useFecharAoClicarFora'

export interface Notificacao {
  id: string
  tipo: string
  titulo: string
  mensagem: string
  link: string | null
  criadaEm: string
  lidaEm: string | null
}

interface Estado {
  itens: Notificacao[]
  naoLidas: number
}

const ICONE_POR_TIPO: Record<string, { icone: NomeIcone; tom: string }> = {
  usuario_bloqueado: { icone: 'lock', tom: 'bg-danger-soft text-danger' },
  sessao_revogada: { icone: 'shield', tom: 'bg-danger-soft text-danger' },
  historico: { icone: 'clock', tom: 'bg-info-soft text-info' },
  primeiros_passos: { icone: 'shirt', tom: 'bg-primary-soft text-primary' },
}
const PADRAO = { icone: 'bell' as NomeIcone, tom: 'bg-surface-2 text-muted' }

const relativo = new Intl.RelativeTimeFormat('pt-BR', { numeric: 'auto' })
function haQuantoTempo(iso: string) {
  const minutos = Math.round((new Date(iso).getTime() - Date.now()) / 60_000)
  if (Math.abs(minutos) < 1) return 'agora'
  if (Math.abs(minutos) < 60) return relativo.format(minutos, 'minute')
  const horas = Math.round(minutos / 60)
  if (Math.abs(horas) < 24) return relativo.format(horas, 'hour')
  return relativo.format(Math.round(horas / 24), 'day')
}

function rotuloContador(n: number) {
  if (n === 0) return 'Notificações: nenhuma não lida'
  return `Notificações: ${n} ${n === 1 ? 'não lida' : 'não lidas'}`
}

/** Sininho da barra superior: lista, marca como lida e recebe notificações em tempo real (SignalR). */
export function Sino() {
  const { estado: auth } = useAuth()
  const { getJson, post } = useApi()
  const navigate = useNavigate()
  const [{ itens, naoLidas }, setEstado] = useState<Estado>({ itens: [], naoLidas: 0 })
  const [aberto, setAberto] = useState(false)
  const [aba, setAba] = useState<'naoLidas' | 'todas'>('naoLidas')
  const ref = useRef<HTMLDivElement>(null)
  useFecharAoClicarFora(ref, aberto, () => setAberto(false))

  // O token muda a cada renovação; a conexão sempre pede o mais recente.
  const token = auth.status === 'autenticado' ? auth.sessao.accessToken : ''
  const tokenRef = useRef(token)
  useEffect(() => {
    tokenRef.current = token
  }, [token])

  useEffect(() => {
    const controle = new AbortController()
    getJson<Estado>('/api/notificacoes', controle.signal)
      .then(setEstado)
      .catch(() => {
        // sem notificações agora; o tempo real continua tentando
      })
    return () => controle.abort()
    // carrega uma vez por sessão; o tempo real mantém a lista atualizada
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    const conexao = new HubConnectionBuilder()
      .withUrl('/hubs/notificacoes', { accessTokenFactory: () => tokenRef.current })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    conexao.on('notificacao', (nova: Notificacao) =>
      setEstado((atual) => ({ itens: [nova, ...atual.itens], naoLidas: atual.naoLidas + 1 })),
    )
    conexao.start().catch(() => {
      // API fora do ar: a lista carregada continua visível
    })
    return () => {
      void conexao.stop()
    }
  }, [])

  const marcarComoLida = (n: Notificacao) => {
    if (n.lidaEm) return
    const agora = new Date().toISOString()
    setEstado((atual) => ({
      itens: atual.itens.map((i) => (i.id === n.id ? { ...i, lidaEm: agora } : i)),
      naoLidas: Math.max(0, atual.naoLidas - 1),
    }))
    void post(`/api/notificacoes/${n.id}/lida`).catch(() => {})
  }

  const abrir = (n: Notificacao) => {
    marcarComoLida(n)
    setAberto(false)
    if (!n.link) return
    // Links da API (ex.: arquivo de relatório pronto) abrem no navegador; os demais são telas do app.
    if (n.link.startsWith('/api/')) window.open(n.link, '_self')
    else navigate(n.link)
  }

  const marcarTodas = () => {
    const agora = new Date().toISOString()
    setEstado((atual) => ({ itens: atual.itens.map((i) => ({ ...i, lidaEm: i.lidaEm ?? agora })), naoLidas: 0 }))
    void post('/api/notificacoes/lidas').catch(() => {})
  }

  const visiveis = aba === 'naoLidas' ? itens.filter((n) => !n.lidaEm) : itens

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setAberto((v) => !v)}
        aria-label={rotuloContador(naoLidas)}
        aria-expanded={aberto}
        className={`relative grid size-10 place-items-center rounded-lg border bg-surface text-muted hover:text-foreground ${
          aberto ? 'border-primary text-primary ring-3 ring-primary-soft' : 'border-border'
        }`}
      >
        <Icone nome="bell" />
        {naoLidas > 0 && (
          <span className="absolute -top-1.5 -right-1.5 grid h-[18px] min-w-[18px] place-items-center rounded-full border-2 border-surface bg-danger px-1 text-[10px] font-bold text-white">
            {naoLidas > 9 ? '9+' : naoLidas}
          </span>
        )}
      </button>

      {aberto && (
        <div
          role="dialog"
          aria-label="Notificações"
          className="absolute right-0 z-30 mt-2 w-[22rem] overflow-hidden rounded-xl border border-border bg-surface shadow-lg sm:w-96"
        >
          <div className="flex items-center px-4 pt-3.5 pb-2">
            <h2 className="flex-1 font-semibold">Notificações</h2>
            {naoLidas > 0 && (
              <button type="button" onClick={marcarTodas} className="text-xs font-semibold text-primary hover:underline">
                Marcar todas como lidas
              </button>
            )}
          </div>

          <div role="tablist" className="flex gap-4 border-b border-border px-4 text-sm">
            {(
              [
                ['naoLidas', `Não lidas${naoLidas ? ` (${naoLidas})` : ''}`],
                ['todas', 'Todas'],
              ] as const
            ).map(([valor, rotulo]) => (
              <button
                key={valor}
                type="button"
                role="tab"
                aria-selected={aba === valor}
                onClick={() => setAba(valor)}
                className={`py-2 font-medium ${aba === valor ? 'text-foreground shadow-[inset_0_-2px_0_var(--primary)]' : 'text-muted'}`}
              >
                {rotulo}
              </button>
            ))}
          </div>

          {visiveis.length === 0 ? (
            <p className="px-4 py-8 text-center text-sm text-muted">
              {aba === 'naoLidas' ? 'Tudo em dia: nenhuma notificação não lida.' : 'Nenhuma notificação ainda.'}
            </p>
          ) : (
            <ul className="max-h-[26rem] divide-y divide-border overflow-y-auto">
              {visiveis.map((n) => {
                const estilo = ICONE_POR_TIPO[n.tipo] ?? PADRAO
                return (
                  <li key={n.id}>
                    <button
                      type="button"
                      onClick={() => abrir(n)}
                      className={`flex w-full gap-3 px-4 py-3 text-left hover:bg-surface-2 ${n.lidaEm ? 'opacity-60' : ''}`}
                    >
                      <span className={`grid size-8 shrink-0 place-items-center rounded-lg ${estilo.tom}`}>
                        <Icone nome={estilo.icone} />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block text-[13px] font-semibold">{n.titulo}</span>
                        <span className="mt-0.5 block text-xs leading-snug text-muted">{n.mensagem}</span>
                      </span>
                      <span className="shrink-0 text-[11px] whitespace-nowrap text-muted">{haQuantoTempo(n.criadaEm)}</span>
                      {!n.lidaEm && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-primary" aria-hidden />}
                    </button>
                  </li>
                )
              })}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
