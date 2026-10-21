import { screen, waitFor, within } from '@testing-library/react'
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
    'GET /api/usuarios?situacao=todos&pagina=1&tamanho=20': () => json({ itens: usuarios, total: usuarios.length, pagina: 1, tamanho: 20 }),
    'GET /api/empresas': () => json(empresas),
    ...extras,
  })
}

const linhaDe = async (email: string) => (await screen.findByText(email)).closest('tr')!

describe('Administrativo → Usuários e senhas', () => {
  it('grade com perfil por empresa, situação e total de registros', async () => {
    api()
    renderApp('/configuracoes/usuarios')

    expect(await screen.findByRole('heading', { name: 'Usuários e senhas' })).toBeInTheDocument()
    const vendedora = await linhaDe('vendedor@kamus.local')
    expect(within(vendedora).getByText('Kamus Moda Centro: Vendedor')).toBeInTheDocument()
    expect(within(await linhaDe('ex@kamus.local')).getByText('Inativo')).toBeInTheDocument()
    expect(screen.getByText('3 registros')).toBeInTheDocument()
    // Para si mesmo não há redefinir senha nem desativar.
    const eu = await linhaDe('admin@kamus.local')
    expect(within(eu).queryByRole('button', { name: /Desativar/ })).not.toBeInTheDocument()
    expect(within(eu).queryByRole('button', { name: /Redefinir senha/ })).not.toBeInTheDocument()
  })

  it('filtros de situação, empresa e perfil', async () => {
    const pagina = (itens: typeof usuarios) => json({ itens, total: itens.length, pagina: 1, tamanho: 20 })
    api({
      'GET /api/usuarios?situacao=inativos&pagina=1&tamanho=20': () => pagina([usuarios[2]!]),
      'GET /api/usuarios?situacao=inativos&empresaId=e2&pagina=1&tamanho=20': () => pagina([usuarios[2]!]),
      'GET /api/usuarios?situacao=inativos&empresaId=e2&perfil=Gerente&pagina=1&tamanho=20': () => pagina([]),
    })
    renderApp('/configuracoes/usuarios')
    await linhaDe('vendedor@kamus.local')

    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'inativos')
    expect(await screen.findByText('1 registro')).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByLabelText('Empresa'), 'e2')
    await userEvent.selectOptions(screen.getByLabelText('Perfil'), 'Gerente')
    expect(await screen.findByText('Nenhum usuário encontrado com esses filtros.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }))
    expect(await screen.findByText('3 registros')).toBeInTheDocument()
  })

  it('seleção: desativa em lote e exporta só os selecionados', async () => {
    let lote: Record<string, unknown> | null = null
    const fetch = api({
      'POST /api/usuarios/situacao': (init) => {
        lote = corpo(init)
        return json({ alterados: 1 })
      },
      'GET /api/relatorios/usuarios.csv?ids=u2': () => new Response('csv', { headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=usuarios.csv' } }),
    })
    URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
    URL.revokeObjectURL = () => {}
    renderApp('/configuracoes/usuarios')
    await linhaDe('vendedor@kamus.local')

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Vendedora Demo' }))
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/usuarios.csv?ids=u2')).toBe(true))

    await userEvent.click(within(screen.getByRole('region', { name: 'Seleção' })).getByRole('button', { name: 'Desativar selecionados' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Desativar usuários' })).getByRole('button', { name: 'Desativar' }))
    await waitFor(() => expect(lote).toEqual({ ids: ['u2'], ativo: false }))
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

  it('novo usuário com senha definida pelo Dono envia a senha e confere a repetição', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/usuarios': (init) => {
        enviado = corpo(init)
        return json({ id: 'u9', email: 'bia@kamus.local', senhaProvisoria: 'Loja2026bia' }, 201)
      },
    })
    renderApp('/configuracoes/usuarios')
    await linhaDe('vendedor@kamus.local')

    await userEvent.click(screen.getByRole('button', { name: 'Novo usuário' }))
    const dialogo = screen.getByRole('dialog', { name: 'Novo usuário' })
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Bia Souza')
    await userEvent.type(within(dialogo).getByLabelText('E-mail'), 'bia@kamus.local')
    await userEvent.selectOptions(within(dialogo).getByLabelText('Perfil em Kamus Moda Centro'), 'Vendedor')
    await userEvent.click(within(dialogo).getByRole('radio', { name: 'Definir a senha agora' }))
    await userEvent.type(within(dialogo).getByLabelText('Senha'), 'Loja2026bia')
    await userEvent.type(within(dialogo).getByLabelText('Repita a senha'), 'Loja2026bix')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))
    expect(within(dialogo).getByRole('alert')).toHaveTextContent('As senhas não conferem.')
    expect(enviado).toBeNull()

    await userEvent.clear(within(dialogo).getByLabelText('Repita a senha'))
    await userEvent.type(within(dialogo).getByLabelText('Repita a senha'), 'Loja2026bia')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toEqual({ nome: 'Bia Souza', email: 'bia@kamus.local', senha: 'Loja2026bia', acessos: [{ empresaId: 'e1', perfil: 'Vendedor' }] })
    expect(await screen.findByText('Loja2026bia')).toBeInTheDocument()
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
    await userEvent.click(within(await linhaDe('admin@kamus.local')).getByRole('button', { name: 'Editar Admin Demo' }))
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

    await userEvent.click(within(await linhaDe('vendedor@kamus.local')).getByRole('button', { name: 'Desativar Vendedora Demo' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Desativar usuário' })).getByRole('button', { name: 'Desativar' }))
    await waitFor(() => expect(chamadas).toEqual(['desativar']))
    await userEvent.click(within(await linhaDe('ex@kamus.local')).getByRole('button', { name: 'Reativar Ex Funcionária' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Reativar usuário' })).getByRole('button', { name: 'Reativar' }))

    await waitFor(() => expect(chamadas).toEqual(['desativar', 'reativar']))
  })

  it('redefinir senha mostra a nova senha provisória', async () => {
    api({ 'POST /api/usuarios/u2/redefinir-senha': () => json({ id: 'u2', email: 'vendedor@kamus.local', senhaProvisoria: 'Nw3pQ8xZr2Kb' }) })
    renderApp('/configuracoes/usuarios')

    await userEvent.click(within(await linhaDe('vendedor@kamus.local')).getByRole('button', { name: 'Redefinir senha de Vendedora Demo' }))

    expect(await screen.findByText('Nw3pQ8xZr2Kb')).toBeInTheDocument()
  })

  it('vendedor não vê a tela no menu', async () => {
    api({}, sessaoVendedor)
    renderApp()

    await screen.findByRole('heading', { name: /Olá, Vendedora/ })
    expect(screen.queryByRole('link', { name: /Usuários e senhas/ })).not.toBeInTheDocument()
  })
})
