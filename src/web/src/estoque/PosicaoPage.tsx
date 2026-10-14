import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { Kardex } from './Kardex'
import { MatrizDeEstoque } from './MatrizDeEstoque'
import { pecas, PERFIS_QUE_MOVIMENTAM, PERFIS_QUE_VEEM_CUSTO, type Celula, type Matriz, type Posicao } from './tipos'
import { useLojas } from './useLojas'

/** Posição de estoque por produto; clicar abre a matriz cor × tamanho e, numa célula, o kardex do SKU. */
export function PosicaoPage() {
  const { getJson } = useApi()
  const { perfilAtual } = useEmpresa()
  const veCusto = PERFIS_QUE_VEEM_CUSTO.includes(perfilAtual)
  const movimenta = PERFIS_QUE_MOVIMENTAM.includes(perfilAtual)
  const lojas = useLojas()
  const [lojaId, setLojaId] = useState('')
  const [busca, setBusca] = useState('')
  const [{ posicao, erro }, setEstado] = useState<{ posicao: Posicao | null; erro: string | null }>({ posicao: null, erro: null })
  const [matriz, setMatriz] = useState<Matriz | null>(null)
  const [celula, setCelula] = useState<Celula | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    const consulta = new URLSearchParams()
    if (lojaId) consulta.set('lojaId', lojaId)
    if (busca.trim()) consulta.set('busca', busca.trim())
    const timer = window.setTimeout(() => {
      getJson<Posicao>(`/api/estoque/posicao${consulta.size ? `?${consulta}` : ''}`, controle.signal)
        .then((p) => setEstado({ posicao: p, erro: null }))
        .catch((e: unknown) => {
          if (!controle.signal.aborted) setEstado({ posicao: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar.' })
        })
    }, busca ? 250 : 0)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, lojaId, busca])

  const abrir = (produtoId: string) => {
    getJson<Matriz>(`/api/estoque/posicao/${produtoId}${lojaId ? `?lojaId=${lojaId}` : ''}`)
      .then(setMatriz)
      .catch(() => {})
  }

  const fechar = () => {
    setMatriz(null)
    setCelula(null)
  }

  return (
    <div className="mx-auto max-w-7xl">
      <p className="text-xs text-muted">Estoque</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Posição de estoque</h1>
          <p className="mt-1 text-sm text-muted">Saldo por produto. Clique no produto para ver a grade cor × tamanho.</p>
        </div>
        {movimenta && (
          <Link to="/estoque/entradas" className={estilos.botaoPrimario}>
            Entrada de mercadoria
          </Link>
        )}
      </div>

      <div className="mt-6 flex flex-wrap items-end gap-3">
        <label className="min-w-60 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input value={busca} onChange={(e) => setBusca(e.target.value)} placeholder="Referência, descrição ou código de barras" className={estilos.campo} />
        </label>
        <label className="text-sm">
          <span className="mb-1 block font-semibold">Loja</span>
          <select value={lojaId} onChange={(e) => setLojaId(e.target.value)} className={estilos.campo}>
            <option value="">Todas as lojas</option>
            {lojas?.map((l) => (
              <option key={l.id} value={l.id}>{l.nome}</option>
            ))}
          </select>
        </label>
        {veCusto && <BotaoExportar relatorio="posicao-de-estoque" parametros={{ lojaId, busca: busca.trim() }} />}
      </div>

      {erro && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {posicao && (
        <>
          <dl className="mt-4 grid gap-3 sm:grid-cols-3">
            <Indicador rotulo="Peças em estoque" valor={posicao.totalDePecas.toLocaleString('pt-BR')} />
            {posicao.valorEmCusto !== null && <Indicador rotulo="Valor em custo" valor={moeda(posicao.valorEmCusto)} />}
            <Indicador rotulo="Valor em venda" valor={moeda(posicao.valorEmVenda)} />
          </dl>
          <div className="mt-4 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
            {posicao.itens.length === 0 ? (
              <p className="p-8 text-center text-sm text-muted">
                Nenhum produto com estoque{busca ? ' nessa busca' : ''}.{' '}
                {movimenta && !busca && (
                  <>
                    Lance uma <Link to="/estoque/entradas" className="text-primary underline">entrada de mercadoria</Link> ou importe o{' '}
                    <Link to="/cadastros/importacao?tipo=estoque-inicial" className="text-primary underline">estoque inicial por planilha</Link>.
                  </>
                )}
              </p>
            ) : (
              <table className="w-full text-sm">
                <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                  <tr>
                    <th className="px-4 py-2">Referência</th>
                    <th className="px-4 py-2">Descrição</th>
                    <th className="px-4 py-2 text-right">Peças</th>
                    {veCusto && <th className="px-4 py-2 text-right">Valor em custo</th>}
                    <th className="px-4 py-2 text-right">Valor em venda</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {posicao.itens.map((i) => (
                    <tr key={i.produtoId}>
                      <td className="px-4 py-2.5 font-mono text-xs">{i.referencia}</td>
                      <td className="px-4 py-2.5">
                        <button type="button" onClick={() => abrir(i.produtoId)} className="text-left font-semibold hover:text-primary hover:underline">
                          {i.descricao}
                        </button>
                      </td>
                      <td className={`px-4 py-2.5 text-right tabular-nums ${i.quantidade < 0 ? 'text-danger' : ''}`}>{i.quantidade}</td>
                      {veCusto && <td className="px-4 py-2.5 text-right tabular-nums">{i.valorEmCusto === null ? '—' : moeda(i.valorEmCusto)}</td>}
                      <td className="px-4 py-2.5 text-right tabular-nums">{moeda(i.valorEmVenda)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}

      {matriz && (
        <Dialogo titulo={`${matriz.referencia} — ${matriz.descricao}`} onFechar={fechar} largo>
          {celula ? (
            <div className="space-y-3">
              <button type="button" onClick={() => setCelula(null)} className="text-sm text-primary hover:underline">← Voltar à grade</button>
              <p className="text-sm">
                <span className="font-semibold">{`Kardex: ${matriz.cores.find((c) => c.id === celula.corId)?.nome ?? ''} ${celula.tamanho}`}</span>
                <span className="text-muted">{` · código ${celula.codigoDeBarras}`}</span>
              </p>
              <Kardex skuId={celula.skuId} lojaId={lojaId} />
            </div>
          ) : (
            <div className="space-y-4">
              <MatrizDeEstoque matriz={matriz} onCelula={veCusto ? setCelula : undefined} />
              <p className="text-xs text-muted">
                {veCusto ? 'Clique numa quantidade para ver o kardex (o extrato de movimentos) do SKU.' : 'Quantidades em estoque por cor e tamanho.'}
                {lojaId ? '' : ' Soma de todas as lojas.'}
              </p>
              {matriz.lojas.length > 1 && (
                <ul aria-label="Por loja" className="flex flex-wrap gap-2 text-sm">
                  {matriz.lojas.map((l) => (
                    <li key={l.lojaId} className="rounded-lg border border-border px-3 py-1.5">
                      {l.nome}: <span className="font-semibold tabular-nums">{pecas(l.quantidade)}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </Dialogo>
      )}
    </div>
  )
}

function Indicador({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
      <dt className="text-xs text-muted uppercase">{rotulo}</dt>
      <dd className="mt-1 text-xl font-bold tabular-nums">{valor}</dd>
    </div>
  )
}
