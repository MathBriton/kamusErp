import { useColorScheme } from 'react-native'

/** Os mesmos tokens de cor da web (src/web/src/index.css), em claro e escuro: nenhuma tela usa cor fixa. */
const claro = {
  fundo: '#f6f6f8',
  superficie: '#ffffff',
  superficie2: '#f2f2f5',
  borda: '#e5e5ea',
  texto: '#16161d',
  suave: '#6b6b78',
  primaria: '#4f46e5',
  textoNaPrimaria: '#ffffff',
  primariaSuave: '#eeeffe',
  sucesso: '#15803d',
  aviso: '#b45309',
  avisoSuave: '#fef3dd',
  perigo: '#dc2626',
  perigoSuave: '#fdecec',
}

const escuro: typeof claro = {
  fundo: '#0c0c11',
  superficie: '#15151c',
  superficie2: '#1d1d26',
  borda: '#2a2a36',
  texto: '#f2f2f6',
  suave: '#9a9aa8',
  primaria: '#8b85ff',
  textoNaPrimaria: '#0c0c11',
  primariaSuave: '#23213f',
  sucesso: '#4ade80',
  aviso: '#fbbf24',
  avisoSuave: '#33270c',
  perigo: '#f87171',
  perigoSuave: '#351616',
}

export type Cores = typeof claro

export const useCores = (): Cores => (useColorScheme() === 'dark' ? escuro : claro)
