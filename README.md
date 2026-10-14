# Kamus ERP

SaaS de gestão para lojas de roupas: estoque em grade (cor × tamanho), financeiro e indicadores em tempo real, na web e no celular.

- **Levantamento e decisões:** [LEVANTAMENTO.md](LEVANTAMENTO.md)
- **Mockups das telas:** [docs/mockups](docs/mockups/README.md)
- **Guia para agentes de código (Codex / Claude Code):** [AGENTS.md](AGENTS.md)

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

**Logins de teste** (senha `Kamus@Dev123`, cliente "Loja Demo" com 2 CNPJs):
- `admin@kamus.local` — perfil Dono nas duas empresas (vê tudo, inclusive a visão consolidada)
- `vendedor@kamus.local` — perfil Vendedor só no CNPJ Centro (menu reduzido)
Sem banco, os dados ficam **em memória**: são recriados a cada vez que a API sobe e somem quando ela para.

| O quê | Onde |
|---|---|
| **Aplicação web** (login) | http://localhost:5173 |
| **Tela para testar a API** (Scalar) | http://localhost:5249/scalar |
| Documento OpenAPI | http://localhost:5249/openapi/v1.json |
| Saúde | http://localhost:5249/health → `Degraded` sem banco (normal neste modo) |
| Autenticação | `POST /api/auth/login`, `/refresh`, `/logout` e `GET /api/auth/me` |
| Histórico de alterações | `GET /api/auditoria` · tela em Configurações → Histórico de alterações |
| Notificações | `GET /api/notificacoes` · tempo real em `/hubs/notificacoes` · `POST /api/notificacoes/teste` dispara uma de teste |
| Pesquisa global | `GET /api/busca?q=...` · **Ctrl+K** na aplicação web |
| Exportação | `GET /api/relatorios/historico-de-alteracoes.pdf` (ou `.xlsx`, `.csv`) · botão **Exportar** no histórico |

Para ver a geração em segundo plano (aviso no sininho) com poucos dados: `Relatorios__LimiteSincrono=1 dotnet run --project src/backend/Kamus.Api`.
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

## Dúvidas em aberto e débitos técnicos

Lista viva: cada sessão de desenvolvimento acrescenta aqui o que ficou para decidir ou para melhorar depois.
O detalhamento das regras de negócio está no [LEVANTAMENTO.md](LEVANTAMENTO.md) (seção 12).

### Dúvidas de negócio (aguardando o dono do projeto)

| # | Dúvida | Situação |
|---|---|---|
| 1 | Vendas sem PDV: registro de venda simples no sistema web (opção A)? | Assumido **A**, falta confirmar |
| 2 | Loja piloto: loja única ou várias lojas? Que sistema usa hoje? | Em aberto |
| 3 | Compras: precisa de pedido de compra ou só a entrada de mercadoria? | Em aberto |
| 4 | Comissão sobre venda **faturada** ou **recebida** como padrão? | Em aberto |
| 5 | Crediário: multa/juros padrão e dias de atraso para bloquear novas compras | Em aberto |
| 6 | Modelo de cobrança do SaaS (por loja, usuário ou plano) | Provisório: **plano único "Piloto"** com limites por cliente |
| 7 | Licença: dias de carência e uso durante a carência | Provisório: **5 dias, uso normal com aviso** |
| 8 | Licença: cliente suspenso pode exportar os próprios dados? | Provisório: **sim** |
| 9 | Período de teste (trial) de cliente novo | Provisório: **14 dias** |
| 10 | Painel Admin: só o dono usa ou haverá equipe (suporte, financeiro)? | Provisório: **só SuperAdmin** |
| 11 | Troca: prazo padrão e validade do vale-troca | Em aberto |
| 12 | Condicional: prazo de devolução e limite por cliente | Em aberto |
| 13 | Resumo diário: blocos ligados por padrão | Em aberto |
| 14 | Idioma/moeda: só Brasil? | Assumido **só Brasil** (pt-BR, R$) |

### Débitos técnicos

| Débito | Impacto | Quando resolver |
|---|---|---|
| Histórico de alterações é gravado logo **após** o `SaveChanges`, não na mesma transação | Se a gravação do histórico falhar, a alteração fica sem registro | Quando houver PostgreSQL em produção |
| Arquivos de relatório gerados em segundo plano ficam **em memória** | Somem se a API reiniciar | Quando houver servidor (disco ou armazenamento de objetos) |
| Link de download do relatório usa o id aleatório como "segredo" (sem login) | Quem tiver o link baixa por até 60 min | Avaliar link assinado de uso único |
| Pesquisa global compara texto sem acento **em memória** | Lento com milhares de registros | Ao entrar produtos/clientes: `unaccent` + `pg_trgm` no PostgreSQL |
| Desenvolvimento sem persistência (EF Core InMemory) | Dados somem ao reiniciar; RLS só é testado no CI | Quando houver banco local ou servidor |
| SignalR com um único servidor | Com mais de uma instância da API, notificações não chegam a todos | Ao escalar: backplane (Redis) |
| Sem CD (deploy automático) | Releases geram imagens, mas ninguém as publica | Quando houver servidor |
| Imagem web do Docker não é testada localmente (limite do Docker Hub no ambiente) | Só validada no CI de release | — |

## Multi-tenancy em uma frase

Toda entidade de negócio implementa `ITenantOwned`; o `KamusDbContext` filtra consultas e valida gravações pelo tenant atual, e o PostgreSQL reforça o isolamento com Row Level Security (`EnableTenantRowLevelSecurity` na migration de cada tabela). A aplicação conecta com o papel `kamus_app`, que não é superusuário — senão o RLS seria ignorado.
