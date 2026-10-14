import type { NomeIcone } from '../components/Icone'

export type Perfil = 'Dono' | 'Gerente' | 'Vendedor' | 'Estoquista' | 'Financeiro'

export interface ItemMenu {
  titulo: string
  rota: string
  /** Release em que a tela deve ficar pronta (mostrado nas páginas ainda em construção). */
  previstoPara?: string
  descricao?: string
  /** Perfis que veem o item. Ausente = todos. */
  perfis?: Perfil[]
}

export interface GrupoMenu extends Partial<ItemMenu> {
  titulo: string
  icone: NomeIcone
  itens?: ItemMenu[]
}

const GESTAO: Perfil[] = ['Dono', 'Gerente']

/**
 * Menu principal: ÚNICA fonte da navegação (sidebar, rotas e páginas "em construção").
 * A visibilidade por perfil aqui é só conforto visual — a API sempre valida as permissões.
 */
export const MENU: GrupoMenu[] = [
  { titulo: 'Dashboard', icone: 'dashboard', rota: '/' },
  {
    titulo: 'Cadastros',
    icone: 'shirt',
    itens: [
      { titulo: 'Produtos', rota: '/cadastros/produtos', descricao: 'Produtos com grade (cor × tamanho), SKUs e códigos de barras.', perfis: [...GESTAO, 'Estoquista'] },
      { titulo: 'Grades e cores', rota: '/cadastros/grades', descricao: 'Grades de tamanho reutilizáveis (PP–GG, 36–48, infantil) e cores.', perfis: [...GESTAO, 'Estoquista'] },
      { titulo: 'Clientes', rota: '/cadastros/clientes', descricao: 'Ficha do cliente: tamanhos preferidos, contato, aniversariantes e WhatsApp em 1 clique.', perfis: [...GESTAO, 'Vendedor', 'Financeiro'] },
      { titulo: 'Fornecedores', rota: '/cadastros/fornecedores', descricao: 'Fornecedores e de-para de produtos para a importação de XML.', perfis: [...GESTAO, 'Estoquista', 'Financeiro'] },
      { titulo: 'Importar planilha', rota: '/cadastros/importacao', descricao: 'Clientes, fornecedores e produtos a partir do Excel.', perfis: [...GESTAO, 'Estoquista'] },
      { titulo: 'Vendedores', rota: '/cadastros/vendedores', descricao: 'Vendedores e comissão padrão.', perfis: GESTAO },
    ],
  },
  {
    titulo: 'Estoque',
    icone: 'box',
    perfis: [...GESTAO, 'Estoquista'],
    itens: [
      { titulo: 'Posição de estoque', rota: '/estoque/posicao', previstoPara: 'v0.11.0', descricao: 'Saldo por produto em matriz cor × tamanho, quebra de grade e valor em estoque.' },
      { titulo: 'Entrada de mercadoria', rota: '/estoque/entradas', previstoPara: 'v0.11.0', descricao: 'Entrada em matriz de grade ou por XML da NF-e do fornecedor, com custo médio.' },
      { titulo: 'Ajustes', rota: '/estoque/ajustes', previstoPara: 'v0.11.0', descricao: 'Perdas, avarias e correções com motivo obrigatório.' },
      { titulo: 'Transferências', rota: '/estoque/transferencias', previstoPara: 'v0.11.0', descricao: 'Transferência de peças entre lojas.' },
      { titulo: 'Inventário', rota: '/estoque/inventario', previstoPara: 'v0.11.0', descricao: 'Contagem física e ajuste pela diferença.' },
    ],
  },
  {
    titulo: 'Financeiro',
    icone: 'wallet',
    perfis: [...GESTAO, 'Financeiro'],
    itens: [
      { titulo: 'Contas a pagar', rota: '/financeiro/contas-a-pagar', previstoPara: 'v0.12.0', descricao: 'Títulos, parcelas, baixas, juros e multa.' },
      { titulo: 'Contas a receber', rota: '/financeiro/contas-a-receber', previstoPara: 'v0.12.0', descricao: 'Recebíveis de cartão (com taxa e prazo), crediário e outros.' },
      { titulo: 'Fluxo de caixa', rota: '/financeiro/fluxo-de-caixa', previstoPara: 'v0.12.0', descricao: 'Previsto × realizado por período.' },
      { titulo: 'Contas financeiras', rota: '/financeiro/contas', previstoPara: 'v0.12.0', descricao: 'Caixa da loja, contas bancárias e carteiras.' },
    ],
  },
  {
    titulo: 'Vendas',
    icone: 'bag',
    perfis: [...GESTAO, 'Vendedor', 'Financeiro'],
    itens: [
      { titulo: 'Registro de vendas', rota: '/vendas/registro', previstoPara: 'v0.13.0', descricao: 'Venda no backoffice (sem emissão fiscal) com baixa de estoque e lançamento financeiro.', perfis: [...GESTAO, 'Vendedor'] },
      { titulo: 'Trocas e vale-troca', rota: '/vendas/trocas', previstoPara: 'v0.13.0', descricao: 'Trocas vinculadas à venda e vale-troca como crédito do cliente.', perfis: [...GESTAO, 'Vendedor'] },
      { titulo: 'Condicional', rota: '/vendas/condicional', previstoPara: 'v0.14.0', descricao: 'Peças que o cliente leva para provar, com prazo de devolução.', perfis: [...GESTAO, 'Vendedor'] },
      { titulo: 'Crediário', rota: '/vendas/crediario', previstoPara: 'v0.14.0', descricao: 'Limite por cliente, carnê, recebimento de parcelas e inadimplência.' },
      { titulo: 'Comissões', rota: '/vendas/comissoes', previstoPara: 'v0.13.0', descricao: 'Fechamento de comissão e metas por vendedor.', perfis: [...GESTAO, 'Financeiro'] },
    ],
  },
  {
    titulo: 'Relatórios',
    icone: 'file',
    rota: '/relatorios',
    previstoPara: 'v0.15.0',
    descricao: 'Relatórios com exportação em PDF e Excel.',
    perfis: [...GESTAO, 'Financeiro'],
  },
  {
    titulo: 'Configurações',
    icone: 'sliders',
    perfis: GESTAO,
    itens: [
      { titulo: 'Histórico de alterações', rota: '/configuracoes/historico', descricao: 'Quem alterou o quê e quando, logins e eventos de segurança.', perfis: GESTAO },
      { titulo: 'Empresas', rota: '/configuracoes/empresas', descricao: 'CNPJs do seu negócio: cadastro e dados.', perfis: ['Dono'] },
      { titulo: 'Usuários e permissões', rota: '/configuracoes/usuarios', descricao: 'Usuários, perfis por empresa e senhas.', perfis: ['Dono'] },
      { titulo: 'Licença e assinatura', rota: '/configuracoes/licenca', descricao: 'Plano, vencimento e situação da mensalidade.', perfis: ['Dono'] },
    ],
  },
]

const visivel = (item: { perfis?: Perfil[] }, perfil: string) => !item.perfis || item.perfis.includes(perfil as Perfil)

export function filtrarMenu(menu: GrupoMenu[], perfil: string): GrupoMenu[] {
  return menu.flatMap((grupo) => {
    if (!visivel(grupo, perfil)) return []
    if (!grupo.itens) return [grupo]
    const itens = grupo.itens.filter((item) => visivel(item, perfil))
    return itens.length ? [{ ...grupo, itens }] : []
  })
}

/** Todas as telas navegáveis (folhas do menu). */
export function todasAsRotas(menu: GrupoMenu[]): ItemMenu[] {
  return menu.flatMap((grupo) => (grupo.itens ? grupo.itens : grupo.rota ? [grupo as ItemMenu] : []))
}
