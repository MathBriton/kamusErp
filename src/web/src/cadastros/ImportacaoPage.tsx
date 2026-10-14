import { useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { estilos } from '../components/estilos'
import { Icone } from '../components/Icone'
import { useEmpresa } from '../empresa/EmpresaContext'
import { salvarArquivo } from '../relatorios/salvarArquivo'

interface LinhaValidada {
  numero: number
  acao: 'Criar' | 'Atualizar' | 'Erro'
  descricao: string
  erros: string[]
}

interface Validacao {
  id: string
  tipo: string
  nomeDoArquivo: string
  total: number
  validas: number
  comErro: number
  criar: number
  atualizar: number
  linhas: LinhaValidada[]
}

const TIPOS = [
  { tipo: 'clientes', rotulo: 'Clientes', ajuda: 'Casa pelo CPF/CNPJ, telefone ou nome: reimportar atualiza, não duplica.', perfis: ['Dono', 'Gerente'] },
  { tipo: 'fornecedores', rotulo: 'Fornecedores', ajuda: 'Pessoa jurídica identificada pelo CNPJ.', perfis: ['Dono', 'Gerente'] },
  { tipo: 'produtos', rotulo: 'Produtos', ajuda: 'Uma linha por SKU (referência + cor + tamanho). A grade precisa estar cadastrada.', perfis: ['Dono', 'Gerente', 'Estoquista'] },
]

const ESTILO_ACAO: Record<LinhaValidada['acao'], { rotulo: string; classe: string }> = {
  Criar: { rotulo: 'Novo', classe: 'bg-success-soft text-success' },
  Atualizar: { rotulo: 'Atualiza', classe: 'bg-info-soft text-info' },
  Erro: { rotulo: 'Erro', classe: 'bg-danger-soft text-danger' },
}

const plural = (n: number, singular: string, pluralTexto: string) => `${n} ${n === 1 ? singular : pluralTexto}`

/** Importação em 4 passos: tipo e modelo → enviar → prévia (nada é gravado) → importar. */
export function ImportacaoPage() {
  const { baixar, enviarArquivo, postarComStatus } = useApi()
  const { perfilAtual } = useEmpresa()
  const tipos = TIPOS.filter((t) => t.perfis.includes(perfilAtual))
  const [tipo, setTipo] = useState(tipos[0]?.tipo ?? 'clientes')
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [validacao, setValidacao] = useState<Validacao | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [ocupado, setOcupado] = useState(false)

  const recomecar = () => {
    setValidacao(null)
    setAviso(null)
    setErro(null)
  }

  async function executar(acao: () => Promise<void>) {
    setOcupado(true)
    setErro(null)
    try {
      await acao()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível concluir.')
    } finally {
      setOcupado(false)
    }
  }

  const baixarModelo = () =>
    void executar(async () => {
      const r = await baixar(`/api/importacoes/${tipo}/modelo.xlsx`)
      if (r.tipo === 'arquivo') salvarArquivo(r.blob, r.nome)
    })

  const validar = (e: FormEvent) => {
    e.preventDefault()
    if (!arquivo) return
    recomecar()
    void executar(async () => {
      const formulario = new FormData()
      formulario.append('arquivo', arquivo)
      setValidacao((await enviarArquivo<Validacao>(`/api/importacoes/${tipo}/validar`, formulario)).corpo)
    })
  }

  const importar = () =>
    void executar(async () => {
      const { status, corpo } = await postarComStatus<{ criados: number; atualizados: number; ignorados: number; mensagem?: string }>(
        `/api/importacoes/${validacao!.id}/confirmar`,
      )
      setValidacao(null)
      setArquivo(null)
      setAviso(
        status === 202
          ? corpo.mensagem!
          : `Importação concluída: ${plural(corpo.criados, 'criado', 'criados')}, ${plural(corpo.atualizados, 'atualizado', 'atualizados')}, ${plural(corpo.ignorados, 'linha ignorada', 'linhas ignoradas')}.`,
      )
    })

  const linhasOrdenadas = validacao ? [...validacao.linhas].sort((a, b) => Number(b.acao === 'Erro') - Number(a.acao === 'Erro') || a.numero - b.numero) : []

  return (
    <div className="mx-auto max-w-5xl">
      <p className="text-xs text-muted">Cadastros</p>
      <h1 className="text-2xl font-bold tracking-tight">Importar planilha</h1>
      <p className="mt-1 text-sm text-muted">Traga os cadastros que você já tem em Excel. Nada é gravado até você confirmar.</p>

      <form onSubmit={validar} className="mt-6 space-y-5 rounded-2xl border border-border bg-surface p-5 shadow-sm">
        <fieldset>
          <legend className="mb-2 text-sm font-semibold">1. O que você vai importar?</legend>
          <div className="grid gap-2 sm:grid-cols-3">
            {tipos.map((t) => (
              <label key={t.tipo} className={`cursor-pointer rounded-xl border p-3 text-sm ${tipo === t.tipo ? 'border-primary bg-primary-soft' : 'border-border'}`}>
                <span className="flex items-center gap-2 font-semibold">
                  <input type="radio" name="tipo" value={t.tipo} checked={tipo === t.tipo} onChange={() => { setTipo(t.tipo); recomecar() }} className="accent-primary" />
                  {t.rotulo}
                </span>
                <span className="mt-1 block text-xs text-muted">{t.ajuda}</span>
              </label>
            ))}
          </div>
        </fieldset>

        <div>
          <p className="mb-2 text-sm font-semibold">2. Preencha o modelo</p>
          <button type="button" onClick={baixarModelo} disabled={ocupado} className={estilos.botao}>
            <Icone nome="download" />
            Baixar modelo (.xlsx)
          </button>
          <p className="mt-1 text-xs text-muted">O modelo traz as colunas, um exemplo e uma aba de instruções. Também aceitamos .csv separado por ponto e vírgula.</p>
        </div>

        <div>
          <label className="block text-sm">
            <span className="mb-2 block font-semibold">3. Envie para conferir</span>
            <span className="sr-only">Planilha (.xlsx ou .csv)</span>
            <input
              type="file"
              aria-label="Planilha (.xlsx ou .csv)"
              accept=".xlsx,.csv"
              onChange={(e) => { setArquivo(e.target.files?.[0] ?? null); recomecar() }}
              className="block w-full text-sm file:mr-3 file:rounded-lg file:border file:border-border file:bg-surface-2 file:px-3 file:py-2 file:text-sm file:font-semibold"
            />
          </label>
          <button type="submit" disabled={!arquivo || ocupado} className={`${estilos.botaoPrimario} mt-3`}>
            {ocupado && !validacao ? 'Conferindo...' : 'Validar planilha'}
          </button>
        </div>
      </form>

      {erro && (
        <p role="alert" className={`mt-5 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}
      {aviso && (
        <p role="status" className="mt-5 rounded-lg bg-success-soft px-4 py-3 text-sm text-success">
          {aviso}
        </p>
      )}

      {validacao && (
        <section aria-label="Prévia da importação" className="mt-5 rounded-2xl border border-border bg-surface p-5 shadow-sm">
          <h2 className="font-semibold">4. Confira e importe — {validacao.nomeDoArquivo}</h2>
          <div className="mt-3 flex flex-wrap gap-2 text-sm">
            <span className="rounded-full bg-success-soft px-2.5 py-0.5 font-semibold text-success">{plural(validacao.criar, 'novo', 'novos')}</span>
            <span className="rounded-full bg-info-soft px-2.5 py-0.5 font-semibold text-info">{plural(validacao.atualizar, 'atualização', 'atualizações')}</span>
            <span className="rounded-full bg-danger-soft px-2.5 py-0.5 font-semibold text-danger">{`${validacao.comErro} com erro`}</span>
            <span className="text-muted">{`de ${validacao.total} linhas`}</span>
          </div>

          <div className="mt-4 max-h-96 overflow-auto rounded-xl border border-border">
            <table className="w-full text-sm">
              <thead className="sticky top-0 bg-surface-2 text-left text-xs text-muted uppercase">
                <tr>
                  <th className="px-3 py-2">Linha</th>
                  <th className="px-3 py-2">Situação</th>
                  <th className="px-3 py-2">Registro</th>
                  <th className="px-3 py-2">Problemas</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {linhasOrdenadas.map((l) => (
                  <tr key={l.numero}>
                    <td className="px-3 py-2 tabular-nums">{l.numero}</td>
                    <td className="px-3 py-2">
                      <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${ESTILO_ACAO[l.acao].classe}`}>{ESTILO_ACAO[l.acao].rotulo}</span>
                    </td>
                    <td className="px-3 py-2">{l.descricao}</td>
                    <td className="px-3 py-2 text-danger">
                      {l.erros.map((e) => (
                        <span key={e} className="block">{e}</span>
                      ))}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-4 flex flex-wrap items-center justify-end gap-3">
            {validacao.validas === 0 ? (
              <p className="text-sm text-muted">Nenhuma linha pode ser importada. Corrija a planilha e envie de novo.</p>
            ) : (
              <>
                {validacao.comErro > 0 && <p className="text-sm text-muted">As linhas com erro serão ignoradas.</p>}
                <button type="button" onClick={importar} disabled={ocupado} className={estilos.botaoPrimario}>
                  {ocupado ? 'Importando...' : `Importar ${plural(validacao.validas, 'linha válida', 'linhas válidas')}`}
                </button>
              </>
            )}
          </div>
        </section>
      )}
    </div>
  )
}
