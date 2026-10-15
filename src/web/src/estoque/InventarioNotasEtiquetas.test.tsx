import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const lojas = [{ id: 'l1', empresaId: 'e1', nome: 'Kamus Moda Centro', ativa: true, quantidade: 19 }]
const cores = [{ id: 'c1', nome: 'Azul', hex: '#1D4ED8' }, { id: 'c2', nome: 'Preto', hex: '#111827' }]
const celula = (skuId: string, corId: string, tamanho: string, quantidade: number) => ({
  skuId, corId, tamanho, codigoDeBarras: `20000000000${skuId}`, ativo: true, quantidade, custoMedio: quantidade ? 22 : null,
})
const matriz = {
  produtoId: 'p1', referencia: 'CAM-001', descricao: 'Camiseta Básica', precoVenda: 59.9, precoCusto: 24, tamanhos: ['P', 'M'], cores,
  celulas: [celula('1', 'c1', 'P', 0), celula('2', 'c1', 'M', 5), celula('3', 'c2', 'P', 2), celula('4', 'c2', 'M', 0)],
  lojas: [{ lojaId: 'l1', nome: 'Kamus Moda Centro', quantidade: 7 }], total: 7,
}
const produtoResumo = { id: 'p1', referencia: 'CAM-001', descricao: 'Camiseta Básica', marca: null, categoria: null, colecao: null, precoVenda: 59.9, markup: 2.5, skus: 4, cores: ['Azul', 'Preto'], ativo: true }
const pdf = () => new Response('%PDF', { headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': "attachment; filename=etiquetas.pdf; filename*=UTF-8''etiquetas.pdf" } })

function api(extras: Parameters<typeof mockApi>[0] = {}) {
  URL.createObjectURL = vi.fn(() => 'blob:x') as unknown as typeof URL.createObjectURL
  URL.revokeObjectURL = vi.fn()
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessaoDemo),
    'GET /health': () => new Response('Healthy'),
    'GET /api/estoque/lojas': () => json(lojas),
    'GET /api/estoque/posicao/p1?lojaId=l1': () => json(matriz),
    'GET /api/estoque/posicao/p1': () => json(matriz),
    'GET /api/produtos?situacao=ativos&busca=cam': () => json({ itens: [produtoResumo], total: 1 }),
    ...extras,
  })
}

async function escolherCamiseta() {
  await userEvent.type(await screen.findByLabelText('Buscar produto'), 'cam')
  await userEvent.click(await screen.findByRole('button', { name: /Camiseta Básica/ }))
  await screen.findByRole('table', { name: 'Estoque de Camiseta Básica' })
}

describe('Estoque → Etiquetas', () => {
  it('usa o saldo como quantidade e gera o PDF no formato escolhido', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/estoque/etiquetas': (init) => {
        enviado = corpo(init)
        return pdf()
      },
    })
    renderApp('/estoque/etiquetas')
    await within(await screen.findByLabelText('Loja')).findByRole('option', { name: 'Kamus Moda Centro' })
    await userEvent.selectOptions(screen.getByLabelText('Formato'), 'termica')
    await escolherCamiseta()

    await userEvent.click(screen.getByRole('button', { name: 'Usar o saldo atual' }))
    expect(screen.getByLabelText('Azul M')).toHaveValue('5')
    await userEvent.click(screen.getByRole('button', { name: 'Adicionar à lista' }))
    await userEvent.click(screen.getByRole('button', { name: 'Gerar PDF' }))

    expect(await screen.findByRole('status')).toHaveTextContent('PDF gerado: 7 etiquetas.')
    expect(enviado).toEqual({ formato: 'termica', itens: [{ skuId: '2', quantidade: 5 }, { skuId: '3', quantidade: 2 }] })
  })

  it('depois da entrada, oferece as etiquetas das peças recebidas', async () => {
    const chamadas = api({
      'POST /api/estoque/entradas': () => json({ operacaoId: 'op9', itens: 1, quantidade: 3 }),
      'GET /api/estoque/etiquetas/operacao/op9?formato=a4': () => pdf(),
    })
    renderApp('/estoque/entradas')
    await within(await screen.findByLabelText('Loja')).findByRole('option', { name: 'Kamus Moda Centro' })
    await escolherCamiseta()
    await userEvent.type(screen.getByLabelText('Azul P'), '3')
    await userEvent.click(screen.getByRole('button', { name: 'Adicionar à lista' }))
    await userEvent.click(screen.getByRole('button', { name: 'Lançar entrada' }))

    await userEvent.click(await screen.findByRole('button', { name: 'Etiquetas (A4)' }))

    expect(chamadas.mock.calls.some(([url]) => url === '/api/estoque/etiquetas/operacao/op9?formato=a4')).toBe(true)
  })
})

