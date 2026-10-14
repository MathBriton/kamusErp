import { useState } from 'react'
import { botao } from './formatos'

const CHAVE = 'kamus.admin.tema'

/** Alterna claro/escuro (a escolha inicial segue o sistema; ver index.html). */
export function AlternarTema() {
  const [escuro, setEscuro] = useState(() => document.documentElement.classList.contains('dark'))

  const alternar = () => {
    const novo = !escuro
    document.documentElement.classList.toggle('dark', novo)
    document.documentElement.style.colorScheme = novo ? 'dark' : 'light'
    try {
      localStorage.setItem(CHAVE, novo ? 'dark' : 'light')
    } catch {
      // sem armazenamento
    }
    setEscuro(novo)
  }

  return (
    <button type="button" onClick={alternar} className={botao}>
      {escuro ? 'Tema claro' : 'Tema escuro'}
    </button>
  )
}
