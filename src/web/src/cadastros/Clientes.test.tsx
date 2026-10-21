import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const maria = {
  id: 'p1', tipo: 'Fisica', nome: 'Maria Silva', nomeFantasia: null, documento: '529.982.247-25', ehCliente: true, ehFornecedor: false,
  telefone: '11987654321', whatsApp: '5511987654321', email: 'maria@email.com', dataNascimento: '1990-10-16',
  endereco: { cep: null, logradouro: null, numero: null, complemento: null, bairro: null, cidade: 'Santos', uf: 'SP' },
  preferencias: { blusa: 'M', calca: '40', calcado: '37', estilo: null }, observacoes: null,
  aceitaMarketing: true, consentimentoMarketingEm: '2026-10-01T12:00:00Z', ativo: true, criadoEm: '2026-10-01T12:00:00Z', anonimizadoEm: null,
}
const pagina = (itens: unknown[], total = itens.length, numero = 1) => json({ itens, total, pagina: numero, tamanho: 20 })
const aniversariantes = [{ id: 'p1', nome: 'Maria Silva', dataNascimento: '1990-10-16', idade: 36, whatsApp: '5511987654321', aceitaMarketing: true }]

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/pessoas?papel=cliente&situacao=ativos&pagina=1&tamanho=20': () => pagina([maria]),
    'GET /api/pessoas/aniversariantes?periodo=semana': () => json(aniversariantes),
    ...extras,
  })
}

