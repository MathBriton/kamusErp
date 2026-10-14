import type { ReactNode } from 'react'
import { Route, Routes } from 'react-router'
import { useAuth } from './auth/AuthContext'
import { EmpresaProvider } from './empresa/EmpresaProvider'
import { Shell } from './layout/Shell'
import { MENU, todasAsRotas } from './navegacao/menu'
import { DashboardPage } from './pages/DashboardPage'
import { EmConstrucaoPage } from './pages/EmConstrucaoPage'
import { HistoricoPage } from './pages/HistoricoPage'
import { LoginPage } from './pages/LoginPage'
import { NaoEncontradaPage } from './pages/NaoEncontradaPage'

/** Telas já implementadas; as demais rotas do menu mostram "Previsto para a vX". */
const PAGINAS_IMPLEMENTADAS: Record<string, ReactNode> = {
  '/configuracoes/historico': <HistoricoPage />,
}

const ROTAS_DO_MENU = todasAsRotas(MENU).filter((item) => item.rota !== '/')

export function App() {
  const { estado } = useAuth()

  if (estado.status === 'carregando')
    return <main className="grid min-h-dvh place-items-center text-muted">Carregando...</main>

  if (estado.status === 'anonimo') return <LoginPage />

  return (
    // key: trocar de usuário recria o contexto de empresa do zero
    <EmpresaProvider key={estado.sessao.usuario.id} sessao={estado.sessao}>
      <Routes>
        <Route element={<Shell sessao={estado.sessao} />}>
          <Route index element={<DashboardPage />} />
          {ROTAS_DO_MENU.map((item) => (
            <Route key={item.rota} path={item.rota} element={PAGINAS_IMPLEMENTADAS[item.rota] ?? <EmConstrucaoPage item={item} />} />
          ))}
          <Route path="*" element={<NaoEncontradaPage />} />
        </Route>
      </Routes>
    </EmpresaProvider>
  )
}
