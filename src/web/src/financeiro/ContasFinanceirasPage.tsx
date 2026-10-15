import { useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { lerNumero, moeda } from '../cadastros/catalogo'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { dataBr, hojeIso, PERFIS_DO_FINANCEIRO, somarDias, type ContaFinanceira } from './tipos'
import { useContas } from './useFinanceiro'

interface Extrato {
  conta: string
  de: string
  ate: string
  saldoAnterior: number
  movimentos: { id: string; data: string; descricao: string; valor: number; saldo: number; usuario: string | null }[]
  saldoFinal: number
}

const TIPOS: Record<ContaFinanceira['tipo'], string> = { Caixa: 'Caixa da loja', Banco: 'Conta bancária', Carteira: 'Carteira digital' }

/** Contas financeiras (saldo atual), extrato por período e transferência entre contas. */
export function ContasFinanceirasPage() {
  const { perfilAtual, consolidado } = useEmpresa()
  const [versao, setVersao] = useState(0)
  const contas = useContas(versao)
  const [nova, setNova] = useState(false)
  const [transferir, setTransferir] = useState(false)
  const [extrato, setExtrato] = useState<ContaFinanceira | null>(null)

  if (!PERFIS_DO_FINANCEIRO.includes(perfilAtual))
    return (
      <div className="mx-auto max-w-3xl">
        <h1 className="text-2xl font-bold tracking-tight">Contas financeiras</h1>
        <p className={`mt-4 ${estilos.erro}`}>Só Dono, Gerente e Financeiro acessam o financeiro.</p>
      </div>
    )

  const recarregar = () => setVersao((v) => v + 1)
  const total = contas?.reduce((s, c) => s + c.saldo, 0) ?? 0

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Financeiro</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Contas financeiras</h1>
          <p className="mt-1 text-sm text-muted">Onde o dinheiro está. Cada pagamento ou recebimento movimenta uma conta.</p>
        </div>
        {!consolidado && (
          <>
            {contas && contas.length > 1 && <button type="button" onClick={() => setTransferir(true)} className={estilos.botao}>Transferir entre contas</button>}
            <button type="button" onClick={() => setNova(true)} className={estilos.botaoPrimario}>Nova conta</button>
          </>
        )}
      </div>

      {contas && (
        <div className="mt-6 overflow-hidden rounded-2xl border border-border bg-surface shadow-sm">
          {contas.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted">Nenhuma conta ainda. Comece pelo caixa da loja e pela conta do banco.</p>
          ) : (
            <ul className="divide-y divide-border">
              {contas.map((c) => (
                <li key={c.id} className="flex flex-wrap items-center gap-3 px-4 py-3">
                  <div className="flex-1">
                    <p className={`font-semibold ${c.ativa ? '' : 'text-muted line-through'}`}>{c.nome}</p>
                    <p className="text-xs text-muted">{TIPOS[c.tipo]}</p>
                  </div>
                  <span className={`text-lg font-bold tabular-nums ${c.saldo < 0 ? 'text-danger' : ''}`}>{moeda(c.saldo)}</span>
                  <button type="button" onClick={() => setExtrato(c)} className={estilos.botao} aria-label={`Extrato de ${c.nome}`}>Extrato</button>
                </li>
              ))}
              <li className="flex items-center justify-between bg-surface-2 px-4 py-3 text-sm font-semibold">
                <span>Total</span>
                <span className="tabular-nums">{moeda(total)}</span>
              </li>
            </ul>
          )}
        </div>
      )}

      {nova && (
        <NovaConta
          onFechar={() => setNova(false)}
          onSalvo={() => {
            setNova(false)
            recarregar()
          }}
        />
      )}
      {transferir && contas && (
        <Transferencia
          contas={contas.filter((c) => c.ativa)}
          onFechar={() => setTransferir(false)}
          onSalvo={() => {
            setTransferir(false)
            recarregar()
          }}
        />
      )}
      {extrato && <ExtratoDaConta conta={extrato} onFechar={() => setExtrato(null)} />}
    </div>
  )
}

