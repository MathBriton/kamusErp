import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { Confirmacao } from '../components/grade/Confirmacao'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade, useListaPaginada } from '../components/grade/useGrade'
import { useEmpresa } from '../empresa/EmpresaContext'
import { dataBr, hojeIso, somarDias } from '../financeiro/tipos'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { inicioDoMes, PERFIS_DE_GESTAO, PERFIS_QUE_CONSULTAM, type ComissaoDoVendedor, type Fechamento, type Pagina } from './tipos'
import { useVendedores } from './useVendas'

/**
 * Comissão por vendedor no período (vendas concluídas, ajustada pelas trocas) como resumo, fechamento com conta a pagar
 * e a grade dos fechamentos (reabrir, exportar os selecionados).
 */
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
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Vendas"
        titulo="Comissões"
        descricao="Percentual do vendedor sobre o total de cada venda concluída; trocas ajustam pela diferença e canceladas não contam. Feche o período para gerar a conta a pagar."
        acoes={<BotaoExportar relatorio="comissoes" parametros={filtros} />}
      />

      <BarraDeFiltros
        ativos={de !== inicioDoMes(hojeIso()) || ate !== hojeIso()}
        onLimpar={() => {
          setDe(inicioDoMes(hojeIso()))
          setAte(hojeIso())
        }}
      >
        <Campo rotulo="De" className="w-40">
          <input type="date" value={de} onChange={(e) => setDe(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Até" className="w-40">
          <input type="date" value={ate} onChange={(e) => setAte(e.target.value)} className={estilos.campo} />
        </Campo>
      </BarraDeFiltros>

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

const FILTROS_DOS_FECHAMENTOS = { vendedorId: '' }

/** Fechamentos já feitos, no padrão de grade: filtro por vendedor, reabrir com confirmação e exportar os selecionados. */
function Fechamentos({ versao, gestao, onMudou }: { versao: number; gestao: boolean; onMudou: () => void }) {
  const { enviar } = useApi()
  const vendedores = useVendedores()
  const grade = useGrade(FILTROS_DOS_FECHAMENTOS)
  const { dados, erro } = useListaPaginada<Pagina<Fechamento>>(`/api/vendas/comissoes/fechamentos?${grade.consulta}`, versao)
  const [reabrindo, setReabrindo] = useState<Fechamento | null>(null)

  const colunas: Coluna<Fechamento>[] = [
    { titulo: 'Vendedor', celula: (f) => <span className="font-semibold">{f.vendedorNome}</span> },
    { titulo: 'Período', celula: (f) => `${dataBr(f.de)} a ${dataBr(f.ate)}` },
    { titulo: 'Vendas', celula: (f) => (f.trocas > 0 ? `${f.vendas} (+${f.trocas} trocas)` : f.vendas), alinhar: 'direita' },
    { titulo: 'Total vendido', celula: (f) => moeda(f.totalVendido), alinhar: 'direita' },
    { titulo: 'Comissão', celula: (f) => <span className="font-semibold">{moeda(f.comissao)}</span>, alinhar: 'direita' },
    { titulo: 'Financeiro', celula: (f) => (f.contaAPagarGerada ? 'Conta a pagar gerada' : '—') },
    { titulo: 'Fechado por', celula: (f) => <span className="text-muted">{`${f.fechadoPor ?? 'Sistema'} · ${new Date(f.fechadoEm).toLocaleDateString('pt-BR')}`}</span> },
  ]

  return (
    <section className="mt-10">
      <div className="flex flex-wrap items-end gap-3">
        <h2 className="flex-1 text-lg font-semibold">Fechamentos</h2>
        <BotaoExportar relatorio="fechamentos-de-comissao" parametros={grade.parametros} selecionados={[...grade.selecionados]} />
      </div>
      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <Campo rotulo="Vendedor" className="w-56">
          <select value={grade.filtros.vendedorId} onChange={(e) => grade.filtrar({ vendedorId: e.target.value })} className={estilos.campo}>
            <option value="">Todos</option>
            {vendedores?.map((v) => (
              <option key={v.id} value={v.id}>{v.nome}</option>
            ))}
          </select>
        </Campo>
      </BarraDeFiltros>
      {erro && <p role="alert" className={`mt-2 ${estilos.erro}`}>{erro}</p>}
      <GradeDeRegistros
        rotulo="Fechamentos"
        colunas={colunas}
        itens={dados?.itens ?? (erro ? [] : null)}
        chave={(f) => f.id}
        nome={(f) => `fechamento de ${f.vendedorNome}`}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        vazio={grade.ativos ? 'Nenhum fechamento com esses filtros.' : 'Nenhum fechamento ainda.'}
        acoes={gestao ? (f) => <AcaoDaLinha rotulo={`Reabrir fechamento de ${f.vendedorNome}`} icone="x" perigo onClick={() => setReabrindo(f)} /> : undefined}
      />
      {reabrindo && (
        <Confirmacao
          titulo="Reabrir fechamento"
          rotulo="Reabrir"
          onCancelar={() => setReabrindo(null)}
          onConfirmar={async () => {
            await enviar('POST', `/api/vendas/comissoes/fechamentos/${reabrindo.id}/reabrir`)
            setReabrindo(null)
            grade.limparSelecao()
            onMudou()
          }}
        >
          <p>
            {`A comissão de ${reabrindo.vendedorNome} (${dataBr(reabrindo.de)} a ${dataBr(reabrindo.ate)}, ${moeda(reabrindo.comissao)}) volta a ficar pendente`}
            {reabrindo.contaAPagarGerada ? ' e a conta a pagar é cancelada (se ainda não foi paga).' : '.'}
          </p>
        </Confirmacao>
      )}
    </section>
  )
}
