import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
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
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { dataBr, hojeIso } from '../financeiro/tipos'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { inicioDoMes, PERFIS_DE_GESTAO, porcentagem, PERFIS_QUE_CONSULTAM, PERFIS_QUE_VENDEM, type LinhaDeVenda, type ListaDeVendas, type Venda } from './tipos'
import { useVendedores } from './useVendas'

function SeloDaVenda({ status }: { status: Venda['status'] }) {
  return status === 'Cancelada' ? (
    <span className="rounded-full bg-danger-soft px-2 py-0.5 text-xs font-semibold text-danger">Cancelada</span>
  ) : (
    <span className="rounded-full bg-success-soft px-2 py-0.5 text-xs font-semibold text-success">Concluída</span>
  )
}

/** Filtros padrão: o mês corrente (a ordem das chaves é a da URL). */
const filtrosDoMes = () => ({ de: inicioDoMes(hojeIso()), ate: hojeIso(), status: 'todas', vendedorId: '', busca: '' })

/** Vendas do período no padrão de grade: totais de todas as filtradas, detalhe, cancelamento com motivo e exportação (o Vendedor vê só as suas). */
export function VendasPage() {
  const { perfilAtual } = useEmpresa()
  const veTudo = PERFIS_QUE_CONSULTAM.includes(perfilAtual)
  const permitido = PERFIS_QUE_VENDEM.includes(perfilAtual) || veTudo
  const podeCancelar = PERFIS_DE_GESTAO.includes(perfilAtual)
  const vendedores = useVendedores()
  const [padrao] = useState(filtrosDoMes)
  const grade = useGrade(padrao)
  const [versao, setVersao] = useState(0)
  const { dados: lista, erro } = useListaPaginada<ListaDeVendas>(permitido ? `/api/vendas?${grade.consulta}` : null, versao, grade.filtros.busca ? 250 : 0)
  const [detalhe, setDetalhe] = useState<string | null>(null)
  const [cancelar, setCancelar] = useState<LinhaDeVenda | null>(null)

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Vendas realizadas</h1>
        <p className={`mt-4 ${estilos.erro}`}>Seu perfil não acessa as vendas.</p>
      </div>
    )

  const colunas: Coluna<LinhaDeVenda>[] = [
    {
      titulo: 'Nº',
      celula: (v) => <button type="button" onClick={() => setDetalhe(v.id)} className="font-semibold text-primary hover:underline">{String(v.numero)}</button>,
    },
    { titulo: 'Data', celula: (v) => <span className="whitespace-nowrap">{dataBr(v.data)}</span> },
    { titulo: 'Cliente', celula: (v) => v.clienteNome ?? '—' },
    { titulo: 'Vendedor', celula: (v) => v.vendedorNome ?? '—' },
    { titulo: 'Peças', celula: (v) => v.pecas, alinhar: 'direita' },
    { titulo: 'Pagamento', celula: (v) => v.formas },
    {
      titulo: 'Total',
      celula: (v) => <span className={`font-semibold ${v.status === 'Cancelada' ? 'line-through' : ''}`}>{moeda(v.total)}</span>,
      alinhar: 'direita',
    },
    { titulo: 'Situação', celula: (v) => <SeloDaVenda status={v.status} /> },
  ]

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Vendas"
        titulo="Vendas realizadas"
        descricao={veTudo ? 'Todas as vendas da empresa no período.' : 'As vendas que você registrou.'}
        acoes={
          <>
            <BotaoExportar relatorio="vendas" parametros={grade.parametros} selecionados={[...grade.selecionados]} />
            <Link to="/vendas/registro" className={estilos.botaoPrimario}>
              <Icone nome="plus" />
              Nova venda
            </Link>
          </>
        }
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <Campo rotulo="De" className="w-40">
          <input type="date" value={grade.filtros.de} onChange={(e) => grade.filtrar({ de: e.target.value })} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Até" className="w-40">
          <input type="date" value={grade.filtros.ate} onChange={(e) => grade.filtrar({ ate: e.target.value })} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Situação" className="w-40">
          <select value={grade.filtros.status} onChange={(e) => grade.filtrar({ status: e.target.value })} className={estilos.campo}>
            <option value="todas">Todas</option>
            <option value="concluidas">Concluídas</option>
            <option value="canceladas">Canceladas</option>
          </select>
        </Campo>
        {veTudo && (
          <Campo rotulo="Vendedor" className="w-48">
            <select value={grade.filtros.vendedorId} onChange={(e) => grade.filtrar({ vendedorId: e.target.value })} className={estilos.campo}>
              <option value="">Todos</option>
              {vendedores?.map((v) => (
                <option key={v.id} value={v.id}>{v.nome}</option>
              ))}
            </select>
          </Campo>
        )}
        <Campo rotulo="Busca" className="min-w-48 flex-1">
          <input value={grade.filtros.busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Nº, cliente ou nota" className={estilos.campo} />
        </Campo>
      </BarraDeFiltros>

      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {lista && (
        <dl className="mt-4 grid gap-3 sm:grid-cols-4">
          {[
            ['Vendas', String(lista.quantidade)],
            ['Peças', String(lista.pecas)],
            ['Total vendido', moeda(lista.total)],
            ['Ticket médio', moeda(lista.ticketMedio)],
          ].map(([rotulo, valor]) => (
            <div key={rotulo} className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
              <dt className="text-xs text-muted">{rotulo}</dt>
              <dd className="text-xl font-bold tabular-nums">{valor}</dd>
            </div>
          ))}
        </dl>
      )}

      <GradeDeRegistros
        rotulo="Vendas"
        colunas={colunas}
        itens={lista?.itens ?? (erro ? [] : null)}
        chave={(v) => v.id}
        nome={(v) => `venda nº ${v.numero}`}
        total={lista?.registros ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        apagada={(v) => v.status === 'Cancelada'}
        vazio="Nenhuma venda com esses filtros."
        acoes={(v) => (
          <>
            <AcaoDaLinha rotulo={`Ver venda nº ${v.numero}`} icone="eye" onClick={() => setDetalhe(v.id)} />
            {podeCancelar && v.status === 'Concluida' && (
              <AcaoDaLinha rotulo={`Cancelar venda nº ${v.numero}`} icone="x" perigo onClick={() => setCancelar(v)} />
            )}
          </>
        )}
      />

      {detalhe && <DetalheDaVenda id={detalhe} onFechar={() => setDetalhe(null)} onMudou={() => setVersao((x) => x + 1)} />}
      {cancelar && (
        <CancelarVenda
          venda={cancelar}
          onFechar={() => setCancelar(null)}
          onCancelada={() => {
            setCancelar(null)
            setVersao((x) => x + 1)
          }}
        />
      )}
    </div>
  )
}

