import { useRef, useState } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Icone, type NomeIcone } from '../components/Icone'
import { useFecharAoClicarFora } from '../components/useFecharAoClicarFora'

const FORMATOS: { formato: string; rotulo: string; descricao: string; icone: NomeIcone; tom: string }[] = [
  { formato: 'pdf', rotulo: 'PDF', descricao: 'Formatado para impressão', icone: 'file', tom: 'bg-danger-soft text-danger' },
  { formato: 'xlsx', rotulo: 'Excel (.xlsx)', descricao: 'Números e datas reais, com filtros', icone: 'file', tom: 'bg-success-soft text-success' },
  { formato: 'csv', rotulo: 'CSV', descricao: 'Para importar em outros sistemas', icone: 'file', tom: 'bg-surface-2 text-muted' },
]

type Aviso = { tipo: 'status' | 'erro'; texto: string } | null

/**
 * Botão padrão "Exportar ▾" de toda tela com relatório. Envia os mesmos filtros da tela;
 * a API decide entre devolver o arquivo na hora ou gerar em segundo plano.
 */
export function BotaoExportar({ relatorio, parametros = {} }: { relatorio: string; parametros?: Record<string, string> }) {
  const { baixar } = useApi()
  const [aberto, setAberto] = useState(false)
  const [gerando, setGerando] = useState(false)
  const [aviso, setAviso] = useState<Aviso>(null)
  const ref = useRef<HTMLDivElement>(null)
  useFecharAoClicarFora(ref, aberto, () => setAberto(false))

  async function exportar(formato: string) {
    setAberto(false)
    setGerando(true)
    setAviso(null)
    const filtros = new URLSearchParams(Object.entries(parametros).filter(([, v]) => v !== ''))
    const query = filtros.size ? `?${filtros}` : ''
    try {
      const resultado = await baixar(`/api/relatorios/${relatorio}.${formato}${query}`)
      if (resultado.tipo === 'agendado') {
        setAviso({ tipo: 'status', texto: resultado.mensagem })
        return
      }
      const url = URL.createObjectURL(resultado.blob)
      const link = document.createElement('a')
      link.href = url
      link.download = resultado.nome
      link.click()
      URL.revokeObjectURL(url)
      setAviso({ tipo: 'status', texto: `Download iniciado: ${resultado.nome}` })
    } catch (e) {
      setAviso({ tipo: 'erro', texto: e instanceof ErroDeApi ? e.message : 'Não foi possível exportar o relatório.' })
    } finally {
      setGerando(false)
    }
  }

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setAberto((v) => !v)}
        disabled={gerando}
        aria-haspopup="menu"
        aria-expanded={aberto}
        className={`flex h-9 items-center gap-2 rounded-lg border bg-surface px-3 text-sm font-medium disabled:opacity-60 ${
          aberto ? 'border-primary ring-3 ring-primary-soft' : 'border-border'
        }`}
      >
        <Icone nome="download" />
        {gerando ? 'Gerando...' : 'Exportar'}
        <Icone nome="chevronDown" className="size-4 text-muted" />
      </button>

      {aberto && (
        <div role="menu" aria-label="Exportar" className="absolute right-0 z-30 mt-2 w-72 overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
          <p className="px-3 pt-3 pb-1.5 text-[11px] font-semibold tracking-wider text-muted uppercase">Exportar com os filtros atuais</p>
          <div className="p-1.5 pt-0">
            {FORMATOS.map((f) => (
              <button
                key={f.formato}
                type="button"
                role="menuitem"
                onClick={() => void exportar(f.formato)}
                className="flex w-full items-center gap-3 rounded-lg px-2.5 py-2 text-left hover:bg-surface-2"
              >
                <span className={`grid size-8 place-items-center rounded-lg ${f.tom}`}>
                  <Icone nome={f.icone} />
                </span>
                <span>
                  <span className="block text-sm font-semibold">{f.rotulo}</span>
                  <span className="block text-xs text-muted">{f.descricao}</span>
                </span>
              </button>
            ))}
          </div>
          <p className="border-t border-border bg-surface-2 px-3 py-2 text-[11px] leading-relaxed text-muted">
            O PDF sai sempre no tema claro. Relatórios grandes são gerados em segundo plano e avisados no sininho.
          </p>
        </div>
      )}

      {aviso && (
        <p
          role={aviso.tipo === 'erro' ? 'alert' : 'status'}
          className={`absolute right-0 z-20 mt-2 w-72 rounded-lg px-3 py-2 text-xs shadow-sm ${
            aviso.tipo === 'erro' ? 'bg-danger-soft text-danger' : 'bg-info-soft text-info'
          }`}
        >
          {aviso.texto}
        </p>
      )}
    </div>
  )
}
