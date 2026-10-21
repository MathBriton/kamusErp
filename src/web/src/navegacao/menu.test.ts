import { describe, expect, it } from 'vitest'
import { filtrarMenu, MENU, todasAsRotas } from './menu'

const titulos = (perfil: string) => filtrarMenu(MENU, perfil).map((g) => g.titulo)

describe('menu', () => {
  it('Dono vê todos os módulos', () => {
    expect(titulos('Dono')).toEqual(['Dashboard', 'Cadastros', 'Estoque', 'Financeiro', 'Vendas', 'Relatórios', 'Administrativo'])
  })

  it('Vendedor não vê Financeiro nem Administrativo; no Estoque, só a posição', () => {
    const visiveis = titulos('Vendedor')
    expect(visiveis).not.toContain('Financeiro')
    expect(visiveis).not.toContain('Administrativo')
    expect(visiveis).toContain('Vendas')
    const estoque = filtrarMenu(MENU, 'Vendedor').find((g) => g.titulo === 'Estoque')
    expect(estoque?.itens?.map((i) => i.titulo)).toEqual(['Posição de estoque'])
  })

  it('Vendedor registra e consulta vendas, sem comissões nem formas de pagamento', () => {
    const vendas = filtrarMenu(MENU, 'Vendedor').find((g) => g.titulo === 'Vendas')?.itens?.map((i) => i.titulo)
    expect(vendas).toContain('Registrar venda')
    expect(vendas).toContain('Vendas realizadas')
    expect(vendas).not.toContain('Comissões')
    expect(vendas).not.toContain('Formas de pagamento')
  })

  it('Gerente vê o histórico de alterações, mas não usuários e licença', () => {
    const config = filtrarMenu(MENU, 'Gerente').find((g) => g.titulo === 'Administrativo')
    expect(config?.itens?.map((i) => i.titulo)).toEqual(['Histórico de alterações'])
  })

  it('Administrativo começa por Usuários e senhas para o Dono', () => {
    const admin = filtrarMenu(MENU, 'Dono').find((g) => g.titulo === 'Administrativo')
    expect(admin?.itens?.map((i) => i.titulo)).toEqual(['Usuários e senhas', 'Empresas', 'Lojas', 'Licença e assinatura', 'Histórico de alterações'])
  })

  it('grupo sem nenhum item visível some do menu', () => {
    const cadastros = filtrarMenu(MENU, 'Vendedor').find((g) => g.titulo === 'Cadastros')
    expect(cadastros?.itens?.map((i) => i.titulo)).toEqual(['Clientes'])
  })

  it('toda rota do menu é única e começa com /', () => {
    const rotas = todasAsRotas(MENU).map((r) => r.rota)
    expect(new Set(rotas).size).toBe(rotas.length)
    rotas.forEach((r) => expect(r.startsWith('/')).toBe(true))
  })
})
