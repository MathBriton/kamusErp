import { useTheme } from '../theme/ThemeContext'
import type { ThemePreference } from '../theme/theme'

const OPTIONS: { value: ThemePreference; label: string }[] = [
  { value: 'light', label: 'Claro' },
  { value: 'dark', label: 'Escuro' },
]

export function ThemeToggle() {
  const { preference, setPreference } = useTheme()

  return (
    <div role="radiogroup" aria-label="Tema" className="inline-flex rounded-lg border border-border bg-surface-2 p-0.5">
      {OPTIONS.map((option) => {
        const checked = preference === option.value
        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={checked}
            onClick={() => setPreference(option.value)}
            className={`rounded-md px-3 py-1 text-sm font-medium transition-colors ${
              checked ? 'bg-surface text-foreground shadow-sm' : 'text-muted hover:text-foreground'
            }`}
          >
            {option.label}
          </button>
        )
      })}
    </div>
  )
}
