import { API_URL } from '../config'

type Handler = (init: RequestInit | undefined) => Response | Promise<Response>

/** Mock de fetch por "MÉTODO /caminho?query" (sem o endereço da API). Rotas não mapeadas respondem 404. */
export function mockApi(rotas: Record<string, Handler>) {
  const chamadas: { rota: string; init: RequestInit | undefined }[] = []
  globalThis.fetch = jest.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input).replace(API_URL, '')
    const rota = `${(init?.method ?? 'GET').toUpperCase()} ${url}`
    chamadas.push({ rota, init })
    const handler = rotas[rota]
    return handler ? handler(init) : new Response(null, { status: 404 })
  }) as typeof fetch
  return chamadas
}

export const json = (corpo: unknown, status = 200) => new Response(JSON.stringify(corpo), { status, headers: { 'Content-Type': 'application/json' } })

export const problema = (status: number, detail: string) =>
  new Response(JSON.stringify({ title: 'Erro', detail }), { status, headers: { 'Content-Type': 'application/problem+json' } })

export const corpo = (init: RequestInit | undefined) => JSON.parse(String(init?.body)) as Record<string, unknown>

export const cabecalho = (init: RequestInit | undefined, nome: string) => new Headers(init?.headers).get(nome)

const empresas = [
  { id: 'e1', nomeFantasia: 'Kamus Moda Centro', razaoSocial: 'Kamus Moda Centro LTDA', cnpj: '12.345.678/0001-95', perfil: 'Dono' },
  { id: 'e2', nomeFantasia: 'Kamus Moda Shopping', razaoSocial: 'Kamus Moda Shopping LTDA', cnpj: '98.765.432/0001-98', perfil: 'Dono' },
]

export const sessao = (extra = {}) => ({
  accessToken: 'token-de-acesso',
  expiraEm: new Date(Date.now() + 15 * 60_000).toISOString(),
  usuario: { id: 'u1', nome: 'Admin Demo', email: 'admin@kamus.local', deveTrocarSenha: false },
  empresas,
  tokenDeSessao: 'sessao-1',
  sessaoExpiraEm: new Date(Date.now() + 30 * 86_400_000).toISOString(),
  ...extra,
})

const cartao = (extra = {}) => ({ total: 179.7, vendas: 2, pecas: 3, ticketMedio: 89.85, totalAnterior: 59.9, variacao: 200, ...extra })

export const dashboard = (extra = {}) => ({
  periodo: 'hoje',
  de: '2026-10-19',
  ate: '2026-10-19',
  vendas: {
    periodo: cartao(),
    mes: cartao({ total: 2390.5, vendas: 30, variacao: -12.5 }),
    ultimos30Dias: [],
    porForma: [{ nome: 'PIX', quantidade: 2, total: 179.7 }],
    porVendedor: [],
    maisVendidos: [{ skuId: 's1', descricao: 'Camiseta básica · Azul M', pecas: 2, total: 119.8 }],
    somenteAsMinhas: false,
  },
  financeiro: {
    contas: [{ id: 'c1', nome: 'Caixa da loja', tipo: 'Caixa', saldo: 339.6 }],
    saldoTotal: 339.6,
    aReceber: { hoje: 120, amanha: 0, proximosSeteDias: 600, vencido: 0, parcelasVencidas: 0 },
    aPagar: { hoje: 30, amanha: 0, proximosSeteDias: 30, vencido: 50, parcelasVencidas: 1 },
    crediarioEmAtraso: 0,
    clientesEmAtraso: 0,
  },
  estoque: null,
  condicionais: { abertas: 1, atrasadas: 1, pecas: 1, valor: 59.9 },
  alertas: [{ tipo: 'contas_a_pagar_vencidas', gravidade: 'perigo', titulo: '1 conta a pagar vencida', detalhe: 'R$ 50,00 em atraso', link: '/financeiro/contas-a-pagar' }],
  ...extra,
})
