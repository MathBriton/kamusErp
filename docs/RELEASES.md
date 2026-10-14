# Releases versionadas

O Kamus usa **versionamento semântico** (`MAJOR.MINOR.PATCH`) calculado automaticamente a partir dos commits ([Conventional Commits](https://www.conventionalcommits.org/pt-br/)). Não depende de nenhuma configuração extra do repositório.

## Como lançar uma release

1. Abra **Actions → release → Run workflow** (branch `main`).
2. Escolha o incremento: **automatico** (recomendado, pelos commits) ou force `patch` / `minor` / `major`.
3. Pronto. O workflow:
   - confere se o **CI está verde** no último commit da `main` (senão, para com erro);
   - calcula a próxima versão desde a última tag;
   - cria a tag **`vX.Y.Z`** e a **GitHub Release** com as notas em português;
   - publica as imagens Docker no GHCR:
     - `ghcr.io/mathbriton/kamus-api:X.Y.Z` (+ `X.Y` e `latest`)
     - `ghcr.io/mathbriton/kamus-web:X.Y.Z` (+ `X.Y` e `latest`)
     - `ghcr.io/mathbriton/kamus-admin:X.Y.Z` (Painel Administrativo; + `X.Y` e `latest`)

Se não houver commits `feat`/`fix`/`perf` desde a última release, o modo automático não lança nada (avisa no resumo da execução).

## Como a versão é calculada

| Commit | Exemplo | Antes da 1.0 | Depois da 1.0 |
|---|---|---|---|
| `fix:` / `perf:` | `fix(estoque): custo médio com saldo zerado` | patch: 0.1.0 → 0.1.1 | 1.2.0 → 1.2.1 |
| `feat:` | `feat(vendas): troca e vale-troca` | minor: 0.1.0 → 0.2.0 | 1.2.0 → 1.3.0 |
| `feat!:` ou `BREAKING CHANGE:` no corpo | `feat!: nova autenticação` | minor: 0.1.0 → 0.2.0 | major: 1.2.0 → 2.0.0 |
| `docs:`, `refactor:`, `build:` | | entram nas notas, não geram versão sozinhos | |
| `ci:`, `test:`, `chore:`, `style:` | | não aparecem nas notas | |

Commits fora do padrão (ex.: "Merge pull request...") são ignorados. A **1.0.0** marca o primeiro cliente em produção: lance com o incremento `major` quando chegar a hora.

## Onde a versão aparece

- **GitHub Releases:** notas de cada versão (é o changelog do projeto)
- **Imagens Docker:** tag da imagem e label `org.opencontainers.image.version`
- **API:** `GET /api/versao` → `{ "versao": "0.2.0", "commit": "3f2c1ab..." }`

Em desenvolvimento local a versão é `0.0.0` — a real é injetada no build da release.

## Testar o cálculo localmente (não cria nada)

```bash
INCREMENTO=automatico node .github/scripts/release/cli.mjs   # mostra a próxima versão e gera release-notes.md
node --test .github/scripts/release/*.test.mjs               # testes do cálculo
```

## Boas práticas recomendadas (opcionais)

- **Squash merge com o título do PR** (Settings → General → Pull Requests): o título vira o commit e entra nas notas; o workflow `pr-title` valida o formato.
- **Proteger a `main`** (Settings → Rules → Rulesets): exigir PR e os checks `build-test` e `pr-title`.
- **Imagens no GHCR** ficam privadas por padrão; quando houver servidor, use `docker login ghcr.io` com um token `read:packages`.

## Arquivos envolvidos

| Arquivo | Papel |
|---|---|
| `.github/workflows/release.yml` | Botão de release: valida CI, cria tag/Release e publica imagens |
| `.github/scripts/release/versao.mjs` | Cálculo da versão e geração das notas (funções puras, testadas) |
| `.github/scripts/release/cli.mjs` | Lê o histórico do git e alimenta o workflow |
| `.github/workflows/scripts-ci.yml` | Roda os testes do script quando ele muda |
| `.github/workflows/pr-title.yml` | Garante títulos de PR no padrão Conventional Commits |
