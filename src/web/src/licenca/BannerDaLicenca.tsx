import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Icone } from '../components/Icone'
import { formatarDia, useLicenca } from './LicencaContext'

const AVISAR_COM_DIAS = 7

/** Faixa no topo do conteúdo: carência (âmbar) ou vencimento próximo (azul). Nada quando está tudo em dia. */
export function BannerDaLicenca() {
  const { situacao } = useLicenca()
  if (!situacao) return null

  let texto: ReactNode = null
  let tom = 'border-info bg-info-soft text-info'
  if (situacao.estado === 'Carencia') {
    tom = 'border-warning bg-warning-soft text-warning'
    texto = (
      <>
        <b>Mensalidade vencida em {formatarDia(situacao.vencimento)}.</b> Você está no período de carência — regularize até{' '}
        <b>{formatarDia(situacao.fimDaCarencia)}</b> para evitar a suspensão do acesso.
      </>
    )
  } else if ((situacao.estado === 'Ativa' || situacao.estado === 'Trial') && situacao.diasParaVencer !== null && situacao.diasParaVencer <= AVISAR_COM_DIAS) {
    const oque = situacao.estado === 'Trial' ? 'Seu período de avaliação' : 'Sua licença'
    const quando = situacao.diasParaVencer === 0 ? 'vence hoje' : situacao.diasParaVencer === 1 ? 'vence amanhã' : `vence em ${situacao.diasParaVencer} dias`
    texto = (
      <>
        <b>
          {oque} {quando}
        </b>{' '}
        ({formatarDia(situacao.vencimento)}).
      </>
    )
  }
  if (!texto) return null

  return (
    <div role="status" aria-label="Situação da licença" className={`mb-5 flex flex-wrap items-center gap-3 rounded-xl border px-4 py-3 text-sm ${tom}`}>
      <Icone nome="alert" className="size-5" />
      <span className="min-w-60 flex-1 text-foreground">{texto}</span>
      <Link to="/configuracoes/licenca" className="rounded-lg border border-current px-3 py-1.5 font-semibold">
        Ver detalhes
      </Link>
    </div>
  )
}
