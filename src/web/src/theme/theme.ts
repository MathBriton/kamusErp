export type ThemePreference = 'light' | 'dark' | 'system'
export type ResolvedTheme = 'light' | 'dark'

/** Mesma chave usada pelo script inline do index.html (evita o "piscar" branco ao carregar). */
export const THEME_STORAGE_KEY = 'kamus.theme'

const PREFERENCES: readonly ThemePreference[] = ['light', 'dark', 'system']

export function resolveTheme(preference: ThemePreference, systemPrefersDark: boolean): ResolvedTheme {
  if (preference === 'system') return systemPrefersDark ? 'dark' : 'light'
  return preference
}

export function readStoredTheme(): ThemePreference {
  try {
    const value = localStorage.getItem(THEME_STORAGE_KEY)
    return PREFERENCES.includes(value as ThemePreference) ? (value as ThemePreference) : 'system'
  } catch {
    return 'system'
  }
}

export function storeTheme(preference: ThemePreference): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, preference)
  } catch {
    // Armazenamento indisponível (modo privado): a escolha vale só para esta sessão.
  }
}

export function applyTheme(theme: ResolvedTheme, root: HTMLElement = document.documentElement): void {
  root.classList.toggle('dark', theme === 'dark')
  root.style.colorScheme = theme
}

export const systemDarkQuery = () => window.matchMedia('(prefers-color-scheme: dark)')
