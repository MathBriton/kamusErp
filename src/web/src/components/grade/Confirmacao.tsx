import { useState, type ReactNode } from 'react'
import { ErroDeApi } from '../../auth/api'
import { Dialogo } from '../Dialogo'
import { estilos } from '../estilos'

/** Pergunta antes de uma ação (inativar, excluir...). Mostra o erro da API sem fechar. */
export function Confirmacao({ titulo, children, rotulo, onConfirmar, onCancelar }: {
  titulo: string
  children: ReactNode
  rotulo: string
  onConfirmar: () => Promise<void>
  onCancelar: () => void
}) {
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function confirmar() {
    setEnviando(true)
    setErro(null)
    try {
      await onConfirmar()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível concluir.')
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={titulo} onFechar={onCancelar}>
      <div className="space-y-4 text-sm">
        <div>{children}</div>
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onCancelar} className={estilos.botao}>Cancelar</button>
          <button type="button" disabled={enviando} onClick={() => void confirmar()} className={`${estilos.botaoPrimario} bg-danger`}>
            {enviando ? 'Aguarde...' : rotulo}
          </button>
        </div>
      </div>
    </Dialogo>
  )
}
