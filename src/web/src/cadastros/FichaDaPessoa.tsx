import { useState, type FormEvent, type ReactNode } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { useEmpresa } from '../empresa/EmpresaContext'
import { formatarTelefone, type Papel, type Pessoa } from './tipos'

const vazio = (papel: Papel) => ({
  tipo: (papel === 'cliente' ? 'Fisica' : 'Juridica') as Pessoa['tipo'],
  nome: '', nomeFantasia: '', documento: '', ehCliente: papel === 'cliente', ehFornecedor: papel === 'fornecedor',
  telefone: '', email: '', dataNascimento: '', observacoes: '', aceitaMarketing: false,
  endereco: { cep: '', logradouro: '', numero: '', complemento: '', bairro: '', cidade: '', uf: '' },
  preferencias: { blusa: '', calca: '', calcado: '', estilo: '' },
})

type Form = ReturnType<typeof vazio>

const doCadastro = (p: Pessoa): Form => ({
  tipo: p.tipo, nome: p.nome, nomeFantasia: p.nomeFantasia ?? '', documento: p.documento ?? '', ehCliente: p.ehCliente, ehFornecedor: p.ehFornecedor,
  telefone: p.telefone ? formatarTelefone(p.telefone) : '', email: p.email ?? '', dataNascimento: p.dataNascimento ?? '', observacoes: p.observacoes ?? '', aceitaMarketing: p.aceitaMarketing,
  endereco: Object.fromEntries(Object.entries(p.endereco).map(([k, v]) => [k, v ?? ''])) as Form['endereco'],
  preferencias: Object.fromEntries(Object.entries(p.preferencias).map(([k, v]) => [k, v ?? ''])) as Form['preferencias'],
})

function Secao({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <fieldset className="space-y-3 rounded-xl border border-border p-4">
      <legend className="px-1 text-sm font-semibold">{titulo}</legend>
      {children}
    </fieldset>
  )
}

