# Releases versionadas

O Kamus usa **versionamento semântico** (`MAJOR.MINOR.PATCH`) gerado automaticamente a partir dos commits, com o [release-please](https://github.com/googleapis/release-please).

## Como funciona

```
commits na main (feat:, fix:, ...)
        │
        ▼
release-please abre/atualiza o PR "chore: release x.y.z"  ← CHANGELOG e versões já atualizados
        │  (você revisa e faz o merge quando quiser lançar)
        ▼
tag vX.Y.Z + GitHub Release com notas
        │
        ▼
imagens Docker publicadas no GHCR
  ghcr.io/mathbriton/kamus-api:X.Y.Z  (+ X.Y e latest)
  ghcr.io/mathbriton/kamus-web:X.Y.Z  (+ X.Y e latest)
```

Nada é lançado sozinho: **a release só acontece quando você faz o merge do PR de release.** Enquanto isso, cada novo commit na `main` atualiza o mesmo PR.

## Como a versão é calculada

| Commit | Exemplo | Efeito (antes da 1.0) | Efeito (depois da 1.0) |
|---|---|---|---|
| `fix:` | `fix(estoque): custo médio com saldo zerado` | patch: 0.1.0 → 0.1.1 | 1.2.0 → 1.2.1 |
| `feat:` | `feat(vendas): troca e vale-troca` | minor: 0.1.0 → 0.2.0 | 1.2.0 → 1.3.0 |
| `feat!:` ou `BREAKING CHANGE:` no corpo | `feat!: nova API de autenticação` | minor: 0.1.0 → 0.2.0 | major: 1.2.0 → 2.0.0 |
| `docs:`, `build:`, `refactor:`, `perf:` | | entram no changelog, sem nova versão sozinhos | |
| `ci:`, `test:`, `chore:`, `style:` | | não aparecem no changelog | |

Enquanto o produto estiver em `0.x`, mudanças incompatíveis sobem só o **minor**. A `1.0.0` marca o primeiro cliente em produção — para lançá-la, faça um commit com `Release-As: 1.0.0` no corpo da mensagem.

## Onde a versão aparece

- `CHANGELOG.md` e `version.txt` na raiz
- `Directory.Build.props` (`<Version>`) → todos os projetos .NET
- `src/web/package.json`
- API: `GET /api/versao` → `{ "versao": "0.2.0", "commit": "3f2c1ab..." }`

Não edite esses números à mão: o release-please cuida disso.

## Configuração única no GitHub (feita uma vez)

1. **Settings → Actions → General → Workflow permissions:** marque **Read and write permissions** e **Allow GitHub Actions to create and approve pull requests**. Sem isso o release-please não consegue abrir o PR de release.
2. **Settings → General → Pull Requests:** deixe habilitado **Allow squash merging** com **Default to pull request title**. O título do PR vira o commit e alimenta o changelog (o workflow `pr-title` valida o formato).
3. **Proteção da `main`** (Settings → Rules → Rulesets): exigir PR e os checks `backend-ci`, `web-ci` e `pr-title`.
4. **Opcional — CI no PR de release:** PRs abertos pelo token padrão do Actions não disparam outros workflows. Para os checks rodarem também no PR de release, crie um *fine-grained token* (permissões: Contents e Pull requests = Read and write, só neste repositório) e salve como secret `RELEASE_PLEASE_TOKEN`.
5. **Imagens no GHCR:** depois da primeira release, os pacotes aparecem em *Packages* no seu perfil. Ficam privados por padrão; para a VPS baixar, use `docker login ghcr.io` com um token com `read:packages`.

## Arquivos envolvidos

| Arquivo | Papel |
|---|---|
| `release-please-config.json` | Tipo de release, seções do changelog em português, arquivos com versão |
| `.release-please-manifest.json` | Última versão lançada (atualizado pelo release-please) |
| `.github/workflows/release.yml` | Roda o release-please e publica as imagens Docker |
| `.github/workflows/pr-title.yml` | Garante títulos de PR no padrão Conventional Commits |
