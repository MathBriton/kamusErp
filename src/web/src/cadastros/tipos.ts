export interface Endereco {
  cep: string | null
  logradouro: string | null
  numero: string | null
  complemento: string | null
  bairro: string | null
  cidade: string | null
  uf: string | null
}

export interface Preferencias {
  blusa: string | null
  calca: string | null
  calcado: string | null
  estilo: string | null
}

export interface Pessoa {
  id: string
  tipo: 'Fisica' | 'Juridica'
  nome: string
  nomeFantasia: string | null
  documento: string | null
  ehCliente: boolean
  ehFornecedor: boolean
  telefone: string | null
  whatsApp: string | null
  email: string | null
  dataNascimento: string | null
  endereco: Endereco
  preferencias: Preferencias
  observacoes: string | null
  aceitaMarketing: boolean
  consentimentoMarketingEm: string | null
  ativo: boolean
  criadoEm: string
  anonimizadoEm: string | null
}

export interface Aniversariante {
  id: string
  nome: string
  dataNascimento: string
  idade: number
  whatsApp: string | null
  aceitaMarketing: boolean
}

export type Papel = 'cliente' | 'fornecedor'

export const linkWhatsApp = (numero: string, mensagem?: string) =>
  `https://wa.me/${numero}${mensagem ? `?text=${encodeURIComponent(mensagem)}` : ''}`

/** "11987654321" → "(11) 98765-4321". */
export function formatarTelefone(telefone: string | null) {
  if (!telefone) return '—'
  const ddd = telefone.slice(0, 2)
  const numero = telefone.slice(2)
  return `(${ddd}) ${numero.slice(0, numero.length - 4)}-${numero.slice(-4)}`
}
