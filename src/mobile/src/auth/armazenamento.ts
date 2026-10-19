import * as SecureStore from 'expo-secure-store'
import { Platform } from 'react-native'

const CHAVE = 'kamus_sessao'

/** Token de sessão (renovação): no aparelho, no armazenamento seguro (Keychain/Keystore); no navegador de desenvolvimento, no localStorage. */
export const armazenamento = {
  async ler(): Promise<string | null> {
    if (Platform.OS === 'web') return globalThis.localStorage?.getItem(CHAVE) ?? null
    return SecureStore.getItemAsync(CHAVE)
  },
  async gravar(token: string) {
    if (Platform.OS === 'web') globalThis.localStorage?.setItem(CHAVE, token)
    else await SecureStore.setItemAsync(CHAVE, token)
  },
  async apagar() {
    if (Platform.OS === 'web') globalThis.localStorage?.removeItem(CHAVE)
    else await SecureStore.deleteItemAsync(CHAVE)
  },
}
