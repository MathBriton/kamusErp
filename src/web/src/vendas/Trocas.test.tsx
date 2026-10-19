import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { hojeIso } from '../financeiro/tipos'
import { corpo, empresaAtivaHandler, json, mockApi, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'
import { inicioDoMes } from './tipos'

const formas = [
  { id: 'f1', nome: 'Dinheiro', tipo: 'Dinheiro', contaId: 'c1', taxaAVista: 0, taxaParcelado: 0, maxParcelas: 1, prazoDias: 0, ativa: true },
  { id: 'f9', nome: 'Vale-troca', tipo: 'ValeTroca', contaId: null, taxaAVista: 0, taxaParcelado: 0, maxParcelas: 1, prazoDias: 0, ativa: true },
]
const listaDaVenda = {
  itens: [{ id: 'v7', empresaId: 'e1', numero: 7, data: '2026-10-17', clienteNome: 'Maria', vendedorId: 'b1', vendedorNome: 'Bia', pecas: 2, total: 109.02,
    formas: 'PIX', status: 'Concluida', notaExterna: null }],
  quantidade: 1, pecas: 2, total: 109.02, ticketMedio: 109.02,
}
const devolviveis = (extra = {}) => ({
  vendaId: 'v7', lojaId: 'l1', numero: 7, data: '2026-10-17', clienteNome: 'Maria', vendedorNome: 'Bia', diasDesdeAVenda: 3, prazoDeTrocaDias: 30, dentroDoPrazo: true,
  itens: [{ skuId: 's1', descricao: 'Camiseta básica Azul M', vendidas: 2, jaDevolvidas: 0, disponiveis: 2, valorPorPeca: 54.51 }],
  ...extra,
})
const vale = { id: 'vt1', codigo: 'VT7K2M9Q', clienteId: null, clienteNome: 'Maria', valor: 54.51, saldo: 54.51, emitidoEm: '2026-10-20', validoAte: '2027-01-18',
  situacao: 'Ativo', trocaId: 't3', motivoDoCancelamento: null }
const troca = (extra = {}) => ({
  id: 't3', numero: 3, data: '2026-10-20', vendaId: 'v7', vendaNumero: 7, clienteNome: 'Maria', vendedorNome: 'Bia', motivo: 'Defeito', observacao: null,
  valorDevolvido: 54.51, valorLevado: 0, diferenca: -54.51, troco: 0, comissao: -2.73, registradaPor: 'Admin Demo',
  devolvidos: [{ skuId: 's1', descricao: 'Camiseta básica Azul M', quantidade: 1, valor: 54.51, destino: 'Avariado' }], levados: [], pagamentos: [], vale, avisos: [],
  ...extra,
})
const periodo = `de=${inicioDoMes(hojeIso())}&ate=${hojeIso()}`

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/estoque/lojas': () => json([{ id: 'l1', empresaId: 'e1', nome: 'Loja Centro', ativa: true, quantidade: 3 }]),
    'GET /api/vendedores': () => json([{ id: 'b1', nome: 'Bia', comissaoPercentual: 5, usuarioId: null, ativo: true }]),
    'GET /api/vendas/formas': () => json(formas),
    [`GET /api/vendas/trocas?${periodo}`]: () => json([]),
    'GET /api/vendas/devolviveis?numero=7': () => json(devolviveis()),
    ...extras,
  })
}

async function abrirVenda() {
  await userEvent.type(await screen.findByLabelText('Nº da venda'), '7')
  await userEvent.click(screen.getByRole('button', { name: 'Buscar venda' }))
  return screen.findByRole('table', { name: 'Peças da venda' })
}

