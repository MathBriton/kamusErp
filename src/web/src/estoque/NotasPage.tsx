import { useEffect, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoEtiquetas } from './BotaoEtiquetas'
import { BuscaDeProduto } from './BuscaDeProduto'
import { MatrizDeEstoque } from './MatrizDeEstoque'
import { pecas, PERFIS_QUE_MOVIMENTAM, type Matriz } from './tipos'
import { useLojas } from './useLojas'

interface ItemDaNota {
  numero: number
  codigo: string
  ean: string | null
  descricao: string
  unidade: string
  quantidade: number
  custoTotal: number
  custoPorPeca: number
  fator: number
  pecasEsperadas: number
  situacao: 'Pendente' | 'Pronto' | 'Ignorado'
  origemDoVinculo: string | null
  produtoId: string | null
  produto: string | null
  distribuicao: { skuId: string; cor: string; tamanho: string; quantidade: number }[]
}

interface Nota {
  id: string
  chave: string
  numero: string
  serie: string
  emitidaEm: string
  fornecedorDocumento: string
  fornecedorNome: string
  fornecedorCadastrado: boolean
  valorTotal: number
  autorizada: boolean
  status: 'Pendente' | 'Lancada' | 'Descartada'
  prontaParaLancar: boolean
  loja: string | null
  operacaoId: string | null
  itens: ItemDaNota[]
  duplicatas: { numero: string; vencimento: string; valor: number }[]
}

interface ResumoDaNota {
  id: string
  numero: string
  serie: string
  emitidaEm: string
  fornecedorNome: string
  valorTotal: number
  status: Nota['status']
  itens: number
  pendentes: number
}

const data = (iso: string) => new Date(iso).toLocaleDateString('pt-BR')
const dataCurta = (dia: string) => dia.split('-').reverse().join('/')
const documento = (d: string) => (d.length === 14 ? d.replace(/^(..)(...)(...)(....)(..)$/, '$1.$2.$3/$4-$5') : d)
const quantidade = (n: number) => n.toLocaleString('pt-BR', { maximumFractionDigits: 4 })

const SITUACAO: Record<ItemDaNota['situacao'], string> = {
  Pendente: 'bg-warning-soft text-warning',
  Pronto: 'bg-success-soft text-success',
  Ignorado: 'bg-surface-2 text-muted',
}

