import { useState, type FormEvent } from 'react'
import { chamar, ErroDeApi } from './api'
import { Campo } from './Campo'
import { Dialogo } from './Dialogo'
import { botao, botaoPrimario, campo } from './formatos'

interface Criado {
  tenantId: string
  emailDoDono: string
  senhaProvisoria: string
}

const VAZIO = { nome: '', razaoSocial: '', nomeFantasia: '', cnpj: '', nomeDoDono: '', emailDoDono: '', valorMensal: '199.9', diasDeTrial: '14' }

/** Cadastra cliente (empresa + Dono) com licença em avaliação e mostra a senha provisória uma única vez. */
export function NovoCliente({ token, onFechar, onCriado }: { token: string; onFechar: () => void; onCriado: () => void }) {
  const [dados, setDados] = useState(VAZIO)
  const [criado, setCriado] = useState<Criado | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const alterar = (campoAlterado: keyof typeof VAZIO) => (e: { target: { value: string } }) => setDados({ ...dados, [campoAlterado]: e.target.value })

  async function criar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setErro(null)
    try {
      setCriado(
        await chamar<Criado>('/admin-api/clientes', {
          metodo: 'POST',
          token,
          corpo: { ...dados, valorMensal: Number(dados.valorMensal), diasDeTrial: Number(dados.diasDeTrial) },
        }),
      )
      onCriado()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível criar o cliente.')
    } finally {
      setEnviando(false)
    }
  }

  if (criado)
    return (
      <Dialogo titulo="Cliente criado" onFechar={onFechar}>
        <p className="text-sm">Repasse ao cliente os dados de acesso do Dono:</p>
        <dl className="mt-3 space-y-2 rounded-xl border border-border bg-surface-2 p-4 text-sm">
          <div className="flex gap-2">
            <dt className="w-32 text-muted">E-mail</dt>
            <dd className="font-semibold">{criado.emailDoDono}</dd>
          </div>
          <div className="flex gap-2">
            <dt className="w-32 text-muted">Senha provisória</dt>
            <dd className="font-mono font-semibold select-all">{criado.senhaProvisoria}</dd>
          </div>
        </dl>
        <p className="mt-3 text-sm text-warning">Anote agora: a senha provisória não será mostrada de novo.</p>
        <div className="mt-5 flex justify-end">
          <button type="button" onClick={onFechar} className={botaoPrimario}>
            Concluir
          </button>
        </div>
      </Dialogo>
    )

  return (
    <Dialogo titulo="Novo cliente" onFechar={onFechar}>
      <form onSubmit={(e) => void criar(e)} className="space-y-3">
        <Campo rotulo="Nome do cliente">
          <input value={dados.nome} onChange={alterar('nome')} className={campo} />
        </Campo>
        <div className="grid gap-3 sm:grid-cols-2">
          <Campo rotulo="Razão social">
            <input value={dados.razaoSocial} onChange={alterar('razaoSocial')} className={campo} />
          </Campo>
          <Campo rotulo="Nome fantasia">
            <input value={dados.nomeFantasia} onChange={alterar('nomeFantasia')} className={campo} />
          </Campo>
          <Campo rotulo="CNPJ">
            <input value={dados.cnpj} onChange={alterar('cnpj')} inputMode="numeric" className={campo} />
          </Campo>
          <Campo rotulo="Nome do dono">
            <input value={dados.nomeDoDono} onChange={alterar('nomeDoDono')} className={campo} />
          </Campo>
        </div>
        <Campo rotulo="E-mail do dono">
          <input type="email" value={dados.emailDoDono} onChange={alterar('emailDoDono')} className={campo} />
        </Campo>
        <div className="grid grid-cols-2 gap-3">
          <Campo rotulo="Mensalidade (R$)">
            <input type="number" min="0" step="0.01" value={dados.valorMensal} onChange={alterar('valorMensal')} className={campo} />
          </Campo>
          <Campo rotulo="Dias de avaliação">
            <input type="number" min="0" max="90" value={dados.diasDeTrial} onChange={alterar('diasDeTrial')} className={campo} />
          </Campo>
        </div>
        {erro && (
          <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
            {erro}
          </p>
        )}
        <div className="flex justify-end gap-2 pt-2">
          <button type="button" onClick={onFechar} className={botao}>
            Cancelar
          </button>
          <button type="submit" disabled={enviando} className={botaoPrimario}>
            {enviando ? 'Criando...' : 'Criar cliente'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
