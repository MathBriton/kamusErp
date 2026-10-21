import { useEffect, useRef, type ReactNode } from 'react'
import { Icone, type NomeIcone } from '../Icone'

export interface Coluna<T> {
  titulo: string
  celula: (item: T) => ReactNode
  alinhar?: 'direita'
}

const TAMANHOS_DE_PAGINA = [20, 50, 100] as const

interface Props<T> {
  /** Nome acessível da tabela. */
  rotulo: string
  colunas: Coluna<T>[]
  /** null enquanto carrega pela primeira vez. */
  itens: T[] | null
  chave: (item: T) => string
  /** Nome da linha para leitores de tela ("Selecionar Camiseta"). */
  nome?: (item: T) => string
  total: number
  pagina: number
  tamanho: number
  onPagina: (pagina: number) => void
  onTamanho: (tamanho: number) => void
  /** Seleção que sobrevive à troca de página (a tela limpa ao mudar os filtros). */
  selecao?: { selecionados: Set<string>; onMudar: (selecionados: Set<string>) => void }
  /** Botões de cada linha (ver, editar, inativar...). */
  acoes?: (item: T) => ReactNode
  /** Botões da barra que aparece quando há linhas selecionadas. */
  acoesEmLote?: ReactNode
  /** Linha esmaecida (ex.: inativa). */
  apagada?: (item: T) => boolean
  vazio?: string
}

const botaoPagina = 'grid size-8 place-items-center rounded-lg border border-border bg-surface text-muted hover:text-foreground disabled:opacity-40 disabled:hover:text-muted'
const plural = (n: number, um: string, varios: string) => `${n.toLocaleString('pt-BR')} ${n === 1 ? um : varios}`

/**
 * Grade padrão das telas de cadastro e consulta: tabela com seleção de linhas, ações por linha e em lote,
 * total de registros e paginação manual (a página e o tamanho vão para a API).
 */
