import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { empresaAtivaHandler, json, mockApi, problema, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

function api(empresaAtiva: Parameters<typeof mockApi>[0][string] = empresaAtivaHandler()) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtiva,
    'GET /health': () => new Response('Degraded'),
  })
}

describe('Dashboard', () => {
  it('saúda o usuário e mostra a empresa validada pela API', async () => {
    api()
    renderApp()

    expect(await screen.findByRole('heading', { name: /Olá, Admin/ })).toBeInTheDocument()
    expect(await screen.findByText('Kamus Moda Centro · 12.345.678/0001-95')).toBeInTheDocument()
  })

  it('indicadores ainda sem dados aparecem com estado vazio explicativo', async () => {
    api()
    renderApp()
    const card = (await screen.findByText('Vendas hoje')).closest('article')!

    expect(within(card).getByText('—')).toBeInTheDocument()
    expect(within(card).getByText(/registro de vendas/)).toBeInTheDocument()
  })

  it('primeiros passos levam às telas certas', async () => {
    api()
    renderApp()

    const passo = await screen.findByRole('link', { name: /Cadastrar produtos/ })
    expect(passo).toHaveAttribute('href', '/cadastros/produtos')
  })

  it('visão consolidada mostra quantas empresas estão somadas', async () => {
    api()
    renderApp()
    await screen.findByText('Kamus Moda Centro · 12.345.678/0001-95')

    await userEvent.click(screen.getByRole('button', { name: /Empresa ativa/ }))
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Todas as empresas/ }))

    expect(await screen.findByText('Visão consolidada · 2 empresas (somente leitura)')).toBeInTheDocument()
  })

  it('empresa recusada pela API mostra o motivo', async () => {
    api(() => problema(403, 'Você não tem acesso a esta empresa.'))
    renderApp()

    expect(await screen.findByRole('alert')).toHaveTextContent('Você não tem acesso a esta empresa.')
  })
})
