import { useEffect, useState, type FormEvent } from 'react'
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
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { formatarTelefone } from './tipos'

interface Vendedor {
  id: string
  nome: string
  comissaoPercentual: number
  usuarioId: string | null
  usuarioNome: string | null
  telefone: string | null
  ativo: boolean
}

interface Usuario {
  id: string
  nome: string
  email: string
}

const percentual = (valor: number) => `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(valor)}%`

const FILTROS = { situacao: 'ativos', busca: '' }

type Janela =
  | { tipo: 'formulario'; vendedor: Vendedor | null }
  | { tipo: 'mudarSituacao'; vendedor: Vendedor }
  | { tipo: 'mudarSelecionados'; ativo: boolean }
  | null

/** Vendedores no padrão das telas de cadastro: filtros, grade paginada com seleção, ações por linha e em lote, exportação. */
export function VendedoresPage() {
  const { getJson, enviar } = useApi()
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { dados, erro } = useListaPaginada<{ itens: Vendedor[]; total: number }>(`/api/vendedores?${grade.consulta}`, versao, grade.filtros.busca ? 250 : 0)
  const [usuarios, setUsuarios] = useState<Usuario[]>([])
  const [janela, setJanela] = useState<Janela>(null)

  useEffect(() => {
    const controle = new AbortController()
    getJson<Usuario[]>('/api/vendedores/usuarios', controle.signal).then(setUsuarios).catch(() => {})
    return () => controle.abort()
  }, [getJson, versao])

  const recarregar = () => {
    setJanela(null)
    setVersao((v) => v + 1)
  }

  const colunas: Coluna<Vendedor>[] = [
    { titulo: 'Nome', celula: (v) => <span className="font-semibold">{v.nome}</span> },
    { titulo: 'Comissão', celula: (v) => percentual(v.comissaoPercentual), alinhar: 'direita' },
    { titulo: 'Telefone', celula: (v) => <span className="tabular-nums">{formatarTelefone(v.telefone)}</span> },
    { titulo: 'Login', celula: (v) => (v.usuarioNome ? `vinculado: ${v.usuarioNome}` : 'sem login') },
    {
      titulo: 'Situação',
      celula: (v) => (
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${v.ativo ? 'bg-success-soft text-success' : 'bg-surface-2 text-muted'}`}>
          {v.ativo ? 'Ativo' : 'Inativo'}
        </span>
      ),
    },
  ]
  const quantos = grade.selecionados.size

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Cadastros"
        titulo="Vendedores"
        descricao="Comissão padrão de cada vendedor. Vincule a um usuário para ele ver as próprias vendas."
        acoes={
          <>
            <BotaoExportar relatorio="vendedores" parametros={grade.parametros} selecionados={[...grade.selecionados]} />
            <button type="button" onClick={() => setJanela({ tipo: 'formulario', vendedor: null })} className={estilos.botaoPrimario}>
              <Icone nome="plus" />
              Novo vendedor
            </button>
          </>
        }
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="min-w-60 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input value={grade.filtros.busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Nome ou telefone" className={estilos.campo} />
        </label>
        <label className="w-40 text-sm">
          <span className="mb-1 block font-semibold">Situação</span>
          <select value={grade.filtros.situacao} onChange={(e) => grade.filtrar({ situacao: e.target.value })} className={estilos.campo}>
            <option value="ativos">Ativos</option>
            <option value="inativos">Inativos</option>
            <option value="todos">Todos</option>
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
        rotulo="Vendedores"
        colunas={colunas}
        itens={dados?.itens ?? (erro ? [] : null)}
        chave={(v) => v.id}
        nome={(v) => v.nome}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        apagada={(v) => !v.ativo}
        vazio="Nenhum vendedor encontrado com esses filtros."
        acoes={(v) => (
          <>
            <AcaoDaLinha rotulo={`Editar ${v.nome}`} icone="pencil" onClick={() => setJanela({ tipo: 'formulario', vendedor: v })} />
            <AcaoDaLinha rotulo={`${v.ativo ? 'Inativar' : 'Reativar'} ${v.nome}`} icone="power" perigo={v.ativo} onClick={() => setJanela({ tipo: 'mudarSituacao', vendedor: v })} />
          </>
        )}
        acoesEmLote={
          <>
            <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionados', ativo: false })} className={estilos.botao}>Inativar selecionados</button>
            <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionados', ativo: true })} className={estilos.botao}>Reativar selecionados</button>
          </>
        }
      />

      {janela?.tipo === 'formulario' && (
        <FormularioDeVendedor vendedor={janela.vendedor} usuarios={usuarios} onFechar={() => setJanela(null)} onSalvo={recarregar} />
      )}
      {janela?.tipo === 'mudarSituacao' && (
        <Confirmacao
          titulo={janela.vendedor.ativo ? 'Inativar vendedor' : 'Reativar vendedor'}
          rotulo={janela.vendedor.ativo ? 'Inativar' : 'Reativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', `/api/vendedores/${janela.vendedor.id}/${janela.vendedor.ativo ? 'inativar' : 'reativar'}`)
            recarregar()
          }}
        >
          {janela.vendedor.ativo
            ? <p>{`"${janela.vendedor.nome}" deixa de aparecer no registro de vendas e nas condicionais. As vendas e comissões continuam guardadas e dá para reativar depois.`}</p>
            : <p>{`"${janela.vendedor.nome}" volta a aparecer no registro de vendas e nas condicionais.`}</p>}
        </Confirmacao>
      )}
      {janela?.tipo === 'mudarSelecionados' && (
        <Confirmacao
          titulo={janela.ativo ? 'Reativar vendedores' : 'Inativar vendedores'}
          rotulo={janela.ativo ? 'Reativar' : 'Inativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', '/api/vendedores/situacao', { ids: [...grade.selecionados], ativo: janela.ativo })
            grade.limparSelecao()
            recarregar()
          }}
        >
          <p>{`${janela.ativo ? 'Reativar' : 'Inativar'} ${quantos === 1 ? 'o vendedor selecionado' : `os ${quantos} vendedores selecionados`}?`}</p>
        </Confirmacao>
      )}
    </div>
  )
}

function FormularioDeVendedor({ vendedor, usuarios, onFechar, onSalvo }: { vendedor: Vendedor | null; usuarios: Usuario[]; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [nome, setNome] = useState(vendedor?.nome ?? '')
  const [comissao, setComissao] = useState(vendedor ? String(vendedor.comissaoPercentual).replace('.', ',') : '3')
  const [usuarioId, setUsuarioId] = useState(vendedor?.usuarioId ?? '')
  const [telefone, setTelefone] = useState(vendedor?.telefone ?? '')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setErro(null)
    const corpo = { nome, comissaoPercentual: Number(comissao.replace(',', '.')), usuarioId: usuarioId || null, telefone: telefone || null }
    try {
      if (vendedor) await enviar('PUT', `/api/vendedores/${vendedor.id}`, corpo)
      else await enviar('POST', '/api/vendedores', corpo)
      onSalvo()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível salvar.')
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={vendedor ? 'Editar vendedor' : 'Novo vendedor'} onFechar={onFechar}>
      <form onSubmit={(e) => void salvar(e)} className="space-y-4">
        <Campo rotulo="Nome">
          <input value={nome} onChange={(e) => setNome(e.target.value)} className={estilos.campo} />
        </Campo>
        <div className="grid grid-cols-2 gap-3">
          <Campo rotulo="Comissão (%)">
            <input value={comissao} onChange={(e) => setComissao(e.target.value)} inputMode="decimal" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Telefone">
            <input value={telefone} onChange={(e) => setTelefone(e.target.value)} inputMode="tel" className={estilos.campo} />
          </Campo>
        </div>
        <Campo rotulo="Usuário do sistema">
          <select value={usuarioId} onChange={(e) => setUsuarioId(e.target.value)} className={estilos.campo}>
            <option value="">Sem login</option>
            {usuarios.map((u) => (
              <option key={u.id} value={u.id}>
                {`${u.nome} (${u.email})`}
              </option>
            ))}
          </select>
        </Campo>
        {erro && (
          <p role="alert" className={estilos.erro}>
            {erro}
          </p>
        )}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>
            Cancelar
          </button>
          <button type="submit" disabled={enviando} className={estilos.botaoPrimario}>
            {enviando ? 'Salvando...' : 'Salvar'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
