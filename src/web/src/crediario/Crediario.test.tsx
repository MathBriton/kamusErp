import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const carteira = {
  aVencer: 80, ate30: 59.9, ate60: 0, ate90: 0, acima90: 0, total: 139.9,
  clientes: [
    { clienteId: 'p1', clienteNome: 'Maria Souza', limite: 500, emAberto: 139.9, atrasado: 59.9, diasDeAtraso: 30, disponivel: 360.1, bloqueado: true },
    { clienteId: 'p2', clienteNome: 'João', limite: 200, emAberto: 0, atrasado: 0, diasDeAtraso: 0, disponivel: 200, bloqueado: false },
  ],
  registros: 2, pagina: 1, tamanho: 20,
}
const cliente = (extra = {}) => ({
  clienteId: 'p1', clienteNome: 'Maria Souza', limite: 500, emAberto: 139.9, disponivel: 360.1, diasDeAtraso: 30, bloqueado: true,
  motivo: 'Parcela atrasada há 30 dias (bloqueia com 15).', bloqueadoManualmente: false, observacao: null,
  parcelas: [
    { parcelaId: 'pa1', tituloId: 't1', descricao: 'Venda nº 1 — Crediário 2x', numero: 1, parcelas: 2, vencimento: '2026-09-18', valor: 59.9, saldo: 59.9,
      situacao: 'Vencida', diasDeAtraso: 30, multa: 1.2, juros: 0.6 },
    { parcelaId: 'pa2', tituloId: 't1', descricao: 'Venda nº 1 — Crediário 2x', numero: 2, parcelas: 2, vencimento: '2026-10-18', valor: 80, saldo: 80,
      situacao: 'Aberta', diasDeAtraso: 0, multa: 0, juros: 0 },
  ],
  ...extra,
})

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/financeiro/crediario/carteira?pagina=1&tamanho=20': () => json(carteira),
    'GET /api/financeiro/crediario/clientes/p1': () => json(cliente()),
    'GET /api/financeiro/crediario/contas': () => json([{ id: 'c1', nome: 'Caixa da loja' }]),
    'GET /api/financeiro/crediario/configuracao': () => json({ multaPercentual: 2, jurosMensalPercentual: 1, diasParaBloquear: 15 }),
    ...extras,
  })
}

beforeEach(() => {
  URL.createObjectURL = vi.fn(() => 'blob:x') as unknown as typeof URL.createObjectURL
  URL.revokeObjectURL = vi.fn()
})

