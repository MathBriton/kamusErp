export function Logo({ invertido = false }: { invertido?: boolean }) {
  return (
    <span className="flex items-center gap-2.5 text-lg font-bold tracking-tight">
      <span
        className={`grid size-9 place-items-center rounded-lg font-extrabold ${
          invertido ? 'bg-white text-[#0d5c3a]' : 'bg-primary text-primary-foreground'
        }`}
      >
        K
      </span>
      kamus
    </span>
  )
}
