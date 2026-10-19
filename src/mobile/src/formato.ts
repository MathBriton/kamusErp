/** 1234.5 → "R$ 1.234,50" (sem depender do Intl do aparelho). */
export function moeda(valor: number) {
  const [inteiro, centavos] = Math.abs(valor).toFixed(2).split('.')
  return `${valor < 0 ? '−' : ''}R$ ${inteiro!.replace(/\B(?=(\d{3})+(?!\d))/g, '.')},${centavos}`
}

/** Hoje no calendário de Brasília (UTC−3), como a API. */
export const hojeIso = () => new Date(Date.now() - 3 * 3_600_000).toISOString().slice(0, 10)

export function somarDias(dia: string, dias: number) {
  const d = new Date(`${dia}T12:00:00Z`)
  d.setUTCDate(d.getUTCDate() + dias)
  return d.toISOString().slice(0, 10)
}

/** "2026-10-19" → "19/10". */
export const diaMes = (dia: string) => `${dia.slice(8, 10)}/${dia.slice(5, 7)}`

/** 200 → "▲ 200%", -12.5 → "▼ 12,5%". */
export function variacao(v: number) {
  const texto = (Math.round(Math.abs(v) * 10) / 10).toString().replace('.', ',')
  return `${v >= 0 ? '▲' : '▼'} ${texto}%`
}

export const plural = (n: number, singular: string, varios: string) => `${n} ${n === 1 ? singular : varios}`
