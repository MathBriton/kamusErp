import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const lojas = [
  { id: 'l1', empresaId: 'e1', nome: 'Kamus Moda Centro', ativa: true, quantidade: 19 },
  { id: 'l2', empresaId: 'e1', nome: 'Depósito', ativa: true, quantidade: 0 },
]
const celula = (skuId: string, corId: string, tamanho: string, quantidade: number, extra = {}) => ({
  skuId, corId, tamanho, codigoDeBarras: `20000000000${skuId}`, ativo: true, quantidade, custoMedio: quantidade ? 22 : null, ...extra,
})
const matriz = {
  produtoId: 'p1', referencia: 'CAM-001', descricao: 'Camiseta Básica', precoVenda: 59.9, precoCusto: 24,
  tamanhos: ['P', 'M', 'G'],
  cores: [{ id: 'c1', nome: 'Azul', hex: '#1D4ED8' }, { id: 'c2', nome: 'Preto', hex: '#111827' }],
  celulas: [celula('1', 'c1', 'P', 0), celula('2', 'c1', 'M', 15), celula('3', 'c1', 'G', 0), celula('4', 'c2', 'P', 0, { ativo: false }), celula('5', 'c2', 'M', 0), celula('6', 'c2', 'G', 4)],
  lojas: [{ lojaId: 'l1', nome: 'Kamus Moda Centro', quantidade: 19 }, { lojaId: 'l2', nome: 'Depósito', quantidade: 0 }],
  total: 19,
}
const posicao = {
  itens: [{ produtoId: 'p1', referencia: 'CAM-001', descricao: 'Camiseta Básica', quantidade: 19, valorEmCusto: 418, valorEmVenda: 1138.1 }],
  total: 1, pagina: 1, tamanho: 20, totalDePecas: 19, valorEmCusto: 418, valorEmVenda: 1138.1,
}
const kardex = [
  { id: 'm2', ocorridoEm: '2026-10-14T15:00:00Z', tipo: 'Ajuste', lojaId: 'l1', loja: 'Kamus Moda Centro', lojaRelacionada: null, referencia: 'CAM-001', cor: 'Azul', tamanho: 'M',
    quantidade: -1, custoUnitario: 22, saldoApos: 15, custoMedioApos: 22, motivo: 'Avaria', documento: null, observacao: null, usuario: 'Admin Demo' },
  { id: 'm1', ocorridoEm: '2026-10-14T14:00:00Z', tipo: 'Entrada', lojaId: 'l1', loja: 'Kamus Moda Centro', lojaRelacionada: null, referencia: 'CAM-001', cor: 'Azul', tamanho: 'M',
    quantidade: 16, custoUnitario: 22, saldoApos: 16, custoMedioApos: 22, motivo: null, documento: 'NF 123', observacao: null, usuario: 'Admin Demo' },
]
const produtoResumo = { id: 'p1', referencia: 'CAM-001', descricao: 'Camiseta Básica', marca: null, categoria: null, colecao: null, precoVenda: 59.9, markup: 2.5, skus: 6, cores: ['Azul', 'Preto'], ativo: true }

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/estoque/lojas': () => json(lojas),
    'GET /api/estoque/posicao?pagina=1&tamanho=20': () => json(posicao),
    'GET /api/estoque/posicao/p1': () => json(matriz),
    'GET /api/estoque/posicao/p1?lojaId=l1': () => json(matriz),
    'GET /api/estoque/kardex?skuId=2': () => json(kardex),
    'GET /api/produtos?situacao=ativos&busca=cam': () => json({ itens: [produtoResumo], total: 1 }),
    ...extras,
  })
}

