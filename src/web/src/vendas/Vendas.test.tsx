import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { hojeIso } from '../financeiro/tipos'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'
import { inicioDoMes } from './tipos'

const formas = [
  { id: 'f1', nome: 'Dinheiro', tipo: 'Dinheiro', contaId: 'c1', taxaAVista: 0, taxaParcelado: 0, maxParcelas: 1, prazoDias: 0, ativa: true },
  { id: 'f2', nome: 'PIX', tipo: 'Pix', contaId: null, taxaAVista: 0, taxaParcelado: 0, maxParcelas: 1, prazoDias: 0, ativa: true },
  { id: 'f4', nome: 'Cartão de crédito', tipo: 'Credito', contaId: null, taxaAVista: 3.5, taxaParcelado: 5.5, maxParcelas: 12, prazoDias: 30, ativa: true },
]
const produto = { id: 'p1', referencia: 'CAM-001', descricao: 'Camiseta básica', precoVenda: 59.9 }
const porCodigo = { produto, sku: { id: 's1', corId: 'k1', tamanho: 'M', codigoDeBarras: '7891234567895', precoVenda: null, preco: 59.9, ativo: true }, cor: 'Azul' }
const matriz = {
  produtoId: 'p1', referencia: 'CAM-001', descricao: 'Camiseta básica', precoVenda: 59.9, precoCusto: 20, tamanhos: ['M', 'G'],
  cores: [{ id: 'k1', nome: 'Azul', hex: '#00f' }],
  celulas: [
    { skuId: 's1', corId: 'k1', tamanho: 'M', codigoDeBarras: '1', ativo: true, quantidade: 3, custoMedio: 20, preco: 59.9 },
    { skuId: 's2', corId: 'k1', tamanho: 'G', codigoDeBarras: '2', ativo: true, quantidade: 0, custoMedio: null, preco: 64.9 },
  ],
  lojas: [], total: 3,
}
const venda = (extra = {}) => ({
  id: 'v7', empresaId: 'e1', lojaId: 'l1', numero: 7, data: '2026-10-17', clienteId: null, clienteNome: 'Maria', vendedorId: 'b1', vendedorNome: 'Bia',
  comissaoPercentual: 5, comissao: 5.99, subtotal: 119.8, descontoGeral: 0, total: 119.8, troco: 0.2, pecas: 2, taxaTotal: 0, custoTotal: 40, lucroBruto: 79.8,
  status: 'Concluida', notaExterna: 'NFC-e 991', observacao: null, registradaPor: 'Admin Demo', registradaEm: '2026-10-17T13:00:00Z',
  motivoDoCancelamento: null, canceladaPor: null, canceladaEm: null,
  itens: [{ skuId: 's1', produtoId: 'p1', descricao: 'Camiseta básica Azul M', quantidade: 2, precoUnitario: 59.9, desconto: 0, total: 119.8, custoUnitario: 20 }],
  pagamentos: [{ formaId: 'f1', formaNome: 'Dinheiro', tipo: 'Dinheiro', valor: 119.8, parcelas: 1, taxaPercentual: 0, valorTaxa: 0 }],
  avisos: [], ...extra,
})

/** O leitor e a busca só liberam depois que a loja carrega. */
async function campoLiberado(rotulo: string) {
  const campo = await screen.findByLabelText(rotulo)
  await waitFor(() => expect(campo).toBeEnabled())
  return campo
}

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/estoque/lojas': () => json([{ id: 'l1', empresaId: 'e1', nome: 'Loja Centro', ativa: true, quantidade: 3 }]),
    'GET /api/vendedores': () => json([{ id: 'b1', nome: 'Bia', comissaoPercentual: 5, usuarioId: 'u2', ativo: true }]),
    'GET /api/vendas/formas': () => json(formas),
    'GET /api/vendas/formas?situacao=todas&pagina=1&tamanho=20': () => json({ itens: formas, total: 3, pagina: 1, tamanho: 20 }),
    'GET /api/financeiro/contas': () => json([{ id: 'c1', empresaId: 'e1', nome: 'Caixa da loja', tipo: 'Caixa', saldoInicial: 0, saldoInicialEm: '2026-10-01', saldo: 0, ativa: true }]),
    ...extras,
  })
}

