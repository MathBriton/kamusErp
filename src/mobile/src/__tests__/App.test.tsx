import { render, screen, userEvent, waitFor, within } from '@testing-library/react-native'
import * as SecureStore from 'expo-secure-store'
import * as Notifications from 'expo-notifications'
import App from '../../App'
import { hojeIso, somarDias } from '../formato'
import { cabecalho, corpo, dashboard, json, mockApi, problema, sessao } from '../test/mockApi'

const hoje = hojeIso()

const basico = (extras: Parameters<typeof mockApi>[0] = {}) =>
  mockApi({
    'POST /api/auth/mobile/login': () => json(sessao()),
    'POST /api/auth/mobile/refresh': () => json(sessao({ tokenDeSessao: 'sessao-2', accessToken: 'token-renovado' })),
    'POST /api/auth/mobile/logout': () => new Response(null, { status: 204 }),
    'GET /api/indicadores?periodo=hoje': () => json(dashboard()),
    'PUT /api/notificacoes/dispositivos': () => new Response(null, { status: 204 }),
    'GET /api/notificacoes': () => json({ itens: [], naoLidas: 0 }),
    ...extras,
  })

async function entrar() {
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('E-mail'), 'admin@kamus.local')
  await user.type(screen.getByLabelText('Senha'), 'Kamus@Dev123')
  await user.press(screen.getByRole('button', { name: 'Entrar' }))
  return user
}