const inventario = { id: 'i1', lojaId: 'l1', loja: 'Kamus Moda Centro', status: 'Aberto', observacao: 'Balanço', abertoEm: '2026-10-15T12:00:00Z', abertoPor: 'Admin Demo', encerradoEm: null, concluidoPor: null, skusContados: 1, pecasContadas: 4 }
const contagem = {
  produtoId: 'p1', referencia: 'CAM-001', descricao: 'Camiseta Básica', tamanhos: ['P', 'M'], cores,
  celulas: [{ skuId: '1', corId: 'c1', tamanho: 'P', ativo: true, contado: null }, { skuId: '2', corId: 'c1', tamanho: 'M', ativo: true, contado: 4 },
    { skuId: '3', corId: 'c2', tamanho: 'P', ativo: true, contado: null }, { skuId: '4', corId: 'c2', tamanho: 'M', ativo: true, contado: null }],
}
const divergencias = (zerar: boolean) => ({
  itens: [
    { skuId: '2', referencia: 'CAM-001', descricao: 'Camiseta Básica', cor: 'Azul', tamanho: 'M', saldoNaAbertura: 5, contado: 4, diferenca: -1, valorDaDiferenca: -22 },
    ...(zerar ? [{ skuId: '3', referencia: 'CAM-001', descricao: 'Camiseta Básica', cor: 'Preto', tamanho: 'P', saldoNaAbertura: 2, contado: null, diferenca: -2, valorDaDiferenca: -44 }] : []),
  ],
  sobras: 0, faltas: zerar ? 3 : 1, valor: zerar ? -66 : -22, naoContados: 1,
})