/** Entrada por XML da NF-e: importar o arquivo, conferir cada item (SKU, grade e fator) e lançar no estoque. */
export function NotasPage() {
  const { getJson, enviarArquivo } = useApi()
  const { perfilAtual, consolidado } = useEmpresa()
  const [params, setParams] = useSearchParams()
  const id = params.get('id')
  const [lista, setLista] = useState<ResumoDaNota[] | null>(null)
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    if (id || consolidado || !PERFIS_QUE_MOVIMENTAM.includes(perfilAtual)) return
    const controle = new AbortController()
    getJson<ResumoDaNota[]>('/api/estoque/notas', controle.signal)
      .then(setLista)
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, id, consolidado, perfilAtual])

  if (!PERFIS_QUE_MOVIMENTAM.includes(perfilAtual) || consolidado)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Entrada por XML da NF-e</h1>
        <p className={`mt-4 ${estilos.erro}`}>
          {consolidado ? 'Escolha uma empresa no seletor do topo: a nota precisa ser da empresa que a recebeu.' : 'Só Dono, Gerente e Estoquista lançam notas de entrada.'}
        </p>
      </div>
    )

  if (id) return <DetalheDaNota id={id} onVoltar={() => setParams({})} />

  async function importar(e: FormEvent) {
    e.preventDefault()
    if (!arquivo) return
    setErro(null)
    setEnviando(true)
    try {
      const formulario = new FormData()
      formulario.append('arquivo', arquivo)
      const r = await enviarArquivo<Nota>('/api/estoque/notas', formulario)
      setParams({ id: r.corpo.id })
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível importar o XML.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Estoque</p>
      <h1 className="text-2xl font-bold tracking-tight">Entrada por XML da NF-e</h1>
      <p className="mt-1 text-sm text-muted">
        Envie o XML que o fornecedor mandou. Itens já conhecidos (pelo código de barras ou por uma nota anterior) vêm vinculados; os outros você liga à grade uma vez e o sistema lembra.
      </p>

      <form onSubmit={importar} className="mt-6 flex flex-wrap items-end gap-3 rounded-2xl border border-border bg-surface p-5 shadow-sm">
        <Campo rotulo="Arquivo XML da NF-e" className="min-w-72 flex-1">
          <input type="file" accept=".xml,text/xml,application/xml" onChange={(e) => setArquivo(e.target.files?.[0] ?? null)} className={`${estilos.campo} py-1.5`} />
        </Campo>
        <button type="submit" disabled={!arquivo || enviando} className={estilos.botaoPrimario}>{enviando ? 'Importando...' : 'Importar XML'}</button>
      </form>
      {erro && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {lista && (
        <div className="mt-6 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          {lista.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted">Nenhuma nota importada ainda.</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-4 py-2">Nota</th>
                  <th className="px-4 py-2">Fornecedor</th>
                  <th className="px-4 py-2">Emissão</th>
                  <th className="px-4 py-2 text-right">Valor</th>
                  <th className="px-4 py-2">Situação</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {lista.map((n) => (
                  <tr key={n.id}>
                    <td className="px-4 py-2.5">
                      <button type="button" onClick={() => setParams({ id: n.id })} className="font-semibold hover:text-primary hover:underline">
                        {`NF ${n.numero}/${n.serie}`}
                      </button>
                    </td>
                    <td className="px-4 py-2.5">{n.fornecedorNome}</td>
                    <td className="px-4 py-2.5 text-muted">{data(n.emitidaEm)}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums">{moeda(n.valorTotal)}</td>
                    <td className="px-4 py-2.5">
                      {n.status === 'Lancada' ? (
                        <span className="rounded-full bg-success-soft px-2 py-0.5 text-xs font-semibold text-success">Lançada</span>
                      ) : (
                        <span className="rounded-full bg-warning-soft px-2 py-0.5 text-xs font-semibold text-warning">
                          {n.pendentes ? `Conferência: ${n.pendentes} de ${n.itens} pendente(s)` : 'Pronta para lançar'}
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  )
}

function DetalheDaNota({ id, onVoltar }: { id: string; onVoltar: () => void }) {
  const { getJson, enviar } = useApi()
  const lojas = useLojas()
  const ativas = lojas?.filter((l) => l.ativa) ?? []
  const [nota, setNota] = useState<Nota | null>(null)
  const [versao, setVersao] = useState(0)
  const [vinculando, setVinculando] = useState<ItemDaNota | null>(null)
  const [lojaId, setLojaId] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)
  const lojaEscolhida = lojaId || (ativas.length === 1 ? ativas[0]!.id : '')

  useEffect(() => {
    const controle = new AbortController()
    getJson<Nota>(`/api/estoque/notas/${id}`, controle.signal)
      .then(setNota)
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, id, versao])

  if (!nota) return <p className="text-sm text-muted">Carregando...</p>
  const pendente = nota.status === 'Pendente'
  const recarregar = () => setVersao((v) => v + 1)

  /** Executa e mostra a mensagem devolvida (ou o erro da API). */
  async function acao(fazer: () => Promise<string>) {
    setErro(null)
    try {
      setAviso(await fazer())
      recarregar()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível concluir a ação.')
    }
  }

  const ignorar = (item: ItemDaNota, ignorado: boolean) =>
    acao(async () => {
      await enviar('PUT', `/api/estoque/notas/${id}/itens/${item.numero}`, { ignorado })
      return ignorado ? `Item ${item.numero} ignorado.` : `Item ${item.numero} voltou para conferência.`
    })
  const lancar = () =>
    acao(async () => {
      const r = await enviar<{ quantidade: number }>('POST', `/api/estoque/notas/${id}/lancar`, { lojaId: lojaEscolhida })
      return `Entrada lançada: ${pecas(r.quantidade)}.`
    })
  const descartar = () =>
    acao(async () => {
      await enviar('POST', `/api/estoque/notas/${id}/descartar`)
      return 'Nota descartada.'
    })

  return (
    <div className="mx-auto max-w-6xl">
      <button type="button" onClick={onVoltar} className="text-sm text-primary hover:underline">← Notas importadas</button>
      <div className="mt-2 flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">{`NF ${nota.numero}/${nota.serie} — ${nota.fornecedorNome}`}</h1>
          <p className="mt-1 text-sm text-muted">
            {`CNPJ ${documento(nota.fornecedorDocumento)} · emitida em ${data(nota.emitidaEm)} · ${moeda(nota.valorTotal)}`}
            {nota.status === 'Lancada' && ` · lançada em ${nota.loja}`}
            {nota.status === 'Descartada' && ' · descartada'}
          </p>
        </div>
      </div>

      <div className="mt-3 flex flex-wrap gap-2 text-xs">
        {nota.autorizada ? (
          <span className="rounded-full bg-success-soft px-2 py-0.5 font-semibold text-success">Autorizada pela SEFAZ</span>
        ) : (
          <span className="rounded-full bg-warning-soft px-2 py-0.5 font-semibold text-warning">XML sem protocolo de autorização — confira com o fornecedor</span>
        )}
        {!nota.fornecedorCadastrado && <span className="rounded-full bg-info-soft px-2 py-0.5 font-semibold text-info">Fornecedor novo: será cadastrado ao lançar</span>}
      </div>

      {(aviso || (nota.status === 'Lancada' && nota.operacaoId)) && (
        <div role="status" className="mt-4 flex flex-wrap items-center gap-3 rounded-lg bg-success-soft px-3 py-2.5 text-sm text-success">
          <Icone nome="check" />
          {aviso ?? 'Nota lançada no estoque.'}
          {nota.operacaoId && <BotaoEtiquetas operacaoId={nota.operacaoId} />}
        </div>
      )}
      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      <div className="mt-6 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
        <table aria-label="Itens da nota" className="w-full text-sm">
          <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
            <tr>
              <th className="px-3 py-2">#</th>
              <th className="px-3 py-2">Item do fornecedor</th>
              <th className="px-3 py-2 text-right">Qtd.</th>
              <th className="px-3 py-2 text-right">Custo/peça</th>
              <th className="px-3 py-2">No estoque</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {nota.itens.map((i) => (
              <tr key={i.numero} className={i.situacao === 'Ignorado' ? 'text-muted' : ''}>
                <td className="px-3 py-2.5 tabular-nums">{i.numero}</td>
                <td className="px-3 py-2.5">
                  <span className="font-semibold">{i.descricao}</span>
                  <span className="block font-mono text-xs text-muted">{`${i.codigo}${i.ean ? ` · EAN ${i.ean}` : ''}`}</span>
                </td>
                <td className="px-3 py-2.5 text-right whitespace-nowrap tabular-nums">
                  {`${quantidade(i.quantidade)} ${i.unidade}`}
                  {i.fator > 1 && <span className="block text-xs text-muted">{`× ${i.fator} = ${quantidade(i.pecasEsperadas)} peças`}</span>}
                </td>
                <td className="px-3 py-2.5 text-right tabular-nums">{moeda(i.custoPorPeca)}</td>
                <td className="px-3 py-2.5">
                  <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${SITUACAO[i.situacao]}`}>
                    {i.situacao === 'Pronto' ? 'Vinculado' : i.situacao === 'Ignorado' ? 'Ignorado' : 'Pendente'}
                  </span>
                  {i.origemDoVinculo && <span className="ml-1 text-xs text-muted">{i.origemDoVinculo === 'ean' ? 'pelo código de barras' : 'pelo de-para'}</span>}
                  {i.produto && <span className="mt-1 block text-xs">{i.produto}</span>}
                  {i.distribuicao.length > 0 && (
                    <span className="block text-xs text-muted">{i.distribuicao.map((d) => `${d.cor} ${d.tamanho}: ${d.quantidade}`).join(' · ')}</span>
                  )}
                </td>
                <td className="px-3 py-2.5 text-right whitespace-nowrap">
                  {pendente && (
                    <span className="inline-flex gap-2">
                      {i.situacao !== 'Ignorado' && (
                        <button type="button" onClick={() => setVinculando(i)} className={estilos.botao} aria-label={`Vincular item ${i.numero}`}>
                          {i.situacao === 'Pronto' ? 'Alterar' : 'Vincular'}
                        </button>
                      )}
                      <button type="button" onClick={() => void ignorar(i, i.situacao !== 'Ignorado')} className={estilos.botao} aria-label={`${i.situacao === 'Ignorado' ? 'Não ignorar' : 'Ignorar'} item ${i.numero}`}>
                        {i.situacao === 'Ignorado' ? 'Não ignorar' : 'Ignorar'}
                      </button>
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {nota.duplicatas.length > 0 && (
        <p className="mt-3 text-xs text-muted">
          {`Duplicatas: ${nota.duplicatas.map((d) => `${d.numero} em ${dataCurta(d.vencimento)} (${moeda(d.valor)})`).join(' · ')}. Elas irão para contas a pagar quando o financeiro chegar (v0.13.0).`}
        </p>
      )}

      {pendente && (
        <div className="mt-6 flex flex-wrap items-end justify-end gap-3">
          <button type="button" onClick={() => void descartar()} className={estilos.botao}>Descartar nota</button>
          <Campo rotulo="Loja que recebeu" className="min-w-52">
            <select value={lojaEscolhida} onChange={(e) => setLojaId(e.target.value)} className={estilos.campo}>
              <option value="">Escolha...</option>
              {ativas.map((l) => (
                <option key={l.id} value={l.id}>{l.nome}</option>
              ))}
            </select>
          </Campo>
          <button type="button" onClick={() => void lancar()} disabled={!nota.prontaParaLancar || !lojaEscolhida} className={estilos.botaoPrimario}>
            Lançar entrada
          </button>
        </div>
      )}
      {pendente && !nota.prontaParaLancar && <p className="mt-2 text-right text-xs text-muted">Vincule ou ignore todos os itens para lançar.</p>}

      {vinculando && (
        <VincularItem
          notaId={id}
          item={vinculando}
          onFechar={() => setVinculando(null)}
          onSalvo={() => {
            setVinculando(null)
            setAviso(`Item ${vinculando.numero} vinculado.`)
            recarregar()
          }}
        />
      )}
    </div>
  )
}

function VincularItem({ notaId, item, onFechar, onSalvo }: { notaId: string; item: ItemDaNota; onFechar: () => void; onSalvo: () => void }) {
  const { getJson, enviar } = useApi()
  const [matriz, setMatriz] = useState<Matriz | null>(null)
  const [valores, setValores] = useState<Record<string, string>>(Object.fromEntries(item.distribuicao.map((d) => [d.skuId, String(d.quantidade)])))
  const [fator, setFator] = useState(String(item.fator))
  const [erro, setErro] = useState<string | null>(null)

  const abrir = (produtoId: string, manterValores: boolean) => {
    getJson<Matriz>(`/api/estoque/posicao/${produtoId}`)
      .then((m) => {
        setMatriz(m)
        if (!manterValores) setValores({})
      })
      .catch(() => setErro('Não foi possível abrir o produto.'))
  }

  useEffect(() => {
    if (!item.produtoId) return
    const timer = window.setTimeout(() => abrir(item.produtoId!, true), 0)
    return () => window.clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps -- abre só o produto sugerido ao montar
  }, [])

  const fatorNumero = Math.max(1, Number.parseInt(fator, 10) || 1)
  const esperadas = item.quantidade * fatorNumero
  const digitadas = Object.values(valores).reduce((s, v) => s + (Number.parseInt(v, 10) || 0), 0)

  async function salvar() {
    if (!matriz) return
    setErro(null)
    const distribuicao = Object.entries(valores)
      .map(([skuId, v]) => ({ skuId, quantidade: Number.parseInt(v, 10) || 0 }))
      .filter((d) => d.quantidade > 0)
    try {
      await enviar('PUT', `/api/estoque/notas/${notaId}/itens/${item.numero}`, { produtoId: matriz.produtoId, fator: fatorNumero, distribuicao })
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível vincular.')
    }
  }

  return (
    <Dialogo titulo={`Vincular item ${item.numero}`} onFechar={onFechar} largo>
      <div className="space-y-4">
        <p className="text-sm">
          <span className="font-semibold">{item.descricao}</span>
          <span className="block text-muted">{`${item.codigo} · ${quantidade(item.quantidade)} ${item.unidade} · custo ${moeda(item.custoTotal)}`}</span>
        </p>
        <div className="flex flex-wrap items-end gap-3">
          <Campo rotulo={`Peças por ${item.unidade}`} className="w-40">
            <input value={fator} onChange={(e) => setFator(e.target.value.replace(/\D/g, ''))} inputMode="numeric" className={estilos.campo} />
          </Campo>
          <p className="pb-2 text-sm text-muted">{`A nota traz ${quantidade(esperadas)} peça(s) para distribuir.`}</p>
        </div>
        {matriz ? (
          <>
            <div className="flex items-center justify-between gap-2">
              <p className="font-semibold">
                <span className="font-mono text-xs text-muted">{matriz.referencia}</span> {matriz.descricao}
              </p>
              <button type="button" onClick={() => setMatriz(null)} className="text-sm text-primary hover:underline">Trocar produto</button>
            </div>
            <MatrizDeEstoque matriz={matriz} digitacao={{ valores, onMudar: (skuId, v) => setValores((x) => ({ ...x, [skuId]: v })), somenteAtivos: true }} />
            <p className={`text-sm ${digitadas === esperadas ? 'text-success' : 'text-muted'}`}>{`Distribuídas: ${digitadas} de ${quantidade(esperadas)} peça(s).`}</p>
          </>
        ) : (
          <BuscaDeProduto onEscolher={(produtoId) => abrir(produtoId, false)} />
        )}
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="button" onClick={() => void salvar()} disabled={!matriz} className={estilos.botaoPrimario}>Salvar vínculo</button>
        </div>
      </div>
    </Dialogo>
  )
}