function NovaConta({ onFechar, onSalvo }: { onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [nome, setNome] = useState('')
  const [tipo, setTipo] = useState<ContaFinanceira['tipo']>('Caixa')
  const [saldo, setSaldo] = useState('')
  const [desde, setDesde] = useState(hojeIso())
  const [erro, setErro] = useState<string | null>(null)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    try {
      await enviar('POST', '/api/financeiro/contas', { nome, tipo, saldoInicial: lerNumero(saldo) ?? 0, saldoInicialEm: desde })
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível salvar.')
    }
  }

  return (
    <Dialogo titulo="Nova conta" onFechar={onFechar}>
      <form onSubmit={salvar} className="space-y-4">
        <Campo rotulo="Nome">
          <input value={nome} onChange={(e) => setNome(e.target.value)} maxLength={60} autoFocus placeholder="Ex.: Caixa da loja, Banco do Brasil" className={estilos.campo} />
        </Campo>
        <Campo rotulo="Tipo">
          <select value={tipo} onChange={(e) => setTipo(e.target.value as ContaFinanceira['tipo'])} className={estilos.campo}>
            {Object.entries(TIPOS).map(([v, r]) => (
              <option key={v} value={v}>{r}</option>
            ))}
          </select>
        </Campo>
        <div className="grid grid-cols-2 gap-3">
          <Campo rotulo="Saldo inicial (R$)">
            <input value={saldo} onChange={(e) => setSaldo(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Em">
            <input type="date" value={desde} onChange={(e) => setDesde(e.target.value)} className={estilos.campo} />
          </Campo>
        </div>
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" className={estilos.botaoPrimario}>Salvar</button>
        </div>
      </form>
    </Dialogo>
  )
}

function Transferencia({ contas, onFechar, onSalvo }: { contas: ContaFinanceira[]; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const [origemId, setOrigemId] = useState('')
  const [destinoId, setDestinoId] = useState('')
  const [valor, setValor] = useState('')
  const [data, setData] = useState(hojeIso())
  const [observacao, setObservacao] = useState('')
  const [erro, setErro] = useState<string | null>(null)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setErro(null)
    try {
      await enviar('POST', '/api/financeiro/transferencias', { origemId, destinoId, data, valor: lerNumero(valor) ?? 0, observacao: observacao || null })
      onSalvo()
    } catch (ex) {
      setErro(ex instanceof ErroDeApi ? ex.message : 'Não foi possível transferir.')
    }
  }

  return (
    <Dialogo titulo="Transferir entre contas" onFechar={onFechar}>
      <form onSubmit={salvar} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <Campo rotulo="De">
            <select value={origemId} onChange={(e) => setOrigemId(e.target.value)} className={estilos.campo}>
              <option value="">Escolha...</option>
              {contas.map((c) => (
                <option key={c.id} value={c.id}>{c.nome}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Para">
            <select value={destinoId} onChange={(e) => setDestinoId(e.target.value)} className={estilos.campo}>
              <option value="">Escolha...</option>
              {contas.filter((c) => c.id !== origemId).map((c) => (
                <option key={c.id} value={c.id}>{c.nome}</option>
              ))}
            </select>
          </Campo>
          <Campo rotulo="Valor (R$)">
            <input value={valor} onChange={(e) => setValor(e.target.value)} inputMode="decimal" placeholder="0,00" className={estilos.campo} />
          </Campo>
          <Campo rotulo="Data">
            <input type="date" value={data} onChange={(e) => setData(e.target.value)} className={estilos.campo} />
          </Campo>
        </div>
        <Campo rotulo="Observação (opcional)">
          <input value={observacao} onChange={(e) => setObservacao(e.target.value)} maxLength={100} placeholder="Ex.: depósito do caixa" className={estilos.campo} />
        </Campo>
        {erro && <p role="alert" className={estilos.erro}>{erro}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>Cancelar</button>
          <button type="submit" disabled={!origemId || !destinoId} className={estilos.botaoPrimario}>Transferir</button>
        </div>
      </form>
    </Dialogo>
  )
}

function ExtratoDaConta({ conta, onFechar }: { conta: ContaFinanceira; onFechar: () => void }) {
  const { getJson } = useApi()
  const [de, setDe] = useState(somarDias(hojeIso(), -30))
  const [ate, setAte] = useState(hojeIso())
  const [extrato, setExtrato] = useState<Extrato | null>(null)

  useEffect(() => {
    if (!de || !ate) return
    const controle = new AbortController()
    getJson<Extrato>(`/api/financeiro/contas/${conta.id}/extrato?de=${de}&ate=${ate}`, controle.signal)
      .then(setExtrato)
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, conta.id, de, ate])

  return (
    <Dialogo titulo={`Extrato — ${conta.nome}`} onFechar={onFechar} largo>
      <div className="space-y-4 text-sm">
        <div className="flex gap-3">
          <Campo rotulo="De">
            <input type="date" value={de} onChange={(e) => setDe(e.target.value)} className={estilos.campo} />
          </Campo>
          <Campo rotulo="Até">
            <input type="date" value={ate} onChange={(e) => setAte(e.target.value)} className={estilos.campo} />
          </Campo>
        </div>
        {extrato && (
          <div className="overflow-x-auto rounded-xl border border-border">
            <table aria-label="Extrato" className="w-full text-sm">
              <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-3 py-2">Data</th>
                  <th className="px-3 py-2">Descrição</th>
                  <th className="px-3 py-2 text-right">Valor</th>
                  <th className="px-3 py-2 text-right">Saldo</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                <tr className="text-muted">
                  <td className="px-3 py-2" colSpan={3}>Saldo anterior</td>
                  <td className="px-3 py-2 text-right tabular-nums">{moeda(extrato.saldoAnterior)}</td>
                </tr>
                {extrato.movimentos.map((m) => (
                  <tr key={m.id}>
                    <td className="px-3 py-2 whitespace-nowrap tabular-nums">{dataBr(m.data)}</td>
                    <td className="px-3 py-2">
                      {m.descricao}
                      {m.usuario && <span className="block text-xs text-muted">{m.usuario}</span>}
                    </td>
                    <td className={`px-3 py-2 text-right font-semibold tabular-nums ${m.valor < 0 ? 'text-danger' : 'text-success'}`}>{moeda(m.valor)}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{moeda(m.saldo)}</td>
                  </tr>
                ))}
                <tr className="bg-surface-2 font-semibold">
                  <td className="px-3 py-2" colSpan={3}>Saldo final</td>
                  <td className="px-3 py-2 text-right tabular-nums">{moeda(extrato.saldoFinal)}</td>
                </tr>
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Dialogo>
  )
}
