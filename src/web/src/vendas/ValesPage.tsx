import { useState } from 'react'
import { useApi } from '../api/useApi'
import { moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { Confirmacao } from '../components/grade/Confirmacao'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade, useListaPaginada } from '../components/grade/useGrade'
import { useEmpresa } from '../empresa/EmpresaContext'
import { dataBr } from '../financeiro/tipos'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { PERFIS_DE_GESTAO, PERFIS_QUE_CONSULTAM, PERFIS_QUE_VENDEM, type ListaDeVales, type ValeTroca } from './tipos'

const TOM: Record<ValeTroca['situacao'], string> = {
  Ativo: 'bg-success-soft text-success',
  Usado: 'bg-surface-2 text-muted',
  Vencido: 'bg-warning-soft text-warning',
  Cancelado: 'bg-danger-soft text-danger',
}

const FILTROS = { situacao: 'ativos', busca: '' }

/** Vales-troca emitidos nas trocas, no padrão de grade: saldo, validade e situação; Dono e Gerente cancelam com motivo. */
export function ValesPage() {
  const { perfilAtual } = useEmpresa()
  const permitido = PERFIS_QUE_VENDEM.includes(perfilAtual) || PERFIS_QUE_CONSULTAM.includes(perfilAtual)
  const gestao = PERFIS_DE_GESTAO.includes(perfilAtual)
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { dados: lista, erro } = useListaPaginada<ListaDeVales>(permitido ? `/api/vendas/vales?${grade.consulta}` : null, versao, grade.filtros.busca ? 250 : 0)
  const [cancelando, setCancelando] = useState<ValeTroca | null>(null)

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Vales-troca</h1>
        <p className={`mt-4 ${estilos.erro}`}>Seu perfil não acessa os vales-troca.</p>
      </div>
    )

  const colunas: Coluna<ValeTroca>[] = [
    { titulo: 'Código', celula: (v) => <span className="font-mono font-semibold">{v.codigo}</span> },
    { titulo: 'Cliente', celula: (v) => v.clienteNome ?? '—' },
    { titulo: 'Emitido', celula: (v) => dataBr(v.emitidoEm) },
    { titulo: 'Válido até', celula: (v) => dataBr(v.validoAte) },
    { titulo: 'Valor', celula: (v) => moeda(v.valor), alinhar: 'direita' },
    { titulo: 'Saldo', celula: (v) => <span className="font-semibold">{moeda(v.saldo)}</span>, alinhar: 'direita' },
    { titulo: 'Situação', celula: (v) => <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${TOM[v.situacao]}`}>{v.situacao}</span> },
  ]

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Vendas"
        titulo="Vales-troca"
        descricao="Crédito do cliente gerado nas trocas. É aceito como forma de pagamento, em uma ou mais compras, até a validade."
        acoes={<BotaoExportar relatorio="vales-troca" parametros={grade.parametros} selecionados={[...grade.selecionados]} />}
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <Campo rotulo="Situação" className="w-40">
          <select value={grade.filtros.situacao} onChange={(e) => grade.filtrar({ situacao: e.target.value })} className={estilos.campo}>
            <option value="ativos">Ativos</option>
            <option value="usados">Usados</option>
            <option value="vencidos">Vencidos</option>
            <option value="cancelados">Cancelados</option>
            <option value="todos">Todos</option>
          </select>
        </Campo>
        <Campo rotulo="Busca" className="min-w-48 flex-1">
          <input value={grade.filtros.busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Código ou cliente" className={estilos.campo} />
        </Campo>
      </BarraDeFiltros>

      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {lista && (
        <dl className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
            <dt className="text-xs text-muted">Saldo em vales ativos</dt>
            <dd className="text-xl font-bold tabular-nums">{moeda(lista.saldoAtivo)}</dd>
          </div>
          <div className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
            <dt className="text-xs text-muted">Saldo vencido (não usado)</dt>
            <dd className="text-xl font-bold tabular-nums">{moeda(lista.saldoVencido)}</dd>
          </div>
        </dl>
      )}

      <GradeDeRegistros
        rotulo="Vales-troca"
        colunas={colunas}
        itens={lista?.itens ?? (erro ? [] : null)}
        chave={(v) => v.id}
        nome={(v) => v.codigo}
        total={lista?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        apagada={(v) => v.situacao === 'Cancelado' || v.situacao === 'Usado'}
        vazio="Nenhum vale com esses filtros."
        acoes={
          gestao
            ? (v) =>
                v.situacao !== 'Cancelado' && v.situacao !== 'Usado' && (
                  <AcaoDaLinha rotulo={`Cancelar ${v.codigo}`} icone="x" perigo onClick={() => setCancelando(v)} />
                )
            : undefined
        }
      />

      {cancelando && (
        <CancelarVale
          vale={cancelando}
          onFechar={() => setCancelando(null)}
          onCancelado={() => {
            setCancelando(null)
            setVersao((x) => x + 1)
          }}
        />
      )}
    </div>
  )
}

/** Cancelamento com motivo (a API recusa sem ele e o erro aparece no diálogo). */
function CancelarVale({ vale, onFechar, onCancelado }: { vale: ValeTroca; onFechar: () => void; onCancelado: () => void }) {
  const { enviar } = useApi()
  const [motivo, setMotivo] = useState('')
  return (
    <Confirmacao
      titulo={`Cancelar vale ${vale.codigo}`}
      rotulo="Cancelar vale"
      onCancelar={onFechar}
      onConfirmar={async () => {
        await enviar('POST', `/api/vendas/vales/${vale.id}/cancelar`, { motivo })
        onCancelado()
      }}
    >
      <p className="mb-3 text-muted">{`O saldo de ${moeda(vale.saldo)} deixa de valer como crédito do cliente.`}</p>
      <Campo rotulo="Motivo">
        <input value={motivo} onChange={(e) => setMotivo(e.target.value)} maxLength={300} autoFocus className={estilos.campo} />
      </Campo>
    </Confirmacao>
  )
}
