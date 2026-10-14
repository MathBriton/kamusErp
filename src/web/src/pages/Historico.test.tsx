import { screen, within } from '@testing-library/react'
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

function api(auditoria: Parameters<typeof mockApi>[0][string] = () => json({ itens: registros, total: 2, pagina: 1, tamanho: 50 })) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(),
    'GET /health': () => new Response('Degraded'),
    'GET /api/auditoria?pagina=1&tamanho=50': auditoria,
    'GET /api/auditoria?pagina=1&tamanho=50&tipo=Alteracao': () => json({ itens: [registros[1]], total: 1, pagina: 1, tamanho: 50 }),
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
    const linha = (await screen.findByText('Alteração de Empresa')).closest('li')!

    await userEvent.click(within(linha).getByRole('button', { name: 'Ver detalhes' }))

    expect(within(linha).getByText('NomeFantasia')).toBeInTheDocument()
    expect(within(linha).getByText('Kamus Moda Centro')).toBeInTheDocument()
    expect(within(linha).getByText('Kamus Moda Centro Novo')).toBeInTheDocument()
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
    api(() => json({ itens: [], total: 0, pagina: 1, tamanho: 50 }))
    renderApp('/configuracoes/historico')

    expect(await screen.findByText('Nenhum registro encontrado.')).toBeInTheDocument()
  })
})
