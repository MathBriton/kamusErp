import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { lerNumero, moeda, type ProdutoResumo } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { estilos } from '../components/estilos'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { MatrizDeEstoque } from './MatrizDeEstoque'
import { MOTIVOS_DE_AJUSTE, pecas, PERFIS_QUE_MOVIMENTAM, type Matriz } from './tipos'
import { useLojas } from './useLojas'
import { BotaoEtiquetas } from './BotaoEtiquetas'
import { salvarArquivo } from '../relatorios/salvarArquivo'

export type Modo = 'entrada' | 'ajuste' | 'transferencia' | 'etiquetas'

const TEXTOS: Record<Modo, { titulo: string; subtitulo: string; confirmar: string; feito: string }> = {
  entrada: {
    titulo: 'Entrada de mercadoria',
    subtitulo: 'Digite as quantidades recebidas na grade. O custo médio é recalculado a cada entrada.',
    confirmar: 'Lançar entrada',
    feito: 'Entrada lançada',
  },
  ajuste: {
    titulo: 'Ajustes de estoque',
    subtitulo: 'Use número negativo para baixar (perda, avaria, furto) e positivo para somar (sobra na contagem).',
    confirmar: 'Lançar ajuste',
    feito: 'Ajuste lançado',
  },
  etiquetas: {
    titulo: 'Etiquetas',
    subtitulo: 'Escolha os produtos e quantas etiquetas de cada cor e tamanho. As peças de uma entrada também podem ser impressas no fim do lançamento.',
    confirmar: 'Gerar PDF',
    feito: 'PDF gerado',
  },
  transferencia: {
    titulo: 'Transferências',
    subtitulo: 'Peças que saem de uma loja e entram em outra da mesma empresa, levando o custo médio.',
    confirmar: 'Transferir',
    feito: 'Transferência feita',
  },
}

interface Linha {
  skuId: string
  rotulo: string
  quantidade: number
}

interface ItemDoProduto {
  produtoId: string
  referencia: string
  descricao: string
  custo: number | null
  linhas: Linha[]
}

