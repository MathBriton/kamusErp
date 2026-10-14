import { screen, within } from '@testing-library/react'
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
const pagina = (itens: unknown[]) => json({ itens, total: itens.length, pagina: 1, tamanho: 50 })
const aniversariantes = [{ id: 'p1', nome: 'Maria Silva', dataNascimento: '1990-10-16', idade: 36, whatsApp: '5511987654321', aceitaMarketing: true }]

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/pessoas?papel=cliente&situacao=ativos': () => pagina([maria]),
    'GET /api/pessoas/aniversariantes?periodo=semana': () => json(aniversariantes),
    ...extras,
  })
}

describe('Cadastros → Clientes', () => {
  it('lista clientes com WhatsApp em 1 clique', async () => {
    api()
    renderApp('/cadastros/clientes')

    expect(await screen.findByRole('heading', { name: 'Clientes' })).toBeInTheDocument()
    const linha = (await screen.findByRole('cell', { name: 'Maria Silva' })).closest('tr')!
    expect(within(linha).getByText('529.982.247-25')).toBeInTheDocument()
    expect(within(linha).getByRole('link', { name: 'WhatsApp de Maria Silva' })).toHaveAttribute('href', 'https://wa.me/5511987654321')
    expect(within(linha).getByText('Santos/SP')).toBeInTheDocument()
  })

  it('busca envia o termo para a API', async () => {
    api({ 'GET /api/pessoas?papel=cliente&situacao=ativos&busca=joao': () => pagina([]) })
    renderApp('/cadastros/clientes')
    await screen.findByRole('cell', { name: 'Maria Silva' })

    await userEvent.type(screen.getByLabelText('Buscar'), 'joao')

    expect(await screen.findByText('Nenhum cadastro encontrado.')).toBeInTheDocument()
  })

  it('aniversariantes da semana com mensagem de parabéns pronta', async () => {
    api()
    renderApp('/cadastros/clientes')

    const card = await screen.findByRole('region', { name: 'Aniversariantes da semana' })
    expect(await within(card).findByText('Maria Silva')).toBeInTheDocument()
    expect(within(card).getByText(/16\/10 · 36 anos/)).toBeInTheDocument()
    expect(within(card).getByRole('link', { name: 'Parabenizar Maria Silva' }).getAttribute('href')).toContain('Feliz%20anivers%C3%A1rio')
  })

  it('novo cliente: ficha com preferências e consentimento', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/pessoas': (init) => {
        enviado = corpo(init)
        return json({ ...maria, id: 'p2', nome: 'Ana Costa' }, 201)
      },
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
  })
})
