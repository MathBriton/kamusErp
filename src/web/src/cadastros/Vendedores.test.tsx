import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const vendedores = [
  { id: 'v1', nome: 'Vendedora Demo', comissaoPercentual: 3.5, usuarioId: 'u2', usuarioNome: 'Vendedora Demo', telefone: null, ativo: true },
  { id: 'v2', nome: 'Carlos (ex)', comissaoPercentual: 2, usuarioId: null, usuarioNome: null, telefone: null, ativo: false },
]
const usuarios = [{ id: 'u2', nome: 'Vendedora Demo', email: 'vendedor@kamus.local' }, { id: 'u3', nome: 'Bia', email: 'bia@kamus.local' }]

function api(extras: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(),
    'GET /health': () => new Response('Healthy'),
    'GET /api/vendedores': () => json(vendedores),
    'GET /api/vendedores/usuarios': () => json(usuarios),
    ...extras,
  })
}

describe('Cadastros → Vendedores', () => {
  it('lista com comissão, login vinculado e situação', async () => {
    api()
    renderApp('/cadastros/vendedores')

    const linha = (await screen.findByRole('cell', { name: 'Vendedora Demo' })).closest('tr')!
    expect(within(linha).getByText('3,5%')).toBeInTheDocument()
    expect(within(linha).getByText('vinculado: Vendedora Demo')).toBeInTheDocument()
    expect(within((await screen.findByRole('cell', { name: 'Carlos (ex)' })).closest('tr')!).getByText('Inativo')).toBeInTheDocument()
  })

  it('novo vendedor com comissão e usuário', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/vendedores': (init) => {
        enviado = corpo(init)
        return json({ id: 'v3' }, 201)
      },
    })
    renderApp('/cadastros/vendedores')
    await screen.findByRole('cell', { name: 'Vendedora Demo' })

    await userEvent.click(screen.getByRole('button', { name: 'Novo vendedor' }))
    const dialogo = screen.getByRole('dialog', { name: 'Novo vendedor' })
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Bia')
    await userEvent.clear(within(dialogo).getByLabelText('Comissão (%)'))
    await userEvent.type(within(dialogo).getByLabelText('Comissão (%)'), '4,5')
    await userEvent.selectOptions(within(dialogo).getByLabelText('Usuário do sistema'), 'u3')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toEqual({ nome: 'Bia', comissaoPercentual: 4.5, usuarioId: 'u3', telefone: null })
  })

  it('inativar e reativar', async () => {
    const chamadas: string[] = []
    api({
      'POST /api/vendedores/v1/inativar': () => {
        chamadas.push('inativar')
        return new Response(null, { status: 204 })
      },
      'POST /api/vendedores/v2/reativar': () => {
        chamadas.push('reativar')
        return new Response(null, { status: 204 })
      },
    })
    renderApp('/cadastros/vendedores')

    await userEvent.click(within((await screen.findByRole('cell', { name: 'Vendedora Demo' })).closest('tr')!).getByRole('button', { name: 'Inativar' }))
    await userEvent.click(within((await screen.findByRole('cell', { name: 'Carlos (ex)' })).closest('tr')!).getByRole('button', { name: 'Reativar' }))

    expect(chamadas).toEqual(['inativar', 'reativar'])
  })
})
