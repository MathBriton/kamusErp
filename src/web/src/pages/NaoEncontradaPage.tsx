import { Link } from 'react-router'

export function NaoEncontradaPage() {
  return (
    <div className="mx-auto max-w-xl py-16 text-center">
      <p className="text-sm font-semibold text-primary">404</p>
      <h1 className="mt-2 text-2xl font-bold tracking-tight">Página não encontrada</h1>
      <p className="mt-2 text-muted">O endereço não existe ou você não tem acesso a esta tela.</p>
      <Link to="/" className="mt-6 inline-block text-sm font-semibold text-primary hover:underline">
        ← Voltar ao dashboard
      </Link>
    </div>
  )
}
