import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'
import { hojeIso, somarDias, somarMeses } from './tipos'

const contas = [
  { id: 'c1', empresaId: 'e1', nome: 'Caixa da loja', tipo: 'Caixa', saldoInicial: 500, saldoInicialEm: '2026-10-01', saldo: 415, ativa: true },
  { id: 'c2', empresaId: 'e1', nome: 'Banco', tipo: 'Banco', saldoInicial: 0, saldoInicialEm: '2026-10-01', saldo: 1000, ativa: true },
]
const categorias = [
  { id: 'k1', nome: 'Aluguel', grupo: 'Ocupação', tipo: 'Despesa', ativa: true },
  { id: 'k2', nome: 'Vendas', grupo: 'Vendas', tipo: 'Receita', ativa: true },
]
const linha = (extra = {}) => ({
  tituloId: 't1', parcelaId: 'p1', numero: 1, parcelas: 2, vencimento: '2026-10-20', descricao: 'Reforma da vitrine', pessoaNome: 'Marcenaria Silva',
  documento: 'Orçamento 7', categoria: 'Aluguel', valor: 200, valorPago: 0, saldo: 200, situacao: 'Aberta', origem: 'Manual', ...extra,
})
const lista = { itens: [linha(), linha({ parcelaId: 'p2', numero: 2, vencimento: '2026-09-20', situacao: 'Vencida', valor: 100, saldo: 100 })], valor: 300, pago: 0, saldo: 300, vencido: 100 }
const titulo = {
  id: 't1', tipo: 'Pagar', descricao: 'Reforma da vitrine', pessoaId: null, pessoaNome: 'Marcenaria Silva', categoriaId: 'k1', categoria: 'Aluguel', documento: 'Orçamento 7',
  competencia: '2026-10-15', observacao: null, valorTotal: 300, cancelado: false, origem: 'Manual',
  parcelas: [
    { id: 'p1', numero: 1, vencimento: '2026-10-20', valor: 200, valorPago: 80, saldo: 120, situacao: 'Parcial',
      baixas: [{ id: 'b1', data: '2026-10-15', contaId: 'c1', conta: 'Caixa da loja', valor: 80, juros: 5, multa: 0, desconto: 0, total: 85, usuario: 'Admin Demo', estornada: false, estornadaPor: null }] },
    { id: 'p2', numero: 2, vencimento: '2026-11-20', valor: 100, valorPago: 0, saldo: 100, situacao: 'Aberta', baixas: [] },
  ],
}

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/financeiro/contas': () => json(contas),
    'GET /api/financeiro/categorias': () => json(categorias),
    'GET /api/financeiro/titulos?tipo=pagar&situacao=abertas': () => json(lista),
    'GET /api/financeiro/titulos/t1': () => json(titulo),
    ...extras,
  })
}