describe('Estoque → Posição', () => {
  it('grade com peças, valores, totais e paginação; "Ver" abre a matriz e o kardex do SKU', async () => {
    api()
    renderApp('/estoque/posicao')

    await screen.findByText('CAM-001')
    const linha = within(screen.getByRole('table', { name: 'Posição de estoque' })).getByRole('row', { name: /CAM-001/ })
    expect(within(linha).getByText('19')).toBeInTheDocument()
    expect(within(linha).getByText('R$ 418,00')).toBeInTheDocument()
    expect(screen.getByText('R$ 1.138,10', { selector: 'dd' })).toBeInTheDocument()
    expect(screen.getByText('1 registro')).toBeInTheDocument()

    await userEvent.click(within(linha).getByRole('button', { name: 'Ver Camiseta Básica' }))
    const grade = await screen.findByRole('table', { name: 'Estoque de Camiseta Básica' })
    expect(within(grade).getByRole('button', { name: 'Preto G: 4' })).toBeInTheDocument()
    expect(within(screen.getByRole('list', { name: 'Por loja' })).getByText('Depósito:')).toBeInTheDocument()

    await userEvent.click(within(grade).getByRole('button', { name: 'Azul M: 15' }))
    const extrato = await screen.findByRole('table', { name: 'Kardex' })
    expect(within(extrato).getByText('NF 123 · Admin Demo')).toBeInTheDocument()
    expect(within(extrato).getByText('-1')).toBeInTheDocument()
    expect(within(extrato).getByText('+16')).toBeInTheDocument()
  })

  it('filtra por loja (totais da resposta) e exporta só os selecionados', async () => {
    const fetch = api({
      'GET /api/estoque/posicao?lojaId=l1&pagina=1&tamanho=20': () => json({ ...posicao, totalDePecas: 7, total: 41 }),
      'GET /api/estoque/posicao?lojaId=l1&pagina=2&tamanho=20': () => json({ ...posicao, pagina: 2, totalDePecas: 7, total: 41 }),
      'GET /api/relatorios/posicao-de-estoque.csv?ids=p1': () => new Response('csv', { headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=posicao.csv' } }),
    })
    URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
    URL.revokeObjectURL = () => {}
    renderApp('/estoque/posicao')
    await screen.findByText('CAM-001')

    const loja = screen.getByLabelText('Loja')
    await within(loja).findByRole('option', { name: 'Depósito' })
    await userEvent.selectOptions(loja, 'l1')
    expect(await screen.findByText('7', { selector: 'dd' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Próxima página' }))
    expect(await screen.findByText('Página 2 de 3')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Camiseta Básica' }))
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/posicao-de-estoque.csv?ids=p1')).toBe(true))
  })

  it('vendedor vê quantidades, sem custo, sem kardex e sem exportar', async () => {
    api({ 'GET /api/estoque/posicao?pagina=1&tamanho=20': () => json({ ...posicao, valorEmCusto: null, itens: [{ ...posicao.itens[0], valorEmCusto: null }] }) }, sessaoVendedor)
    renderApp('/estoque/posicao')

    await screen.findByText('CAM-001')
    expect(screen.queryByText('Valor em custo')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Exportar/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Entrada de mercadoria' })).not.toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: 'Selecionar Camiseta Básica' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Ver Camiseta Básica' }))
    expect(await screen.findByLabelText('Azul M: 15')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Azul M: 15' })).not.toBeInTheDocument()
  })
})

describe('Estoque → Entrada, ajuste e transferência', () => {
  async function escolherLoja(rotulo: string, lojaId: string) {
    const campo = await screen.findByLabelText(rotulo)
    await within(campo).findByRole('option', { name: 'Depósito' })
    await userEvent.selectOptions(campo, lojaId)
  }

  async function escolherCamiseta() {
    await userEvent.type(await screen.findByLabelText('Buscar produto'), 'cam')
    await userEvent.click(await screen.findByRole('button', { name: /Camiseta Básica/ }))
    await screen.findByRole('table', { name: 'Estoque de Camiseta Básica' })
  }

  it('entrada: digita na matriz, junta na lista e lança com custo', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/estoque/entradas': (init) => {
        enviado = corpo(init)
        return json({ operacaoId: 'op1', itens: 2, quantidade: 7 })
      },
    })
    renderApp('/estoque/entradas')
    await escolherLoja('Loja', 'l1')
    await escolherCamiseta()

    expect(screen.getByLabelText('Preto P')).toBeDisabled() // SKU desativado não recebe entrada
    await userEvent.type(screen.getByLabelText('Azul M'), '5')
    await userEvent.type(screen.getByLabelText('Preto G'), '2')
    expect(screen.getByLabelText('Total da grade')).toHaveTextContent('7')
    const custo = screen.getByLabelText('Custo unitário (R$)')
    expect(custo).toHaveValue('24')
    await userEvent.clear(custo)
    await userEvent.type(custo, '25,50')
    await userEvent.type(screen.getByLabelText('Documento (opcional)'), 'NF 77')
    await userEvent.click(screen.getByRole('button', { name: 'Adicionar à lista' }))

    const lista = screen.getByRole('table', { name: 'Itens da operação' })
    expect(within(lista).getByText('Azul M: 5 · Preto G: 2')).toBeInTheDocument()
    expect(screen.getByText('Total: 7 peças')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Lançar entrada' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Entrada lançada: 7 peças.')
    expect(enviado).toEqual({
      lojaId: 'l1', documento: 'NF 77', observacao: '',
      itens: [{ skuId: '2', quantidade: 5, custoUnitario: 25.5 }, { skuId: '6', quantidade: 2, custoUnitario: 25.5 }],
    })
    expect(screen.queryByRole('table', { name: 'Itens da operação' })).not.toBeInTheDocument()
  })

  it('ajuste: aceita negativo, exige motivo e mostra o erro da API', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/estoque/ajustes': (init) => {
        enviado = corpo(init)
        return problema(409, 'CAM-001 Azul M: há 15 e a operação pede 20.')
      },
    })
    renderApp('/estoque/ajustes')
    await escolherLoja('Loja', 'l1')
    await escolherCamiseta()
    await userEvent.type(screen.getByLabelText('Azul M'), '-20')
    await userEvent.click(screen.getByRole('button', { name: 'Adicionar à lista' }))

    expect(screen.getByRole('button', { name: 'Lançar ajuste' })).toBeDisabled()
    await userEvent.selectOptions(screen.getByLabelText('Motivo'), 'Avaria')
    await userEvent.click(screen.getByRole('button', { name: 'Lançar ajuste' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('há 15 e a operação pede 20')
    expect(enviado).toMatchObject({ lojaId: 'l1', motivo: 'Avaria', itens: [{ skuId: '2', quantidade: -20, custoUnitario: null }] })
  })

  it('transferência: origem e destino diferentes', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/estoque/transferencias': (init) => {
        enviado = corpo(init)
        return json({ operacaoId: 'op1', itens: 1, quantidade: 3 })
      },
    })
    renderApp('/estoque/transferencias')
    await escolherLoja('Loja de origem', 'l1')
    const destino = screen.getByLabelText('Loja de destino')
    expect(within(destino).queryByRole('option', { name: 'Kamus Moda Centro' })).not.toBeInTheDocument()
    await userEvent.selectOptions(destino, 'l2')
    await escolherCamiseta()
    await userEvent.type(screen.getByLabelText('Azul M'), '3')
    await userEvent.click(screen.getByRole('button', { name: 'Adicionar à lista' }))
    await userEvent.click(screen.getByRole('button', { name: 'Transferir' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Transferência feita: 3 peças.')
    expect(enviado).toMatchObject({ origemId: 'l1', destinoId: 'l2', itens: [{ skuId: '2', quantidade: 3 }] })
  })

  it('grade vazia não entra na lista', async () => {
    api()
    renderApp('/estoque/entradas')
    await escolherLoja('Loja', 'l1')
    await escolherCamiseta()

    await userEvent.click(screen.getByRole('button', { name: 'Adicionar à lista' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Digite ao menos uma quantidade')
  })
})

describe('Administrativo → Lojas', () => {
  const paginaDeLojas = (itens = lojas) => json({ itens, total: itens.length, pagina: 1, tamanho: 20 })
  const lojasApi = (extras: Parameters<typeof mockApi>[0] = {}) =>
    api({ 'GET /api/estoque/lojas?situacao=todas&pagina=1&tamanho=20': () => paginaDeLojas(), ...extras })

  it('grade com peças, situação e total; Dono cria loja e vê o limite do plano', async () => {
    let enviado: Record<string, unknown> | null = null
    lojasApi({
      'POST /api/estoque/lojas': (init) => {
        enviado = corpo(init)
        return problema(409, 'Seu plano permite até 2 lojas. Fale com o suporte para ampliar.')
      },
    })
    renderApp('/configuracoes/lojas')
    expect(await screen.findByText('19 peças')).toBeInTheDocument()
    const linha = within(screen.getByRole('table', { name: 'Lojas' })).getByRole('row', { name: /Depósito/ })
    expect(within(linha).getByText('Ativa')).toBeInTheDocument()
    expect(screen.getByText('2 registros')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Nova loja' }))
    await userEvent.type(screen.getByLabelText('Nome'), 'Loja Shopping')
    await userEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('até 2 lojas')
    expect(enviado).toEqual({ nome: 'Loja Shopping' })
  })

  it('filtra por situação e busca', async () => {
    lojasApi({
      'GET /api/estoque/lojas?situacao=inativas&pagina=1&tamanho=20': () => paginaDeLojas([{ ...lojas[1]!, ativa: false }]),
      'GET /api/estoque/lojas?situacao=inativas&busca=dep&pagina=1&tamanho=20': () => paginaDeLojas([{ ...lojas[1]!, ativa: false }]),
    })
    renderApp('/configuracoes/lojas')
    await screen.findByText('19 peças')

    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'inativas')
    expect(await screen.findByText('Inativa')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Buscar'), 'dep')
    expect(await screen.findByText('1 registro')).toBeInTheDocument()
  })

  it('renomear e desativar com confirmação (mostra o motivo da recusa)', async () => {
    let renomeado: Record<string, unknown> | null = null
    lojasApi({
      'PUT /api/estoque/lojas/l2': (init) => {
        renomeado = corpo(init)
        return json({ ...lojas[1], nome: 'Depósito Central' })
      },
      'POST /api/estoque/lojas/l1/desativar': () => problema(409, 'Kamus Moda Centro ainda tem estoque.'),
    })
    renderApp('/configuracoes/lojas')

    await userEvent.click(await screen.findByRole('button', { name: 'Renomear Depósito' }))
    const dialogo = screen.getByRole('dialog', { name: 'Renomear loja' })
    await userEvent.clear(within(dialogo).getByLabelText('Nome'))
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Depósito Central')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(renomeado).toEqual({ nome: 'Depósito Central' }))

    await userEvent.click(await screen.findByRole('button', { name: 'Desativar Kamus Moda Centro' }))
    const confirmar = screen.getByRole('dialog', { name: 'Desativar loja' })
    await userEvent.click(within(confirmar).getByRole('button', { name: 'Desativar' }))
    expect(await within(confirmar).findByRole('alert')).toHaveTextContent('ainda tem estoque')
  })

  it('seleção: desativa em lote e exporta só as selecionadas', async () => {
    let lote: Record<string, unknown> | null = null
    const fetch = lojasApi({
      'POST /api/estoque/lojas/situacao': (init) => {
        lote = corpo(init)
        return json({ alterados: 1 })
      },
      'GET /api/relatorios/lojas.pdf?ids=l2': () => new Response('%PDF', { headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': 'attachment; filename=lojas.pdf' } }),
    })
    URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
    URL.revokeObjectURL = () => {}
    renderApp('/configuracoes/lojas')

    await userEvent.click(await screen.findByRole('checkbox', { name: 'Selecionar Depósito' }))
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /PDF/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/lojas.pdf?ids=l2')).toBe(true))

    await userEvent.click(within(screen.getByRole('region', { name: 'Seleção' })).getByRole('button', { name: 'Desativar selecionadas' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Desativar lojas' })).getByRole('button', { name: 'Desativar' }))
    await waitFor(() => expect(lote).toEqual({ ids: ['l2'], ativo: false }))
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Seleção' })).not.toBeInTheDocument())
  })
})
