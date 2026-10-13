// Usado pelo workflow de release. Lê o histórico do git, calcula a próxima versão e
// escreve as notas em release-notes.md. Saídas no GITHUB_OUTPUT: lancar, versao, tag.
//
// Teste local (não cria nada): INCREMENTO=automatico node .github/scripts/release/cli.mjs
import { execFileSync } from 'node:child_process'
import { appendFileSync, writeFileSync } from 'node:fs'
import { gerarNotas, interpretarCommit, proximaVersao } from './versao.mjs'

const git = (...args) => execFileSync('git', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim()

let anterior = null
try {
  anterior = git('describe', '--tags', '--abbrev=0', '--match', 'v[0-9]*.[0-9]*.[0-9]*')
} catch {
  // Nenhuma release ainda: a primeira parte de 0.0.0 com todo o histórico.
}

const intervalo = anterior ? [`${anterior}..HEAD`] : ['HEAD']
const SEP_CAMPO = '\x1f'
const SEP_COMMIT = '\x1e'
const brutos = git('log', ...intervalo, '--no-merges', `--format=%H${SEP_CAMPO}%s${SEP_CAMPO}%b${SEP_COMMIT}`)
const commits = brutos
  .split(SEP_COMMIT)
  .map((b) => b.trim())
  .filter(Boolean)
  .map((b) => {
    const [sha, assunto, corpo] = b.split(SEP_CAMPO)
    return interpretarCommit({ sha, assunto, corpo })
  })
  .filter(Boolean)

const atual = anterior ? anterior.replace(/^v/, '') : '0.0.0'
const incremento = process.env.INCREMENTO || 'automatico'
const versao = proximaVersao(atual, commits, incremento)

const saida = (chave, valor) => {
  console.log(`${chave}=${valor}`)
  if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, `${chave}=${valor}\n`)
}

if (!versao) {
  console.log(`Nada a lançar desde ${anterior ?? 'o início'}: nenhum commit feat/fix/perf. Use um incremento forçado se quiser lançar mesmo assim.`)
  saida('lancar', 'false')
} else {
  const repoUrl = `${process.env.GITHUB_SERVER_URL ?? 'https://github.com'}/${process.env.GITHUB_REPOSITORY ?? 'MathBriton/kamusErp'}`
  const data = new Date().toISOString().slice(0, 10)
  writeFileSync('release-notes.md', gerarNotas({ versao, data, commits, repoUrl, anterior }))
  saida('lancar', 'true')
  saida('versao', versao)
  saida('tag', `v${versao}`)
}
