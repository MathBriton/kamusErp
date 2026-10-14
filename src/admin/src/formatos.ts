import type { Estado } from './api'

/** "2026-10-15" → "15/10/2026" (sem passar por Date, para não sofrer com fuso). */
export const dia = (valor: string | null) => (valor ? valor.slice(0, 10).split('-').reverse().join('/') : '—')

export const moeda = (valor: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor)

export const dataHora = (iso: string) => new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(iso))

export const ESTADOS: Record<Estado, { rotulo: string; classe: string }> = {
  Ativa: { rotulo: 'Ativa', classe: 'bg-success-soft text-success' },
  Trial: { rotulo: 'Trial', classe: 'bg-info-soft text-info' },
  Carencia: { rotulo: 'Carência', classe: 'bg-warning-soft text-warning' },
  Suspensa: { rotulo: 'Suspensa', classe: 'bg-danger-soft text-danger' },
  Cancelada: { rotulo: 'Cancelada', classe: 'bg-surface-2 text-muted' },
  SemLicenca: { rotulo: 'Sem licença', classe: 'bg-surface-2 text-muted' },
}

export const campo = 'h-9 w-full rounded-lg border border-border bg-surface px-2.5 text-sm'
export const botaoPrimario =
  'flex h-9 items-center justify-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground disabled:opacity-60'
export const botao = 'flex h-9 items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 text-sm font-semibold'
