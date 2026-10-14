import { vi } from 'vitest'

type Handler = (init: RequestInit | undefined) => Response | Promise<Response>

/** Mock de fetch por "MÉTODO /caminho" (inclusive query string). Rotas não mapeadas respondem 404. */
export function mockApi(rotas: Record<string, Handler>) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.pathname : input.url
    const handler = rotas[`${(init?.method ?? 'GET').toUpperCase()} ${url}`]
    return handler ? handler(init) : new Response(null, { status: 404 })
  })
}

export const json = (corpo: unknown, status = 200) =>
  new Response(JSON.stringify(corpo), { status, headers: { 'Content-Type': 'application/json' } })

export const problema = (status: number, detail: string) =>
  new Response(JSON.stringify({ title: 'Erro', detail }), { status, headers: { 'Content-Type': 'application/problem+json' } })

export const corpo = (init: RequestInit | undefined) => JSON.parse(String(init?.body)) as Record<string, unknown>

export const sessaoAdmin = {
  token: 'token-admin',
  expiraEm: new Date(Date.now() + 8 * 3600_000).toISOString(),
  nome: 'Super Admin',
  email: 'superadmin@kamus.local',
}

export const TENANT = 'd0000000-0000-0000-0000-000000000001'

export const clienteDemo = {
  tenantId: TENANT, nome: 'Loja Demo', emailDoDono: 'admin@kamus.local', usuarios: 2, criadoEm: '2026-09-01T12:00:00Z',
  plano: 'Piloto', estado: 'Ativa', vencimento: '2026-11-07', fimDaCarencia: '2026-11-12', diasParaVencer: 24, valorMensal: 199.9,
  diasDeCarencia: 5, limiteDeCnpjs: null, limiteDeLojas: null, limiteDeUsuarios: null, ultimoPagamentoEm: '2026-10-07',
}

export const clienteSuspenso = {
  ...clienteDemo, tenantId: 'a0000000-0000-0000-0000-000000000002', nome: 'Estilo Urbano', emailDoDono: 'dono@estilo.com',
  estado: 'Suspensa', vencimento: '2026-09-30', fimDaCarencia: '2026-10-05', diasParaVencer: -14, ultimoPagamentoEm: '2026-08-30',
}

export const resumo = { ativas: 1, trial: 0, emCarencia: 0, suspensas: 1, canceladas: 0, receitaMensal: 199.9 }

export const acoes = [
  { id: 'a1', ocorridoEm: '2026-10-14T13:00:00Z', acao: 'login', descricao: 'Entrou no Painel Administrativo', administradorEmail: 'superadmin@kamus.local', tenantId: null, ip: '127.0.0.1' },
]
