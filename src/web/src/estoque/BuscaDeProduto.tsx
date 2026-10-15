import { useEffect, useState } from 'react'
import { useApi } from '../api/useApi'
import type { ProdutoResumo } from '../cadastros/catalogo'
import { estilos } from '../components/estilos'

/** Campo de busca de produto (referência, descrição ou código de barras) com lista de sugestões. */
export function BuscaDeProduto({ onEscolher, desabilitado = false, placeholder }: { onEscolher: (produtoId: string) => void; desabilitado?: boolean; placeholder?: string }) {
  const { getJson } = useApi()
  const [busca, setBusca] = useState('')
  const [encontrados, setEncontrados] = useState<ProdutoResumo[]>([])

  useEffect(() => {
    const termo = busca.trim()
    if (termo.length < 2) return
    const controle = new AbortController()
    const timer = window.setTimeout(() => {
      getJson<{ itens: ProdutoResumo[] }>(`/api/produtos?${new URLSearchParams({ situacao: 'ativos', busca: termo })}`, controle.signal)
        .then((r) => setEncontrados(r.itens.slice(0, 8)))
        .catch(() => {})
    }, 250)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, busca])

  return (
    <div className="relative">
      <input
        aria-label="Buscar produto"
        value={busca}
        onChange={(e) => {
          setBusca(e.target.value)
          if (e.target.value.trim().length < 2) setEncontrados([])
        }}
        disabled={desabilitado}
        placeholder={placeholder ?? 'Buscar produto por referência, descrição ou código de barras'}
        className={estilos.campo}
      />
      {encontrados.length > 0 && (
        <ul className="absolute z-10 mt-1 w-full overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
          {encontrados.map((p) => (
            <li key={p.id}>
              <button
                type="button"
                onClick={() => {
                  setBusca('')
                  setEncontrados([])
                  onEscolher(p.id)
                }}
                className="w-full px-3 py-2 text-left text-sm hover:bg-surface-2"
              >
                <span className="font-mono text-xs text-muted">{p.referencia}</span> <span className="font-semibold">{p.descricao}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
