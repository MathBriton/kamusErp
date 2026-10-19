import { useEffect, useState } from 'react'
import { Modal, Platform, Pressable, StyleSheet, Text, View } from 'react-native'
import { SafeAreaView } from 'react-native-safe-area-context'
import { TODAS, useSessao } from '../auth/Sessao'
import { Botao, Texto } from '../componentes/base'
import { tokenDePush } from '../push'
import { useCores } from '../tema'
import { AvisosTela } from './AvisosTela'
import { ContasTela } from './ContasTela'
import { InicioTela } from './InicioTela'
import { VendasTela } from './VendasTela'

type Aba = 'inicio' | 'vendas' | 'contas' | 'avisos'

const ABAS: { valor: Aba; rotulo: string; perfis?: string[] }[] = [
  { valor: 'inicio', rotulo: 'Início' },
  { valor: 'vendas', rotulo: 'Vendas', perfis: ['Dono', 'Gerente', 'Financeiro', 'Vendedor'] },
  { valor: 'contas', rotulo: 'Contas', perfis: ['Dono', 'Gerente', 'Financeiro'] },
  { valor: 'avisos', rotulo: 'Avisos' },
]

/** Cabeçalho (empresa ativa e sair), conteúdo da aba e barra de abas. Registra o aparelho para push ao entrar. */
export function Principal() {
  const { estado, enviar, getJson, sair, escolherEmpresa } = useSessao()
  const c = useCores()
  const [aba, setAba] = useState<Aba>('inicio')
  const [escolhendo, setEscolhendo] = useState(false)
  const [naoLidas, setNaoLidas] = useState(0)

  useEffect(() => {
    void tokenDePush().then(async (token) => {
      if (token) await enviar('PUT', '/api/notificacoes/dispositivos', { token, plataforma: Platform.OS }).catch(() => null)
    })
  }, [enviar])

  useEffect(() => {
    getJson<{ naoLidas: number }>('/api/notificacoes')
      .then((r) => setNaoLidas(r.naoLidas))
      .catch(() => null)
  }, [getJson, aba])

  if (estado.status !== 'logado') return null
  const perfis = estado.empresaId === TODAS ? ['Dono'] : estado.empresas.filter((e) => e.id === estado.empresaId).map((e) => e.perfil)
  const abas = ABAS.filter((a) => !a.perfis || a.perfis.some((p) => perfis.includes(p)))
  const atual = abas.some((a) => a.valor === aba) ? aba : 'inicio'
  const podeConsolidar = estado.empresas.length > 1 && estado.empresas.every((e) => e.perfil === 'Dono')
  const nomeDaEmpresa = estado.empresaId === TODAS ? 'Todas as empresas' : (estado.empresas.find((e) => e.id === estado.empresaId)?.nomeFantasia ?? '')

  return (
    <SafeAreaView style={[estilos.tela, { backgroundColor: c.fundo }]} edges={['top', 'bottom']}>
      <View style={[estilos.cabecalho, { borderBottomColor: c.borda, backgroundColor: c.superficie }]}>
        <Pressable accessibilityRole="button" accessibilityLabel={`Empresa ativa: ${nomeDaEmpresa}`} onPress={() => setEscolhendo(true)} style={{ flex: 1 }}>
          <Texto suave tamanho={12}>{`Olá, ${estado.usuario.nome.split(' ')[0]}`}</Texto>
          <Texto forte>{`${nomeDaEmpresa} ▾`}</Texto>
        </Pressable>
        <Pressable accessibilityRole="button" accessibilityLabel="Sair" onPress={() => void sair()} style={estilos.sair}>
          <Text style={{ color: c.primaria, fontWeight: '600' }}>Sair</Text>
        </Pressable>
      </View>

      <View style={{ flex: 1 }}>
        {atual === 'inicio' && <InicioTela />}
        {atual === 'vendas' && <VendasTela />}
        {atual === 'contas' && <ContasTela />}
        {atual === 'avisos' && <AvisosTela onLidas={() => setNaoLidas(0)} />}
      </View>

      <View accessibilityRole="tablist" style={[estilos.abas, { borderTopColor: c.borda, backgroundColor: c.superficie }]}>
        {abas.map((a) => {
          const rotulo = a.valor === 'avisos' && naoLidas > 0 ? `Avisos (${naoLidas})` : a.rotulo
          return (
            <Pressable key={a.valor} accessibilityRole="tab" accessibilityLabel={rotulo} accessibilityState={{ selected: atual === a.valor }}
              onPress={() => setAba(a.valor)} style={estilos.aba}>
              <Text style={{ color: atual === a.valor ? c.primaria : c.suave, fontWeight: atual === a.valor ? '700' : '500' }}>{rotulo}</Text>
            </Pressable>
          )
        })}
      </View>

      <Modal visible={escolhendo} transparent animationType="fade" onRequestClose={() => setEscolhendo(false)}>
        <Pressable accessibilityLabel="Fechar" style={estilos.fundoDoModal} onPress={() => setEscolhendo(false)}>
          <View style={[estilos.modal, { backgroundColor: c.superficie, borderColor: c.borda }]}>
            <Texto forte tamanho={16}>Empresa</Texto>
            {podeConsolidar && <Opcao rotulo="Todas as empresas" onPress={() => (escolherEmpresa(TODAS), setEscolhendo(false))} />}
            {estado.empresas.map((e) => (
              <Opcao key={e.id} rotulo={e.nomeFantasia} detalhe={`${e.cnpj} · ${e.perfil}`} onPress={() => (escolherEmpresa(e.id), setEscolhendo(false))} />
            ))}
            <Botao rotulo="Cancelar" onPress={() => setEscolhendo(false)} />
          </View>
        </Pressable>
      </Modal>
    </SafeAreaView>
  )
}

function Opcao({ rotulo, detalhe, onPress }: { rotulo: string; detalhe?: string; onPress: () => void }) {
  const c = useCores()
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={rotulo} onPress={onPress} style={[estilos.opcao, { borderColor: c.borda }]}>
      <Texto forte>{rotulo}</Texto>
      {detalhe && <Texto suave tamanho={12}>{detalhe}</Texto>}
    </Pressable>
  )
}

const estilos = StyleSheet.create({
  tela: { flex: 1 },
  cabecalho: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, paddingVertical: 10, borderBottomWidth: StyleSheet.hairlineWidth },
  sair: { padding: 8 },
  abas: { flexDirection: 'row', borderTopWidth: StyleSheet.hairlineWidth },
  aba: { flex: 1, alignItems: 'center', paddingVertical: 14 },
  fundoDoModal: { flex: 1, backgroundColor: 'rgba(0,0,0,0.4)', justifyContent: 'center', padding: 24 },
  modal: { borderRadius: 16, borderWidth: 1, padding: 16, gap: 8 },
  opcao: { borderWidth: 1, borderRadius: 10, padding: 12 },
})
