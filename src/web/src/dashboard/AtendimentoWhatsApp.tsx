import { useEffect, useState } from 'react'
import { Icone } from '../components/Icone'
import { linkDoWhatsApp } from './suporte'

const BALAO_VISIVEL_MS = 8_000

/** Botão flutuante de atendimento (canto inferior direito) com o balão "Dúvidas?". */
export function AtendimentoWhatsApp() {
  const [balao, setBalao] = useState(true)
  const link = linkDoWhatsApp('Olá! Preciso de ajuda com o Kamus.')

  // O balão aparece ao abrir a tela e some sozinho para não cobrir os cards; volta ao passar o mouse no botão.
  useEffect(() => {
    const timer = window.setTimeout(() => setBalao(false), BALAO_VISIVEL_MS)
    return () => window.clearTimeout(timer)
  }, [])

  return (
    <aside
      aria-label="Atendimento pelo WhatsApp"
      onMouseEnter={() => setBalao(true)}
      onMouseLeave={() => setBalao(false)}
      className="fixed right-6 bottom-6 z-50 flex items-end gap-3"
    >
      {balao && (
        <div className="relative max-w-56 rounded-2xl rounded-br-sm border border-border bg-surface px-4 py-3 text-sm shadow-lg">
          <button type="button" aria-label="Fechar aviso de atendimento" onClick={() => setBalao(false)} className="absolute top-1.5 right-1.5 text-muted hover:text-foreground">
            <Icone nome="x" className="size-3.5" />
          </button>
          <p className="pr-3 font-semibold">Dúvidas? Fale com a assistente virtual</p>
          {!link && <p className="mt-1 text-xs text-muted">O número de atendimento ainda não foi configurado.</p>}
        </div>
      )}
      {link ? (
        <a href={link} target="_blank" rel="noreferrer" aria-label="Abrir conversa no WhatsApp" className="relative grid size-14 place-items-center rounded-full bg-whatsapp text-whatsapp-foreground shadow-lg transition-transform hover:scale-105">
          <Icone nome="mensagem" className="size-7" />
          <span aria-hidden className="absolute top-0.5 right-0.5 size-3.5 rounded-full border-2 border-surface bg-success" />
        </a>
      ) : (
        <button type="button" aria-label="Atendimento pelo WhatsApp" onClick={() => setBalao((v) => !v)} className="grid size-14 place-items-center rounded-full bg-whatsapp text-whatsapp-foreground shadow-lg">
          <Icone nome="mensagem" className="size-7" />
        </button>
      )}
    </aside>
  )
}
