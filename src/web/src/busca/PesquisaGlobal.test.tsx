import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { empresaAtivaHandler, json, mockApi, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const vazio = (q: string) => () => json({ termo: q, grupos: [] })

function api(sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Degraded'),
    'GET /api/notificacoes': () => json({ itens: [], naoLidas: 0 }),
    'GET /api/busca?q=shop': () =>
      json({
        termo: 'shop',
        grupos: [{ grupo: 'Empresas', itens: [{ tipo: 'empresa', id: 'e2', titulo: 'Kamus Moda Shopping', subtitulo: '98.765.432/0001-98', link: null }] }],
      }),
    'GET /api/busca?q=contas': vazio('contas'),
    'GET /api/busca?q=contas%20a': vazio('contas a'),
    'GET /api/busca?q=xyz': vazio('xyz'),
  })
}

async function abrirComAtalho() {
  await screen.findByText(/Olá,/)
  await userEvent.keyboard('{Control>}k{/Control}')
  return screen.getByRole('dialog', { name: 'Pesquisa global' })
}

describe('Pesquisa global (Ctrl+K)', () => {
  it('abre com Ctrl+K e fecha com Esc', async () => {
    api()
    renderApp()

    await abrirComAtalho()
    expect(screen.getByRole('textbox', { name: 'Pesquisar' })).toHaveFocus()

    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: 'Pesquisa global' })).not.toBeInTheDocument()
  })

  it('abre ao clicar no campo de pesquisa da barra superior', async () => {
    api()
    renderApp()
    await screen.findByText(/Olá,/)

    await userEvent.click(screen.getByRole('button', { name: 'Pesquisar (Ctrl+K)' }))

    expect(screen.getByRole('dialog', { name: 'Pesquisa global' })).toBeInTheDocument()
  })

  it('encontra telas do menu e Enter abre a primeira', async () => {
    api()
    renderApp()
    await abrirComAtalho()

    await userEvent.type(screen.getByRole('textbox', { name: 'Pesquisar' }), 'contas a')
    expect(screen.getByRole('option', { name: /Contas a pagar/ })).toHaveAttribute('aria-selected', 'true')
    await userEvent.keyboard('{Enter}')

    expect(await screen.findByRole('heading', { name: 'Contas a pagar' })).toBeInTheDocument()
  })

  it('setas mudam o resultado selecionado', async () => {
    api()
    renderApp()
    await abrirComAtalho()

    await userEvent.type(screen.getByRole('textbox', { name: 'Pesquisar' }), 'contas')
    await userEvent.keyboard('{ArrowDown}{Enter}')

    expect(await screen.findByRole('heading', { name: 'Contas a receber' })).toBeInTheDocument()
  })

  it('mostra resultados da API e escolher uma empresa troca o CNPJ ativo', async () => {
    api()
    renderApp()
    const dialogo = await abrirComAtalho()

    await userEvent.type(screen.getByRole('textbox', { name: 'Pesquisar' }), 'shop')
    await userEvent.click(await within(dialogo).findByRole('option', { name: /Kamus Moda Shopping/ }))

    expect(screen.queryByRole('dialog', { name: 'Pesquisa global' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Empresa ativa/ })).toHaveTextContent('Kamus Moda Shopping')
  })

  it('vendedora não encontra telas que não pode acessar', async () => {
    api(sessaoVendedor)
    renderApp()
    await abrirComAtalho()

    await userEvent.type(screen.getByRole('textbox', { name: 'Pesquisar' }), 'contas')

    expect(screen.queryByRole('option', { name: /Contas a pagar/ })).not.toBeInTheDocument()
    expect(await screen.findByText('Nenhum resultado para "contas".')).toBeInTheDocument()
  })
})
