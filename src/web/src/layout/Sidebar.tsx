import { useState } from 'react'
import { NavLink, useLocation } from 'react-router'
import { Icone } from '../components/Icone'
import { Logo } from '../components/Logo'
import type { GrupoMenu } from '../navegacao/menu'

interface Props {
  menu: GrupoMenu[]
  recolhido: boolean
  onAlternarRecolhido: () => void
  onNavegar: () => void
}

const itemBase = 'flex h-9 items-center gap-2.5 rounded-lg px-2.5 text-sm font-medium transition-colors'

export function Sidebar({ menu, recolhido, onAlternarRecolhido, onNavegar }: Props) {
  const { pathname } = useLocation()
  const grupoDaRota = menu.find((g) => g.itens?.some((i) => pathname.startsWith(i.rota)))?.titulo
  // Cada grupo abre/fecha só pelo clique do usuário. A rota atual abre o seu grupo (inclusive ao chegar por
  // link/notificação), mas nunca fecha os outros que o usuário deixou abertos.
  const [abertos, setAbertos] = useState<Set<string>>(() => new Set(grupoDaRota ? [grupoDaRota] : []))
  const [grupoVisto, setGrupoVisto] = useState(grupoDaRota)
  if (grupoDaRota !== grupoVisto) {
    setGrupoVisto(grupoDaRota)
    if (grupoDaRota && !abertos.has(grupoDaRota)) setAbertos(new Set(abertos).add(grupoDaRota))
  }
  const estaAberto = (titulo: string) => abertos.has(titulo)

  const alternarGrupo = (titulo: string) => {
    if (recolhido) {
      // Com o menu recolhido, clicar no ícone expande o menu com o grupo aberto.
      onAlternarRecolhido()
      setAbertos((atual) => new Set(atual).add(titulo))
      return
    }
    setAbertos((atual) => {
      const novo = new Set(atual)
      if (novo.has(titulo)) novo.delete(titulo)
      else novo.add(titulo)
      return novo
    })
  }

  const classeLink = ({ isActive }: { isActive: boolean }) =>
    `${itemBase} ${isActive ? 'bg-primary-soft text-primary' : 'text-muted hover:bg-surface-2 hover:text-foreground'}`

  return (
    <aside className={`flex h-full flex-col border-r border-border bg-surface p-3 ${recolhido ? 'w-[72px]' : 'w-64'}`}>
      <div className={`flex items-center px-1.5 pb-4 ${recolhido ? 'justify-center' : ''}`}>
        {recolhido ? (
          <span className="grid size-9 place-items-center rounded-lg bg-primary font-extrabold text-primary-foreground">K</span>
        ) : (
          <>
            <Logo />
            <span className="ml-auto rounded-full bg-surface-2 px-2 py-0.5 text-[11px] font-semibold text-muted">ERP</span>
          </>
        )}
      </div>

      <nav aria-label="Menu principal" className="flex-1 space-y-0.5 overflow-y-auto">
        {menu.map((grupo) =>
          grupo.itens ? (
            <div key={grupo.titulo}>
              <button
                type="button"
                onClick={() => alternarGrupo(grupo.titulo)}
                aria-expanded={estaAberto(grupo.titulo)}
                title={recolhido ? grupo.titulo : undefined}
                className={`${itemBase} w-full ${grupoDaRota === grupo.titulo ? 'text-foreground' : 'text-muted'} hover:bg-surface-2 hover:text-foreground ${recolhido ? 'justify-center' : ''}`}
              >
                <Icone nome={grupo.icone} className="size-[18px]" />
                {!recolhido && (
                  <>
                    <span className="flex-1 text-left">{grupo.titulo}</span>
                    <Icone nome={estaAberto(grupo.titulo) ? 'chevronDown' : 'chevronRight'} className="size-4" />
                  </>
                )}
              </button>
              {!recolhido && estaAberto(grupo.titulo) && (
                <div className="mt-0.5 mb-1 ml-[22px] space-y-0.5 border-l border-border pl-3">
                  {grupo.itens.map((item) => (
                    <NavLink key={item.rota} to={item.rota} onClick={onNavegar} className={classeLink}>
                      {item.titulo}
                    </NavLink>
                  ))}
                </div>
              )}
            </div>
          ) : (
            <NavLink
              key={grupo.titulo}
              to={grupo.rota!}
              end
              onClick={onNavegar}
              title={recolhido ? grupo.titulo : undefined}
              className={(estado) => `${classeLink(estado)} ${recolhido ? 'justify-center' : ''}`}
            >
              <Icone nome={grupo.icone} className="size-[18px]" />
              {!recolhido && grupo.titulo}
            </NavLink>
          ),
        )}
      </nav>

      <div className="mt-2 hidden border-t border-border pt-2 lg:block">
        <button
          type="button"
          onClick={onAlternarRecolhido}
          className={`${itemBase} w-full text-muted hover:bg-surface-2 hover:text-foreground ${recolhido ? 'justify-center' : ''}`}
        >
          <Icone nome={recolhido ? 'chevronsRight' : 'chevronsLeft'} />
          <span className={recolhido ? 'sr-only' : ''}>{recolhido ? 'Expandir menu' : 'Recolher menu'}</span>
        </button>
      </div>
    </aside>
  )
}
