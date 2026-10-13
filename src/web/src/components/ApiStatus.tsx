import { useEffect, useState } from 'react'

type Status = 'checking' | 'online' | 'offline'

const LABELS: Record<Status, string> = {
  checking: 'Verificando API...',
  online: 'API online',
  offline: 'API indisponível',
}

const DOT: Record<Status, string> = {
  checking: 'bg-muted',
  online: 'bg-success',
  offline: 'bg-danger',
}

export function ApiStatus() {
  const [status, setStatus] = useState<Status>('checking')

  useEffect(() => {
    const controller = new AbortController()
    fetch('/health', { signal: controller.signal })
      .then((res) => setStatus(res.ok ? 'online' : 'offline'))
      .catch((err: unknown) => {
        if (!controller.signal.aborted) setStatus('offline')
        return err
      })
    return () => controller.abort()
  }, [])

  return (
    <span className="inline-flex items-center gap-2 text-sm text-muted">
      <span className={`size-2 rounded-full ${DOT[status]}`} aria-hidden />
      {LABELS[status]}
    </span>
  )
}
