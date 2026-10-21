import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { empresaAtivaHandler, json, mockApi, sessaoDemo, sessaoVendedor } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const base = {
  plano: 'Piloto', valorMensal: 199.9, limiteDeCnpjs: null, limiteDeLojas: null, limiteDeUsuarios: null,
}
const ativa = { ...base, estado: 'Ativa', vencimento: '2026-11-07', fimDaCarencia: '2026-11-12', diasParaVencer: 24, mensagem: 'Licença em dia. Próximo vencimento em 07/11/2026.' }
const carencia = {
  ...base, estado: 'Carencia', vencimento: '2026-10-10', fimDaCarencia: '2026-10-15', diasParaVencer: -4,
  mensagem: 'A mensalidade venceu em 10/10/2026. Você está no período de carência até 15/10/2026: o uso continua normal, mas regularize para evitar a suspensão.',
}
const suspensa = {
  ...base, estado: 'Suspensa', vencimento: '2026-10-01', fimDaCarencia: '2026-10-06', diasParaVencer: -13,
  mensagem: 'Licença suspensa por falta de pagamento (carência terminou em 06/10/2026). Seus dados estão preservados e podem ser exportados.',
}
const bloqueio = () =>
  new Response(JSON.stringify({ title: 'Licença suspensa', detail: suspensa.mensagem, codigo: 'LICENCA_SUSPENSA' }), {
    status: 402, headers: { 'Content-Type': 'application/problem+json' },
  })

function api(licenca: () => Response, extras: Parameters<typeof mockApi>[0] = {}, sessao = sessaoDemo) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessao),
    'POST /api/auth/logout': () => new Response(null, { status: 204 }),
    'GET /api/empresas/ativa': empresaAtivaHandler(sessao),
    'GET /health': () => new Response('Healthy'),
    'GET /api/licenca': licenca,
    ...extras,
  })
}

describe('Licença do cliente', () => {
  it('em dia: nenhum aviso', async () => {
    api(() => json(ativa))
    renderApp()

    expect(await screen.findByRole('heading', { name: /Olá, Admin/ })).toBeInTheDocument()
    expect(screen.queryByRole('status', { name: 'Situação da licença' })).not.toBeInTheDocument()
  })

  it('na carência: banner com prazo e link para os detalhes, sem bloquear', async () => {
    api(() => json(carencia))
    renderApp()

    const banner = await screen.findByRole('status', { name: 'Situação da licença' })
    expect(banner).toHaveTextContent('Mensalidade vencida em 10/10/2026')
    expect(banner).toHaveTextContent('regularize até 15/10/2026')
    expect(within(banner).getByRole('link', { name: 'Ver detalhes' })).toHaveAttribute('href', '/configuracoes/licenca')
    expect(screen.getByRole('heading', { name: /Olá, Admin/ })).toBeInTheDocument()
  })

  it('suspensa: tela de bloqueio com dados preservados no lugar do sistema', async () => {
    api(() => json(suspensa))
    renderApp()

    expect(await screen.findByRole('heading', { name: 'Acesso temporariamente suspenso' })).toBeInTheDocument()
    expect(screen.getByText(/dados estão preservados/)).toBeInTheDocument()
    expect(screen.getByText('R$ 199,90 / mês')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /Olá, Admin/ })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Exportar/ })).toBeInTheDocument()
  })

  it('"Já paguei — verificar novamente" libera assim que a API confirma', async () => {
    let chamadas = 0
    api(() => json(chamadas++ === 0 ? suspensa : ativa))
    renderApp()
    await screen.findByRole('heading', { name: 'Acesso temporariamente suspenso' })

    await userEvent.click(screen.getByRole('button', { name: 'Já paguei — verificar novamente' }))

    expect(await screen.findByRole('heading', { name: /Olá, Admin/ })).toBeInTheDocument()
  })

  it('ainda suspensa ao verificar: avisa que o pagamento não foi confirmado', async () => {
    api(() => json(suspensa))
    renderApp()
    await screen.findByRole('heading', { name: 'Acesso temporariamente suspenso' })

    await userEvent.click(screen.getByRole('button', { name: 'Já paguei — verificar novamente' }))

    expect(await screen.findByText(/ainda não foi confirmado/)).toBeInTheDocument()
  })

  it('uma resposta 402 em qualquer tela mostra o bloqueio na hora', async () => {
    let chamadas = 0
    api(() => json(chamadas++ === 0 ? ativa : suspensa), { 'GET /api/auditoria?pagina=1&tamanho=50': bloqueio })
    renderApp('/configuracoes/historico')

    expect(await screen.findByRole('heading', { name: 'Acesso temporariamente suspenso' })).toBeInTheDocument()
  })

  it('sair na tela de bloqueio volta ao login', async () => {
    api(() => json(suspensa))
    renderApp()
    await screen.findByRole('heading', { name: 'Acesso temporariamente suspenso' })

    await userEvent.click(screen.getByRole('button', { name: 'Sair' }))

    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument()
  })
})

describe('Administrativo → Licença', () => {
  const pagamentos = [
    { id: 'p1', valor: 199.9, meses: 1, pagoEm: '2026-10-07', estendeuAte: '2026-11-07', forma: 'Pix' },
  ]

  it('mostra plano, situação, vencimento, valor e pagamentos', async () => {
    api(() => json(ativa), { 'GET /api/licenca/pagamentos': () => json(pagamentos) })
    renderApp('/configuracoes/licenca')

    expect(await screen.findByRole('heading', { name: 'Licença e assinatura' })).toBeInTheDocument()
    expect(await screen.findByText('Piloto')).toBeInTheDocument()
    expect(screen.getByText('Em dia')).toBeInTheDocument()
    expect(screen.getAllByText('07/11/2026').length).toBeGreaterThan(0)
    expect(screen.getByText('R$ 199,90 / mês')).toBeInTheDocument()
    const tabela = await screen.findByRole('table', { name: 'Pagamentos' })
    expect(within(tabela).getByText('07/10/2026')).toBeInTheDocument()
    expect(within(tabela).getByText('Pix')).toBeInTheDocument()
  })

  it('fica no menu Administrativo só para o Dono', async () => {
    api(() => json(ativa), {}, sessaoVendedor)
    renderApp()

    await screen.findByRole('heading', { name: /Olá, Vendedora/ })
    expect(screen.queryByRole('link', { name: /Licença e assinatura/ })).not.toBeInTheDocument()
  })
})
