export interface Sessao {
  token: string
  expiraEm: string
  nome: string
  email: string
}

export type Estado = 'Trial' | 'Ativa' | 'Carencia' | 'Suspensa' | 'Cancelada' | 'SemLicenca'

export interface Cliente {
  tenantId: string
  nome: string
  emailDoDono: string | null
  usuarios: number
  criadoEm: string
  plano: string
  estado: Estado
  vencimento: string | null
  fimDaCarencia: string | null
  diasParaVencer: number | null
  valorMensal: number
  diasDeCarencia: number
  limiteDeCnpjs: number | null
  limiteDeLojas: number | null
  limiteDeUsuarios: number | null
  ultimoPagamentoEm: string | null
}

export interface Resumo {
  ativas: number
  trial: number
  emCarencia: number
  suspensas: number
  canceladas: number
  receitaMensal: number
}

export interface Pagamento {
  id: string
  valor: number
  meses: number
  pagoEm: string
  estendeuAte: string
  forma: string | null
  observacao: string | null
  registradoPor: string
  registradoEm: string
}

export interface Acao {
  id: string
  ocorridoEm: string
  acao: string
  descricao: string
  administradorEmail: string
  tenantId: string | null
  ip: string | null
}

export interface Detalhe {
  cliente: Cliente
  pagamentos: Pagamento[]
  acoes: Acao[]
}

/** Erro com mensagem pronta para mostrar. */
export class ErroDeApi extends Error {
  readonly status: number

  constructor(status: number, mensagem: string) {
    super(mensagem)
    this.status = status
  }
}

const CHAVE = 'kamus.admin.sessao'
export const EVENTO_SESSAO_EXPIRADA = 'kamus-admin:sessao-expirada'

/** Sessão na aba (sessionStorage): some ao fechar o navegador. O token expira em poucas horas. */
export function lerSessao(): Sessao | null {
  try {
    const sessao = JSON.parse(sessionStorage.getItem(CHAVE) ?? 'null') as Sessao | null
    return sessao && new Date(sessao.expiraEm) > new Date() ? sessao : null
  } catch {
    return null
  }
}

export function guardarSessao(sessao: Sessao | null) {
  try {
    if (sessao) sessionStorage.setItem(CHAVE, JSON.stringify(sessao))
    else sessionStorage.removeItem(CHAVE)
  } catch {
    // sem armazenamento: a sessão vale só enquanto a página estiver aberta
  }
}

/** Chama a API do painel. Corpo em JSON; 204 devolve null; 401 encerra a sessão. */
export async function chamar<T>(caminho: string, opcoes: { metodo?: string; corpo?: unknown; token?: string } = {}): Promise<T> {
  const headers: Record<string, string> = {}
  if (opcoes.token) headers.Authorization = `Bearer ${opcoes.token}`
  if (opcoes.corpo !== undefined) headers['Content-Type'] = 'application/json'

  let resposta: Response
  try {
    resposta = await fetch(caminho, {
      method: opcoes.metodo ?? 'GET',
      headers,
      body: opcoes.corpo === undefined ? undefined : JSON.stringify(opcoes.corpo),
    })
  } catch {
    throw new ErroDeApi(0, 'Não foi possível conectar à API. Verifique se ela está rodando em http://localhost:5249.')
  }

  if (!resposta.ok) {
    let mensagem = resposta.status === 404 ? 'Painel indisponível (Admin:Chave não configurada ou IP não permitido).' : 'Algo deu errado. Tente novamente.'
    try {
      const problema = (await resposta.json()) as { detail?: string; title?: string }
      mensagem = problema.detail ?? problema.title ?? mensagem
    } catch {
      if (resposta.status === 429) mensagem = 'Muitas tentativas. Aguarde um minuto e tente novamente.'
    }
    if (resposta.status === 401 && opcoes.token) window.dispatchEvent(new Event(EVENTO_SESSAO_EXPIRADA))
    throw new ErroDeApi(resposta.status, mensagem)
  }
  return (resposta.status === 204 ? null : await resposta.json()) as T
}
