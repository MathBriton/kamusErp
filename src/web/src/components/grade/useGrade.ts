import { useEffect, useState } from 'react'
import { useApi } from '../../api/useApi'
import { ErroDeApi } from '../../auth/api'

/**
 * Estado padrão de uma tela em grade: filtros, página, tamanho e linhas selecionadas.
 * `padrao` deve ser uma constante do módulo; a ordem das chaves é a ordem dos parâmetros na URL.
 * Filtro novo volta à página 1 e limpa a seleção (as linhas marcadas podem nem aparecer mais).
 */
export function useGrade<F extends Record<string, string>>(padrao: F, tamanhoInicial = 20) {
  const [filtros, setFiltros] = useState<F>(padrao)
  const [pagina, setPagina] = useState(1)
  const [tamanho, setTamanho] = useState(tamanhoInicial)
  const [selecionados, setSelecionados] = useState<Set<string>>(new Set())

  const recomecar = () => {
    setPagina(1)
    setSelecionados(new Set())
  }
  const filtrar = (mudanca: Partial<F>) => {
    setFiltros((atual) => ({ ...atual, ...mudanca }))
    recomecar()
  }
  const limpar = () => {
    setFiltros(padrao)
    recomecar()
  }

  /** Filtros preenchidos (vão para a API e para o "Exportar"). */
  const parametros: Record<string, string> = Object.fromEntries(
    Object.entries(filtros)
      .map(([chave, valor]) => [chave, valor.trim()])
      .filter(([, valor]) => valor !== ''),
  )
  const consulta = new URLSearchParams({ ...parametros, pagina: String(pagina), tamanho: String(tamanho) })

  return {
    filtros,
    filtrar,
    limpar,
    ativos: Object.keys(padrao).some((chave) => (filtros[chave] ?? '').trim() !== padrao[chave]),
    parametros,
    consulta,
    selecionados,
    limparSelecao: () => setSelecionados(new Set()),
    /** Espalhe em <GradeDeRegistros {...grade.paginacao} />. */
    paginacao: {
      pagina,
      tamanho,
      onPagina: setPagina,
      onTamanho: (t: number) => {
        setTamanho(t)
        setPagina(1)
      },
    },
    selecao: { selecionados, onMudar: setSelecionados },
  }
}

/**
 * Busca uma página da API (`{ itens, total, ... }`), cancelando a anterior. `atraso` (ms) segura a busca enquanto
 * o usuário digita. Mude `versao` para recarregar depois de criar, editar ou inativar.
 */
export function useListaPaginada<R extends { itens: unknown[]; total: number }>(url: string | null, versao = 0, atraso = 0) {
  const { getJson } = useApi()
  const [estado, setEstado] = useState<{ dados: R | null; erro: string | null }>({ dados: null, erro: null })

  useEffect(() => {
    if (!url) return
    const controle = new AbortController()
    const timer = window.setTimeout(() => {
      getJson<R>(url, controle.signal)
        .then((dados) => setEstado({ dados, erro: null }))
        .catch((e: unknown) => {
          if (!controle.signal.aborted) setEstado({ dados: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar.' })
        })
    }, atraso)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, url, versao, atraso])

  return estado
}
