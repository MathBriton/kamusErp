import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native'
import type { LinhaDeVenda } from '../api/tipos'
import { useDados } from '../api/useDados'
import { Bloco, Carregando, Erro, Texto } from '../componentes/base'
import { hojeIso, moeda, plural } from '../formato'
import { useCores } from '../tema'

interface Lista {
  itens: LinhaDeVenda[]
  quantidade: number
  total: number
}

/** Vendas de hoje (o Vendedor vê só as dele, como na web). */
export function VendasTela() {
  const c = useCores()
  const hoje = hojeIso()
  const { dados, erro, carregando, recarregar } = useDados<Lista>(`/api/vendas?de=${hoje}&ate=${hoje}`)

  return (
    <ScrollView contentContainerStyle={estilos.conteudo} refreshControl={<RefreshControl refreshing={carregando && !!dados} onRefresh={recarregar} />}>
      {erro && <Erro mensagem={erro} />}
      {!dados && carregando && <Carregando />}
      {dados && (
        <Bloco titulo="Vendas de hoje">
          <Texto suave>{`${plural(dados.quantidade, 'venda', 'vendas')} · ${moeda(dados.total)}`}</Texto>
          {dados.itens.length === 0 && <Texto suave>Nenhuma venda hoje.</Texto>}
          {dados.itens.map((v) => (
            <View key={v.id} style={[estilos.item, { borderTopColor: c.borda }]}>
              <View style={{ flex: 1 }}>
                <Texto forte>{`Venda nº ${v.numero}${v.clienteNome ? ` · ${v.clienteNome}` : ''}`}</Texto>
                <Texto suave tamanho={12}>{[plural(v.pecas, 'peça', 'peças'), v.formas, v.vendedorNome].filter(Boolean).join(' · ')}</Texto>
              </View>
              <Texto forte cor={v.status === 'Cancelada' ? c.suave : undefined}>{v.status === 'Cancelada' ? 'Cancelada' : moeda(v.total)}</Texto>
            </View>
          ))}
        </Bloco>
      )}
    </ScrollView>
  )
}

const estilos = StyleSheet.create({
  conteudo: { padding: 16, gap: 12 },
  item: { flexDirection: 'row', gap: 12, alignItems: 'center', borderTopWidth: StyleSheet.hairlineWidth, paddingTop: 10, marginTop: 4 },
})
