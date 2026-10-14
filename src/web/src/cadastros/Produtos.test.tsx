import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const grades = [{ id: 'g1', nome: 'PP ao GG', tamanhos: ['P', 'M', 'G'], emUso: false }]
const cores = [{ id: 'c1', nome: 'Azul', hex: '#1D4ED8' }, { id: 'c2', nome: 'Preto', hex: '#111827' }]
const sku = (id: string, corId: string, tamanho: string, ordem: number, extra = {}) => ({
  id, corId, tamanho, ordemTamanho: ordem, codigoDeBarras: `200000000${id.padStart(3, '0')}1`, precoVenda: null, preco: 59.9, ativo: true, ...extra,
})
const camiseta = {
  id: 'p1', referencia: 'CAM-001', descricao: 'Camiseta Básica Gola V', marca: 'Kamus', categoria: 'Camisetas', colecao: 'Verão 2027',
  referenciaDoFornecedor: null, fornecedorId: null, precoVenda: 59.9, precoCusto: 24, markup: 2.5, ativo: true,
  grade: { ...grades[0], emUso: true }, cores,
  skus: [
    sku('1', 'c1', 'P', 0), sku('2', 'c1', 'M', 1), sku('3', 'c1', 'G', 2, { precoVenda: 69.9, preco: 69.9 }),
    sku('4', 'c2', 'P', 0, { ativo: false }), sku('5', 'c2', 'M', 1), sku('6', 'c2', 'G', 2),
  ],
}
const resumo = { id: 'p1', referencia: 'CAM-001', descricao: 'Camiseta Básica Gola V', marca: 'Kamus', categoria: 'Camisetas', colecao: 'Verão 2027', precoVenda: 59.9, markup: 2.5, skus: 5, cores: ['Azul', 'Preto'], ativo: true }

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/produtos?situacao=ativos': () => json({ itens: [resumo], total: 1, pagina: 1, tamanho: 50 }),
    'GET /api/grades': () => json(grades),
    'GET /api/cores': () => json(cores),
    'GET /api/produtos/sugestoes': () => json({ marcas: ['Kamus'], categorias: ['Camisetas'], colecoes: ['Verão 2027'] }),
    'GET /api/produtos/p1': () => json(camiseta),
    ...extras,
  })
}

describe('Cadastros → Produtos', () => {
  it('lista com cores, quantidade de SKUs, preço e markup', async () => {
    api()
    renderApp('/cadastros/produtos')

    const linha = (await screen.findByText('CAM-001')).closest('tr')!
    expect(within(linha).getByText('Azul, Preto')).toBeInTheDocument()
    expect(within(linha).getByText('5')).toBeInTheDocument()
    expect(within(linha).getByText('R$ 59,90')).toBeInTheDocument()
    expect(within(linha).getByText('2,50')).toBeInTheDocument()
  })

  it('novo produto: escolhe grade e cores, mostra o markup e envia', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/produtos': (init) => {
        enviado = corpo(init)
        return json(camiseta, 201)
      },
    })
    renderApp('/cadastros/produtos')
    await screen.findByText('CAM-001')

    await userEvent.click(screen.getByRole('button', { name: 'Novo produto' }))
    const dialogo = screen.getByRole('dialog', { name: 'Novo produto' })
    await userEvent.type(within(dialogo).getByLabelText('Referência'), 'CAM-001')
    await userEvent.type(within(dialogo).getByLabelText('Descrição'), 'Camiseta Básica Gola V')
    await userEvent.type(within(dialogo).getByLabelText('Preço de venda (R$)'), '59,90')
    await userEvent.type(within(dialogo).getByLabelText('Custo (R$)'), '24')
    expect(within(dialogo).getByText('Markup 2,50')).toBeInTheDocument()
    await userEvent.selectOptions(within(dialogo).getByLabelText('Grade de tamanhos'), 'g1')
    await userEvent.click(within(dialogo).getByRole('checkbox', { name: 'Azul' }))
    await userEvent.click(within(dialogo).getByRole('checkbox', { name: 'Preto' }))
    expect(within(dialogo).getByText('Serão criados 6 SKUs (2 cores × 3 tamanhos).')).toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toMatchObject({ referencia: 'CAM-001', descricao: 'Camiseta Básica Gola V', precoVenda: 59.9, precoCusto: 24, gradeId: 'g1', corIds: ['c1', 'c2'] })
    expect(await screen.findByRole('table', { name: 'Grade de Camiseta Básica Gola V' })).toBeInTheDocument()
  })

  it('detalhe: matriz cor × tamanho com preço próprio e combinação desativada', async () => {
    api()
    renderApp('/cadastros/produtos?id=p1')

    const matriz = await screen.findByRole('table', { name: 'Grade de Camiseta Básica Gola V' })
    const cabecalho = within(matriz).getAllByRole('columnheader').map((c) => c.textContent)
    expect(cabecalho).toEqual(['Cor', 'P', 'M', 'G'])
    const azul = within(matriz).getByRole('row', { name: /Azul/ })
    expect(within(azul).getByRole('button', { name: 'Azul G: R$ 69,90' })).toBeInTheDocument()
    const preto = within(matriz).getByRole('row', { name: /Preto/ })
    expect(within(preto).getByRole('button', { name: 'Preto P: desativado' })).toBeInTheDocument()
  })

  it('editar SKU: código do fornecedor, preço próprio e ativo', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'PUT /api/produtos/p1/skus/2': (init) => {
        enviado = corpo(init)
        return json(sku('2', 'c1', 'M', 1, { codigoDeBarras: '7891000315507' }))
      },
    })
    renderApp('/cadastros/produtos?id=p1')
    const matriz = await screen.findByRole('table', { name: 'Grade de Camiseta Básica Gola V' })

    await userEvent.click(within(matriz).getByRole('button', { name: 'Azul M: R$ 59,90' }))
    const dialogo = screen.getByRole('dialog', { name: 'Azul · M' })
    await userEvent.clear(within(dialogo).getByLabelText('Código de barras'))
    await userEvent.type(within(dialogo).getByLabelText('Código de barras'), '7891000315507')
    await userEvent.type(within(dialogo).getByLabelText('Preço próprio (R$, opcional)'), '64,90')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toEqual({ codigoDeBarras: '7891000315507', precoVenda: 64.9, ativo: true })
  })

  it('vendedor consulta, mas não vê botões de alteração', async () => {
    api({}, sessaoVendedor)
    renderApp('/cadastros/produtos?id=p1')

    const matriz = await screen.findByRole('table', { name: 'Grade de Camiseta Básica Gola V' })
    expect(within(matriz).queryAllByRole('button')).toHaveLength(0)
    expect(screen.queryByRole('button', { name: 'Editar produto' })).not.toBeInTheDocument()
  })
})
