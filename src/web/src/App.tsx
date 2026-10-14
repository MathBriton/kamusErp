import type { ReactNode } from 'react'
import { Route, Routes } from 'react-router'
import { useAuth } from './auth/AuthContext'
import { EmpresaProvider } from './empresa/EmpresaProvider'
import { Shell } from './layout/Shell'
import { LicencaPage } from './licenca/LicencaPage'
import { LicencaProvider } from './licenca/LicencaProvider'
import { MENU, todasAsRotas } from './navegacao/menu'
import { DashboardPage } from './pages/DashboardPage'
import { EmConstrucaoPage } from './pages/EmConstrucaoPage'
import { TrocarSenhaPage } from './configuracoes/TrocarSenhaPage'
import { EmpresasPage } from './configuracoes/EmpresasPage'
import { UsuariosPage } from './configuracoes/UsuariosPage'
import { PessoasPage } from './cadastros/PessoasPage'
import { VendedoresPage } from './cadastros/VendedoresPage'
import { GradesPage } from './cadastros/GradesPage'
import { ProdutosPage } from './cadastros/ProdutosPage'
import { LojasPage } from './estoque/LojasPage'
import { MovimentacaoPage } from './estoque/MovimentacaoPage'
import { PosicaoPage } from './estoque/PosicaoPage'
import { ImportacaoPage } from './cadastros/ImportacaoPage'
import { HistoricoPage } from './pages/HistoricoPage'
import { LoginPage } from './pages/LoginPage'
import { NaoEncontradaPage } from './pages/NaoEncontradaPage'

/** Telas já implementadas; as demais rotas do menu mostram "Previsto para a vX". */
const PAGINAS_IMPLEMENTADAS: Record<string, ReactNode> = {
  '/configuracoes/historico': <HistoricoPage />,
  '/configuracoes/licenca': <LicencaPage />,
  '/configuracoes/empresas': <EmpresasPage />,
  '/configuracoes/usuarios': <UsuariosPage />,
  '/cadastros/clientes': <PessoasPage key="cliente" papel="cliente" />,
  '/cadastros/fornecedores': <PessoasPage key="fornecedor" papel="fornecedor" />,
  '/cadastros/vendedores': <VendedoresPage />,
  '/cadastros/grades': <GradesPage />,
  '/cadastros/produtos': <ProdutosPage />,
  '/cadastros/importacao': <ImportacaoPage />,
  '/estoque/posicao': <PosicaoPage />,
  '/estoque/entradas': <MovimentacaoPage key="entrada" modo="entrada" />,
  '/estoque/ajustes': <MovimentacaoPage key="ajuste" modo="ajuste" />,
  '/estoque/transferencias': <MovimentacaoPage key="transferencia" modo="transferencia" />,
  '/configuracoes/lojas': <LojasPage />,
}

const ROTAS_DO_MENU = todasAsRotas(MENU).filter((item) => item.rota !== '/')

export function App() {
  const { estado } = useAuth()

  if (estado.status === 'carregando')
    return <main className="grid min-h-dvh place-items-center text-muted">Carregando...</main>

  if (estado.status === 'anonimo') return <LoginPage />

  if (estado.sessao.usuario.deveTrocarSenha) return <TrocarSenhaPage nome={estado.sessao.usuario.nome} />

  return (
    // key: trocar de usuário recria o contexto de empresa do zero
    <EmpresaProvider key={estado.sessao.usuario.id} sessao={estado.sessao}>
      <LicencaProvider>
        <Routes>
          <Route element={<Shell sessao={estado.sessao} />}>
            <Route index element={<DashboardPage />} />
            {ROTAS_DO_MENU.map((item) => (
              <Route key={item.rota} path={item.rota} element={PAGINAS_IMPLEMENTADAS[item.rota] ?? <EmConstrucaoPage item={item} />} />
            ))}
            <Route path="*" element={<NaoEncontradaPage />} />
          </Route>
        </Routes>
      </LicencaProvider>
    </EmpresaProvider>
  )
}