describe('Financeiro → Contas a pagar', () => {
  it('lista as parcelas com situação e totais', async () => {
    api()
    renderApp('/financeiro/contas-a-pagar')

    const tabela = await screen.findByRole('table', { name: 'Contas a pagar' })
    expect(within(tabela).getAllByText('Reforma da vitrine')).toHaveLength(2)
    expect(within(tabela).getByText('Vencida')).toBeInTheDocument()
    expect(screen.getByText('R$ 300,00', { selector: 'dd' })).toBeInTheDocument()
    expect(screen.getByText('R$ 100,00', { selector: 'dd' })).toHaveClass('text-danger')
  })

  it('nova conta em 3 parcelas mensais com fornecedor buscado no cadastro', async () => {
    let enviado: Record<string, unknown> | null = null
    const hoje = hojeIso()
    api({
      'GET /api/pessoas?papel=fornecedor&situacao=ativos&busca=au': () => json({ itens: [{ id: 'f1', nome: 'Aurora Moda' }], total: 1 }),
      'POST /api/financeiro/titulos': (init) => {
        enviado = corpo(init)
        return json(titulo, 201)
      },
    })
    renderApp('/financeiro/contas-a-pagar')
    await userEvent.click(await screen.findByRole('button', { name: 'Nova conta a pagar' }))
    const dialogo = screen.getByRole('dialog', { name: 'Nova conta a pagar' })

    await userEvent.type(within(dialogo).getByLabelText('Descrição'), 'Coleção de verão')
    await userEvent.type(within(dialogo).getByLabelText('Fornecedor (opcional)'), 'au')
    await userEvent.click(await within(dialogo).findByRole('button', { name: 'Aurora Moda' }))
    await userEvent.selectOptions(within(dialogo).getByLabelText('Categoria'), 'k1')
    await userEvent.type(within(dialogo).getByLabelText('Valor total (R$)'), '100,00')
    await userEvent.clear(within(dialogo).getByLabelText('Parcelas'))
    await userEvent.type(within(dialogo).getByLabelText('Parcelas'), '3')
    expect(within(dialogo).getByText(/1ª .*R\$ 33,34/)).toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    await screen.findByRole('table', { name: 'Contas a pagar' })
    expect(enviado).toMatchObject({
      tipo: 'pagar', descricao: 'Coleção de verão', pessoaId: 'f1', pessoaNome: 'Aurora Moda', categoriaId: 'k1', repetirMeses: 1,
      parcelas: [{ vencimento: hoje, valor: 33.34 }, { vencimento: somarMeses(hoje, 1), valor: 33.33 }, { vencimento: somarMeses(hoje, 2), valor: 33.33 }],
    })
  })

  it('despesa fixa: repete por 12 meses', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/financeiro/titulos': (init) => {
        enviado = corpo(init)
        return json(titulo, 201)
      },
    })
    renderApp('/financeiro/contas-a-pagar')
    await userEvent.click(await screen.findByRole('button', { name: 'Nova conta a pagar' }))
    const dialogo = screen.getByRole('dialog')
    await userEvent.type(within(dialogo).getByLabelText('Descrição'), 'Aluguel')
    await userEvent.type(within(dialogo).getByLabelText('Valor total (R$)'), '2500')
    await userEvent.selectOptions(within(dialogo).getByLabelText('Repetir todo mês (despesa/receita fixa)'), '12')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    await screen.findByRole('table', { name: 'Contas a pagar' })
    expect(enviado).toMatchObject({ repetirMeses: 12, parcelas: [{ valor: 2500 }] })
  })

  it('paga parcialmente com juros e mostra o total', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/financeiro/parcelas/p1/baixas': (init) => {
        enviado = corpo(init)
        return json(titulo)
      },
    })
    renderApp('/financeiro/contas-a-pagar')
    await userEvent.click(await screen.findByRole('button', { name: 'Pagar Reforma da vitrine 1/2' }))
    const dialogo = screen.getByRole('dialog', { name: 'Pagar: Reforma da vitrine' })

    await userEvent.selectOptions(within(dialogo).getByLabelText('Conta'), 'c1')
    await userEvent.clear(within(dialogo).getByLabelText('Valor da parcela (R$)'))
    await userEvent.type(within(dialogo).getByLabelText('Valor da parcela (R$)'), '80')
    await userEvent.type(within(dialogo).getByLabelText('Juros (R$)'), '5')
    expect(within(dialogo).getByText('R$ 85,00')).toBeInTheDocument()
    expect(within(dialogo).getByText(/Baixa parcial/)).toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Confirmar' }))

    await screen.findByRole('table', { name: 'Contas a pagar' })
    expect(enviado).toMatchObject({ contaId: 'c1', valor: 80, juros: 5, multa: 0, desconto: 0, data: hojeIso() })
  })

  it('detalhe com baixas: estornar mostra o erro da API se houver', async () => {
    api({ 'POST /api/financeiro/baixas/b1/estornar': () => problema(409, 'Esta baixa já foi estornada.') })
    renderApp('/financeiro/contas-a-pagar')

    await userEvent.click((await screen.findAllByRole('button', { name: 'Reforma da vitrine' }))[0]!)
    const dialogo = await screen.findByRole('dialog', { name: 'Reforma da vitrine' })
    expect(within(dialogo).getByText(/Caixa da loja · R\$ 85,00/)).toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Estornar' }))

    expect(await within(dialogo).findByRole('alert')).toHaveTextContent('já foi estornada')
  })

  it('vendedor não acessa', async () => {
    api({}, sessaoVendedor)
    renderApp('/financeiro/contas-a-pagar')

    expect(await screen.findByText(/Só Dono, Gerente e Financeiro/)).toBeInTheDocument()
  })
})

