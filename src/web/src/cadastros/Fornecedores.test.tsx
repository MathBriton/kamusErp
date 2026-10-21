import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const malharia = {
  id: 'f1', tipo: 'Juridica', nome: 'Malharia Sul LTDA', nomeFantasia: 'Malharia Sul', documento: '11.222.333/0001-81', ehCliente: false, ehFornecedor: true,
  telefone: '4733334444', whatsApp: '554733334444', email: null, dataNascimento: null,
  endereco: { cep: null, logradouro: null, numero: null, complemento: null, bairro: null, cidade: 'Blumenau', uf: 'SC' },
  preferencias: { blusa: null, calca: null, calcado: null, estilo: null }, observacoes: null,
  aceitaMarketing: false, consentimentoMarketingEm: null, ativo: true, criadoEm: '2026-10-01T12:00:00Z', anonimizadoEm: null,
}

describe('Cadastros → Fornecedores', () => {
  it('lista e cadastra pessoa jurídica com CNPJ, sem campos de cliente', async () => {
    let enviado: Record<string, unknown> | null = null
    mockApi({
      'POST /api/auth/refresh': () => json(sessaoDemo),
      'GET /api/empresas/ativa': empresaAtivaHandler(),
      'GET /health': () => new Response('Healthy'),
      'GET /api/pessoas?papel=fornecedor&situacao=ativos&pagina=1&tamanho=20': () => json({ itens: [malharia], total: 1, pagina: 1, tamanho: 20 }),
      'POST /api/pessoas': (init) => {
        enviado = corpo(init)
        return json(malharia, 201)
      },
    })
    renderApp('/cadastros/fornecedores')

    const linha = (await screen.findByText('Malharia Sul')).closest('tr')!
    expect(within(linha).getByText('Malharia Sul LTDA')).toBeInTheDocument()
    expect(within(linha).getByText('11.222.333/0001-81')).toBeInTheDocument()
    expect(screen.getByRole('table', { name: 'Fornecedores' })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Aniversariantes da semana' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Novo fornecedor' }))
    const ficha = screen.getByRole('dialog', { name: 'Novo fornecedor' })
    expect(within(ficha).queryByLabelText('Calça')).not.toBeInTheDocument()
    await userEvent.type(within(ficha).getByLabelText('Razão social'), 'Tecelagem Aurora LTDA')
    await userEvent.type(within(ficha).getByLabelText('CNPJ'), '11.444.777/0001-61')
    await userEvent.click(within(ficha).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toMatchObject({ tipo: 'Juridica', nome: 'Tecelagem Aurora LTDA', documento: '11.444.777/0001-61', ehFornecedor: true, ehCliente: false })
  })
})
