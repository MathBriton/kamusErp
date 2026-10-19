import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { hojeIso, somarDias } from '../financeiro/tipos'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const hoje = hojeIso()
const dias = Array.from({ length: 30 }, (_, i) => ({ data: somarDias(hoje, i - 29), vendas: 0, pecas: 0, total: 0 }))
dias[29] = { data: hoje, vendas: 2, pecas: 3, total: 179.7 }

const cartao = (extra = {}) => ({ total: 179.7, vendas: 2, pecas: 3, ticketMedio: 89.85, totalAnterior: 59.9, variacao: 200, ...extra })
const compromissos = (extra = {}) => ({ hoje: 0, amanha: 0, proximosSeteDias: 0, vencido: 0, parcelasVencidas: 0, ...extra })
const dashboard = (extra = {}) => ({
  periodo: 'hoje',
  de: hoje,
  ate: hoje,
  vendas: {
    periodo: cartao(),
    mes: cartao({ total: 2390.5, vendas: 30, variacao: -12.5, totalAnterior: 2732 }),
    ultimos30Dias: dias,
    porForma: [{ nome: 'PIX', quantidade: 2, total: 179.7 }],
    porVendedor: [{ nome: 'Bia', quantidade: 2, total: 179.7 }],
    maisVendidos: [{ skuId: 's1', descricao: 'Camiseta básica · Azul M', pecas: 2, total: 119.8 }],
    somenteAsMinhas: false,
  },
  financeiro: {
    contas: [{ id: 'c1', nome: 'Caixa da loja', tipo: 'Caixa', saldo: 339.6 }],
    saldoTotal: 339.6,
    aReceber: compromissos({ hoje: 120, proximosSeteDias: 600 }),
    aPagar: compromissos({ hoje: 30, proximosSeteDias: 30, vencido: 50, parcelasVencidas: 1 }),
    crediarioEmAtraso: 0,
    clientesEmAtraso: 0,
  },
  estoque: { pecas: 6, valorDeCusto: 120, valorDeVenda: 359.4, skusNegativos: 0, quebras: [], totalDeQuebras: 0 },
  condicionais: { abertas: 1, atrasadas: 1, pecas: 1, valor: 59.9 },
  alertas: [
    { tipo: 'contas_a_pagar_vencidas', gravidade: 'perigo', titulo: '1 conta a pagar vencida', detalhe: 'R$ 50,00 em atraso', link: '/financeiro/contas-a-pagar' },
    { tipo: 'condicionais_atrasadas', gravidade: 'perigo', titulo: '1 condicional atrasada', detalhe: 'Passaram do prazo de devolução', link: '/vendas/condicional' },
  ],
  ...extra,
})
const vazio = () =>
  dashboard({
    vendas: { ...dashboard().vendas, periodo: cartao({ total: 0, vendas: 0, pecas: 0, ticketMedio: 0, totalAnterior: 0, variacao: null }), ultimos30Dias: dias.map((d) => ({ ...d, vendas: 0, pecas: 0, total: 0 })), maisVendidos: [] },
    estoque: { pecas: 0, valorDeCusto: 0, valorDeVenda: 0, skusNegativos: 0, quebras: [], totalDeQuebras: 0 },
    alertas: [],
  })

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo, empresaAtiva: Parameters<typeof mockApi>[0][string] = empresaAtivaHandler(sessao)) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtiva,
    'GET /health': () => new Response('Degraded'),
    'GET /api/indicadores?periodo=hoje': () => json(dashboard()),
    ...extras,
  })
}

