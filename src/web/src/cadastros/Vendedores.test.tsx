import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const vendedores = [
  { id: 'v1', nome: 'Vendedora Demo', comissaoPercentual: 3.5, usuarioId: 'u2', usuarioNome: 'Vendedora Demo', telefone: null, ativo: true },
  { id: 'v2', nome: 'Carlos (ex)', comissaoPercentual: 2, usuarioId: null, usuarioNome: null, telefone: null, ativo: false },
]
const usuarios = [{ id: 'u2', nome: 'Vendedora Demo', email: 'vendedor@kamus.local' }, { id: 'u3', nome: 'Bia', email: 'bia@kamus.local' }]

const pagina = (itens: unknown[], total = itens.length) => json({ itens, total, pagina: 1, tamanho: 20 })

function api(extras: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(),
    'GET /health': () => new Response('Healthy'),
    'GET /api/vendedores?situacao=ativos&pagina=1&tamanho=20': () => pagina([vendedores[0]]),
    'GET /api/vendedores?situacao=todos&pagina=1&tamanho=20': () => pagina(vendedores),
    'GET /api/vendedores/usuarios': () => json(usuarios),
    ...extras,
  })
}

describe('Cadastros → Vendedores', () => {
  it('grade com comissão, login vinculado, situação e total; filtro de situação vai para a API', async () => {
    api({ 'GET /api/vendedores?situacao=todos&busca=carlos&pagina=1&tamanho=20': () => pagina([vendedores[1]]) })
    renderApp('/cadastros/vendedores')

    const linha = (await screen.findByRole('cell', { name: 'Vendedora Demo' })).closest('tr')!
    expect(within(linha).getByText('3,5%')).toBeInTheDocument()
    expect(within(linha).getByText('vinculado: Vendedora Demo')).toBeInTheDocument()
    expect(within(linha).getByText('Ativo')).toBeInTheDocument()
    expect(screen.getByRole('table', { name: 'Vendedores' })).toBeInTheDocument()
    expect(screen.getByText('1 registro')).toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'todos')
    expect(within((await screen.findByRole('cell', { name: 'Carlos (ex)' })).closest('tr')!).getByText('Inativo')).toBeInTheDocument()
    expect(screen.getByText('2 registros')).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Buscar'), 'carlos')
    await waitFor(() => expect(screen.queryByRole('cell', { name: 'Vendedora Demo' })).not.toBeInTheDocument())
    expect(screen.getByRole('cell', { name: 'Carlos (ex)' })).toBeInTheDocument()
  })

  it('novo vendedor com comissão e usuário', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/vendedores': (init) => {
        enviado = corpo(init)
        return json({ id: 'v3' }, 201)
      },
    })
    renderApp('/cadastros/vendedores')
    await screen.findByRole('cell', { name: 'Vendedora Demo' })

    await userEvent.click(screen.getByRole('button', { name: 'Novo vendedor' }))
    const dialogo = screen.getByRole('dialog', { name: 'Novo vendedor' })
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Bia')
    await userEvent.clear(within(dialogo).getByLabelText('Comissão (%)'))
    await userEvent.type(within(dialogo).getByLabelText('Comissão (%)'), '4,5')
    await userEvent.selectOptions(within(dialogo).getByLabelText('Usuário do sistema'), 'u3')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toEqual({ nome: 'Bia', comissaoPercentual: 4.5, usuarioId: 'u3', telefone: null })
  })

  it('editar pela ação da linha', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'PUT /api/vendedores/v1': (init) => {
        enviado = corpo(init)
        return json(vendedores[0])
      },
    })
    renderApp('/cadastros/vendedores')

    await userEvent.click(within((await screen.findByRole('cell', { name: 'Vendedora Demo' })).closest('tr')!).getByRole('button', { name: 'Editar Vendedora Demo' }))
    const dialogo = screen.getByRole('dialog', { name: 'Editar vendedor' })
    expect(within(dialogo).getByLabelText('Usuário do sistema')).toHaveValue('u2')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toEqual({ nome: 'Vendedora Demo', comissaoPercentual: 3.5, usuarioId: 'u2', telefone: null })
  })

  it('inativar e reativar com confirmação', async () => {
    const chamadas: string[] = []
    api({
      'POST /api/vendedores/v1/inativar': () => {
        chamadas.push('inativar')
        return new Response(null, { status: 204 })
      },
      'POST /api/vendedores/v2/reativar': () => {
        chamadas.push('reativar')
        return new Response(null, { status: 204 })
      },
    })
    renderApp('/cadastros/vendedores')
    await screen.findByRole('cell', { name: 'Vendedora Demo' })
    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'todos')

    await userEvent.click(within((await screen.findByRole('cell', { name: 'Carlos (ex)' })).closest('tr')!).getByRole('button', { name: 'Reativar Carlos (ex)' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Reativar vendedor' })).getByRole('button', { name: 'Reativar' }))
    await waitFor(() => expect(chamadas).toEqual(['reativar']))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())

    await userEvent.click(within(screen.getByRole('cell', { name: 'Vendedora Demo' }).closest('tr')!).getByRole('button', { name: 'Inativar Vendedora Demo' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Inativar vendedor' })).getByRole('button', { name: 'Inativar' }))
    await waitFor(() => expect(chamadas).toEqual(['reativar', 'inativar']))
  })

  it('seleção: reativa em lote e exporta só os selecionados', async () => {
    let lote: Record<string, unknown> | null = null
    const fetch = api({
      'POST /api/vendedores/situacao': (init) => {
        lote = corpo(init)
        return json({ alterados: 1 })
      },
      'GET /api/relatorios/vendedores.pdf?ids=v1%2Cv2': () => new Response('pdf', { headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': 'attachment; filename=vendedores.pdf' } }),
    })
    URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
    URL.revokeObjectURL = () => {}
    renderApp('/cadastros/vendedores')
    await screen.findByRole('cell', { name: 'Vendedora Demo' })
    await userEvent.selectOptions(screen.getByLabelText('Situação'), 'todos')
    await screen.findByRole('cell', { name: 'Carlos (ex)' })

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar todos da página' }))
    const barra = screen.getByRole('region', { name: 'Seleção' })
    expect(barra).toHaveTextContent('2 selecionados')

    await userEvent.click(screen.getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(screen.getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /PDF/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/vendedores.pdf?ids=v1%2Cv2')).toBe(true))

    await userEvent.click(within(barra).getByRole('button', { name: 'Reativar selecionados' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Reativar vendedores' })).getByRole('button', { name: 'Reativar' }))
    await waitFor(() => expect(lote).toEqual({ ids: ['v1', 'v2'], ativo: true }))
  })
})
