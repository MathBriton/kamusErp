/**
 * Endereço da API. No navegador (expo start --web) o padrão localhost funciona; no celular (Expo Go),
 * use o IP do computador na rede: EXPO_PUBLIC_API_URL=http://192.168.0.10:5249 npx expo start
 */
export const API_URL = (process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5249').replace(/\/$/, '')
