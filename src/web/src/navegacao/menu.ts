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
      { titulo: 'Importar planilha', rota: '/cadastros/importacao', descricao: 'Clientes, fornecedores, produtos e estoque inicial a partir do Excel.', perfis: [...GESTAO, 'Estoquista'] },
      { titulo: 'Vendedores', rota: '/cadastros/vendedores', descricao: 'Vendedores e comissão padrão.', perfis: GESTAO },
    ],
  },
  {
    titulo: 'Estoque',
    icone: 'box',
    itens: [
      { titulo: 'Posição de estoque', rota: '/estoque/posicao', descricao: 'Saldo por produto em matriz cor × tamanho, por loja, com valor em estoque e kardex.' },
      { titulo: 'Entrada de mercadoria', rota: '/estoque/entradas', descricao: 'Entrada digitando na matriz de grade, com custo médio.', perfis: [...GESTAO, 'Estoquista'] },
      { titulo: 'Ajustes', rota: '/estoque/ajustes', descricao: 'Perdas, avarias e correções com motivo obrigatório.', perfis: [...GESTAO, 'Estoquista'] },
      { titulo: 'Transferências', rota: '/estoque/transferencias', descricao: 'Transferência de peças entre lojas.', perfis: [...GESTAO, 'Estoquista'] },
      { titulo: 'Entrada por XML da NF-e', rota: '/estoque/xml', descricao: 'Importação do XML da nota do fornecedor, com de-para memorizado.', perfis: [...GESTAO, 'Estoquista'] },
      { titulo: 'Inventário', rota: '/estoque/inventario', descricao: 'Contagem física e ajuste pela diferença.', perfis: [...GESTAO, 'Estoquista'] },
      { titulo: 'Etiquetas', rota: '/estoque/etiquetas', descricao: 'Etiquetas com código de barras, cor, tamanho e preço.', perfis: [...GESTAO, 'Estoquista'] },
    ],
  },
  {
    titulo: 'Financeiro',
    icone: 'wallet',
    perfis: [...GESTAO, 'Financeiro'],
    itens: [
      { titulo: 'Contas a pagar', rota: '/financeiro/contas-a-pagar', descricao: 'Fornecedores e despesas: parcelas, pagamentos parciais, juros, multa e desconto.' },
      { titulo: 'Contas a receber', rota: '/financeiro/contas-a-receber', descricao: 'Valores a receber de clientes e outras receitas.' },
      { titulo: 'Fluxo de caixa', rota: '/financeiro/fluxo-de-caixa', descricao: 'Realizado até hoje e previsto daqui em diante.' },
      { titulo: 'Contas financeiras', rota: '/financeiro/contas', descricao: 'Caixa da loja, contas bancárias e carteiras: saldo, extrato e transferências.' },
      { titulo: 'Categorias', rota: '/financeiro/categorias', descricao: 'Plano de contas: grupos e categorias de receitas e despesas.' },
    ],
  },
  {
    titulo: 'Vendas',
    icone: 'bag',
    perfis: [...GESTAO, 'Vendedor', 'Financeiro'],
    itens: [
      { titulo: 'Registrar venda', rota: '/vendas/registro', descricao: 'Venda no backoffice (sem emissão fiscal) com baixa de estoque e lançamento financeiro.', perfis: [...GESTAO, 'Vendedor'] },
      { titulo: 'Vendas realizadas', rota: '/vendas/realizadas', descricao: 'Vendas do período com totais, detalhe e cancelamento.' },
      { titulo: 'Comissões', rota: '/vendas/comissoes', descricao: 'Comissão por vendedor no período, fechamento e conta a pagar.', perfis: [...GESTAO, 'Financeiro'] },
      { titulo: 'Formas de pagamento', rota: '/vendas/formas-de-pagamento', descricao: 'Dinheiro, PIX e cartões: taxas, prazos, parcelas e conta de entrada.', perfis: GESTAO },
      { titulo: 'Trocas', rota: '/vendas/trocas', descricao: 'Troca vinculada à venda: devolve, leva outra peça e acerta a diferença ou emite vale-troca.', perfis: [...GESTAO, 'Vendedor'] },
      { titulo: 'Vales-troca', rota: '/vendas/vales-troca', descricao: 'Crédito do cliente gerado nas trocas: saldo, validade e cancelamento.' },
      { titulo: 'Condicional', rota: '/vendas/condicional', descricao: 'Peças que o cliente leva para provar, com prazo de devolução.', perfis: [...GESTAO, 'Vendedor'] },
      { titulo: 'Crediário', rota: '/vendas/crediario', descricao: 'Limite por cliente, carnê, recebimento de parcelas e inadimplência.' },
    ],
  },
  {
    titulo: 'Relatórios',
    icone: 'file',
    rota: '/relatorios',
    previstoPara: 'v0.18.0',
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
      { titulo: 'Lojas', rota: '/configuracoes/lojas', descricao: 'Lojas (locais de estoque) de cada empresa.', perfis: ['Dono'] },
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
