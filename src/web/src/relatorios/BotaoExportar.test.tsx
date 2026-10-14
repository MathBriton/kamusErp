import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { empresaAtivaHandler, json, mockApi, notificacoesDemo, problema, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const historico = { itens: [], total: 0, pagina: 1, tamanho: 50 }
const arquivo = () =>
  new Response('conteudo', {
    status: 200,
    headers: {
      'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      'Content-Disposition': "attachment; filename=historico.xlsx; filename*=UTF-8''historico-de-alteracoes-20261014-1230.xlsx",
    },
  })

function api(exportar: Parameters<typeof mockApi>[0][string]) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(),
    'GET /health': () => new Response('Degraded'),
    'GET /api/notificacoes': () => json({ itens: [], naoLidas: 0 }),
    'GET /api/auditoria?pagina=1&tamanho=50': () => json(historico),
    'GET /api/auditoria?pagina=1&tamanho=50&tipo=Alteracao': () => json(historico),
    'GET /api/relatorios/historico-de-alteracoes.xlsx?tipo=Alteracao': exportar,
    'GET /api/relatorios/historico-de-alteracoes.pdf': exportar,
  })
}

describe('Exportar relatório', () => {
  let criarUrl: ReturnType<typeof vi.fn>
  let cliques: string[]

  beforeEach(() => {
    criarUrl = vi.fn(() => 'blob:arquivo')
    cliques = []
    URL.createObjectURL = criarUrl as unknown as typeof URL.createObjectURL
    URL.revokeObjectURL = vi.fn()
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      cliques.push(this.download)
    })
  })

  afterEach(() => vi.restoreAllMocks())

  async function abrirMenu() {
    await screen.findByText('0 registros')
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    return screen.getByRole('menu', { name: 'Exportar' })
  }

  it('baixa o Excel com os filtros da tela e o nome de arquivo da API', async () => {
    const fetch = api(arquivo)
    renderApp('/configuracoes/historico')
    await screen.findByText('0 registros')
    await userEvent.selectOptions(screen.getByLabelText('Tipo'), 'Alteracao')

    const menu = await abrirMenu()
    await userEvent.click(within(menu).getByRole('menuitem', { name: /Excel/ }))

    expect(await screen.findByRole('status')).toHaveTextContent('Download iniciado')
    expect(criarUrl).toHaveBeenCalled()
    expect(cliques).toEqual(['historico-de-alteracoes-20261014-1230.xlsx'])
    const chamada = fetch.mock.calls.find(([url]) => String(url).startsWith('/api/relatorios/'))!
    expect(new Headers(chamada[1]?.headers).get('X-Empresa-Id')).toBe('e1')
  })

  it('relatório grande avisa que vai chegar pelo sininho', async () => {
    api(() => json({ mensagem: 'Relatório grande (9000 linhas): você será avisado no sininho quando o arquivo estiver pronto.' }, 202))
    renderApp('/configuracoes/historico')

    const menu = await abrirMenu()
    await userEvent.click(within(menu).getByRole('menuitem', { name: /PDF/ }))

    expect(await screen.findByRole('status')).toHaveTextContent('avisado no sininho')
    expect(cliques).toEqual([])
  })

  it('erro da API aparece para o usuário', async () => {
    api(() => problema(403, 'Seu perfil não pode exportar "Histórico de alterações".'))
    renderApp('/configuracoes/historico')

    const menu = await abrirMenu()
    await userEvent.click(within(menu).getByRole('menuitem', { name: /PDF/ }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Seu perfil não pode exportar')
  })

  it('notificação de relatório pronto abre o download direto', async () => {
    const abrir = vi.spyOn(window, 'open').mockImplementation(() => null)
    mockApi({
      'POST /api/auth/refresh': () => json(sessaoDemo),
      'GET /api/empresas/ativa': empresaAtivaHandler(),
      'GET /health': () => new Response('Degraded'),
      'POST /api/notificacoes/r1/lida': () => new Response(null, { status: 204 }),
      'GET /api/notificacoes': () =>
        json({
          naoLidas: 1,
          itens: [{ ...notificacoesDemo.itens[0]!, id: 'r1', tipo: 'relatorio_pronto', titulo: 'Relatório pronto para baixar', link: '/api/relatorios/arquivos/abc' }],
        }),
    })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Notificações: 1 não lida' }))
    await userEvent.click(screen.getByRole('button', { name: /Relatório pronto para baixar/ }))

    expect(abrir).toHaveBeenCalledWith('/api/relatorios/arquivos/abc', '_self')
  })
})
