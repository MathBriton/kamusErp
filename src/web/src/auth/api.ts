export interface Usuario {
  id: string
  nome: string
  email: string
  /** Senha provisória: o sistema só libera depois de trocar. */
  deveTrocarSenha?: boolean
}

export interface Empresa {
  id: string
  nomeFantasia: string
  razaoSocial: string
  cnpj: string
  perfil: string
}

export interface Sessao {
  accessToken: string
  expiraEm: string
  usuario: Usuario
  empresas: Empresa[]
}

/** Erro com mensagem pronta para mostrar ao usuário. */
export class ErroDeApi extends Error {
  readonly status: number
  /** Código estável enviado pela API em alguns erros (ex.: LICENCA_SUSPENSA). */
  readonly codigo: string | null

  constructor(status: number, mensagem: string, codigo: string | null = null) {
    super(mensagem)
    this.status = status
    this.codigo = codigo
  }
}

/** Disparado quando qualquer chamada recebe 402 (licença suspensa/cancelada): a tela de bloqueio aparece na hora. */
export const EVENTO_LICENCA_BLOQUEADA = 'kamus:licenca-bloqueada'

const SEM_CONEXAO = 'Não foi possível conectar à API. Verifique se ela está rodando em http://localhost:5249.'

export async function chamar(caminho: string, init: RequestInit = {}): Promise<Response> {
  let resposta: Response
  try {
    // credentials: o cookie de sessão (HttpOnly) vai junto, mas o JavaScript nunca o lê.
    resposta = await fetch(caminho, { credentials: 'same-origin', ...init })
  } catch {
    throw new ErroDeApi(0, SEM_CONEXAO)
  }

  if (!resposta.ok) {
    let mensagem = 'Algo deu errado. Tente novamente.'
    let codigo: string | null = null
    try {
      const problema = (await resposta.json()) as { detail?: string; title?: string; codigo?: string }
      mensagem = problema.detail ?? problema.title ?? mensagem
      codigo = problema.codigo ?? null
    } catch {
      if (resposta.status === 429) mensagem = 'Muitas tentativas. Aguarde um minuto e tente novamente.'
    }
    if (resposta.status === 402) window.dispatchEvent(new Event(EVENTO_LICENCA_BLOQUEADA))
    throw new ErroDeApi(resposta.status, mensagem, codigo)
  }
  return resposta
}

const postJson = (caminho: string, corpo?: unknown) =>
  chamar(caminho, {
    method: 'POST',
    headers: corpo === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: corpo === undefined ? undefined : JSON.stringify(corpo),
  })

export async function entrar(email: string, senha: string, manterConectado: boolean): Promise<Sessao> {
  return (await postJson('/api/auth/login', { email, senha, manterConectado })).json()
}

/** Restaura/renova a sessão pelo cookie. Retorna null quando não há sessão válida. */
export async function renovar(): Promise<Sessao | null> {
  try {
    return await (await postJson('/api/auth/refresh')).json()
  } catch (erro) {
    if (erro instanceof ErroDeApi && erro.status !== 0) return null
    throw erro
  }
}

export async function sair(): Promise<void> {
  await postJson('/api/auth/logout')
}

export async function trocarSenha(token: string, senhaAtual: string, novaSenha: string): Promise<void> {
  await chamar('/api/auth/trocar-senha', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ senhaAtual, novaSenha }),
  })
}
