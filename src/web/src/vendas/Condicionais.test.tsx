import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { hojeIso, somarDias } from '../financeiro/tipos'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const formas = [{ id: 'f2', nome: 'PIX', tipo: 'Pix', contaId: 'c1', taxaAVista: 0, taxaParcelado: 0, maxParcelas: 1, prazoDias: 0, ativa: true }]
const condicional = (extra = {}) => ({
  id: 'k4', numero: 4, data: hojeIso(), prazo: somarDias(hojeIso(), 2), clienteId: 'p1', clienteNome: 'Maria Souza', vendedorId: 'b1', vendedorNome: 'Bia',
  situacao: 'Aberta', pecas: 3, valor: 179.7, vendaId: null, vendaNumero: null, observacao: null, registradaPor: 'Admin Demo',
  itens: [
    { skuId: 's1', descricao: 'Camiseta básica Azul M', quantidade: 2, precoUnitario: 59.9, devolvidas: 0 },
    { skuId: 's2', descricao: 'Camiseta básica Azul G', quantidade: 1, precoUnitario: 59.9, devolvidas: 0 },
  ],
  ...extra,
})
const linha = (extra = {}) => ({
  id: 'k4', numero: 4, data: hojeIso(), prazo: somarDias(hojeIso(), 2), clienteId: 'p1', clienteNome: 'Maria Souza', vendedorNome: 'Bia', pecas: 3, valor: 179.7,
  situacao: 'Aberta', vendaNumero: null, ...extra,
})
const lista = { itens: [linha(), linha({ id: 'k2', numero: 2, situacao: 'Atrasada', pecas: 1, valor: 59.9 })], abertas: 2, atrasadas: 1, pecasNaRua: 4, valorNaRua: 239.6 }

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/estoque/lojas': () => json([{ id: 'l1', empresaId: 'e1', nome: 'Loja Centro', ativa: true, quantidade: 3 }]),
    'GET /api/vendedores': () => json([{ id: 'b1', nome: 'Bia', comissaoPercentual: 5, usuarioId: 'u2', ativo: true }]),
    'GET /api/vendas/formas': () => json(formas),
    'GET /api/vendas/configuracao': () => json({ permitirEstoqueNegativo: true, prazoDeTrocaDias: 30, validadeDoValeDias: 90, prazoCondicionalDias: 2, limiteCondicionalPecas: 10, limiteCondicionalValor: 1000 }),
    'GET /api/vendas/condicionais?situacao=abertas': () => json(lista),
    'GET /api/vendas/condicionais/k4': () => json(condicional()),
    ...extras,
  })
}

beforeEach(() => {
  URL.createObjectURL = vi.fn(() => 'blob:x') as unknown as typeof URL.createObjectURL
  URL.revokeObjectURL = vi.fn()
})

