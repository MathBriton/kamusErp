import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { gerarNotas, interpretarCommit, proximaVersao } from './versao.mjs'

const c = (assunto, corpo = '', sha = 'a'.repeat(40)) => ({ sha, assunto, corpo })

describe('interpretarCommit', () => {
  it('lê tipo, escopo e assunto', () => {
    assert.deepEqual(interpretarCommit(c('feat(vendas): troca e vale-troca')), {
      tipo: 'feat', escopo: 'vendas', descricao: 'troca e vale-troca', incompativel: false, sha: 'a'.repeat(40),
    })
  })

  it('reconhece mudança incompatível por "!" ou BREAKING CHANGE no corpo', () => {
    assert.equal(interpretarCommit(c('feat!: nova autenticação')).incompativel, true)
    assert.equal(interpretarCommit(c('fix(api): rota', 'BREAKING CHANGE: remove /v1')).incompativel, true)
  })

  it('ignora commits fora do padrão (ex.: merge de PR)', () => {
    assert.equal(interpretarCommit(c('Merge pull request #1 from x/y')), null)
    assert.equal(interpretarCommit(c('ajustes')), null)
  })
})

describe('proximaVersao', () => {
  const commits = (...assuntos) => assuntos.map((a) => interpretarCommit(c(a)))

  it('fix → patch, feat → minor', () => {
    assert.equal(proximaVersao('0.1.0', commits('fix: x')), '0.1.1')
    assert.equal(proximaVersao('0.1.3', commits('fix: x', 'feat: y')), '0.2.0')
    assert.equal(proximaVersao('1.2.3', commits('perf: z')), '1.2.4')
  })

  it('mudança incompatível: minor antes da 1.0, major depois', () => {
    assert.equal(proximaVersao('0.4.2', commits('feat!: x')), '0.5.0')
    assert.equal(proximaVersao('1.4.2', commits('fix!: x')), '2.0.0')
  })

  it('sem feat/fix/perf não há release no modo automático', () => {
    assert.equal(proximaVersao('0.1.0', commits('docs: x', 'ci: y', 'chore: z')), null)
    assert.equal(proximaVersao('0.1.0', []), null)
  })

  it('incremento forçado vale mesmo sem commits relevantes', () => {
    assert.equal(proximaVersao('0.1.0', commits('docs: x'), 'patch'), '0.1.1')
    assert.equal(proximaVersao('0.1.9', [], 'minor'), '0.2.0')
    assert.equal(proximaVersao('0.9.9', [], 'major'), '1.0.0')
  })

  it('primeira release parte de 0.0.0', () => {
    assert.equal(proximaVersao('0.0.0', commits('feat: base')), '0.1.0')
  })
})

describe('gerarNotas', () => {
  const lista = [
    c('feat(vendas): troca e vale-troca', '', '1111111aaaa'),
    c('fix(estoque): custo médio com saldo zero', '', '2222222bbbb'),
    c('feat!: nova autenticação', '', '3333333cccc'),
    c('ci: ajusta cache', '', '4444444dddd'),
    c('docs: readme', '', '5555555eeee'),
    c('Merge pull request #2', '', '6666666ffff'),
  ].map(interpretarCommit).filter(Boolean)

  const notas = gerarNotas({ versao: '0.2.0', data: '2026-10-13', commits: lista, repoUrl: 'https://github.com/o/r', anterior: 'v0.1.0' })

  it('agrupa por seção em português, com link do commit', () => {
    assert.match(notas, /## 0\.2\.0 \(2026-10-13\)/)
    assert.match(notas, /### Novidades\n\n- \*\*vendas:\*\* troca e vale-troca \(\[1111111\]\(https:\/\/github\.com\/o\/r\/commit\/1111111aaaa\)\)/)
    assert.match(notas, /### Correções\n\n- \*\*estoque:\*\* custo médio com saldo zero/)
    assert.match(notas, /### Documentação\n\n- readme/)
  })

  it('destaca mudanças incompatíveis primeiro', () => {
    assert.ok(notas.indexOf('### ⚠️ Mudanças incompatíveis') < notas.indexOf('### Novidades'))
    assert.match(notas, /### ⚠️ Mudanças incompatíveis\n\n- nova autenticação/)
  })

  it('oculta ci/test/chore e mostra o link de comparação', () => {
    assert.doesNotMatch(notas, /ajusta cache/)
    assert.match(notas, /https:\/\/github\.com\/o\/r\/compare\/v0\.1\.0\.\.\.v0\.2\.0/)
  })
})
