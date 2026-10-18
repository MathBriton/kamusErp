import { afterEach, describe, expect, it, vi } from 'vitest'
import { hojeIso } from './tipos'

describe('hojeIso', () => {
  afterEach(() => vi.useRealTimers())

  it('usa a data de Brasília (como a API), não a do fuso do navegador', () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-10-18T01:30:00Z')) // 22:30 de 17/10 em Brasília
    expect(hojeIso()).toBe('2026-10-17')
    vi.setSystemTime(new Date('2026-10-18T03:00:00Z'))
    expect(hojeIso()).toBe('2026-10-18')
  })
})