describe('Vendas → Trocas', () => {
  it('devolve peça com defeito sem levar nada e emite o vale-troca', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/vendas/trocas': (init) => {
        enviado = corpo(init)
        return json(troca(), 201)
      },
    })
    renderApp('/vendas/trocas')

    await abrirVenda()
    expect(screen.getByText(/há 3 dias · prazo de 30 dias/)).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Devolver Camiseta básica Azul M'), '1')
    await userEvent.selectOptions(screen.getByLabelText('Destino de Camiseta básica Azul M'), 'Avariado')
    await userEvent.selectOptions(screen.getByLabelText('Motivo'), 'Defeito')
    expect(screen.getByText('Vale-troca para o cliente: R$ 54,51')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Registrar troca' }))

    const feito = await screen.findByRole('status')
    expect(feito).toHaveTextContent('Troca nº 3 registrada')
    expect(within(feito).getByText('VT7K2M9Q')).toBeInTheDocument()
    expect(within(feito).getByRole('button', { name: 'Imprimir vale' })).toBeInTheDocument()
    expect(enviado).toEqual({
      vendaId: 'v7', motivo: 'Defeito', observacao: null, foraDoPrazo: false,
      devolvidos: [{ skuId: 's1', quantidade: 1, destino: 'Avariado' }], levados: [], pagamentos: [],
    })
  })

  it('leva uma peça mais cara e paga a diferença', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'GET /api/produtos/por-codigo/789': () =>
        json({ produto: { descricao: 'Vestido' }, sku: { id: 's9', tamanho: 'P', preco: 64.9, ativo: true }, cor: 'Preto' }),
      'POST /api/vendas/trocas': (init) => {
        enviado = corpo(init)
        return json(troca({ valorLevado: 64.9, diferenca: 10.39, vale: null }), 201)
      },
    })
    renderApp('/vendas/trocas')
    await abrirVenda()
    await userEvent.type(screen.getByLabelText('Devolver Camiseta básica Azul M'), '1')
    const leitor = screen.getByLabelText('Código de barras')
    await waitFor(() => expect(leitor).toBeEnabled())
    await userEvent.type(leitor, '789{Enter}')
    await screen.findByRole('table', { name: 'Peças levadas' })
    expect(screen.getByText('Cliente paga a diferença: R$ 10,39')).toBeInTheDocument()
    expect(screen.getByLabelText('Valor do pagamento 1')).toHaveValue('10,39')
    await userEvent.click(screen.getByRole('button', { name: 'Registrar troca' }))

    await screen.findByText(/Troca nº 3 registrada/)
    expect(enviado).toMatchObject({ levados: [{ skuId: 's9', quantidade: 1, desconto: 0 }], pagamentos: [{ formaId: 'f1', valor: 10.39, parcelas: 1 }] })
  })

  it('fora do prazo: Dono aceita marcando; vendedor não pode', async () => {
    api({ 'GET /api/vendas/devolviveis?numero=7': () => json(devolviveis({ diasDesdeAVenda: 40, dentroDoPrazo: false })) })
    renderApp('/vendas/trocas')
    await abrirVenda()
    await userEvent.type(screen.getByLabelText('Devolver Camiseta básica Azul M'), '1')
    const registrar = screen.getByRole('button', { name: 'Registrar troca' })
    expect(registrar).toBeDisabled()
    await userEvent.click(screen.getByRole('checkbox', { name: /Aceitar fora do prazo/ }))
    expect(registrar).toBeEnabled()
  })

  it('vendedor não vê a opção de aceitar fora do prazo', async () => {
    api({ 'GET /api/vendas/devolviveis?numero=7': () => json(devolviveis({ diasDesdeAVenda: 40, dentroDoPrazo: false })) }, sessaoVendedor)
    renderApp('/vendas/trocas')
    await abrirVenda()
    expect(screen.getByText(/Fora do prazo/)).toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: /Aceitar fora do prazo/ })).not.toBeInTheDocument()
  })
})

