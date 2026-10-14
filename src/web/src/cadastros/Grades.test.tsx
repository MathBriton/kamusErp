import { screen, within } from '@testing-library/react'
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

    const grade = (await screen.findByText('PP ao GG')).closest('li')!
    expect(within(grade).getByText('PP · P · M · G · GG')).toBeInTheDocument()
    expect(within(grade).getByText('Em uso')).toBeInTheDocument()
    expect(within(grade).queryByRole('button', { name: 'Editar' })).not.toBeInTheDocument()
    expect(screen.getByText('Azul')).toBeInTheDocument()
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
    await screen.findByText('Azul')

    await userEvent.click(screen.getByRole('button', { name: 'Nova cor' }))
    const dialogo = screen.getByRole('dialog', { name: 'Nova cor' })
    await userEvent.type(within(dialogo).getByLabelText('Nome'), 'Terracota')
    await userEvent.click(within(dialogo).getByRole('button', { name: 'Salvar' }))

    expect(enviado).toMatchObject({ nome: 'Terracota' })
  })
})
