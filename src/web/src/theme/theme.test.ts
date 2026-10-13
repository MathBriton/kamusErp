import { describe, expect, it } from 'vitest'
import { applyTheme, readStoredTheme, resolveTheme, storeTheme, THEME_STORAGE_KEY } from './theme'

describe('resolveTheme', () => {
  it('respeita a escolha explícita do usuário', () => {
    expect(resolveTheme('light', true)).toBe('light')
    expect(resolveTheme('dark', false)).toBe('dark')
  })

  it('no modo sistema segue a preferência do sistema operacional', () => {
    expect(resolveTheme('system', true)).toBe('dark')
    expect(resolveTheme('system', false)).toBe('light')
  })
})

describe('preferência salva', () => {
  it('retorna "system" quando não há nada salvo', () => {
    expect(readStoredTheme()).toBe('system')
  })

  it('ignora valores inválidos', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'roxo')
    expect(readStoredTheme()).toBe('system')
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
