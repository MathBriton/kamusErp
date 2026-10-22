import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo, sessaoVendedor } from '../test/mockApi'
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
    'GET /api/produtos?situacao=ativos&pagina=1&tamanho=20': () => json({ itens: [resumo], total: 1, pagina: 1, tamanho: 20 }),
    'GET /api/grades': () => json(grades),
    'GET /api/cores': () => json(cores),
    'GET /api/produtos/sugestoes': () => json({ marcas: ['Kamus'], categorias: ['Camisetas'], colecoes: ['Verão 2027'] }),
    'GET /api/produtos/p1': () => json(camiseta),
    ...extras,
  })
}

describe('Cadastros → Produtos', () => {
  it('grade com colunas, situação, total de registros e paginação', async () => {
    api()
    renderApp('/cadastros/produtos')

    await screen.findByText('CAM-001')
    const linha = within(screen.getByRole('table', { name: 'Produtos' })).getByRole('row', { name: /CAM-001/ })
    expect(within(linha).getByText('Azul, Preto')).toBeInTheDocument()
    expect(within(linha).getByText('5')).toBeInTheDocument()
    expect(within(linha).getByText('R$ 59,90')).toBeInTheDocument()
    expect(within(linha).getByText('2,50×')).toBeInTheDocument()
    expect(within(linha).getByText('Ativo')).toBeInTheDocument()
    expect(screen.getByText('1 registro')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 1')).toBeInTheDocument()
  })

  it('filtros de categoria e coleção, página seguinte e limpar filtros', async () => {
    const pagina = (n: number) => json({ itens: [{ ...resumo, id: `p${n}`, referencia: `REF-${n}` }], total: 45, pagina: n, tamanho: 20 })
    api({
      'GET /api/produtos?situacao=ativos&pagina=1&tamanho=20': () => pagina(1),
      'GET /api/produtos?situacao=ativos&categoria=Camisetas&pagina=1&tamanho=20': () => pagina(1),
      'GET /api/produtos?situacao=ativos&categoria=Camisetas&pagina=2&tamanho=20': () => pagina(2),
      'GET /api/produtos?situacao=ativos&categoria=Camisetas&colecao=Ver%C3%A3o+2027&pagina=1&tamanho=20': () => pagina(1),
    })
    renderApp('/cadastros/produtos')
    await screen.findByText('REF-1')

    await userEvent.selectOptions(await screen.findByLabelText('Categoria'), 'Camisetas')
    await userEvent.click(await screen.findByRole('button', { name: 'Próxima página' }))
    expect(await screen.findByText('REF-2')).toBeInTheDocument()
    expect(screen.getByText('Página 2 de 3')).toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText('Coleção'), 'Verão 2027')
    expect(await screen.findByText('REF-1')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 3')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }))
    expect(screen.getByLabelText('Categoria')).toHaveValue('')
    expect(screen.getByLabelText('Coleção')).toHaveValue('')
  })

  it('ações da linha: ver, editar e inativar com confirmação', async () => {
    let inativado = false
    api({
      'POST /api/produtos/p1/inativar': () => {
        inativado = true
        return new Response(null, { status: 204 })
      },
    })
    renderApp('/cadastros/produtos')
    await screen.findByText('CAM-001')
    const linha = screen.getByRole('table', { name: 'Produtos' }).querySelector('tbody tr')!

    await userEvent.click(within(linha as HTMLElement).getByRole('button', { name: 'Ver Camiseta Básica Gola V' }))
    expect(await screen.findByRole('table', { name: 'Grade de Camiseta Básica Gola V' })).toBeInTheDocument()
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Camiseta Básica Gola V' })).getByRole('button', { name: 'Fechar' }))

    await userEvent.click(within(linha as HTMLElement).getByRole('button', { name: 'Editar Camiseta Básica Gola V' }))
    const edicao = await screen.findByRole('dialog', { name: 'Editar Camiseta Básica Gola V' })
    expect(within(edicao).getByLabelText('Descrição')).toHaveValue('Camiseta Básica Gola V')
    await userEvent.click(within(edicao).getByRole('button', { name: 'Cancelar' }))

    await userEvent.click(within(linha as HTMLElement).getByRole('button', { name: 'Inativar Camiseta Básica Gola V' }))
    const confirmar = screen.getByRole('dialog', { name: 'Inativar produto' })
    expect(confirmar).toHaveTextContent('Camiseta Básica Gola V')
    await userEvent.click(within(confirmar).getByRole('button', { name: 'Inativar' }))
    await waitFor(() => expect(inativado).toBe(true))
  })

  it('seleção: inativa em lote e exporta só os selecionados', async () => {
    let lote: Record<string, unknown> | null = null
    const fetch = api({
      'POST /api/produtos/situacao': (init) => {
        lote = corpo(init)
        return json({ alterados: 1 })
      },
      'GET /api/relatorios/produtos.csv?ids=p1': () => new Response('csv', { headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=produtos.csv' } }),
    })
    URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
    URL.revokeObjectURL = () => {}
    renderApp('/cadastros/produtos')
    await screen.findByText('CAM-001')

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Camiseta Básica Gola V' }))
    const barra = screen.getByRole('region', { name: 'Seleção' })
    expect(barra).toHaveTextContent('1 selecionado')

    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    const menu = screen.getByRole('menu', { name: 'Exportar' })
    expect(menu).toHaveTextContent('Exportar os 1 selecionados')
    await userEvent.click(within(menu).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/produtos.csv?ids=p1')).toBe(true))

    await userEvent.click(within(barra).getByRole('button', { name: 'Inativar selecionados' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Inativar produtos' })).getByRole('button', { name: 'Inativar' }))
    await waitFor(() => expect(lote).toEqual({ ids: ['p1'], ativo: false }))
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Seleção' })).not.toBeInTheDocument())
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
    expect(within(dialogo).getByText('Markup 2,50× (preço ÷ custo)')).toBeInTheDocument()
    await userEvent.selectOptions(within(dialogo).getByLabelText('Grade de tamanhos'), 'g1')
    await userEvent.click(within(dialogo).getByRole('checkbox', { name: 'Azul' }))
    await userEvent.click(within(dialogo).getByRole('checkbox', { name: 'Preto' }))
    expect(within(dialogo).getByText('Serão criados 6 SKUs (2 cores × 3 tamanhos).')).toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toMatchObject({ referencia: 'CAM-001', descricao: 'Camiseta Básica Gola V', precoVenda: 59.9, precoCusto: 24, gradeId: 'g1', corIds: ['c1', 'c2'] })
    expect(await screen.findByRole('table', { name: 'Grade de Camiseta Básica Gola V' })).toBeInTheDocument()
  })

  it('loja sem grades nem cores: o formulário oferece criar as padrão sem sair do cadastro', async () => {
    let criadas = false
    api({
      'GET /api/grades': () => json(criadas ? grades : []),
      'GET /api/cores': () => json(criadas ? cores : []),
      'POST /api/grades/padrao': () => {
        criadas = true
        return new Response(null, { status: 204 })
      },
      'POST /api/cores/padrao': () => new Response(null, { status: 204 }),
    })
    renderApp('/cadastros/produtos')
    await screen.findByText('CAM-001')
    expect(screen.getByText(/SKU é cada combinação de cor e tamanho/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Novo produto' }))
    const dialogo = screen.getByRole('dialog', { name: 'Novo produto' })
    const aviso = await within(dialogo).findByRole('region', { name: 'Grades e cores' })
    await userEvent.click(within(aviso).getByRole('button', { name: 'Adicionar grades e cores padrão' }))

    expect(await within(dialogo).findByRole('checkbox', { name: 'Azul' })).toBeInTheDocument()
    expect(within(dialogo).getByRole('option', { name: /PP ao GG/ })).toBeInTheDocument()
    expect(within(dialogo).queryByRole('region', { name: 'Grades e cores' })).not.toBeInTheDocument()
  })

  it('novo produto sem grade envia gradeId nulo e mostra a mensagem da API', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/produtos': (init) => {
        enviado = corpo(init)
        return problema(400, 'Escolha uma grade de tamanhos.')
      },
    })
    renderApp('/cadastros/produtos')
    await screen.findByText('CAM-001')

    await userEvent.click(screen.getByRole('button', { name: 'Novo produto' }))
    const dialogo = screen.getByRole('dialog', { name: 'Novo produto' })
    await userEvent.type(within(dialogo).getByLabelText('Referência'), 'CAM-002')
    await userEvent.type(within(dialogo).getByLabelText('Descrição'), 'Sem grade')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(await within(dialogo).findByRole('alert')).toHaveTextContent('Escolha uma grade de tamanhos.')
    expect(enviado).toMatchObject({ gradeId: null })
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
