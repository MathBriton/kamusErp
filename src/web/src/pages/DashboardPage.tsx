import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { Icone, type NomeIcone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { filtrarMenu, MENU, todasAsRotas } from '../navegacao/menu'

interface EmpresaAtivaResposta {
  consolidado: boolean
  empresas: { id: string; nomeFantasia: string; cnpj: string }[]
}

const INDICADORES: { titulo: string; icone: NomeIcone; tom: string; dica: string }[] = [
  { titulo: 'Vendas hoje', icone: 'bag', tom: 'bg-primary-soft text-primary', dica: 'Aparece com o registro de vendas (v0.10.0).' },
  { titulo: 'Vendas no mês', icone: 'trendingUp', tom: 'bg-success-soft text-success', dica: 'Aparece com o registro de vendas (v0.10.0).' },
  { titulo: 'A receber · 7 dias', icone: 'wallet', tom: 'bg-info-soft text-info', dica: 'Aparece com o módulo financeiro (v0.9.0).' },
  { titulo: 'A pagar · 7 dias', icone: 'alert', tom: 'bg-warning-soft text-warning', dica: 'Aparece com o módulo financeiro (v0.9.0).' },
]

const PRIMEIROS_PASSOS = [
  { titulo: 'Cadastrar produtos', descricao: 'Com grade de cor × tamanho, ou importando sua planilha.', rota: '/cadastros/produtos' },
  { titulo: 'Cadastrar clientes', descricao: 'Ficha com tamanhos preferidos e WhatsApp em 1 clique.', rota: '/cadastros/clientes' },
  { titulo: 'Dar entrada no estoque', descricao: 'Digite na matriz de grade ou importe o XML da nota.', rota: '/estoque/entradas' },
  { titulo: 'Lançar contas a pagar', descricao: 'Fornecedores, aluguel e despesas fixas.', rota: '/financeiro/contas-a-pagar' },
  { titulo: 'Registrar a primeira venda', descricao: 'Baixa o estoque e lança no financeiro automaticamente.', rota: '/vendas/registro' },
]

export function DashboardPage() {
  const { estado } = useAuth()
  const { selecao, perfilAtual } = useEmpresa()
  const { getJson } = useApi()
  const [{ contexto, erro }, setResultado] = useState<{ contexto: EmpresaAtivaResposta | null; erro: string | null }>({
    contexto: null,
    erro: null,
  })

  useEffect(() => {
    const controle = new AbortController()
    getJson<EmpresaAtivaResposta>('/api/empresas/ativa', controle.signal)
      .then((resposta) => setResultado({ contexto: resposta, erro: null }))
      .catch((e: unknown) => {
        if (controle.signal.aborted) return
        setResultado({ contexto: null, erro: e instanceof ErroDeApi ? e.message : 'Não foi possível carregar a empresa ativa.' })
      })
    return () => controle.abort()
  }, [getJson, selecao])

  const rotasPermitidas = useMemo(() => new Set(todasAsRotas(filtrarMenu(MENU, perfilAtual)).map((r) => r.rota)), [perfilAtual])
  const passos = PRIMEIROS_PASSOS.filter((p) => rotasPermitidas.has(p.rota))

  const primeiroNome = estado.status === 'autenticado' ? estado.sessao.usuario.nome.split(' ')[0] : ''
  const data = new Intl.DateTimeFormat('pt-BR', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date())
  const hoje = data.charAt(0).toUpperCase() + data.slice(1) // "Quarta-feira, 14 de outubro"

  return (
    <div className="mx-auto max-w-7xl">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs text-muted">Início</p>
          <h1 className="text-2xl font-bold tracking-tight">Olá, {primeiroNome} 👋</h1>
          {contexto && (
            <p className="mt-1 text-sm text-muted">
              {contexto.consolidado
                ? `Visão consolidada · ${contexto.empresas.length} empresas (somente leitura)`
                : `${contexto.empresas[0]?.nomeFantasia} · ${contexto.empresas[0]?.cnpj}`}
            </p>
          )}
          <p className="text-xs text-muted">{hoje}</p>
        </div>
        <div className="inline-flex rounded-lg border border-border bg-surface-2 p-0.5 text-sm" title="Filtros de período chegam junto com os indicadores">
          {['Hoje', '7 dias', 'Mês'].map((p, i) => (
            <span key={p} className={`rounded-md px-3 py-1 font-medium ${i === 0 ? 'bg-surface text-foreground shadow-sm' : 'text-muted'}`}>
              {p}
            </span>
          ))}
        </div>
      </div>

      {erro && (
        <p role="alert" className="mb-5 flex items-center gap-2 rounded-lg bg-danger-soft px-4 py-3 text-sm text-danger">
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      <div className="mb-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {INDICADORES.map((k) => (
          <article key={k.titulo} className="rounded-2xl border border-border bg-surface p-4 shadow-sm">
            <div className="flex items-center gap-2 text-[13px] font-medium text-muted">
              <span className={`grid size-8 place-items-center rounded-lg ${k.tom}`}>
                <Icone nome={k.icone} />
              </span>
              {k.titulo}
            </div>
            <p className="mt-3 text-2xl font-bold">—</p>
            <p className="mt-1 text-xs text-muted">{k.dica}</p>
          </article>
        ))}
      </div>

      <div className="grid gap-5 lg:grid-cols-3">
        <section className="rounded-2xl border border-border bg-surface p-5 shadow-sm lg:col-span-2">
          <h2 className="font-semibold">Primeiros passos</h2>
          <p className="mt-1 text-sm text-muted">Configure sua loja nesta ordem. Os indicadores acendem conforme os dados entram.</p>
          <ol className="mt-4 divide-y divide-border">
            {passos.map((passo, i) => (
              <li key={passo.rota}>
                <Link to={passo.rota} className="group flex items-center gap-3 rounded-lg py-3 hover:bg-surface-2">
                  <span className="grid size-8 shrink-0 place-items-center rounded-full border border-border text-sm font-semibold text-muted">{i + 1}</span>
                  <span className="flex-1">
                    <span className="block text-sm font-semibold">{passo.titulo}</span>
                    <span className="block text-xs text-muted">{passo.descricao}</span>
                  </span>
                  <Icone nome="arrowRight" className="size-4 text-muted group-hover:text-primary" />
                </Link>
              </li>
            ))}
          </ol>
        </section>

        <section className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
          <h2 className="font-semibold">Alertas</h2>
          <div className="mt-6 flex flex-col items-center text-center">
            <span className="grid size-12 place-items-center rounded-full bg-success-soft text-success">
              <Icone nome="check" className="size-5" />
            </span>
            <p className="mt-3 text-sm font-medium">Sem alertas por enquanto</p>
            <p className="mt-1 text-xs text-muted">Estoque baixo, quebra de grade e contas vencendo aparecem aqui.</p>
          </div>
        </section>
      </div>
    </div>
  )
}
