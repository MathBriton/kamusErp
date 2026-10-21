import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const empresas = [
  { id: 'e1', razaoSocial: 'Kamus Moda Centro LTDA', nomeFantasia: 'Kamus Moda Centro', cnpj: '12.345.678/0001-95' },
  { id: 'e2', razaoSocial: 'Kamus Moda Shopping LTDA', nomeFantasia: 'Kamus Moda Shopping', cnpj: '98.765.432/0001-98' },
]

function api(extras: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(),
    'GET /health': () => new Response('Healthy'),
    'GET /api/empresas': () => json(empresas),
    ...extras,
  })
}

describe('Administrativo → Empresas', () => {
  it('lista os CNPJs do cliente', async () => {
    api()
    renderApp('/configuracoes/empresas')

    expect(await screen.findByRole('heading', { name: 'Empresas' })).toBeInTheDocument()
    expect(await screen.findByText('Kamus Moda Shopping LTDA')).toBeInTheDocument()
    expect(screen.getByText('98.765.432/0001-98')).toBeInTheDocument()
  })

  it('cadastra empresa e avisa o limite do plano quando atingido', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/empresas': (init) => {
        enviado = corpo(init)
        return new Response(JSON.stringify({ title: 'Limite do plano', detail: 'Seu plano permite até 2 CNPJs. Fale com o suporte para ampliar.', codigo: 'LIMITE_DA_LICENCA' }), {
          status: 409, headers: { 'Content-Type': 'application/problem+json' },
        })
      },
    })
    renderApp('/configuracoes/empresas')
    await screen.findByText('Kamus Moda Shopping LTDA')

    await userEvent.click(screen.getByRole('button', { name: 'Nova empresa' }))
    const dialogo = screen.getByRole('dialog', { name: 'Nova empresa' })
    await userEvent.type(within(dialogo).getByLabelText('Razão social'), 'Kamus Outlet LTDA')
    await userEvent.type(within(dialogo).getByLabelText('Nome fantasia'), 'Kamus Outlet')
    await userEvent.type(within(dialogo).getByLabelText('CNPJ'), '11.222.333/0001-81')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toEqual({ razaoSocial: 'Kamus Outlet LTDA', nomeFantasia: 'Kamus Outlet', cnpj: '11.222.333/0001-81' })
    expect(await within(dialogo).findByRole('alert')).toHaveTextContent('Seu plano permite até 2 CNPJs')
  })

  it('edita nomes sem mexer no CNPJ', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'PUT /api/empresas/e2': (init) => {
        enviado = corpo(init)
        return new Response(null, { status: 204 })
      },
    })
    renderApp('/configuracoes/empresas')
    const linha = (await screen.findByText('Kamus Moda Shopping LTDA')).closest('tr')!

    await userEvent.click(within(linha).getByRole('button', { name: 'Editar' }))
    const dialogo = screen.getByRole('dialog', { name: 'Editar empresa' })
    expect(within(dialogo).getByLabelText('CNPJ')).toBeDisabled()
    await userEvent.clear(within(dialogo).getByLabelText('Nome fantasia'))
    await userEvent.type(within(dialogo).getByLabelText('Nome fantasia'), 'Kamus Shopping')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toEqual({ razaoSocial: 'Kamus Moda Shopping LTDA', nomeFantasia: 'Kamus Shopping' })
  })
})
