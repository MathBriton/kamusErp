import { StatusBar } from 'expo-status-bar'
import { SafeAreaProvider } from 'react-native-safe-area-context'
import { SessaoProvider, useSessao } from './src/auth/Sessao'
import { Carregando } from './src/componentes/base'
import { LoginTela } from './src/telas/LoginTela'
import { Principal } from './src/telas/Principal'

function Raiz() {
  const { estado } = useSessao()
  if (estado.status === 'carregando') return <Carregando />
  return estado.status === 'logado' ? <Principal /> : <LoginTela />
}

export default function App() {
  return (
    <SafeAreaProvider>
      <SessaoProvider>
        <Raiz />
      </SessaoProvider>
      <StatusBar style="auto" />
    </SafeAreaProvider>
  )
}
