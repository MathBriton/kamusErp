import { useState } from 'react'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade, useListaPaginada } from '../components/grade/useGrade'
import { Icone } from '../components/Icone'
import { BotaoExportar } from '../relatorios/BotaoExportar'

interface Alteracao {
  campo: string
  antes: string | null
  depois: string | null
}

interface Registro {
  id: string
  ocorridoEm: string
  tipo: 'Inclusao' | 'Alteracao' | 'Exclusao' | 'Evento'
  evento: string | null
  entidade: string | null
  descricao: string
  usuarioNome: string
  ip: string | null
  alteracoes: Alteracao[]
}

interface Pagina {
  itens: Registro[]
  total: number
}

const TIPOS = [
  { valor: '', rotulo: 'Todos' },
  { valor: 'Inclusao', rotulo: 'Inclusões' },
  { valor: 'Alteracao', rotulo: 'Alterações' },
  { valor: 'Exclusao', rotulo: 'Exclusões' },
  { valor: 'Evento', rotulo: 'Eventos (login, segurança)' },
]

const ESTILO_TIPO: Record<Registro['tipo'], { rotulo: string; classe: string }> = {
  Inclusao: { rotulo: 'Inclusão', classe: 'bg-success-soft text-success' },
  Alteracao: { rotulo: 'Alteração', classe: 'bg-info-soft text-info' },
  Exclusao: { rotulo: 'Exclusão', classe: 'bg-danger-soft text-danger' },
  Evento: { rotulo: 'Evento', classe: 'bg-surface-2 text-muted' },
}

const FILTROS = { tipo: '', de: '', ate: '' }

const formatarData = (iso: string) =>
  new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(iso))

/** As datas da tela são dias de Brasília; a API recebe o início e o fim do dia com o fuso (UTC−3). */
function comFuso(parametros: Record<string, string>) {
  const r = { ...parametros }
  if (r.de) r.de = `${r.de}T00:00:00-03:00`
  if (r.ate) r.ate = `${r.ate}T23:59:59-03:00`
  return r
}

/** Histórico de alterações em grade paginada: filtros de tipo e período, detalhes de cada registro e exportação. */
export function HistoricoPage() {
  const grade = useGrade(FILTROS)
  const parametros = comFuso(grade.parametros)
  const consulta = new URLSearchParams({ ...parametros, pagina: String(grade.paginacao.pagina), tamanho: String(grade.paginacao.tamanho) })
  const { dados, erro } = useListaPaginada<Pagina>(`/api/auditoria?${consulta}`)
  const [detalhe, setDetalhe] = useState<Registro | null>(null)

  const colunas: Coluna<Registro>[] = [
    {
      titulo: 'Tipo',
      celula: (r) => (
        <span className={`inline-block w-20 rounded-full px-2 py-0.5 text-center text-xs font-semibold ${ESTILO_TIPO[r.tipo].classe}`}>{ESTILO_TIPO[r.tipo].rotulo}</span>
      ),
    },
    { titulo: 'Descrição', celula: (r) => <span className="font-medium">{r.descricao}</span> },
    { titulo: 'Usuário', celula: (r) => <span className="text-muted">{r.usuarioNome}</span> },
    { titulo: 'Data/hora', celula: (r) => <span className="whitespace-nowrap text-muted tabular-nums">{formatarData(r.ocorridoEm)}</span> },
  ]

  return (
    <div className="mx-auto max-w-6xl">
      <CabecalhoDaPagina
        secao="Administrativo"
        titulo="Histórico de alterações"
        descricao="Quem incluiu, alterou ou excluiu registros, além de logins e eventos de segurança. O histórico não pode ser editado nem apagado."
        acoes={<BotaoExportar relatorio="historico-de-alteracoes" parametros={parametros} selecionados={[...grade.selecionados]} />}
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="w-60 text-sm">
          <span className="mb-1 block font-semibold">Tipo</span>
          <select value={grade.filtros.tipo} onChange={(e) => grade.filtrar({ tipo: e.target.value })} className={estilos.campo}>
            {TIPOS.map((t) => (
              <option key={t.valor} value={t.valor}>{t.rotulo}</option>
            ))}
          </select>
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">De</span>
          <input type="date" value={grade.filtros.de} onChange={(e) => grade.filtrar({ de: e.target.value })} className={estilos.campo} />
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">Até</span>
          <input type="date" value={grade.filtros.ate} onChange={(e) => grade.filtrar({ ate: e.target.value })} className={estilos.campo} />
        </label>
      </BarraDeFiltros>

      {erro && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      <GradeDeRegistros
        rotulo="Histórico de alterações"
        colunas={colunas}
        itens={dados?.itens ?? (erro ? [] : null)}
        chave={(r) => r.id}
        nome={(r) => r.descricao}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        acoes={(r) => <AcaoDaLinha rotulo={`Ver detalhes de ${r.descricao}`} icone="eye" onClick={() => setDetalhe(r)} />}
      />

      {detalhe && (
        <Dialogo titulo={detalhe.descricao} onFechar={() => setDetalhe(null)} largo>
          <div className="space-y-3 text-sm">
            <p className="text-muted">
              {`${ESTILO_TIPO[detalhe.tipo].rotulo} · ${detalhe.usuarioNome} · ${formatarData(detalhe.ocorridoEm)}`}
              {detalhe.ip && ` · IP ${detalhe.ip}`}
            </p>
            {detalhe.alteracoes.length === 0 ? (
              <p className="text-muted">Sem campos alterados (evento).</p>
            ) : (
              <table aria-label="Campos alterados" className="w-full overflow-hidden rounded-lg border border-border text-sm">
                <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                  <tr>
                    <th className="px-3 py-2">Campo</th>
                    <th className="px-3 py-2">Antes</th>
                    <th className="px-3 py-2">Depois</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {detalhe.alteracoes.map((a) => (
                    <tr key={a.campo}>
                      <td className="px-3 py-2 font-medium">{a.campo}</td>
                      <td className="px-3 py-2 text-muted">{a.antes ?? '—'}</td>
                      <td className="px-3 py-2">{a.depois ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </Dialogo>
      )}
    </div>
  )
}
