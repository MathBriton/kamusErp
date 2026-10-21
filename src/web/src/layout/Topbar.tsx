import { useEffect, useState } from 'react'
import { PesquisaGlobal } from '../busca/PesquisaGlobal'
import { Icone } from '../components/Icone'
import { Sino } from '../notificacoes/Sino'
import { useTheme } from '../theme/ThemeContext'
import type { ThemePreference } from '../theme/theme'
import { MenuUsuario } from './MenuUsuario'
import { SeletorEmpresa } from './SeletorEmpresa'

const PROXIMO_TEMA: Record<ThemePreference, ThemePreference> = { light: 'dark', dark: 'light' }
const NOME_TEMA: Record<ThemePreference, string> = { light: 'Claro', dark: 'Escuro' }
const ICONE_TEMA = { light: 'sun', dark: 'moon' } as const

interface Props {
  usuario: { nome: string; email: string }
  onAbrirMenu: () => void
}

export function Topbar({ usuario, onAbrirMenu }: Props) {
  const { preference, setPreference } = useTheme()

  const botaoIcone = 'grid size-10 place-items-center rounded-lg border border-border bg-surface text-muted hover:text-foreground'

  return (
    <header className="flex h-16 shrink-0 items-center gap-3 border-b border-border bg-surface px-4 lg:px-6">
      <button type="button" onClick={onAbrirMenu} aria-label="Abrir menu" className={`${botaoIcone} lg:hidden`}>
        <Icone nome="menu" />
      </button>

      <PesquisaGlobal />

      <div className="ml-auto flex items-center gap-2">
        <Relogio />
        <SeletorEmpresa />

        <Sino />

        <button
          type="button"
          onClick={() => setPreference(PROXIMO_TEMA[preference])}
          aria-label={`Tema: ${NOME_TEMA[preference]} (clique para trocar)`}
          title={`Tema: ${NOME_TEMA[preference]}`}
          className={botaoIcone}
        >
          <Icone nome={ICONE_TEMA[preference]} />
        </button>

        <MenuUsuario nome={usuario.nome} email={usuario.email} />
      </div>
    </header>
  )
}

const formatoDoRelogio = new Intl.DateTimeFormat('pt-BR', {
  timeZone: 'America/Sao_Paulo',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

/** Data e hora de Brasília (a mesma referência da API), atualizadas a cada 30 s. */
function Relogio() {
  const [agora, setAgora] = useState(() => new Date())
  useEffect(() => {
    const timer = window.setInterval(() => setAgora(new Date()), 30_000)
    return () => window.clearInterval(timer)
  }, [])
  const texto = formatoDoRelogio.format(agora).replace(', ', ' · ').replace(' às ', ' · ')
  return (
    <time dateTime={agora.toISOString()} className="hidden px-2 text-sm text-muted tabular-nums xl:block">
      {texto}
    </time>
  )
}
