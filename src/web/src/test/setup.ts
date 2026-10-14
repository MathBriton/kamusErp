import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { limparSignalr } from './signalrFake'

// Nenhum teste abre conexão real de tempo real.
vi.mock('@microsoft/signalr', () => import('./signalrFake'))

afterEach(() => {
  cleanup()
  limparSignalr()
  localStorage.clear()
  document.documentElement.className = ''
  vi.restoreAllMocks()
})

// jsdom não implementa matchMedia
export function mockPrefersDark(dark: boolean) {
  const listeners = new Set<(e: MediaQueryListEvent) => void>()
  const mql = {
    matches: dark,
    media: '(prefers-color-scheme: dark)',
    addEventListener: (_: string, cb: (e: MediaQueryListEvent) => void) => listeners.add(cb),
    removeEventListener: (_: string, cb: (e: MediaQueryListEvent) => void) => listeners.delete(cb),
  }
  window.matchMedia = vi.fn().mockReturnValue(mql) as unknown as typeof window.matchMedia
  return {
    change(next: boolean) {
      mql.matches = next
      listeners.forEach((cb) => cb({ matches: next } as MediaQueryListEvent))
    },
  }
}

mockPrefersDark(false)
