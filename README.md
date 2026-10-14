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
cd src/admin && npm install && npm run dev   # Painel Administrativo em http://localhost:5174 (opcional)
```

**Logins de teste** (senha `Kamus@Dev123`, cliente "Loja Demo" com 2 CNPJs):
- `admin@kamus.local` — perfil Dono nas duas empresas (vê tudo, inclusive a visão consolidada)
- `vendedor@kamus.local` — perfil Vendedor só no CNPJ Centro (menu reduzido)

**Painel Administrativo** (operador do SaaS, http://localhost:5174): `superadmin@kamus.local`, senha `Kamus@Admin123`.
Lá você registra pagamentos (o cliente é liberado na hora), cria clientes, cancela/reativa licenças e ajusta valor e carência.
Usuário criado em Configurações → Usuários recebe uma **senha provisória** e precisa trocá-la no primeiro acesso.
Para ver a tela de bloqueio no web do cliente: cancele a licença da "Loja Demo" no painel e recarregue o web; reative para voltar.

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
| Licença do cliente | `GET /api/licenca` e `/api/licenca/pagamentos` · tela em Configurações → Licença e assinatura |
| Empresas e usuários | `/api/empresas`, `/api/usuarios`, `POST /api/auth/trocar-senha` · Configurações → Empresas / Usuários e permissões (perfil Dono) |
| Painel Administrativo | http://localhost:5174 · API em `/admin-api/*` (token próprio) |
| Versão | http://localhost:5249/api/versao |
| Requisições prontas | [`src/backend/Kamus.Api/Kamus.Api.http`](src/backend/Kamus.Api/Kamus.Api.http) (VS Code com REST Client, Visual Studio, Rider) |

Para ver a geração em segundo plano (aviso no sininho) com poucos dados: `Relatorios__LimiteSincrono=1 dotnet run --project src/backend/Kamus.Api`.

A documentação da API (`/scalar`, `/openapi`) só existe em ambiente de desenvolvimento.

### Com banco (opcional)

**Tudo via Docker:**

```bash
cp .env.example .env
docker compose up -d --build   # web :5173, painel :5174, API :8080, PostgreSQL :5432
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
cd src/admin && npm test            # Painel Administrativo
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
src/backend/Modules/                     Módulos: Identidade, Notificacoes, Licenciamento (+ API do painel)
src/web                                  Frontend React (cliente)
src/admin                                Painel Administrativo (operador do SaaS)
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
| 6 | Modelo de cobrança do SaaS (por loja, usuário ou plano) | Provisório e implementado (v0.6.0): **plano único "Piloto"**, valor e limites por cliente |
| 7 | Licença: dias de carência e uso durante a carência | Provisório e implementado: **5 dias, uso normal com banner** (ajustável por cliente) |
| 8 | Licença: cliente suspenso pode exportar os próprios dados? | Provisório e implementado: **sim** (hoje só o histórico; os demais relatórios entram conforme forem criados) |
| 9 | Período de teste (trial) de cliente novo | Provisório e implementado: **14 dias** (ajustável ao criar) |
| 10 | Painel Admin: só o dono usa ou haverá equipe (suporte, financeiro)? | Provisório e implementado: **só SuperAdmin** |
| 11 | Troca: prazo padrão e validade do vale-troca | Em aberto |
| 12 | Condicional: prazo de devolução e limite por cliente | Em aberto |
| 13 | Resumo diário: blocos ligados por padrão | Em aberto |
| 14 | Idioma/moeda: só Brasil? | Assumido **só Brasil** (pt-BR, R$) |
| 15 | Valor padrão da mensalidade | Provisório: **R$ 199,90** (alterável por cliente no painel) |
| 16 | Limites de CNPJs/lojas/usuários devem **impedir** cadastros acima do limite? | Provisório: **só informativos** por enquanto |
| 17 | Avisos de vencimento: 7, 3, 1 e 0 dias antes, na carência e na suspensão, só para os Donos — está bom? | Provisório |
| 18 | Cobrança automática (Pix/boleto com baixa automática) ou registro manual no painel? | Provisório: **manual**; gateway depois |
| 19 | E-mail/WhatsApp de suporte exibido na tela de bloqueio | Provisório: `suporte@kamus.com.br` (fictício) |
| 20 | Cliente criado no painel recebe senha provisória: obrigar troca no primeiro acesso? | Provisório e implementado (v0.7.0): **sim**, para todo usuário com senha provisória |
| 21 | Gerente pode cadastrar/desativar usuários (ex.: vendedores da própria empresa)? | Provisório: **não**, só o Dono |
| 22 | "Loja" é diferente de "empresa (CNPJ)"? Ex.: duas lojas no mesmo CNPJ | Provisório: loja = local de estoque, cadastrado no Estoque (v0.11.0) |

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
| Imagens web e admin do Docker não são testadas localmente (limite do Docker Hub no ambiente) | Só validadas no CI de release | — |
| API do Painel Administrativo no **mesmo processo** da API do cliente (`/admin-api`) | Uma falha/ataque no painel atinge a mesma aplicação; mitigado com chave e esquema próprios, filtro por IP e 404 sem chave | Separar em `Kamus.Admin.Api` quando houver PostgreSQL em produção |
| SuperAdmin **sem 2FA** | Só senha + bloqueio após 5 falhas + filtro por IP | Antes de ir para produção |
| Token do painel guardado no `sessionStorage` (8 h, sem renovação nem revogação) | Exposto a XSS na aba do painel; sair não invalida o token no servidor | Junto com o 2FA |
| Limites de CNPJs/lojas/usuários **não são aplicados** | O cliente pode cadastrar acima do limite | Quando houver cadastro de empresas/usuários (v0.7.0) |
| Cache da licença por instância (1 min) e rotina de avisos em cada instância | Com várias instâncias: até 1 min para bloquear/liberar nas outras; avisos podem duplicar | Ao escalar (cache distribuído / rotina única) |
| Cliente **sem licença cadastrada não é bloqueado** | Um tenant criado fora do painel usa sem cobrança | Ao ir para produção: criar licença para todos os tenants existentes |
| Renovação de sessão simultânea em duas abas é tratada como roubo de token e encerra as sessões | O usuário pode ser deslogado ao abrir várias abas ao mesmo tempo (na mesma aba já foi resolvido no web) | Aceitar o token anterior por alguns segundos após a troca |
| Limite de **lojas** ainda não aplicado | Depende do cadastro de lojas/locais | v0.11.0 (Estoque) |
| Sem "esqueci minha senha" por e-mail | Quem esquece depende do Dono redefinir | Quando houver envio de e-mail |

## Multi-tenancy em uma frase

Toda entidade de negócio implementa `ITenantOwned`; o `KamusDbContext` filtra consultas e valida gravações pelo tenant atual, e o PostgreSQL reforça o isolamento com Row Level Security (`EnableTenantRowLevelSecurity` na migration de cada tabela). A aplicação conecta com o papel `kamus_app`, que não é superusuário — senão o RLS seria ignorado.
