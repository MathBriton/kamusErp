# Kamus ERP

SaaS de gestão para lojas de roupas: estoque em grade (cor × tamanho), financeiro e indicadores em tempo real, na web e no celular.

- **Levantamento e decisões:** [LEVANTAMENTO.md](LEVANTAMENTO.md)
- **Mockups das telas:** [docs/mockups](docs/mockups/README.md)

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 (ASP.NET Core), EF Core, PostgreSQL 17 |
| Web | React 19 + TypeScript + Tailwind CSS 4 (Vite) |
| Testes | xUnit v3 + Shouldly + Testcontainers · Vitest + Testing Library |
| Infra | Docker Compose · GitHub Actions |

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org)
- [Docker](https://www.docker.com) — **opcional**: só para rodar com banco e para os testes com PostgreSQL

## Rodando localmente

### Só localhost — sem Docker e sem PostgreSQL (padrão)

Precisa apenas do .NET SDK 10 e do Node.js.

```bash
dotnet run --project src/backend/Kamus.Api   # API em http://localhost:5249 (abre a tela de testes)
cd src/web && npm install && npm run dev     # web em http://localhost:5173 (proxy para a API)
```

| O quê | Onde |
|---|---|
| **Tela para testar a API** (Scalar) | http://localhost:5249/scalar |
| Documento OpenAPI | http://localhost:5249/openapi/v1.json |
| Saúde | http://localhost:5249/health → `Degraded` sem banco (normal neste modo) |
| Versão | http://localhost:5249/api/versao |
| Requisições prontas | [`src/backend/Kamus.Api/Kamus.Api.http`](src/backend/Kamus.Api/Kamus.Api.http) (VS Code com REST Client, Visual Studio, Rider) |

A documentação da API (`/scalar`, `/openapi`) só existe em ambiente de desenvolvimento.

### Com banco (opcional)

**Tudo via Docker:**

```bash
cp .env.example .env
docker compose up -d --build   # web :5173, API :8080, PostgreSQL :5432
```

**API local + PostgreSQL do Docker:**

```bash
docker compose up -d db
dotnet user-secrets set "ConnectionStrings:Kamus" "Host=localhost;Port=5432;Database=kamus;Username=kamus_app;Password=kamus_app_dev" --project src/backend/Kamus.Api
dotnet run --project src/backend/Kamus.Api
```

Para o `npm run dev` usar a API do Compose: `KAMUS_API_URL=http://localhost:8080 npm run dev`.

## Testes

```bash
dotnet test --solution Kamus.slnx   # backend
cd src/web && npm test              # frontend
```

Os testes que precisam de PostgreSQL usam Testcontainers (Docker). **Sem Docker na máquina eles são pulados com aviso** e o restante roda normalmente; no CI (`CI=true`) eles são obrigatórios e a falta de Docker é erro.

TDD first: todo código de produção nasce de um teste que falhou antes.

## Versões e releases

Versionamento semântico calculado a partir de [Conventional Commits](https://www.conventionalcommits.org/pt-br/) (`feat:`, `fix:`, `docs:`...). Para lançar: **Actions → release → Run workflow**. Cada release gera tag, notas e imagens Docker no GHCR. Detalhes em [docs/RELEASES.md](docs/RELEASES.md). A versão em execução aparece em `GET /api/versao`.

## Estrutura

```
src/backend/Kamus.Api                    API (ponto de entrada)
src/backend/Shared/Kamus.SharedKernel    Tipos de domínio compartilhados (sem dependências de framework)
src/backend/Shared/Kamus.Infrastructure  EF Core, multi-tenancy (filtro global + Row Level Security)
src/web                                  Frontend React
tests/backend/                           Testes unitários, de integração e de arquitetura
docker/                                  Scripts de inicialização dos containers
```

## Multi-tenancy em uma frase

Toda entidade de negócio implementa `ITenantOwned`; o `KamusDbContext` filtra consultas e valida gravações pelo tenant atual, e o PostgreSQL reforça o isolamento com Row Level Security (`EnableTenantRowLevelSecurity` na migration de cada tabela). A aplicação conecta com o papel `kamus_app`, que não é superusuário — senão o RLS seria ignorado.
