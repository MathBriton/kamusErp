import { useRef, useState } from 'react'
import { Icone } from '../components/Icone'
import { useFecharAoClicarFora } from '../components/useFecharAoClicarFora'
import { CONSOLIDADO, useEmpresa } from '../empresa/EmpresaContext'

export function SeletorEmpresa() {
  const { empresas, selecao, empresaAtual, consolidado, podeConsolidar, selecionar } = useEmpresa()
  const [aberto, setAberto] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  useFecharAoClicarFora(ref, aberto, () => setAberto(false))

  const titulo = consolidado ? 'Todas as empresas' : (empresaAtual?.nomeFantasia ?? '—')
  const subtitulo = consolidado ? 'Visão consolidada' : (empresaAtual?.cnpj ?? '')

  const escolher = (valor: string) => {
    selecionar(valor)
    setAberto(false)
  }

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setAberto((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={aberto}
        aria-label={`Empresa ativa: ${titulo}`}
        className={`flex h-10 items-center gap-2.5 rounded-lg border bg-surface px-2.5 text-left ${
          aberto ? 'border-primary ring-3 ring-primary-soft' : 'border-border'
        }`}
      >
        <span className="grid size-7 place-items-center rounded-md bg-primary-soft text-primary">
          <Icone nome={consolidado ? 'layers' : 'building'} />
        </span>
        <span className="hidden leading-tight sm:block">
          <span className="block max-w-44 truncate text-[13px] font-semibold">{titulo}</span>
          <span className="block text-[11px] text-muted">{subtitulo}</span>
        </span>
        <Icone nome="chevronDown" className="size-4 text-muted" />
      </button>

      {aberto && (
        <div
          role="menu"
          aria-label="Trocar empresa"
          className="absolute right-0 z-30 mt-2 w-80 overflow-hidden rounded-xl border border-border bg-surface shadow-lg"
        >
          <p className="px-3 pt-3 pb-1.5 text-[11px] font-semibold tracking-wider text-muted uppercase">Trocar empresa</p>
          <div className="p-1.5 pt-0">
            {empresas.map((empresa) => (
              <button
                key={empresa.id}
                type="button"
                role="menuitemradio"
                aria-checked={selecao === empresa.id}
                onClick={() => escolher(empresa.id)}
                className={`flex w-full items-center gap-3 rounded-lg px-2.5 py-2 text-left ${
                  selecao === empresa.id ? 'bg-primary-soft' : 'hover:bg-surface-2'
                }`}
              >
                <span className="grid size-8 place-items-center rounded-lg bg-surface-2 text-muted">
                  <Icone nome="building" />
                </span>
                <span className="flex-1">
                  <span className="block text-sm font-semibold">{empresa.nomeFantasia}</span>
                  <span className="block text-xs text-muted">
                    {empresa.cnpj} · {empresa.perfil}
                  </span>
                </span>
                {selecao === empresa.id && <Icone nome="check" className="size-4 text-primary" />}
              </button>
            ))}
          </div>
          {podeConsolidar && (
            <div className="border-t border-border p-1.5">
              <button
                type="button"
                role="menuitemradio"
                aria-checked={consolidado}
                onClick={() => escolher(CONSOLIDADO)}
                className={`flex w-full items-center gap-3 rounded-lg px-2.5 py-2 text-left ${consolidado ? 'bg-primary-soft' : 'hover:bg-surface-2'}`}
              >
                <span className="grid size-8 place-items-center rounded-lg bg-surface-2 text-muted">
                  <Icone nome="layers" />
                </span>
                <span className="flex-1">
                  <span className="block text-sm font-semibold">Todas as empresas</span>
                  <span className="block text-xs text-muted">Visão consolidada · somente leitura</span>
                </span>
                {consolidado && <Icone nome="check" className="size-4 text-primary" />}
              </button>
            </div>
          )}
          <p className="border-t border-border bg-surface-2 px-3 py-2 text-[11px] text-muted">
            Ao trocar, a tela recarrega no contexto da empresa escolhida.
          </p>
        </div>
      )}
    </div>
  )
}
