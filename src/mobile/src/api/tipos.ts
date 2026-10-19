export interface Empresa {
  id: string
  nomeFantasia: string
  cnpj: string
  perfil: string
}

export interface SessaoMobile {
  accessToken: string
  usuario: { id: string; nome: string; email: string; deveTrocarSenha: boolean }
  empresas: Empresa[]
  tokenDeSessao: string
}

export type CodigoDoPeriodo = 'hoje' | '7dias' | 'mes'

export interface Cartao {
  total: number
  vendas: number
  pecas: number
  ticketMedio: number
  totalAnterior: number
  variacao: number | null
}

export interface Compromissos {
  hoje: number
  amanha: number
  proximosSeteDias: number
  vencido: number
  parcelasVencidas: number
}

export interface Dashboard {
  periodo: CodigoDoPeriodo
  vendas: {
    periodo: Cartao
    mes: Cartao
    porForma: { nome: string; quantidade: number; total: number }[]
    maisVendidos: { skuId: string; descricao: string; pecas: number; total: number }[]
    somenteAsMinhas: boolean
  } | null
  financeiro: { saldoTotal: number; aReceber: Compromissos; aPagar: Compromissos } | null
  estoque: { pecas: number; valorDeCusto: number; totalDeQuebras: number } | null
  condicionais: { abertas: number; atrasadas: number; pecas: number; valor: number } | null
  alertas: { tipo: string; gravidade: 'perigo' | 'aviso'; titulo: string; detalhe: string }[]
}

export interface LinhaDeVenda {
  id: string
  numero: number
  clienteNome: string | null
  vendedorNome: string | null
  pecas: number
  total: number
  formas: string
  status: 'Concluida' | 'Cancelada'
}

export interface Parcela {
  parcelaId: string
  vencimento: string
  descricao: string
  pessoaNome: string | null
  saldo: number
  situacao: string
}

export interface Notificacao {
  id: string
  tipo: string
  titulo: string
  mensagem: string
  criadaEm: string
  lidaEm: string | null
}
