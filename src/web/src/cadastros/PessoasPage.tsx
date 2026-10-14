import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import { FichaDaPessoa } from './FichaDaPessoa'
import { formatarTelefone, linkWhatsApp, type Aniversariante, type Papel, type Pessoa } from './tipos'

const TEXTOS = {
  cliente: { titulo: 'Clientes', novo: 'Novo cliente', descricao: 'Ficha do cliente com tamanhos preferidos, contato e WhatsApp em 1 clique.', relatorio: 'clientes' },
  fornecedor: { titulo: 'Fornecedores', novo: 'Novo fornecedor', descricao: 'Fornecedores de mercadoria e serviços.', relatorio: 'fornecedores' },
}

type Ficha = { pessoa: Pessoa | null } | null

/** Lista de clientes ou fornecedores (mesma ficha, papéis diferentes). */
export function PessoasPage({ papel }: { papel: Papel }) {
  const { getJson } = useApi()
  const [params, setParams] = useSearchParams()
  const [busca, setBusca] = useState('')
  const [situacao, setSituacao] = useState('ativos')
  const [{ pessoas, erro }, setResultado] = useState<{ pessoas: Pessoa[] | null; erro: string | null }>({ pessoas: null, erro: null })
  const [ficha, setFicha] = useState<Ficha>(null)
  const [versao, setVersao] = useState(0)
  const textos = TEXTOS[papel]
  const idDaUrl = params.get('id')

  useEffect(() => {
    const controle = new AbortController()
    const consulta = new URLSearchParams({ papel, situacao })
    if (busca.trim()) consulta.set('busca', busca.trim())
    const timer = window.setTimeout(() => {
      getJson<{ itens: Pessoa[] }>(`/api/pessoas?${consulta}`, controle.signal)
        .then((r) => setResultado({ pessoas: r.itens, erro: null }))
        .catch((e: unknown) => {
          if (!controle.signal.aborted) setResultado({ pessoas: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar.' })
        })
    }, busca ? 250 : 0)
    return () => {
      window.clearTimeout(timer)
      controle.abort()
    }
  }, [getJson, papel, situacao, busca, versao])

  // Link da pesquisa global: /cadastros/clientes?id=...
  useEffect(() => {
    if (!idDaUrl) return
    const controle = new AbortController()
    getJson<Pessoa>(`/api/pessoas/${idDaUrl}`, controle.signal)
      .then((pessoa) => setFicha({ pessoa }))
      .catch(() => {})
    return () => controle.abort()
  }, [getJson, idDaUrl])

  const fechar = useCallback(() => {
    setFicha(null)
    if (params.has('id')) setParams({}, { replace: true })
  }, [params, setParams])

  return (
    <div className="mx-auto max-w-7xl">
      <p className="text-xs text-muted">Cadastros</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex-1">
          <h1 className="text-2xl font-bold tracking-tight">{textos.titulo}</h1>
          <p className="mt-1 text-sm text-muted">{textos.descricao}</p>
        </div>
        <button type="button" onClick={() => setFicha({ pessoa: null })} className={estilos.botaoPrimario}>
          {textos.novo}
        </button>
      </div>

      <div className="mt-6 grid gap-5 lg:grid-cols-[1fr_18rem]">
        <section className="min-w-0">
          <div className="flex flex-wrap items-end gap-3">
            <label className="min-w-60 flex-1 text-sm">
              <span className="mb-1 block font-semibold">Buscar</span>
              <input value={busca} onChange={(e) => setBusca(e.target.value)} placeholder="Nome, CPF/CNPJ, telefone ou e-mail" className={estilos.campo} />
            </label>
            <label className="text-sm">
              <span className="mb-1 block font-semibold">Situação</span>
              <select value={situacao} onChange={(e) => setSituacao(e.target.value)} className={estilos.campo}>
                <option value="ativos">Ativos</option>
                <option value="inativos">Inativos</option>
                <option value="todos">Todos</option>
              </select>
            </label>
            <BotaoExportar relatorio={textos.relatorio} parametros={{ busca: busca.trim(), situacao }} />
          </div>

          {erro && (
            <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
              <Icone nome="alert" />
              {erro}
            </p>
          )}

          {pessoas && (
            <div className="mt-4 overflow-x-auto rounded-2xl border border-border bg-surface shadow-sm">
              {pessoas.length === 0 ? (
                <p className="p-8 text-center text-sm text-muted">Nenhum cadastro encontrado.</p>
              ) : (
                <table className="w-full text-sm">
                  <thead className="bg-surface-2 text-left text-xs text-muted uppercase">
                    <tr>
                      <th className="px-4 py-2">Nome</th>
                      <th className="px-4 py-2">CPF/CNPJ</th>
                      <th className="px-4 py-2">Telefone</th>
                      <th className="px-4 py-2">Cidade</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {pessoas.map((p) => (
                      <tr key={p.id} className={p.ativo ? '' : 'text-muted'}>
                        <td className="px-4 py-2.5">
                          <button type="button" onClick={() => setFicha({ pessoa: p })} className="text-left font-semibold hover:text-primary hover:underline">
                            {papel === 'fornecedor' ? (p.nomeFantasia ?? p.nome) : p.nome}
                          </button>
                        </td>
                        <td className="px-4 py-2.5 tabular-nums">{p.documento ?? '—'}</td>
                        <td className="px-4 py-2.5">
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
                        </td>
                        <td className="px-4 py-2.5">{p.endereco.cidade ? `${p.endereco.cidade}/${p.endereco.uf ?? ''}` : '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          )}
        </section>

        {papel === 'cliente' && <Aniversariantes />}
      </div>

      {ficha && (
        <FichaDaPessoa
          key={ficha.pessoa?.id ?? 'nova'}
          papel={papel}
          pessoa={ficha.pessoa}
          onFechar={fechar}
          onSalvo={() => {
            fechar()
            setVersao((v) => v + 1)
          }}
        />
      )}
    </div>
  )
}

function Aniversariantes() {
  const { getJson } = useApi()
  const [lista, setLista] = useState<Aniversariante[]>([])

  useEffect(() => {
    const controle = new AbortController()
    getJson<Aniversariante[]>('/api/pessoas/aniversariantes?periodo=semana', controle.signal)
      .then(setLista)
      .catch(() => {})
    return () => controle.abort()
  }, [getJson])

  return (
    <section aria-label="Aniversariantes da semana" className="h-fit rounded-2xl border border-border bg-surface p-4 shadow-sm">
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
