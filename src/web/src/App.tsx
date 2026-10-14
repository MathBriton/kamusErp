import { useAuth } from './auth/AuthContext'
import { InicioPage } from './pages/InicioPage'
import { LoginPage } from './pages/LoginPage'

export function App() {
  const { estado } = useAuth()

  if (estado.status === 'carregando')
    return <main className="grid min-h-dvh place-items-center text-muted">Carregando...</main>

  return estado.status === 'autenticado' ? <InicioPage sessao={estado.sessao} /> : <LoginPage />
}
