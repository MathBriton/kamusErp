import { vi } from 'vitest'

type Handler = (...args: unknown[]) => void

/** SignalR falso: os testes disparam eventos do servidor com `emitirDoServidor`. */
const handlers = new Map<string, Handler[]>()

export function emitirDoServidor(evento: string, ...args: unknown[]) {
  handlers.get(evento)?.forEach((h) => h(...args))
}

export function limparSignalr() {
  handlers.clear()
}

class ConexaoFalsa {
  on(evento: string, handler: Handler) {
    handlers.set(evento, [...(handlers.get(evento) ?? []), handler])
  }
  off(evento: string) {
    handlers.delete(evento)
  }
  start = vi.fn(() => Promise.resolve())
  stop = vi.fn(() => Promise.resolve())
  onreconnected() {}
}

export class HubConnectionBuilder {
  withUrl() {
    return this
  }
  withAutomaticReconnect() {
    return this
  }
  configureLogging() {
    return this
  }
  build() {
    return new ConexaoFalsa()
  }
}

export const LogLevel = { None: 6, Warning: 3 }
