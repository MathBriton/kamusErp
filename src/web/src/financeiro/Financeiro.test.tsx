import { screen, waitFor, within } from '@testing-library/react'
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
const lista = {
  itens: [linha(), linha({ parcelaId: 'p2', numero: 2, vencimento: '2026-09-20', situacao: 'Vencida', valor: 100, saldo: 100 })],
  valor: 300, pago: 0, saldo: 300, vencido: 100, total: 2, pagina: 1, tamanho: 20,
}
const titulo = {
  id: 't1', tipo: 'Pagar', descricao: 'Reforma da vitrine', pessoaId: null, pessoaNome: 'Marcenaria Silva', categoriaId: 'k1', categoria: 'Aluguel', documento: 'Orçamento 7',
  competencia: '2026-10-15', observacao: null, valorTotal: 300, cancelado: false, origem: 'Manual',
  parcelas: [
    { id: 'p1', numero: 1, vencimento: '2026-10-20', valor: 200, valorPago: 80, saldo: 120, situacao: 'Parcial',
      baixas: [{ id: 'b1', data: '2026-10-15', contaId: 'c1', conta: 'Caixa da loja', valor: 80, juros: 5, multa: 0, desconto: 0, total: 85, usuario: 'Admin Demo', estornada: false, estornadaPor: null }] },
    { id: 'p2', numero: 2, vencimento: '2026-11-20', valor: 100, valorPago: 0, saldo: 100, situacao: 'Aberta', baixas: [] },
  ],
}
const URL_PAGAR = '/api/financeiro/titulos?tipo=pagar&situacao=abertas&pagina=1&tamanho=20'
const URL_CONTAS = '/api/financeiro/contas?situacao=ativas&pagina=1&tamanho=20'
const URL_CATEGORIAS = '/api/financeiro/categorias?situacao=ativas&pagina=1&tamanho=20'
const csv = () => new Response('csv', { headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=x.csv' } })

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/financeiro/contas': () => json(contas),
    [`GET ${URL_CONTAS}`]: () => json({ itens: contas, total: 2, pagina: 1, tamanho: 20, saldo: 1415 }),
    'GET /api/financeiro/categorias': () => json(categorias),
    [`GET ${URL_CATEGORIAS}`]: () => json({ itens: categorias, total: 2, pagina: 1, tamanho: 20 }),
    [`GET ${URL_PAGAR}`]: () => json(lista),
    'GET /api/financeiro/titulos/t1': () => json(titulo),
    ...extras,
  })
}

function prepararDownload() {
  URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
  URL.revokeObjectURL = () => {}
}

