import { useState, type FormEvent } from 'react'
import { chamar, ErroDeApi, type Cliente } from './api'
import { Campo } from './Campo'
import { Dialogo } from './Dialogo'
import { botao, botaoPrimario, campo, dia } from './formatos'

const FORMAS = ['Pix', 'Boleto', 'Cartão', 'Transferência', 'Dinheiro']

/** Registra a mensalidade: a API libera o cliente na hora e avisa os donos. */
export function RegistrarPagamento({ cliente, token, onFechar, onRegistrado }: {
  cliente: Cliente
  token: string
  onFechar: () => void
  onRegistrado: (mensagem: string) => void
}) {
  const [valor, setValor] = useState(String(cliente.valorMensal))
  const [meses, setMeses] = useState('1')
  const [forma, setForma] = useState('')
  const [observacao, setObservacao] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function registrar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setErro(null)
    try {
      const situacao = await chamar<{ vencimento: string | null }>(`/admin-api/clientes/${cliente.tenantId}/pagamentos`, {
        metodo: 'POST',
        token,
        corpo: { valor: Number(valor), meses: Number(meses), forma: forma || null, observacao: observacao || null },
      })
      onRegistrado(`Pagamento registrado: ${cliente.nome} em dia até ${dia(situacao.vencimento)}.`)
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível registrar.')
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={`Registrar pagamento — ${cliente.nome}`} onFechar={onFechar}>
      <form onSubmit={(e) => void registrar(e)} className="space-y-4">
        <p className="text-sm text-muted">
          Vencimento atual: <b className="text-foreground">{dia(cliente.vencimento)}</b>. Em dia ou na carência, os meses contam a partir do
          vencimento; suspenso, a partir de hoje.
        </p>
        <div className="grid grid-cols-2 gap-3">
          <Campo rotulo="Valor (R$)">
            <input type="number" min="0.01" step="0.01" required value={valor} onChange={(e) => setValor(e.target.value)} className={campo} />
          </Campo>
          <Campo rotulo="Meses">
            <input type="number" min="1" max="12" required value={meses} onChange={(e) => setMeses(e.target.value)} className={campo} />
          </Campo>
        </div>
        <Campo rotulo="Forma">
          <select value={forma} onChange={(e) => setForma(e.target.value)} className={campo}>
            <option value="">Não informar</option>
            {FORMAS.map((f) => (
              <option key={f}>{f}</option>
            ))}
          </select>
        </Campo>
        <Campo rotulo="Observação">
          <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={500} className={campo} />
        </Campo>
        {erro && (
          <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
            {erro}
          </p>
        )}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={botao}>
            Cancelar
          </button>
          <button type="submit" disabled={enviando} className={botaoPrimario}>
            {enviando ? 'Registrando...' : 'Registrar e liberar'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
