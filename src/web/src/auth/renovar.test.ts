import { describe, expect, it } from 'vitest'
import { json, mockApi, sessaoDemo } from '../test/mockApi'
import { renovar } from './api'

describe('renovar sessão', () => {
  it('chamadas simultâneas usam uma única requisição (token de sessão é de uso único)', async () => {
    const api = mockApi({ 'POST /api/auth/refresh': () => json(sessaoDemo) })

    const [a, b] = await Promise.all([renovar(), renovar()])

    expect(a).toEqual(sessaoDemo)
    expect(b).toEqual(sessaoDemo)
    expect(api.mock.calls.filter(([url]) => url === '/api/auth/refresh')).toHaveLength(1)
  })

  it('depois de terminar, uma nova chamada faz nova requisição', async () => {
    const api = mockApi({ 'POST /api/auth/refresh': () => json(sessaoDemo) })

    await renovar()
    await renovar()

    expect(api.mock.calls.filter(([url]) => url === '/api/auth/refresh')).toHaveLength(2)
  })
})