describe('Vendas → Registrar venda', () => {
  it('lê o código de barras, soma a mesma peça, calcula o troco e registra', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'GET /api/produtos/por-codigo/7891234567895': () => json(porCodigo),
      'POST /api/vendas': (init) => {
        enviado = corpo(init)
        return json(venda({ avisos: ['Camiseta básica Azul M ficou com estoque -1.'] }), 201)
      },
    })
    renderApp('/vendas/registro')

    const leitor = await campoLiberado('Código de barras')
    await userEvent.type(leitor, '7891234567895{Enter}')
    await userEvent.type(leitor, '7891234567895{Enter}')
    const itens = await screen.findByRole('table', { name: 'Itens da venda' })
    expect(within(itens).getByText('Camiseta básica · Azul M')).toBeInTheDocument()
    expect(screen.getByLabelText('Quantidade de Camiseta básica · Azul M')).toHaveValue('2')
    expect(screen.getByText('R$ 119,80', { selector: 'dd' })).toBeInTheDocument()

    expect(screen.getByLabelText('Forma do pagamento 1')).toHaveValue('f1')
    const valor = screen.getByLabelText('Valor do pagamento 1')
    expect(valor).toHaveValue('119,80')
    await userEvent.clear(valor)
    await userEvent.type(valor, '120')
    expect(screen.getByText('Troco: R$ 0,20')).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Nota externa (opcional)'), 'NFC-e 991')
    await userEvent.click(screen.getByRole('button', { name: 'Registrar venda' }))

    expect(await screen.findByText('Venda nº 7 registrada: R$ 119,80')).toBeInTheDocument()
    expect(screen.getByText('Camiseta básica Azul M ficou com estoque -1.')).toBeInTheDocument()
    expect(enviado).toEqual({
      lojaId: 'l1', data: hojeIso(), clienteId: null, clienteNome: null, vendedorId: null, descontoGeral: 0, notaExterna: 'NFC-e 991', observacao: null,
      itens: [{ skuId: 's1', quantidade: 2, desconto: 0 }],
      pagamentos: [{ formaId: 'f1', valor: 120, parcelas: 1 }],
    })
  })

  it('escolhe na grade, divide entre dinheiro e crédito parcelado e só libera quando fecha o total', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'GET /api/produtos?situacao=ativos&busca=cam': () => json({ itens: [{ id: 'p1', referencia: 'CAM-001', descricao: 'Camiseta básica' }], total: 1 }),
      'GET /api/estoque/posicao/p1?lojaId=l1': () => json(matriz),
      'POST /api/vendas': (init) => {
        enviado = corpo(init)
        return json(venda(), 201)
      },
    })
    renderApp('/vendas/registro')

    await userEvent.type(await campoLiberado('Buscar produto'), 'cam')
    await userEvent.click(await screen.findByRole('button', { name: /Camiseta básica/ }))
    await screen.findByRole('table', { name: 'Estoque de Camiseta básica' })
    await userEvent.type(screen.getByLabelText('Azul M'), '1')
    await userEvent.type(screen.getByLabelText('Azul G'), '1')
    await userEvent.click(screen.getByRole('button', { name: 'Adicionar à venda' }))
    expect(screen.getByText('R$ 124,80', { selector: 'dd' })).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Desconto na venda (R$)'), '4,80')
    const valor1 = screen.getByLabelText('Valor do pagamento 1')
    await userEvent.clear(valor1)
    await userEvent.type(valor1, '20')
    expect(screen.getByText('Falta: R$ 100,00')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Registrar venda' })).toBeDisabled()

    await userEvent.click(screen.getByRole('button', { name: 'Adicionar forma de pagamento' }))
    await userEvent.selectOptions(screen.getByLabelText('Forma do pagamento 2'), 'f4')
    expect(screen.getByLabelText('Valor do pagamento 2')).toHaveValue('100,00')
    await userEvent.selectOptions(screen.getByLabelText('Parcelas do pagamento 2'), '3')
    await userEvent.selectOptions(screen.getByLabelText('Vendedor'), 'b1')
    await userEvent.click(screen.getByRole('button', { name: 'Registrar venda' }))

    await screen.findByText('Venda nº 7 registrada: R$ 119,80')
    expect(enviado).toMatchObject({
      vendedorId: 'b1',
      descontoGeral: 4.8,
      itens: [{ skuId: 's1', quantidade: 1, desconto: 0 }, { skuId: 's2', quantidade: 1, desconto: 0 }],
      pagamentos: [{ formaId: 'f1', valor: 20, parcelas: 1 }, { formaId: 'f4', valor: 100, parcelas: 3 }],
    })
  })

  it('mostra o erro da API sem perder a venda', async () => {
    api({
      'GET /api/produtos/por-codigo/7891234567895': () => json(porCodigo),
      'POST /api/vendas': () => problema(409, 'Camiseta básica Azul M: há 0 em estoque e a venda pede 1.'),
    })
    renderApp('/vendas/registro')
    await userEvent.type(await campoLiberado('Código de barras'), '7891234567895{Enter}')
    await screen.findByRole('table', { name: 'Itens da venda' })
    await userEvent.click(screen.getByRole('button', { name: 'Registrar venda' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('há 0 em estoque')
    expect(screen.getByRole('table', { name: 'Itens da venda' })).toBeInTheDocument()
  })

  it('o vendedor já vem escolhido para quem está vinculado a ele', async () => {
    api({}, sessaoVendedor)
    renderApp('/vendas/registro')
    await screen.findByLabelText('Código de barras')
    expect(await screen.findByLabelText('Vendedor')).toHaveValue('b1')
  })
})

describe('Vendas → Vendas realizadas', () => {
  const filtro = `de=${inicioDoMes(hojeIso())}&ate=${hojeIso()}&status=todas&pagina=1&tamanho=20`
  const lista = {
    itens: [{ id: 'v7', empresaId: 'e1', numero: 7, data: '2026-10-17', clienteNome: 'Maria', vendedorId: 'b1', vendedorNome: 'Bia', pecas: 2, total: 119.8,
      formas: 'Dinheiro', status: 'Concluida', notaExterna: 'NFC-e 991' }],
    quantidade: 1, pecas: 2, total: 119.8, ticketMedio: 119.8, registros: 1, pagina: 1, tamanho: 20,
  }

  async function gradeCarregada() {
    const tabela = await screen.findByRole('table', { name: 'Vendas' })
    await within(tabela).findByText('Maria')
    return tabela
  }

  it('grade com totais de todas as filtradas, total de registros, paginação e "Nova venda"', async () => {
    const pagina = (n: number) => json({ ...lista, itens: [{ ...lista.itens[0], id: `v${n}`, numero: n }], quantidade: 30, total: 3000, registros: 30, pagina: n })
    api({
      [`GET /api/vendas?${filtro}`]: () => pagina(1),
      [`GET /api/vendas?${filtro.replace('status=todas', 'status=canceladas')}`]: () => pagina(1),
      [`GET /api/vendas?${filtro.replace('status=todas', 'status=canceladas').replace('pagina=1', 'pagina=2')}`]: () => pagina(2),
    })
    renderApp('/vendas/realizadas')

    const tabela = await gradeCarregada()
    expect(within(tabela).getByText('Maria')).toBeInTheDocument()
    expect(screen.getByText('R$ 3.000,00', { selector: 'dd' })).toBeInTheDocument()
    expect(screen.getByText('30 registros')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Nova venda/ })).toHaveAttribute('href', '/vendas/registro')

    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'canceladas')
    await userEvent.click(await screen.findByRole('button', { name: 'Próxima página' }))
    expect(await within(tabela).findByRole('button', { name: '2' })).toBeInTheDocument()
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }))
    expect(screen.getByLabelText('Situação')).toHaveValue('todas')
  })

  it('abre o detalhe e cancela com motivo', async () => {
    let motivo: unknown = null
    api({
      [`GET /api/vendas?${filtro}`]: () => json(lista),
      'GET /api/vendas/v7': () => json(venda()),
      'POST /api/vendas/v7/cancelar': (init) => {
        motivo = corpo(init).motivo
        return json(venda({ status: 'Cancelada', motivoDoCancelamento: 'Cliente desistiu', canceladaPor: 'Admin Demo' }))
      },
    })
    renderApp('/vendas/realizadas')

    const tabela = await gradeCarregada()
    expect(screen.getAllByText('R$ 119,80', { selector: 'dd' })).toHaveLength(2)

    await userEvent.click(within(tabela).getByRole('button', { name: 'Ver venda nº 7' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Venda nº 7' })
    expect(within(dialogo).getByText('Camiseta básica Azul M')).toBeInTheDocument()
    expect(within(dialogo).getByText('Lucro bruto')).toBeInTheDocument()

    await userEvent.click(within(dialogo).getByRole('button', { name: 'Cancelar venda' }))
    await userEvent.type(within(dialogo).getByLabelText('Motivo do cancelamento'), 'Cliente desistiu')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Confirmar cancelamento' }))
    expect(await within(dialogo).findByText(/Cancelada por Admin Demo/)).toBeInTheDocument()
    expect(motivo).toBe('Cliente desistiu')
  })

  it('cancela pela linha com motivo e exporta só as selecionadas', async () => {
    let motivo: unknown = null
    const fetch = api({
      [`GET /api/vendas?${filtro}`]: () => json(lista),
      'POST /api/vendas/v7/cancelar': (init) => {
        motivo = corpo(init).motivo
        return json(venda({ status: 'Cancelada' }))
      },
      'GET /api/relatorios/vendas.csv?ids=v7': () => new Response('csv', { headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=vendas.csv' } }),
    })
    URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
    URL.revokeObjectURL = () => {}
    renderApp('/vendas/realizadas')
    const tabela = await gradeCarregada()

    await userEvent.click(within(tabela).getByRole('button', { name: 'Cancelar venda nº 7' }))
    const confirmar = screen.getByRole('dialog', { name: 'Cancelar venda nº 7' })
    await userEvent.type(within(confirmar).getByLabelText('Motivo do cancelamento'), 'Duplicada')
    await userEvent.click(within(confirmar).getByRole('button', { name: 'Cancelar venda' }))
    await waitFor(() => expect(motivo).toBe('Duplicada'))

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar venda nº 7' }))
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/vendas.csv?ids=v7')).toBe(true))
  })

  it('o vendedor vê as suas vendas, sem cancelar nem ver o lucro', async () => {
    api({
      [`GET /api/vendas?${filtro}`]: () => json(lista),
      'GET /api/vendas/v7': () => json(venda({ custoTotal: null, lucroBruto: null })),
    }, sessaoVendedor)
    renderApp('/vendas/realizadas')
    const tabela = await gradeCarregada()
    expect(within(tabela).queryByRole('button', { name: 'Cancelar venda nº 7' })).not.toBeInTheDocument()
    await userEvent.click(within(tabela).getByRole('button', { name: '7' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Venda nº 7' })
    expect(within(dialogo).queryByText('Lucro bruto')).not.toBeInTheDocument()
    expect(within(dialogo).queryByRole('button', { name: 'Cancelar venda' })).not.toBeInTheDocument()
  })
})

describe('Vendas → Formas de pagamento', () => {
  it('avisa forma sem conta, edita a conta e muda a regra de estoque negativo', async () => {
    const condicional = { prazoCondicionalDias: 2, limiteCondicionalPecas: 10, limiteCondicionalValor: 1000 }
    let forma: Record<string, unknown> | null = null
    let regra: Record<string, unknown> | null = null
    api({
      'GET /api/vendas/configuracao': () => json({ permitirEstoqueNegativo: true, prazoDeTrocaDias: 30, validadeDoValeDias: 90, ...condicional }),
      'PUT /api/vendas/formas/f2': (init) => {
        forma = corpo(init)
        return json({ ...formas[1], contaId: 'c1' })
      },
      'PUT /api/vendas/configuracao': (init) => {
        regra = corpo(init)
        return json(regra)
      },
    })
    renderApp('/vendas/formas-de-pagamento')

    const tabela = await screen.findByRole('table', { name: 'Formas de pagamento' })
    expect(await within(tabela).findByText('Caixa da loja')).toBeInTheDocument()
    expect(screen.getByText('3 registros')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('PIX')

    await userEvent.click(within(tabela).getByRole('button', { name: 'Editar PIX' }))
    const dialogo = await screen.findByRole('dialog', { name: 'PIX' })
    await userEvent.selectOptions(within(dialogo).getByLabelText('Conta onde o dinheiro entra'), 'c1')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))
    expect(forma).toMatchObject({ nome: 'PIX', tipo: 'Pix', contaId: 'c1' })

    const caixa = await screen.findByRole('checkbox', { name: /Permitir vender sem saldo/ })
    expect(caixa).toBeChecked()
    await userEvent.click(caixa)
    expect(regra).toEqual({ permitirEstoqueNegativo: false, prazoDeTrocaDias: 30, validadeDoValeDias: 90, ...condicional })

    const prazo = screen.getByLabelText('Prazo de troca (dias)')
    await userEvent.clear(prazo)
    await userEvent.type(prazo, '15')
    await userEvent.click(screen.getByRole('button', { name: 'Salvar regras' }))
    await waitFor(() => expect(regra).toEqual({ permitirEstoqueNegativo: false, prazoDeTrocaDias: 15, validadeDoValeDias: 90, ...condicional }))

    const pecas = screen.getByLabelText('Limite de peças na condicional')
    await userEvent.clear(pecas)
    await userEvent.type(pecas, '6')
    await userEvent.click(screen.getByRole('button', { name: 'Salvar regras' }))
    await waitFor(() => expect(regra).toMatchObject({ prazoDeTrocaDias: 15, limiteCondicionalPecas: 6, limiteCondicionalValor: 1000, prazoCondicionalDias: 2 }))
  })
})

describe('Vendas → Formas de pagamento (grade)', () => {
  const regras = { permitirEstoqueNegativo: true, prazoDeTrocaDias: 30, validadeDoValeDias: 90, prazoCondicionalDias: 2, limiteCondicionalPecas: 0, limiteCondicionalValor: 0 }

  it('filtra por situação e busca, desativa com confirmação, em lote e exporta as selecionadas', async () => {
    let desativada = false
    let lote: Record<string, unknown> | null = null
    const fetch = api({
      'GET /api/vendas/configuracao': () => json(regras),
      'GET /api/vendas/formas?situacao=inativas&pagina=1&tamanho=20': () => json({ itens: [], total: 0, pagina: 1, tamanho: 20 }),
      'GET /api/vendas/formas?situacao=todas&busca=pix&pagina=1&tamanho=20': () => json({ itens: [formas[1]], total: 1, pagina: 1, tamanho: 20 }),
      'POST /api/vendas/formas/f1/desativar': () => {
        desativada = true
        return json({ ...formas[0], ativa: false })
      },
      'POST /api/vendas/formas/situacao': (init) => {
        lote = corpo(init)
        return json({ alterados: 2 })
      },
      'GET /api/relatorios/formas-de-pagamento.csv?ids=f1%2Cf2': () =>
        new Response('csv', { headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=formas.csv' } }),
    })
    URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
    URL.revokeObjectURL = () => {}
    renderApp('/vendas/formas-de-pagamento')
    const tabela = await screen.findByRole('table', { name: 'Formas de pagamento' })
    await within(tabela).findByText('Caixa da loja')

    await userEvent.click(within(tabela).getByRole('button', { name: 'Desativar Dinheiro' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Desativar forma de pagamento' })).getByRole('button', { name: 'Desativar' }))
    await waitFor(() => expect(desativada).toBe(true))

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Dinheiro' }))
    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar PIX' }))
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/formas-de-pagamento.csv?ids=f1%2Cf2')).toBe(true))
    await userEvent.click(within(screen.getByRole('region', { name: 'Seleção' })).getByRole('button', { name: 'Desativar selecionadas' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Desativar formas de pagamento' })).getByRole('button', { name: 'Desativar' }))
    await waitFor(() => expect(lote).toEqual({ ids: ['f1', 'f2'], ativo: false }))

    await userEvent.type(screen.getByLabelText('Buscar'), 'pix')
    await waitFor(() => expect(within(tabela).queryByText('Dinheiro')).not.toBeInTheDocument())
    expect(screen.getByText('1 registro')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }))
    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'inativas')
    expect(await screen.findByText('Nenhuma forma de pagamento com esses filtros.')).toBeInTheDocument()
  })
})

describe('Vendas → Comissões', () => {
  it('mostra a comissão por vendedor no período com total', async () => {
    api({
      [`GET /api/vendas/comissoes?de=${inicioDoMes(hojeIso())}&ate=${hojeIso()}`]: () =>
        json([{ vendedorId: 'b1', vendedor: 'Bia', vendas: 2, total: 159.9, comissao: 8, comissaoPendente: 0 }, { vendedorId: null, vendedor: 'Sem vendedor', vendas: 1, total: 59.9, comissao: 0, comissaoPendente: 0 }]),
    })
    renderApp('/vendas/comissoes')
    const tabela = await screen.findByRole('table', { name: 'Comissões' })
    expect(within(tabela).getByText('Bia')).toBeInTheDocument()
    expect(within(tabela).getByText('R$ 219,80')).toBeInTheDocument()
    expect(within(tabela).getByText('R$ 8,00', { selector: 'tfoot td' })).toBeInTheDocument()
  })
})
