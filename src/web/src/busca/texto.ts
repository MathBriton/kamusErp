/** Mesma regra do backend (TextoParaBusca): sem acento, minúsculas, espaços simples. */
export function normalizar(texto: string): string {
  return texto.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase().replace(/\s+/g, ' ').trim()
}

export const contem = (texto: string | undefined, termo: string) => normalizar(texto ?? '').includes(normalizar(termo))
