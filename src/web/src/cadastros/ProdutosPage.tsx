import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { Amostra } from './Amostra'
import { decimal, lerNumero, moeda, PERFIS_DO_CATALOGO, type Cor, type Grade, type Produto, type ProdutoResumo, type Sku } from './catalogo'

type Janela = { tipo: 'novo' } | { tipo: 'detalhe'; produto: Produto } | { tipo: 'editar'; produto: Produto } | null

export function ProdutosPage() {
  const { getJson } = useApi()
  const { perfilAtual } = useEmpresa()
  const podeEditar = PERFIS_DO_CATALOGO.includes(perfilAtual)
  const [params, setParams] = useSearchParams()
  const [busca, setBusca] = useState('')
  const [situacao, setSituacao] = useState('ativos')
  const [{ produtos, erro }, setResultado] = useState<{ produtos: ProdutoResumo[] | null; erro: string | null }>({ produtos: null, erro: null })
  const [janela, setJanela] = useState<Janela>(null)
  const [versao, setVersao] = useState(0)
  const idDaUrl = params.get('id')

  useEffect(() => {
    const controle = new AbortController()
    const consulta = new URLSearchParams({ situacao })
    if (busca.trim()) consulta.set('busca', busca.trim())
    const timer = window.setTimeout(() => {
      getJson<{ itens: ProdutoResumo[] }>(`/api/produtos?${consulta}`, controle.signal)
        .then((r) => setResultado({ produtos: r.itens, erro: null }))
        .catch((e: unknown) => {
          if (!controle.signal.aborted) setResultado({ produtos: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar.' })
        })
    }, busca ? 250 : 0)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, situacao, busca, versao])

  const abrir = useCallback(
    (id: string) => {
      getJson<Produto>(`/api/produtos/${id}`)
        .then((produto) => setJanela({ tipo: 'detalhe', produto }))
        .catch(() => {})
    },
    [getJson],
  )

  useEffect(() => {
    if (!idDaUrl) return
    const timer = window.setTimeout(() => abrir(idDaUrl), 0)
    return () => window.clearTimeout(timer)
  }, [abrir, idDaUrl])

  const fechar = () => {
    setJanela(null)
    if (params.has('id')) setParams({}, { replace: true })
  }
  const atualizado = (produto: Produto) => {
    setJanela({ tipo: 'detalhe', produto })
    setVersao((v) => v + 1)
  }

  return (
    <div className="mx-auto max-w-7xl">
      <p className="text-xs text-muted">Cadastros</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Produtos</h1>
          <p className="mt-1 text-sm text-muted">Cada produto gera um SKU por cor × tamanho, com código de barras próprio.</p>
        </div>
        {podeEditar && (
          <button type="button" onClick={() => setJanela({ tipo: 'novo' })} className={estilos.botaoPrimario}>
            Novo produto
          </button>
        )}
      </div>

      <div className="mt-6 flex flex-wrap items-end gap-3">
        <label className="min-w-60 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input value={busca} onChange={(e) => setBusca(e.target.value)} placeholder="Descrição, referência, marca ou código de barras" className={estilos.campo} />
        </label>
        <label className="text-sm">
          <span className="mb-1 block font-semibold">Situação</span>
          <select value={situacao} onChange={(e) => setSituacao(e.target.value)} className={estilos.campo}>
            <option value="ativos">Ativos</option>
            <option value="inativos">Inativos</option>
            <option value="todos">Todos</option>
          </select>
        </label>
        <BotaoExportar relatorio="produtos" parametros={{ busca: busca.trim(), situacao }} />
      </div>

      {erro && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {produtos && (
        <div className="mt-4 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          {produtos.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted">Nenhum produto encontrado.</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-4 py-2">Referência</th>
                  <th className="px-4 py-2">Descrição</th>
                  <th className="px-4 py-2">Cores</th>
                  <th className="px-4 py-2 text-right">SKUs</th>
                  <th className="px-4 py-2 text-right">Preço</th>
                  <th className="px-4 py-2 text-right">Markup</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {produtos.map((p) => (
                  <tr key={p.id} className={p.ativo ? '' : 'text-muted'}>
                    <td className="px-4 py-2.5 font-mono text-xs">{p.referencia}</td>
                    <td className="px-4 py-2.5">
                      <button type="button" onClick={() => abrir(p.id)} className="text-left font-semibold hover:text-primary hover:underline">
                        {p.descricao}
                      </button>
                      <span className="block text-xs text-muted">{[p.marca, p.categoria, p.colecao].filter(Boolean).join(' · ')}</span>
                    </td>
                    <td className="px-4 py-2.5">{p.cores.join(', ')}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums">{p.skus}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums">{moeda(p.precoVenda)}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums">{p.markup === null ? '—' : decimal(p.markup)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {janela?.tipo === 'novo' && <FormularioDeProduto produto={null} onFechar={fechar} onSalvo={atualizado} />}
      {janela?.tipo === 'editar' && <FormularioDeProduto produto={janela.produto} onFechar={() => setJanela({ tipo: 'detalhe', produto: janela.produto })} onSalvo={atualizado} />}
      {janela?.tipo === 'detalhe' && (
        <DetalheDoProduto
          produto={janela.produto}
          podeEditar={podeEditar}
          onFechar={fechar}
          onEditar={() => setJanela({ tipo: 'editar', produto: janela.produto })}
          onAtualizado={atualizado}
        />
      )}
    </div>
  )
}

function FormularioDeProduto({ produto, onFechar, onSalvo }: { produto: Produto | null; onFechar: () => void; onSalvo: (p: Produto) => void }) {
  const { getJson, enviar } = useApi()
  const [grades, setGrades] = useState<Grade[]>([])
  const [cores, setCores] = useState<Cor[]>([])
  const [sugestoes, setSugestoes] = useState<{ marcas: string[]; categorias: string[]; colecoes: string[] }>({ marcas: [], categorias: [], colecoes: [] })
  const [form, setForm] = useState({
    referencia: produto?.referencia ?? '', descricao: produto?.descricao ?? '', marca: produto?.marca ?? '', categoria: produto?.categoria ?? '',
    colecao: produto?.colecao ?? '', referenciaDoFornecedor: produto?.referenciaDoFornecedor ?? '',
    precoVenda: produto ? decimal(produto.precoVenda) : '', precoCusto: produto?.precoCusto != null ? decimal(produto.precoCusto) : '',
    gradeId: produto?.grade.id ?? '',
  })
  const [corIds, setCorIds] = useState<string[]>(() => (produto ? [...new Set(produto.skus.filter((s) => s.ativo).map((s) => s.corId))] : []))
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    const controle = new AbortController()
    Promise.all([
      getJson<Grade[]>('/api/grades', controle.signal),
      getJson<Cor[]>('/api/cores', controle.signal),
      getJson<typeof sugestoes>('/api/produtos/sugestoes', controle.signal),
    ])
      .then(([g, c, s]) => {
        setGrades(g)
        setCores(c)
        setSugestoes(s)
      })
      .catch(() => {})
    return () => controle.abort()
  }, [getJson])

  const muda = (campo: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [campo]: e.target.value })
  const preco = lerNumero(form.precoVenda)
  const custo = lerNumero(form.precoCusto)
  const markup = preco !== null && custo ? preco / custo : null
  const grade = grades.find((g) => g.id === form.gradeId) ?? produto?.grade
  const alternarCor = (id: string) => setCorIds((atual) => (atual.includes(id) ? atual.filter((c) => c !== id) : [...atual, id]))

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setErro(null)
    const corpo = { ...form, gradeId: form.gradeId || null, precoVenda: preco ?? 0, precoCusto: custo, corIds }
    try {
      onSalvo(produto ? await enviar<Produto>('PUT', `/api/produtos/${produto.id}`, corpo) : await enviar<Produto>('POST', '/api/produtos', corpo))
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível salvar.')
      setEnviando(false)
    }
  }

  const listas = (id: string, valores: string[]) => (
    <datalist id={id}>
      {valores.map((v) => (
        <option key={v} value={v} />
      ))}
    </datalist>
  )

  return (
    <Dialogo titulo={produto ? `Editar ${produto.descricao}` : 'Novo produto'} onFechar={onFechar} largo>
      <form onSubmit={(e) => void salvar(e)} className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-3">
          <Campo rotulo="Referência">
            <input value={form.referencia} onChange={muda('referencia')} disabled={!!produto} placeholder="CAM-001" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Descrição" className="sm:col-span-2">
            <input value={form.descricao} onChange={muda('descricao')} className={estilos.campo} />
          </Campo>
          <Campo rotulo="Marca">
            <input value={form.marca} onChange={muda('marca')} list="sugestoes-marcas" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Categoria">
            <input value={form.categoria} onChange={muda('categoria')} list="sugestoes-categorias" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Coleção">
            <input value={form.colecao} onChange={muda('colecao')} list="sugestoes-colecoes" placeholder="Verão 2027" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Ref. do fornecedor">
            <input value={form.referenciaDoFornecedor} onChange={muda('referenciaDoFornecedor')} className={estilos.campo} />
          </Campo>
          <Campo rotulo="Preço de venda (R$)">
            <input value={form.precoVenda} onChange={muda('precoVenda')} inputMode="decimal" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Custo (R$)">
            <input value={form.precoCusto} onChange={muda('precoCusto')} inputMode="decimal" className={estilos.campo} />
          </Campo>
        </div>
        {listas('sugestoes-marcas', sugestoes.marcas)}
        {listas('sugestoes-categorias', sugestoes.categorias)}
        {listas('sugestoes-colecoes', sugestoes.colecoes)}
        {markup !== null && <p className="text-sm text-muted">{`Markup ${decimal(markup)}`}</p>}

        <Campo rotulo="Grade de tamanhos">
          <select value={form.gradeId} onChange={muda('gradeId')} disabled={!!produto} className={estilos.campo}>
            <option value="">Escolha...</option>
            {(produto ? [produto.grade] : grades).map((g) => (
              <option key={g.id} value={g.id}>
                {`${g.nome} (${g.tamanhos.join(', ')})`}
              </option>
            ))}
          </select>
        </Campo>

        <fieldset>
          <legend className="mb-1.5 text-sm font-semibold">Cores</legend>
          {cores.length === 0 ? (
            <p className="text-sm text-muted">
              Nenhuma cor cadastrada. <Link to="/cadastros/grades" className="text-primary underline">Cadastre em Grades e cores</Link>.
            </p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {cores.map((c) => (
                <label key={c.id} className={`flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-1.5 text-sm ${corIds.includes(c.id) ? 'border-primary bg-primary-soft' : 'border-border'}`}>
                  <input type="checkbox" checked={corIds.includes(c.id)} onChange={() => alternarCor(c.id)} aria-label={c.nome} className="sr-only" />
                  <Amostra hex={c.hex} />
                  {c.nome}
                </label>
              ))}
            </div>
          )}
        </fieldset>
        {grade && corIds.length > 0 && !produto && (
          <p className="text-sm text-muted">{`Serão criados ${grade.tamanhos.length * corIds.length} SKUs (${corIds.length} ${corIds.length === 1 ? 'cor' : 'cores'} × ${grade.tamanhos.length} tamanhos).`}</p>
        )}

        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" disabled={enviando} className={estilos.botaoPrimario}>{enviando ? 'Salvando...' : 'Salvar'}</button>
        </div>
      </form>
    </Dialogo>
  )
}

function DetalheDoProduto({ produto, podeEditar, onFechar, onEditar, onAtualizado }: {
  produto: Produto
  podeEditar: boolean
  onFechar: () => void
  onEditar: () => void
  onAtualizado: (p: Produto) => void
}) {
  const { getJson, enviar } = useApi()
  const [sku, setSku] = useState<Sku | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const recarregar = async () => onAtualizado(await getJson<Produto>(`/api/produtos/${produto.id}`))
  const tamanhos = produto.grade.tamanhos
  const corDe = (id: string) => produto.cores.find((c) => c.id === id)

  async function mudarSituacao() {
    try {
      await enviar('POST', `/api/produtos/${produto.id}/${produto.ativo ? 'inativar' : 'reativar'}`)
      await recarregar()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível concluir.')
    }
  }

  return (
    <Dialogo titulo={produto.descricao} onFechar={onFechar} largo>
      <dl className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
        {[
          ['Referência', produto.referencia],
          ['Preço', moeda(produto.precoVenda)],
          ['Custo', produto.precoCusto === null ? '—' : moeda(produto.precoCusto)],
          ['Markup', produto.markup === null ? '—' : decimal(produto.markup)],
          ['Marca', produto.marca ?? '—'],
          ['Categoria', produto.categoria ?? '—'],
          ['Coleção', produto.colecao ?? '—'],
          ['Situação', produto.ativo ? 'Ativo' : 'Inativo'],
        ].map(([rotulo, valor]) => (
          <div key={rotulo}>
            <dt className="text-xs text-muted">{rotulo}</dt>
            <dd className="font-semibold">{valor}</dd>
          </div>
        ))}
      </dl>

      <div className="mt-5 overflow-x-auto">
        <table aria-label={`Grade de ${produto.descricao}`} className="w-full border-separate border-spacing-1 text-sm">
          <thead>
            <tr>
              <th className="px-2 py-1 text-left text-xs text-muted uppercase">Cor</th>
              {tamanhos.map((t) => (
                <th key={t} className="px-2 py-1 text-center text-xs text-muted uppercase">{t}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {produto.cores.map((cor) => (
              <tr key={cor.id}>
                <th scope="row" className="px-2 py-1 text-left font-semibold whitespace-nowrap">
                  <span className="flex items-center gap-2">
                    <Amostra hex={cor.hex} />
                    {cor.nome}
                  </span>
                </th>
                {tamanhos.map((t) => {
                  const s = produto.skus.find((x) => x.corId === cor.id && x.tamanho === t)
                  if (!s) return <td key={t} />
                  const rotulo = `${cor.nome} ${t}: ${s.ativo ? moeda(s.preco).replace(/\s/g, ' ') : 'desativado'}`
                  const conteudo = (
                    <>
                      <span className={`block font-semibold tabular-nums ${s.precoVenda !== null ? 'text-primary' : ''}`}>{s.ativo ? moeda(s.preco) : '—'}</span>
                      <span className="block font-mono text-[11px] text-muted">…{s.codigoDeBarras.slice(-5)}</span>
                    </>
                  )
                  const classe = `w-full rounded-lg border px-2 py-1.5 text-center ${s.ativo ? 'border-border bg-surface' : 'border-dashed border-border bg-surface-2 text-muted'}`
                  return (
                    <td key={t}>
                      {podeEditar ? (
                        <button type="button" aria-label={rotulo} onClick={() => setSku(s)} className={`${classe} hover:border-primary`}>
                          {conteudo}
                        </button>
                      ) : (
                        <div aria-label={rotulo} className={classe}>{conteudo}</div>
                      )}
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="mt-2 text-xs text-muted">Preço em destaque = preço próprio do SKU. Clique numa célula para trocar o código de barras, o preço ou desativar a combinação.</p>

      {erro && <p role="alert" className={`mt-3 ${estilos.erro}`}>{erro}</p>}
      {podeEditar && (
        <div className="mt-5 flex flex-wrap justify-end gap-2">
          <button type="button" onClick={() => void mudarSituacao()} className={estilos.botao}>{produto.ativo ? 'Inativar' : 'Reativar'}</button>
          <button type="button" onClick={onEditar} className={estilos.botaoPrimario}>Editar produto</button>
        </div>
      )}

      {sku && (
        <EdicaoDeSku
          titulo={`${corDe(sku.corId)?.nome ?? ''} · ${sku.tamanho}`}
          produtoId={produto.id}
          sku={sku}
          onFechar={() => setSku(null)}
          onSalvo={() => {
            setSku(null)
            void recarregar()
          }}
        />
      )}
    </Dialogo>
  )
}

function EdicaoDeSku({ titulo, produtoId, sku, onFechar, onSalvo }: { titulo: string; produtoId: string; sku: Sku; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [codigo, setCodigo] = useState(sku.codigoDeBarras)
  const [preco, setPreco] = useState(sku.precoVenda === null ? '' : decimal(sku.precoVenda))
  const [ativo, setAtivo] = useState(sku.ativo)
  const [erro, setErro] = useState<string | null>(null)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    try {
      await enviar('PUT', `/api/produtos/${produtoId}/skus/${sku.id}`, { codigoDeBarras: codigo, precoVenda: lerNumero(preco), ativo })
      onSalvo()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível salvar.')
    }
  }

  return (
    <Dialogo titulo={titulo} onFechar={onFechar}>
      <form onSubmit={(e) => void salvar(e)} className="space-y-4">
        <Campo rotulo="Código de barras">
          <input value={codigo} onChange={(e) => setCodigo(e.target.value)} inputMode="numeric" className={`${estilos.campo} font-mono`} />
        </Campo>
        <p className="text-xs text-muted">Use o EAN da etiqueta do fornecedor, se houver. Códigos começando com 2 são internos da loja.</p>
        <Campo rotulo="Preço próprio (R$, opcional)">
          <input value={preco} onChange={(e) => setPreco(e.target.value)} inputMode="decimal" placeholder="Vazio = preço do produto" className={estilos.campo} />
        </Campo>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} className="size-4 accent-primary" />
          Combinação ativa (desmarque se essa cor/tamanho não existe)
        </label>
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" className={estilos.botaoPrimario}>Salvar</button>
        </div>
      </form>
    </Dialogo>
  )
}
