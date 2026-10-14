import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { empresaAtivaHandler, json, mockApi, problema, sessaoDemo } from '../test/mockApi'
import { renderApp } from '../test/renderApp'

const validacao = {
  id: 'imp1', tipo: 'clientes', nomeDoArquivo: 'clientes.xlsx', total: 3, validas: 2, comErro: 1, criar: 1, atualizar: 1,
  linhas: [
    { numero: 2, acao: 'Criar', descricao: 'Maria Silva — cliente novo', erros: [] },
    { numero: 3, acao: 'Erro', descricao: 'CPF Errado — cliente novo', erros: ['CPF inválido.'] },
    { numero: 4, acao: 'Atualizar', descricao: 'Ana — atualiza o cadastro existente', erros: [] },
  ],
}

function api(extras: Parameters<typeof mockApi>[0] = {}) {
  return mockApi({
    'POST /api/auth/refresh': () => json(sessaoDemo),
    'GET /api/empresas/ativa': empresaAtivaHandler(),
    'GET /health': () => new Response('Healthy'),
    ...extras,
  })
}

const arquivo = () => new File(['conteudo'], 'clientes.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })

describe('Cadastros → Importar planilha', () => {
  it('baixa o modelo do tipo escolhido', async () => {
    const chamadas = api({ 'GET /api/importacoes/produtos/modelo.xlsx': () => new Response('xlsx', { headers: { 'Content-Disposition': 'attachment; filename=modelo-produtos.xlsx' } }) })
    URL.createObjectURL = vi.fn(() => 'blob:x') as unknown as typeof URL.createObjectURL
    URL.revokeObjectURL = vi.fn()
    const clique = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    renderApp('/cadastros/importacao')

    await userEvent.click(await screen.findByRole('radio', { name: /Produtos/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Baixar modelo (.xlsx)' }))

    expect(chamadas.mock.calls.some(([url]) => url === '/api/importacoes/produtos/modelo.xlsx')).toBe(true)
    expect(clique).toHaveBeenCalled()
  })

  it('envia a planilha, mostra a prévia com os erros e importa só as válidas', async () => {
    let enviado: FormData | null = null
    api({
      'POST /api/importacoes/clientes/validar': (init) => {
        enviado = init?.body as FormData
        return json(validacao)
      },
      'POST /api/importacoes/imp1/confirmar': () => json({ criados: 1, atualizados: 1, ignorados: 1 }),
    })
    renderApp('/cadastros/importacao')

    await userEvent.upload(await screen.findByLabelText('Planilha (.xlsx ou .csv)'), arquivo())
    await userEvent.click(screen.getByRole('button', { name: 'Validar planilha' }))

    expect((enviado as FormData | null)?.get('arquivo')).toBeInstanceOf(File)
    const resumo = await screen.findByRole('region', { name: 'Prévia da importação' })
    expect(within(resumo).getByText('1 novo')).toBeInTheDocument()
    expect(within(resumo).getByText('1 atualização')).toBeInTheDocument()
    expect(within(resumo).getByText('1 com erro')).toBeInTheDocument()
    const linhaComErro = within(resumo).getByText('CPF Errado — cliente novo').closest('tr')!
    expect(within(linhaComErro).getByText('CPF inválido.')).toBeInTheDocument()
    expect(within(linhaComErro).getByText('3')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Importar 2 linhas válidas' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Importação concluída: 1 criado, 1 atualizado, 1 linha ignorada.')
  })

  it('planilha grande vai para segundo plano', async () => {
    api({
      'POST /api/importacoes/clientes/validar': () => json(validacao),
      'POST /api/importacoes/imp1/confirmar': () => json({ mensagem: 'A importação continua em segundo plano e você será avisado no sininho.' }, 202),
    })
    renderApp('/cadastros/importacao')

    await userEvent.upload(await screen.findByLabelText('Planilha (.xlsx ou .csv)'), arquivo())
    await userEvent.click(screen.getByRole('button', { name: 'Validar planilha' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Importar 2 linhas válidas' }))

    expect(await screen.findByRole('status')).toHaveTextContent('segundo plano')
  })

  it('arquivo recusado mostra o motivo', async () => {
    api({ 'POST /api/importacoes/clientes/validar': () => problema(400, 'Envie um arquivo .xlsx (Excel) ou .csv.') })
    renderApp('/cadastros/importacao')

    await userEvent.upload(await screen.findByLabelText('Planilha (.xlsx ou .csv)'), arquivo())
    await userEvent.click(screen.getByRole('button', { name: 'Validar planilha' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('.xlsx')
  })

  it('nenhuma linha válida: não oferece importar', async () => {
    api({ 'POST /api/importacoes/clientes/validar': () => json({ ...validacao, validas: 0, comErro: 3, criar: 0, atualizar: 0 }) })
    renderApp('/cadastros/importacao')

    await userEvent.upload(await screen.findByLabelText('Planilha (.xlsx ou .csv)'), arquivo())
    await userEvent.click(screen.getByRole('button', { name: 'Validar planilha' }))

    expect(await screen.findByText(/Nenhuma linha pode ser importada/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Importar/ })).not.toBeInTheDocument()
  })

  it('estoque inicial: abre já escolhido pelo link da posição de estoque', async () => {
    api()
    renderApp('/cadastros/importacao?tipo=estoque-inicial')

    expect(await screen.findByRole('radio', { name: /Estoque inicial/ })).toBeChecked()
  })
})
