import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { applyTheme, readStoredTheme, storeTheme, type ThemePreference } from './theme'
import { ThemeContext } from './ThemeContext'

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(readStoredTheme)

  useEffect(() => applyTheme(preference), [preference])

  const setPreference = useCallback((next: ThemePreference) => {
    storeTheme(next)
    setPreferenceState(next)
  }, [])

  const value = useMemo(() => ({ preference, setPreference }), [preference, setPreference])

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
