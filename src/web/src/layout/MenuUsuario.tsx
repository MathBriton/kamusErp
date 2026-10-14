import { useRef, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { Icone } from '../components/Icone'
import { ThemeToggle } from '../components/ThemeToggle'
import { useFecharAoClicarFora } from '../components/useFecharAoClicarFora'
import { useEmpresa } from '../empresa/EmpresaContext'

const iniciais = (nome: string) =>
  nome
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!.toUpperCase())
    .join('')

export function MenuUsuario({ nome, email }: { nome: string; email: string }) {
  const { sair } = useAuth()
  const { perfilAtual } = useEmpresa()
  const [aberto, setAberto] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  useFecharAoClicarFora(ref, aberto, () => setAberto(false))

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setAberto((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={aberto}
        aria-label={`Menu do usuário: ${nome}`}
        className="flex items-center gap-2 rounded-lg p-1 hover:bg-surface-2"
      >
        <span className="grid size-8 place-items-center rounded-full bg-gradient-to-br from-pink-400 to-violet-500 text-xs font-bold text-white">
          {iniciais(nome)}
        </span>
        <span className="hidden text-left leading-tight md:block">
          <span className="block text-[13px] font-semibold">{nome}</span>
          <span className="block text-[11px] text-muted">{perfilAtual}</span>
        </span>
      </button>

      {aberto && (
        <div role="menu" aria-label="Menu do usuário" className="absolute right-0 z-30 mt-2 w-72 rounded-xl border border-border bg-surface p-1.5 shadow-lg">
          <div className="px-2.5 py-2">
            <p className="text-sm font-semibold">{nome}</p>
            <p className="text-xs text-muted">{email}</p>
          </div>
          <div className="flex items-center justify-between border-t border-border px-2.5 py-3">
            <span className="text-sm">Tema</span>
            <ThemeToggle />
          </div>
          <button
            type="button"
            role="menuitem"
            onClick={() => void sair()}
            className="flex w-full items-center gap-2.5 rounded-lg border-t border-border px-2.5 py-2 text-left text-sm text-danger hover:bg-danger-soft"
          >
            <Icone nome="logOut" />
            Sair
          </button>
        </div>
      )}
    </div>
  )
}
