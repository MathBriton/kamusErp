import { useMemo, useState } from 'react'
import { Outlet } from 'react-router'
import type { Sessao } from '../auth/api'
import { useEmpresa } from '../empresa/EmpresaContext'
import { filtrarMenu, MENU } from '../navegacao/menu'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'

const CHAVE_RECOLHIDO = 'kamus.menu.recolhido'

function lerRecolhido() {
  try {
    return localStorage.getItem(CHAVE_RECOLHIDO) === 'true'
  } catch {
    return false
  }
}

/** Layout das telas autenticadas: menu lateral + barra superior + conteúdo. */
export function Shell({ sessao }: { sessao: Sessao }) {
  const { perfilAtual } = useEmpresa()
  const menu = useMemo(() => filtrarMenu(MENU, perfilAtual), [perfilAtual])
  const [recolhido, setRecolhido] = useState(lerRecolhido)
  const [menuMovelAberto, setMenuMovelAberto] = useState(false)

  const alternarRecolhido = () => {
    setRecolhido((atual) => {
      try {
        localStorage.setItem(CHAVE_RECOLHIDO, String(!atual))
      } catch {
        // sem armazenamento
      }
      return !atual
    })
  }

  return (
    <div className="flex h-dvh overflow-hidden">
      <div className="hidden lg:block">
        <Sidebar menu={menu} recolhido={recolhido} onAlternarRecolhido={alternarRecolhido} onNavegar={() => {}} />
      </div>

      {menuMovelAberto && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button type="button" aria-label="Fechar menu" onClick={() => setMenuMovelAberto(false)} className="absolute inset-0 bg-black/40" />
          <div className="relative h-full w-fit">
            <Sidebar menu={menu} recolhido={false} onAlternarRecolhido={() => {}} onNavegar={() => setMenuMovelAberto(false)} />
          </div>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar usuario={sessao.usuario} onAbrirMenu={() => setMenuMovelAberto(true)} />
        <main className="flex-1 overflow-y-auto p-4 lg:p-7">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