describe('App mobile', () => {
  it('entra, guarda o token de sessão com segurança e mostra o dashboard', async () => {
    const chamadas = basico()
    await render(<App />)

    await entrar()

    expect(await screen.findByText('Vendas hoje')).toBeOnTheScreen()
    expect(screen.getByText('R$ 179,70')).toBeOnTheScreen()
    expect(screen.getByText('▲ 200% vs ontem')).toBeOnTheScreen()
    expect(screen.getByText('1 conta a pagar vencida')).toBeOnTheScreen()
    const login = chamadas.find((c) => c.rota === 'POST /api/auth/mobile/login')!
    expect(corpo(login.init)).toEqual({ email: 'admin@kamus.local', senha: 'Kamus@Dev123' })
    expect(SecureStore.setItemAsync).toHaveBeenCalledWith('kamus_sessao', 'sessao-1')
    const indicadores = chamadas.find((c) => c.rota === 'GET /api/indicadores?periodo=hoje')!
    expect(cabecalho(indicadores.init, 'Authorization')).toBe('Bearer token-de-acesso')
    expect(cabecalho(indicadores.init, 'X-Empresa-Id')).toBe('e1')
  })

  it('login inválido mostra o motivo', async () => {
    basico({ 'POST /api/auth/mobile/login': () => problema(401, 'E-mail ou senha inválidos.') })
    await render(<App />)

    await entrar()

    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha inválidos.')
  })

  it('abre já logado com o token guardado e registra o aparelho para push', async () => {
    await SecureStore.setItemAsync('kamus_sessao', 'sessao-guardada')
    const chamadas = basico()
    await render(<App />)

    expect(await screen.findByText('Vendas hoje')).toBeOnTheScreen()
    expect(corpo(chamadas.find((c) => c.rota === 'POST /api/auth/mobile/refresh')!.init)).toEqual({ tokenDeSessao: 'sessao-guardada' })
    await waitFor(() => expect(chamadas.some((c) => c.rota === 'PUT /api/notificacoes/dispositivos')).toBe(true))
    const registro = chamadas.find((c) => c.rota === 'PUT /api/notificacoes/dispositivos')!
    expect(corpo(registro.init)).toMatchObject({ token: 'ExponentPushToken[teste]' })
    expect(Notifications.getExpoPushTokenAsync).toHaveBeenCalledWith({ projectId: 'projeto-teste' })
  })

  it('token de acesso vencido: renova a sessão e repete a chamada', async () => {
    let tentativas = 0
    const chamadas = basico({
      'GET /api/indicadores?periodo=hoje': () => (++tentativas === 1 ? new Response(null, { status: 401 }) : json(dashboard())),
    })
    await render(<App />)
    await entrar()

    expect(await screen.findByText('Vendas hoje')).toBeOnTheScreen()
    expect(chamadas.filter((c) => c.rota === 'POST /api/auth/mobile/refresh')).toHaveLength(1)
    expect(cabecalho(chamadas.filter((c) => c.rota === 'GET /api/indicadores?periodo=hoje')[1]!.init, 'Authorization')).toBe('Bearer token-renovado')
  })

  it('troca o período e a empresa (inclusive todas)', async () => {
    const chamadas = basico({
      'GET /api/indicadores?periodo=mes': () => json(dashboard({ periodo: 'mes' })),
    })
    await render(<App />)
    const user = await entrar()
    await screen.findByText('Vendas hoje')

    await user.press(screen.getByRole('radio', { name: 'Mês' }))
    expect(await screen.findByText('Vendas no mês')).toBeOnTheScreen()

    await user.press(screen.getByRole('button', { name: /Empresa ativa/ }))
    await user.press(await screen.findByRole('button', { name: 'Todas as empresas' }))
    await waitFor(() => expect(chamadas.some((c) => c.rota === 'GET /api/indicadores?periodo=mes' && cabecalho(c.init, 'X-Empresa-Id') === 'todas')).toBe(true))
    expect(screen.getByRole('button', { name: 'Empresa ativa: Todas as empresas' })).toBeOnTheScreen()
  })

  it('abas de vendas de hoje, contas e avisos', async () => {
    let lidas = false
    basico({
      [`GET /api/vendas?de=${hoje}&ate=${hoje}`]: () =>
        json({
          itens: [{ id: 'v1', numero: 8, data: hoje, clienteNome: 'Maria Souza', vendedorNome: 'Bia', pecas: 2, total: 119.8, formas: 'PIX', status: 'Concluida' }],
          quantidade: 1, pecas: 2, total: 119.8, ticketMedio: 119.8,
        }),
      [`GET /api/financeiro/titulos?tipo=pagar&situacao=abertas&ate=${somarDias(hoje, 7)}`]: () =>
        json({ itens: [{ parcelaId: 'p1', vencimento: somarDias(hoje, -1), descricao: 'Aluguel', pessoaNome: null, saldo: 1500, situacao: 'Vencida', numero: 1, parcelas: 1 }], saldo: 1500, vencido: 1500 }),
      [`GET /api/financeiro/titulos?tipo=receber&situacao=abertas&ate=${somarDias(hoje, 7)}`]: () => json({ itens: [], saldo: 0, vencido: 0 }),
      'GET /api/notificacoes': () =>
        json({ itens: [{ id: 'n1', tipo: 'resumo_diario', titulo: 'Resumo do dia 19/10', mensagem: 'Vendas: R$ 179,70', link: '/', criadaEm: new Date().toISOString(), lidaEm: lidas ? new Date().toISOString() : null }], naoLidas: lidas ? 0 : 1 }),
      'POST /api/notificacoes/lidas': () => {
        lidas = true
        return new Response(null, { status: 204 })
      },
    })
    await render(<App />)
    const user = await entrar()
    await screen.findByText('Vendas hoje')

    await user.press(screen.getByRole('tab', { name: 'Vendas' }))
    expect(await screen.findByText('Venda nº 8 · Maria Souza')).toBeOnTheScreen()
    expect(screen.getByText('1 venda · R$ 119,80')).toBeOnTheScreen()

    await user.press(screen.getByRole('tab', { name: 'Contas' }))
    const pagar = await screen.findByLabelText('A pagar')
    expect(within(pagar).getByText('Aluguel')).toBeOnTheScreen()
    expect(within(pagar).getByText('Vencida')).toBeOnTheScreen()
    expect(screen.getByText('Nada a receber nos próximos 7 dias.')).toBeOnTheScreen()

    await user.press(screen.getByRole('tab', { name: /Avisos/ }))
    expect(await screen.findByText('Resumo do dia 19/10')).toBeOnTheScreen()
    await user.press(screen.getByRole('button', { name: 'Marcar todas como lidas' }))
    await waitFor(() => expect(lidas).toBe(true))
  })

  it('sair encerra a sessão, esquece o token e volta ao login', async () => {
    const chamadas = basico()
    await render(<App />)
    const user = await entrar()
    await screen.findByText('Vendas hoje')

    await user.press(screen.getByRole('button', { name: 'Sair' }))

    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeOnTheScreen()
    expect(corpo(chamadas.find((c) => c.rota === 'POST /api/auth/mobile/logout')!.init)).toEqual({ tokenDeSessao: 'sessao-1' })
    expect(SecureStore.deleteItemAsync).toHaveBeenCalledWith('kamus_sessao')
  })
})
