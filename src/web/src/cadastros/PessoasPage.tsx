import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router'
import { useApi } from '../api/useApi'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { Confirmacao } from '../components/grade/Confirmacao'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade, useListaPaginada } from '../components/grade/useGrade'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { FichaDaPessoa } from './FichaDaPessoa'
import { formatarTelefone, linkWhatsApp, type Aniversariante, type Papel, type Pessoa } from './tipos'

const TEXTOS = {
  cliente: {
    titulo: 'Clientes', novo: 'Novo cliente', singular: 'cliente', descricao: 'Ficha do cliente com tamanhos preferidos, contato e WhatsApp em 1 clique.',
    relatorio: 'clientes', inativar: 'deixa de aparecer na busca de clientes das vendas.',
  },
  fornecedor: {
    titulo: 'Fornecedores', novo: 'Novo fornecedor', singular: 'fornecedor', descricao: 'Fornecedores de mercadoria e serviços.',
    relatorio: 'fornecedores', inativar: 'deixa de aparecer na busca de fornecedores das contas a pagar.',
  },
}

const FILTROS = { situacao: 'ativos', busca: '' }

type Janela =
  | { tipo: 'ficha'; pessoa: Pessoa | null }
  | { tipo: 'mudarSituacao'; pessoa: Pessoa }
  | { tipo: 'mudarSelecionados'; ativo: boolean }
  | null

/** Clientes ou fornecedores (mesma ficha, papéis diferentes). A chave por papel zera filtros e seleção ao trocar de tela. */
export function PessoasPage({ papel }: { papel: Papel }) {
  return <ListaDePessoas key={papel} papel={papel} />
}

