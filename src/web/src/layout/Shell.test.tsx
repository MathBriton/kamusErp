import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { empresaAtivaHandler, json, mockApi, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

function api(sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'POST /api/auth/logout': () => new Response(null, { status: 204 }),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Degraded'),
  })
}

const menuLateral = () => screen.getByRole('navigation', { name: 'Menu principal' })

describe('Shell', () => {
  it('mostra o menu lateral com os módulos', async () => {
    api()
    renderApp()
    await screen.findByText(/Olá, Admin/)
    for (const modulo of ['Dashboard', 'Cadastros', 'Estoque', 'Financeiro', 'Vendas', 'Relatórios', 'Configurações'])
      expect(within(menuLateral()).getByText(modulo)).toBeInTheDocument()
  })

  it('navega pelo menu até a central de relatórios', async () => {
    api()
    renderApp()
    await screen.findByText(/Olá, Admin/)

    await userEvent.click(within(menuLateral()).getByRole('link', { name: /Relatórios/ }))

    expect(await screen.findByRole('heading', { name: 'Relatórios' })).toBeInTheDocument()
    expect(screen.queryByText(/Previsto para a/)).not.toBeInTheDocument()
  })

  it('abrir outro grupo e navegar por ele não fecha os grupos já abertos', async () => {
    api()
    renderApp('/cadastros/produtos')
    await screen.findByRole('link', { name: 'Grades e cores' })
    const grupo = (nome: string) => within(menuLateral()).getByRole('button', { name: nome })
    expect(grupo('Cadastros')).toHaveAttribute('aria-expanded', 'true')

    await userEvent.click(grupo('Estoque'))
    await userEvent.click(within(menuLateral()).getByRole('link', { name: 'Ajustes' }))

    expect(grupo('Estoque')).toHaveAttribute('aria-expanded', 'true')
    expect(grupo('Cadastros')).toHaveAttribute('aria-expanded', 'true')

    await userEvent.click(grupo('Cadastros'))
    expect(grupo('Cadastros')).toHaveAttribute('aria-expanded', 'false')
    expect(grupo('Estoque')).toHaveAttribute('aria-expanded', 'true')
  })

  it('recolher o menu guarda a preferência', async () => {
    api()
    renderApp()
    await screen.findByText(/Olá, Admin/)

    await userEvent.click(screen.getByRole('button', { name: 'Recolher menu' }))

    expect(screen.getByRole('button', { name: 'Expandir menu' })).toBeInTheDocument()
    expect(localStorage.getItem('kamus.menu.recolhido')).toBe('true')
  })

  it('seletor de empresa troca o CNPJ ativo e envia o header para a API', async () => {
    const fetch = api()
    renderApp()
    await screen.findByText(/Olá, Admin/)
    const seletor = screen.getByRole('button', { name: /Empresa ativa/ })
    expect(seletor).toHaveTextContent('Kamus Moda Centro')

    await userEvent.click(seletor)
    const opcoes = screen.getByRole('menu', { name: 'Trocar empresa' })
    expect(within(opcoes).getByText('Todas as empresas')).toBeInTheDocument()
    await userEvent.click(within(opcoes).getByRole('menuitemradio', { name: /Kamus Moda Shopping/ }))

    expect(screen.getByRole('button', { name: /Empresa ativa/ })).toHaveTextContent('Kamus Moda Shopping')
    expect(localStorage.getItem('kamus.empresa.u1')).toBe('e2')
    expect(await screen.findByText(/98\.765\.432\/0001-98/, { selector: 'p' })).toBeInTheDocument()
    const ultima = fetch.mock.calls.filter(([url]) => url === '/api/empresas/ativa').at(-1)!
    expect(new Headers(ultima[1]?.headers).get('X-Empresa-Id')).toBe('e2')
    expect(new Headers(ultima[1]?.headers).get('Authorization')).toBe('Bearer token-de-teste')
  })

  it('restaura a última empresa escolhida pelo usuário', async () => {
    localStorage.setItem('kamus.empresa.u1', 'e2')
    api()
    renderApp()

    expect(await screen.findByRole('button', { name: /Empresa ativa/ })).toHaveTextContent('Kamus Moda Shopping')
  })

  it('vendedora não vê Financeiro nem a visão consolidada', async () => {
    api(sessaoVendedor)
    renderApp()
    await screen.findByText(/Olá, Vendedora/)

    expect(within(menuLateral()).queryByText('Financeiro')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Empresa ativa/ }))
    expect(screen.queryByText('Todas as empresas')).not.toBeInTheDocument()
  })

  it('menu do usuário mostra o perfil e permite sair', async () => {
    api()
    renderApp()
    await screen.findByText(/Olá, Admin/)

    await userEvent.click(screen.getByRole('button', { name: /Menu do usuário/ }))
    const menu = screen.getByRole('menu', { name: 'Menu do usuário' })
    expect(within(menu).getByText('admin@kamus.local')).toBeInTheDocument()
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Sair' }))

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
  })

  it('rota inexistente mostra página não encontrada', async () => {
    api()
    renderApp('/nao-existe')

    expect(await screen.findByRole('heading', { name: 'Página não encontrada' })).toBeInTheDocument()
  })
})
