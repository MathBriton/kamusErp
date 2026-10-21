import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
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
  it('mostra só Claro e Escuro', () => {
    renderToggle()
    expect(screen.getAllByRole('radio').map((r) => r.textContent)).toEqual(['Claro', 'Escuro'])
    expect(screen.getByRole('radio', { name: 'Claro' })).toBeChecked()
  })

  it('ao escolher Escuro aplica o tema e salva a preferência', async () => {
    renderToggle()
    await userEvent.click(screen.getByRole('radio', { name: 'Escuro' }))

    expect(document.documentElement).toHaveClass('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
  })

  it('restaura a preferência salva ao abrir', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    renderToggle()
    expect(screen.getByRole('radio', { name: 'Escuro' })).toBeChecked()
    expect(document.documentElement).toHaveClass('dark')
  })
})
