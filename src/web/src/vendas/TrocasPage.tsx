import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { dataBr, hojeIso } from '../financeiro/tipos'
import { EditorDePagamentos } from './EditorDePagamentos'
import { EntradaDeItens } from './EntradaDeItens'
import { calcularPagamentos, corpoDosPagamentos, inteiro, novaLinha, somaDosItens, valorDe, type ItemDigitado, type PagamentoDigitado } from './itens'
import {
  centavos, inicioDoMes, MOTIVOS_DA_TROCA, PERFIS_DE_GESTAO, PERFIS_QUE_VENDEM,
  type Devolviveis, type LinhaDeTroca, type ListaDeVendas, type MotivoDaTroca, type Troca,
} from './tipos'
import { useFormas } from './useVendas'

type Destino = 'Estoque' | 'Avariado'

/** Troca vinculada à venda: escolhe as peças devolvidas, as levadas e acerta a diferença (pagamento ou vale-troca). */
export function TrocasPage() {
  const { getJson, enviar } = useApi()
  const { perfilAtual, consolidado } = useEmpresa()
  const formas = useFormas()?.filter((f) => f.ativa) ?? []
  const [numero, setNumero] = useState('')
  const [venda, setVenda] = useState<Devolviveis | null>(null)
  const [quantidades, setQuantidades] = useState<Record<string, string>>({})
  const [destinos, setDestinos] = useState<Record<string, Destino>>({})
  const [motivo, setMotivo] = useState<MotivoDaTroca>('Tamanho')
  const [observacao, setObservacao] = useState('')
  const [foraDoPrazo, setForaDoPrazo] = useState(false)
  const [levados, setLevados] = useState<ItemDigitado[]>([])
  const [pagamentos, setPagamentos] = useState<PagamentoDigitado[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [registrada, setRegistrada] = useState<Troca | null>(null)
  const [versao, setVersao] = useState(0)
  const gestao = PERFIS_DE_GESTAO.includes(perfilAtual)

  const devolvido = centavos((venda?.itens ?? []).reduce((s, i) => s + Math.min(inteiro(quantidades[i.skuId] ?? ''), i.disponiveis) * i.valorPorPeca, 0))
  const levado = somaDosItens(levados)
  const diferenca = centavos(levado - devolvido)
  const formaPadrao = formas.find((f) => f.tipo === 'Dinheiro') ?? formas[0]
  const linhas = pagamentos.length > 0 || !formaPadrao ? pagamentos : [novaLinha(formaPadrao)]
  const { valores, falta } = calcularPagamentos(linhas, Math.max(0, diferenca))

  if (!PERFIS_QUE_VENDEM.includes(perfilAtual) || consolidado)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Trocas</h1>
        <p className={`mt-4 ${estilos.erro}`}>
          {consolidado ? 'Escolha uma empresa no seletor do topo para registrar trocas.' : 'Só Dono, Gerente e Vendedor registram trocas.'}
        </p>
      </div>
    )

  const limpar = () => {
    setVenda(null)
    setQuantidades({})
    setDestinos({})
    setMotivo('Tamanho')
    setObservacao('')
    setForaDoPrazo(false)
    setLevados([])
    setPagamentos([])
  }

  async function buscar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    limpar()
    const n = numero.trim()
    if (!n) return
    try {
      const lista = await getJson<ListaDeVendas>(`/api/vendas?${new URLSearchParams({ status: 'concluidas', busca: n })}`)
      const achada = lista.itens.find((v) => String(v.numero) === n)
      if (!achada) {
        setErro(`Venda nº ${n} não encontrada (ou cancelada).`)
        return
      }
      setVenda(await getJson<Devolviveis>(`/api/vendas/${achada.id}/devolviveis`))
      setRegistrada(null)
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível buscar a venda.')
    }
  }

  async function registrar(e: FormEvent) {
    e.preventDefault()
    if (!venda) return
    setErro(null)
    setEnviando(true)
    try {
      const troca = await enviar<Troca>('POST', '/api/vendas/trocas', {
        vendaId: venda.vendaId,
        motivo,
        observacao: observacao.trim() || null,
        foraDoPrazo,
        devolvidos: venda.itens
          .filter((i) => inteiro(quantidades[i.skuId] ?? '') > 0)
          .map((i) => ({ skuId: i.skuId, quantidade: inteiro(quantidades[i.skuId] ?? ''), destino: destinos[i.skuId] ?? 'Estoque' })),
        levados: levados.map((i) => ({ skuId: i.skuId, quantidade: inteiro(i.quantidade), desconto: valorDe(i.desconto) })),
        pagamentos: diferenca > 0 ? corpoDosPagamentos(linhas, valores) : [],
      })
      setRegistrada(troca)
      limpar()
      setNumero('')
      setVersao((v) => v + 1)
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível registrar a troca. Tente de novo.')
    } finally {
      setEnviando(false)
    }
  }

  const algumaDevolvida = (venda?.itens ?? []).some((i) => inteiro(quantidades[i.skuId] ?? '') > 0)
  const prazoOk = !venda || venda.dentroDoPrazo || foraDoPrazo
  const podeRegistrar = !!venda && algumaDevolvida && prazoOk && (diferenca <= 0 || falta <= 0) && levados.every((i) => inteiro(i.quantidade) > 0)

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Vendas</p>
      <h1 className="text-2xl font-bold tracking-tight">Trocas</h1>
      <p className="mt-1 text-sm text-muted">
        Peças devolvidas valem o que o cliente pagou (com o desconto da venda). Se ele leva menos, a diferença vira vale-troca; se leva mais, paga a diferença.
      </p>

      {registrada && <TrocaRegistrada troca={registrada} />}

      <form onSubmit={buscar} className="mt-6 flex max-w-md items-end gap-2">
        <Campo rotulo="Nº da venda" className="flex-1">
          <input value={numero} onChange={(e) => setNumero(e.target.value.replace(/\D/g, ''))} inputMode="numeric" placeholder="Ex.: 128" className={estilos.campo} />
        </Campo>
        <button type="submit" className={estilos.botao}>Buscar venda</button>
      </form>

      {erro && !venda && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {venda && (
        <form onSubmit={registrar} className="mt-6 space-y-6">
          <section className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
            <h2 className="font-semibold">{`Venda nº ${venda.numero}`}</h2>
            <p className="mt-1 text-sm text-muted">
              {`${dataBr(venda.data)}${venda.clienteNome ? ` · ${venda.clienteNome}` : ''}${venda.vendedorNome ? ` · ${venda.vendedorNome}` : ''} · há ${venda.diasDesdeAVenda} dias · prazo de ${venda.prazoDeTrocaDias} dias`}
            </p>
            {!venda.dentroDoPrazo && (
              <div className="mt-3 rounded-lg bg-warning-soft px-3 py-2.5 text-sm text-warning">
                <p>{`Fora do prazo: a venda foi há ${venda.diasDesdeAVenda} dias e o prazo de troca é de ${venda.prazoDeTrocaDias} dias.`}</p>
                {gestao ? (
                  <label className="mt-2 flex items-center gap-2 font-semibold">
                    <input type="checkbox" checked={foraDoPrazo} onChange={(e) => setForaDoPrazo(e.target.checked)} className="h-4 w-4" />
                    Aceitar fora do prazo
                  </label>
                ) : (
                  <p className="mt-1">Só Dono ou Gerente podem aceitar.</p>
                )}
              </div>
            )}

            <table aria-label="Peças da venda" className="mt-4 w-full text-sm">
              <thead className="text-left text-xs text-muted uppercase">
                <tr>
                  <th className="py-2">Peça</th>
                  <th className="py-2 text-right">Vendidas</th>
                  <th className="py-2 text-right">Valor pago/peça</th>
                  <th className="w-24 py-2 text-right">Devolver</th>
                  <th className="w-44 py-2 pl-3">Destino</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {venda.itens.map((i) => (
                  <tr key={i.skuId} className={i.disponiveis === 0 ? 'text-muted' : ''}>
                    <td className="py-2 font-semibold">
                      {i.descricao}
                      {i.jaDevolvidas > 0 && <span className="block text-xs font-normal text-muted">{`${i.jaDevolvidas} já devolvida(s)`}</span>}
                    </td>
                    <td className="py-2 text-right tabular-nums">{i.vendidas}</td>
                    <td className="py-2 text-right tabular-nums">{moeda(i.valorPorPeca)}</td>
                    <td className="py-2 pl-2">
                      <input
                        aria-label={`Devolver ${i.descricao}`}
                        value={quantidades[i.skuId] ?? ''}
                        onChange={(e) => setQuantidades((q) => ({ ...q, [i.skuId]: e.target.value.replace(/\D/g, '') }))}
                        disabled={i.disponiveis === 0}
                        inputMode="numeric"
                        placeholder={`até ${i.disponiveis}`}
                        className="h-9 w-full rounded-lg border border-border bg-surface px-2 text-right tabular-nums disabled:bg-surface-2"
                      />
                    </td>
                    <td className="py-2 pl-3">
                      <select
                        aria-label={`Destino de ${i.descricao}`}
                        value={destinos[i.skuId] ?? 'Estoque'}
                        onChange={(e) => setDestinos((d) => ({ ...d, [i.skuId]: e.target.value as Destino }))}
                        className="h-9 w-full rounded-lg border border-border bg-surface px-2"
                      >
                        <option value="Estoque">Volta ao estoque</option>
                        <option value="Avariado">Avariada (não volta)</option>
                      </select>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div className="mt-4 grid gap-4 sm:grid-cols-3">
              <Campo rotulo="Motivo">
                <select value={motivo} onChange={(e) => setMotivo(e.target.value as MotivoDaTroca)} className={estilos.campo}>
                  {MOTIVOS_DA_TROCA.map((m) => (
                    <option key={m.valor} value={m.valor}>{m.rotulo}</option>
                  ))}
                </select>
              </Campo>
              <Campo rotulo="Observação (opcional)" className="sm:col-span-2">
                <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={500} className={estilos.campo} />
              </Campo>
            </div>
          </section>

          <section className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
            <h2 className="font-semibold">Peças levadas (opcional)</h2>
            <EntradaDeItens lojaId={venda.lojaId} itens={levados} onItens={setLevados} onErro={setErro} rotulo="Peças levadas" botaoDaGrade="Adicionar à troca" />
          </section>

          <section className="grid gap-6 rounded-2xl border border-border bg-surface p-5 shadow-sm md:grid-cols-2">
            <dl className="space-y-1 text-sm">
              <div className="flex justify-between"><dt className="text-muted">Devolvido</dt><dd className="tabular-nums">{moeda(devolvido)}</dd></div>
              <div className="flex justify-between"><dt className="text-muted">Levado</dt><dd className="tabular-nums">{moeda(levado)}</dd></div>
              <p className={`pt-2 text-base font-bold ${diferenca > 0 ? 'text-foreground' : 'text-success'}`}>
                {diferenca > 0 ? `Cliente paga a diferença: ${moeda(diferenca)}` : diferenca < 0 ? `Vale-troca para o cliente: ${moeda(-diferenca)}` : 'Sem diferença de valor'}
              </p>
            </dl>
            {diferenca > 0 && (
              <div className="space-y-2">
                <h3 className="font-semibold">Pagamento da diferença</h3>
                <EditorDePagamentos formas={formas} total={diferenca} linhas={linhas} onLinhas={setPagamentos} />
                {falta > 0 && <p className="font-semibold text-danger">{`Falta: ${moeda(falta)}`}</p>}
                {falta < 0 && <p className="font-semibold text-success">{`Troco: ${moeda(-falta)}`}</p>}
              </div>
            )}
          </section>

          {erro && <p role="alert" className={estilos.erro}>{erro}</p>}

          <div className="flex justify-end gap-2">
            <button type="button" onClick={limpar} className={estilos.botao}>Cancelar</button>
            <button type="submit" disabled={!podeRegistrar || enviando} className={estilos.botaoPrimario}>
              {enviando ? 'Registrando...' : 'Registrar troca'}
            </button>
          </div>
        </form>
      )}

      <TrocasDoMes versao={versao} />
    </div>
  )
}

function TrocaRegistrada({ troca }: { troca: Troca }) {
  return (
    <div role="status" className="mt-4 space-y-2 rounded-lg bg-success-soft px-4 py-3 text-sm text-success">
      <p className="flex flex-wrap items-center gap-2 font-semibold">
        <Icone nome="check" />
        {`Troca nº ${troca.numero} registrada`}
        {troca.diferenca > 0 && <span>{`· diferença paga ${moeda(troca.diferenca)}`}</span>}
        {troca.troco > 0 && <span>{`· troco ${moeda(troca.troco)}`}</span>}
      </p>
      {troca.vale && (
        <div className="flex flex-wrap items-center gap-3 rounded-lg border border-success bg-surface px-3 py-2 text-foreground">
          <span>Vale-troca</span>
          <span className="font-mono text-lg font-bold tracking-wider">{troca.vale.codigo}</span>
          <span>{`${moeda(troca.vale.valor)} · válido até ${dataBr(troca.vale.validoAte)}`}</span>
          <button type="button" onClick={() => window.print()} className={estilos.botao}>Imprimir vale</button>
        </div>
      )}
      {troca.avisos.map((a) => (
        <p key={a} className="text-warning">{a}</p>
      ))}
    </div>
  )
}

function TrocasDoMes({ versao }: { versao: number }) {
  const { getJson } = useApi()
  const [trocas, setTrocas] = useState<LinhaDeTroca[] | null>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<LinhaDeTroca[]>(`/api/vendas/trocas?${new URLSearchParams({ de: inicioDoMes(hojeIso()), ate: hojeIso() })}`, controle.signal)
      .then(setTrocas)
      .catch(() => {
        if (!controle.signal.aborted) setTrocas([])
      })
    return () => controle.abort()
  }, [getJson, versao])

  if (!trocas || trocas.length === 0) return null
  return (
    <section className="mt-8">
      <div className="flex items-center justify-between">
        <h2 className="font-semibold">Trocas do mês</h2>
        <Link to="/vendas/vales-troca" className="text-sm text-primary hover:underline">Ver vales-troca</Link>
      </div>
      <div className="mt-3 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
        <table aria-label="Trocas do mês" className="w-full text-sm">
          <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
            <tr>
              <th className="px-4 py-2">Nº</th>
              <th className="px-4 py-2">Data</th>
              <th className="px-4 py-2">Venda</th>
              <th className="px-4 py-2">Cliente</th>
              <th className="px-4 py-2">Motivo</th>
              <th className="px-4 py-2 text-right">Devolvido</th>
              <th className="px-4 py-2 text-right">Levado</th>
              <th className="px-4 py-2">Vale</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {trocas.map((t) => (
              <tr key={t.id}>
                <td className="px-4 py-2 font-semibold">{t.numero}</td>
                <td className="px-4 py-2">{dataBr(t.data)}</td>
                <td className="px-4 py-2">{`nº ${t.vendaNumero}`}</td>
                <td className="px-4 py-2">{t.clienteNome ?? '—'}</td>
                <td className="px-4 py-2">{MOTIVOS_DA_TROCA.find((m) => m.valor === t.motivo)?.rotulo ?? t.motivo}</td>
                <td className="px-4 py-2 text-right tabular-nums">{moeda(t.valorDevolvido)}</td>
                <td className="px-4 py-2 text-right tabular-nums">{moeda(t.valorLevado)}</td>
                <td className="px-4 py-2 font-mono">{t.valeCodigo ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
