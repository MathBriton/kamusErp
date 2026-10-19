import { useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { decimal, lerNumero } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { useContas } from '../financeiro/useFinanceiro'
import { entraNaHora, PERFIS_DE_GESTAO, porcentagem, ROTULO_DO_TIPO, type FormaDePagamento, type RegrasDeVenda, type TipoDePagamento } from './tipos'
import { useFormas } from './useVendas'

/** Formas de pagamento (taxa, prazo, parcelas e conta onde o dinheiro entra) e a regra de estoque negativo. */
export function FormasDePagamentoPage() {
  const { enviar } = useApi()
  const { perfilAtual, consolidado } = useEmpresa()
  const [versao, setVersao] = useState(0)
  const formas = useFormas(versao)
  const contas = useContas()
  const [editando, setEditando] = useState<FormaDePagamento | 'nova' | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  if (!PERFIS_DE_GESTAO.includes(perfilAtual) || consolidado)
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Formas de pagamento</h1>
        <p className={`mt-4 ${estilos.erro}`}>
          {consolidado ? 'Escolha uma empresa no seletor do topo.' : 'Só Dono e Gerente configuram as formas de pagamento.'}
        </p>
      </div>
    )

  const recarregar = () => setVersao((v) => v + 1)
  const acao = async (caminho: string) => {
    setErro(null)
    try {
      await enviar('POST', caminho)
      recarregar()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível concluir.')
    }
  }
  const nomeDaConta = (id: string | null) => (id ? (contas?.find((c) => c.id === id)?.nome ?? '?') : '—')
  const semConta = formas?.filter((f) => f.ativa && entraNaHora(f.tipo) && !f.contaId) ?? []

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Vendas</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Formas de pagamento</h1>
          <p className="mt-1 text-sm text-muted">
            Dinheiro e PIX entram na hora na conta escolhida. Cartões e outras formas viram contas a receber, já sem a taxa, no prazo de cada uma.
          </p>
        </div>
        <button type="button" onClick={() => void acao('/api/vendas/formas/padrao')} className={estilos.botao}>Adicionar formas padrão</button>
        <button type="button" onClick={() => setEditando('nova')} className={estilos.botaoPrimario}>Nova forma</button>
      </div>

      {semConta.length > 0 && (
        <p role="alert" className="mt-4 rounded-lg bg-warning-soft px-3 py-2.5 text-sm text-warning">
          {`Defina a conta onde o dinheiro entra para: ${semConta.map((f) => f.nome).join(', ')}. Sem ela, a venda com essa forma é recusada.`}
        </p>
      )}
      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {formas && (
        <div className="mt-6 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          {formas.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted">Nenhuma forma ainda. Comece pelas formas padrão (dinheiro, PIX, débito e crédito).</p>
          ) : (
            <table aria-label="Formas de pagamento" className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-4 py-2">Forma</th>
                  <th className="px-4 py-2">Conta</th>
                  <th className="px-4 py-2 text-right">Taxa</th>
                  <th className="px-4 py-2 text-right">Parcelas</th>
                  <th className="px-4 py-2 text-right">Prazo</th>
                  <th />
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {formas.map((f) => (
                  <tr key={f.id} className={f.ativa ? '' : 'text-muted'}>
                    <td className="px-4 py-2">
                      <span className={`font-semibold ${f.ativa ? '' : 'line-through'}`}>{f.nome}</span>
                      {f.nome !== ROTULO_DO_TIPO[f.tipo] && <span className="block text-xs text-muted">{ROTULO_DO_TIPO[f.tipo]}</span>}
                    </td>
                    <td className="px-4 py-2">{nomeDaConta(f.contaId)}</td>
                    <td className="px-4 py-2 text-right tabular-nums">
                      {f.maxParcelas > 1 ? `${porcentagem(f.taxaAVista)} à vista · ${porcentagem(f.taxaParcelado)} parcelado` : porcentagem(f.taxaAVista)}
                    </td>
                    <td className="px-4 py-2 text-right tabular-nums">{f.maxParcelas > 1 ? `até ${f.maxParcelas}x` : '1x'}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{f.prazoDias === 0 ? 'na hora' : `${f.prazoDias} ${f.prazoDias === 1 ? 'dia' : 'dias'}`}</td>
                    <td className="px-4 py-2">
                      <div className="flex justify-end gap-2">
                        <button type="button" onClick={() => setEditando(f)} className={estilos.botao} aria-label={`Editar ${f.nome}`}>Editar</button>
                        <button
                          type="button"
                          onClick={() => void acao(`/api/vendas/formas/${f.id}/${f.ativa ? 'desativar' : 'reativar'}`)}
                          className={estilos.botao}
                          aria-label={`${f.ativa ? 'Desativar' : 'Reativar'} ${f.nome}`}
                        >
                          {f.ativa ? 'Desativar' : 'Reativar'}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      <RegraDeEstoque />

      {editando && (
        <EdicaoDeForma
          forma={editando === 'nova' ? null : editando}
          onFechar={() => setEditando(null)}
          onSalvo={() => {
            setEditando(null)
            recarregar()
          }}
        />
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
