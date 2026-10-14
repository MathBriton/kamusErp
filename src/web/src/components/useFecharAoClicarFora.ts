import { useEffect, type RefObject } from 'react'

/** Fecha menus suspensos ao clicar fora ou apertar Esc. */
export function useFecharAoClicarFora(ref: RefObject<HTMLElement | null>, aberto: boolean, fechar: () => void) {
  useEffect(() => {
    if (!aberto) return
    const onClique = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) fechar()
    }
    const onTecla = (e: KeyboardEvent) => e.key === 'Escape' && fechar()
    document.addEventListener('mousedown', onClique)
    document.addEventListener('keydown', onTecla)
    return () => {
      document.removeEventListener('mousedown', onClique)
      document.removeEventListener('keydown', onTecla)
    }
  }, [ref, aberto, fechar])
}