/** Padrão das telas de cadastro: filtros, grade paginada com seleção, ações por linha e em lote, exportação. */
function ListaDePessoas({ papel }: { papel: Papel }) {
  const { getJson, enviar } = useApi()
  const { perfilAtual } = useEmpresa()
  const gestao = perfilAtual === 'Dono' || perfilAtual === 'Gerente'
  const [params, setParams] = useSearchParams()
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { dados, erro } = useListaPaginada<{ itens: Pessoa[]; total: number }>(`/api/pessoas?papel=${papel}&${grade.consulta}`, versao, grade.filtros.busca ? 250 : 0)
  const [janela, setJanela] = useState<Janela>(null)
  const textos = TEXTOS[papel]
  const idDaUrl = params.get('id')
  const nomeDe = (p: Pessoa) => (papel === 'fornecedor' ? (p.nomeFantasia ?? p.nome) : p.nome)

  // Link da pesquisa global: /cadastros/clientes?id=...
  useEffect(() => {
    if (!idDaUrl) return
    const controle = new AbortController()
    getJson<Pessoa>(`/api/pessoas/${idDaUrl}`, controle.signal)
      .then((pessoa) => setJanela({ tipo: 'ficha', pessoa }))
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, idDaUrl])

  const fechar = useCallback(() => {
    setJanela(null)
    if (params.has('id')) setParams({}, { replace: true })
  }, [params, setParams])
  const recarregar = () => {
    fechar()
    setVersao((v) => v + 1)
  }

  const colunas: Coluna<Pessoa>[] = [
    {
      titulo: 'Nome',
      celula: (p) => (
        <>
          <button type="button" onClick={() => setJanela({ tipo: 'ficha', pessoa: p })} className="text-left font-semibold hover:text-primary hover:underline">
            {nomeDe(p)}
          </button>
          {papel === 'fornecedor' && p.nomeFantasia && <span className="block text-xs text-muted">{p.nome}</span>}
        </>
      ),
    },
    { titulo: 'CPF/CNPJ', celula: (p) => <span className="tabular-nums">{p.documento ?? '—'}</span> },
    {
      titulo: 'Telefone',
      celula: (p) => (
        <span className="flex items-center gap-2 tabular-nums">
          {formatarTelefone(p.telefone)}
          {p.whatsApp && (
            <a
              href={linkWhatsApp(p.whatsApp)}
              target="_blank"
              rel="noreferrer"
              aria-label={`WhatsApp de ${p.nome}`}
              className="rounded-md bg-success-soft px-1.5 py-0.5 text-xs font-semibold text-success"
            >
              WhatsApp
            </a>
          )}
        </span>
      ),
    },
    { titulo: 'Cidade', celula: (p) => (p.endereco.cidade ? `${p.endereco.cidade}/${p.endereco.uf ?? ''}` : '—') },
    {
      titulo: 'Situação',
      celula: (p) => (
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${p.ativo ? 'bg-success-soft text-success' : 'bg-surface-2 text-muted'}`}>
          {p.ativo ? 'Ativo' : 'Inativo'}
        </span>
      ),
    },
  ]

  const quantos = grade.selecionados.size

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Cadastros"
        titulo={textos.titulo}
        descricao={textos.descricao}
        acoes={
          <>
            <BotaoExportar relatorio={textos.relatorio} parametros={grade.parametros} selecionados={[...grade.selecionados]} />
            <button type="button" onClick={() => setJanela({ tipo: 'ficha', pessoa: null })} className={estilos.botaoPrimario}>
              <Icone nome="plus" />
              {textos.novo}
            </button>
          </>
        }
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="min-w-60 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input
            value={grade.filtros.busca}
            onChange={(e) => grade.filtrar({ busca: e.target.value })}
            placeholder="Nome, CPF/CNPJ, telefone ou e-mail"
            className={estilos.campo}
          />
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

      <div className={papel === 'cliente' ? 'grid gap-5 lg:grid-cols-[1fr_18rem]' : ''}>
        <div className="min-w-0">
          <GradeDeRegistros
            rotulo={textos.titulo}
            colunas={colunas}
            itens={dados?.itens ?? (erro ? [] : null)}
            chave={(p) => p.id}
            nome={nomeDe}
            total={dados?.total ?? 0}
            {...grade.paginacao}
            selecao={grade.selecao}
            apagada={(p) => !p.ativo}
            vazio="Nenhum cadastro encontrado com esses filtros."
            acoes={(p) => (
              <>
                <AcaoDaLinha rotulo={`Ver ficha de ${nomeDe(p)}`} icone="eye" onClick={() => setJanela({ tipo: 'ficha', pessoa: p })} />
                {gestao && !p.anonimizadoEm && (
                  <AcaoDaLinha
                    rotulo={`${p.ativo ? 'Inativar' : 'Reativar'} ${nomeDe(p)}`}
                    icone="power"
                    perigo={p.ativo}
                    onClick={() => setJanela({ tipo: 'mudarSituacao', pessoa: p })}
                  />
                )}
              </>
            )}
            acoesEmLote={
              gestao && (
                <>
                  <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionados', ativo: false })} className={estilos.botao}>Inativar selecionados</button>
                  <button type="button" onClick={() => setJanela({ tipo: 'mudarSelecionados', ativo: true })} className={estilos.botao}>Reativar selecionados</button>
                </>
              )
            }
          />
        </div>

        {papel === 'cliente' && <Aniversariantes versao={versao} />}
      </div>

      {janela?.tipo === 'ficha' && (
        <FichaDaPessoa key={janela.pessoa?.id ?? 'nova'} papel={papel} pessoa={janela.pessoa} onFechar={fechar} onSalvo={recarregar} />
      )}
      {janela?.tipo === 'mudarSituacao' && (
        <Confirmacao
          titulo={`${janela.pessoa.ativo ? 'Inativar' : 'Reativar'} ${textos.singular}`}
          rotulo={janela.pessoa.ativo ? 'Inativar' : 'Reativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', `/api/pessoas/${janela.pessoa.id}/${janela.pessoa.ativo ? 'inativar' : 'reativar'}`)
            recarregar()
          }}
        >
          {janela.pessoa.ativo
            ? <p>{`"${nomeDe(janela.pessoa)}" ${textos.inativar} O histórico continua guardado e dá para reativar depois.`}</p>
            : <p>{`"${nomeDe(janela.pessoa)}" volta a ficar disponível para uso.`}</p>}
        </Confirmacao>
      )}
      {janela?.tipo === 'mudarSelecionados' && (
        <Confirmacao
          titulo={`${janela.ativo ? 'Reativar' : 'Inativar'} ${textos.titulo.toLowerCase()}`}
          rotulo={janela.ativo ? 'Reativar' : 'Inativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', '/api/pessoas/situacao', { ids: [...grade.selecionados], ativo: janela.ativo })
            grade.limparSelecao()
            recarregar()
          }}
        >
          <p>{`${janela.ativo ? 'Reativar' : 'Inativar'} ${quantos === 1 ? `o ${textos.singular} selecionado` : `os ${quantos} ${textos.titulo.toLowerCase()} selecionados`}?`}</p>
          {janela.ativo && <p className="mt-2 text-xs text-muted">Cadastros anonimizados (LGPD) continuam inativos.</p>}
        </Confirmacao>
      )}
    </div>
  )
}

/** `versao` muda a cada cadastro salvo: a lista é recarregada. */
function Aniversariantes({ versao }: { versao: number }) {
  const { getJson } = useApi()
  const [lista, setLista] = useState<Aniversariante[]>([])

  useEffect(() => {
    const controle = new AbortController()
    getJson<Aniversariante[]>('/api/pessoas/aniversariantes?periodo=semana', controle.signal)
      .then(setLista)
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, versao])

  return (
    <section aria-label="Aniversariantes da semana" className="mt-4 h-fit rounded-2xl border border-border bg-surface p-4 shadow-sm">
      <h2 className="font-semibold">Aniversariantes da semana</h2>
      {lista.length === 0 ? (
        <p className="mt-2 text-sm text-muted">Ninguém faz aniversário nos próximos 7 dias.</p>
      ) : (
        <ul className="mt-3 space-y-3">
          {lista.map((a) => {
            const [, mes, dia] = a.dataNascimento.split('-')
            const mensagem = `Feliz aniversário, ${a.nome.split(' ')[0]}! 🎉 Preparamos um mimo para você na loja. Venha nos visitar!`
            return (
              <li key={a.id} className="text-sm">
                <p className="font-semibold">{a.nome}</p>
                <p className="text-xs text-muted">{`${dia}/${mes} · ${a.idade} anos`}</p>
                {a.whatsApp && a.aceitaMarketing ? (
                  <a
                    href={linkWhatsApp(a.whatsApp, mensagem)}
                    target="_blank"
                    rel="noreferrer"
                    aria-label={`Parabenizar ${a.nome}`}
                    className="mt-1 inline-block rounded-md bg-success-soft px-2 py-0.5 text-xs font-semibold text-success"
                  >
                    Enviar parabéns no WhatsApp
                  </a>
                ) : (
                  <p className="mt-1 text-xs text-muted">{a.whatsApp ? 'Sem consentimento para mensagens' : 'Sem WhatsApp cadastrado'}</p>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
