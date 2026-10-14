import { Amostra } from '../cadastros/Amostra'
import type { Celula, Matriz } from './tipos'

interface Props {
  matriz: Matriz
  /** Leitura: clicar numa célula (ex.: abrir o kardex). Sem isso, as células são só texto. */
  onCelula?: (celula: Celula) => void
  /** Digitação: quantidades por SKU (texto livre, validado por quem usa). */
  digitacao?: { valores: Record<string, string>; onMudar: (skuId: string, valor: string) => void; permitirNegativo?: boolean; somenteAtivos?: boolean }
}

/** Saldo (ou digitação de quantidades) em matriz cor × tamanho, com totais por cor e por tamanho. */
export function MatrizDeEstoque({ matriz, onCelula, digitacao }: Props) {
  const celula = (corId: string, tamanho: string) => matriz.celulas.find((c) => c.corId === corId && c.tamanho === tamanho)
  const totalDaCor = (corId: string) => matriz.celulas.filter((c) => c.corId === corId).reduce((s, c) => s + c.quantidade, 0)
  const totalDoTamanho = (t: string) => matriz.celulas.filter((c) => c.tamanho === t).reduce((s, c) => s + c.quantidade, 0)

  return (
    <div className="overflow-x-auto">
      <table aria-label={`Estoque de ${matriz.descricao}`} className="w-full border-separate border-spacing-1 text-sm">
        <thead>
          <tr>
            <th className="px-2 py-1 text-left text-xs text-muted uppercase">Cor</th>
            {matriz.tamanhos.map((t) => (
              <th key={t} className="px-2 py-1 text-center text-xs text-muted uppercase">{t}</th>
            ))}
            <th className="px-2 py-1 text-right text-xs text-muted uppercase">Total</th>
          </tr>
        </thead>
        <tbody>
          {matriz.cores.map((cor) => (
            <tr key={cor.id}>
              <th scope="row" className="px-2 py-1 text-left font-semibold whitespace-nowrap">
                <span className="flex items-center gap-2">
                  <Amostra hex={cor.hex} />
                  {cor.nome}
                </span>
              </th>
              {matriz.tamanhos.map((t) => {
                const c = celula(cor.id, t)
                if (!c) return <td key={t} />
                const nome = `${cor.nome} ${t}`
                if (digitacao) {
                  const bloqueado = digitacao.somenteAtivos && !c.ativo
                  return (
                    <td key={t} className="min-w-16">
                      <input
                        aria-label={nome}
                        inputMode="numeric"
                        disabled={bloqueado}
                        placeholder={bloqueado ? '—' : '0'}
                        value={digitacao.valores[c.skuId] ?? ''}
                        onChange={(e) => digitacao.onMudar(c.skuId, e.target.value.replace(digitacao.permitirNegativo ? /[^\d-]/g : /\D/g, ''))}
                        className="h-9 w-full rounded-lg border border-border bg-surface px-1 text-center tabular-nums disabled:bg-surface-2"
                      />
                      <span className="mt-0.5 block text-center text-[11px] text-muted tabular-nums" title="Saldo atual">{`hoje ${c.quantidade}`}</span>
                    </td>
                  )
                }
                const classe = `w-full rounded-lg border px-2 py-1.5 text-center tabular-nums ${
                  c.quantidade < 0 ? 'border-danger text-danger' : c.quantidade === 0 ? 'border-border bg-surface-2 text-muted' : 'border-border bg-surface font-semibold'
                } ${c.ativo ? '' : 'border-dashed'}`
                const rotulo = `${nome}: ${c.quantidade}`
                return (
                  <td key={t} className="min-w-14">
                    {onCelula ? (
                      <button type="button" aria-label={rotulo} onClick={() => onCelula(c)} className={`${classe} hover:border-primary`}>
                        {c.quantidade}
                      </button>
                    ) : (
                      <div aria-label={rotulo} className={classe}>{c.quantidade}</div>
                    )}
                  </td>
                )
              })}
              <td className="px-2 text-right font-semibold tabular-nums">{totalDaCor(cor.id)}</td>
            </tr>
          ))}
          <tr>
            <th scope="row" className="px-2 py-1 text-left text-xs text-muted uppercase">Total</th>
            {matriz.tamanhos.map((t) => (
              <td key={t} className="text-center font-semibold tabular-nums">{totalDoTamanho(t)}</td>
            ))}
            <td className="px-2 text-right font-bold tabular-nums">{matriz.total}</td>
          </tr>
        </tbody>
      </table>
    </div>
  )
}
