import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const provisoria = { ...sessaoDemo, usuario: { ...sessaoDemo.usuario, deveTrocarSenha: true } }

describe('Troca de senha', () => {
  it('senha provisória: obriga a trocar antes de mostrar o sistema', async () => {
    let enviado: Record<string, unknown> | null = null
    let renovacoes = 0
    mockApi({
      'POST /api/auth/refresh': () => json(renovacoes++ === 0 ? provisoria : sessaoDemo),
      'POST /api/auth/trocar-senha': (init) => {
        enviado = corpo(init)
        return new Response(null, { status: 204 })
      },
      'GET /api/empresas/ativa': empresaAtivaHandler(),
      'GET /health': () => new Response('Healthy'),
    })
    renderApp()

    expect(await screen.findByRole('heading', { name: 'Crie sua senha' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /Olá, Admin/ })).not.toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Senha atual (provisória)'), 'Xk7pQ2mZr4Ta')
    await userEvent.type(screen.getByLabelText('Nova senha'), 'MinhaSenha2026')
    await userEvent.type(screen.getByLabelText('Repita a nova senha'), 'MinhaSenha2026')
    await userEvent.click(screen.getByRole('button', { name: 'Salvar senha' }))

    expect(enviado).toEqual({ senhaAtual: 'Xk7pQ2mZr4Ta', novaSenha: 'MinhaSenha2026' })
    expect(await screen.findByRole('heading', { name: /Olá, Admin/ })).toBeInTheDocument()
  })

  it('confirmação diferente não envia', async () => {
    mockApi({ 'POST /api/auth/refresh': () => json(provisoria), 'POST /api/auth/logout': () => new Response(null, { status: 204 }) })
    renderApp()
    await screen.findByRole('heading', { name: 'Crie sua senha' })

    await userEvent.type(screen.getByLabelText('Senha atual (provisória)'), 'Xk7pQ2mZr4Ta')
    await userEvent.type(screen.getByLabelText('Nova senha'), 'MinhaSenha2026')
    await userEvent.type(screen.getByLabelText('Repita a nova senha'), 'OutraSenha2026')
    await userEvent.click(screen.getByRole('button', { name: 'Salvar senha' }))

    expect(screen.getByRole('alert')).toHaveTextContent('As senhas não conferem.')
  })

  it('pelo menu do usuário, qualquer um troca a própria senha', async () => {
    mockApi({
      'POST /api/auth/refresh': () => json(sessaoDemo),
      'POST /api/auth/trocar-senha': () => problema(400, 'A senha atual não confere.'),
      'GET /api/empresas/ativa': empresaAtivaHandler(),
      'GET /health': () => new Response('Healthy'),
    })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: /Menu do usuário/ }))
    await userEvent.click(screen.getByRole('menuitem', { name: 'Trocar senha' }))
    const dialogo = screen.getByRole('dialog', { name: 'Trocar senha' })
    await userEvent.type(within(dialogo).getByLabelText('Senha atual'), 'errada')
    await userEvent.type(within(dialogo).getByLabelText('Nova senha'), 'MinhaSenha2026')
    await userEvent.type(within(dialogo).getByLabelText('Repita a nova senha'), 'MinhaSenha2026')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar senha' }))

    expect(await within(dialogo).findByRole('alert')).toHaveTextContent('A senha atual não confere.')
  })
})
