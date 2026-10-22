/**
 * Número de WhatsApp do atendimento Kamus (só dígitos, com DDI e DDD), vindo de VITE_SUPORTE_WHATSAPP.
 * Sem número configurado, a tela não mostra o atendimento (nada de prometer ajuda que não existe).
 */
export function linkDoWhatsApp(mensagem: string): string | null {
  const numero = String(import.meta.env.VITE_SUPORTE_WHATSAPP ?? '').replace(/\D/g, '')
  return numero ? `https://wa.me/${numero}?text=${encodeURIComponent(mensagem)}` : null
}
