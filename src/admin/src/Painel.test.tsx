import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { App } from './App'
import { acoes, clienteDemo, clienteSuspenso, corpo, json, mockApi, problema, resumo, sessaoAdmin, TENANT } from './test/mockApi'

const entrarComSessao = () => sessionStorage.setItem('kamus.admin.sessao', JSON.stringify(sessaoAdmin))

function api(extras: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    'POST /admin-api/login': (init) =>
      corpo(init).senha === 'Kamus@Admin123' ? json(sessaoAdmin) : problema(401, 'E-mail ou senha inválidos.'),
    'GET /admin-api/resumo': () => json(resumo),
    'GET /admin-api/clientes': () => json([clienteDemo, clienteSuspenso]),
    'GET /admin-api/acoes': () => json(acoes),
    ...extras,
  })
}

async function linhaDo(nome: string) {
  return (await screen.findByRole('cell', { name: nome })).closest('tr')!
}

describe('Login do Painel Administrativo', () => {
  it('senha errada mostra o motivo', async () => {
    api()
    render(<App />)

    await userEvent.type(screen.getByLabelText('E-mail'), 'superadmin@kamus.local')
    await userEvent.type(screen.getByLabelText('Senha'), 'errada')
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha inválidos.')
  })

  it('entra e mostra os clientes com a situação da licença', async () => {
    api()
    render(<App />)

    await userEvent.type(screen.getByLabelText('E-mail'), 'superadmin@kamus.local')
    await userEvent.type(screen.getByLabelText('Senha'), 'Kamus@Admin123')
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))

    const linha = await linhaDo('Loja Demo')
    expect(within(linha).getByText('Ativa')).toBeInTheDocument()
    expect(within(linha).getByText('07/11/2026')).toBeInTheDocument()
    expect(within(await linhaDo('Estilo Urbano')).getByText('Suspensa')).toBeInTheDocument()
    expect(screen.getByText('Super Admin')).toBeInTheDocument()
  })

  it('sessão expirada (401) volta ao login com aviso', async () => {
    entrarComSessao()
    api({ 'GET /admin-api/clientes': () => problema(401, 'Não autorizado') })
    render(<App />)

    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Sua sessão expirou')
  })

  it('sair limpa a sessão', async () => {
    entrarComSessao()
    api()
    render(<App />)
    await linhaDo('Loja Demo')

    await userEvent.click(screen.getByRole('button', { name: 'Sair' }))

    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument()
    expect(sessionStorage.getItem('kamus.admin.sessao')).toBeNull()
  })
})

