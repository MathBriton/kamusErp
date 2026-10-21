import { useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { ErroDeApi } from '../auth/api'
import { useAuth } from '../auth/AuthContext'
import { Campo } from '../components/Campo'
import { Dialogo } from '../components/Dialogo'
import { estilos } from '../components/estilos'
import { BarraDeFiltros } from '../components/grade/BarraDeFiltros'
import { CabecalhoDaPagina } from '../components/grade/CabecalhoDaPagina'
import { AcaoDaLinha, GradeDeRegistros, type Coluna } from '../components/grade/GradeDeRegistros'
import { useGrade, useListaPaginada } from '../components/grade/useGrade'
import { Icone } from '../components/Icone'
import { BotaoExportar } from '../relatorios/BotaoExportar'
import type { EmpresaDoCliente } from './tipos'

type Edicao = { tipo: 'nova' } | { tipo: 'editar'; empresa: EmpresaDoCliente } | null

const FILTROS = { busca: '' }

/** Empresas (CNPJs) do cliente em grade: busca, editar, "Nova empresa" e exportar (todas ou as selecionadas). */
export function EmpresasPage() {
  const grade = useGrade(FILTROS)
  const [versao, setVersao] = useState(0)
  const { dados, erro } = useListaPaginada<{ itens: EmpresaDoCliente[]; total: number }>(`/api/empresas?${grade.consulta}`, versao, grade.filtros.busca ? 250 : 0)
  const [edicao, setEdicao] = useState<Edicao>(null)

  const colunas: Coluna<EmpresaDoCliente>[] = [
    { titulo: 'Nome fantasia', celula: (e) => <span className="font-semibold">{e.nomeFantasia}</span> },
    { titulo: 'Razão social', celula: (e) => e.razaoSocial },
    { titulo: 'CNPJ', celula: (e) => <span className="tabular-nums">{e.cnpj}</span> },
  ]

  return (
    <div className="mx-auto max-w-5xl">
      <CabecalhoDaPagina
        secao="Administrativo"
        titulo="Empresas"
        descricao="Os CNPJs do seu negócio. Quem cadastra uma empresa vira Dono dela."
        acoes={
          <>
            <BotaoExportar relatorio="empresas" parametros={grade.parametros} selecionados={[...grade.selecionados]} />
            <button type="button" onClick={() => setEdicao({ tipo: 'nova' })} className={estilos.botaoPrimario}>
              <Icone nome="plus" />
              Nova empresa
            </button>
          </>
        }
      />

      <BarraDeFiltros ativos={grade.ativos} onLimpar={grade.limpar}>
        <label className="min-w-60 flex-1 text-sm">
          <span className="mb-1 block font-semibold">Buscar</span>
          <input value={grade.filtros.busca} onChange={(e) => grade.filtrar({ busca: e.target.value })} placeholder="Nome fantasia, razão social ou CNPJ" className={estilos.campo} />
        </label>
      </BarraDeFiltros>

      {erro && (
        <p role="alert" className={`mt-4 flex items-center gap-2 ${estilos.erro}`}>
          <Icone nome="alert" />
          {erro}
        </p>
      )}

      <GradeDeRegistros
        rotulo="Empresas"
        colunas={colunas}
        itens={dados?.itens ?? (erro ? [] : null)}
        chave={(e) => e.id}
        nome={(e) => e.nomeFantasia}
        total={dados?.total ?? 0}
        {...grade.paginacao}
        selecao={grade.selecao}
        vazio="Nenhuma empresa encontrada."
        acoes={(e) => <AcaoDaLinha rotulo={`Editar ${e.nomeFantasia}`} icone="pencil" onClick={() => setEdicao({ tipo: 'editar', empresa: e })} />}
      />

      {edicao && (
        <FormularioDeEmpresa
          empresa={edicao.tipo === 'editar' ? edicao.empresa : null}
          onFechar={() => setEdicao(null)}
          onSalvo={() => {
            setEdicao(null)
            setVersao((v) => v + 1)
          }}
        />
      )}
    </div>
  )
}

function FormularioDeEmpresa({ empresa, onFechar, onSalvo }: { empresa: EmpresaDoCliente | null; onFechar: () => void; onSalvo: () => void }) {
  const { enviar } = useApi()
  const { recarregar } = useAuth()
  const [razaoSocial, setRazaoSocial] = useState(empresa?.razaoSocial ?? '')
  const [nomeFantasia, setNomeFantasia] = useState(empresa?.nomeFantasia ?? '')
  const [cnpj, setCnpj] = useState(empresa?.cnpj ?? '')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function salvar(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setErro(null)
    try {
      if (empresa) await enviar('PUT', `/api/empresas/${empresa.id}`, { razaoSocial, nomeFantasia })
      else {
        await enviar('POST', '/api/empresas', { razaoSocial, nomeFantasia, cnpj })
        await recarregar() // a empresa nova entra no seletor de CNPJ
      }
      onSalvo()
    } catch (falha) {
      setErro(falha instanceof ErroDeApi ? falha.message : 'Não foi possível salvar.')
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={empresa ? 'Editar empresa' : 'Nova empresa'} onFechar={onFechar}>
      <form onSubmit={(e) => void salvar(e)} className="space-y-4">
        <Campo rotulo="Razão social">
          <input value={razaoSocial} onChange={(e) => setRazaoSocial(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="Nome fantasia">
          <input value={nomeFantasia} onChange={(e) => setNomeFantasia(e.target.value)} className={estilos.campo} />
        </Campo>
        <Campo rotulo="CNPJ">
          <input value={cnpj} onChange={(e) => setCnpj(e.target.value)} disabled={!!empresa} inputMode="numeric" className={estilos.campo} />
        </Campo>
        {empresa && <p className="text-xs text-muted">O CNPJ não muda: outro CNPJ é outra empresa.</p>}
        {erro && (
          <p role="alert" className={estilos.erro}>
            {erro}
          </p>
        )}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onFechar} className={estilos.botao}>
            Cancelar
          </button>
          <button type="submit" disabled={enviando} className={estilos.botaoPrimario}>
            {enviando ? 'Salvando...' : 'Salvar'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