describe('Financeiro → Contas a pagar', () => {
  it('grade das parcelas com situação, totais sobre todos os filtrados e total de registros', async () => {
    api({ [`GET ${URL_PAGAR}`]: () => json({ ...lista, total: 45 }) })
    renderApp('/financeiro/contas-a-pagar')

    const tabela = await screen.findByRole('table', { name: 'Contas a pagar' })
    expect(await within(tabela).findAllByText('Reforma da vitrine')).toHaveLength(2)
    expect(within(tabela).getByText('Vencida')).toBeInTheDocument()
    expect(screen.getByText('R$ 300,00', { selector: 'dd' })).toBeInTheDocument()
    expect(screen.getByText('R$ 100,00', { selector: 'dd' })).toHaveClass('text-danger')
    expect(screen.getByText('45 registros')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 3')).toBeInTheDocument()
  })

  it('filtro de situação volta à página 1 e "Limpar filtros" restaura o padrão', async () => {
    const pagina = (n: number) => json({ ...lista, itens: [linha({ parcelaId: `x${n}`, descricao: `Conta ${n}` })], total: 45, pagina: n })
    api({
      [`GET ${URL_PAGAR}`]: () => pagina(1),
      'GET /api/financeiro/titulos?tipo=pagar&situacao=abertas&pagina=2&tamanho=20': () => pagina(2),
      'GET /api/financeiro/titulos?tipo=pagar&situacao=vencidas&pagina=1&tamanho=20': () => pagina(1),
    })
    renderApp('/financeiro/contas-a-pagar')
    await screen.findByText('Conta 1')

    await userEvent.click(screen.getByRole('button', { name: 'Próxima página' }))
    expect(await screen.findByText('Conta 2')).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'vencidas')
    expect(await screen.findByText('Conta 1')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 3')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }))
    expect(screen.getByLabelText('Situação')).toHaveValue('abertas')
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

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
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

    await waitFor(() => expect(enviado).toMatchObject({ repetirMeses: 12, parcelas: [{ valor: 2500 }] }))
  })

  it('ação da linha: paga parcialmente com juros e mostra o total', async () => {
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

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(enviado).toMatchObject({ contaId: 'c1', valor: 80, juros: 5, multa: 0, desconto: 0, data: hojeIso() })
  })

  it('ação da linha: ver detalhe com baixas; estornar mostra o erro da API', async () => {
    api({ 'POST /api/financeiro/baixas/b1/estornar': () => problema(409, 'Esta baixa já foi estornada.') })
    renderApp('/financeiro/contas-a-pagar')

    await userEvent.click(await screen.findByRole('button', { name: 'Ver Reforma da vitrine 1/2' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Reforma da vitrine' })
    expect(within(dialogo).getByText(/Caixa da loja · R\$ 85,00/)).toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Estornar' }))

    expect(await within(dialogo).findByRole('alert')).toHaveTextContent('já foi estornada')
  })

  it('ação da linha: cancelar título pede confirmação', async () => {
    let cancelado = false
    api({
      'POST /api/financeiro/titulos/t1/cancelar': () => {
        cancelado = true
        return json({ ...titulo, cancelado: true })
      },
    })
    renderApp('/financeiro/contas-a-pagar')

    await userEvent.click(await screen.findByRole('button', { name: 'Cancelar título Reforma da vitrine 1/2' }))
    const confirmar = screen.getByRole('dialog', { name: 'Cancelar título' })
    expect(confirmar).toHaveTextContent('Reforma da vitrine')
    await userEvent.click(within(confirmar).getByRole('button', { name: 'Cancelar título' }))
    await waitFor(() => expect(cancelado).toBe(true))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('seleção: exporta só as parcelas selecionadas', async () => {
    const fetch = api({ 'GET /api/relatorios/titulos.csv?ids=p2': csv })
    prepararDownload()
    renderApp('/financeiro/contas-a-pagar')
    await screen.findAllByText('Reforma da vitrine')

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Reforma da vitrine 2/2' }))
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    expect(screen.getByRole('menu', { name: 'Exportar' })).toHaveTextContent('Exportar os 1 selecionados')
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/titulos.csv?ids=p2')).toBe(true))
  })

  it('seleção: paga as selecionadas de uma vez numa conta, com contagem, total e o que não deu', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/financeiro/parcelas/baixas-em-lote': (init) => {
        enviado = corpo(init)
        return json({ baixadas: 1, total: 200, falhas: [{ parcelaId: 'p2', descricao: 'Reforma da vitrine (2/2)', motivo: 'Título cancelado não recebe baixa.' }] })
      },
    })
    renderApp('/financeiro/contas-a-pagar')
    await screen.findAllByText('Reforma da vitrine')

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar todos da página' }))
    await userEvent.click(within(screen.getByRole('region', { name: 'Seleção' })).getByRole('button', { name: 'Pagar selecionadas' }))
    const dialogo = screen.getByRole('dialog', { name: 'Pagar selecionadas' })
    expect(dialogo).toHaveTextContent('2 parcelas')
    expect(dialogo).toHaveTextContent('R$ 300,00')
    await userEvent.selectOptions(within(dialogo).getByLabelText('Conta'), 'c2')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Pagar 2 parcelas' }))

    expect(await within(dialogo).findByRole('status')).toHaveTextContent('1 parcela paga: R$ 200,00')
    expect(within(dialogo).getByRole('alert')).toHaveTextContent('Reforma da vitrine (2/2): Título cancelado não recebe baixa.')
    expect(enviado).toEqual({ parcelaIds: ['p1', 'p2'], contaId: 'c2', data: hojeIso() })
    expect(screen.queryByRole('region', { name: 'Seleção' })).not.toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Concluir' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('vendedor não acessa', async () => {
    api({}, sessaoVendedor)
    renderApp('/financeiro/contas-a-pagar')

    expect(await screen.findByText(/Só Dono, Gerente e Financeiro/)).toBeInTheDocument()
  })
})

describe('Financeiro → Contas financeiras', () => {
  it('grade com saldo por conta, saldo total e transferência', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/financeiro/transferencias': (init) => {
        enviado = corpo(init)
        return new Response(null, { status: 200 })
      },
    })
    renderApp('/financeiro/contas')

    expect(await screen.findByText('R$ 1.415,00', { selector: 'dd' })).toBeInTheDocument()
    const tabela = screen.getByRole('table', { name: 'Contas financeiras' })
    expect(within(tabela).getByRole('row', { name: /Banco/ })).toHaveTextContent('R$ 1.000,00')
    expect(screen.getByText('2 registros')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Transferir entre contas' }))
    const dialogo = screen.getByRole('dialog')
    await userEvent.selectOptions(within(dialogo).getByLabelText('De'), 'c1')
    await userEvent.selectOptions(within(dialogo).getByLabelText('Para'), 'c2')
    await userEvent.type(within(dialogo).getByLabelText('Valor (R$)'), '300')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Transferir' }))

    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument()) // 200 sem corpo fecha o diálogo
    expect(enviado).toMatchObject({ origemId: 'c1', destinoId: 'c2', valor: 300 })
  })

  it('ação da linha: extrato com saldo linha a linha', async () => {
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

  it('ação da linha: editar e desativar com confirmação (mostra o erro da API)', async () => {
    let editado: Record<string, unknown> | null = null
    api({
      'PUT /api/financeiro/contas/c1': (init) => {
        editado = corpo(init)
        return json({ ...contas[0], nome: 'Caixa principal' })
      },
      'POST /api/financeiro/contas/c2/desativar': () => problema(409, 'Banco ainda tem saldo de R$ 1.000,00. Transfira antes de desativar.'),
    })
    renderApp('/financeiro/contas')
    await screen.findByRole('button', { name: 'Extrato de Caixa da loja' })

    await userEvent.click(screen.getByRole('button', { name: 'Editar Caixa da loja' }))
    const edicao = screen.getByRole('dialog', { name: 'Editar Caixa da loja' })
    await userEvent.clear(within(edicao).getByLabelText('Nome'))
    await userEvent.type(within(edicao).getByLabelText('Nome'), 'Caixa principal')
    await userEvent.click(within(edicao).getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(editado).toEqual({ nome: 'Caixa principal', tipo: 'Caixa', saldoInicial: 500, saldoInicialEm: '2026-10-01' }))

    await userEvent.click(await screen.findByRole('button', { name: 'Desativar Banco' }))
    const confirmar = screen.getByRole('dialog', { name: 'Desativar conta' })
    await userEvent.click(within(confirmar).getByRole('button', { name: 'Desativar' }))
    expect(await within(confirmar).findByRole('alert')).toHaveTextContent('Transfira antes de desativar')
  })

  it('seleção: desativa em lote e exporta só as selecionadas', async () => {
    let lote: Record<string, unknown> | null = null
    const fetch = api({
      'POST /api/financeiro/contas/situacao': (init) => {
        lote = corpo(init)
        return json({ alterados: 1 })
      },
      'GET /api/relatorios/contas-financeiras.csv?ids=c1': csv,
    })
    prepararDownload()
    renderApp('/financeiro/contas')
    await screen.findByRole('button', { name: 'Extrato de Caixa da loja' })

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Caixa da loja' }))
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/contas-financeiras.csv?ids=c1')).toBe(true))

    await userEvent.click(within(screen.getByRole('region', { name: 'Seleção' })).getByRole('button', { name: 'Desativar selecionadas' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Desativar contas' })).getByRole('button', { name: 'Desativar' }))
    await waitFor(() => expect(lote).toEqual({ ids: ['c1'], ativo: false }))
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Seleção' })).not.toBeInTheDocument())
  })
})

describe('Financeiro → Fluxo de caixa', () => {
  it('mostra só os dias com movimento e o atraso à parte', async () => {
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
    expect(screen.getByRole('search', { name: 'Filtros' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Limpar filtros' })).not.toBeInTheDocument()
  })
})

describe('Financeiro → Categorias', () => {
  it('grade com tipo, grupo e situação; filtro por tipo', async () => {
    api({
      'GET /api/financeiro/categorias?situacao=ativas&tipo=Despesa&pagina=1&tamanho=20': () => json({ itens: [categorias[0]], total: 1, pagina: 1, tamanho: 20 }),
    })
    renderApp('/financeiro/categorias')

    const tabela = await screen.findByRole('table', { name: 'Categorias' })
    const aluguel = await within(tabela).findByRole('row', { name: /Aluguel/ })
    expect(aluguel).toHaveTextContent('Despesa')
    expect(aluguel).toHaveTextContent('Ocupação')
    expect(aluguel).toHaveTextContent('Ativa')
    expect(screen.getByText('2 registros')).toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText('Tipo'), 'Despesa')
    expect(await screen.findByText('1 registro')).toBeInTheDocument()
    expect(within(screen.getByRole('table', { name: 'Categorias' })).queryByText('Vendas')).not.toBeInTheDocument()
  })

  it('nova categoria', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/financeiro/categorias': (init) => {
        enviado = corpo(init)
        return json({ id: 'k3', nome: 'Manutenção', grupo: 'Ocupação', tipo: 'Despesa', ativa: true }, 201)
      },
    })
    renderApp('/financeiro/categorias')
    await screen.findByText('Aluguel')

    await userEvent.click(screen.getByRole('button', { name: 'Nova categoria' }))
    const dialogo = screen.getByRole('dialog')
    await userEvent.type(within(dialogo).getByLabelText('Grupo'), 'Ocupação')
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Manutenção')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    await vi.waitFor(() => expect(enviado).toEqual({ nome: 'Manutenção', grupo: 'Ocupação', tipo: 'Despesa' }))
  })

  it('ações da linha: editar e desativar com confirmação', async () => {
    let editado: Record<string, unknown> | null = null
    let desativada = false
    api({
      'PUT /api/financeiro/categorias/k1': (init) => {
        editado = corpo(init)
        return json({ ...categorias[0], nome: 'Aluguel da loja' })
      },
      'POST /api/financeiro/categorias/k1/desativar': () => {
        desativada = true
        return json({ ...categorias[0], ativa: false })
      },
    })
    renderApp('/financeiro/categorias')
    await screen.findByText('Aluguel')

    await userEvent.click(screen.getByRole('button', { name: 'Editar Aluguel' }))
    const edicao = screen.getByRole('dialog', { name: 'Editar categoria' })
    await userEvent.clear(within(edicao).getByLabelText('Nome'))
    await userEvent.type(within(edicao).getByLabelText('Nome'), 'Aluguel da loja')
    await userEvent.click(within(edicao).getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(editado).toEqual({ nome: 'Aluguel da loja', grupo: 'Ocupação', tipo: 'Despesa' }))

    await userEvent.click(await screen.findByRole('button', { name: 'Desativar Aluguel' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Desativar categoria' })).getByRole('button', { name: 'Desativar' }))
    await waitFor(() => expect(desativada).toBe(true))
  })

  it('seleção: desativa em lote e exporta só as selecionadas', async () => {
    let lote: Record<string, unknown> | null = null
    const fetch = api({
      'POST /api/financeiro/categorias/situacao': (init) => {
        lote = corpo(init)
        return json({ alterados: 2 })
      },
      'GET /api/relatorios/categorias-financeiras.csv?ids=k1%2Ck2': csv,
    })
    prepararDownload()
    renderApp('/financeiro/categorias')
    await screen.findByText('Aluguel')

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar todos da página' }))
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/categorias-financeiras.csv?ids=k1%2Ck2')).toBe(true))

    await userEvent.click(within(screen.getByRole('region', { name: 'Seleção' })).getByRole('button', { name: 'Desativar selecionadas' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Desativar categorias' })).getByRole('button', { name: 'Desativar' }))
    await waitFor(() => expect(lote).toEqual({ ids: ['k1', 'k2'], ativo: false }))
  })
})
