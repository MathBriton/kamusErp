import { SITUACAO, type Situacao } from './tipos'

export function SeloDeSituacao({ situacao }: { situacao: Situacao }) {
  return <span className={`rounded-full px-2 py-0.5 text-xs font-semibold whitespace-nowrap ${SITUACAO[situacao].classe}`}>{SITUACAO[situacao].rotulo}</span>
}
