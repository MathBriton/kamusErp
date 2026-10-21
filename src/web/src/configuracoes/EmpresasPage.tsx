import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import type { EmpresaDoCliente } from './tipos'

type Edicao = { tipo: 'nova' } | { tipo: 'editar'; empresa: EmpresaDoCliente } | null

export function EmpresasPage() {
  const { getJson } = useApi()
  const [empresas, setEmpresas] = useState<EmpresaDoCliente[] | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [edicao, setEdicao] = useState<Edicao>(null)

  const carregar = useCallback(async () => {
    try {
      setEmpresas(await getJson<EmpresaDoCliente[]>('/api/empresas'))
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível carregar as empresas.')
    }
  }, [getJson])

  useEffect(() => {
    const timer = window.setTimeout(() => void carregar(), 0)
    return () => window.clearTimeout(timer)
  }, [carregar])

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Administrativo</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">Empresas</h1>
          <p className="mt-1 text-sm text-muted">Os CNPJs do seu negócio. Quem cadastra uma empresa vira Dono dela.</p>
        </div>
        <button type="button" onClick={() => setEdicao({ tipo: 'nova' })} className={estilos.botaoPrimario}>
          Nova empresa
        </button>
      </div>

      {erro && (
        <p role="alert" className={`mt-5 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      {empresas && (
        <div className="mt-5 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
              <tr>
                <th className="px-4 py-2">Nome fantasia</th>
                <th className="px-4 py-2">Razão social</th>
                <th className="px-4 py-2">CNPJ</th>
                <th className="px-4 py-2" />
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {empresas.map((e) => (
                <tr key={e.id}>
                  <td className="px-4 py-2.5 font-semibold">{e.nomeFantasia}</td>
                  <td className="px-4 py-2.5">{e.razaoSocial}</td>
                  <td className="px-4 py-2.5 tabular-nums">{e.cnpj}</td>
                  <td className="px-4 py-2.5 text-right">
                    <button type="button" onClick={() => setEdicao({ tipo: 'editar', empresa: e })} className={`${estilos.botao} ml-auto`}>
                      Editar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {edicao && (
        <FormularioDeEmpresa
          empresa={edicao.tipo === 'editar' ? edicao.empresa : null}
          onFechar={() => setEdicao(null)}
          onSalvo={() => {
            setEdicao(null)
            void carregar()
          }}
        />
      )}
    </div>
  )
}

function FormularioDeEmpresa({ empresa, onFechar, onSalvo }: { empresa: EmpresaDoCliente | null; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const { recarregar } = useAuth()
  const [razaoSocial, setRazaoSocial] = useState(empresa?.razaoSocial ?? '')
  const [nomeFantasia, setNomeFantasia] = useState(empresa?.nomeFantasia ?? '')
  const [cnpj, setCnpj] = useState(empresa?.cnpj ?? '')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setErro(null)
    try {
      if (empresa) await enviar('PUT', `/api/empresas/${empresa.id}`, { razaoSocial, nomeFantasia })
      else {
        await enviar('POST', '/api/empresas', { razaoSocial, nomeFantasia, cnpj })
        await recarregar() // a empresa nova entra no seletor de CNPJ
      }
      onSalvo()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível salvar.')
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={empresa ? 'Editar empresa' : 'Nova empresa'} onFechar={onFechar}>
      <form onSubmit={(e) => void salvar(e)} className="space-y-4">
        <Campo rotulo="Razão social">
          <input value={razaoSocial} onChange={(e) => setRazaoSocial(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Nome fantasia">
          <input value={nomeFantasia} onChange={(e) => setNomeFantasia(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="CNPJ">
          <input value={cnpj} onChange={(e) => setCnpj(e.target.value)} disabled={!!empresa} inputMode="numeric" className={estilos.campo} />
        </Campo>
        {empresa && <p className="text-xs text-muted">O CNPJ não muda: outro CNPJ é outra empresa.</p>}
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