describe('Dashboard', () => {
  it('saúda o usuário e mostra a empresa validada pela API', async () => {
    api()
    renderApp()

    expect(await screen.findByRole('heading', { name: /Olá, Admin/ })).toBeInTheDocument()
    expect(await screen.findByText('Kamus Moda Centro · 12.345.678/0001-95')).toBeInTheDocument()
  })

  it('mostra vendas com comparação, contas, gráfico, mais vendidos e alertas com link', async () => {
    api()
    renderApp()

    const vendas = (await screen.findByText('Vendas hoje')).closest('article')!
    expect(within(vendas).getByText('R$ 179,70')).toBeInTheDocument()
    expect(within(vendas).getByText('▲ 200% vs ontem')).toBeInTheDocument()
    expect(within(vendas).getByText('2 vendas · ticket R$ 89,85')).toBeInTheDocument()
    const mes = screen.getByText('Vendas no mês').closest('article')!
    expect(within(mes).getByText('▼ 12,5% vs mês anterior')).toBeInTheDocument()
    const pagar = screen.getByText('A pagar').closest('article')!
    expect(within(pagar).getByText('Hoje R$ 30,00')).toBeInTheDocument()
    expect(within(pagar).getByText('R$ 50,00 vencido')).toBeInTheDocument()

    expect(screen.getByRole('img', { name: /Vendas dos últimos 30 dias/ })).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Saldo das contas' })).getByText('R$ 339,60')).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Mais vendidos' })).getByText('Camiseta básica · Azul M')).toBeInTheDocument()

    const alertas = screen.getByRole('region', { name: 'Alertas' })
    expect(within(alertas).getByRole('link', { name: /1 conta a pagar vencida/ })).toHaveAttribute('href', '/financeiro/contas-a-pagar')
    expect(within(alertas).getByRole('link', { name: /1 condicional atrasada/ })).toHaveAttribute('href', '/vendas/condicional')
    expect(screen.queryByRole('region', { name: 'Primeiros passos' })).not.toBeInTheDocument()
  })

  it('trocar o período busca de novo e muda o título do cartão', async () => {
    api({ 'GET /api/indicadores?periodo=7dias': () => json(dashboard({ periodo: '7dias', vendas: { ...dashboard().vendas, periodo: cartao({ total: 900 }) } })) })
    renderApp()
    await screen.findByText('Vendas hoje')

    await userEvent.click(screen.getByRole('radio', { name: '7 dias' }))

    const card = (await screen.findByText('Vendas em 7 dias')).closest('article')!
    expect(within(card).getByText('R$ 900,00')).toBeInTheDocument()
    expect(within(card).getByText('▲ 200% vs 7 dias anteriores')).toBeInTheDocument()
  })

  it('loja sem dados mostra os primeiros passos', async () => {
    api({ 'GET /api/indicadores?periodo=hoje': () => json(vazio()) })
    renderApp()

    const passos = await screen.findByRole('region', { name: 'Primeiros passos' })
    expect(within(passos).getByRole('link', { name: /Cadastrar produtos/ })).toHaveAttribute('href', '/cadastros/produtos')
    expect(within(screen.getByRole('region', { name: 'Alertas' })).getByText('Sem alertas por enquanto')).toBeInTheDocument()
  })

  it('vendedor vê só as próprias vendas, sem financeiro', async () => {
    api({ 'GET /api/indicadores?periodo=hoje': () => json(dashboard({ vendas: { ...dashboard().vendas, somenteAsMinhas: true }, financeiro: null, estoque: null })) }, sessaoVendedor)
    renderApp()

    expect(await screen.findByText('Minhas vendas hoje')).toBeInTheDocument()
    expect(screen.queryByText('A pagar')).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Saldo das contas' })).not.toBeInTheDocument()
  })

  it('visão consolidada mostra quantas empresas estão somadas', async () => {
    api()
    renderApp()
    await screen.findByText('Kamus Moda Centro · 12.345.678/0001-95')

    await userEvent.click(screen.getByRole('button', { name: /Empresa ativa/ }))
    await userEvent.click(screen.getByRole('menuitemradio', { name: /Todas as empresas/ }))

    expect(await screen.findByText('Visão consolidada · 2 empresas (somente leitura)')).toBeInTheDocument()
  })

  it('empresa recusada pela API mostra o motivo', async () => {
    api({}, sessaoDemo, () => problema(403, 'Você não tem acesso a esta empresa.'))
    renderApp()

    expect(await screen.findByRole('alert')).toHaveTextContent('Você não tem acesso a esta empresa.')
  })
})

describe('Dashboard → resumo diário', () => {
  it('configura horário, dias e blocos, vê a prévia e envia para o sininho', async () => {
    let salvo: Record<string, unknown> | null = null
    let enviado = false
    api({
      'GET /api/indicadores/resumo-diario': () =>
        json({ ativo: true, hora: 20, dias: [0, 1, 2, 3, 4, 5, 6], vendas: true, financeiro: true, estoque: true, condicionais: true, crediario: true, enviarSemMovimento: false, ultimoEnvio: null }),
      'PUT /api/indicadores/resumo-diario': (init) => {
        salvo = corpo(init)
        return json({ ...salvo, ultimoEnvio: null })
      },
      'GET /api/indicadores/resumo-diario/previa': () =>
        json({ titulo: `Resumo do dia ${hoje.slice(8, 10)}/${hoje.slice(5, 7)}`, linhas: ['Vendas: R$ 179,70 em 2 vendas (3 peças)', 'Condicionais: 1 aberta na rua'], temMovimento: true }),
      'POST /api/indicadores/resumo-diario/enviar': () => {
        enviado = true
        return new Response(null, { status: 204 })
      },
    })
    renderApp()
    await userEvent.click(await screen.findByRole('button', { name: 'Resumo diário' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Resumo diário' })

    expect(await within(dialogo).findByLabelText('Horário')).toHaveValue('20')
    await userEvent.selectOptions(within(dialogo).getByLabelText('Horário'), '8')
    await userEvent.click(within(dialogo).getByRole('checkbox', { name: 'Dom' }))
    await userEvent.click(within(dialogo).getByRole('checkbox', { name: 'Estoque (quebra de grade e negativos)' }))
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(salvo).toMatchObject({ ativo: true, hora: 8, dias: [1, 2, 3, 4, 5, 6], estoque: false, vendas: true }))
    expect(await within(dialogo).findByText('Preferências salvas.')).toBeInTheDocument()

    await userEvent.click(within(dialogo).getByRole('button', { name: 'Ver prévia de hoje' }))
    expect(await within(dialogo).findByText('Vendas: R$ 179,70 em 2 vendas (3 peças)')).toBeInTheDocument()
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Enviar agora para o sininho' }))
    await waitFor(() => expect(enviado).toBe(true))
    expect(await within(dialogo).findByText('Enviado. Confira o sininho.')).toBeInTheDocument()
  })
})
