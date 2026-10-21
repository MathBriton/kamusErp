import { moeda } from '../cadastros/catalogo'
import { dataBr } from '../financeiro/tipos'
import type { VendasDoDia } from './tipos'

const pecas = (n: number) => `${n} ${n === 1 ? 'peça' : 'peças'}`
const vendasTexto = (n: number) => `${n} ${n === 1 ? 'venda' : 'vendas'}`

const LARGURA = 300
const ALTURA = 100

/** Curva de vendas dos últimos 30 dias (SVG, sem biblioteca) com média diária, peças vendidas e condicionais na rua. */
export function GraficoDeVendas({ dias, pecasEmCondicional }: { dias: VendasDoDia[]; pecasEmCondicional: number | null }) {
  const total = dias.reduce((s, d) => s + d.total, 0)
  const itens = dias.reduce((s, d) => s + d.pecas, 0)
  const maior = Math.max(...dias.map((d) => d.total), 1)
  const passo = LARGURA / Math.max(dias.length - 1, 1)
  const pontos = dias.map((d, i) => [i * passo, ALTURA - 6 - (d.total / maior) * (ALTURA - 16)] as const)
  const linha = pontos.map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${x.toFixed(2)},${y.toFixed(2)}`).join(' ')
  const area = `${linha} L${LARGURA},${ALTURA} L0,${ALTURA} Z`
  const intervalo = `${dataBr(dias[0]!.data)} – ${dataBr(dias[dias.length - 1]!.data)}`

  return (
    <section aria-label="Vendas — últimos 30 dias" className="rounded-2xl border border-border bg-surface p-6 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-xs font-bold tracking-wider text-muted uppercase">Vendas — últimos 30 dias</h2>
          <p className="mt-1 text-[28px] leading-9 font-bold tracking-tight tabular-nums">{moeda(total)}</p>
        </div>
        <span className="rounded-full border border-border px-3 py-1 text-xs text-muted tabular-nums">{intervalo}</span>
      </div>

      {total === 0 ? (
        <div className="mt-4 grid h-44 place-items-center rounded-xl border border-dashed border-border bg-surface-2/50 px-6 text-center text-sm text-muted">
          Aguardando a primeira venda para desenhar a curva dos últimos 30 dias.
        </div>
      ) : (
        <svg role="img" aria-label={`Vendas dos últimos 30 dias: ${moeda(total)}`} viewBox={`0 0 ${LARGURA} ${ALTURA}`} preserveAspectRatio="none" className="mt-4 h-44 w-full overflow-visible">
          <defs>
            <linearGradient id="kamus-area-vendas" x1="0" x2="0" y1="0" y2="1">
              <stop offset="0%" style={{ stopColor: 'var(--color-primary)', stopOpacity: 0.28 }} />
              <stop offset="100%" style={{ stopColor: 'var(--color-primary)', stopOpacity: 0 }} />
            </linearGradient>
          </defs>
          {[0.25, 0.5, 0.75].map((f) => (
            <line key={f} x1={0} x2={LARGURA} y1={ALTURA * f} y2={ALTURA * f} className="stroke-border" strokeWidth={1} vectorEffect="non-scaling-stroke" />
          ))}
          <path d={area} fill="url(#kamus-area-vendas)" />
          <path d={linha} fill="none" className="stroke-primary" strokeWidth={2.5} strokeLinejoin="round" strokeLinecap="round" vectorEffect="non-scaling-stroke" />
          {dias.map((d, i) => (
            <rect key={d.data} x={i * passo - passo / 2} y={0} width={passo} height={ALTURA} fill="transparent">
              <title>{`${dataBr(d.data)}: ${moeda(d.total)} (${vendasTexto(d.vendas)})`}</title>
            </rect>
          ))}
        </svg>
      )}

      <dl className="mt-5 grid grid-cols-3 gap-4 border-t border-border pt-4 text-sm">
        <div>
          <dt className="text-xs text-muted">Média diária</dt>
          <dd className="mt-0.5 font-semibold tabular-nums">{moeda(Math.round((total / dias.length) * 100) / 100)}</dd>
        </div>
        <div>
          <dt className="text-xs text-muted">Itens vendidos</dt>
          <dd className="mt-0.5 font-semibold tabular-nums">{pecas(itens)}</dd>
        </div>
        {pecasEmCondicional !== null && (
          <div>
            <dt className="text-xs text-muted">Condicionais abertos</dt>
            <dd className="mt-0.5 font-semibold tabular-nums">{pecas(pecasEmCondicional)}</dd>
          </div>
        )}
      </dl>
    </section>
  )
}
