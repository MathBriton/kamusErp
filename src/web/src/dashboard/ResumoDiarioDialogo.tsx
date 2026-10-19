import { useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import type { PreferenciasDoResumo, PreviaDoResumo } from './tipos'

const DIAS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb']

const BLOCOS: { campo: 'vendas' | 'financeiro' | 'estoque' | 'condicionais' | 'crediario'; rotulo: string }[] = [
  { campo: 'vendas', rotulo: 'Vendas (total, mais vendidos e formas de pagamento)' },
  { campo: 'financeiro', rotulo: 'Financeiro (contas de amanhã e vencidas)' },
  { campo: 'crediario', rotulo: 'Crediário em atraso' },
  { campo: 'condicionais', rotulo: 'Condicionais na rua e atrasadas' },
  { campo: 'estoque', rotulo: 'Estoque (quebra de grade e negativos)' },
]

/** Preferências do resumo diário do usuário logado, prévia de hoje e envio de teste para o sininho. */
export function ResumoDiarioDialogo({ onFechar }: { onFechar: () => void }) {
  const { getJson, enviar, post } = useApi()
  const [p, setP] = useState<PreferenciasDoResumo | null>(null)
  const [previa, setPrevia] = useState<PreviaDoResumo | null>(null)
  const [mensagem, setMensagem] = useState<string | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<PreferenciasDoResumo>('/api/indicadores/resumo-diario', controle.signal)
      .then(setP)
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setErro(e instanceof ErroDeApi ? e.message : 'Não foi possível carregar as preferências.')
      })
    return () => controle.abort()
  }, [getJson])

  const mudar = (mudanca: Partial<PreferenciasDoResumo>) => {
    setMensagem(null)
    setP((atual) => (atual ? { ...atual, ...mudanca } : atual))
  }

  async function salvar(e: FormEvent) {
    e.preventDefault()
    if (!p) return
    setErro(null)
    try {
      const corpo = { ...p, ultimoEnvio: undefined }
      setP(await enviar<PreferenciasDoResumo>('PUT', '/api/indicadores/resumo-diario', corpo))
      setMensagem('Preferências salvas.')
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.')
    }
  }

  async function verPrevia() {
    setErro(null)
    try {
      setPrevia(await getJson<PreviaDoResumo>('/api/indicadores/resumo-diario/previa'))
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível montar a prévia.')
    }
  }

  async function enviarAgora() {
    setErro(null)
    try {
      await post('/api/indicadores/resumo-diario/enviar')
      setMensagem('Enviado. Confira o sininho.')
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível enviar.')
    }
  }

  return (
    <Dialogo titulo="Resumo diário" onFechar={onFechar} largo>
      {!p && !erro && <p className="text-sm text-muted">Carregando...</p>}
      {p && (
        <form onSubmit={salvar} className="space-y-4 text-sm">
          <p className="text-muted">
            Um resumo do dia chega no sininho no horário escolhido (horário de Brasília). Push no celular e e-mail virão com o app e o servidor.
          </p>
          <label className="flex items-center gap-3">
            <input type="checkbox" checked={p.ativo} onChange={(e) => mudar({ ativo: e.target.checked })} className="h-4 w-4" />
            <span className="font-semibold">Receber o resumo diário</span>
          </label>
          <Campo rotulo="Horário" className="max-w-40">
            <select value={String(p.hora)} onChange={(e) => mudar({ hora: Number(e.target.value) })} className={estilos.campo}>
              {Array.from({ length: 24 }, (_, h) => (
                <option key={h} value={h}>{`${h}h`}</option>
              ))}
            </select>
          </Campo>
          <fieldset>
            <legend className="mb-1 font-semibold">Dias da semana</legend>
            <div className="flex flex-wrap gap-3">
              {DIAS.map((rotulo, dia) => (
                <label key={rotulo} className="flex items-center gap-1.5">
                  <input
                    type="checkbox"
                    checked={p.dias.includes(dia)}
                    onChange={(e) => mudar({ dias: e.target.checked ? [...p.dias, dia].sort((a, b) => a - b) : p.dias.filter((d) => d !== dia) })}
                    className="h-4 w-4"
                  />
                  {rotulo}
                </label>
              ))}
            </div>
          </fieldset>
          <fieldset className="space-y-1.5">
            <legend className="mb-1 font-semibold">O que entra</legend>
            {BLOCOS.map((b) => (
              <label key={b.campo} className="flex items-center gap-2">
                <input type="checkbox" checked={p[b.campo]} onChange={(e) => mudar({ [b.campo]: e.target.checked })} className="h-4 w-4" />
                {b.rotulo}
              </label>
            ))}
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={p.enviarSemMovimento} onChange={(e) => mudar({ enviarSemMovimento: e.target.checked })} className="h-4 w-4" />
              Enviar mesmo sem movimento no dia
            </label>
          </fieldset>

          {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
          {mensagem && <p role="status" className="text-success">{mensagem}</p>}

          <div className="flex flex-wrap justify-end gap-2">
            <button type="button" onClick={() => void verPrevia()} className={estilos.botao}>Ver prévia de hoje</button>
            <button type="button" onClick={() => void enviarAgora()} className={estilos.botao}>Enviar agora para o sininho</button>
            <button type="submit" className={estilos.botaoPrimario}>Salvar</button>
          </div>

          {previa && (
            <section aria-label="Prévia do resumo" className="rounded-xl bg-surface-2 p-4">
              <h3 className="font-semibold">{previa.titulo}</h3>
              {previa.linhas.length === 0 ? (
                <p className="mt-1 text-muted">Nada para resumir hoje.</p>
              ) : (
                <ul className="mt-1 space-y-0.5">
                  {previa.linhas.map((l) => (
                    <li key={l}>{l}</li>
                  ))}
                </ul>
              )}
              {!previa.temMovimento && <p className="mt-2 text-xs text-muted">Sem movimento: só chega se "enviar mesmo sem movimento" estiver marcado.</p>}
            </section>
          )}
        </form>
      )}
      {!p && erro && <p role="alert" className={estilos.erro}>{erro}</p>}
    </Dialogo>
  )
}