describe('Painel', () => {
  it('mostra os indicadores e as ações recentes', async () => {
    entrarComSessao()
    api()
    render(<App />)

    const indicadores = await screen.findByRole('region', { name: 'Indicadores' })
    expect(within(indicadores).getByText('Suspensas').closest('div')).toHaveTextContent('1')
    expect(within(indicadores).getByText('R$ 199,90')).toBeInTheDocument()
    expect(await screen.findByText('Entrou no Painel Administrativo')).toBeInTheDocument()
  })

  it('filtra por situação e busca por nome ou e-mail', async () => {
    entrarComSessao()
    api()
    render(<App />)
    await linhaDo('Loja Demo')

    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'Suspensa')
    expect(screen.queryByRole('cell', { name: 'Loja Demo' })).not.toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText('Situação'), '')
    await userEvent.type(screen.getByLabelText('Buscar cliente'), 'admin@kamus')
    expect(screen.getByRole('cell', { name: 'Loja Demo' })).toBeInTheDocument()
    expect(screen.queryByRole('cell', { name: 'Estilo Urbano' })).not.toBeInTheDocument()
  })

  it('registrar pagamento envia valor, meses e forma e atualiza a lista', async () => {
    entrarComSessao()
    let enviado: Record<string, unknown> | null = null
    let liberado = false
    api({
      'GET /admin-api/clientes': () => json([clienteDemo, liberado ? { ...clienteSuspenso, estado: 'Ativa' } : clienteSuspenso]),
      [`POST /admin-api/clientes/${clienteSuspenso.tenantId}/pagamentos`]: (init) => {
        enviado = corpo(init)
        liberado = true
        return json({ ...clienteSuspenso, estado: 'Ativa', vencimento: '2026-11-14' })
      },
    })
    render(<App />)
    const linha = await linhaDo('Estilo Urbano')

    await userEvent.click(within(linha).getByRole('button', { name: 'Registrar pagamento' }))
    const dialogo = screen.getByRole('dialog', { name: 'Registrar pagamento — Estilo Urbano' })
    expect(within(dialogo).getByLabelText('Valor (R$)')).toHaveValue(199.9)
    await userEvent.selectOptions(within(dialogo).getByLabelText('Forma'), 'Pix')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Registrar e liberar' }))

    expect(enviado).toMatchObject({ valor: 199.9, meses: 1, forma: 'Pix' })
    expect(await screen.findByRole('status')).toHaveTextContent('Pagamento registrado')
    expect(within(await linhaDo('Estilo Urbano')).getByText('Ativa')).toBeInTheDocument()
  })

  it('novo cliente mostra a senha provisória uma única vez', async () => {
    entrarComSessao()
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /admin-api/clientes': (init) => {
        enviado = corpo(init)
        return json({ tenantId: 'novo', emailDoDono: 'ana@flordelis.com.br', senhaProvisoria: 'Xk7pQ2mZr4Ta' }, 201)
      },
    })
    render(<App />)
    await linhaDo('Loja Demo')

    await userEvent.click(screen.getByRole('button', { name: 'Novo cliente' }))
    const dialogo = screen.getByRole('dialog', { name: 'Novo cliente' })
    await userEvent.type(within(dialogo).getByLabelText('Nome do cliente'), 'Ateliê Flor de Lis')
    await userEvent.type(within(dialogo).getByLabelText('Razão social'), 'Flor de Lis Moda LTDA')
    await userEvent.type(within(dialogo).getByLabelText('Nome fantasia'), 'Ateliê Flor de Lis')
    await userEvent.type(within(dialogo).getByLabelText('CNPJ'), '11.222.333/0001-81')
    await userEvent.type(within(dialogo).getByLabelText('Nome do dono'), 'Ana Flor')
    await userEvent.type(within(dialogo).getByLabelText('E-mail do dono'), 'ana@flordelis.com.br')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Criar cliente' }))

    expect(enviado).toMatchObject({ nome: 'Ateliê Flor de Lis', cnpj: '11.222.333/0001-81', emailDoDono: 'ana@flordelis.com.br', valorMensal: 199.9 })
    expect(await screen.findByText('Xk7pQ2mZr4Ta')).toBeInTheDocument()
    expect(screen.getByText(/não será mostrada de novo/)).toBeInTheDocument()
  })

  it('erro ao criar cliente aparece no formulário', async () => {
    entrarComSessao()
    api({ 'POST /admin-api/clientes': () => problema(409, 'Já existe um usuário com este e-mail.') })
    render(<App />)
    await linhaDo('Loja Demo')

    await userEvent.click(screen.getByRole('button', { name: 'Novo cliente' }))
    const dialogo = screen.getByRole('dialog', { name: 'Novo cliente' })
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Criar cliente' }))

    expect(await within(dialogo).findByRole('alert')).toHaveTextContent('Já existe um usuário com este e-mail.')
  })

  it('detalhe do cliente mostra pagamentos e permite cancelar a licença', async () => {
    entrarComSessao()
    let cancelou = false
    api({
      [`GET /admin-api/clientes/${TENANT}`]: () =>
        json({
          cliente: clienteDemo,
          pagamentos: [{ id: 'p1', valor: 199.9, meses: 1, pagoEm: '2026-10-07', estendeuAte: '2026-11-07', forma: 'Pix', observacao: null, registradoPor: 'seed', registradoEm: '2026-10-07T12:00:00Z' }],
          acoes: [],
        }),
      [`POST /admin-api/clientes/${TENANT}/cancelar`]: () => {
        cancelou = true
        return new Response(null, { status: 204 })
      },
    })
    render(<App />)

    await userEvent.click(await screen.findByRole('button', { name: 'Detalhes de Loja Demo' }))
    const painel = await screen.findByRole('dialog', { name: 'Loja Demo' })
    expect(await within(painel).findByRole('table', { name: 'Pagamentos' })).toHaveTextContent('07/10/2026')

    await userEvent.click(within(painel).getByRole('button', { name: 'Cancelar licença' }))
    await userEvent.click(within(painel).getByRole('button', { name: 'Confirmar cancelamento' }))

    expect(cancelou).toBe(true)
  })
})
