import type { ReactNode } from 'react'
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native'
import { useCores } from '../tema'

/** Bloco com borda e fundo de superfície. */
export function Bloco({ titulo, children, rotulo }: { titulo?: string; children: ReactNode; rotulo?: string }) {
  const c = useCores()
  return (
    <View accessibilityLabel={rotulo ?? titulo} style={[estilos.bloco, { backgroundColor: c.superficie, borderColor: c.borda }]}>
      {titulo && <Text style={[estilos.tituloDoBloco, { color: c.texto }]}>{titulo}</Text>}
      {children}
    </View>
  )
}

export function Texto({ children, suave, forte, tamanho = 14, cor }: { children: ReactNode; suave?: boolean; forte?: boolean; tamanho?: number; cor?: string }) {
  const c = useCores()
  return <Text style={{ color: cor ?? (suave ? c.suave : c.texto), fontWeight: forte ? '700' : '400', fontSize: tamanho }}>{children}</Text>
}

export function Botao({ rotulo, onPress, primario, desabilitado }: { rotulo: string; onPress: () => void; primario?: boolean; desabilitado?: boolean }) {
  const c = useCores()
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: !!desabilitado }}
      disabled={desabilitado}
      onPress={onPress}
      style={[estilos.botao, primario ? { backgroundColor: c.primaria } : { borderColor: c.borda, borderWidth: 1, backgroundColor: c.superficie }, desabilitado && { opacity: 0.6 }]}
    >
      <Text style={{ color: primario ? c.textoNaPrimaria : c.texto, fontWeight: '600' }}>{rotulo}</Text>
    </Pressable>
  )
}

export function Carregando() {
  const c = useCores()
  return <ActivityIndicator accessibilityLabel="Carregando" color={c.primaria} style={{ marginTop: 32 }} />
}

export function Erro({ mensagem }: { mensagem: string }) {
  const c = useCores()
  return (
    <Text accessibilityRole="alert" style={[estilos.erro, { backgroundColor: c.perigoSuave, color: c.perigo }]}>
      {mensagem}
    </Text>
  )
}

const estilos = StyleSheet.create({
  bloco: { borderWidth: 1, borderRadius: 16, padding: 16, gap: 6 },
  tituloDoBloco: { fontSize: 15, fontWeight: '700', marginBottom: 4 },
  botao: { height: 44, borderRadius: 10, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 16 },
  erro: { borderRadius: 10, padding: 12, fontSize: 14 },
})
