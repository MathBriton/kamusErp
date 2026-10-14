import { Link } from 'react-router'
import { Icone } from '../components/Icone'
import type { ItemMenu } from '../navegacao/menu'

/** Página de um item do menu que ainda não foi implementado. */
export function EmConstrucaoPage({ item }: { item: ItemMenu }) {
  return (
    <div className="mx-auto max-w-2xl py-10 text-center">
      <span className="mx-auto grid size-14 place-items-center rounded-2xl bg-primary-soft text-primary">
        <Icone nome="construction" className="size-6" />
      </span>
      <h1 className="mt-5 text-2xl font-bold tracking-tight">{item.titulo}</h1>
      {item.descricao && <p className="mt-2 text-muted">{item.descricao}</p>}
      {item.previstoPara && (
        <p className="mt-4 inline-flex items-center gap-1.5 rounded-full bg-info-soft px-3 py-1 text-sm font-semibold text-info">
          <Icone nome="clock" className="size-3.5" />
          Previsto para a {item.previstoPara}
        </p>
      )}
      <p className="mt-8">
        <Link to="/" className="text-sm font-semibold text-primary hover:underline">
          ← Voltar ao dashboard
        </Link>
      </p>
    </div>
  )
}