/** Ficha de cliente/fornecedor (nova ou existente), com as ações de inativar e anonimizar conforme o perfil. */
export function FichaDaPessoa({ papel, pessoa, onFechar, onSalvo }: {
  papel: Papel
  pessoa: Pessoa | null
  onFechar: () => void
  onSalvo: () => void
}) {
  const { enviar } = useApi()
  const { perfilAtual } = useEmpresa()
  const [form, setForm] = useState<Form>(() => (pessoa ? doCadastro(pessoa) : vazio(papel)))
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [confirmandoLgpd, setConfirmandoLgpd] = useState(false)
  const gestao = perfilAtual === 'Dono' || perfilAtual === 'Gerente'
  const fisica = form.tipo === 'Fisica'
  const anonimizado = !!pessoa?.anonimizadoEm

  const muda = (campo: keyof Form) => (e: { target: { value: string } }) => setForm({ ...form, [campo]: e.target.value })
  const mudaEndereco = (campo: keyof Form['endereco']) => (e: { target: { value: string } }) =>
    setForm({ ...form, endereco: { ...form.endereco, [campo]: e.target.value } })
  const mudaPreferencia = (campo: keyof Form['preferencias']) => (e: { target: { value: string } }) =>
    setForm({ ...form, preferencias: { ...form.preferencias, [campo]: e.target.value } })

  async function executar(acao: () => Promise<unknown>) {
    setEnviando(true)
    setErro(null)
    try {
      await acao()
      onSalvo()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível salvar.')
      setEnviando(false)
    }
  }

  const salvar = (e: FormEvent) => {
    e.preventDefault()
    const corpo = { ...form, dataNascimento: form.dataNascimento || null, nomeFantasia: fisica ? null : form.nomeFantasia }
    void executar(() => (pessoa ? enviar('PUT', `/api/pessoas/${pessoa.id}`, corpo) : enviar('POST', '/api/pessoas', corpo)))
  }

  const titulo = pessoa ? pessoa.nome : papel === 'cliente' ? 'Novo cliente' : 'Novo fornecedor'
  const campoPequeno = 'h-10 w-full rounded-lg border border-border bg-surface px-3 text-sm'

  return (
    <Dialogo titulo={titulo} onFechar={onFechar} largo>
      <form onSubmit={salvar} className="space-y-4">
        <fieldset disabled={anonimizado} className="space-y-4">
          <Secao titulo="Dados">
            <Campo rotulo="Tipo">
              <select value={form.tipo} onChange={muda('tipo')} className={estilos.campo}>
                <option value="Fisica">Pessoa física</option>
                <option value="Juridica">Pessoa jurídica</option>
              </select>
            </Campo>
            <Campo rotulo={fisica ? 'Nome' : 'Razão social'}>
              <input value={form.nome} onChange={muda('nome')} className={estilos.campo} />
            </Campo>
            {!fisica && (
              <Campo rotulo="Nome fantasia">
                <input value={form.nomeFantasia} onChange={muda('nomeFantasia')} className={estilos.campo} />
              </Campo>
            )}
            <div className="grid gap-3 sm:grid-cols-2">
              <Campo rotulo={fisica ? 'CPF' : 'CNPJ'}>
                <input value={form.documento} onChange={muda('documento')} placeholder="Opcional" className={estilos.campo} />
              </Campo>
              {papel === 'cliente' && fisica && (
                <Campo rotulo="Data de nascimento">
                  <input type="date" value={form.dataNascimento} onChange={muda('dataNascimento')} className={estilos.campo} />
                </Campo>
              )}
            </div>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={papel === 'cliente' ? form.ehFornecedor : form.ehCliente}
                onChange={(e) => setForm(papel === 'cliente' ? { ...form, ehFornecedor: e.target.checked } : { ...form, ehCliente: e.target.checked })}
                className="size-4 accent-primary"
              />
              {papel === 'cliente' ? 'Também é fornecedor' : 'Também é cliente'}
            </label>
          </Secao>

          <Secao titulo="Contato">
            <div className="grid gap-3 sm:grid-cols-2">
              <Campo rotulo="Telefone / WhatsApp">
                <input value={form.telefone} onChange={muda('telefone')} placeholder="(11) 98765-4321" inputMode="tel" className={estilos.campo} />
              </Campo>
              <Campo rotulo="E-mail">
                <input type="email" value={form.email} onChange={muda('email')} className={estilos.campo} />
              </Campo>
            </div>
          </Secao>

          <Secao titulo="Endereço">
            <div className="grid grid-cols-6 gap-3">
              <Campo rotulo="CEP" className="col-span-2">
                <input value={form.endereco.cep} onChange={mudaEndereco('cep')} className={campoPequeno} />
              </Campo>
              <Campo rotulo="Logradouro" className="col-span-4">
                <input value={form.endereco.logradouro} onChange={mudaEndereco('logradouro')} className={campoPequeno} />
              </Campo>
              <Campo rotulo="Número" className="col-span-2">
                <input value={form.endereco.numero} onChange={mudaEndereco('numero')} className={campoPequeno} />
              </Campo>
              <Campo rotulo="Complemento" className="col-span-4">
                <input value={form.endereco.complemento} onChange={mudaEndereco('complemento')} className={campoPequeno} />
              </Campo>
              <Campo rotulo="Bairro" className="col-span-2">
                <input value={form.endereco.bairro} onChange={mudaEndereco('bairro')} className={campoPequeno} />
              </Campo>
              <Campo rotulo="Cidade" className="col-span-3">
                <input value={form.endereco.cidade} onChange={mudaEndereco('cidade')} className={campoPequeno} />
              </Campo>
              <Campo rotulo="UF" className="col-span-1">
                <input value={form.endereco.uf} onChange={mudaEndereco('uf')} maxLength={2} className={campoPequeno} />
              </Campo>
            </div>
          </Secao>

          {papel === 'cliente' && (
            <Secao titulo="Preferências de moda">
              <div className="grid grid-cols-3 gap-3">
                <Campo rotulo="Blusa">
                  <input value={form.preferencias.blusa} onChange={mudaPreferencia('blusa')} placeholder="M" className={campoPequeno} />
                </Campo>
                <Campo rotulo="Calça">
                  <input value={form.preferencias.calca} onChange={mudaPreferencia('calca')} placeholder="40" className={campoPequeno} />
                </Campo>
                <Campo rotulo="Calçado">
                  <input value={form.preferencias.calcado} onChange={mudaPreferencia('calcado')} placeholder="37" className={campoPequeno} />
                </Campo>
              </div>
              <Campo rotulo="Estilo e cores preferidas">
                <input value={form.preferencias.estilo} onChange={mudaPreferencia('estilo')} className={estilos.campo} />
              </Campo>
            </Secao>
          )}

          <Campo rotulo="Observações">
            <textarea value={form.observacoes} onChange={muda('observacoes')} rows={2} className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm" />
          </Campo>

          {form.ehCliente && (
            <label className="flex items-start gap-2 text-sm">
              <input
                type="checkbox"
                checked={form.aceitaMarketing}
                onChange={(e) => setForm({ ...form, aceitaMarketing: e.target.checked })}
                className="mt-0.5 size-4 accent-primary"
              />
              <span>
                Aceita receber novidades e mensagens da loja (LGPD)
                {pessoa?.consentimentoMarketingEm && form.aceitaMarketing && (
                  <span className="block text-xs text-muted">Consentimento registrado em {new Date(pessoa.consentimentoMarketingEm).toLocaleDateString('pt-BR')}</span>
                )}
              </span>
            </label>
          )}
        </fieldset>

        {anonimizado && <p className="rounded-lg bg-surface-2 px-3 py-2.5 text-sm text-muted">Cadastro anonimizado (LGPD): os dados pessoais foram apagados.</p>}
        {erro && (
          <p role="alert" className={estilos.erro}>
            {erro}
          </p>
        )}

        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex flex-wrap gap-2">
            {pessoa && gestao && !anonimizado && (
              <button
                type="button"
                onClick={() => void executar(() => enviar('POST', `/api/pessoas/${pessoa.id}/${pessoa.ativo ? 'inativar' : 'reativar'}`))}
                className={estilos.botao}
              >
                {pessoa.ativo ? 'Inativar' : 'Reativar'}
              </button>
            )}
            {pessoa && perfilAtual === 'Dono' && !anonimizado && pessoa.ehCliente &&
              (confirmandoLgpd ? (
                <button type="button" onClick={() => void executar(() => enviar('POST', `/api/pessoas/${pessoa.id}/anonimizar`))} className={`${estilos.botao} text-danger`}>
                  Confirmar anonimização
                </button>
              ) : (
                <button type="button" onClick={() => setConfirmandoLgpd(true)} className={`${estilos.botao} text-danger`}>
                  Anonimizar (LGPD)
                </button>
              ))}
          </div>
          <div className="flex gap-2">
            <button type="button" onClick={onFechar} className={estilos.botao}>
              Cancelar
            </button>
            {!anonimizado && (
              <button type="submit" disabled={enviando} className={estilos.botaoPrimario}>
                {enviando ? 'Salvando...' : 'Salvar'}
              </button>
            )}
          </div>
        </div>
        {confirmandoLgpd && (
          <p className="text-xs text-danger">Apaga nome, documento, contatos, endereço e preferências. O histórico de vendas continua, sem identificar a pessoa. Não dá para desfazer.</p>
        )}
      </form>
    </Dialogo>
  )
}
