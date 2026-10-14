import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import { useNavigate } from 'react-router'
import { useApi } from '../api/useApi'
import { Icone, type NomeIcone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { filtrarMenu, MENU, todasAsRotas } from '../navegacao/menu'
import { contem } from './texto'

interface ItemApi {
  tipo: string
  id: string
  titulo: string
  subtitulo: string | null
  link: string | null
}

interface RespostaBusca {
  grupos: { grupo: string; itens: ItemApi[] }[]
}

interface Opcao {
  chave: string
  grupo: string
  titulo: string
  subtitulo?: string
  icone: NomeIcone
  executar: () => void
}

const ICONE_POR_TIPO: Record<string, NomeIcone> = { empresa: 'building', usuario: 'users' }
const MINIMO = 2

/** Pesquisa global (Ctrl+K / ⌘K): telas do menu (local) + resultados de todos os módulos (API). */
export function PesquisaGlobal() {
  const [aberta, setAberta] = useState(false)

  useEffect(() => {
    const onTecla = (e: globalThis.KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        setAberta(true)
      }
    }
    window.addEventListener('keydown', onTecla)
    return () => window.removeEventListener('keydown', onTecla)
  }, [])

  return (
    <>
      <button
        type="button"
        onClick={() => setAberta(true)}
        aria-label="Pesquisar (Ctrl+K)"
        className="hidden h-10 w-full max-w-md items-center gap-2.5 rounded-lg border border-border bg-surface-2 px-3 text-sm text-muted hover:border-primary md:flex"
      >
        <Icone nome="search" />
        <span className="flex-1 truncate text-left">Pesquisar empresas, usuários, telas...</span>
        <kbd className="rounded border border-border bg-surface px-1.5 text-[11px]">Ctrl K</kbd>
      </button>
      <button
        type="button"
        onClick={() => setAberta(true)}
        aria-label="Pesquisar"
        className="grid size-10 place-items-center rounded-lg border border-border bg-surface text-muted md:hidden"
      >
        <Icone nome="search" />
      </button>
      {aberta && <Dialogo onFechar={() => setAberta(false)} />}
    </>
  )
}

