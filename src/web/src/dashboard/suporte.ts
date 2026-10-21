/**
 * Número de WhatsApp do atendimento Kamus (só dígitos, com DDI e DDD), vindo de VITE_SUPORTE_WHATSAPP.
 * Sem número configurado, os botões de atendimento explicam que o contato ainda não foi definido.
 */
const numero = (import.meta.env.VITE_SUPORTE_WHATSAPP ?? '').replace(/\D/g, '')

export function linkDoWhatsApp(mensagem: string): string | null {
  return numero ? `https://wa.me/${numero}?text=${encodeURIComponent(mensagem)}` : null
}