/** Cancelamento direto da linha: pede o motivo (a API recusa sem ele). */
function CancelarVenda({ venda, onFechar, onCancelada }: { venda: LinhaDeVenda; onFechar: () => void; onCancelada: () => void }) {
  const { enviar } = useApi()
  const [motivo, setMotivo] = useState('')
  return (
    <Confirmacao
      titulo={`Cancelar venda nº ${venda.numero}`}
      rotulo="Cancelar venda"
      onCancelar={onFechar}
      onConfirmar={async () => {
        await enviar('POST', `/api/vendas/${venda.id}/cancelar`, { motivo })
        onCancelada()
      }}
    >
      <p className="mb-3 text-muted">{`As peças voltam ao estoque e o que entrou no caixa ou ficou a receber é estornado (${moeda(venda.total)}).`}</p>
      <Campo rotulo="Motivo do cancelamento">
        <input value={motivo} onChange={(e) => setMotivo(e.target.value)} maxLength={300} autoFocus className={estilos.campo} />
      </Campo>
    </Confirmacao>
  )
}

function DetalheDaVenda({ id, onFechar, onMudou }: { id: string; onFechar: () => void; onMudou: () => void }) {
  const { getJson, enviar } = useApi()
  const { perfilAtual } = useEmpresa()
  const [venda, setVenda] = useState<Venda | null>(null)
  const [cancelando, setCancelando] = useState(false)
  const [motivo, setMotivo] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    const controle = new AbortController()
    getJson<Venda>(`/api/vendas/${id}`, controle.signal)
      .then(setVenda)
      .catch((e: unknown) => {
        if (!controle.signal.aborted) setErro(e instanceof ErroDeApi ? e.message : 'Não foi possível abrir a venda.')
      })
    return () => controle.abort()
  }, [getJson, id])

  async function cancelar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      setVenda(await enviar<Venda>('POST', `/api/vendas/${id}/cancelar`, { motivo }))
      setCancelando(false)
      onMudou()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível cancelar.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={venda ? `Venda nº ${venda.numero}` : 'Venda'} onFechar={onFechar} largo>
      {erro && <p role="alert" className={`mb-3 ${estilos.erro}`}>{erro}</p>}
      {venda && (
        <div className="space-y-4 text-sm">
          <div className="flex flex-wrap items-center gap-2">
            <SeloDaVenda status={venda.status} />
            <span className="text-muted">{`${dataBr(venda.data)} · ${venda.vendedorNome ?? 'sem vendedor'}${venda.clienteNome ? ` · ${venda.clienteNome}` : ''}`}</span>
            {venda.notaExterna && <span className="text-muted">{`· ${venda.notaExterna}`}</span>}
          </div>
          {venda.status === 'Cancelada' && (
            <p className={estilos.erro}>{`Cancelada por ${venda.canceladaPor ?? 'Sistema'}: ${venda.motivoDoCancelamento ?? ''}`}</p>
          )}
          <table className="w-full">
            <thead className="text-left text-xs text-muted uppercase">
              <tr>
                <th className="py-1">Item</th>
                <th className="py-1 text-right">Qtd.</th>
                <th className="py-1 text-right">Preço</th>
                <th className="py-1 text-right">Desconto</th>
                <th className="py-1 text-right">Total</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {venda.itens.map((i) => (
                <tr key={i.skuId}>
                  <td className="py-1">{i.descricao}</td>
                  <td className="py-1 text-right tabular-nums">{i.quantidade}</td>
                  <td className="py-1 text-right tabular-nums">{moeda(i.precoUnitario)}</td>
                  <td className="py-1 text-right tabular-nums">{i.desconto ? moeda(i.desconto) : '—'}</td>
                  <td className="py-1 text-right tabular-nums">{moeda(i.total)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-1 rounded-xl bg-surface-2 p-4">
            <dt className="text-muted">Subtotal</dt><dd className="text-right tabular-nums">{moeda(venda.subtotal)}</dd>
            {venda.descontoGeral > 0 && (<><dt className="text-muted">Desconto na venda</dt><dd className="text-right tabular-nums">{`− ${moeda(venda.descontoGeral)}`}</dd></>)}
            <dt className="font-bold">Total</dt><dd className="text-right font-bold tabular-nums">{moeda(venda.total)}</dd>
            {venda.troco > 0 && (<><dt className="text-muted">Troco</dt><dd className="text-right tabular-nums">{moeda(venda.troco)}</dd></>)}
            <dt className="text-muted">{`Comissão (${porcentagem(venda.comissaoPercentual)})`}</dt><dd className="text-right tabular-nums">{moeda(venda.comissao)}</dd>
            {venda.taxaTotal > 0 && (<><dt className="text-muted">Taxas de cartão</dt><dd className="text-right tabular-nums">{moeda(venda.taxaTotal)}</dd></>)}
            {venda.lucroBruto !== null && (<><dt className="text-muted">Custo das peças</dt><dd className="text-right tabular-nums">{moeda(venda.custoTotal ?? 0)}</dd></>)}
            {venda.lucroBruto !== null && (<><dt className="text-muted">Lucro bruto</dt><dd className="text-right tabular-nums">{moeda(venda.lucroBruto)}</dd></>)}
          </dl>
          <div>
            <h3 className="mb-1 font-semibold">Pagamento</h3>
            <ul className="space-y-0.5">
              {venda.pagamentos.map((p, n) => (
                <li key={n} className="flex justify-between">
                  <span>{`${p.formaNome}${p.parcelas > 1 ? ` em ${p.parcelas}x` : ''}${p.taxaPercentual ? ` (taxa ${porcentagem(p.taxaPercentual)})` : ''}`}</span>
                  <span className="tabular-nums">{moeda(p.valor)}</span>
                </li>
              ))}
            </ul>
          </div>
          <p className="text-xs text-muted">{`Registrada por ${venda.registradaPor ?? 'Sistema'} em ${new Date(venda.registradaEm).toLocaleString('pt-BR')}`}</p>

          {venda.status === 'Concluida' && PERFIS_DE_GESTAO.includes(perfilAtual) && !cancelando && (
            <div className="flex justify-end">
              <button type="button" onClick={() => setCancelando(true)} className={`${estilos.botao} text-danger`}>Cancelar venda</button>
            </div>
          )}
          {cancelando && (
            <form onSubmit={cancelar} className="space-y-3 rounded-xl border border-danger p-4">
              <p className="text-muted">As peças voltam ao estoque e o que entrou no caixa ou ficou a receber é estornado.</p>
              <Campo rotulo="Motivo do cancelamento">
                <input value={motivo} onChange={(e) => setMotivo(e.target.value)} maxLength={300} autoFocus className={estilos.campo} />
              </Campo>
              <div className="flex justify-end gap-2">
                <button type="button" onClick={() => setCancelando(false)} className={estilos.botao}>Voltar</button>
                <button type="submit" disabled={!motivo.trim() || enviando} className={estilos.botaoPrimario}>Confirmar cancelamento</button>
              </div>
            </form>
          )}
        </div>
      )}
    </Dialogo>
  )
}
