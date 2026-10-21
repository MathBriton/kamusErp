import { useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
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
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { pecas, type Loja } from './tipos'

type Janela = { tipo: 'nova' } | { tipo: 'renomear'; loja: Loja } | { tipo: 'mudarSituacao'; loja: Loja } | { tipo: 'mudarSelecionadas'; ativo: boolean } | null

const FILTROS = { situacao: 'todas', busca: '' }

/** Lojas (locais de estoque) da empresa ativa em grade. Só o Dono cadastra, renomeia e desativa; contam no limite de lojas do plano. */
export function LojasPage() {
  const { enviar } = useApi()
  const { empresaAtual, perfilAtual, consolidado } = useEmpresa()
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { dados, erro } = useListaPaginada<{ itens: Loja[]; total: number }>(`/api/estoque/lojas?${grade.consulta}`, versao, grade.filtros.busca ? 250 : 0)
  const [janela, setJanela] = useState<Janela>(null)
  const ehDono = perfilAtual === 'Dono' && !consolidado

  const recarregar = () => {
    setJanela(null)
    setVersao((v) => v + 1)
  }

  const colunas: Coluna<Loja>[] = [
    { titulo: 'Loja', celula: (l) => <span className="font-semibold">{l.nome}</span> },
    { titulo: 'Peças em estoque', celula: (l) => pecas(l.quantidade), alinhar: 'direita' },
    {
      titulo: 'Situação',
      celula: (l) => (
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${l.ativa ? 'bg-success-soft text-success' : 'bg-surface-2 text-muted'}`}>
          {l.ativa ? 'Ativa' : 'Inativa'}
        </span>
      ),
    },
  ]

  return (
    <div className="mx-auto max-w-5xl">
      <CabecalhoDaPagina
        secao="Administrativo"
        titulo="Lojas"
        descricao={consolidado ? 'Lojas de todas as empresas.' : `Locais com estoque próprio de ${empresaAtual?.nomeFantasia ?? 'sua empresa'}.`}
        acoes={
          <>
            <BotaoExportar relatorio="lojas" parametros={grade.parametros} selecionados={[...grade.selecionados]} />
            {ehDono && (
              <button type="button" onClick={() => setJanela({ tipo: 'nova' })} className={estilos.botaoPrimario}>
                <Icone nome="plus" />
                Nova loja
              </button>
            )}
          </>
        }
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="min-w-60 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input value={grade.filtros.busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Nome da loja" className={estilos.campo} />
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">Situação</span>
          <select value={grade.filtros.situacao} onChange={(e) => grade.filtrar({ situacao: e.target.value })} className={estilos.campo}>
            <option value="todas">Todas</option>
            <option value="ativas">Ativas</option>
            <option value="inativas">Inativas</option>
          </select>
        </label>
      </BarraDeFiltros>

      {erro && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      <GradeDeRegistros
        rotulo="Lojas"
        colunas={colunas}
        itens={dados?.itens ?? (erro ? [] : null)}
        chave={(l) => l.id}
        nome={(l) => l.nome}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        apagada={(l) => !l.ativa}
        vazio="Nenhuma loja encontrada com esses filtros."
        acoes={
          ehDono
            ? (l) => (
                <>
                  <AcaoDaLinha rotulo={`Renomear ${l.nome}`} icone="pencil" onClick={() => setJanela({ tipo: 'renomear', loja: l })} />
                  <AcaoDaLinha
                    rotulo={`${l.ativa ? 'Desativar' : 'Reativar'} ${l.nome}`}
                    icone="power"
                    perigo={l.ativa}
                    onClick={() => setJanela({ tipo: 'mudarSituacao', loja: l })}
                  />
                </>
              )
            : undefined
        }
        acoesEmLote={
          ehDono && (
            <>
              <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionadas', ativo: false })} className={estilos.botao}>Desativar selecionadas</button>
              <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionadas', ativo: true })} className={estilos.botao}>Reativar selecionadas</button>
            </>
          )
        }
      />

      {(janela?.tipo === 'nova' || janela?.tipo === 'renomear') && (
        <FormularioDeLoja loja={janela.tipo === 'renomear' ? janela.loja : null} onFechar={() => setJanela(null)} onSalvo={recarregar} />
      )}
      {janela?.tipo === 'mudarSituacao' && (
        <Confirmacao
          titulo={janela.loja.ativa ? 'Desativar loja' : 'Reativar loja'}
          rotulo={janela.loja.ativa ? 'Desativar' : 'Reativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', `/api/estoque/lojas/${janela.loja.id}/${janela.loja.ativa ? 'desativar' : 'reativar'}`)
            recarregar()
          }}
        >
          {janela.loja.ativa
            ? <p>{`"${janela.loja.nome}" deixa de receber movimentos. Ela precisa estar sem estoque e a empresa precisa de outra loja ativa.`}</p>
            : <p>{`"${janela.loja.nome}" volta a receber movimentos (conta no limite de lojas do plano).`}</p>}
        </Confirmacao>
      )}
      {janela?.tipo === 'mudarSelecionadas' && (
        <Confirmacao
          titulo={janela.ativo ? 'Reativar lojas' : 'Desativar lojas'}
          rotulo={janela.ativo ? 'Reativar' : 'Desativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', '/api/estoque/lojas/situacao', { ids: [...grade.selecionados], ativo: janela.ativo })
            grade.limparSelecao()
            recarregar()
          }}
        >
          <p>{`${janela.ativo ? 'Reativar' : 'Desativar'} ${grade.selecionados.size === 1 ? 'a loja selecionada' : `as ${grade.selecionados.size} lojas selecionadas`}?`}</p>
        </Confirmacao>
      )}
    </div>
  )
}

function FormularioDeLoja({ loja, onFechar, onSalvo }: { loja: Loja | null; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [nome, setNome] = useState(loja?.nome ?? '')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setErro(null)
    try {
      await enviar(loja ? 'PUT' : 'POST', loja ? `/api/estoque/lojas/${loja.id}` : '/api/estoque/lojas', { nome })
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={loja ? 'Renomear loja' : 'Nova loja'} onFechar={onFechar}>
      <form onSubmit={salvar} className="space-y-4">
        <Campo rotulo="Nome">
          <input value={nome} onChange={(e) => setNome(e.target.value)} maxLength={60} autoFocus className={estilos.campo} />
        </Campo>
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" disabled={enviando} className={estilos.botaoPrimario}>{enviando ? 'Salvando...' : 'Salvar'}</button>
        </div>
      </form>
    </Dialogo>
  )
}
