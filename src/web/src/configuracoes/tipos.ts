export interface EmpresaDoCliente {
  id: string
  razaoSocial: string
  nomeFantasia: string
  cnpj: string
}

export interface Acesso {
  empresaId: string
  empresa: string
  perfil: string
}

export interface UsuarioDoCliente {
  id: string
  nome: string
  email: string
  ativo: boolean
  deveTrocarSenha: boolean
  ultimoLoginEm: string | null
  acessos: Acesso[]
}

export interface SenhaProvisoria {
  id: string
  email: string
  senhaProvisoria: string
}

export const PERFIS = ['Dono', 'Gerente', 'Vendedor', 'Estoquista', 'Financeiro'] as const