describe('Cadastros → Clientes', () => {
  it('grade com WhatsApp em 1 clique, situação, total de registros e paginação', async () => {
    api()
    renderApp('/cadastros/clientes')

    expect(await screen.findByRole('heading', { name: 'Clientes' })).toBeInTheDocument()
    const linha = (await screen.findByRole('cell', { name: 'Maria Silva' })).closest('tr')!
    expect(within(linha).getByText('529.982.247-25')).toBeInTheDocument()
    expect(within(linha).getByRole('link', { name: 'WhatsApp de Maria Silva' })).toHaveAttribute('href', 'https://wa.me/5511987654321')
    expect(within(linha).getByText('Santos/SP')).toBeInTheDocument()
    expect(within(linha).getByText('Ativo')).toBeInTheDocument()
    expect(screen.getByRole('table', { name: 'Clientes' })).toBeInTheDocument()
    expect(screen.getByText('1 registro')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 1')).toBeInTheDocument()
  })

  it('busca e situação vão para a API, voltam à página 1 e limpar filtros restaura', async () => {
    api({
      'GET /api/pessoas?papel=cliente&situacao=ativos&pagina=1&tamanho=20': () => pagina([maria], 45),
      'GET /api/pessoas?papel=cliente&situacao=ativos&pagina=2&tamanho=20': () => pagina([{ ...maria, id: 'p9', nome: 'Zélia Souza' }], 45, 2),
      'GET /api/pessoas?papel=cliente&situacao=ativos&busca=joao&pagina=1&tamanho=20': () => pagina([]),
      'GET /api/pessoas?papel=cliente&situacao=inativos&busca=joao&pagina=1&tamanho=20': () => pagina([{ ...maria, id: 'p3', nome: 'João Inativo', ativo: false }]),
    })
    renderApp('/cadastros/clientes')
    await screen.findByRole('cell', { name: 'Maria Silva' })

    await userEvent.click(screen.getByRole('button', { name: 'Próxima página' }))
    expect(await screen.findByRole('cell', { name: 'Zélia Souza' })).toBeInTheDocument()
    expect(screen.getByText('Página 2 de 3')).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Buscar'), 'joao')
    expect(await screen.findByText('Nenhum cadastro encontrado com esses filtros.')).toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'inativos')
    const linha = (await screen.findByRole('cell', { name: 'João Inativo' })).closest('tr')!
    expect(within(linha).getByText('Inativo')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }))
    expect(await screen.findByRole('cell', { name: 'Maria Silva' })).toBeInTheDocument()
    expect(screen.getByLabelText('Buscar')).toHaveValue('')
  })

  it('ações da linha: ver ficha e inativar com confirmação', async () => {
    let inativado = false
    api({
      'POST /api/pessoas/p1/inativar': () => {
        inativado = true
        return new Response(null, { status: 204 })
      },
    })
    renderApp('/cadastros/clientes')
    const linha = (await screen.findByRole('cell', { name: 'Maria Silva' })).closest('tr')!

    await userEvent.click(within(linha).getByRole('button', { name: 'Ver ficha de Maria Silva' }))
    const ficha = await screen.findByRole('dialog', { name: 'Maria Silva' })
    expect(within(ficha).getByLabelText('Calça')).toHaveValue('40')
    await userEvent.click(within(ficha).getByRole('button', { name: 'Cancelar' }))

    await userEvent.click(within(linha).getByRole('button', { name: 'Inativar Maria Silva' }))
    const confirmar = screen.getByRole('dialog', { name: 'Inativar cliente' })
    expect(confirmar).toHaveTextContent('Maria Silva')
    await userEvent.click(within(confirmar).getByRole('button', { name: 'Inativar' }))
    await waitFor(() => expect(inativado).toBe(true))
  })

  it('seleção: inativa em lote e exporta só os selecionados', async () => {
    let lote: Record<string, unknown> | null = null
    const fetch = api({
      'POST /api/pessoas/situacao': (init) => {
        lote = corpo(init)
        return json({ alterados: 1 })
      },
      'GET /api/relatorios/clientes.csv?ids=p1': () => new Response('csv', { headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=clientes.csv' } }),
    })
    URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
    URL.revokeObjectURL = () => {}
    renderApp('/cadastros/clientes')
    await screen.findByRole('cell', { name: 'Maria Silva' })

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Maria Silva' }))
    const barra = screen.getByRole('region', { name: 'Seleção' })
    expect(barra).toHaveTextContent('1 selecionado')

    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/clientes.csv?ids=p1')).toBe(true))

    await userEvent.click(within(barra).getByRole('button', { name: 'Inativar selecionados' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Inativar clientes' })).getByRole('button', { name: 'Inativar' }))
    await waitFor(() => expect(lote).toEqual({ ids: ['p1'], ativo: false }))
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Seleção' })).not.toBeInTheDocument())
  })

  it('aniversariantes da semana com mensagem de parabéns pronta', async () => {
    api()
    renderApp('/cadastros/clientes')

    const card = await screen.findByRole('region', { name: 'Aniversariantes da semana' })
    expect(await within(card).findByText('Maria Silva')).toBeInTheDocument()
    expect(within(card).getByText(/16\/10 · 36 anos/)).toBeInTheDocument()
    expect(within(card).getByRole('link', { name: 'Parabenizar Maria Silva' }).getAttribute('href')).toContain('Feliz%20anivers%C3%A1rio')
  })

  it('novo cliente: ficha com preferências e consentimento; aniversariantes recarregam', async () => {
    let enviado: Record<string, unknown> | null = null
    let salvo = false
    api({
      'POST /api/pessoas': (init) => {
        enviado = corpo(init)
        salvo = true
        return json({ ...maria, id: 'p2', nome: 'Ana Costa' }, 201)
      },
      'GET /api/pessoas/aniversariantes?periodo=semana': () =>
        json(salvo ? [...aniversariantes, { ...aniversariantes[0], id: 'p2', nome: 'Ana Costa' }] : aniversariantes),
    })
    renderApp('/cadastros/clientes')
    await screen.findByRole('cell', { name: 'Maria Silva' })

    await userEvent.click(screen.getByRole('button', { name: 'Novo cliente' }))
    const ficha = screen.getByRole('dialog', { name: 'Novo cliente' })
    await userEvent.type(within(ficha).getByLabelText('Nome'), 'Ana Costa')
    await userEvent.type(within(ficha).getByLabelText('CPF'), '111.444.777-35')
    await userEvent.type(within(ficha).getByLabelText('Telefone / WhatsApp'), '(13) 99876-5432')
    await userEvent.type(within(ficha).getByLabelText('Calça'), '38')
    await userEvent.click(within(ficha).getByLabelText(/Aceita receber novidades/))
    await userEvent.click(within(ficha).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toMatchObject({
      tipo: 'Fisica', nome: 'Ana Costa', documento: '111.444.777-35', ehCliente: true, ehFornecedor: false,
      telefone: '(13) 99876-5432', aceitaMarketing: true, preferencias: { calca: '38' },
    })
    const card = screen.getByRole('region', { name: 'Aniversariantes da semana' })
    expect(await within(card).findByText('Ana Costa')).toBeInTheDocument()
  })

  it('erro de validação aparece na ficha', async () => {
    api({ 'POST /api/pessoas': () => problema(400, 'CPF inválido.') })
    renderApp('/cadastros/clientes')
    await screen.findByRole('cell', { name: 'Maria Silva' })

    await userEvent.click(screen.getByRole('button', { name: 'Novo cliente' }))
    const ficha = screen.getByRole('dialog', { name: 'Novo cliente' })
    await userEvent.type(within(ficha).getByLabelText('Nome'), 'Ana')
    await userEvent.click(within(ficha).getByRole('button', { name: 'Salvar' }))

    expect(await within(ficha).findByRole('alert')).toHaveTextContent('CPF inválido.')
  })

  it('abrir pelo link da pesquisa global (?id=) mostra a ficha', async () => {
    api({ 'GET /api/pessoas/p1': () => json(maria) })
    renderApp('/cadastros/clientes?id=p1')

    const ficha = await screen.findByRole('dialog', { name: 'Maria Silva' })
    expect(within(ficha).getByLabelText('Calça')).toHaveValue('40')
    expect(within(ficha).getByLabelText(/Aceita receber novidades/)).toBeChecked()
  })

  it('Dono pode inativar e anonimizar; vendedor não vê essas ações', async () => {
    const chamadas: string[] = []
    api({
      'GET /api/pessoas/p1': () => json(maria),
      'POST /api/pessoas/p1/anonimizar': () => {
        chamadas.push('anonimizar')
        return new Response(null, { status: 204 })
      },
    })
    renderApp('/cadastros/clientes?id=p1')
    const ficha = await screen.findByRole('dialog', { name: 'Maria Silva' })

    await userEvent.click(within(ficha).getByRole('button', { name: 'Anonimizar (LGPD)' }))
    await userEvent.click(within(ficha).getByRole('button', { name: 'Confirmar anonimização' }))

    expect(chamadas).toEqual(['anonimizar'])
  })

  it('vendedor cadastra clientes, mas não vê inativar nem anonimizar', async () => {
    api({ 'GET /api/pessoas/p1': () => json(maria) }, sessaoVendedor)
    renderApp('/cadastros/clientes?id=p1')

    const ficha = await screen.findByRole('dialog', { name: 'Maria Silva' })
    expect(within(ficha).queryByRole('button', { name: 'Inativar' })).not.toBeInTheDocument()
    expect(within(ficha).queryByRole('button', { name: 'Anonimizar (LGPD)' })).not.toBeInTheDocument()
    await userEvent.click(within(ficha).getByRole('button', { name: 'Cancelar' }))
    const linha = screen.getByRole('cell', { name: 'Maria Silva' }).closest('tr')!
    expect(within(linha).queryByRole('button', { name: 'Inativar Maria Silva' })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Maria Silva' }))
    expect(screen.queryByRole('button', { name: 'Inativar selecionados' })).not.toBeInTheDocument()
  })
})
