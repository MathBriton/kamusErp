/** Dispara o download de um arquivo recebido da API (o navegador pergunta onde salvar). */
export function salvarArquivo(blob: Blob, nome: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = nome
  link.click()
  URL.revokeObjectURL(url)
}