describe('Financeiro → Contas financeiras, fluxo e categorias', () => {
  it('saldo por conta, total e transferência', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/financeiro/transferencias': (init) => {
        enviado = corpo(init)
        return new Response(null, { status: 200 })
      },
    })
    renderApp('/financeiro/contas')

    expect(await screen.findByText('R$ 1.415,00')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Transferir entre contas' }))
    const dialogo = screen.getByRole('dialog')
    await userEvent.selectOptions(within(dialogo).getByLabelText('De'), 'c1')
    await userEvent.selectOptions(within(dialogo).getByLabelText('Para'), 'c2')
    await userEvent.type(within(dialogo).getByLabelText('Valor (R$)'), '300')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Transferir' }))

    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument()) // 200 sem corpo fecha o diálogo
    expect(enviado).toMatchObject({ origemId: 'c1', destinoId: 'c2', valor: 300 })
  })

  it('extrato com saldo linha a linha', async () => {
    const hoje = hojeIso()
    api({
      [`GET /api/financeiro/contas/c1/extrato?de=${somarDias(hoje, -30)}&ate=${hoje}`]: () =>
        json({ conta: 'Caixa da loja', de: '', ate: '', saldoAnterior: 500, saldoFinal: 415, movimentos: [{ id: 'm1', data: '2026-10-15', descricao: 'Reforma da vitrine (1/2)', valor: -85, saldo: 415, usuario: 'Admin Demo' }] }),
    })
    renderApp('/financeiro/contas')

    await userEvent.click(await screen.findByRole('button', { name: 'Extrato de Caixa da loja' }))
    const extrato = await screen.findByRole('table', { name: 'Extrato' })
    expect(within(extrato).getByText('Reforma da vitrine (1/2)')).toBeInTheDocument()
    expect(within(extrato).getByText('-R$ 85,00')).toHaveClass('text-danger')
  })

  it('fluxo de caixa mostra só os dias com movimento e o atraso à parte', async () => {
    const hoje = hojeIso()
    api({
      [`GET /api/financeiro/fluxo?de=${hoje}&ate=${somarDias(hoje, 30)}`]: () =>
        json({
          agrupamento: 'dia', saldoInicial: 1000, aReceberEmAtraso: 0, aPagarEmAtraso: 70, saldoFinal: 1150,
          periodos: [
            { data: hoje, entradasRealizadas: 0, saidasRealizadas: 200, entradasPrevistas: 0, saidasPrevistas: 0, saldo: 800 },
            { data: somarDias(hoje, 1), entradasRealizadas: 0, saidasRealizadas: 0, entradasPrevistas: 0, saidasPrevistas: 0, saldo: 800 },
            { data: somarDias(hoje, 2), entradasRealizadas: 0, saidasRealizadas: 0, entradasPrevistas: 500, saidasPrevistas: 150, saldo: 1150 },
          ],
        }),
    })
    renderApp('/financeiro/fluxo-de-caixa')

    const tabela = await screen.findByRole('table', { name: 'Fluxo de caixa' })
    expect(within(tabela).getAllByRole('row')).toHaveLength(3) // cabeçalho + 2 dias com movimento
    expect(within(tabela).getByText('previsto')).toBeInTheDocument()
    expect(screen.getByText('R$ 70,00', { selector: 'dd' })).toHaveClass('text-danger')
  })

  it('categorias agrupadas por receita e despesa; nova categoria', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/financeiro/categorias': (init) => {
        enviado = corpo(init)
        return json({ id: 'k3', nome: 'Manutenção', grupo: 'Ocupação', tipo: 'Despesa', ativa: true }, 201)
      },
    })
    renderApp('/financeiro/categorias')

    expect(within(await screen.findByRole('region', { name: 'Despesas' })).getByText('Aluguel')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Nova categoria' }))
    const dialogo = screen.getByRole('dialog')
    await userEvent.type(within(dialogo).getByLabelText('Grupo'), 'Ocupação')
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Manutenção')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    await vi.waitFor(() => expect(enviado).toEqual({ nome: 'Manutenção', grupo: 'Ocupação', tipo: 'Despesa' }))
  })
})

