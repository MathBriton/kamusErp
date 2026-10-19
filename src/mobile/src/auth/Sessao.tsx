import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { API_URL } from '../config'
import type { Empresa, SessaoMobile } from '../api/tipos'
import { armazenamento } from './armazenamento'

/** Erro da API com a mensagem pronta para mostrar (o "detail" do ProblemDetails). */
export class ErroDeApi extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message)
  }
}

type Estado =
  | { status: 'carregando' }
  | { status: 'deslogado' }
  | { status: 'logado'; usuario: SessaoMobile['usuario']; empresas: Empresa[]; empresaId: string }

interface ContextoDaSessao {
  estado: Estado
  entrar: (email: string, senha: string) => Promise<string | null>
  sair: () => Promise<void>
  escolherEmpresa: (id: string) => void
  getJson: <T>(caminho: string) => Promise<T>
  enviar: (metodo: 'POST' | 'PUT' | 'DELETE', caminho: string, corpo?: unknown) => Promise<void>
}

const Contexto = createContext<ContextoDaSessao | null>(null)

export const TODAS = 'todas'

async function mensagemDeErro(resposta: Response, padrao: string) {
  try {
    const corpo = (await resposta.json()) as { detail?: string }
    return corpo.detail ?? padrao
  } catch {
    return padrao
  }
}

async function postarSessao(caminho: string, corpo: unknown) {
  return fetch(`${API_URL}${caminho}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) })
}

/**
 * Sessão do app: token de acesso em memória, token de sessão no armazenamento seguro.
 * Toda chamada leva o token e a empresa ativa; 401 renova a sessão uma vez e repete.
 */
export function SessaoProvider({ children }: { children: ReactNode }) {
  const [estado, setEstado] = useState<Estado>({ status: 'carregando' })
  const acesso = useRef<string | null>(null)
  const empresaAtiva = useRef('')
  const renovando = useRef<Promise<boolean> | null>(null)

  const aplicar = useCallback(async (s: SessaoMobile, manterEmpresa = false) => {
    acesso.current = s.accessToken
    await armazenamento.gravar(s.tokenDeSessao)
    const empresaId = manterEmpresa && empresaAtiva.current ? empresaAtiva.current : (s.empresas[0]?.id ?? '')
    empresaAtiva.current = empresaId
    setEstado({ status: 'logado', usuario: s.usuario, empresas: s.empresas, empresaId })
  }, [])

  const esquecer = useCallback(async () => {
    acesso.current = null
    empresaAtiva.current = ''
    await armazenamento.apagar()
    setEstado({ status: 'deslogado' })
  }, [])

  const renovar = useCallback(() => {
    renovando.current ??= (async () => {
      const token = await armazenamento.ler()
      const resposta = token ? await postarSessao('/api/auth/mobile/refresh', { tokenDeSessao: token }) : null
      if (!resposta?.ok) {
        await esquecer()
        return false
      }
      await aplicar((await resposta.json()) as SessaoMobile, true)
      return true
    })().finally(() => {
      renovando.current = null
    })
    return renovando.current
  }, [aplicar, esquecer])

  useEffect(() => {
    void renovar().catch(() => setEstado({ status: 'deslogado' }))
  }, [renovar])

  const chamar = useCallback(
    async (caminho: string, init: RequestInit = {}): Promise<Response> => {
      const executar = () =>
        fetch(`${API_URL}${caminho}`, {
          ...init,
          headers: { ...(init.headers as Record<string, string>), Authorization: `Bearer ${acesso.current ?? ''}`, 'X-Empresa-Id': empresaAtiva.current },
        })
      let resposta = await executar()
      if (resposta.status === 401 && (await renovar())) resposta = await executar()
      if (!resposta.ok) throw new ErroDeApi(await mensagemDeErro(resposta, 'Não foi possível falar com o servidor.'), resposta.status)
      return resposta
    },
    [renovar],
  )

  const valor = useMemo<ContextoDaSessao>(
    () => ({
      estado,
      async entrar(email, senha) {
        try {
          const resposta = await postarSessao('/api/auth/mobile/login', { email: email.trim(), senha })
          if (!resposta.ok) return await mensagemDeErro(resposta, 'Não foi possível entrar.')
          await aplicar((await resposta.json()) as SessaoMobile)
          return null
        } catch {
          return `Sem conexão com o servidor (${API_URL}).`
        }
      },
      async sair() {
        const token = await armazenamento.ler()
        if (token) await postarSessao('/api/auth/mobile/logout', { tokenDeSessao: token }).catch(() => null)
        await esquecer()
      },
      escolherEmpresa(id) {
        empresaAtiva.current = id
        setEstado((atual) => (atual.status === 'logado' ? { ...atual, empresaId: id } : atual))
      },
      async getJson<T>(caminho: string) {
        return (await (await chamar(caminho)).json()) as T
      },
      async enviar(metodo, caminho, corpo) {
        await chamar(caminho, {
          method: metodo,
          headers: corpo === undefined ? {} : { 'Content-Type': 'application/json' },
          body: corpo === undefined ? undefined : JSON.stringify(corpo),
        })
      },
    }),
    [estado, aplicar, esquecer, chamar],
  )

  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>
}

export function useSessao() {
  const contexto = useContext(Contexto)
  if (!contexto) throw new Error('useSessao fora do SessaoProvider')
  return contexto
}