/** Entrada, ajuste e transferência: escolhe o produto, digita na matriz cor × tamanho, junta numa lista e confirma tudo de uma vez. */
export function MovimentacaoPage({ modo }: { modo: Modo }) {
  const { getJson, enviar, baixar } = useApi()
  const { perfilAtual, consolidado } = useEmpresa()
  const textos = TEXTOS[modo]
  const [versaoDasLojas, setVersaoDasLojas] = useState(0)
  const lojas = useLojas(versaoDasLojas)
  const ativas = lojas?.filter((l) => l.ativa) ?? []
  const [lojaId, setLojaId] = useState('')
  const [destinoId, setDestinoId] = useState('')
  const [documento, setDocumento] = useState('')
  const [motivo, setMotivo] = useState('')
  const [observacao, setObservacao] = useState('')
  const [busca, setBusca] = useState('')
  const [encontrados, setEncontrados] = useState<ProdutoResumo[]>([])
  const [matriz, setMatriz] = useState<Matriz | null>(null)
  const [valores, setValores] = useState<Record<string, string>>({})
  const [custo, setCusto] = useState('')
  const [itens, setItens] = useState<ItemDoProduto[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [sucesso, setSucesso] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [formato, setFormato] = useState<'a4' | 'termica'>('a4')
  const [ultimaOperacao, setUltimaOperacao] = useState<string | null>(null)

  const lojaEscolhida = lojaId || (ativas.length === 1 ? ativas[0]!.id : '')

  useEffect(() => {
    const termo = busca.trim()
    if (termo.length < 2) return
    const controle = new AbortController()
    const timer = window.setTimeout(() => {
      getJson<{ itens: ProdutoResumo[] }>(`/api/produtos?${new URLSearchParams({ situacao: 'ativos', busca: termo })}`, controle.signal)
        .then((r) => setEncontrados(r.itens.slice(0, 8)))
        .catch(() => {})
    }, 250)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, busca])

  if (!PERFIS_QUE_MOVIMENTAM.includes(perfilAtual) || consolidado)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">{textos.titulo}</h1>
        <p className={`mt-4 ${estilos.erro}`}>
          {consolidado ? 'Escolha uma empresa no seletor do topo para movimentar o estoque.' : 'Só Dono, Gerente e Estoquista movimentam o estoque.'}
        </p>
      </div>
    )

  const escolher = (produtoId: string) => {
    setErro(null)
    setSucesso(null)
    getJson<Matriz>(`/api/estoque/posicao/${produtoId}?lojaId=${lojaEscolhida}`)
      .then((m) => {
        setMatriz(m)
        const jaNaLista = itens.find((i) => i.produtoId === m.produtoId)
        setValores(Object.fromEntries(jaNaLista?.linhas.map((l) => [l.skuId, String(l.quantidade)]) ?? []))
        setCusto(jaNaLista?.custo != null ? String(jaNaLista.custo).replace('.', ',') : m.precoCusto != null ? String(m.precoCusto).replace('.', ',') : '')
        setBusca('')
        setEncontrados([])
      })
      .catch((e: unknown) => setErro(e instanceof ErroDeApi ? e.message : 'Não foi possível abrir o produto.'))
  }

  const adicionar = () => {
    if (!matriz) return
    const linhas: Linha[] = matriz.celulas.flatMap((c) => {
      const q = Number(valores[c.skuId] ?? '')
      if (!valores[c.skuId] || !Number.isInteger(q) || q === 0) return []
      const cor = matriz.cores.find((x) => x.id === c.corId)?.nome ?? ''
      return [{ skuId: c.skuId, rotulo: `${cor} ${c.tamanho}`, quantidade: q }]
    })
    if (linhas.length === 0) {
      setErro('Digite ao menos uma quantidade na grade.')
      return
    }
    if (modo !== 'ajuste' && linhas.some((l) => l.quantidade < 0)) {
      setErro('As quantidades precisam ser positivas.')
      return
    }
    const custoInformado = modo === 'entrada' ? lerNumero(custo) : null
    const item = { produtoId: matriz.produtoId, referencia: matriz.referencia, descricao: matriz.descricao, custo: custoInformado, linhas }
    setItens((atual) => [...atual.filter((i) => i.produtoId !== item.produtoId), item])
    setMatriz(null)
    setErro(null)
  }

  const total = itens.reduce((s, i) => s + i.linhas.reduce((t, l) => t + Math.abs(l.quantidade), 0), 0)

  async function confirmar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    const corpoDosItens = itens.flatMap((i) => i.linhas.map((l) => ({ skuId: l.skuId, quantidade: l.quantidade, custoUnitario: i.custo })))
    if (modo === 'etiquetas') {
      setEnviando(true)
      try {
        const r = await baixar('/api/estoque/etiquetas', { formato, itens: corpoDosItens.map(({ skuId, quantidade }) => ({ skuId, quantidade })) })
        if (r.tipo === 'arquivo') salvarArquivo(r.blob, r.nome)
        setSucesso(`${textos.feito}: ${total} ${total === 1 ? 'etiqueta' : 'etiquetas'}.`)
        setItens([])
      } catch (ex) {
        setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível gerar as etiquetas.')
      } finally {
        setEnviando(false)
      }
      return
    }
    const [caminho, corpo] =
      modo === 'entrada'
        ? ['/api/estoque/entradas', { lojaId: lojaEscolhida, documento, observacao, itens: corpoDosItens }]
        : modo === 'ajuste'
          ? ['/api/estoque/ajustes', { lojaId: lojaEscolhida, motivo, observacao, itens: corpoDosItens }]
          : ['/api/estoque/transferencias', { origemId: lojaEscolhida, destinoId, observacao, itens: corpoDosItens }]
    setEnviando(true)
    try {
      const r = await enviar<{ operacaoId: string; quantidade: number }>('POST', caminho, corpo)
      setSucesso(`${textos.feito}: ${pecas(r.quantidade)}.`)
      setUltimaOperacao(modo === 'ajuste' ? null : r.operacaoId)
      setItens([])
      setDocumento('')
      setObservacao('')
      setMotivo('')
      setVersaoDasLojas((v) => v + 1)
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível lançar. Tente de novo.')
    } finally {
      setEnviando(false)
    }
  }

  const travado = itens.length > 0 || matriz !== null
  const podeConfirmar = itens.length > 0 && !!lojaEscolhida && (modo !== 'transferencia' || !!destinoId) && (modo !== 'ajuste' || !!motivo)

  return (
    <div className="mx-auto max-w-5xl">
      <CabecalhoDaPagina secao="Estoque" titulo={textos.titulo} descricao={textos.subtitulo} />

      {sucesso && (
        <p role="status" className="mt-4 flex items-center gap-2 rounded-lg bg-success-soft px-3 py-2.5 text-sm text-success">
          <Icone nome="check" />
          {sucesso}
          {modo !== 'etiquetas' && <Link to="/estoque/posicao" className="underline">Ver posição de estoque</Link>}
        </p>
      )}
      {sucesso && ultimaOperacao && modo !== 'etiquetas' && (
        <div className="mt-2 flex flex-wrap items-center gap-2 text-sm">
          <span className="text-muted">Imprimir uma etiqueta por peça recebida:</span>
          <BotaoEtiquetas operacaoId={ultimaOperacao} />
        </div>
      )}

      <form onSubmit={confirmar} className="mt-6 space-y-6">
        <section className="grid gap-4 rounded-2xl border border-border bg-surface p-5 shadow-sm sm:grid-cols-2">
          <Campo rotulo={modo === 'transferencia' ? 'Loja de origem' : 'Loja'}>
            <select value={lojaEscolhida} onChange={(e) => setLojaId(e.target.value)} disabled={travado} className={estilos.campo}>
              <option value="">Escolha...</option>
              {ativas.map((l) => (
                <option key={l.id} value={l.id}>{l.nome}</option>
              ))}
            </select>
          </Campo>
          {modo === 'transferencia' && (
            <Campo rotulo="Loja de destino">
              <select value={destinoId} onChange={(e) => setDestinoId(e.target.value)} className={estilos.campo}>
                <option value="">Escolha...</option>
                {ativas.filter((l) => l.id !== lojaEscolhida).map((l) => (
                  <option key={l.id} value={l.id}>{l.nome}</option>
                ))}
              </select>
            </Campo>
          )}
          {modo === 'entrada' && (
            <Campo rotulo="Documento (opcional)">
              <input value={documento} onChange={(e) => setDocumento(e.target.value)} placeholder="Ex.: NF 1234" maxLength={60} className={estilos.campo} />
            </Campo>
          )}
          {modo === 'etiquetas' && (
            <Campo rotulo="Formato">
              <select value={formato} onChange={(e) => setFormato(e.target.value as 'a4' | 'termica')} className={estilos.campo}>
                <option value="a4">Folha A4 (3 × 10 etiquetas)</option>
                <option value="termica">Impressora térmica (40 × 25 mm)</option>
              </select>
            </Campo>
          )}
          {modo === 'ajuste' && (
            <Campo rotulo="Motivo">
              <select value={motivo} onChange={(e) => setMotivo(e.target.value)} className={estilos.campo}>
                <option value="">Escolha...</option>
                {MOTIVOS_DE_AJUSTE.map((m) => (
                  <option key={m.valor} value={m.valor}>{m.rotulo}</option>
                ))}
              </select>
            </Campo>
          )}
          {modo !== 'etiquetas' && (
            <Campo rotulo={motivo === 'Outro' ? 'Observação (obrigatória)' : 'Observação (opcional)'} className="sm:col-span-2">
              <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={500} className={estilos.campo} />
            </Campo>
          )}
          {modo === 'transferencia' && ativas.length < 2 && lojas && (
            <p className="text-sm text-muted sm:col-span-2">
              Para transferir é preciso ter duas lojas na empresa. O Dono cadastra em <Link to="/configuracoes/lojas" className="text-primary underline">Administrativo → Lojas</Link>.
            </p>
          )}
          {travado && <p className="text-xs text-muted sm:col-span-2">Para trocar a loja, esvazie a lista de itens.</p>}
        </section>

        <section className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
          <h2 className="font-semibold">Itens</h2>
          {!matriz && (
            <div className="relative mt-3">
              <input
                aria-label="Buscar produto"
                value={busca}
                onChange={(e) => {
                  setBusca(e.target.value)
                  if (e.target.value.trim().length < 2) setEncontrados([])
                }}
                disabled={!lojaEscolhida}
                placeholder={lojaEscolhida ? 'Buscar produto por referência, descrição ou código de barras' : 'Escolha a loja primeiro'}
                className={estilos.campo}
              />
              {encontrados.length > 0 && (
                <ul className="absolute z-10 mt-1 w-full overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
                  {encontrados.map((p) => (
                    <li key={p.id}>
                      <button type="button" onClick={() => escolher(p.id)} className="w-full px-3 py-2 text-left text-sm hover:bg-surface-2">
                        <span className="font-mono text-xs text-muted">{p.referencia}</span> <span className="font-semibold">{p.descricao}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}

          {matriz && (
            <div className="mt-3 space-y-3 rounded-xl border border-primary p-4">
              <p className="font-semibold">
                <span className="font-mono text-xs text-muted">{matriz.referencia}</span> {matriz.descricao}
              </p>
              <MatrizDeEstoque
                matriz={matriz}
                digitacao={{
                  valores,
                  onMudar: (skuId, valor) => setValores((v) => ({ ...v, [skuId]: valor })),
                  permitirNegativo: modo === 'ajuste',
                  somenteAtivos: modo === 'entrada',
                }}
              />
              {modo === 'entrada' && (
                <Campo rotulo="Custo unitário (R$)" className="max-w-48">
                  <input value={custo} onChange={(e) => setCusto(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
                </Campo>
              )}
              <div className="flex justify-end gap-2">
                {modo === 'etiquetas' && (
                  <button
                    type="button"
                    onClick={() => setValores(Object.fromEntries(matriz.celulas.filter((c) => c.quantidade > 0).map((c) => [c.skuId, String(c.quantidade)])))}
                    className={estilos.botao}
                  >
                    Usar o saldo atual
                  </button>
                )}
                <button type="button" onClick={() => setMatriz(null)} className={estilos.botao}>Cancelar</button>
                <button type="button" onClick={adicionar} className={estilos.botaoPrimario}>Adicionar à lista</button>
              </div>
            </div>
          )}

          {itens.length > 0 && (
            <table aria-label="Itens da operação" className="mt-4 w-full text-sm">
              <thead className="text-left text-xs text-muted uppercase">
                <tr>
                  <th className="py-2">Produto</th>
                  <th className="py-2">Grade</th>
                  {modo === 'entrada' && <th className="py-2 text-right">Custo</th>}
                  <th className="py-2 text-right">Peças</th>
                  <th />
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {itens.map((i) => (
                  <tr key={i.produtoId}>
                    <td className="py-2">
                      <button type="button" onClick={() => escolher(i.produtoId)} className="text-left font-semibold hover:text-primary hover:underline">
                        {i.descricao}
                      </button>
                      <span className="block font-mono text-xs text-muted">{i.referencia}</span>
                    </td>
                    <td className="py-2 text-xs">{i.linhas.map((l) => `${l.rotulo}: ${l.quantidade > 0 && modo === 'ajuste' ? '+' : ''}${l.quantidade}`).join(' · ')}</td>
                    {modo === 'entrada' && <td className="py-2 text-right tabular-nums">{i.custo === null ? 'do cadastro' : moeda(i.custo)}</td>}
                    <td className="py-2 text-right font-semibold tabular-nums">{i.linhas.reduce((s, l) => s + Math.abs(l.quantidade), 0)}</td>
                    <td className="py-2 text-right">
                      <button type="button" onClick={() => setItens((atual) => atual.filter((x) => x.produtoId !== i.produtoId))} className="text-xs text-danger hover:underline">
                        Remover
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}

        <div className="flex items-center justify-end gap-3">
          {itens.length > 0 && <span className="text-sm text-muted">{`Total: ${pecas(total)}`}</span>}
          <button type="submit" disabled={!podeConfirmar || enviando} className={estilos.botaoPrimario}>
            {enviando ? 'Lançando...' : textos.confirmar}
          </button>
        </div>
      </form>
    </div>
  )
}
