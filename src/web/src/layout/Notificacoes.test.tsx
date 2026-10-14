import { act, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { empresaAtivaHandler, json, mockApi, notificacoesDemo, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'
import { emitirDoServidor } from '../test/signalrFake'

function api() {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(),
    'GET /health': () => new Response('Degraded'),
    'GET /api/notificacoes': () => json(notificacoesDemo),
    'POST /api/notificacoes/n1/lida': () => new Response(null, { status: 204 }),
    'POST /api/notificacoes/lidas': () => new Response(null, { status: 204 }),
    'GET /api/auditoria?pagina=1&tamanho=50': () => json({ itens: [], total: 0, pagina: 1, tamanho: 50 }),
  })
}

const sino = () => screen.getByRole('button', { name: /Notificações/ })

describe('Notificações (sininho)', () => {
  it('mostra quantas notificações não foram lidas', async () => {
    api()
    renderApp()

    expect(await screen.findByRole('button', { name: 'Notificações: 2 não lidas' })).toHaveTextContent('2')
  })

  it('lista as não lidas e, na aba Todas, também as lidas', async () => {
    api()
    renderApp()
    await screen.findByRole('button', { name: 'Notificações: 2 não lidas' })

    await userEvent.click(sino())
    const painel = screen.getByRole('dialog', { name: 'Notificações' })
    expect(within(painel).getByText('Usuário bloqueado por tentativas de acesso')).toBeInTheDocument()
    expect(within(painel).queryByText('Notificação antiga já lida')).not.toBeInTheDocument()

    await userEvent.click(within(painel).getByRole('tab', { name: 'Todas' }))
    expect(within(painel).getByText('Notificação antiga já lida')).toBeInTheDocument()
  })

  it('clicar na notificação marca como lida e abre a tela relacionada', async () => {
    const fetch = api()
    renderApp()
    await screen.findByRole('button', { name: 'Notificações: 2 não lidas' })

    await userEvent.click(sino())
    await userEvent.click(screen.getByRole('button', { name: /Usuário bloqueado por tentativas de acesso/ }))

    expect(await screen.findByRole('heading', { name: 'Histórico de alterações' })).toBeInTheDocument()
    expect(fetch.mock.calls.some(([url, init]) => url === '/api/notificacoes/n1/lida' && init?.method === 'POST')).toBe(true)
    expect(screen.getByRole('button', { name: 'Notificações: 1 não lida' })).toBeInTheDocument()
  })

  it('marcar todas como lidas zera o contador', async () => {
    api()
    renderApp()
    await screen.findByRole('button', { name: 'Notificações: 2 não lidas' })

    await userEvent.click(sino())
    await userEvent.click(screen.getByRole('button', { name: 'Marcar todas como lidas' }))

    expect(await screen.findByRole('button', { name: 'Notificações: nenhuma não lida' })).toBeInTheDocument()
  })

  it('notificação nova chega em tempo real e aparece no topo', async () => {
    api()
    renderApp()
    await screen.findByRole('button', { name: 'Notificações: 2 não lidas' })

    act(() =>
      emitirDoServidor('notificacao', {
        id: 'n9', tipo: 'teste', titulo: 'Chegou agora', mensagem: 'Tempo real', link: '/', empresaId: null,
        criadaEm: new Date().toISOString(), lidaEm: null,
      }),
    )

    expect(screen.getByRole('button', { name: 'Notificações: 3 não lidas' })).toBeInTheDocument()
    await userEvent.click(sino())
    const itens = within(screen.getByRole('dialog', { name: 'Notificações' })).getAllByRole('listitem')
    expect(itens[0]).toHaveTextContent('Chegou agora')
  })
})