function Dialogo({ onFechar }: { onFechar: () => void }) {
  const navigate = useNavigate()
  const { getJson } = useApi()
  const { perfilAtual, selecionar } = useEmpresa()
  const [termo, setTermo] = useState('')
  const [ativo, setAtivo] = useState(0)
  const [resultadoApi, setResultadoApi] = useState<{ termo: string; grupos: RespostaBusca['grupos'] }>({ termo: '', grupos: [] })
  const listaRef = useRef<HTMLUListElement>(null)

  const termoLimpo = termo.trim()

  // Busca na API com espera curta (evita uma requisição por tecla)
  useEffect(() => {
    if (termoLimpo.length < MINIMO) return
    const controle = new AbortController()
    const timer = setTimeout(() => {
      getJson<RespostaBusca>(`/api/busca?q=${encodeURIComponent(termoLimpo)}`, controle.signal)
        .then((r) => setResultadoApi({ termo: termoLimpo, grupos: r.grupos }))
        .catch(() => {})
    }, 200)
    return () => {
      clearTimeout(timer)
      controle.abort()
    }
  }, [termoLimpo, getJson])

  const opcoes = useMemo<Opcao[]>(() => {
    if (termoLimpo.length < MINIMO) return []
    const telas = todasAsRotas(filtrarMenu(MENU, perfilAtual))
      .filter((t) => contem(t.titulo, termoLimpo) || contem(t.descricao, termoLimpo))
      .slice(0, 6)
      .map<Opcao>((t) => ({
        chave: `tela:${t.rota}`,
        grupo: 'Telas',
        titulo: t.titulo,
        subtitulo: t.previstoPara ? `Previsto para a ${t.previstoPara}` : t.descricao,
        icone: 'arrowRight',
        executar: () => navigate(t.rota),
      }))

    const daApi = resultadoApi.termo === termoLimpo ? resultadoApi.grupos : []
    const remotas = daApi.flatMap((g) =>
      g.itens.map<Opcao>((i) => ({
        chave: `${i.tipo}:${i.id}`,
        grupo: g.grupo,
        titulo: i.titulo,
        subtitulo: i.subtitulo ?? undefined,
        icone: ICONE_POR_TIPO[i.tipo] ?? 'search',
        executar: () => {
          if (i.tipo === 'empresa') selecionar(i.id)
          else if (i.link) navigate(i.link)
        },
      })),
    )
    return [...telas, ...remotas]
  }, [termoLimpo, perfilAtual, resultadoApi, navigate, selecionar])

  const indice = Math.min(ativo, Math.max(0, opcoes.length - 1))

  const escolher = (opcao: Opcao | undefined) => {
    if (!opcao) return
    opcao.executar()
    onFechar()
  }

  const onTecla = (e: KeyboardEvent) => {
    if (e.key === 'Escape') onFechar()
    else if (e.key === 'ArrowDown') {
      e.preventDefault()
      setAtivo((indice + 1) % Math.max(1, opcoes.length))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setAtivo((indice - 1 + opcoes.length) % Math.max(1, opcoes.length))
    } else if (e.key === 'Enter') {
      e.preventDefault()
      escolher(opcoes[indice])
    }
  }

  useEffect(() => {
    listaRef.current?.querySelector('[aria-selected="true"]')?.scrollIntoView?.({ block: 'nearest' })
  }, [indice])

  const grupos = opcoes.reduce<{ grupo: string; itens: { opcao: Opcao; i: number }[] }[]>((acc, opcao, i) => {
    const ultimo = acc.at(-1)
    if (ultimo?.grupo === opcao.grupo) ultimo.itens.push({ opcao, i })
    else acc.push({ grupo: opcao.grupo, itens: [{ opcao, i }] })
    return acc
  }, [])

  return (
    <div className="fixed inset-0 z-50">
      <button type="button" aria-label="Fechar pesquisa" onClick={onFechar} className="absolute inset-0 bg-black/40 backdrop-blur-[2px]" />
      <div
        role="dialog"
        aria-label="Pesquisa global"
        className="relative mx-auto mt-20 w-[min(42rem,calc(100%-2rem))] overflow-hidden rounded-xl border border-border bg-surface shadow-2xl"
      >
        <div className="flex items-center gap-3 border-b border-border px-4">
          <Icone nome="search" className="size-5 text-muted" />
          <input
            autoFocus
            aria-label="Pesquisar"
            value={termo}
            onChange={(e) => {
              setTermo(e.target.value)
              setAtivo(0)
            }}
            onKeyDown={onTecla}
            placeholder="Digite pelo menos 2 letras..."
            className="h-14 flex-1 bg-transparent text-base outline-none"
          />
          <kbd className="rounded border border-border px-1.5 text-[11px] text-muted">Esc</kbd>
        </div>

        <div className="max-h-[26rem] overflow-y-auto p-2">
          {termoLimpo.length < MINIMO ? (
            <p className="px-3 py-6 text-center text-sm text-muted">Pesquise telas, empresas e usuários. Produtos e clientes entram com os cadastros.</p>
          ) : opcoes.length === 0 ? (
            <p className="px-3 py-6 text-center text-sm text-muted">Nenhum resultado para "{termoLimpo}".</p>
          ) : (
            <ul ref={listaRef} role="listbox" aria-label="Resultados">
              {grupos.map((g) => (
                <li key={g.grupo} role="presentation">
                  <p className="px-3 pt-2 pb-1 text-[11px] font-semibold tracking-wider text-muted uppercase">{g.grupo}</p>
                  <ul role="presentation">
                    {g.itens.map(({ opcao, i }) => (
                      <li
                        key={opcao.chave}
                        role="option"
                        aria-selected={i === indice}
                        onMouseEnter={() => setAtivo(i)}
                        onClick={() => escolher(opcao)}
                        className={`flex cursor-pointer items-center gap-3 rounded-lg px-3 py-2 ${i === indice ? 'bg-primary-soft' : ''}`}
                      >
                        <span className="grid size-8 place-items-center rounded-lg bg-surface-2 text-muted">
                          <Icone nome={opcao.icone} />
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="block text-sm font-semibold">{opcao.titulo}</span>
                          {opcao.subtitulo && <span className="block truncate text-xs text-muted">{opcao.subtitulo}</span>}
                        </span>
                      </li>
                    ))}
                  </ul>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="flex gap-4 border-t border-border bg-surface-2 px-4 py-2 text-[11px] text-muted">
          <span>↑ ↓ navegar</span>
          <span>↵ abrir</span>
          <span>Esc fechar</span>
          <span className="ml-auto">Ignora acentos</span>
        </div>
      </div>
    </div>
  )
}
