import { useCallback } from 'react'

export type ResultadoDownload = { tipo: 'arquivo'; nome: string; blob: Blob } | { tipo: 'agendado'; mensagem: string }

/** Nome do arquivo no Content-Disposition (prefere o filename* em UTF-8). */
function nomeDoArquivo(disposicao: string | null, padrao: string) {
  const utf8 = disposicao?.match(/filename\*=UTF-8''([^;]+)/i)?.[1]
  if (utf8) return decodeURIComponent(utf8)
  return disposicao?.match(/filename="?([^";]+)"?/i)?.[1] ?? padrao
}
import { chamar } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { useEmpresa } from '../empresa/EmpresaContext'

/** Cliente da API para telas autenticadas: envia o token e a empresa ativa (X-Empresa-Id). */
export function useApi() {
  const { estado } = useAuth()
  const { selecao } = useEmpresa()
  const token = estado.status === 'autenticado' ? estado.sessao.accessToken : ''

  const getJson = useCallback(
    async <T,>(caminho: string, signal?: AbortSignal): Promise<T> => {
      const resposta = await chamar(caminho, {
        signal,
        headers: { Authorization: `Bearer ${token}`, 'X-Empresa-Id': selecao },
      })
      return (await resposta.json()) as T
    },
    [token, selecao],
  )

  const post = useCallback(
    async (caminho: string, signal?: AbortSignal): Promise<void> => {
      await chamar(caminho, {
        method: 'POST',
        signal,
        headers: { Authorization: `Bearer ${token}`, 'X-Empresa-Id': selecao },
      })
    },
    [token, selecao],
  )

  /** Exportações: 200 = arquivo para baixar; 202 = gerado em segundo plano (aviso no sininho). */
  const baixar = useCallback(
    async (caminho: string): Promise<ResultadoDownload> => {
      const resposta = await chamar(caminho, { headers: { Authorization: `Bearer ${token}`, 'X-Empresa-Id': selecao } })
      if (resposta.status === 202) {
        const corpo = (await resposta.json()) as { mensagem: string }
        return { tipo: 'agendado', mensagem: corpo.mensagem }
      }
      const padrao = caminho.split('?')[0]!.split('/').at(-1)!
      return { tipo: 'arquivo', nome: nomeDoArquivo(resposta.headers.get('Content-Disposition'), padrao), blob: await resposta.blob() }
    },
    [token, selecao],
  )

  return { getJson, post, baixar }
}
