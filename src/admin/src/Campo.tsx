import type { ReactNode } from 'react'

export function Campo({ rotulo, children, className = '' }: { rotulo: string; children: ReactNode; className?: string }) {
  return (
    <label className={`block text-sm ${className}`}>
      <span className="mb-1 block font-semibold">{rotulo}</span>
      {children}
    </label>
  )
}
