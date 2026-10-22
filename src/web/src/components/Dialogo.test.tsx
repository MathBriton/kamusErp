import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { Dialogo } from './Dialogo'

function Exemplo() {
  const [aberto, setAberto] = useState(false)
  return (
    <>
      <input aria-label="Buscar no fundo" />
      <button type="button" onClick={() => setAberto(true)}>Novo produto</button>
      {aberto && (
        <Dialogo titulo="Novo produto" onFechar={() => setAberto(false)}>
          <input aria-label="Referência" />
          <input aria-label="Descrição" />
          <button type="button">Salvar</button>
        </Dialogo>
      )}
    </>
  )
}

describe('Dialogo', () => {
  it('ao abrir, o foco vai para o primeiro campo do formulário', async () => {
    render(<Exemplo />)
    await userEvent.click(screen.getByRole('button', { name: 'Novo produto' }))

    expect(screen.getByLabelText('Referência')).toHaveFocus()
  })

  it('Tab e Shift+Tab ficam presos dentro do diálogo', async () => {
    render(<Exemplo />)
    await userEvent.click(screen.getByRole('button', { name: 'Novo produto' }))

    await userEvent.tab()
    await userEvent.tab()
    expect(screen.getByRole('button', { name: 'Salvar' })).toHaveFocus()
    await userEvent.tab()
    expect(screen.getByRole('dialog').contains(document.activeElement)).toBe(true)
    expect(screen.getByLabelText('Buscar no fundo')).not.toHaveFocus()

    screen.getByLabelText('Referência').focus()
    await userEvent.tab({ shift: true })
    expect(screen.getByRole('dialog').contains(document.activeElement)).toBe(true)
  })

  it('ao fechar com Esc, o foco volta para o botão que abriu', async () => {
    render(<Exemplo />)
    await userEvent.click(screen.getByRole('button', { name: 'Novo produto' }))

    await userEvent.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Novo produto' })).toHaveFocus()
  })
})
