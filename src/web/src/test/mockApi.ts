import { vi } from 'vitest'

type Handler = (init: RequestInit | undefined) => Response | Promise<Response>

/** Mock de fetch por "MÉTODO /caminho". Rotas não mapeadas respondem 404. */
export function mockApi(rotas: Record<string, Handler>) {
  const spy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.pathname : input.url
    const metodo = (init?.method ?? 'GET').toUpperCase()
    const handler = rotas[`${metodo} ${url}`]
    return handler ? handler(init) : new Response(null, { status: 404 })
  })
  return spy
}

export const json = (corpo: unknown, status = 200) =>
  new Response(JSON.stringify(corpo), { status, headers: { 'Content-Type': 'application/json' } })

export const problema = (status: number, detail: string) =>
  new Response(JSON.stringify({ title: 'Erro', detail }), { status, headers: { 'Content-Type': 'application/problem+json' } })

export const sessaoDemo = {
  accessToken: 'token-de-teste',
  expiraEm: new Date(Date.now() + 15 * 60_000).toISOString(),
  usuario: { id: 'u1', nome: 'Admin Demo', email: 'admin@kamus.local' },
  empresas: [
    { id: 'e1', nomeFantasia: 'Kamus Moda Centro', razaoSocial: 'Kamus Moda Centro LTDA', cnpj: '12.345.678/0001-95', perfil: 'Dono' },
    { id: 'e2', nomeFantasia: 'Kamus Moda Shopping', razaoSocial: 'Kamus Moda Shopping LTDA', cnpj: '98.765.432/0001-98', perfil: 'Dono' },
  ],
}

export const sessaoVendedor = {
  ...sessaoDemo,
  usuario: { id: 'u2', nome: 'Vendedora Demo', email: 'vendedor@kamus.local' },
  empresas: [{ ...sessaoDemo.empresas[0]!, perfil: 'Vendedor' }],
}

/** Responde GET /api/empresas/ativa conforme o header X-Empresa-Id enviado. */
export function empresaAtivaHandler(sessao = sessaoDemo) {
  return (init: RequestInit | undefined) => {
    const valor = new Headers(init?.headers).get('X-Empresa-Id')
    const resumo = (e: (typeof sessaoDemo.empresas)[number]) => ({ id: e.id, nomeFantasia: e.nomeFantasia, razaoSocial: e.razaoSocial, cnpj: e.cnpj })
    if (valor === 'todas') return json({ consolidado: true, empresas: sessao.empresas.map(resumo) })
    const empresa = sessao.empresas.find((e) => e.id === valor)
    return empresa ? json({ consolidado: false, empresas: [resumo(empresa)] }) : problema(403, 'Você não tem acesso a esta empresa.')
  }
}
