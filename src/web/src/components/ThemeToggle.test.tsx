import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockPrefersDark } from '../test/setup'
import { ThemeProvider } from '../theme/ThemeProvider'
import { THEME_STORAGE_KEY } from '../theme/theme'
import { ThemeToggle } from './ThemeToggle'

function renderToggle() {
  return render(
    <ThemeProvider>
      <ThemeToggle />
    </ThemeProvider>,
  )
}

describe('ThemeToggle', () => {
  it('mostra as três opções e começa em "Sistema"', () => {
    renderToggle()
    expect(screen.getByRole('radio', { name: 'Claro' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Escuro' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Sistema' })).toBeChecked()
  })

  it('ao escolher Escuro aplica o tema e salva a preferência', async () => {
    renderToggle()
    await userEvent.click(screen.getByRole('radio', { name: 'Escuro' }))

    expect(document.documentElement).toHaveClass('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
  })

  it('no modo Sistema acompanha a mudança do sistema operacional em tempo real', () => {
    const media = mockPrefersDark(false)
    renderToggle()
    expect(document.documentElement).not.toHaveClass('dark')

    act(() => media.change(true))

    expect(document.documentElement).toHaveClass('dark')
  })

  it('restaura a preferência salva ao abrir', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    renderToggle()
    expect(screen.getByRole('radio', { name: 'Escuro' })).toBeChecked()
    expect(document.documentElement).toHaveClass('dark')
  })
})
