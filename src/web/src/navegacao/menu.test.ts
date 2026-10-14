import { describe, expect, it } from 'vitest'
import { filtrarMenu, MENU, todasAsRotas } from './menu'

const titulos = (perfil: string) => filtrarMenu(MENU, perfil).map((g) => g.titulo)

describe('menu', () => {
  it('Dono vê todos os módulos', () => {
    expect(titulos('Dono')).toEqual(['Dashboard', 'Cadastros', 'Estoque', 'Financeiro', 'Vendas', 'Relatórios', 'Configurações'])
  })

  it('Vendedor não vê Financeiro, Estoque nem Configurações', () => {
    const visiveis = titulos('Vendedor')
    expect(visiveis).not.toContain('Financeiro')
    expect(visiveis).not.toContain('Estoque')
    expect(visiveis).not.toContain('Configurações')
    expect(visiveis).toContain('Vendas')
  })

  it('Gerente vê o histórico de alterações, mas não usuários e licença', () => {
    const config = filtrarMenu(MENU, 'Gerente').find((g) => g.titulo === 'Configurações')
    expect(config?.itens?.map((i) => i.titulo)).toEqual(['Histórico de alterações'])
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
