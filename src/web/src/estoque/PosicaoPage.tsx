import { useState } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'
import { moeda } from '../cadastros/catalogo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade, useListaPaginada } from '../components/grade/useGrade'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { Kardex } from './Kardex'
import { MatrizDeEstoque } from './MatrizDeEstoque'
import { pecas, PERFIS_QUE_MOVIMENTAM, PERFIS_QUE_VEEM_CUSTO, type Celula, type ItemDaPosicao, type Matriz, type Posicao } from './tipos'
import { useLojas } from './useLojas'

const FILTROS = { lojaId: '', busca: '' }

/** Posição de estoque por produto em grade paginada; "Ver" abre a matriz cor × tamanho e, numa célula, o kardex do SKU. */
export function PosicaoPage() {
  const { getJson } = useApi()
  const { perfilAtual } = useEmpresa()
  const veCusto = PERFIS_QUE_VEEM_CUSTO.includes(perfilAtual)
  const movimenta = PERFIS_QUE_MOVIMENTAM.includes(perfilAtual)
  const lojas = useLojas()
  const grade = useGrade(FILTROS)
  const { lojaId, busca } = grade.filtros
  const { dados: posicao, erro } = useListaPaginada<Posicao>(`/api/estoque/posicao?${grade.consulta}`, 0, busca ? 250 : 0)
  const [matriz, setMatriz] = useState<Matriz | null>(null)
  const [celula, setCelula] = useState<Celula | null>(null)

  const abrir = (produtoId: string) => {
    getJson<Matriz>(`/api/estoque/posicao/${produtoId}${lojaId ? `?lojaId=${lojaId}` : ''}`)
      .then(setMatriz)
      .catch(() => {})
  }

  const fechar = () => {
    setMatriz(null)
    setCelula(null)
  }

  const colunas: Coluna<ItemDaPosicao>[] = [
    { titulo: 'Referência', celula: (i) => <span className="font-mono text-xs">{i.referencia}</span> },
    { titulo: 'Descrição', celula: (i) => <span className="font-semibold">{i.descricao}</span> },
    { titulo: 'Peças', celula: (i) => <span className={i.quantidade < 0 ? 'text-danger' : ''}>{i.quantidade}</span>, alinhar: 'direita' },
    ...(veCusto ? [{ titulo: 'Valor em custo', celula: (i: ItemDaPosicao) => (i.valorEmCusto === null ? '—' : moeda(i.valorEmCusto)), alinhar: 'direita' as const }] : []),
    { titulo: 'Valor em venda', celula: (i) => moeda(i.valorEmVenda), alinhar: 'direita' },
  ]

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Estoque"
        titulo="Posição de estoque"
        descricao="Saldo por produto. Use “Ver” para a grade cor × tamanho."
        acoes={
          <>
            {veCusto && <BotaoExportar relatorio="posicao-de-estoque" parametros={grade.parametros} selecionados={[...grade.selecionados]} />}
            {movimenta && (
              <Link to="/estoque/entradas" className={estilos.botaoPrimario}>
                Entrada de mercadoria
              </Link>
            )}
          </>
        }
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="min-w-60 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input value={busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Referência, descrição ou código de barras" className={estilos.campo} />
        </label>
        <label className="w-56 text-sm">
          <span className="mb-1 block font-semibold">Loja</span>
          <select value={lojaId} onChange={(e) => grade.filtrar({ lojaId: e.target.value })} className={estilos.campo}>
            <option value="">Todas as lojas</option>
            {lojas?.map((l) => (
              <option key={l.id} value={l.id}>{l.nome}</option>
            ))}
          </select>
        </label>
      </BarraDeFiltros>

      {erro && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {posicao && (
        <dl className="mt-4 grid gap-3 sm:grid-cols-3">
          <Indicador rotulo="Peças em estoque" valor={posicao.totalDePecas.toLocaleString('pt-BR')} />
          {posicao.valorEmCusto !== null && <Indicador rotulo="Valor em custo" valor={moeda(posicao.valorEmCusto)} />}
          <Indicador rotulo="Valor em venda" valor={moeda(posicao.valorEmVenda)} />
        </dl>
      )}

      <GradeDeRegistros
        rotulo="Posição de estoque"
        colunas={colunas}
        itens={posicao?.itens ?? (erro ? [] : null)}
        chave={(i) => i.produtoId}
        nome={(i) => i.descricao}
        total={posicao?.total ?? 0}
        {...grade.paginacao}
        selecao={veCusto ? grade.selecao : undefined}
        vazio={grade.ativos ? 'Nenhum produto com estoque nesses filtros.' : 'Nenhum produto com estoque.'}
        acoes={(i) => <AcaoDaLinha rotulo={`Ver ${i.descricao}`} icone="eye" onClick={() => abrir(i.produtoId)} />}
      />
      {posicao?.total === 0 && movimenta && !grade.ativos && (
        <p className="mt-2 px-1 text-sm text-muted">
          Lance uma <Link to="/estoque/entradas" className="text-primary underline">entrada de mercadoria</Link> ou importe o{' '}
          <Link to="/cadastros/importacao?tipo=estoque-inicial" className="text-primary underline">estoque inicial por planilha</Link>.
        </p>
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