describe('Estoque → Inventário', () => {
  it('abre o inventário da loja', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'GET /api/estoque/inventarios': () => json([]),
      'POST /api/estoque/inventarios': (init) => {
        enviado = corpo(init)
        return json(inventario, 201)
      },
      'GET /api/estoque/inventarios/i1': () => json(inventario),
    })
    renderApp('/estoque/inventario')
    expect(await screen.findByText('Nenhum inventário ainda.')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Observação (opcional)'), 'Balanço')

    await userEvent.click(screen.getByRole('button', { name: 'Abrir inventário' }))

    expect(await screen.findByRole('heading', { name: /Inventário — Kamus Moda Centro/ })).toBeInTheDocument()
    expect(enviado).toEqual({ lojaId: 'l1', observacao: 'Balanço' })
  })

  it('conta pela grade sem ver o saldo e salva só o que mudou', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'GET /api/estoque/inventarios/i1': () => json(inventario),
      'GET /api/estoque/inventarios/i1/produtos/p1': () => json(contagem),
      'PUT /api/estoque/inventarios/i1/contagens': (init) => {
        enviado = corpo(init)
        return json(inventario)
      },
    })
    renderApp('/estoque/inventario?id=i1')
    await escolherCamiseta()

    expect(screen.getByLabelText('Azul M')).toHaveValue('4')
    expect(screen.queryByText(/hoje/)).not.toBeInTheDocument() // contagem cega
    await userEvent.type(screen.getByLabelText('Preto P'), '2')
    await userEvent.clear(screen.getByLabelText('Azul M'))
    await userEvent.click(screen.getByRole('button', { name: 'Salvar contagem' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Contagem de CAM-001 salva.')
    expect(enviado).toEqual({ itens: [{ skuId: '2', quantidade: null }, { skuId: '3', quantidade: 2 }] })
  })

  it('leitor soma a cada código e avisa código desconhecido', async () => {
    api({
      'GET /api/estoque/inventarios/i1': () => json(inventario),
      'POST /api/estoque/inventarios/i1/leituras': (init) =>
        corpo(init).codigo === '999'
          ? problema(404, 'Código 999 não encontrado no catálogo.')
          : json({ skuId: '2', referencia: 'CAM-001', descricao: 'Camiseta Básica', cor: 'Azul', tamanho: 'M', contado: 5 }),
    })
    renderApp('/estoque/inventario?id=i1')
    await userEvent.click(await screen.findByRole('tab', { name: 'Leitor de código' }))

    await userEvent.type(screen.getByLabelText('Código de barras'), '200000000002{Enter}')
    expect(await within(screen.getByRole('list', { name: 'Últimas leituras' })).findByText('contado: 5')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Código de barras'), '999{Enter}')

    expect(await screen.findByRole('alert')).toHaveTextContent('Código 999 não encontrado')
    expect(screen.getByLabelText('Código de barras')).toHaveValue('')
  })

  it('divergências e conclusão zerando os não contados', async () => {
    let concluido: Record<string, unknown> | null = null
    api({
      'GET /api/estoque/inventarios/i1': () => json(concluido ? { ...inventario, status: 'Concluido' } : inventario),
      'GET /api/estoque/inventarios/i1/divergencias?zerarNaoContados=false': () => json(divergencias(false)),
      'GET /api/estoque/inventarios/i1/divergencias?zerarNaoContados=true': () => json(divergencias(true)),
      'POST /api/estoque/inventarios/i1/concluir': (init) => {
        concluido = corpo(init)
        return json({ operacaoId: 'op1', ajustes: 2, sobras: 0, faltas: 3 })
      },
    })
    renderApp('/estoque/inventario?id=i1')
    await userEvent.click(await screen.findByRole('tab', { name: 'Divergências' }))
    expect(await screen.findByText(/1 SKU\(s\) com saldo ainda não contados/)).toBeInTheDocument()

    await userEvent.click(screen.getByLabelText('Considerar como zero o que não foi contado'))
    expect(await screen.findByText('Faltas: 3 peças')).toBeInTheDocument()
    expect(within(screen.getByRole('table', { name: 'Divergências' })).getByText('-2')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Concluir e ajustar' }))
    const dialogo = screen.getByRole('dialog', { name: 'Concluir inventário' })
    expect(within(dialogo).getByRole('checkbox')).toBeChecked()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Concluir e ajustar' }))

    expect(await screen.findByRole('status')).toHaveTextContent('2 SKU(s) ajustados')
    expect(concluido).toEqual({ zerarNaoContados: true })
    expect(await screen.findByText('Concluído')).toBeInTheDocument()
  })
})

const item = (numero: number, extra = {}) => ({
  numero, codigo: `AUR-${numero}00`, ean: null, descricao: `ITEM ${numero}`, unidade: 'UN', quantidade: 10, custoTotal: 260, custoPorPeca: 26, fator: 1,
  pecasEsperadas: 10, situacao: 'Pendente', origemDoVinculo: null, produtoId: null, produto: null, distribuicao: [], ...extra,
})
const nota = (extra = {}) => ({
  id: 'n1', chave: '3526', numero: '4567', serie: '1', emitidaEm: '2026-10-14T12:15:00Z', fornecedorDocumento: '11222333000181', fornecedorNome: 'Aurora Moda',
  fornecedorCadastrado: false, valorTotal: 600, autorizada: true, status: 'Pendente', prontaParaLancar: false, loja: null, operacaoId: null,
  itens: [item(1), item(2, { situacao: 'Pronto', origemDoVinculo: 'ean', produto: 'CAM-001 — Camiseta Básica', distribuicao: [{ skuId: '3', cor: 'Preto', tamanho: 'P', quantidade: 10 }] })],
  duplicatas: [{ numero: '001', vencimento: '2026-11-13', valor: 300 }],
  ...extra,
})

describe('Estoque → Entrada por XML', () => {
  it('importa o XML e abre a conferência', async () => {
    let enviado: FormData | null = null
    api({
      'GET /api/estoque/notas': () => json([]),
      'POST /api/estoque/notas': (init) => {
        enviado = init?.body as FormData
        return json(nota(), 201)
      },
      'GET /api/estoque/notas/n1': () => json(nota()),
    })
    renderApp('/estoque/xml')
    await userEvent.upload(await screen.findByLabelText('Arquivo XML da NF-e'), new File(['<nfeProc/>'], 'nota.xml', { type: 'text/xml' }))
    await userEvent.click(screen.getByRole('button', { name: 'Importar XML' }))

    expect(await screen.findByRole('heading', { name: 'NF 4567/1 — Aurora Moda' })).toBeInTheDocument()
    expect((enviado as FormData | null)?.get('arquivo')).toBeInstanceOf(File)
    expect(screen.getByText('Fornecedor novo: será cadastrado ao lançar')).toBeInTheDocument()
    const itens = screen.getByRole('table', { name: 'Itens da nota' })
    expect(within(itens).getByText('pelo código de barras')).toBeInTheDocument()
    expect(within(itens).getByText('Preto P: 10')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Lançar entrada' })).toBeDisabled()
  })

  it('recusa com o motivo da API', async () => {
    api({ 'GET /api/estoque/notas': () => json([]), 'POST /api/estoque/notas': () => problema(400, 'Esta nota foi emitida para o CNPJ 98.765.432/0001-98.') })
    renderApp('/estoque/xml')
    await userEvent.upload(await screen.findByLabelText('Arquivo XML da NF-e'), new File(['x'], 'nota.xml', { type: 'text/xml' }))
    await userEvent.click(screen.getByRole('button', { name: 'Importar XML' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('98.765.432/0001-98')
  })

  it('vincula o item na grade com fator, lança e oferece etiquetas', async () => {
    let vinculado: Record<string, unknown> | null = null
    let lancada = false
    api({
      'GET /api/estoque/notas/n1': () =>
        json(lancada ? nota({ status: 'Lancada', loja: 'Kamus Moda Centro', operacaoId: 'op5' })
          : vinculado ? nota({ prontaParaLancar: true, itens: [item(1, { situacao: 'Pronto' }), nota().itens[1]] }) : nota()),
      'PUT /api/estoque/notas/n1/itens/1': (init) => {
        vinculado = corpo(init)
        return json(item(1, { situacao: 'Pronto' }))
      },
      'POST /api/estoque/notas/n1/lancar': () => {
        lancada = true
        return json({ operacaoId: 'op5', itens: 3, quantidade: 20 })
      },
    })
    renderApp('/estoque/xml?id=n1')

    await userEvent.click(await screen.findByRole('button', { name: 'Vincular item 1' }))
    const dialogo = screen.getByRole('dialog', { name: 'Vincular item 1' })
    await userEvent.clear(within(dialogo).getByLabelText('Peças por UN'))
    await userEvent.type(within(dialogo).getByLabelText('Peças por UN'), '2')
    expect(within(dialogo).getByText('A nota traz 20 peça(s) para distribuir.')).toBeInTheDocument()
    await userEvent.type(within(dialogo).getByLabelText('Buscar produto'), 'cam')
    await userEvent.click(await within(dialogo).findByRole('button', { name: /Camiseta Básica/ }))
    await userEvent.type(await within(dialogo).findByLabelText('Azul P'), '12')
    await userEvent.type(within(dialogo).getByLabelText('Azul M'), '8')
    expect(within(dialogo).getByText('Distribuídas: 20 de 20 peça(s).')).toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar vínculo' }))

    expect(vinculado).toEqual({ produtoId: 'p1', fator: 2, distribuicao: [{ skuId: '1', quantidade: 12 }, { skuId: '2', quantidade: 8 }] })
    const lancar = await screen.findByRole('button', { name: 'Lançar entrada' })
    await vi.waitFor(() => expect(lancar).toBeEnabled())
    await userEvent.click(lancar)

    expect(await screen.findByText('Entrada lançada: 20 peças.')).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'Etiquetas (térmica)' })).toBeInTheDocument()
  })
})
