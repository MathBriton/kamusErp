import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { empresaAtivaHandler, json, mockApi, problema, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const registros = [
  {
    id: 'r1', ocorridoEm: '2026-10-14T15:30:00Z', tipo: 'Evento', evento: 'login', entidade: null, entidadeId: null,
    descricao: 'Login de admin@kamus.local', usuarioNome: 'Admin Demo', ip: '127.0.0.1', alteracoes: [],
  },
  {
    id: 'r2', ocorridoEm: '2026-10-14T15:00:00Z', tipo: 'Alteracao', evento: null, entidade: 'Empresa', entidadeId: 'e1',
    descricao: 'Alteração de Empresa', usuarioNome: 'Sistema', ip: null,
    alteracoes: [{ campo: 'NomeFantasia', antes: 'Kamus Moda Centro', depois: 'Kamus Moda Centro Novo' }],
  },
]

function api(auditoria: Parameters<typeof mockApi>[0][string] = () => json({ itens: registros, total: 2, pagina: 1, tamanho: 20 }), extras: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(),
    'GET /health': () => new Response('Degraded'),
    'GET /api/auditoria?pagina=1&tamanho=20': auditoria,
    'GET /api/auditoria?tipo=Alteracao&pagina=1&tamanho=20': () => json({ itens: [registros[1]], total: 1, pagina: 1, tamanho: 20 }),
    ...extras,
  })
}

describe('Histórico de alterações', () => {
  it('lista eventos e alterações com usuário e data', async () => {
    api()
    renderApp('/configuracoes/historico')

    expect(await screen.findByRole('heading', { name: 'Histórico de alterações' })).toBeInTheDocument()
    expect(await screen.findByText('Login de admin@kamus.local')).toBeInTheDocument()
    expect(screen.getByText('Alteração de Empresa')).toBeInTheDocument()
    expect(screen.getAllByText('Admin Demo').length).toBeGreaterThan(0)
    expect(screen.getByText('2 registros')).toBeInTheDocument()
  })

  it('mostra os valores antes e depois ao abrir os detalhes', async () => {
    api()
    renderApp('/configuracoes/historico')
    const linha = (await screen.findByText('Alteração de Empresa')).closest('tr')!

    await userEvent.click(within(linha).getByRole('button', { name: 'Ver detalhes de Alteração de Empresa' }))

    const detalhe = screen.getByRole('dialog', { name: 'Alteração de Empresa' })
    expect(within(detalhe).getByText('NomeFantasia')).toBeInTheDocument()
    expect(within(detalhe).getByText('Kamus Moda Centro')).toBeInTheDocument()
    expect(within(detalhe).getByText('Kamus Moda Centro Novo')).toBeInTheDocument()
  })

  it('período em dias de Brasília, página seguinte e exportação dos selecionados', async () => {
    const pagina = (n: number) => json({ itens: [{ ...registros[0], id: `r${n}`, descricao: `Login ${n}` }], total: 45, pagina: n, tamanho: 20 })
    const fetch = api(undefined, {
      'GET /api/auditoria?de=2026-10-01T00%3A00%3A00-03%3A00&pagina=1&tamanho=20': () => pagina(1),
      'GET /api/auditoria?de=2026-10-01T00%3A00%3A00-03%3A00&ate=2026-10-14T23%3A59%3A59-03%3A00&pagina=1&tamanho=20': () => pagina(1),
      'GET /api/auditoria?de=2026-10-01T00%3A00%3A00-03%3A00&ate=2026-10-14T23%3A59%3A59-03%3A00&pagina=2&tamanho=20': () => pagina(2),
      'GET /api/relatorios/historico-de-alteracoes.csv?ids=r2': () => new Response('csv', { headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=historico.csv' } }),
    })
    URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
    URL.revokeObjectURL = () => {}
    renderApp('/configuracoes/historico')
    await screen.findByText('Login de admin@kamus.local')

    await userEvent.type(screen.getByLabelText('De'), '2026-10-01')
    await userEvent.type(screen.getByLabelText('Até'), '2026-10-14')
    expect(await screen.findByText('Login 1')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Próxima página' }))
    expect(await screen.findByText('Login 2')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Login 2' }))
    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/historico-de-alteracoes.csv?ids=r2')).toBe(true))
  })

  it('filtra por tipo', async () => {
    api()
    renderApp('/configuracoes/historico')
    await screen.findByText('Login de admin@kamus.local')

    await userEvent.selectOptions(screen.getByLabelText('Tipo'), 'Alteracao')

    expect(await screen.findByText('1 registro')).toBeInTheDocument()
    expect(screen.queryByText('Login de admin@kamus.local')).not.toBeInTheDocument()
  })

  it('sem permissão mostra o motivo', async () => {
    api(() => problema(403, 'O histórico de alterações é restrito aos perfis Dono e Gerente.'))
    renderApp('/configuracoes/historico')

    expect(await screen.findByRole('alert')).toHaveTextContent('restrito aos perfis Dono e Gerente')
  })

  it('sem registros mostra estado vazio', async () => {
    api(() => json({ itens: [], total: 0, pagina: 1, tamanho: 20 }))
    renderApp('/configuracoes/historico')

    expect(await screen.findByText('Nenhum registro encontrado.')).toBeInTheDocument()
  })
})
