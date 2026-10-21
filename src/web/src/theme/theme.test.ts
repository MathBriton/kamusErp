import { describe, expect, it } from 'vitest'
import { mockPrefersDark } from '../test/setup'
import { applyTheme, readStoredTheme, storeTheme, THEME_STORAGE_KEY } from './theme'

describe('preferência salva', () => {
  it('sem nada salvo, começa pelo tema do sistema operacional (só Claro ou Escuro)', () => {
    mockPrefersDark(true)
    expect(readStoredTheme()).toBe('dark')
    mockPrefersDark(false)
    expect(readStoredTheme()).toBe('light')
  })

  it('ignora valores inválidos e o antigo "system"', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'roxo')
    expect(readStoredTheme()).toBe('light')
    localStorage.setItem(THEME_STORAGE_KEY, 'system')
    expect(readStoredTheme()).toBe('light')
  })

  it('salva e lê a escolha', () => {
    storeTheme('dark')
    expect(readStoredTheme()).toBe('dark')
  })
})

describe('applyTheme', () => {
  it('adiciona a classe dark e o color-scheme no <html>', () => {
    applyTheme('dark')
    expect(document.documentElement).toHaveClass('dark')
    expect(document.documentElement.style.colorScheme).toBe('dark')
  })

  it('remove a classe dark no tema claro', () => {
    document.documentElement.classList.add('dark')
    applyTheme('light')
    expect(document.documentElement).not.toHaveClass('dark')
    expect(document.documentElement.style.colorScheme).toBe('light')
  })
})