export function GradeDeRegistros<T>({
  rotulo, colunas, itens, chave, nome, total, pagina, tamanho, onPagina, onTamanho, selecao, acoes, acoesEmLote, apagada,
  vazio = 'Nenhum registro encontrado.',
}: Props<T>) {
  const paginas = Math.max(1, Math.ceil(total / tamanho))
  const inicio = total === 0 ? 0 : (pagina - 1) * tamanho + 1
  const fim = Math.min(total, pagina * tamanho)
  const idsDaPagina = (itens ?? []).map(chave)
  const marcados = selecao ? idsDaPagina.filter((id) => selecao.selecionados.has(id)).length : 0
  const todos = marcados > 0 && marcados === idsDaPagina.length
  const caixaTodos = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (caixaTodos.current) caixaTodos.current.indeterminate = marcados > 0 && !todos
  }, [marcados, todos])

  const alternar = (id: string) => {
    if (!selecao) return
    const novo = new Set(selecao.selecionados)
    if (novo.has(id)) novo.delete(id)
    else novo.add(id)
    selecao.onMudar(novo)
  }
  const alternarPagina = () => {
    if (!selecao) return
    const novo = new Set(selecao.selecionados)
    for (const id of idsDaPagina) {
      if (todos) novo.delete(id)
      else novo.add(id)
    }
    selecao.onMudar(novo)
  }

  const quantosSelecionados = selecao?.selecionados.size ?? 0
  const celula = (c: Coluna<T>) => `px-4 py-2.5 ${c.alinhar === 'direita' ? 'text-right tabular-nums' : ''}`

  return (
    <div className="mt-4 space-y-2">
      {quantosSelecionados > 0 && (
        <section aria-label="Seleção" className="flex flex-wrap items-center gap-3 rounded-xl border border-primary bg-primary-soft px-4 py-2 text-sm">
          <span className="font-semibold text-primary">{plural(quantosSelecionados, 'selecionado', 'selecionados')}</span>
          <button type="button" onClick={() => selecao?.onMudar(new Set())} className="text-primary underline-offset-2 hover:underline">
            Limpar seleção
          </button>
          <div className="ml-auto flex flex-wrap gap-2">{acoesEmLote}</div>
        </section>
      )}

      <div className="overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
        <table aria-label={rotulo} className="w-full text-sm">
          <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
            <tr>
              {selecao && (
                <th className="w-10 px-4 py-2">
                  <input
                    ref={caixaTodos}
                    type="checkbox"
                    aria-label="Selecionar todos da página"
                    checked={todos}
                    disabled={idsDaPagina.length === 0}
                    onChange={alternarPagina}
                    className="h-4 w-4 accent-primary"
                  />
                </th>
              )}
              {colunas.map((c) => (
                <th key={c.titulo} className={`px-4 py-2 ${c.alinhar === 'direita' ? 'text-right' : ''}`}>{c.titulo}</th>
              ))}
              {acoes && <th className="px-4 py-2 text-right">Ações</th>}
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {itens === null ? (
              <tr>
                <td colSpan={colunas.length + 2} className="p-8 text-center text-muted">Carregando...</td>
              </tr>
            ) : itens.length === 0 ? (
              <tr>
                <td colSpan={colunas.length + 2} className="p-8 text-center text-muted">{vazio}</td>
              </tr>
            ) : (
              itens.map((item) => {
                const id = chave(item)
                const marcado = selecao?.selecionados.has(id) ?? false
                return (
                  <tr key={id} className={`${marcado ? 'bg-primary-soft/40' : 'hover:bg-surface-2/60'} ${apagada?.(item) ? 'text-muted' : ''}`}>
                    {selecao && (
                      <td className="px-4 py-2.5">
                        <input
                          type="checkbox"
                          aria-label={`Selecionar ${nome?.(item) ?? id}`}
                          checked={marcado}
                          onChange={() => alternar(id)}
                          className="h-4 w-4 accent-primary"
                        />
                      </td>
                    )}
                    {colunas.map((c) => (
                      <td key={c.titulo} className={celula(c)}>{c.celula(item)}</td>
                    ))}
                    {acoes && (
                      <td className="px-4 py-2">
                        <div className="flex justify-end gap-1">{acoes(item)}</div>
                      </td>
                    )}
                  </tr>
                )
              })
            )}
          </tbody>
        </table>
      </div>

      <nav aria-label="Paginação" className="flex flex-wrap items-center gap-x-4 gap-y-2 px-1 text-sm text-muted">
        <span className="font-semibold text-foreground">{plural(total, 'registro', 'registros')}</span>
        {total > 0 && <span>{`Mostrando ${inicio}–${fim}`}</span>}
        <label className="flex items-center gap-2">
          <span>Por página</span>
          <select
            aria-label="Registros por página"
            value={tamanho}
            onChange={(e) => onTamanho(Number(e.target.value))}
            className="h-8 rounded-lg border border-border bg-surface px-2 text-sm text-foreground"
          >
            {TAMANHOS_DE_PAGINA.map((t) => (
              <option key={t} value={t}>{t}</option>
            ))}
          </select>
        </label>
        <div className="ml-auto flex items-center gap-1.5">
          <button type="button" aria-label="Primeira página" disabled={pagina <= 1} onClick={() => onPagina(1)} className={botaoPagina}>
            <Icone nome="chevronsLeft" />
          </button>
          <button type="button" aria-label="Página anterior" disabled={pagina <= 1} onClick={() => onPagina(pagina - 1)} className={botaoPagina}>
            <Icone nome="chevronLeft" />
          </button>
          <span className="px-2 text-foreground">{`Página ${pagina} de ${paginas}`}</span>
          <button type="button" aria-label="Próxima página" disabled={pagina >= paginas} onClick={() => onPagina(pagina + 1)} className={botaoPagina}>
            <Icone nome="chevronRight" />
          </button>
          <button type="button" aria-label="Última página" disabled={pagina >= paginas} onClick={() => onPagina(paginas)} className={botaoPagina}>
            <Icone nome="chevronsRight" />
          </button>
        </div>
      </nav>
    </div>
  )
}

/** Botão de ícone para a coluna "Ações" (o rótulo vira o nome acessível e a dica). */
export function AcaoDaLinha({ rotulo, icone, onClick, perigo = false }: { rotulo: string; icone: NomeIcone; onClick: () => void; perigo?: boolean }) {
  return (
    <button
      type="button"
      aria-label={rotulo}
      title={rotulo}
      onClick={onClick}
      className={`grid size-8 place-items-center rounded-lg text-muted hover:bg-surface-2 ${perigo ? 'hover:text-danger' : 'hover:text-primary'}`}
    >
      <Icone nome={icone} />
    </button>
  )
}
