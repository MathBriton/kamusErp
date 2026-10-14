import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const usuarios = [
  { id: 'u1', nome: 'Admin Demo', email: 'admin@kamus.local', ativo: true, deveTrocarSenha: false, ultimoLoginEm: '2026-10-14T12:00:00Z',
    acessos: [{ empresaId: 'e1', empresa: 'Kamus Moda Centro', perfil: 'Dono' }, { empresaId: 'e2', empresa: 'Kamus Moda Shopping', perfil: 'Dono' }] },
  { id: 'u2', nome: 'Vendedora Demo', email: 'vendedor@kamus.local', ativo: true, deveTrocarSenha: false, ultimoLoginEm: null,
    acessos: [{ empresaId: 'e1', empresa: 'Kamus Moda Centro', perfil: 'Vendedor' }] },
  { id: 'u3', nome: 'Ex Funcionária', email: 'ex@kamus.local', ativo: false, deveTrocarSenha: false, ultimoLoginEm: null,
    acessos: [{ empresaId: 'e2', empresa: 'Kamus Moda Shopping', perfil: 'Vendedor' }] },
]
const empresas = [
  { id: 'e1', razaoSocial: 'Kamus Moda Centro LTDA', nomeFantasia: 'Kamus Moda Centro', cnpj: '12.345.678/0001-95' },
  { id: 'e2', razaoSocial: 'Kamus Moda Shopping LTDA', nomeFantasia: 'Kamus Moda Shopping', cnpj: '98.765.432/0001-98' },
]

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/usuarios': () => json(usuarios),
    'GET /api/empresas': () => json(empresas),
    ...extras,
  })
}

const linhaDe = async (email: string) => (await screen.findByText(email)).closest('tr')!

describe('Configurações → Usuários e permissões', () => {
  it('lista usuários com perfil por empresa e situação', async () => {
    api()
    renderApp('/configuracoes/usuarios')

    expect(await screen.findByRole('heading', { name: 'Usuários e permissões' })).toBeInTheDocument()
    const vendedora = await linhaDe('vendedor@kamus.local')
    expect(within(vendedora).getByText('Kamus Moda Centro: Vendedor')).toBeInTheDocument()
    expect(within(await linhaDe('ex@kamus.local')).getByText('Inativo')).toBeInTheDocument()
  })

  it('novo usuário: escolhe perfil por empresa e vê a senha provisória', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/usuarios': (init) => {
        enviado = corpo(init)
        return json({ id: 'u9', email: 'bia@kamus.local', senhaProvisoria: 'Xk7pQ2mZr4Ta' }, 201)
      },
    })
    renderApp('/configuracoes/usuarios')
    await linhaDe('vendedor@kamus.local')

    await userEvent.click(screen.getByRole('button', { name: 'Novo usuário' }))
    const dialogo = screen.getByRole('dialog', { name: 'Novo usuário' })
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Bia Souza')
    await userEvent.type(within(dialogo).getByLabelText('E-mail'), 'bia@kamus.local')
    await userEvent.selectOptions(within(dialogo).getByLabelText('Perfil em Kamus Moda Centro'), 'Vendedor')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toEqual({ nome: 'Bia Souza', email: 'bia@kamus.local', acessos: [{ empresaId: 'e1', perfil: 'Vendedor' }] })
    expect(await screen.findByText('Xk7pQ2mZr4Ta')).toBeInTheDocument()
    expect(screen.getByText(/não será mostrada de novo/)).toBeInTheDocument()
  })

  it('editar: muda perfis e tira acesso a uma empresa', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'PUT /api/usuarios/u1': (init) => {
        enviado = corpo(init)
        return new Response(null, { status: 204 })
      },
    })
    renderApp('/configuracoes/usuarios')
    await userEvent.click(within(await linhaDe('admin@kamus.local')).getByRole('button', { name: 'Editar' }))
    const dialogo = screen.getByRole('dialog', { name: 'Editar usuário' })
    expect(within(dialogo).getByLabelText('E-mail')).toBeDisabled()

    await userEvent.selectOptions(within(dialogo).getByLabelText('Perfil em Kamus Moda Shopping'), '')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toEqual({ nome: 'Admin Demo', acessos: [{ empresaId: 'e1', perfil: 'Dono' }] })
  })

  it('desativar pede confirmação e reativar volta', async () => {
    const chamadas: string[] = []
    api({
      'POST /api/usuarios/u2/desativar': () => {
        chamadas.push('desativar')
        return new Response(null, { status: 204 })
      },
      'POST /api/usuarios/u3/reativar': () => {
        chamadas.push('reativar')
        return new Response(null, { status: 204 })
      },
    })
    renderApp('/configuracoes/usuarios')

    await userEvent.click(within(await linhaDe('vendedor@kamus.local')).getByRole('button', { name: 'Desativar' }))
    await userEvent.click(within(await linhaDe('vendedor@kamus.local')).getByRole('button', { name: 'Confirmar' }))
    await userEvent.click(within(await linhaDe('ex@kamus.local')).getByRole('button', { name: 'Reativar' }))

    expect(chamadas).toEqual(['desativar', 'reativar'])
  })

  it('redefinir senha mostra a nova senha provisória', async () => {
    api({ 'POST /api/usuarios/u2/redefinir-senha': () => json({ id: 'u2', email: 'vendedor@kamus.local', senhaProvisoria: 'Nw3pQ8xZr2Kb' }) })
    renderApp('/configuracoes/usuarios')

    await userEvent.click(within(await linhaDe('vendedor@kamus.local')).getByRole('button', { name: 'Redefinir senha' }))

    expect(await screen.findByText('Nw3pQ8xZr2Kb')).toBeInTheDocument()
  })

  it('vendedor não vê a tela no menu', async () => {
    api({}, sessaoVendedor)
    renderApp()

    await screen.findByRole('heading', { name: /Olá, Vendedora/ })
    expect(screen.queryByRole('link', { name: /Usuários e permissões/ })).not.toBeInTheDocument()
  })
})
