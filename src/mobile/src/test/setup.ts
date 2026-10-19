import { configure } from '@testing-library/react-native'

// Primeira execução (fria) no CI pode passar de 1 s para renderizar.
configure({ asyncUtilTimeout: 5000 })

// Substitutos dos módulos nativos do Expo nos testes (Jest roda fora do aparelho).
const mockGuardado = new Map<string, string>()

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(async (chave: string) => mockGuardado.get(chave) ?? null),
  setItemAsync: jest.fn(async (chave: string, valor: string) => {
    mockGuardado.set(chave, valor)
  }),
  deleteItemAsync: jest.fn(async (chave: string) => {
    mockGuardado.delete(chave)
  }),
}))

jest.mock('expo-notifications', () => ({
  setNotificationHandler: jest.fn(),
  getPermissionsAsync: jest.fn(async () => ({ status: 'granted' })),
  requestPermissionsAsync: jest.fn(async () => ({ status: 'granted' })),
  getExpoPushTokenAsync: jest.fn(async () => ({ data: 'ExponentPushToken[teste]' })),
  setNotificationChannelAsync: jest.fn(async () => null),
  AndroidImportance: { DEFAULT: 3 },
}))

jest.mock('expo-device', () => ({ isDevice: true }))

jest.mock('expo-constants', () => ({ __esModule: true, default: { expoConfig: { extra: { eas: { projectId: 'projeto-teste' } } } } }))

beforeEach(() => mockGuardado.clear())

// O SafeAreaProvider nativo só desenha depois de medir a tela; o mock da biblioteca mede na hora.
jest.mock('react-native-safe-area-context', () => jest.requireActual('react-native-safe-area-context/jest/mock').default)
