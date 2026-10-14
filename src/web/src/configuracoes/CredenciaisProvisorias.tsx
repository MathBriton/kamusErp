import { estilos } from '../components/estilos'

/** Mostra e-mail e senha provisória uma única vez, para repassar à pessoa. */
export function CredenciaisProvisorias({ email, senha, onConcluir }: { email: string; senha: string; onConcluir: () => void }) {
  return (
    <div>
      <p className="text-sm">Repasse os dados de acesso. No primeiro login a pessoa cria uma senha própria.</p>
      <dl className="mt-3 space-y-2 rounded-xl border border-border bg-surface-2 p-4 text-sm">
        <div className="flex gap-2">
          <dt className="w-36 text-muted">E-mail</dt>
          <dd className="font-semibold">{email}</dd>
        </div>
        <div className="flex gap-2">
          <dt className="w-36 text-muted">Senha provisória</dt>
          <dd className="font-mono font-semibold select-all">{senha}</dd>
        </div>
      </dl>
      <p className="mt-3 text-sm text-warning">Anote agora: a senha provisória não será mostrada de novo.</p>
      <div className="mt-5 flex justify-end">
        <button type="button" onClick={onConcluir} className={estilos.botaoPrimario}>
          Concluir
        </button>
      </div>
    </div>
  )
}
