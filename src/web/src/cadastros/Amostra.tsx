/** Bolinha com a cor (a única cor "fixa" permitida na tela: é o dado em si). */
export function Amostra({ hex }: { hex: string | null }) {
  return <span aria-hidden className="inline-block size-3.5 shrink-0 rounded-full border border-border" style={{ backgroundColor: hex ?? 'transparent' }} />
}
