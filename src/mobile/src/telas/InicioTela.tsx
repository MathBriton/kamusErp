import { useState } from 'react'
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native'
import type { Cartao, CodigoDoPeriodo, Dashboard } from '../api/tipos'
import { useDados } from '../api/useDados'
import { Bloco, Carregando, Erro, Texto } from '../componentes/base'
import { moeda, plural, variacao } from '../formato'
import { useCores } from '../tema'

const PERIODOS: { valor: CodigoDoPeriodo; rotulo: string; titulo: string; anterior: string }[] = [
  { valor: 'hoje', rotulo: 'Hoje', titulo: 'hoje', anterior: 'ontem' },
  { valor: '7dias', rotulo: '7 dias', titulo: 'em 7 dias', anterior: '7 dias anteriores' },
  { valor: 'mes', rotulo: 'Mês', titulo: 'no mês', anterior: 'mês anterior' },
]

/** Dashboard do dono: o mesmo /api/indicadores da web, em cartões empilhados. */
export function InicioTela() {
  const c = useCores()
  const [periodo, setPeriodo] = useState<CodigoDoPeriodo>('hoje')
  const { dados, erro, carregando, recarregar } = useDados<Dashboard>(`/api/indicadores?periodo=${periodo}`)
  // O rótulo vem da resposta: enquanto o novo período carrega, título e números continuam juntos.
  const info = PERIODOS.find((p) => p.valor === dados?.periodo) ?? PERIODOS[0]!

  return (
    <ScrollView contentContainerStyle={estilos.conteudo} refreshControl={<RefreshControl refreshing={carregando && !!dados} onRefresh={recarregar} />}>
      <View accessibilityRole="radiogroup" style={[estilos.periodos, { backgroundColor: c.superficie2, borderColor: c.borda }]}>
        {PERIODOS.map((p) => (
          <Pressable key={p.valor} accessibilityRole="radio" accessibilityLabel={p.rotulo} accessibilityState={{ checked: periodo === p.valor }}
            onPress={() => setPeriodo(p.valor)} style={[estilos.periodo, periodo === p.valor && { backgroundColor: c.superficie }]}>
            <Text style={{ color: periodo === p.valor ? c.texto : c.suave, fontWeight: '600' }}>{p.rotulo}</Text>
          </Pressable>
        ))}
      </View>

      {erro && <Erro mensagem={erro} />}
      {!dados && carregando && <Carregando />}

      {dados?.vendas && (
        <>
          <CartaoDeVendas titulo={`${dados.vendas.somenteAsMinhas ? 'Minhas vendas' : 'Vendas'} ${info.titulo}`} cartao={dados.vendas.periodo} anterior={info.anterior} />
          {dados.periodo !== 'mes' && <CartaoDeVendas titulo={`${dados.vendas.somenteAsMinhas ? 'Minhas vendas' : 'Vendas'} no mês`} cartao={dados.vendas.mes} anterior="mês anterior" />}
        </>
      )}

      {dados && dados.alertas.length > 0 && (
        <Bloco titulo="Alertas">
          {dados.alertas.map((a) => (
            <View key={a.tipo} style={[estilos.alerta, { backgroundColor: a.gravidade === 'perigo' ? c.perigoSuave : c.avisoSuave }]}>
              <Texto forte cor={a.gravidade === 'perigo' ? c.perigo : c.aviso}>{a.titulo}</Texto>
              <Texto tamanho={12} cor={a.gravidade === 'perigo' ? c.perigo : c.aviso}>{a.detalhe}</Texto>
            </View>
          ))}
        </Bloco>
      )}

      {dados?.financeiro && (
        <Bloco titulo="Financeiro">
          <Linha rotulo="Saldo das contas" valor={moeda(dados.financeiro.saldoTotal)} forte />
          <Linha rotulo="A receber em 7 dias" valor={moeda(dados.financeiro.aReceber.proximosSeteDias)} />
          <Linha rotulo="A pagar em 7 dias" valor={moeda(dados.financeiro.aPagar.proximosSeteDias)} />
          {dados.financeiro.aPagar.vencido > 0 && <Linha rotulo="A pagar vencido" valor={moeda(dados.financeiro.aPagar.vencido)} cor={c.perigo} />}
        </Bloco>
      )}

      {dados?.vendas && dados.vendas.maisVendidos.length > 0 && (
        <Bloco titulo="Mais vendidos">
          {dados.vendas.maisVendidos.map((m) => (
            <Linha key={m.skuId} rotulo={m.descricao} valor={plural(m.pecas, 'peça', 'peças')} />
          ))}
        </Bloco>
      )}

      {dados?.condicionais && dados.condicionais.abertas > 0 && (
        <Bloco titulo="Condicionais na rua">
          <Texto forte tamanho={22}>{moeda(dados.condicionais.valor)}</Texto>
          <Texto suave>{`${plural(dados.condicionais.abertas, 'aberta', 'abertas')} · ${plural(dados.condicionais.pecas, 'peça', 'peças')}`}</Texto>
        </Bloco>
      )}

      {dados?.estoque && (
        <Bloco titulo="Estoque">
          <Texto forte tamanho={22}>{moeda(dados.estoque.valorDeCusto)}</Texto>
          <Texto suave>{`${plural(dados.estoque.pecas, 'peça', 'peças')} a custo`}</Texto>
        </Bloco>
      )}
    </ScrollView>
  )
}

function CartaoDeVendas({ titulo, cartao, anterior }: { titulo: string; cartao: Cartao; anterior: string }) {
  const c = useCores()
  return (
    <Bloco rotulo={titulo}>
      <Texto suave>{titulo}</Texto>
      <Texto forte tamanho={28}>{moeda(cartao.total)}</Texto>
      {cartao.variacao !== null && (
        <Texto forte tamanho={13} cor={cartao.variacao >= 0 ? c.sucesso : c.perigo}>{`${variacao(cartao.variacao)} vs ${anterior}`}</Texto>
      )}
      <Texto suave tamanho={13}>{`${plural(cartao.vendas, 'venda', 'vendas')} · ticket ${moeda(cartao.ticketMedio)}`}</Texto>
    </Bloco>
  )
}

function Linha({ rotulo, valor, forte, cor }: { rotulo: string; valor: string; forte?: boolean; cor?: string }) {
  return (
    <View style={estilos.linha}>
      <View style={{ flex: 1 }}>
        <Texto cor={cor}>{rotulo}</Texto>
      </View>
      <Texto forte={forte} cor={cor}>{valor}</Texto>
    </View>
  )
}

const estilos = StyleSheet.create({
  conteudo: { padding: 16, gap: 12 },
  periodos: { flexDirection: 'row', borderRadius: 10, borderWidth: 1, padding: 3 },
  periodo: { flex: 1, alignItems: 'center', paddingVertical: 8, borderRadius: 8 },
  alerta: { borderRadius: 10, padding: 10, gap: 2 },
  linha: { flexDirection: 'row', gap: 12, paddingVertical: 2 },
})
