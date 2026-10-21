import { useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { decimal, lerNumero } from '../cadastros/catalogo'
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
import { useContas } from '../financeiro/useFinanceiro'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { entraNaHora, PERFIS_DE_GESTAO, porcentagem, ROTULO_DO_TIPO, type FormaDePagamento, type Pagina, type RegrasDeVenda, type TipoDePagamento } from './tipos'
import { useFormas } from './useVendas'

type Janela =
  | { tipo: 'editar'; forma: FormaDePagamento | null }
  | { tipo: 'situacao'; forma: FormaDePagamento }
  | { tipo: 'selecionadas'; ativa: boolean }
  | null

const FILTROS = { situacao: 'todas', busca: '' }

/** Formas de pagamento no padrão de grade (taxa, prazo, parcelas e conta onde o dinheiro entra) e as regras de venda. */
export function FormasDePagamentoPage() {
  const { enviar } = useApi()
  const { perfilAtual, consolidado } = useEmpresa()
  const permitido = PERFIS_DE_GESTAO.includes(perfilAtual) && !consolidado
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { dados, erro: erroDaLista } = useListaPaginada<Pagina<FormaDePagamento>>(
    permitido ? `/api/vendas/formas?${grade.consulta}` : null, versao, grade.filtros.busca ? 250 : 0)
  const todas = useFormas(versao)
  const contas = useContas()
  const [janela, setJanela] = useState<Janela>(null)
  const [erro, setErro] = useState<string | null>(null)

  if (!permitido)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Formas de pagamento</h1>
        <p className={`mt-4 ${estilos.erro}`}>
          {consolidado ? 'Escolha uma empresa no seletor do topo.' : 'Só Dono e Gerente configuram as formas de pagamento.'}
        </p>
      </div>
    )

  const recarregar = () => {
    setJanela(null)
    setVersao((v) => v + 1)
  }
  const adicionarPadrao = async () => {
    setErro(null)
    try {
      await enviar('POST', '/api/vendas/formas/padrao')
      recarregar()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível concluir.')
    }
  }
  const nomeDaConta = (id: string | null) => (id ? (contas?.find((c) => c.id === id)?.nome ?? '?') : '—')
  const semConta = todas?.filter((f) => f.ativa && entraNaHora(f.tipo) && !f.contaId) ?? []
  const quantas = grade.selecionados.size

  const colunas: Coluna<FormaDePagamento>[] = [
    {
      titulo: 'Forma',
      celula: (f) => (
        <>
          <span className={`font-semibold ${f.ativa ? '' : 'line-through'}`}>{f.nome}</span>
          {f.nome !== ROTULO_DO_TIPO[f.tipo] && <span className="block text-xs text-muted">{ROTULO_DO_TIPO[f.tipo]}</span>}
        </>
      ),
    },
    { titulo: 'Conta', celula: (f) => nomeDaConta(f.contaId) },
    {
      titulo: 'Taxa',
      celula: (f) => (f.maxParcelas > 1 ? `${porcentagem(f.taxaAVista)} à vista · ${porcentagem(f.taxaParcelado)} parcelado` : porcentagem(f.taxaAVista)),
      alinhar: 'direita',
    },
    { titulo: 'Parcelas', celula: (f) => (f.maxParcelas > 1 ? `até ${f.maxParcelas}x` : '1x'), alinhar: 'direita' },
    { titulo: 'Prazo', celula: (f) => (f.prazoDias === 0 ? 'na hora' : `${f.prazoDias} ${f.prazoDias === 1 ? 'dia' : 'dias'}`), alinhar: 'direita' },
    {
      titulo: 'Situação',
      celula: (f) => (
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${f.ativa ? 'bg-success-soft text-success' : 'bg-surface-2 text-muted'}`}>
          {f.ativa ? 'Ativa' : 'Inativa'}
        </span>
      ),
    },
  ]

  return (
    <div className="mx-auto max-w-7xl">
      <CabecalhoDaPagina
        secao="Vendas"
        titulo="Formas de pagamento"
        descricao="Dinheiro e PIX entram na hora na conta escolhida. Cartões e outras formas viram contas a receber, já sem a taxa, no prazo de cada uma."
        acoes={
          <>
            <BotaoExportar relatorio="formas-de-pagamento" parametros={grade.parametros} selecionados={[...grade.selecionados]} />
            <button type="button" onClick={() => void adicionarPadrao()} className={estilos.botao}>Adicionar formas padrão</button>
            <button type="button" onClick={() => setJanela({ tipo: 'editar', forma: null })} className={estilos.botaoPrimario}>
              <Icone nome="plus" />
              Nova forma
            </button>
          </>
        }
      />

      {semConta.length > 0 && (
        <p role="alert" className="mt-4 rounded-lg bg-warning-soft px-3 py-2.5 text-sm text-warning">
          {`Defina a conta onde o dinheiro entra para: ${semConta.map((f) => f.nome).join(', ')}. Sem ela, a venda com essa forma é recusada.`}
        </p>
      )}
      {(erro ?? erroDaLista) && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro ?? erroDaLista}</p>}

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <Campo rotulo="Buscar" className="min-w-60 flex-1">
          <input value={grade.filtros.busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Nome da forma" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Situação" className="w-40">
          <select value={grade.filtros.situacao} onChange={(e) => grade.filtrar({ situacao: e.target.value })} className={estilos.campo}>
            <option value="todas">Todas</option>
            <option value="ativas">Ativas</option>
            <option value="inativas">Inativas</option>
          </select>
        </Campo>
      </BarraDeFiltros>

      <GradeDeRegistros
        rotulo="Formas de pagamento"
        colunas={colunas}
        itens={dados?.itens ?? (erroDaLista ? [] : null)}
        chave={(f) => f.id}
        nome={(f) => f.nome}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        apagada={(f) => !f.ativa}
        vazio={grade.ativos ? 'Nenhuma forma de pagamento com esses filtros.' : 'Nenhuma forma ainda. Comece pelas formas padrão (dinheiro, PIX, débito e crédito).'}
        acoes={(f) => (
          <>
            <AcaoDaLinha rotulo={`Editar ${f.nome}`} icone="pencil" onClick={() => setJanela({ tipo: 'editar', forma: f })} />
            <AcaoDaLinha rotulo={`${f.ativa ? 'Desativar' : 'Reativar'} ${f.nome}`} icone="power" perigo={f.ativa} onClick={() => setJanela({ tipo: 'situacao', forma: f })} />
          </>
        )}
        acoesEmLote={
          <>
            <button type="button" onClick={() => setJanela({ tipo: 'selecionadas', ativa: false })} className={estilos.botao}>Desativar selecionadas</button>
            <button type="button" onClick={() => setJanela({ tipo: 'selecionadas', ativa: true })} className={estilos.botao}>Reativar selecionadas</button>
          </>
        }
      />

      <RegraDeEstoque />

      {janela?.tipo === 'editar' && <EdicaoDeForma forma={janela.forma} onFechar={() => setJanela(null)} onSalvo={recarregar} />}
      {janela?.tipo === 'situacao' && (
        <Confirmacao
          titulo={janela.forma.ativa ? 'Desativar forma de pagamento' : 'Reativar forma de pagamento'}
          rotulo={janela.forma.ativa ? 'Desativar' : 'Reativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', `/api/vendas/formas/${janela.forma.id}/${janela.forma.ativa ? 'desativar' : 'reativar'}`)
            recarregar()
          }}
        >
          <p>
            {janela.forma.ativa
              ? `"${janela.forma.nome}" deixa de aparecer no registro de venda. As vendas já feitas não mudam.`
              : `"${janela.forma.nome}" volta a aparecer no registro de venda.`}
          </p>
        </Confirmacao>
      )}
      {janela?.tipo === 'selecionadas' && (
        <Confirmacao
          titulo={janela.ativa ? 'Reativar formas de pagamento' : 'Desativar formas de pagamento'}
          rotulo={janela.ativa ? 'Reativar' : 'Desativar'}
          onCancelar={() => setJanela(null)}
          onConfirmar={async () => {
            await enviar('POST', '/api/vendas/formas/situacao', { ids: [...grade.selecionados], ativo: janela.ativa })
            grade.limparSelecao()
            recarregar()
          }}
        >
          <p>{`${janela.ativa ? 'Reativar' : 'Desativar'} ${quantas === 1 ? 'a forma selecionada' : `as ${quantas} formas selecionadas`}?`}</p>
        </Confirmacao>
      )}
    </div>
  )
}

function RegraDeEstoque() {
  const { getJson, enviar } = useApi()
  const [regras, setRegras] = useState<RegrasDeVenda | null>(null)
  const [prazo, setPrazo] = useState('')
  const [validade, setValidade] = useState('')
  const [prazoCondicional, setPrazoCondicional] = useState('')
  const [limitePecas, setLimitePecas] = useState('')
  const [limiteValor, setLimiteValor] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [salvo, setSalvo] = useState(false)

  useEffect(() => {
    const controle = new AbortController()
    getJson<RegrasDeVenda>('/api/vendas/configuracao', controle.signal)
      .then((c) => {
        setRegras(c)
        setPrazo(String(c.prazoDeTrocaDias))
        setValidade(String(c.validadeDoValeDias))
        setPrazoCondicional(String(c.prazoCondicionalDias))
        setLimitePecas(String(c.limiteCondicionalPecas))
        setLimiteValor(decimal(c.limiteCondicionalValor))
      })
      .catch(() => {})
    return () => controle.abort()
  }, [getJson])

  if (regras === null) return null

  const salvar = async (novas: RegrasDeVenda) => {
    setErro(null)
    setSalvo(false)
    const anteriores = regras
    setRegras(novas)
    try {
      await enviar('PUT', '/api/vendas/configuracao', novas)
      setSalvo(true)
    } catch (ex) {
      setRegras(anteriores)
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.')
    }
  }

  const salvarPrazos = (e: FormEvent) => {
    e.preventDefault()
    void salvar({
      ...regras,
      prazoDeTrocaDias: Number.parseInt(prazo, 10) || 0,
      validadeDoValeDias: Number.parseInt(validade, 10) || 0,
      prazoCondicionalDias: Number.parseInt(prazoCondicional, 10) || 0,
      limiteCondicionalPecas: Number.parseInt(limitePecas, 10) || 0,
      limiteCondicionalValor: lerNumero(limiteValor) ?? 0,
    })
  }

  return (
    <section className="mt-6 rounded-2xl border border-border bg-surface p-5 shadow-sm">
      <h2 className="font-semibold">Regras da venda</h2>
      <label className="mt-3 flex items-start gap-3 text-sm">
        <input
          type="checkbox"
          checked={regras.permitirEstoqueNegativo}
          onChange={(e) => void salvar({ ...regras, permitirEstoqueNegativo: e.target.checked })}
          className="mt-1 h-4 w-4"
        />
        <span>
          <span className="font-semibold">Permitir vender sem saldo</span>
          <span className="block text-muted">O estoque fica negativo e a venda mostra um aviso. Desligado, a venda é recusada quando falta peça.</span>
        </span>
      </label>
      <form onSubmit={salvarPrazos} className="mt-4 flex flex-wrap items-end gap-3">
        <Campo rotulo="Prazo de troca (dias)" className="w-44">
          <input value={prazo} onChange={(e) => setPrazo(e.target.value.replace(/\D/g, ''))} inputMode="numeric" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Validade do vale-troca (dias)" className="w-56">
          <input value={validade} onChange={(e) => setValidade(e.target.value.replace(/\D/g, ''))} inputMode="numeric" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Prazo da condicional (dias)" className="w-52">
          <input value={prazoCondicional} onChange={(e) => setPrazoCondicional(e.target.value.replace(/\D/g, ''))} inputMode="numeric" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Limite de peças na condicional" className="w-56">
          <input value={limitePecas} onChange={(e) => setLimitePecas(e.target.value.replace(/\D/g, ''))} inputMode="numeric" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Limite de valor na condicional (R$)" className="w-60">
          <input value={limiteValor} onChange={(e) => setLimiteValor(e.target.value)} inputMode="decimal" className={estilos.campo} />
        </Campo>
        <button type="submit" className={estilos.botao}>Salvar regras</button>
        {salvo && <span className="text-sm text-success">Salvo.</span>}
      </form>
      <p className="mt-2 text-xs text-muted">
        Depois do prazo, só Dono ou Gerente aceitam a troca. Os limites da condicional valem por cliente (0 = sem limite); acima deles, ou com condicional atrasada, só Dono ou
        Gerente liberam.
      </p>
      {erro && <p role="alert" className={`mt-3 ${estilos.erro}`}>{erro}</p>}
    </section>
  )
}

function EdicaoDeForma({ forma, onFechar, onSalvo }: { forma: FormaDePagamento | null; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const contas = useContas()?.filter((c) => c.ativa) ?? []
  const [nome, setNome] = useState(forma?.nome ?? '')
  const [tipo, setTipo] = useState<TipoDePagamento>(forma?.tipo ?? 'Credito')
  const [contaId, setContaId] = useState(forma?.contaId ?? '')
  const [taxaAVista, setTaxaAVista] = useState(forma ? decimal(forma.taxaAVista) : '0,00')
  const [taxaParcelado, setTaxaParcelado] = useState(forma ? decimal(forma.taxaParcelado) : '0,00')
  const [maxParcelas, setMaxParcelas] = useState(String(forma?.maxParcelas ?? 1))
  const [prazo, setPrazo] = useState(String(forma?.prazoDias ?? 0))
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const credito = tipo === 'Credito'

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    setEnviando(true)
    const corpo = {
      nome,
      tipo,
      contaId: contaId || null,
      taxaAVista: lerNumero(taxaAVista) ?? 0,
      taxaParcelado: credito ? (lerNumero(taxaParcelado) ?? 0) : (lerNumero(taxaAVista) ?? 0),
      maxParcelas: credito ? Number.parseInt(maxParcelas, 10) || 1 : 1,
      prazoDias: Number.parseInt(prazo, 10) || 0,
    }
    try {
      await enviar(forma ? 'PUT' : 'POST', forma ? `/api/vendas/formas/${forma.id}` : '/api/vendas/formas', corpo)
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={forma ? forma.nome : 'Nova forma de pagamento'} onFechar={onFechar}>
      <form onSubmit={salvar} className="grid gap-4 sm:grid-cols-2">
        <Campo rotulo="Nome" className="sm:col-span-2">
          <input value={nome} onChange={(e) => setNome(e.target.value)} maxLength={60} autoFocus className={estilos.campo} />
        </Campo>
        <Campo rotulo="Tipo">
          <select value={tipo} onChange={(e) => setTipo(e.target.value as TipoDePagamento)} className={estilos.campo}>
            {(Object.keys(ROTULO_DO_TIPO) as TipoDePagamento[]).map((t) => (
              <option key={t} value={t}>{ROTULO_DO_TIPO[t]}</option>
            ))}
          </select>
        </Campo>
        <Campo rotulo="Conta onde o dinheiro entra">
          <select value={contaId} onChange={(e) => setContaId(e.target.value)} className={estilos.campo}>
            <option value="">{entraNaHora(tipo) ? 'Escolha...' : 'Nenhuma'}</option>
            {contas.map((c) => (
              <option key={c.id} value={c.id}>{c.nome}</option>
            ))}
          </select>
        </Campo>
        <Campo rotulo={credito ? 'Taxa à vista (%)' : 'Taxa (%)'}>
          <input value={taxaAVista} onChange={(e) => setTaxaAVista(e.target.value)} inputMode="decimal" className={estilos.campo} />
        </Campo>
        {credito && (
          <Campo rotulo="Taxa parcelado (%)">
            <input value={taxaParcelado} onChange={(e) => setTaxaParcelado(e.target.value)} inputMode="decimal" className={estilos.campo} />
          </Campo>
        )}
        {credito && (
          <Campo rotulo="Máximo de parcelas">
            <input value={maxParcelas} onChange={(e) => setMaxParcelas(e.target.value.replace(/\D/g, ''))} inputMode="numeric" className={estilos.campo} />
          </Campo>
        )}
        <Campo rotulo={credito ? 'Prazo da 1ª parcela (dias)' : 'Prazo para cair (dias)'}>
          <input value={prazo} onChange={(e) => setPrazo(e.target.value.replace(/\D/g, ''))} inputMode="numeric" className={estilos.campo} />
        </Campo>
        {erro && <p role="alert" className={`sm:col-span-2 ${estilos.erro}`}>{erro}</p>}
        <div className="flex justify-end gap-2 sm:col-span-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" disabled={enviando || !nome.trim()} className={estilos.botaoPrimario}>Salvar</button>
        </div>
      </form>
    </Dialogo>
  )
}
