import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

afterEach(() => {
  cleanup()
  sessionStorage.clear()
  localStorage.clear()
  document.documentElement.className = ''
  vi.restoreAllMocks()
})

// jsdom não implementa matchMedia
window.matchMedia = vi.fn().mockReturnValue({
  matches: false,
  media: '(prefers-color-scheme: dark)',
  addEventListener: () => {},
  removeEventListener: () => {},
}) as unknown as typeof window.matchMedia
