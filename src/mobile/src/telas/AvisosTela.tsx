import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native'
import type { Notificacao } from '../api/tipos'
import { useDados } from '../api/useDados'
import { useSessao } from '../auth/Sessao'
import { Bloco, Botao, Carregando, Erro, Texto } from '../componentes/base'
import { useCores } from '../tema'

/** As notificações do sininho (inclusive o resumo diário), as mesmas que chegam por push. */
export function AvisosTela({ onLidas }: { onLidas: () => void }) {
  const c = useCores()
  const { enviar } = useSessao()
  const { dados, erro, carregando, recarregar } = useDados<{ itens: Notificacao[]; naoLidas: number }>('/api/notificacoes')

  async function marcarLidas() {
    await enviar('POST', '/api/notificacoes/lidas')
    recarregar()
    onLidas()
  }

  return (
    <ScrollView contentContainerStyle={estilos.conteudo} refreshControl={<RefreshControl refreshing={carregando && !!dados} onRefresh={recarregar} />}>
      {erro && <Erro mensagem={erro} />}
      {!dados && carregando && <Carregando />}
      {dados && dados.naoLidas > 0 && <Botao rotulo="Marcar todas como lidas" onPress={() => void marcarLidas()} />}
      {dados?.itens.length === 0 && <Texto suave>Nenhum aviso por enquanto.</Texto>}
      {dados?.itens.map((n) => (
        <Bloco key={n.id} rotulo={n.titulo}>
          <View style={estilos.titulo}>
            {!n.lidaEm && <View accessibilityLabel="Não lido" style={[estilos.ponto, { backgroundColor: c.primaria }]} />}
            <Texto forte>{n.titulo}</Texto>
          </View>
          <Texto suave tamanho={13}>{n.mensagem}</Texto>
        </Bloco>
      ))}
    </ScrollView>
  )
}

const estilos = StyleSheet.create({
  conteudo: { padding: 16, gap: 12 },
  titulo: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  ponto: { width: 8, height: 8, borderRadius: 4 },
})
