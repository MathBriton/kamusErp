// Cálculo de versão (SemVer) e notas de release a partir de Conventional Commits.
// Funções puras, testadas em versao.test.mjs. O uso pelo workflow fica em cli.mjs.

const PADRAO = /^(?<tipo>[a-z]+)(?:\((?<escopo>[^)]+)\))?(?<excl>!)?: (?<descricao>.+)$/

const SECOES = [
  ['feat', 'Novidades'],
  ['fix', 'Correções'],
  ['perf', 'Desempenho'],
  ['refactor', 'Refatoração'],
  ['docs', 'Documentação'],
  ['build', 'Build e infraestrutura'],
]

export function interpretarCommit({ sha, assunto, corpo = '' }) {
  const m = PADRAO.exec(assunto.trim())
  if (!m) return null
  const { tipo, escopo, excl, descricao } = m.groups
  return {
    tipo,
    escopo: escopo ?? null,
    descricao,
    incompativel: Boolean(excl) || /^BREAKING[ -]CHANGE:/m.test(corpo),
    sha,
  }
}

/** @param {'automatico'|'patch'|'minor'|'major'} incremento */
export function proximaVersao(atual, commits, incremento = 'automatico') {
  const [major, minor, patch] = atual.split('.').map(Number)
  const nivel = incremento === 'automatico' ? nivelAutomatico(major, commits) : incremento
  switch (nivel) {
    case 'major': return `${major + 1}.0.0`
    case 'minor': return `${major}.${minor + 1}.0`
    case 'patch': return `${major}.${minor}.${patch + 1}`
    default: return null
  }
}

function nivelAutomatico(major, commits) {
  // Antes da 1.0, mudança incompatível sobe só o minor (produto ainda em formação).
  if (commits.some((c) => c.incompativel)) return major === 0 ? 'minor' : 'major'
  if (commits.some((c) => c.tipo === 'feat')) return 'minor'
  if (commits.some((c) => c.tipo === 'fix' || c.tipo === 'perf')) return 'patch'
  return null
}

export function gerarNotas({ versao, data, commits, repoUrl, anterior }) {
  const item = (c) => {
    const escopo = c.escopo ? `**${c.escopo}:** ` : ''
    return `- ${escopo}${c.descricao} ([${c.sha.slice(0, 7)}](${repoUrl}/commit/${c.sha}))`
  }
  const blocos = [`## ${versao} (${data})`]

  const incompativeis = commits.filter((c) => c.incompativel)
  if (incompativeis.length) blocos.push(`### ⚠️ Mudanças incompatíveis\n\n${incompativeis.map(item).join('\n')}`)

  for (const [tipo, titulo] of SECOES) {
    const daSecao = commits.filter((c) => c.tipo === tipo && !c.incompativel)
    if (daSecao.length) blocos.push(`### ${titulo}\n\n${daSecao.map(item).join('\n')}`)
  }

  if (anterior) blocos.push(`**Comparação:** ${repoUrl}/compare/${anterior}...v${versao}`)
  return blocos.join('\n\n') + '\n'
}
