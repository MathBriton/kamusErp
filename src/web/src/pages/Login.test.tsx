import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { empresaAtivaHandler, json, mockApi, problema, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const semSessao = { 'POST /api/auth/refresh': () => problema(401, 'Faça login novamente.') }
const health = { 'GET /health': () => new Response('Degraded'), 'GET /api/empresas/ativa': empresaAtivaHandler() }

async function preencherEEntrar(email: string, senha: string) {
  await userEvent.type(await screen.findByLabelText('E-mail'), email)
  await userEvent.type(screen.getByLabelText('Senha'), senha)
  await userEvent.click(screen.getByRole('button', { name: /entrar/i }))
}

describe('Login', () => {
  it('sem sessão ativa mostra a tela de login', async () => {
    mockApi({ ...semSessao, ...health })
    renderApp()
    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
  })

  it('com sessão ativa (cookie válido) entra direto', async () => {
    mockApi({ 'POST /api/auth/refresh': () => json(sessaoDemo), ...health })
    renderApp()
    expect(await screen.findByText(/Olá, Admin/)).toBeInTheDocument()
  })

  it('login correto mostra o usuário e as empresas que ele acessa', async () => {
    const api = mockApi({ ...semSessao, ...health, 'POST /api/auth/login': () => json(sessaoDemo) })
    renderApp()

    await preencherEEntrar('admin@kamus.local', 'Kamus@Dev123')

    expect(await screen.findByText(/Olá, Admin/)).toBeInTheDocument()
    expect(screen.getAllByText('12.345.678/0001-95').length).toBeGreaterThan(0)
    const chamada = api.mock.calls.find(([url]) => url === '/api/auth/login')!
    expect(JSON.parse(chamada[1]!.body as string)).toEqual({ email: 'admin@kamus.local', senha: 'Kamus@Dev123', manterConectado: false })
  })

  it('credenciais inválidas mostram a mensagem da API', async () => {
    mockApi({ ...semSessao, ...health, 'POST /api/auth/login': () => problema(401, 'E-mail ou senha inválidos.') })
    renderApp()

    await preencherEEntrar('admin@kamus.local', 'errada')

    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha inválidos.')
  })

  it('usuário bloqueado mostra o aviso de bloqueio', async () => {
    mockApi({ ...semSessao, ...health, 'POST /api/auth/login': () => problema(423, 'Usuário bloqueado temporariamente por excesso de tentativas. Tente novamente em 15 minuto(s).') })
    renderApp()

    await preencherEEntrar('admin@kamus.local', 'errada')

    expect(await screen.findByRole('alert')).toHaveTextContent(/bloqueado/)
  })

  it('sem conexão com a API avisa o usuário', async () => {
    mockApi({ ...semSessao, ...health, 'POST /api/auth/login': () => { throw new TypeError('Failed to fetch') } })
    renderApp()

    await preencherEEntrar('admin@kamus.local', 'x')

    expect(await screen.findByRole('alert')).toHaveTextContent(/Não foi possível conectar/)
  })

  it('campos vazios não chamam a API', async () => {
    const api = mockApi({ ...semSessao, ...health })
    renderApp()
    await screen.findByRole('heading', { name: 'Entrar' })

    await userEvent.click(screen.getByRole('button', { name: /entrar/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Informe e-mail e senha.')
    expect(api.mock.calls.some(([url]) => url === '/api/auth/login')).toBe(false)
  })

  it('"Manter conectado" é enviado para a API', async () => {
    const api = mockApi({ ...semSessao, ...health, 'POST /api/auth/login': () => json(sessaoDemo) })
    renderApp()

    await userEvent.click(await screen.findByLabelText('Manter conectado'))
    await preencherEEntrar('admin@kamus.local', 'Kamus@Dev123')

    await screen.findByText(/Olá, Admin/)
    const chamada = api.mock.calls.find(([url]) => url === '/api/auth/login')!
    expect(JSON.parse(chamada[1]!.body as string).manterConectado).toBe(true)
  })

  it('botão de mostrar senha alterna a visibilidade', async () => {
    mockApi({ ...semSessao, ...health })
    renderApp()
    const senha = await screen.findByLabelText('Senha')
    expect(senha).toHaveAttribute('type', 'password')

    await userEvent.click(screen.getByRole('button', { name: 'Mostrar senha' }))

    expect(senha).toHaveAttribute('type', 'text')
  })

  it('sair encerra a sessão e volta para o login', async () => {
    const api = mockApi({
      'POST /api/auth/refresh': () => json(sessaoDemo),
      'POST /api/auth/logout': () => new Response(null, { status: 204 }),
      ...health,
    })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: /Menu do usuário/ }))
    await userEvent.click(screen.getByRole('menuitem', { name: 'Sair' }))

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    await waitFor(() => expect(api.mock.calls.some(([url]) => url === '/api/auth/logout')).toBe(true))
  })
})
