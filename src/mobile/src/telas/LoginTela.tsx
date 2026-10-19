import { useState } from 'react'
import { KeyboardAvoidingView, Platform, StyleSheet, Text, TextInput, View } from 'react-native'
import { useSessao } from '../auth/Sessao'
import { Botao, Erro } from '../componentes/base'
import { useCores } from '../tema'

export function LoginTela() {
  const { entrar } = useSessao()
  const c = useCores()
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar() {
    setErro(null)
    setEnviando(true)
    const falha = await entrar(email, senha)
    setEnviando(false)
    if (falha) setErro(falha)
  }

  const campo = [estilos.campo, { borderColor: c.borda, backgroundColor: c.superficie, color: c.texto }]
  return (
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={[estilos.tela, { backgroundColor: c.fundo }]}>
      <View style={estilos.caixa}>
        <Text style={[estilos.marca, { color: c.primaria }]}>kamus</Text>
        <Text style={{ color: c.suave, marginBottom: 16 }}>Acompanhe sua loja pelo celular.</Text>
        <TextInput accessibilityLabel="E-mail" value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" autoComplete="email"
          placeholder="E-mail" placeholderTextColor={c.suave} style={campo} />
        <TextInput accessibilityLabel="Senha" value={senha} onChangeText={setSenha} secureTextEntry autoComplete="password" placeholder="Senha"
          placeholderTextColor={c.suave} style={campo} onSubmitEditing={() => void enviar()} />
        {erro && <Erro mensagem={erro} />}
        <Botao rotulo={enviando ? 'Entrando...' : 'Entrar'} primario desabilitado={enviando || !email || !senha} onPress={() => void enviar()} />
      </View>
    </KeyboardAvoidingView>
  )
}

const estilos = StyleSheet.create({
  tela: { flex: 1, justifyContent: 'center', padding: 24 },
  caixa: { gap: 12, maxWidth: 420, width: '100%', alignSelf: 'center' },
  marca: { fontSize: 36, fontWeight: '800' },
  campo: { height: 48, borderWidth: 1, borderRadius: 10, paddingHorizontal: 14, fontSize: 16 },
})
