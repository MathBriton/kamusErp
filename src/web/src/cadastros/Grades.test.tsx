import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { corpo, empresaAtivaHandler, json, mockApi, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const grades = [
  { id: 'g1', nome: 'PP ao GG', tamanhos: ['PP', 'P', 'M', 'G', 'GG'], emUso: true },
  { id: 'g2', nome: 'Único', tamanhos: ['Único'], emUso: false },
]
const cores = [{ id: 'c1', nome: 'Azul', hex: '#1D4ED8' }, { id: 'c2', nome: 'Preto', hex: '#111827' }]

function api(extras: Parameters<typeof mockApi>[0] = {}, listas = { grades, cores }) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(),
    'GET /health': () => new Response('Healthy'),
    'GET /api/grades': () => json(listas.grades),
    'GET /api/cores': () => json(listas.cores),
    ...extras,
  })
}

describe('Cadastros → Grades e cores', () => {
  it('lista grades com tamanhos na ordem e cores com amostra', async () => {
    api()
    renderApp('/cadastros/grades')

    const tabela = await screen.findByRole('table', { name: 'Grades de tamanho' })
    const grade = (await within(tabela).findByText('PP ao GG')).closest('tr')!
    expect(within(grade).getByText('PP · P · M · G · GG')).toBeInTheDocument()
    expect(within(grade).getByText('Em uso')).toBeInTheDocument()
    expect(within(grade).queryByRole('button', { name: 'Editar PP ao GG' })).not.toBeInTheDocument()
    expect(within(tabela.closest('section')!).getByText('2 registros')).toBeInTheDocument()
    expect(within(tabela).getByRole('button', { name: 'Editar Único' })).toBeInTheDocument()
    const cores = screen.getByRole('table', { name: 'Cores' })
    expect(within(cores).getByText('Azul')).toBeInTheDocument()
    expect(within(cores).getByText('#1D4ED8')).toBeInTheDocument()
  })

  it('busca filtra grades e cores na tela; seleção exporta só as marcadas', async () => {
    const fetch = api({
      'GET /api/relatorios/cores.csv?ids=c2': () => new Response('csv', { headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=cores.csv' } }),
    })
    URL.createObjectURL = (() => 'blob:x') as typeof URL.createObjectURL
    URL.revokeObjectURL = () => {}
    renderApp('/cadastros/grades')
    await screen.findByText('PP ao GG')

    await userEvent.type(screen.getByLabelText('Buscar'), 'pret')
    const cores = screen.getByRole('table', { name: 'Cores' })
    expect(within(cores).queryByText('Azul')).not.toBeInTheDocument()
    expect(within(cores).getByText('Preto')).toBeInTheDocument()
    expect(within(screen.getByRole('table', { name: 'Grades de tamanho' })).getByText('Nenhuma grade encontrada.')).toBeInTheDocument()

    await userEvent.click(within(cores).getByRole('checkbox', { name: 'Selecionar Preto' }))
    const secao = cores.closest('section')!
    await userEvent.click(within(secao).getByRole('button', { name: /Exportar/ }))
    await userEvent.click(within(within(secao).getByRole('menu', { name: 'Exportar' })).getByRole('menuitem', { name: /CSV/ }))
    await waitFor(() => expect(fetch.mock.calls.some(([url]) => url === '/api/relatorios/cores.csv?ids=c2')).toBe(true))

    await userEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }))
    expect(within(cores).getByText('Azul')).toBeInTheDocument()
  })

  it('editar cor pela ação da linha', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'PUT /api/cores/c1': (init) => {
        enviado = corpo(init)
        return json(cores[0])
      },
    })
    renderApp('/cadastros/grades')

    await userEvent.click(await screen.findByRole('button', { name: 'Editar Azul' }))
    const dialogo = screen.getByRole('dialog', { name: 'Editar cor' })
    await userEvent.clear(within(dialogo).getByLabelText('Nome'))
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Azul royal')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toMatchObject({ nome: 'Azul royal' })
  })

  it('sem nada cadastrado oferece os padrões', async () => {
    const chamadas: string[] = []
    const listas = { grades: [] as typeof grades, cores: [] as typeof cores }
    api({
      'POST /api/grades/padrao': () => {
        chamadas.push('grades')
        listas.grades = grades
        return new Response(null, { status: 204 })
      },
      'POST /api/cores/padrao': () => {
        chamadas.push('cores')
        listas.cores = cores
        return new Response(null, { status: 204 })
      },
    }, listas)
    renderApp('/cadastros/grades')

    await userEvent.click(await screen.findByRole('button', { name: 'Adicionar grades padrão' }))
    await userEvent.click(screen.getByRole('button', { name: 'Adicionar cores padrão' }))

    expect(chamadas).toEqual(['grades', 'cores'])
    expect(await screen.findByText('PP ao GG')).toBeInTheDocument()
    expect(await screen.findByText('Preto')).toBeInTheDocument()
  })

  it('nova grade: tamanhos separados por vírgula, na ordem digitada', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/grades': (init) => {
        enviado = corpo(init)
        return json({ id: 'g3', nome: 'Plus', tamanhos: ['G1', 'G2'], emUso: false }, 201)
      },
    })
    renderApp('/cadastros/grades')
    await screen.findByText('PP ao GG')

    await userEvent.click(screen.getByRole('button', { name: 'Nova grade' }))
    const dialogo = screen.getByRole('dialog', { name: 'Nova grade' })
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Plus')
    await userEvent.type(within(dialogo).getByLabelText('Tamanhos (na ordem, separados por vírgula)'), 'G1, G2,G3 ')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toEqual({ nome: 'Plus', tamanhos: ['G1', 'G2', 'G3'] })
  })

  it('nova cor com amostra', async () => {
    let enviado: Record<string, unknown> | null = null
    api({
      'POST /api/cores': (init) => {
        enviado = corpo(init)
        return json({ id: 'c3', nome: 'Terracota', hex: '#C2410C' }, 201)
      },
    })
    renderApp('/cadastros/grades')
    await screen.findByText('#1D4ED8')

    await userEvent.click(screen.getByRole('button', { name: 'Nova cor' }))
    const dialogo = screen.getByRole('dialog', { name: 'Nova cor' })
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Terracota')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toMatchObject({ nome: 'Terracota' })
  })
})
