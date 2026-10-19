import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { hojeIso } from '../financeiro/tipos'
import { empresaAtivaHandler, json, mockApi, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'
import { inicioDoMes } from '../vendas/tipos'

const hoje = hojeIso()
const mes = inicioDoMes(hoje)

const catalogo = [
  {
    codigo: 'vendas-agrupadas', titulo: 'Vendas agrupadas', area: 'Vendas', descricao: 'Peças, total, custo e lucro bruto do período.',
    parametros: [
      { nome: 'periodo', rotulo: 'Período', tipo: 'Periodo', padrao: 'mes', opcoes: null },
      { nome: 'agrupamento', rotulo: 'Agrupar por', tipo: 'Opcao', padrao: 'produto', opcoes: [{ valor: 'produto', rotulo: 'Produto' }, { valor: 'tamanho', rotulo: 'Tamanho' }] },
    ],
  },
  { codigo: 'dre', titulo: 'DRE gerencial', area: 'Gestão', descricao: 'Resultado do período.', parametros: [{ nome: 'periodo', rotulo: 'Período', tipo: 'Periodo', padrao: 'mes', opcoes: null }] },
  {
    codigo: 'produtos-sem-giro', titulo: 'Produtos sem giro', area: 'Estoque', descricao: 'Produtos parados.',
    parametros: [{ nome: 'dias', rotulo: 'Sem venda há pelo menos (dias)', tipo: 'Inteiro', padrao: '60', opcoes: null }],
  },
]

const dados = (extra = {}) => ({
  titulo: 'Vendas agrupadas',
  colunas: [
    { titulo: 'Tamanho', tipo: 'Texto', totalizar: false },
    { titulo: 'Peças', tipo: 'Inteiro', totalizar: true },
    { titulo: 'Total', tipo: 'Moeda', totalizar: true },
    { titulo: '% do total', tipo: 'Numero', totalizar: false },
  ],
  linhas: [['M', 2, 119.8, 50], ['G', 1, 59.9, 25]],
  filtros: [{ nome: 'Período', valor: '01/10/2026 a 19/10/2026' }],
  totalDeLinhas: 2,
  ...extra,
})

function api(extras: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(),
    'GET /health': () => new Response('Healthy'),
    'GET /api/relatorios': () => json(catalogo),
    ...extras,
  })
}

describe('Relatórios → central', () => {
  it('lista os relatórios por área com a descrição', async () => {
    api()
    renderApp('/relatorios')

    expect(await screen.findByRole('heading', { name: 'Relatórios' })).toBeInTheDocument()
    const vendas = await screen.findByRole('region', { name: 'Vendas' })
    expect(within(vendas).getByRole('button', { name: /Vendas agrupadas/ })).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Gestão' })).getByRole('button', { name: /DRE gerencial/ })).toBeInTheDocument()
    expect(screen.getByText('Produtos parados.')).toBeInTheDocument()
  })

  it('escolhe o relatório, ajusta os filtros, visualiza na tela com total e exporta', async () => {
    const chamadas = api({
      [`GET /api/relatorios/vendas-agrupadas/dados?de=${mes}&ate=${hoje}&agrupamento=produto`]: () => json(dados({ colunas: [{ titulo: 'Produto', tipo: 'Texto', totalizar: false }], linhas: [['CAM-001 · Camiseta']] })),
      [`GET /api/relatorios/vendas-agrupadas/dados?de=${mes}&ate=${hoje}&agrupamento=tamanho`]: () => json(dados()),
    })
    renderApp('/relatorios')
    await userEvent.click(await screen.findByRole('button', { name: /Vendas agrupadas/ }))

    const filtros = await screen.findByRole('form', { name: 'Filtros de Vendas agrupadas' })
    expect(within(filtros).getByLabelText('De')).toHaveValue(mes)
    expect(within(filtros).getByLabelText('Até')).toHaveValue(hoje)
    expect(within(filtros).getByLabelText('Agrupar por')).toHaveValue('produto')
    await userEvent.click(within(filtros).getByRole('button', { name: 'Visualizar' }))
    expect(await screen.findByText('CAM-001 · Camiseta')).toBeInTheDocument()

    await userEvent.selectOptions(within(filtros).getByLabelText('Agrupar por'), 'tamanho')
    await userEvent.click(within(filtros).getByRole('button', { name: 'Visualizar' }))
    const tabela = await screen.findByRole('table', { name: 'Vendas agrupadas' })
    expect(within(tabela).getByText('R$ 119,80')).toBeInTheDocument()
    expect(within(tabela).getByText('50')).toBeInTheDocument()
    const total = within(tabela).getByText('Total', { selector: 'td' }).closest('tr')!
    expect(within(total).getByText('3')).toBeInTheDocument()
    expect(within(total).getByText('R$ 179,70')).toBeInTheDocument()
    expect(screen.getByText(/01\/10\/2026 a 19\/10\/2026/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Exportar/ })).toBeInTheDocument()
    expect(chamadas).toBeDefined()
  })

  it('campo numérico com o padrão e aviso quando a tela mostra só parte das linhas', async () => {
    api({
      'GET /api/relatorios/produtos-sem-giro/dados?dias=90': () =>
        json(dados({ titulo: 'Produtos sem giro', colunas: [{ titulo: 'Produto', tipo: 'Texto', totalizar: false }], linhas: [['Calça']], totalDeLinhas: 800 })),
    })
    renderApp('/relatorios')
    await userEvent.click(await screen.findByRole('button', { name: /Produtos sem giro/ }))
    const campo = await screen.findByLabelText('Sem venda há pelo menos (dias)')
    expect(campo).toHaveValue('60')
    await userEvent.clear(campo)
    await userEvent.type(campo, '90')
    await userEvent.click(screen.getByRole('button', { name: 'Visualizar' }))

    expect(await screen.findByText('Calça')).toBeInTheDocument()
    expect(screen.getByText('Mostrando 1 de 800 linhas. Exporte para ver todas.')).toBeInTheDocument()
  })

  it('perfil sem relatórios vê o aviso', async () => {
    api({ 'GET /api/relatorios': () => json([]) })
    renderApp('/relatorios')

    await waitFor(() => expect(screen.getByText('Nenhum relatório disponível para o seu perfil nesta empresa.')).toBeInTheDocument())
  })
})
