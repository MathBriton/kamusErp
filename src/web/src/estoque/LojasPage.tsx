import { useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { pecas, type Loja } from './tipos'
import { useLojas } from './useLojas'

/** Lojas (locais de estoque) da empresa ativa. Só o Dono cadastra; contam no limite de lojas do plano. */
export function LojasPage() {
  const { enviar } = useApi()
  const { empresaAtual, perfilAtual, consolidado } = useEmpresa()
  const [versao, setVersao] = useState(0)
  const lojas = useLojas(versao)
  const [edicao, setEdicao] = useState<Loja | 'nova' | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const ehDono = perfilAtual === 'Dono' && !consolidado

  async function alternar(loja: Loja) {
    setErro(null)
    try {
      await enviar('POST', `/api/estoque/lojas/${loja.id}/${loja.ativa ? 'desativar' : 'reativar'}`)
      setVersao((v) => v + 1)
    } catch (e) {
      setErro(e instanceof ErroDeApi ? e.message : 'Não foi possível alterar a loja.')
    }
  }

  return (
    <div className="mx-auto max-w-4xl">
      <p className="text-xs text-muted">Administrativo</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Lojas</h1>
          <p className="mt-1 text-sm text-muted">
            {consolidado ? 'Lojas de todas as empresas.' : `Locais com estoque próprio de ${empresaAtual?.nomeFantasia ?? 'sua empresa'}.`}
          </p>
        </div>
        {ehDono && (
          <button type="button" onClick={() => setEdicao('nova')} className={estilos.botaoPrimario}>
            Nova loja
          </button>
        )}
      </div>

      {erro && <p role="alert" className={`mt-4 ${estilos.erro}`}>{erro}</p>}

      {lojas && (
        <ul className="mt-6 divide-y divide-border rounded-2xl border border-border bg-surface shadow-sm">
          {lojas.map((l) => (
            <li key={l.id} className="flex flex-wrap items-center gap-3 px-4 py-3">
              <div className="flex-1">
                <p className={`font-semibold ${l.ativa ? '' : 'text-muted line-through'}`}>{l.nome}</p>
                <p className="text-xs text-muted">{l.ativa ? pecas(l.quantidade) : 'Desativada'}</p>
              </div>
              {ehDono && (
                <>
                  <button type="button" onClick={() => setEdicao(l)} className={estilos.botao} aria-label={`Renomear ${l.nome}`}>
                    Renomear
                  </button>
                  <button type="button" onClick={() => void alternar(l)} className={estilos.botao} aria-label={`${l.ativa ? 'Desativar' : 'Reativar'} ${l.nome}`}>
                    {l.ativa ? 'Desativar' : 'Reativar'}
                  </button>
                </>
              )}
            </li>
          ))}
        </ul>
      )}

      {edicao && (
        <FormularioDeLoja
          loja={edicao === 'nova' ? null : edicao}
          onFechar={() => setEdicao(null)}
          onSalvo={() => {
            setEdicao(null)
            setVersao((v) => v + 1)
          }}
        />
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
