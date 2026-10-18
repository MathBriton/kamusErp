import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { dataBr, hojeIso, somarDias } from '../financeiro/tipos'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { inicioDoMes, PERFIS_DE_GESTAO, PERFIS_QUE_CONSULTAM, type ComissaoDoVendedor, type Fechamento } from './tipos'

/** Comissão por vendedor no período (vendas concluídas, ajustada pelas trocas), fechamento com conta a pagar e reabertura. */
export function ComissoesPage() {
  const { getJson } = useApi()
  const { perfilAtual } = useEmpresa()
  const [de, setDe] = useState(() => inicioDoMes(hojeIso()))
  const [ate, setAte] = useState(hojeIso())
  const [{ linhas, erro }, setEstado] = useState<{ linhas: ComissaoDoVendedor[] | null; erro: string | null }>({ linhas: null, erro: null })
  const [versao, setVersao] = useState(0)
  const [fechando, setFechando] = useState<ComissaoDoVendedor | null>(null)
  const gestao = PERFIS_DE_GESTAO.includes(perfilAtual)
  const permitido = PERFIS_QUE_CONSULTAM.includes(perfilAtual)
  const filtros = useMemo(() => ({ de, ate }), [de, ate])

  useEffect(() => {
    if (!permitido || !de || !ate) return
    const controle = new AbortController()
    getJson<ComissaoDoVendedor[]>(`/api/vendas/comissoes?${new URLSearchParams(filtros)}`, controle.signal)
      .then((l) => setEstado({ linhas: l, erro: null }))
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setEstado({ linhas: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar as comissões.' })
      })
    return () => controle.abort()
  }, [getJson, filtros, permitido, de, ate, versao])

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Comissões</h1>
        <p className={`mt-4 ${estilos.erro}`}>Só Dono, Gerente e Financeiro veem as comissões.</p>
      </div>
    )

  const soma = (f: (c: ComissaoDoVendedor) => number) => (linhas ?? []).reduce((s, c) => s + f(c), 0)

  return (
    <div className="mx-auto max-w-4xl">
      <p className="text-xs text-muted">Vendas</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Comissões</h1>
          <p className="mt-1 text-sm text-muted">
            Percentual do vendedor sobre o total de cada venda concluída; trocas ajustam pela diferença e canceladas não contam. Feche o período para gerar a conta a pagar.
          </p>
        </div>
        <BotaoExportar relatorio="comissoes" parametros={filtros} />
      </div>

      <div className="mt-6 grid max-w-md grid-cols-2 gap-3">
        <Campo rotulo="De">
          <input type="date" value={de} onChange={(e) => setDe(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Até">
          <input type="date" value={ate} onChange={(e) => setAte(e.target.value)} className={estilos.campo} />
        </Campo>
      </div>

      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {linhas && (
        <div className="mt-6 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          {linhas.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted">Nenhuma venda no período.</p>
          ) : (
            <table aria-label="Comissões" className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-4 py-2">Vendedor</th>
                  <th className="px-4 py-2 text-right">Vendas</th>
                  <th className="px-4 py-2 text-right">Total vendido</th>
                  <th className="px-4 py-2 text-right">Comissão</th>
                  <th className="px-4 py-2 text-right">A fechar</th>
                  <th />
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {linhas.map((c) => (
                  <tr key={c.vendedorId ?? 'sem'}>
                    <td className={`px-4 py-2 font-semibold ${c.vendedorId ? '' : 'text-muted'}`}>{c.vendedor}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{c.vendas}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{moeda(c.total)}</td>
                    <td className="px-4 py-2 text-right font-semibold tabular-nums">{moeda(c.comissao)}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{moeda(c.comissaoPendente)}</td>
                    <td className="px-4 py-2 text-right">
                      {gestao && c.vendedorId && c.comissaoPendente > 0 && (
                        <button type="button" onClick={() => setFechando(c)} className={estilos.botao} aria-label={`Fechar comissão de ${c.vendedor}`}>
                          Fechar
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot className="bg-surface-2 font-semibold">
                <tr>
                  <td className="px-4 py-2">Total</td>
                  <td className="px-4 py-2 text-right tabular-nums">{soma((c) => c.vendas)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{moeda(soma((c) => c.total))}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{moeda(soma((c) => c.comissao))}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{moeda(soma((c) => c.comissaoPendente))}</td>
                  <td />
                </tr>
              </tfoot>
            </table>
          )}
        </div>
      )}

      <Fechamentos versao={versao} gestao={gestao} onMudou={() => setVersao((v) => v + 1)} />

      {fechando && (
        <FecharComissao
          linha={fechando}
          de={de}
          ate={ate}
          onFechar={() => setFechando(null)}
          onFechado={() => {
            setFechando(null)
            setVersao((v) => v + 1)
          }}
        />
      )}
    </div>
  )
}

function FecharComissao({ linha, de, ate, onFechar, onFechado }: { linha: ComissaoDoVendedor; de: string; ate: string; onFechar: () => void; onFechado: () => void }) {
  const { enviar } = useApi()
  const [gerar, setGerar] = useState(true)
  const [vencimento, setVencimento] = useState(somarDias(hojeIso(), 5))
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function fechar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      await enviar('POST', '/api/vendas/comissoes/fechamentos', { vendedorId: linha.vendedorId, de, ate, gerarContaAPagar: gerar, vencimento: gerar ? vencimento : null })
      onFechado()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível fechar.')
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={`Fechar comissão de ${linha.vendedor}`} onFechar={onFechar}>
      <form onSubmit={fechar} className="space-y-4 text-sm">
        <p>
          {`De ${dataBr(de)} a ${dataBr(ate)}: as vendas e trocas ainda abertas desse período ficam fechadas. Comissão a fechar: `}
          <strong>{moeda(linha.comissaoPendente)}</strong>
        </p>
        <label className="flex items-center gap-2">
          <input type="checkbox" checked={gerar} onChange={(e) => setGerar(e.target.checked)} className="h-4 w-4" />
          Gerar conta a pagar no financeiro (categoria Comissões)
        </label>
        {gerar && (
          <Campo rotulo="Vencimento" className="max-w-48">
            <input type="date" value={vencimento} onChange={(e) => setVencimento(e.target.value)} className={estilos.campo} />
          </Campo>
        )}
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" disabled={enviando} className={estilos.botaoPrimario}>Fechar comissão</button>
        </div>
      </form>
    </Dialogo>
  )
}

function Fechamentos({ versao, gestao, onMudou }: { versao: number; gestao: boolean; onMudou: () => void }) {
  const { getJson, enviar } = useApi()
  const [lista, setLista] = useState<Fechamento[] | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<Fechamento[]>('/api/vendas/comissoes/fechamentos', controle.signal)
      .then(setLista)
      .catch(() => {
        if (!controle.signal.aborted) setLista([])
      })
    return () => controle.abort()
  }, [getJson, versao])

  const reabrir = async (f: Fechamento) => {
    setErro(null)
    try {
      await enviar('POST', `/api/vendas/comissoes/fechamentos/${f.id}/reabrir`)
      onMudou()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível reabrir.')
    }
  }

  if (!lista || lista.length === 0) return null
  return (
    <section className="mt-8">
      <h2 className="font-semibold">Fechamentos</h2>
      {erro && <p role="alert" className={`mt-2 ${estilos.erro}`}>{erro}</p>}
      <div className="mt-3 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
        <table aria-label="Fechamentos" className="w-full text-sm">
          <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
            <tr>
              <th className="px-4 py-2">Vendedor</th>
              <th className="px-4 py-2">Período</th>
              <th className="px-4 py-2 text-right">Vendas</th>
              <th className="px-4 py-2 text-right">Comissão</th>
              <th className="px-4 py-2">Financeiro</th>
              <th />
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {lista.map((f) => (
              <tr key={f.id}>
                <td className="px-4 py-2 font-semibold">{f.vendedorNome}</td>
                <td className="px-4 py-2">{`${dataBr(f.de)} a ${dataBr(f.ate)}`}</td>
                <td className="px-4 py-2 text-right tabular-nums">{f.trocas > 0 ? `${f.vendas} (+${f.trocas} trocas)` : f.vendas}</td>
                <td className="px-4 py-2 text-right font-semibold tabular-nums">{moeda(f.comissao)}</td>
                <td className="px-4 py-2">{f.contaAPagarGerada ? 'Conta a pagar gerada' : '—'}</td>
                <td className="px-4 py-2 text-right">
                  {gestao && <button type="button" onClick={() => void reabrir(f)} className={estilos.botao}>Reabrir</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
