import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { GradeDeRegistros, type Coluna } from './GradeDeRegistros'

interface Item { id: string; nome: string; valor: number }
const colunas: Coluna<Item>[] = [
  { titulo: 'Nome', celula: (i) => i.nome },
  { titulo: 'Valor', celula: (i) => String(i.valor), alinhar: 'direita' },
]
const pagina1: Item[] = [{ id: 'a', nome: 'Alfa', valor: 1 }, { id: 'b', nome: 'Beta', valor: 2 }]

function Exemplo({ total = 45, itens = pagina1, onPagina = vi.fn(), onTamanho = vi.fn(), pagina = 1, tamanho = 20 }: Partial<{
  total: number; itens: Item[]; onPagina: (p: number) => void; onTamanho: (t: number) => void; pagina: number; tamanho: number
}>) {
  const [selecionados, setSelecionados] = useState<Set<string>>(new Set())
  return (
    <GradeDeRegistros
      rotulo="Itens"
      colunas={colunas}
      itens={itens}
      chave={(i) => i.id}
      nome={(i) => i.nome}
      total={total}
      pagina={pagina}
      tamanho={tamanho}
      onPagina={onPagina}
      onTamanho={onTamanho}
      selecao={{ selecionados, onMudar: setSelecionados }}
      acoes={(i) => <button type="button">{`Editar ${i.nome}`}</button>}
      acoesEmLote={<button type="button">Inativar selecionados</button>}
    />
  )
}

describe('GradeDeRegistros', () => {
  it('mostra o total de registros, o intervalo e a página atual', () => {
    render(<Exemplo />)
    expect(screen.getByText('45 registros')).toBeInTheDocument()
    expect(screen.getByText('Mostrando 1–20')).toBeInTheDocument()
    expect(screen.getByText('Página 1 de 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Página anterior' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Primeira página' })).toBeDisabled()
  })

  it('navega entre páginas e troca o tamanho da página', async () => {
    const onPagina = vi.fn()
    const onTamanho = vi.fn()
    render(<Exemplo onPagina={onPagina} onTamanho={onTamanho} />)

    await userEvent.click(screen.getByRole('button', { name: 'Próxima página' }))
    expect(onPagina).toHaveBeenLastCalledWith(2)
    await userEvent.click(screen.getByRole('button', { name: 'Última página' }))
    expect(onPagina).toHaveBeenLastCalledWith(3)
    await userEvent.selectOptions(screen.getByLabelText('Registros por página'), '50')
    expect(onTamanho).toHaveBeenLastCalledWith(50)
  })

  it('na última página desabilita avançar e mostra o intervalo final', () => {
    render(<Exemplo pagina={3} itens={[{ id: 'z', nome: 'Zeta', valor: 9 }]} />)
    expect(screen.getByText('Mostrando 41–45')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Próxima página' })).toBeDisabled()
  })

  it('seleciona linhas, mostra a barra com a contagem e as ações em lote, e limpa', async () => {
    render(<Exemplo />)
    expect(screen.queryByRole('region', { name: 'Seleção' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Alfa' }))
    const barra = screen.getByRole('region', { name: 'Seleção' })
    expect(barra).toHaveTextContent('1 selecionado')
    expect(within(barra).getByRole('button', { name: 'Inativar selecionados' })).toBeInTheDocument()

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar todos da página' }))
    expect(barra).toHaveTextContent('2 selecionados')
    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar todos da página' }))
    expect(screen.queryByRole('region', { name: 'Seleção' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('checkbox', { name: 'Selecionar Beta' }))
    await userEvent.click(screen.getByRole('button', { name: 'Limpar seleção' }))
    expect(screen.getByRole('checkbox', { name: 'Selecionar Beta' })).not.toBeChecked()
  })

  it('mostra as ações de cada linha e a mensagem quando não há registros', () => {
    const { rerender } = render(<Exemplo />)
    const tabela = screen.getByRole('table', { name: 'Itens' })
    expect(within(tabela).getByRole('row', { name: /Alfa/ })).toHaveTextContent('Editar Alfa')

    rerender(<Exemplo total={0} itens={[]} />)
    expect(screen.getByText('Nenhum registro encontrado.')).toBeInTheDocument()
    expect(screen.getByText('0 registros')).toBeInTheDocument()
  })
})
