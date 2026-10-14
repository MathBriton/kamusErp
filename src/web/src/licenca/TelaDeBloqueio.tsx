import { useState, type ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext'
import { Icone } from '../components/Icone'
import { Logo } from '../components/Logo'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { estaBloqueada, formatarDia, formatarMoeda, useLicenca, type SituacaoDaLicenca } from './LicencaContext'

/** Ocupa a tela inteira quando a licença está suspensa ou cancelada. Os dados continuam preservados e exportáveis. */
export function TelaDeBloqueio({ situacao, email }: { situacao: SituacaoDaLicenca; email: string }) {
  const { atualizar } = useLicenca()
  const { sair } = useAuth()
  const { perfilAtual } = useEmpresa()
  const [verificando, setVerificando] = useState(false)
  const [aviso, setAviso] = useState<string | null>(null)
  const cancelada = situacao.estado === 'Cancelada'

  async function verificar() {
    setVerificando(true)
    setAviso(null)
    const nova = await atualizar()
    setVerificando(false)
    if (estaBloqueada(nova)) setAviso('O pagamento ainda não foi confirmado. Se você já pagou, fale com o suporte.')
  }

  const linhas: [string, ReactNode][] = [
    ['Plano', situacao.plano],
    ['Vencimento', formatarDia(situacao.vencimento)],
    ['Valor', `${formatarMoeda(situacao.valorMensal)} / mês`],
    ['Situação', <span className="rounded-full bg-danger-soft px-2 py-0.5 text-xs font-semibold text-danger">{cancelada ? 'Cancelada' : 'Suspensa'}</span>],
  ]

  return (
    <div className="relative min-h-dvh overflow-y-auto bg-background p-4">
      <header className="mx-auto flex max-w-5xl items-center justify-between py-3">
        <Logo />
        <div className="flex items-center gap-3">
          <span className="hidden text-sm text-muted sm:inline">{email}</span>
          <button type="button" onClick={() => void sair()} className="flex h-9 items-center gap-2 rounded-lg border border-border bg-surface px-3 text-sm font-semibold">
            <Icone nome="logOut" />
            Sair
          </button>
        </div>
      </header>

      <main className="mx-auto mt-8 max-w-xl rounded-2xl border border-border bg-surface p-6 shadow-lg sm:p-10">
        <div className="mb-5 grid size-14 place-items-center rounded-2xl bg-danger-soft text-danger">
          <Icone nome="lock" className="size-7" />
        </div>
        <h1 className="text-2xl font-bold tracking-tight">{cancelada ? 'Licença cancelada' : 'Acesso temporariamente suspenso'}</h1>
        <p className="mt-3 leading-relaxed text-muted">{situacao.mensagem}</p>

        <dl className="my-6 divide-y divide-border overflow-hidden rounded-xl border border-border">
          {linhas.map(([rotulo, valor]) => (
            <div key={rotulo} className="flex items-center px-4 py-2.5 text-sm">
              <dt className="flex-1 text-muted">{rotulo}</dt>
              <dd className="font-semibold tabular-nums">{valor}</dd>
            </div>
          ))}
        </dl>

        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => void verificar()}
            disabled={verificando}
            className="flex h-10 flex-1 items-center justify-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground disabled:opacity-60"
          >
            {verificando ? 'Verificando...' : 'Já paguei — verificar novamente'}
          </button>
          <a href="mailto:suporte@kamus.com.br" className="flex h-10 items-center gap-2 rounded-lg border border-border px-4 text-sm font-semibold">
            <Icone nome="mail" />
            Falar com o suporte
          </a>
        </div>
        {aviso && (
          <p role="alert" className="mt-4 rounded-lg bg-warning-soft px-4 py-3 text-sm text-warning">
            {aviso}
          </p>
        )}

        {(perfilAtual === 'Dono' || perfilAtual === 'Gerente') && (
          <div className="mt-6 flex flex-wrap items-center justify-between gap-3 border-t border-border pt-5 text-sm">
            <span className="text-muted">Exportar meus dados (histórico de alterações)</span>
            <BotaoExportar relatorio="historico-de-alteracoes" />
          </div>
        )}
      </main>
    </div>
  )
}