describe('Crediário → carteira', () => {
  it('mostra as faixas de atraso e os clientes, bloqueados em destaque', async () => {
    api()
    renderApp('/vendas/crediario')
    const tabela = await screen.findByRole('table', { name: 'Carteira do crediário' })
    expect(await within(tabela).findByText('Maria Souza')).toBeInTheDocument()
    expect(within(tabela).getByText('Bloqueado')).toBeInTheDocument()
    expect(screen.getByText('R$ 139,90', { selector: 'dd' })).toBeInTheDocument()
    expect(screen.getByText('1 a 30 dias')).toBeInTheDocument()
    expect(screen.getByText('2 registros')).toBeInTheDocument()
  })

  it('busca na grade, abre a ficha pela linha e exporta a inadimplência dos selecionados', async () => {
    const fetch = api({
      'GET /api/financeiro/crediario/carteira?busca=jo&pagina=1&tamanho=20': () => json({ ...carteira, clientes: [carteira.clientes[1]], registros: 1 }),
      'GET /api/relatorios/inadimplencia.csv?ids=p1': () => new Response('csv', { headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=i.csv' } }),
    })
    renderApp('/vendas/crediario')
    const tabela = await screen.findByRole('table', { name: 'Carteira do crediário' })
    await within(tabela).findByText('Maria Souza')

    await userEvent.click(within(tabela).getByRole('button', { name: 'Ver crediário de Maria Souza' }))
    expect(await screen.findByRole('dialog', { name: 'Crediário de Maria Souza' })).toBeInTheDocument()
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Crediário de Maria Souza' })).getByRole('button', { name: 'Fechar' }))

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Maria Souza' }))
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/inadimplencia.csv?ids=p1')).toBe(true))

    await userEvent.type(screen.getByLabelText('Buscar cliente'), 'jo')
    await waitFor(() => expect(within(tabela).queryByText('Maria Souza')).not.toBeInTheDocument())
    expect(screen.getByText('1 registro')).toBeInTheDocument()
  })

  it('recebe a parcela atrasada com multa e juros sugeridos e oferece o recibo', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/financeiro/crediario/parcelas/pa1/receber': (init) => {
        enviado = corpo(init)
        return json({ baixaId: 'b1', valor: 59.9, multa: 1.2, juros: 0.6, desconto: 0, total: 61.7 })
      },
      'GET /api/financeiro/crediario/baixas/b1/recibo.pdf': () => new Response('%PDF-1.7', { headers: { 'Content-Type': 'application/pdf' } }),
    }, sessaoVendedor)
    renderApp('/vendas/crediario')
    await userEvent.click(await screen.findByRole('button', { name: 'Maria Souza' }))
    const ficha = await screen.findByRole('dialog', { name: 'Crediário de Maria Souza' })
    expect(within(ficha).getByText('Parcela atrasada há 30 dias (bloqueia com 15).')).toBeInTheDocument()
    expect(within(ficha).queryByRole('button', { name: 'Alterar limite' })).not.toBeInTheDocument()

    await userEvent.click(within(ficha).getByRole('button', { name: 'Receber parcela 1' }))
    const receber = await screen.findByRole('dialog', { name: 'Receber parcela 1 de 2' })
    expect(within(receber).getByLabelText('Multa (R$)')).toHaveValue('1,20')
    expect(within(receber).getByLabelText('Juros (R$)')).toHaveValue('0,60')
    expect(within(receber).getByText('Total: R$ 61,70')).toBeInTheDocument()
    await userEvent.click(within(receber).getByRole('button', { name: 'Confirmar recebimento' }))

    expect(await within(receber).findByText('Recebido: R$ 61,70')).toBeInTheDocument()
    expect(enviado).toEqual({ contaId: 'c1', valor: 59.9, multa: 1.2, juros: 0.6, desconto: 0 })
    await userEvent.click(within(receber).getByRole('button', { name: 'Recibo (PDF)' }))
    await waitFor(() => expect(URL.createObjectURL).toHaveBeenCalled())
  })

  it('Dono altera o limite e bloqueia com observação', async () => {
    let limite: Record<string, unknown> | null = null
    api({
      'PUT /api/financeiro/crediario/clientes/p1/limite': (init) => {
        limite = corpo(init)
        return new Response(null, { status: 200 })
      },
    })
    renderApp('/vendas/crediario')
    await userEvent.click(await screen.findByRole('button', { name: 'Maria Souza' }))
    const ficha = await screen.findByRole('dialog', { name: 'Crediário de Maria Souza' })
    await userEvent.click(within(ficha).getByRole('button', { name: 'Alterar limite' }))
    const campo = within(ficha).getByLabelText('Limite (R$)')
    await userEvent.clear(campo)
    await userEvent.type(campo, '800')
    await userEvent.click(within(ficha).getByRole('checkbox', { name: /Bloquear/ }))
    await userEvent.type(within(ficha).getByLabelText('Observação'), 'Cheque devolvido')
    await userEvent.click(within(ficha).getByRole('button', { name: 'Salvar limite' }))
    await waitFor(() => expect(limite).toEqual({ clienteNome: 'Maria Souza', limite: 800, bloqueado: true, observacao: 'Cheque devolvido' }))
  })

  it('Dono altera as regras; erro da API aparece', async () => {
    api({ 'PUT /api/financeiro/crediario/configuracao': () => problema(400, 'A multa por atraso vai de 0% a 2% (limite do CDC).') })
    renderApp('/vendas/crediario')
    const multa = await screen.findByLabelText('Multa por atraso (%)')
    await userEvent.clear(multa)
    await userEvent.type(multa, '3')
    await userEvent.click(screen.getByRole('button', { name: 'Salvar regras' }))
    expect(await screen.findByText(/limite do CDC/)).toBeInTheDocument()
  })
})

describe('Registro de venda no crediário', () => {
  it('mostra o limite disponível do cliente escolhido', async () => {
    api({
      'GET /api/estoque/lojas': () => json([{ id: 'l1', empresaId: 'e1', nome: 'Loja Centro', ativa: true, quantidade: 3 }]),
      'GET /api/vendedores': () => json([]),
      'GET /api/vendas/formas': () =>
        json([{ id: 'f7', nome: 'Crediário', tipo: 'Crediario', contaId: null, taxaAVista: 0, taxaParcelado: 0, maxParcelas: 10, prazoDias: 30, ativa: true }]),
      'GET /api/produtos/por-codigo/789': () => json({ produto: { descricao: 'Vestido' }, sku: { id: 's9', tamanho: 'P', preco: 89.9, ativo: true }, cor: 'Preto' }),
      'GET /api/pessoas?papel=cliente&situacao=ativos&busca=ma': () => json({ itens: [{ id: 'p2', nome: 'Maria Souza' }], total: 1 }),
      'GET /api/financeiro/crediario/clientes/p2': () => json(cliente({ clienteId: 'p2', bloqueado: false, motivo: null, disponivel: 360.1 })),
    })
    renderApp('/vendas/registro')
    const leitor = await screen.findByLabelText('Código de barras')
    await waitFor(() => expect(leitor).toBeEnabled())
    await userEvent.type(leitor, '789{Enter}')
    expect(await screen.findByText(/Escolha um cliente cadastrado/)).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Cliente (opcional)'), 'ma')
    await userEvent.click(await screen.findByRole('button', { name: 'Maria Souza' }))
    expect(await screen.findByText('Crediário disponível: R$ 360,10')).toBeInTheDocument()
  })
})
