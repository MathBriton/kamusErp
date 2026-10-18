import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { useLojas } from '../estoque/useLojas'
import { hojeIso } from '../financeiro/tipos'
import { EditorDePagamentos } from './EditorDePagamentos'
import { EntradaDeItens } from './EntradaDeItens'
import { calcularPagamentos, corpoDosPagamentos, inteiro, novaLinha, somaDosItens, valorDe, type ItemDigitado, type PagamentoDigitado } from './itens'
import { centavos, PERFIS_QUE_VENDEM, porcentagem, type Venda } from './tipos'
import { useFormas, useVendedores } from './useVendas'

interface PessoaResumo {
  id: string
  nome: string
}

/** Venda no backoffice: itens pelo leitor ou pela grade, descontos, vendedor, cliente e uma ou mais formas de pagamento (com troco). */
export function RegistroDeVendaPage() {
  const { getJson, enviar } = useApi()
  const { estado } = useAuth()
  const { perfilAtual, consolidado } = useEmpresa()
  const usuarioId = estado.status === 'autenticado' ? estado.sessao.usuario.id : null
  const lojas = useLojas()
  const ativas = lojas?.filter((l) => l.ativa) ?? []
  const vendedores = useVendedores()?.filter((v) => v.ativo) ?? []
  const formas = useFormas()?.filter((f) => f.ativa) ?? []
  const [lojaId, setLojaId] = useState('')
  const [vendedorId, setVendedorId] = useState<string | null>(null)
  const [cliente, setCliente] = useState<{ id: string | null; nome: string }>({ id: null, nome: '' })
  const [sugestoes, setSugestoes] = useState<PessoaResumo[]>([])
  const [data, setData] = useState(hojeIso())
  const [notaExterna, setNotaExterna] = useState('')
  const [observacao, setObservacao] = useState('')
  const [itens, setItens] = useState<ItemDigitado[]>([])
  const [descontoGeral, setDescontoGeral] = useState('')
  const [pagamentos, setPagamentos] = useState<PagamentoDigitado[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [registrada, setRegistrada] = useState<Venda | null>(null)

  const lojaEscolhida = lojaId || (ativas.length === 1 ? ativas[0]!.id : '')
  const vendedorDoUsuario = vendedores.find((v) => v.usuarioId === usuarioId)?.id ?? ''
  const vendedorEscolhido = vendedorId ?? vendedorDoUsuario
  const formaPadrao = formas.find((f) => f.tipo === 'Dinheiro') ?? formas[0]
  const linhasDePagamento: PagamentoDigitado[] = pagamentos.length > 0 || !formaPadrao ? pagamentos : [novaLinha(formaPadrao)]

  useEffect(() => {
    const termo = cliente.nome.trim()
    if (cliente.id || termo.length < 2) return
    const controle = new AbortController()
    const timer = window.setTimeout(() => {
      getJson<{ itens: PessoaResumo[] }>(`/api/pessoas?${new URLSearchParams({ papel: 'cliente', situacao: 'ativos', busca: termo })}`, controle.signal)
        .then((r) => setSugestoes(r.itens.slice(0, 6)))
        .catch(() => {})
    }, 250)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, cliente])

  const subtotal = somaDosItens(itens)
  const desconto = valorDe(descontoGeral)
  const total = centavos(subtotal - desconto)
  const { valores, falta } = calcularPagamentos(linhasDePagamento, total)

  if (!PERFIS_QUE_VENDEM.includes(perfilAtual) || consolidado)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Registrar venda</h1>
        <p className={`mt-4 ${estilos.erro}`}>
          {consolidado ? 'Escolha uma empresa no seletor do topo para registrar vendas.' : 'Só Dono, Gerente e Vendedor registram vendas.'}
        </p>
      </div>
    )

  async function registrar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      const venda = await enviar<Venda>('POST', '/api/vendas', {
        lojaId: lojaEscolhida,
        data,
        clienteId: cliente.id,
        clienteNome: cliente.nome.trim() || null,
        vendedorId: vendedorEscolhido || null,
        descontoGeral: desconto,
        notaExterna: notaExterna.trim() || null,
        observacao: observacao.trim() || null,
        itens: itens.map((i) => ({ skuId: i.skuId, quantidade: inteiro(i.quantidade), desconto: valorDe(i.desconto) })),
        pagamentos: corpoDosPagamentos(linhasDePagamento, valores),
      })
      setRegistrada(venda)
      setItens([])
      setPagamentos([])
      setDescontoGeral('')
      setCliente({ id: null, nome: '' })
      setNotaExterna('')
      setObservacao('')
      setData(hojeIso())
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível registrar a venda. Tente de novo.')
    } finally {
      setEnviando(false)
    }
  }

  const podeRegistrar = itens.length > 0 && itens.every((i) => inteiro(i.quantidade) > 0) && !!lojaEscolhida && falta <= 0 && total >= 0 && linhasDePagamento.length > 0

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Vendas</p>
      <h1 className="text-2xl font-bold tracking-tight">Registrar venda</h1>
      <p className="mt-1 text-sm text-muted">A nota fiscal continua saindo no sistema atual da loja: aqui ficam o estoque, o caixa, os recebíveis e a comissão.</p>

      {registrada && (
        <div role="status" className="mt-4 space-y-1 rounded-lg bg-success-soft px-3 py-2.5 text-sm text-success">
          <p className="flex flex-wrap items-center gap-2 font-semibold">
            <Icone nome="check" />
            {`Venda nº ${registrada.numero} registrada: ${moeda(registrada.total)}`}
            {registrada.troco > 0 && <span>{`· Troco: ${moeda(registrada.troco)}`}</span>}
            <Link to="/vendas/realizadas" className="font-normal underline">Ver vendas realizadas</Link>
          </p>
          {registrada.avisos.map((a) => (
            <p key={a} className="text-warning">{a}</p>
          ))}
        </div>
      )}

      <form onSubmit={registrar} className="mt-6 space-y-6">
        <section className="grid gap-4 rounded-2xl border border-border bg-surface p-5 shadow-sm sm:grid-cols-3">
          <Campo rotulo="Loja">
            <select value={lojaEscolhida} onChange={(e) => setLojaId(e.target.value)} disabled={itens.length > 0} className={estilos.campo}>
              <option value="">Escolha...</option>
              {ativas.map((l) => (
                <option key={l.id} value={l.id}>{l.nome}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Vendedor">
            <select value={vendedorEscolhido} onChange={(e) => setVendedorId(e.target.value)} className={estilos.campo}>
              <option value="">Sem vendedor</option>
              {vendedores.map((v) => (
                <option key={v.id} value={v.id}>{`${v.nome} (${porcentagem(v.comissaoPercentual)})`}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Data">
            <input type="date" value={data} max={hojeIso()} onChange={(e) => setData(e.target.value)} className={estilos.campo} />
          </Campo>
          <div className="relative sm:col-span-2">
            <Campo rotulo="Cliente (opcional)">
              <input
                value={cliente.nome}
                onChange={(e) => {
                  setCliente({ id: null, nome: e.target.value })
                  if (e.target.value.trim().length < 2) setSugestoes([])
                }}
                maxLength={150}
                placeholder="Buscar no cadastro ou digitar o nome"
                className={estilos.campo}
              />
            </Campo>
            {sugestoes.length > 0 && !cliente.id && (
              <ul className="absolute z-10 mt-1 w-full overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
                {sugestoes.map((p) => (
                  <li key={p.id}>
                    <button
                      type="button"
                      onClick={() => {
                        setCliente({ id: p.id, nome: p.nome })
                        setSugestoes([])
                      }}
                      className="w-full px-3 py-2 text-left text-sm hover:bg-surface-2"
                    >
                      {p.nome}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
          <Campo rotulo="Nota externa (opcional)">
            <input value={notaExterna} onChange={(e) => setNotaExterna(e.target.value)} maxLength={60} placeholder="Ex.: NFC-e 1234" className={estilos.campo} />
          </Campo>
        </section>

        <section className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
          <h2 className="font-semibold">Itens</h2>
          <EntradaDeItens lojaId={lojaEscolhida} itens={itens} onItens={setItens} onErro={setErro} rotulo="Itens da venda" botaoDaGrade="Adicionar à venda" />
        </section>

        {itens.length > 0 && (
          <section className="grid gap-6 rounded-2xl border border-border bg-surface p-5 shadow-sm md:grid-cols-2">
            <div className="space-y-3">
              <h2 className="font-semibold">Pagamento</h2>
              <EditorDePagamentos formas={formas} total={total} linhas={linhasDePagamento} onLinhas={setPagamentos} />
              <Campo rotulo="Observação (opcional)">
                <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={500} className={estilos.campo} />
              </Campo>
            </div>

            <div className="space-y-3">
              <Campo rotulo="Desconto na venda (R$)" className="max-w-48">
                <input value={descontoGeral} onChange={(e) => setDescontoGeral(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
              </Campo>
              <dl className="space-y-1 rounded-xl bg-surface-2 p-4 text-sm">
                {subtotal !== total && (
                  <>
                    <div className="flex justify-between"><dt className="text-muted">Subtotal</dt><dd className="tabular-nums">{moeda(subtotal)}</dd></div>
                    <div className="flex justify-between"><dt className="text-muted">Desconto</dt><dd className="tabular-nums">{`− ${moeda(desconto)}`}</dd></div>
                  </>
                )}
                <div className="flex justify-between text-lg font-bold"><dt>Total</dt><dd className="tabular-nums">{moeda(total)}</dd></div>
              </dl>
              {falta > 0 && <p className="font-semibold text-danger">{`Falta: ${moeda(falta)}`}</p>}
              {falta < 0 && <p className="font-semibold text-success">{`Troco: ${moeda(-falta)}`}</p>}
            </div>
          </section>
        )}

        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}

        <div className="flex justify-end">
          <button type="submit" disabled={!podeRegistrar || enviando} className={estilos.botaoPrimario}>
            {enviando ? 'Registrando...' : 'Registrar venda'}
          </button>
        </div>
      </form>
    </div>
  )
}
