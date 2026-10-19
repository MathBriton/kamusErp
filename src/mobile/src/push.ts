import Constants from 'expo-constants'
import * as Device from 'expo-device'
import * as Notifications from 'expo-notifications'
import { AndroidImportance } from 'expo-notifications'
import { Platform } from 'react-native'

// Com o app aberto, a notificação também aparece no topo da tela.
Notifications.setNotificationHandler({
  handleNotification: async () => ({ shouldShowBanner: true, shouldShowList: true, shouldPlaySound: true, shouldSetBadge: false }),
})

/**
 * Pede permissão e devolve o token do Expo Push deste aparelho, ou null quando não dá:
 * navegador, emulador, permissão negada ou app sem projectId do EAS (extra.eas.projectId no app.json).
 */
export async function tokenDePush(): Promise<string | null> {
  if (Platform.OS === 'web' || !Device.isDevice) return null
  try {
    if (Platform.OS === 'android')
      await Notifications.setNotificationChannelAsync('default', { name: 'Avisos', importance: AndroidImportance.DEFAULT })
    const atual = await Notifications.getPermissionsAsync()
    const permissao = atual.status === 'granted' ? atual : await Notifications.requestPermissionsAsync()
    if (permissao.status !== 'granted') return null
    const projectId = Constants.expoConfig?.extra?.eas?.projectId ?? Constants.easConfig?.projectId
    if (!projectId) return null
    return (await Notifications.getExpoPushTokenAsync({ projectId })).data
  } catch {
    return null
  }
}
