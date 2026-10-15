import { useState } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { estilos } from '../components/estilos'
import { salvarArquivo } from '../relatorios/salvarArquivo'

/** Baixa as etiquetas (uma por peça) de uma operação de entrada: folha A4 ou impressora térmica. */
export function BotaoEtiquetas({ operacaoId }: { operacaoId: string }) {
  const { baixar } = useApi()
  const [erro, setErro] = useState<string | null>(null)

  async function gerar(formato: 'a4' | 'termica') {
    setErro(null)
    try {
      const r = await baixar(`/api/estoque/etiquetas/operacao/${operacaoId}?formato=${formato}`)
      if (r.tipo === 'arquivo') salvarArquivo(r.blob, r.nome)
    } catch (e) {
      setErro(e instanceof ErroDeApi ? e.message : 'Não foi possível gerar as etiquetas.')
    }
  }

  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      <button type="button" onClick={() => void gerar('a4')} className={estilos.botao}>Etiquetas (A4)</button>
      <button type="button" onClick={() => void gerar('termica')} className={estilos.botao}>Etiquetas (térmica)</button>
      {erro && <span role="alert" className="text-sm text-danger">{erro}</span>}
    </span>
  )
}
