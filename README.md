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
- [Docker](https://www.docker.com) (necessário também para os testes de integração)

## Rodando localmente

**Tudo via Docker:**

```bash
cp .env.example .env
docker compose up -d --build
```

| Serviço | URL |
|---|---|
| Web | http://localhost:5173 |
| API | http://localhost:8080/health |
| PostgreSQL | localhost:5432 (usuário da aplicação: `kamus_app`) |

**Desenvolvimento (hot reload):**

```bash
docker compose up -d db                     # só o banco
dotnet run --project src/backend/Kamus.Api  # API em http://localhost:5249
cd src/web && npm install && npm run dev    # web em http://localhost:5173 (proxy para a API)
```

Para usar a API do Docker Compose no `npm run dev`: `KAMUS_API_URL=http://localhost:8080 npm run dev`.

## Testes

```bash
dotnet test --solution Kamus.slnx   # backend (sobe um PostgreSQL real via Testcontainers)
cd src/web && npm test              # frontend
```

TDD first: todo código de produção nasce de um teste que falhou antes.

## Versões e releases

Versionamento semântico automático a partir de [Conventional Commits](https://www.conventionalcommits.org/pt-br/) (`feat:`, `fix:`, `docs:`...). Cada release gera tag, changelog e imagens Docker no GHCR. Detalhes em [docs/RELEASES.md](docs/RELEASES.md). A versão em execução aparece em `GET /api/versao`.

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
