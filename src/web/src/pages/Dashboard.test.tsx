import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { dataBr, hojeIso, somarDias } from '../financeiro/tipos'
import { corpo, empresaAtivaHandler, json, mockApi, problema, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const hoje = hojeIso()
const dias = Array.from({ length: 30 }, (_, i) => ({ data: somarDias(hoje, i - 29), vendas: 0, pecas: 0, total: 0 }))
dias[29] = { data: hoje, vendas: 2, pecas: 3, total: 179.7 }

const cartao = (extra = {}) => ({ total: 179.7, vendas: 2, pecas: 3, ticketMedio: 89.85, totalAnterior: 59.9, variacao: 200, ...extra })
const compromissos = (extra = {}) => ({ hoje: 0, amanha: 0, proximosSeteDias: 0, vencido: 0, parcelasVencidas: 0, parcelasProximosSeteDias: 0, ...extra })
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
    aReceber: compromissos({ hoje: 120, proximosSeteDias: 600, parcelasProximosSeteDias: 3 }),
    aPagar: compromissos({ hoje: 30, proximosSeteDias: 30, vencido: 50, parcelasVencidas: 1, parcelasProximosSeteDias: 1 }),
    crediarioEmAtraso: 0,
    clientesEmAtraso: 0,
  },
  estoque: { pecas: 6, valorDeCusto: 120, valorDeVenda: 359.4, skusNegativos: 0, quebras: [], totalDeQuebras: 2 },
  condicionais: { abertas: 1, atrasadas: 1, pecas: 1, valor: 59.9 },
  alertas: [
    { tipo: 'contas_a_pagar_vencidas', gravidade: 'perigo', titulo: '1 conta a pagar vencida', detalhe: 'R$ 50,00 em atraso', link: '/financeiro/contas-a-pagar' },
    { tipo: 'condicionais_atrasadas', gravidade: 'perigo', titulo: '1 condicional atrasada', detalhe: 'Passaram do prazo de devolução', link: '/vendas/condicional' },
  ],
  primeirosPassos: ['produtos', 'clientes', 'entrada_de_estoque', 'contas_a_pagar', 'formas_de_pagamento', 'primeira_venda'],
  ...extra,
})
const vazio = () =>
  dashboard({
    vendas: { ...dashboard().vendas, periodo: cartao({ total: 0, vendas: 0, pecas: 0, ticketMedio: 0, totalAnterior: 0, variacao: null }), ultimos30Dias: dias.map((d) => ({ ...d, vendas: 0, pecas: 0, total: 0 })), maisVendidos: [] },
    estoque: { pecas: 0, valorDeCusto: 0, valorDeVenda: 0, skusNegativos: 0, quebras: [], totalDeQuebras: 0 },
    alertas: [],
    primeirosPassos: ['produtos', 'clientes'],
  })
const agora = new Date().toISOString()
const historico = {
  itens: [
    { id: 'h1', ocorridoEm: agora, tipo: 'Evento', evento: 'login', entidade: null, entidadeId: null, descricao: 'Login de admin@kamus.local', usuarioNome: 'Admin Demo', ip: null, alteracoes: [] },
    { id: 'h2', ocorridoEm: agora, tipo: 'Inclusao', evento: null, entidade: 'Produto', entidadeId: 'p1', descricao: 'Inclusão de Produto', usuarioNome: 'Admin Demo', ip: null, alteracoes: [] },
    { id: 'h3', ocorridoEm: agora, tipo: 'Evento', evento: 'ajuste_de_estoque', entidade: null, entidadeId: null, descricao: 'Ajuste de estoque: 2 peças (Avaria)', usuarioNome: 'Bia', ip: null, alteracoes: [] },
    { id: 'h4', ocorridoEm: agora, tipo: 'Inclusao', evento: null, entidade: 'Titulo', entidadeId: 't1', descricao: 'Inclusão de Título', usuarioNome: 'Admin Demo', ip: null, alteracoes: [] },
    { id: 'h5', ocorridoEm: agora, tipo: 'Inclusao', evento: null, entidade: 'Parcela', entidadeId: 'pa1', descricao: 'Inclusão de Parcela', usuarioNome: 'Admin Demo', ip: null, alteracoes: [] },
    { id: 'h6', ocorridoEm: agora, tipo: 'Inclusao', evento: null, entidade: 'Categoria', entidadeId: 'c1', descricao: 'Inclusão de Categoria financeira', usuarioNome: 'Admin Demo', ip: null, alteracoes: [] },
    { id: 'h7', ocorridoEm: agora, tipo: 'Inclusao', evento: null, entidade: 'Categoria', entidadeId: 'c2', descricao: 'Inclusão de Categoria financeira', usuarioNome: 'Admin Demo', ip: null, alteracoes: [] },
    { id: 'h8', ocorridoEm: agora, tipo: 'Inclusao', evento: null, entidade: 'Sku', entidadeId: 's1', descricao: 'Inclusão de SKU', usuarioNome: 'Admin Demo', ip: null, alteracoes: [] },
  ],
  total: 8,
  pagina: 1,
  tamanho: 60,
}