describe('Vendas → Condicional', () => {
  it('lista as abertas com peças e valor na rua e destaca a atrasada', async () => {
    api()
    renderApp('/vendas/condicional')
    const tabela = await screen.findByRole('table', { name: 'Condicionais' })
    expect(within(tabela).getAllByText('Maria Souza')).toHaveLength(2)
    expect(within(tabela).getByText('Atrasada')).toBeInTheDocument()
    expect(screen.getByText('R$ 239,60', { selector: 'dd' })).toBeInTheDocument()
  })

  it('abre condicional para cliente cadastrado com as peças pelo leitor e baixa o termo', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'GET /api/pessoas?papel=cliente&situacao=ativos&busca=ma': () => json({ itens: [{ id: 'p1', nome: 'Maria Souza' }], total: 1 }),
      'GET /api/produtos/por-codigo/789': () => json({ produto: { descricao: 'Camiseta básica' }, sku: { id: 's1', tamanho: 'M', preco: 59.9, ativo: true }, cor: 'Azul' }),
      'POST /api/vendas/condicionais': (init) => {
        enviado = corpo(init)
        return json(condicional({ pecas: 2, valor: 119.8 }), 201)
      },
      'GET /api/vendas/condicionais/k4/termo.pdf': () => new Response('%PDF', { headers: { 'Content-Type': 'application/pdf' } }),
    }, sessaoVendedor)
    renderApp('/vendas/condicional')
    await userEvent.click(await screen.findByRole('button', { name: 'Nova condicional' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Nova condicional' })
    expect(within(dialogo).getByLabelText('Vendedor')).toHaveValue('b1')
    expect(within(dialogo).getByLabelText('Devolver até')).toHaveValue(somarDias(hojeIso(), 2))
    expect(within(dialogo).queryByRole('checkbox', { name: /Liberar/ })).not.toBeInTheDocument()

    await userEvent.type(within(dialogo).getByLabelText('Cliente'), 'ma')
    await userEvent.click(await within(dialogo).findByRole('button', { name: 'Maria Souza' }))
    const leitor = within(dialogo).getByLabelText('Código de barras')
    await waitFor(() => expect(leitor).toBeEnabled())
    await userEvent.type(leitor, '789{Enter}')
    await userEvent.type(leitor, '789{Enter}')
    await within(dialogo).findByRole('table', { name: 'Peças da condicional' })
    expect(within(dialogo).queryByLabelText(/Desconto de/)).not.toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Abrir condicional' }))

    expect(await screen.findByText('Condicional nº 4 aberta: 2 peças (R$ 119,80)')).toBeInTheDocument()
    expect(enviado).toEqual({
      lojaId: 'l1', clienteId: 'p1', clienteNome: 'Maria Souza', vendedorId: 'b1', prazo: somarDias(hojeIso(), 2), observacao: null, liberar: false,
      itens: [{ skuId: 's1', quantidade: 2 }],
    })
    await userEvent.click(screen.getByRole('button', { name: 'Termo (PDF)' }))
    await waitFor(() => expect(URL.createObjectURL).toHaveBeenCalled())
  })

  it('erro de limite aparece e o Dono pode liberar', async () => {
    const corpos: Record<string, unknown>[] = []
    api({
      'GET /api/pessoas?papel=cliente&situacao=ativos&busca=ma': () => json({ itens: [{ id: 'p1', nome: 'Maria Souza' }], total: 1 }),
      'GET /api/produtos/por-codigo/789': () => json({ produto: { descricao: 'Camiseta básica' }, sku: { id: 's1', tamanho: 'M', preco: 59.9, ativo: true }, cor: 'Azul' }),
      'POST /api/vendas/condicionais': (init) => {
        corpos.push(corpo(init))
        return corpos.length === 1
          ? problema(400, 'O cliente ficaria com 11 peças em condicional (limite de 10). Só Dono ou Gerente liberam.')
          : json(condicional(), 201)
      },
    })
    renderApp('/vendas/condicional')
    await userEvent.click(await screen.findByRole('button', { name: 'Nova condicional' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Nova condicional' })
    await userEvent.type(within(dialogo).getByLabelText('Cliente'), 'ma')
    await userEvent.click(await within(dialogo).findByRole('button', { name: 'Maria Souza' }))
    const leitor = within(dialogo).getByLabelText('Código de barras')
    await waitFor(() => expect(leitor).toBeEnabled())
    await userEvent.type(leitor, '789{Enter}')
    await userEvent.click(await within(dialogo).findByRole('button', { name: 'Abrir condicional' }))
    expect(await within(dialogo).findByRole('alert')).toHaveTextContent('limite de 10')
    await userEvent.click(within(dialogo).getByRole('checkbox', { name: /Liberar/ }))
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Abrir condicional' }))
    await screen.findByText(/Condicional nº 4 aberta/)
    expect(corpos[1]).toMatchObject({ liberar: true })
  })

  it('fecha devolvendo uma peça e vende o resto no PIX', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/vendas/condicionais/k4/fechar': (init) => {
        enviado = corpo(init)
        return json({ condicional: condicional({ situacao: 'Fechada', vendaNumero: 8 }), vendaNumero: 8, vendaTotal: 119.8, avisos: [] })
      },
    })
    renderApp('/vendas/condicional')
    const tabela = await screen.findByRole('table', { name: 'Condicionais' })
    await userEvent.click(within(tabela).getByRole('button', { name: 'Fechar condicional 4' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Fechar condicional nº 4' })
    const devolveu = await within(dialogo).findByLabelText('Devolveu Camiseta básica Azul M')
    expect(devolveu).toHaveValue('2')
    await userEvent.clear(devolveu)
    await userEvent.type(devolveu, '1')
    await userEvent.clear(within(dialogo).getByLabelText('Devolveu Camiseta básica Azul G'))
    await userEvent.type(within(dialogo).getByLabelText('Devolveu Camiseta básica Azul G'), '0')
    expect(within(dialogo).getByText('O cliente ficou com 2 peças: R$ 119,80')).toBeInTheDocument()
    expect(within(dialogo).getByLabelText('Valor do pagamento 1')).toHaveValue('119,80')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Fechar condicional' }))

    expect(await screen.findByText('Condicional nº 4 fechada · venda nº 8 de R$ 119,80')).toBeInTheDocument()
    expect(enviado).toEqual({
      devolvidos: [{ skuId: 's1', quantidade: 1 }, { skuId: 's2', quantidade: 0 }],
      pagamentos: [{ formaId: 'f2', valor: 119.8, parcelas: 1 }],
      descontoGeral: 0, notaExterna: null,
    })
  })

  it('mostra a conversão por vendedor para quem vê os números', async () => {
    api({
      [`GET /api/vendas/condicionais/conversao?de=${hojeIso().slice(0, 7)}-01&ate=${hojeIso()}`]: () =>
        json([{ vendedorId: 'b1', vendedor: 'Bia', condicionais: 2, pecasLevadas: 4, pecasVendidas: 2, valorLevado: 239.6, valorVendido: 119.8, conversao: 50 }]),
    })
    renderApp('/vendas/condicional')
    const conversao = await screen.findByRole('table', { name: 'Conversão por vendedor' })
    expect(within(conversao).getByText('50%')).toBeInTheDocument()
  })
})
