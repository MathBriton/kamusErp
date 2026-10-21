/** Só Claro ou Escuro: sem escolha salva, o primeiro acesso segue o sistema operacional. */
export type ThemePreference = 'light' | 'dark'

/** Mesma chave usada pelo script inline do index.html (evita o "piscar" branco ao carregar). */
export const THEME_STORAGE_KEY = 'kamus.theme'

const PREFERENCES: readonly ThemePreference[] = ['light', 'dark']

const temaDoSistema = (): ThemePreference => (systemDarkQuery().matches ? 'dark' : 'light')

export function readStoredTheme(): ThemePreference {
  try {
    const value = localStorage.getItem(THEME_STORAGE_KEY)
    return PREFERENCES.includes(value as ThemePreference) ? (value as ThemePreference) : temaDoSistema()
  } catch {
    return temaDoSistema()
  }
}

export function storeTheme(preference: ThemePreference): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, preference)
  } catch {
    // Armazenamento indisponível (modo privado): a escolha vale só para esta sessão.
  }
}

export function applyTheme(theme: ThemePreference, root: HTMLElement = document.documentElement): void {
  root.classList.toggle('dark', theme === 'dark')
  root.style.colorScheme = theme
}

export const systemDarkQuery = () => window.matchMedia('(prefers-color-scheme: dark)')