function api(extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo, empresaAtiva: Parameters<typeof mockApi>[0][string] = empresaAtivaHandler(sessao)) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'GET /api/empresas/ativa': empresaAtiva,
    'GET /health': () => new Response('Degraded'),
    'GET /api/indicadores?periodo=hoje': () => json(dashboard()),
    'GET /api/auditoria?pagina=1&tamanho=60': () => json(historico),
    ...extras,
  })
}

describe('Dashboard', () => {
  it('cabeçalho com saudação, empresa e CNPJ, Nova venda e ações rápidas', async () => {
    api()
    renderApp()

    expect(await screen.findByRole('heading', { name: /Olá, Admin/ })).toBeInTheDocument()
    expect(await screen.findByText('Kamus Moda Centro / CNPJ: 12.345.678/0001-95')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Nova venda/ })).toHaveAttribute('href', '/vendas/registro')

    await userEvent.click(screen.getByRole('button', { name: /Ações rápidas/ }))
    const menu = screen.getByRole('menu', { name: 'Ações rápidas' })
    expect(within(menu).getByRole('menuitem', { name: /Novo produto/ })).toHaveAttribute('href', '/cadastros/produtos')
    expect(within(menu).getByRole('menuitem', { name: /Entrada por XML/ })).toHaveAttribute('href', '/estoque/xml')
    expect(within(menu).getByRole('menuitem', { name: /Lançar conta a pagar/ })).toHaveAttribute('href', '/financeiro/contas-a-pagar')
  })

  it('resumo com vendas, a receber (fluxo previsto) e a pagar (vencidas e títulos a vencer)', async () => {
    api()
    renderApp()

    const vendas = (await screen.findByText('Vendas hoje')).closest('article')!
    expect(within(vendas).getByText('R$ 179,70')).toBeInTheDocument()
    expect(within(vendas).getByText('▲ 200% vs ontem')).toBeInTheDocument()
    expect(within(vendas).getByText('2 vendas · ticket R$ 89,85')).toBeInTheDocument()
    const mes = screen.getByText('Vendas no mês').closest('article')!
    expect(within(mes).getByText('▼ 12,5% vs mês anterior')).toBeInTheDocument()
    const receber = screen.getByText('A receber (7 dias)').closest('article')!
    expect(within(receber).getByText('R$ 600,00')).toBeInTheDocument()
    expect(within(receber).getByText('Fluxo previsto')).toBeInTheDocument()
    expect(within(receber).getByText('Hoje R$ 120,00 · 3 parcelas')).toBeInTheDocument()
    const pagar = screen.getByText('A pagar (7 dias)').closest('article')!
    expect(within(pagar).getByText('1 vencida')).toBeInTheDocument()
    expect(within(pagar).getByText('R$ 50,00 vencido')).toBeInTheDocument()
    expect(within(pagar).getByText('Hoje R$ 30,00 · 1 título a vencer')).toBeInTheDocument()
  })

  it('a pagar sem atraso mostra "Em dia"', async () => {
    api({ 'GET /api/indicadores?periodo=hoje': () => json(dashboard({ financeiro: { ...dashboard().financeiro, aPagar: compromissos({ proximosSeteDias: 30, parcelasProximosSeteDias: 1 }) } })) })
    renderApp()

    const pagar = (await screen.findByText('A pagar (7 dias)')).closest('article')!
    expect(within(pagar).getByText('Em dia')).toBeInTheDocument()
  })

  it('gráfico de 30 dias com total, intervalo, média diária, itens vendidos e condicionais abertos', async () => {
    api()
    renderApp()

    const grafico = await screen.findByRole('region', { name: 'Vendas — últimos 30 dias' })
    expect(within(grafico).getByRole('img', { name: /Vendas dos últimos 30 dias/ })).toBeInTheDocument()
    expect(within(grafico).getByText(`${dataBr(somarDias(hoje, -29))} – ${dataBr(hoje)}`)).toBeInTheDocument()
    expect(within(grafico).getByText('R$ 179,70')).toBeInTheDocument()
    expect(within(grafico).getByText('R$ 5,99')).toBeInTheDocument()
    expect(within(grafico).getByText('3 peças')).toBeInTheDocument()
    expect(within(grafico).getByText('1 peça')).toBeInTheDocument()
  })

  it('sem vendas no período o gráfico mostra o aviso de espera', async () => {
    api({ 'GET /api/indicadores?periodo=hoje': () => json(vazio()) })
    renderApp()

    const grafico = await screen.findByRole('region', { name: 'Vendas — últimos 30 dias' })
    expect(within(grafico).getByText('Aguardando a primeira venda para desenhar a curva dos últimos 30 dias.')).toBeInTheDocument()
  })

  it('primeiros passos com progresso, etapas concluídas e atalhos; somem quando tudo está feito', async () => {
    api({ 'GET /api/indicadores?periodo=hoje': () => json(vazio()) })
    const { unmount } = renderApp()

    const passos = await screen.findByRole('region', { name: 'Primeiros passos' })
    expect(within(passos).getByText('2 de 6 concluídos')).toBeInTheDocument()
    expect(within(passos).getByRole('progressbar')).toHaveAttribute('aria-valuenow', '2')
    expect(within(passos).getByRole('listitem', { name: /Cadastrar produtos/ })).toHaveTextContent('Concluído')
    expect(within(passos).getByRole('link', { name: /Importar XML/ })).toHaveAttribute('href', '/estoque/xml')
    expect(within(passos).getByRole('link', { name: /Lançar/ })).toHaveAttribute('href', '/financeiro/contas-a-pagar')
    expect(within(passos).getByRole('link', { name: /Configurar/ })).toHaveAttribute('href', '/vendas/formas-de-pagamento')
    expect(within(passos).getAllByRole('listitem').map((i) => i.getAttribute('aria-label'))).toEqual([
      'Cadastrar produtos', 'Cadastrar clientes', 'Dar entrada no estoque', 'Lançar contas a pagar', 'Configurar formas de pagamento', 'Registrar a primeira venda',
    ])
    expect(within(passos).getByRole('link', { name: /Registrar venda/ })).toHaveAttribute('href', '/vendas/registro')
    expect(within(passos).getByText('Marco')).toBeInTheDocument()
    unmount()

    api()
    renderApp()
    await screen.findByText('Vendas hoje')
    expect(screen.queryByRole('region', { name: 'Primeiros passos' })).not.toBeInTheDocument()
  })

  it('últimos marcos e atualizações com etiqueta por área, sem logins nem registros internos, repetições agrupadas', async () => {
    api()
    renderApp()

    const marcos = await screen.findByRole('region', { name: 'Últimos marcos e atualizações' })
    expect(await within(marcos).findByText('Inclusão de Produto')).toBeInTheDocument()
    const itens = within(marcos).getAllByRole('listitem')
    expect(itens).toHaveLength(4)
    expect(itens[3]).toHaveTextContent('Inclusão de Categoria financeira (2×)')
    expect(within(marcos).queryByText(/Inclusão de Parcela|Inclusão de SKU/)).not.toBeInTheDocument()
    expect(itens[0]).toHaveTextContent('CATÁLOGO')
    expect(itens[1]).toHaveTextContent('ESTOQUE')
    expect(itens[1]).toHaveTextContent('Bia')
    expect(itens[2]).toHaveTextContent('FINANCEIRO')
    expect(within(marcos).queryByText(/Login de/)).not.toBeInTheDocument()
    expect(within(marcos).getByRole('link', { name: /Ver histórico completo/ })).toHaveAttribute('href', '/configuracoes/historico')
  })

  it('alertas com link, estoque com atalhos e card de implantação', async () => {
    api()
    renderApp()

    const alertas = await screen.findByRole('region', { name: 'Alertas' })
    expect(await within(alertas).findByRole('link', { name: /1 conta a pagar vencida/ })).toHaveAttribute('href', '/financeiro/contas-a-pagar')
    const estoque = screen.getByRole('region', { name: 'Estoque' })
    expect(within(estoque).getByText('R$ 359,40')).toBeInTheDocument()
    expect(within(estoque).getByText('R$ 120,00')).toBeInTheDocument()
    expect(within(estoque).getByText('2 produtos com quebra de grade')).toBeInTheDocument()
    expect(within(estoque).getByRole('link', { name: /Etiquetas/ })).toHaveAttribute('href', '/estoque/etiquetas')
    expect(within(estoque).getByRole('link', { name: /Grades e cores/ })).toHaveAttribute('href', '/cadastros/grades')
    expect(within(estoque).getByRole('link', { name: /Ajustes/ })).toHaveAttribute('href', '/estoque/ajustes')
    // Sem número de atendimento configurado, nada de prometer ajuda que não existe.
    expect(screen.queryByRole('region', { name: 'Consultoria e implantação' })).not.toBeInTheDocument()
    expect(screen.queryByRole('complementary', { name: 'Atendimento pelo WhatsApp' })).not.toBeInTheDocument()
  })

  it('com o número de atendimento configurado, mostra o WhatsApp e o card de implantação com link', async () => {
    vi.stubEnv('VITE_SUPORTE_WHATSAPP', '+55 (11) 99999-0000')
    try {
      api()
      renderApp()
      const atendimento = await screen.findByRole('complementary', { name: 'Atendimento pelo WhatsApp' })
      expect(within(atendimento).getByRole('link', { name: 'Abrir conversa no WhatsApp' })).toHaveAttribute('href', expect.stringContaining('https://wa.me/5511999990000'))
      expect(within(screen.getByRole('region', { name: 'Consultoria e implantação' })).getByRole('link', { name: /Falar com um consultor/ })).toBeInTheDocument()
    } finally {
      vi.unstubAllEnvs()
    }
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

  it('enquanto o novo período carrega, o cartão continua com o título e os números do anterior', async () => {
    let responder: (r: Response) => void = () => {}
    api({ 'GET /api/indicadores?periodo=7dias': () => new Promise<Response>((r) => (responder = r)) })
    renderApp()
    await screen.findByText('Vendas hoje')

    await userEvent.click(screen.getByRole('radio', { name: '7 dias' }))

    expect(screen.getByRole('radio', { name: '7 dias' })).toHaveAttribute('aria-checked', 'true')
    expect(within(screen.getByText('Vendas hoje').closest('article')!).getByText('R$ 179,70')).toBeInTheDocument()
    expect(screen.queryByText('Vendas em 7 dias')).not.toBeInTheDocument()
    responder(json(dashboard({ periodo: '7dias', vendas: { ...dashboard().vendas, periodo: cartao({ total: 900 }) } })))
    expect(await screen.findByText('Vendas em 7 dias')).toBeInTheDocument()
  })

  it('atualiza sozinho a cada 60 segundos', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      const fetch = api()
      renderApp()
      await screen.findByText('Vendas hoje')
      const chamadas = () => fetch.mock.calls.filter(([url]) => url === '/api/indicadores?periodo=hoje').length
      const antes = chamadas()

      await act(async () => {
        await vi.advanceTimersByTimeAsync(60_000)
      })

      await waitFor(() => expect(chamadas()).toBe(antes + 1))
    } finally {
      vi.useRealTimers()
    }
  })

  it('vendedor vê só as próprias vendas, sem financeiro nem marcos', async () => {
    const fetch = api({ 'GET /api/indicadores?periodo=hoje': () => json(dashboard({ vendas: { ...dashboard().vendas, somenteAsMinhas: true }, financeiro: null, estoque: null })) }, sessaoVendedor)
    renderApp()

    expect(await screen.findByText('Minhas vendas hoje')).toBeInTheDocument()
    expect(screen.queryByText('A pagar (7 dias)')).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Saldo das contas' })).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Últimos marcos e atualizações' })).not.toBeInTheDocument()
    expect(fetch.mock.calls.some(([url]) => String(url).startsWith('/api/auditoria'))).toBe(false)
  })

  it('visão consolidada mostra quantas empresas estão somadas', async () => {
    api()
    renderApp()
    await screen.findByText('Kamus Moda Centro / CNPJ: 12.345.678/0001-95')

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
