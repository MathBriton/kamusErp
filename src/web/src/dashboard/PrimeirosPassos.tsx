import { Link } from 'react-router'
import { Icone } from '../components/Icone'

const ETAPAS = [
  { codigo: 'produtos', titulo: 'Cadastrar produtos', descricao: 'Cor × tamanho (grades e cores padrão com um clique no próprio cadastro) ou planilha XLSX/CSV.', acao: 'Cadastrar →', rota: '/cadastros/produtos' },
  { codigo: 'clientes', titulo: 'Cadastrar clientes', descricao: 'Ficha com tamanhos preferidos e WhatsApp em 1 clique.', acao: 'Adicionar →', rota: '/cadastros/clientes' },
  { codigo: 'entrada_de_estoque', titulo: 'Dar entrada no estoque', descricao: 'Matriz de grade ou importação do XML da NF-e.', acao: 'Importar XML', rota: '/estoque/xml' },
  { codigo: 'contas_a_pagar', titulo: 'Lançar contas a pagar', descricao: 'Fornecedores, aluguel e custos fixos.', acao: 'Lançar →', rota: '/financeiro/contas-a-pagar' },
  { codigo: 'formas_de_pagamento', titulo: 'Configurar formas de pagamento', descricao: 'Dinheiro e PIX precisam da conta onde o dinheiro cai (ex.: Caixa da loja); desative o que a loja não usa.', acao: 'Configurar →', rota: '/vendas/formas-de-pagamento' },
  { codigo: 'primeira_venda', titulo: 'Registrar a primeira venda', descricao: 'Baixa o estoque e lança no financeiro automaticamente.', acao: 'Registrar venda', rota: '/vendas/registro', marco: true },
]

/** Checklist da implantação com barra de progresso; some quando todas as etapas que o perfil enxerga estão feitas. */
export function PrimeirosPassos({ concluidos, rotasPermitidas }: { concluidos: string[]; rotasPermitidas: Set<string> }) {
  const etapas = ETAPAS.filter((e) => rotasPermitidas.has(e.rota))
  const feitas = etapas.filter((e) => concluidos.includes(e.codigo)).length
  if (etapas.length === 0 || feitas === etapas.length) return null

  return (
    <section aria-label="Primeiros passos" className="rounded-2xl border border-border bg-surface p-6 shadow-sm">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">Primeiros passos</h2>
          <p className="mt-0.5 text-sm text-muted">Configure a loja nesta ordem: os indicadores acendem conforme os dados entram.</p>
        </div>
        <span className="text-sm font-semibold text-primary tabular-nums">{`${feitas} de ${etapas.length} concluídos`}</span>
      </div>
      <div
        role="progressbar"
        aria-label="Progresso da implantação"
        aria-valuemin={0}
        aria-valuemax={etapas.length}
        aria-valuenow={feitas}
        className="mt-3 h-2 overflow-hidden rounded-full bg-surface-2"
      >
        <div className="h-full rounded-full bg-primary transition-all" style={{ width: `${(feitas / etapas.length) * 100}%` }} />
      </div>

      <ol className="mt-4 divide-y divide-border">
        {etapas.map((etapa, i) => {
          const feita = concluidos.includes(etapa.codigo)
          return (
            <li key={etapa.codigo} aria-label={etapa.titulo} className="flex items-center gap-4 py-3">
              <span
                className={`grid size-9 shrink-0 place-items-center rounded-full text-sm font-bold ${
                  feita ? 'bg-primary text-primary-foreground' : 'border border-border text-muted'
                }`}
              >
                {feita ? <Icone nome="check" /> : i + 1}
              </span>
              <span className="min-w-0 flex-1">
                <span className="flex flex-wrap items-center gap-2">
                  <span className={`text-sm font-semibold ${feita ? 'text-muted line-through decoration-border' : ''}`}>{etapa.titulo}</span>
                  {etapa.marco && <span className="rounded-full bg-accent-soft px-2 py-0.5 text-[11px] font-bold tracking-wide text-accent uppercase">Marco</span>}
                </span>
                <span className="block text-xs text-muted">{etapa.descricao}</span>
              </span>
              {feita ? (
                <span className="flex items-center gap-1 rounded-full bg-success-soft px-2.5 py-1 text-xs font-semibold text-success">
                  <Icone nome="check" className="size-3.5" />
                  Concluído
                </span>
              ) : (
                <Link to={etapa.rota} className="shrink-0 rounded-lg px-3 py-1.5 text-sm font-semibold text-primary hover:bg-primary-soft">
                  {etapa.acao}
                </Link>
              )}
            </li>
          )
        })}
      </ol>
    </section>
  )
}