describe('Vendas → Vales-troca', () => {
  it('lista com saldos e o Dono cancela com motivo', async () => {
    let motivo: unknown = null
    api({
      'GET /api/vendas/vales?situacao=ativos': () => json({ itens: [vale], saldoAtivo: 54.51, saldoVencido: 10 }),
      'POST /api/vendas/vales/vt1/cancelar': (init) => {
        motivo = corpo(init).motivo
        return json({ ...vale, situacao: 'Cancelado' })
      },
    })
    renderApp('/vendas/vales-troca')
    const tabela = await screen.findByRole('table', { name: 'Vales-troca' })
    expect(within(tabela).getByText('VT7K2M9Q')).toBeInTheDocument()
    expect(screen.getByText('R$ 10,00', { selector: 'dd' })).toBeInTheDocument()

    await userEvent.click(within(tabela).getByRole('button', { name: 'Cancelar VT7K2M9Q' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Cancelar vale VT7K2M9Q' })
    await userEvent.type(within(dialogo).getByLabelText('Motivo'), 'Emitido por engano')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Cancelar vale' }))
    await waitFor(() => expect(motivo).toBe('Emitido por engano'))
  })
})

describe('Vendas → pagamento com vale-troca', () => {
  it('pede o código do vale, mostra o saldo e envia o código', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'GET /api/produtos/por-codigo/789': () =>
        json({ produto: { descricao: 'Vestido' }, sku: { id: 's9', tamanho: 'P', preco: 50, ativo: true }, cor: 'Preto' }),
      'GET /api/vendas/vales/por-codigo/VT7K2M9Q': () => json(vale),
      'POST /api/vendas': (init) => {
        enviado = corpo(init)
        return json({ id: 'v8', numero: 8, total: 50, troco: 0, avisos: [] }, 201)
      },
    })
    renderApp('/vendas/registro')
    const leitor = await screen.findByLabelText('Código de barras')
    await waitFor(() => expect(leitor).toBeEnabled())
    await userEvent.type(leitor, '789{Enter}')
    await userEvent.selectOptions(await screen.findByLabelText('Forma do pagamento 1'), 'f9')
    await userEvent.type(screen.getByLabelText('Código do vale do pagamento 1'), 'vt7k2m9q')
    await userEvent.tab()
    expect(await screen.findByText(/Saldo R\$ 54,51/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Registrar venda' }))
    await screen.findByText(/Venda nº 8 registrada/)
    expect(enviado).toMatchObject({ pagamentos: [{ formaId: 'f9', valor: 50, parcelas: 1, valeCodigo: 'VT7K2M9Q' }] })
  })
})

describe('Vendas → fechamento de comissões', () => {
  it('fecha a comissão pendente de um vendedor gerando conta a pagar e reabre', async () => {
    let fechado: Record<string, unknown> | null = null
    let reaberto = false
    let fechamentos = [] as unknown[]
    api({
      [`GET /api/vendas/comissoes?${periodo}`]: () => json([{ vendedorId: 'b1', vendedor: 'Bia', vendas: 2, total: 119.8, comissao: 5.99, comissaoPendente: 5.99 }]),
      'GET /api/vendas/comissoes/fechamentos': () => json(fechamentos),
      'POST /api/vendas/comissoes/fechamentos': (init) => {
        fechado = corpo(init)
        fechamentos = [{ id: 'fc1', vendedorId: 'b1', vendedorNome: 'Bia', de: inicioDoMes(hojeIso()), ate: hojeIso(), vendas: 2, trocas: 1, totalVendido: 119.8,
          comissao: 5.99, fechadoPor: 'Admin Demo', fechadoEm: '2026-10-20T12:00:00Z', contaAPagarGerada: true }]
        return json(fechamentos[0], 201)
      },
      'POST /api/vendas/comissoes/fechamentos/fc1/reabrir': () => {
        reaberto = true
        fechamentos = []
        return new Response(null, { status: 200 })
      },
    })
    renderApp('/vendas/comissoes')
    const tabela = await screen.findByRole('table', { name: 'Comissões' })
    await userEvent.click(within(tabela).getByRole('button', { name: 'Fechar comissão de Bia' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Fechar comissão de Bia' })
    expect(within(dialogo).getByText('R$ 5,99')).toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Fechar comissão' }))

    const lista = await screen.findByRole('table', { name: 'Fechamentos' })
    expect(within(lista).getByText('Conta a pagar gerada')).toBeInTheDocument()
    expect(fechado).toMatchObject({ vendedorId: 'b1', de: inicioDoMes(hojeIso()), ate: hojeIso(), gerarContaAPagar: true })

    await userEvent.click(within(lista).getByRole('button', { name: 'Reabrir' }))
    await waitFor(() => expect(reaberto).toBe(true))
  })
})

describe('Clientes → ficha com compras e vales', () => {
  it('mostra as compras e o saldo de vale-troca do cliente', async () => {
    const maria = {
      id: 'p1', tipo: 'Fisica', nome: 'Maria Silva', nomeFantasia: null, documento: null, ehCliente: true, ehFornecedor: false, telefone: null, whatsApp: null,
      email: null, dataNascimento: null, endereco: { cep: null, logradouro: null, numero: null, complemento: null, bairro: null, cidade: null, uf: null },
      preferencias: { blusa: null, calca: null, calcado: null, estilo: null }, observacoes: null, aceitaMarketing: false, consentimentoMarketingEm: null, ativo: true,
      criadoEm: '2026-10-01T12:00:00Z', anonimizadoEm: null,
    }
    api({
      'GET /api/pessoas?papel=cliente&situacao=ativos': () => json({ itens: [maria], total: 1, pagina: 1, tamanho: 50 }),
      'GET /api/pessoas/aniversariantes?periodo=semana': () => json([]),
      'GET /api/vendas?status=todas&clienteId=p1': () => json(listaDaVenda),
      'GET /api/vendas/vales?situacao=todos&clienteId=p1': () => json({ itens: [vale], saldoAtivo: 54.51, saldoVencido: 0 }),
      'GET /api/vendas/condicionais?situacao=abertas&clienteId=p1': () =>
        json({
          itens: [{ id: 'k4', numero: 4, data: '2026-10-01', prazo: '2026-10-03', clienteId: 'p1', clienteNome: 'Maria Silva', vendedorNome: 'Bia', pecas: 3, valor: 179.7, situacao: 'Atrasada', vendaNumero: null }],
          abertas: 1, atrasadas: 1, pecasNaRua: 3, valorNaRua: 179.7,
        }),
    })
    renderApp('/cadastros/clientes')
    await userEvent.click(await screen.findByRole('button', { name: 'Maria Silva' }))
    const compras = await screen.findByRole('region', { name: 'Compras e vale-troca' })
    expect(await within(compras).findByText('1 compra · R$ 109,02')).toBeInTheDocument()
    expect(within(compras).getByText(/VT7K2M9Q/)).toBeInTheDocument()
    expect(within(compras).getByText(/saldo R\$ 54,51/)).toBeInTheDocument()
    expect(await within(compras).findByText('Condicional nº 4 · 3 peças · R$ 179,70 · devolver até 03/10/2026 (atrasada)')).toBeInTheDocument()
  })
})
