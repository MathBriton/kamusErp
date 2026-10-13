import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { applyTheme, readStoredTheme, resolveTheme, storeTheme, systemDarkQuery, type ThemePreference } from './theme'
import { ThemeContext } from './ThemeContext'

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(readStoredTheme)
  const [systemDark, setSystemDark] = useState(() => systemDarkQuery().matches)

  useEffect(() => {
    const query = systemDarkQuery()
    const onChange = (e: MediaQueryListEvent) => setSystemDark(e.matches)
    query.addEventListener('change', onChange)
    return () => query.removeEventListener('change', onChange)
  }, [])

  const resolved = resolveTheme(preference, systemDark)

  useEffect(() => applyTheme(resolved), [resolved])

  const setPreference = useCallback((next: ThemePreference) => {
    storeTheme(next)
    setPreferenceState(next)
  }, [])

  const value = useMemo(() => ({ preference, resolved, setPreference }), [preference, resolved, setPreference])

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
