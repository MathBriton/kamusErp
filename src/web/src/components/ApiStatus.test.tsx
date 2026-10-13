import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ApiStatus } from './ApiStatus'

describe('ApiStatus', () => {
  it('mostra "API online" quando /health responde 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('Healthy', { status: 200 }))
    render(<ApiStatus />)
    expect(await screen.findByText('API online')).toBeInTheDocument()
    expect(fetch).toHaveBeenCalledWith('/health', expect.anything())
  })

  it('mostra "API indisponível" quando /health falha', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('Unhealthy', { status: 503 }))
    render(<ApiStatus />)
    expect(await screen.findByText('API indisponível')).toBeInTheDocument()
  })

  it('mostra "API indisponível" quando não há conexão', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'))
    render(<ApiStatus />)
    expect(await screen.findByText('API indisponível')).toBeInTheDocument()
  })
})
