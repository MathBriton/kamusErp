import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native'
import type { Parcela } from '../api/tipos'
import { useDados } from '../api/useDados'
import { Bloco, Carregando, Erro, Texto } from '../componentes/base'
import { diaMes, hojeIso, moeda, somarDias } from '../formato'
import { useCores } from '../tema'

interface ListaDeParcelas {
  itens: Parcela[]
  saldo: number
  vencido: number
}

/** Contas a pagar e a receber em aberto: vencidas e as dos próximos 7 dias. */
export function ContasTela() {
  const ate = somarDias(hojeIso(), 7)
  const pagar = useDados<ListaDeParcelas>(`/api/financeiro/titulos?tipo=pagar&situacao=abertas&ate=${ate}`)
  const receber = useDados<ListaDeParcelas>(`/api/financeiro/titulos?tipo=receber&situacao=abertas&ate=${ate}`)
  const carregando = pagar.carregando || receber.carregando

  return (
    <ScrollView
      contentContainerStyle={estilos.conteudo}
      refreshControl={<RefreshControl refreshing={carregando && !!pagar.dados} onRefresh={() => (pagar.recarregar(), receber.recarregar())} />}
    >
      <Lista titulo="A pagar" vazio="Nada a pagar nos próximos 7 dias." {...pagar} />
      <Lista titulo="A receber" vazio="Nada a receber nos próximos 7 dias." {...receber} />
    </ScrollView>
  )
}

function Lista({ titulo, vazio, dados, erro, carregando }: { titulo: string; vazio: string; dados: ListaDeParcelas | null; erro: string | null; carregando: boolean }) {
  const c = useCores()
  if (erro) return <Erro mensagem={erro} />
  if (!dados) return carregando ? <Carregando /> : null
  return (
    <Bloco titulo={titulo}>
      {dados.itens.length === 0 ? (
        <Texto suave>{vazio}</Texto>
      ) : (
        <>
          <Texto suave>{`${moeda(dados.saldo)} em aberto${dados.vencido > 0 ? ` · ${moeda(dados.vencido)} vencido` : ''}`}</Texto>
          {dados.itens.map((p) => {
            const vencida = p.situacao === 'Vencida'
            return (
              <View key={p.parcelaId} style={[estilos.item, { borderTopColor: c.borda }]}>
                <View style={{ flex: 1 }}>
                  <Texto forte>{p.descricao}</Texto>
                  <Texto suave tamanho={12}>{[`vence ${diaMes(p.vencimento)}`, p.pessoaNome].filter(Boolean).join(' · ')}</Texto>
                </View>
                <View style={{ alignItems: 'flex-end' }}>
                  <Texto forte>{moeda(p.saldo)}</Texto>
                  {vencida && <Texto tamanho={12} cor={c.perigo}>Vencida</Texto>}
                </View>
              </View>
            )
          })}
        </>
      )}
    </Bloco>
  )
}

const estilos = StyleSheet.create({
  conteudo: { padding: 16, gap: 12 },
  item: { flexDirection: 'row', gap: 12, alignItems: 'center', borderTopWidth: StyleSheet.hairlineWidth, paddingTop: 10, marginTop: 4 },
})
