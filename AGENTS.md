# AGENTS.md — guia para agentes de código (Codex, Claude Code e outros)

Este arquivo é a **fonte única de contexto** para qualquer agente que trabalhe neste repositório.
O `CLAUDE.md` apenas importa este arquivo. **Ao terminar uma tarefa, atualize a seção
"Estado atual e próximo passo"** para que o próximo agente (ou ferramenta) continue sem perder contexto.

---

## 1. O produto

**Kamus ERP** — SaaS multi-tenant de gestão para **lojas de roupas** (piloto): estoque em grade
(cor × tamanho), financeiro, vendas no backoffice, app mobile somente leitura para o dono.

- Regras de negócio, decisões e roadmap completos: [`LEVANTAMENTO.md`](LEVANTAMENTO.md) (documento vivo, em pt-BR).
- Mockups das telas: [`docs/mockups/`](docs/mockups/README.md).
- Processo de release: [`docs/RELEASES.md`](docs/RELEASES.md).
- **Fora do escopo do piloto:** PDV e emissão fiscal (em estudo), lote/validade.

## 2. Restrições do ambiente do dono do projeto (importante)

- Ele testa **apenas em localhost**, **sem Docker** e **sem PostgreSQL / sem persistência local**.
- Por isso, em desenvolvimento a API usa **EF Core InMemory** (dados de demonstração recriados a cada subida).
- PostgreSQL existe só no **CI** (Testcontainers) e futuramente em produção. **CD (deploy) está adiado** — não há servidor.
- Toda feature precisa ser **testável em localhost sem banco**: `dotnet run` + `npm run dev`.

## 3. Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 (ASP.NET Core minimal APIs), EF Core 10, PostgreSQL 17 (prod/CI), InMemory (dev) |
| Web | React 19 + TypeScript + Vite 8 + Tailwind CSS 4 |
| Testes backend | xUnit v3 (**Microsoft Testing Platform**) + Shouldly + Testcontainers + NetArchTest |
| Testes web | Vitest + Testing Library (jsdom) |
| CI/Release | GitHub Actions; release por botão (script próprio em `.github/scripts/release`) |
| Mobile | React Native (Expo) — ainda não iniciado |

## 4. Comandos

```bash
# Rodar (localhost, sem banco)
dotnet run --project src/backend/Kamus.Api        # API http://localhost:5249 (abre /scalar)
cd src/web && npm install && npm run dev          # Web http://localhost:5173 (proxy /api e /health → 5249)
cd src/admin && npm install && npm run dev        # Painel Administrativo http://localhost:5174 (proxy /admin-api → 5249)
# Login de teste: admin@kamus.local / Kamus@Dev123  (tenant "Loja Demo", 2 CNPJs)
# Painel: superadmin@kamus.local / Kamus@Admin123

# Testes backend — ATENÇÃO: usa Microsoft Testing Platform, então é --solution
dotnet test --solution Kamus.slnx
CI=true dotnet test --solution Kamus.slnx         # simula o CI: falta de Docker vira erro

# Qualidade backend (o CI roda exatamente isto)
dotnet format Kamus.slnx --verify-no-changes
dotnet build Kamus.slnx -c Release                # TreatWarningsAsErrors = true

# Web e Painel (o CI roda exatamente isto, em cada pasta)
cd src/web && npm ci && npm run lint && npm run typecheck && npm test && npm run build
cd src/admin && npm ci && npm run lint && npm run typecheck && npm test && npm run build

# Script de release
node --test .github/scripts/release/*.test.mjs    # (Node 22 exige o caminho do arquivo, não a pasta)
INCREMENTO=automatico node .github/scripts/release/cli.mjs   # simula a próxima versão (não cria nada)

# Migrations (ferramenta local: dotnet-tools.json)
dotnet tool restore
dotnet ef migrations add <Nome> --project src/backend/Modules/<Modulo>/Kamus.Modules.<Modulo> \
  --startup-project src/backend/Modules/<Modulo>/Kamus.Modules.<Modulo> --output-dir Persistencia/Migrations
```

## 5. Estrutura

```
src/backend/Kamus.Api                         Ponto de entrada (Program.cs, health, versão, OpenAPI/Scalar)
src/backend/Shared/Kamus.SharedKernel         Tipos de domínio sem dependências de framework (Cnpj, ITenantOwned...)
src/backend/Shared/Kamus.Infrastructure       KamusDbContext (isolamento por tenant), RLS, persistência, middleware de tenant
src/backend/Shared/Kamus.Infrastructure/Auditoria  Histórico de alterações (interceptor, schema auditoria, /api/auditoria)
src/backend/Shared/Kamus.Infrastructure/Busca      Pesquisa global (/api/busca) — junta os IProvedorDeBusca dos módulos
src/backend/Shared/Kamus.Infrastructure/Relatorios Exportação PDF/Excel/CSV (/api/relatorios/{codigo}.{formato}) + segundo plano
src/backend/Shared/Kamus.Infrastructure/Planilhas  Leitura de .xlsx/.csv (LeitorDePlanilha, ValoresDaPlanilha) e modelos (ModeloDePlanilha)
src/backend/Shared/Kamus.Infrastructure/Importacao Importação genérica (/api/importacoes): IImportador, sessões, confirmação e segundo plano
src/backend/Shared/Kamus.Infrastructure/Etiquetas  Código de barras em SVG (EAN-13/8, ITF-14) e PDF de etiquetas (A4 3×10 e térmica 40×25)
src/backend/Modules/Identidade/...            Módulo Identidade: Dominio/, Aplicacao/, Persistencia/, Endpoints/, Busca/, Seed/
src/backend/Modules/Notificacoes/...          Módulo Notificacoes: notificações por usuário + SignalR (/hubs/notificacoes)
src/backend/Modules/Licenciamento/...         Licença por cliente (/api/licenca, bloqueio 402, avisos) + API do Painel (Administracao/, /admin-api)
src/backend/Modules/Cadastros/...             Clientes/fornecedores (/api/pessoas), vendedores, grades, cores e produtos com SKUs (/api/produtos); ICatalogoDeProdutos
src/backend/Modules/Estoque/...               Lojas, saldos, movimentos/kardex, custo médio, inventário, etiquetas, NF-e (NotasFiscais/LeitorDeNfe) — /api/estoque/...
src/web/src                                   auth/, api/, busca/, cadastros/, configuracoes/, empresa/, layout/, licenca/, navegacao/, notificacoes/, pages/, relatorios/, theme/, test/
src/admin/src                                 Painel Administrativo (operador do SaaS): login SuperAdmin, clientes, pagamentos
tests/backend/Kamus.UnitTests                 Unitários (domínio puro)
tests/backend/Kamus.IntegrationTests          API via WebApplicationFactory; InMemory e PostgreSQL (Testcontainers)
tests/backend/Kamus.ArchitectureTests         Regras de dependência entre camadas
.github/workflows                             backend-ci, web-ci, admin-ci, scripts-ci, pr-title, release
docs/                                         mockups, RELEASES.md
```

Novo módulo = `src/backend/Modules/<Nome>/Kamus.Modules.<Nome>` com as mesmas pastas, um `DbContext`
próprio herdando de `KamusDbContext` registrado com `AddKamusDbContext<T>(config, schema)` (isso já liga
auditoria, RLS e registra as migrations para `AplicarMigracoesKamusAsync`), schema próprio,
`AddModulo<Nome>()` / `MapModulo<Nome>()` chamados no `Program.cs`. Para importar por planilha, registre um `IImportador`
(`Kamus.Infrastructure.Importacao`; recebe `ContextoDaImportacao` com usuário e empresa). Para aparecer na pesquisa global,
registre um `IProvedorDeBusca`. Para ter relatório exportável, registre um `IRelatorio` (código, título,
perfis e `GerarAsync` devolvendo `DadosDoRelatorio`) e use `<BotaoExportar relatorio="codigo" parametros={filtros}/>`
na tela — PDF, Excel, CSV, histórico e segundo plano vêm de graça. **Lembre de adicionar o `.csproj` novo no `src/backend/Kamus.Api/Dockerfile`**
(o restore copia os projetos um a um) e de incluir o assembly nos testes de arquitetura.

**Módulos conversam só pelo SharedKernel** (há teste de arquitetura): `INotificador` (notificar usuários),
`IRegistradorDeEventos` (eventos no histórico), `IProvedorDeBusca`, `IRelatorio`, `IEmpresaAtiva`
(id, perfil, nome e CNPJ da empresa ativa), `IUsuarioAtual`, `ICatalogoDeProdutos` e `IDiretorioDeFornecedores` (produtos/SKUs e fornecedores pelo CNPJ, implementados pelo Cadastros), `IDiretorioDeClientes` (tenants e Donos) e
`ICadastroDeClientes` (criar tenant + empresa + Dono) — esses dois implementados pela Identidade.
Para agir num tenant fora de uma requisição (rotina, painel), crie um escopo e chame `TenantContext.Definir(tenantId)`.

## 6. Regras inegociáveis

1. **TDD first.** Escreva o teste, veja falhar, implemente, veja passar. Nada de código de produção sem teste.
2. **Multi-tenancy:** toda entidade de negócio implementa `ITenantOwned`. O `KamusDbContext` filtra consultas e
   valida gravações; a migration de cada tabela de negócio chama `EnableTenantRowLevelSecurity(schema, tabela)` e
   o módulo chama `GrantKamusAppRole(schema)`. Exceções conscientes (sem RLS): `usuarios`, `tenants`,
   `refresh_tokens` (lidas no login antes de o tenant ser conhecido).
3. **A aplicação conecta como `kamus_app`** (sem superusuário, senão o RLS é ignorado). Migrations usam
   `ConnectionStrings:KamusMigrations` (dono do banco).
4. **Histórico de alterações é automático** para entidades `ITenantOwned` e `[Auditavel]`. Campos sensíveis
   (senhas, tokens, contadores técnicos) recebem `[NaoAuditar]`; entidades volumosas sem valor de negócio
   também (`[NaoAuditar]` na classe). Use `[DisplayName("...")]` na classe para o nome amigável.
5. **Frontend só usa tokens de cor** (`bg-surface`, `text-muted`, `bg-primary`...), nunca cores fixas — dark mode
   precisa funcionar em toda tela nova. Tokens em `src/web/src/index.css`.
6. **Segredos nunca no repositório.** Valores de `appsettings.Development.json` são só para dev local.
7. **Nunca pule/desabilite teste para deixar o CI verde.** Testes de PostgreSQL usam `db.ExigirBanco()`: pulam
   sem Docker local, mas no CI (`CI=true`) a falta de Docker é erro.
8. **Conventional Commits** (`feat:`, `fix:`, `docs:`, `ci:`, `build:`, `refactor:`, `test:`, `chore:`), com escopo
   quando fizer sentido (`feat(identidade): ...`). Eles calculam a versão da release.

## 7. Convenções

- **Idioma:** código de domínio, testes, mensagens e documentação em **português** (ex.: `Usuario`,
  `RegistrarFalhaDeLogin`, `Consulta_SemTenantDefinido_NaoRetornaNada`). Termos técnicos consagrados em inglês
  são aceitos (`DbContext`, `Endpoint`, `Middleware`).
- **Nomes de teste:** `Metodo_Cenario_ResultadoEsperado`.
- **Banco:** `snake_case` (EFCore.NamingConventions), um schema por módulo.
- **Versões de pacotes NuGet:** centralizadas em `Directory.Packages.props`.
- **Analisadores:** `TreatWarningsAsErrors` + `AnalysisLevel latest-recommended`. Logs com `[LoggerMessage]`
  (CA1848). Migrations geradas são ignoradas pelos analisadores via `.editorconfig` (`[**/Migrations/*.cs]`).
- **Respostas de erro da API:** `Results.Problem(...)` (ProblemDetails) com `detail` em português, pronto para exibir.
- **Documentação da API** (`/scalar`, `/openapi/v1.json`) só em Development; requisições prontas em
  `src/backend/Kamus.Api/Kamus.Api.http`.

## 8. Fluxo de trabalho

- O dono pediu commits **direto na `main`** (sem branch/PR por enquanto). Rode a verificação completa antes do push.
- **Release:** GitHub → Actions → `release` → Run workflow (incremento `automatico`). Só lança com CI verde no
  commit. Gera tag `vX.Y.Z`, GitHub Release com notas e imagens `ghcr.io/mathbriton/kamus-api|kamus-web`.
- Ao mudar uma decisão, registre na seção "Registro de decisões" do `LEVANTAMENTO.md`.
- **Dúvidas em aberto e débitos técnicos** ficam no `README.md` (seção própria): acrescente lá o que surgir em cada
  tarefa e risque/remova o que for resolvido.
- Respostas ao dono do projeto em **português**.

## 9. Armadilhas já conhecidas

- `dotnet test` sem `--solution` falha (o projeto usa Microsoft Testing Platform via `global.json`).
- `Microsoft.EntityFrameworkCore.Relational` está fixado explicitamente na Infrastructure: sem isso, o pacote
  `Design` compila contra uma versão que não chega em tempo de execução (FileNotFoundException).
- O `Dockerfile` da API precisa copiar `.editorconfig` (senão as migrations quebram o build por analisador) e
  cada `.csproj` de módulo.
- `Microsoft.Extensions.Identity.Core` já vem no shared framework do ASP.NET — não referenciar (erro NU1510).
- Cookie de sessão `kamus_refresh` é `Secure` fora de Development; em Development (http://localhost) não é.
- Release workflow precisa de `checks: read` para conferir o CI do commit.
- Os mockups ficam em `docs/mockups/src/mockups.html` e viram PNG com `node docs/mockups/src/render.mjs`.
- Testes web: `@microsoft/signalr` é substituído por um falso em `src/web/src/test/signalrFake.ts` (via `setup.ts`);
  dispare eventos do servidor com `emitirDoServidor('notificacao', ...)`. O `mockApi` casa a URL exata
  (inclusive query string).
- Testes de SignalR no backend usam `LongPolling` com `Server.CreateHandler()` (o TestServer não faz WebSocket).
- QuestPDF exige `QuestPDF.Settings.License = LicenseType.Community` (feito no `ExportadorPdf`); funciona no Linux e na
  imagem `aspnet:10.0` sem dependências extras (testado). Licença Community: grátis abaixo de US$ 1 mi/ano de faturamento.
- Testes web que criam `Response` com corpo binário: use string, não `Blob` do jsdom (a `Response` do Node rejeita).
- `pkill -f`/`pgrep -f` com um padrão que aparece no próprio comando mata o shell (exit 144): pare processos pelo PID.
- No frontend, telas implementadas entram em `PAGINAS_IMPLEMENTADAS` (`src/web/src/App.tsx`); item do menu sem
  `previstoPara` precisa ter tela lá.
- Painel Administrativo só existe com `Admin:Chave` (≥ 32 caracteres; em Development vem do appsettings). Sem ela,
  `/admin-api` dá 404. `Admin:IpsPermitidos` restringe por IP. O token do painel não vale na API do cliente e vice-versa.
- Licença: o estado é calculado pela data de **Brasília** (`CalendarioBrasilia`, UTC−3 fixo). Nos testes, mude o vencimento
  pelo `LicenciamentoDbContext` e chame `CacheDeLicencas.Invalidar(tenant)` (cache de 1 min).
- Testes de integração: `KamusApiFactory` fixa `Seed:SenhaSuperAdmin` (no PostgreSQL compartilhado, o primeiro a subir cria o
  SuperAdmin). Rodar só alguns testes: `dotnet run --project tests/backend/Kamus.IntegrationTests -- -method "*Nome*"`.
- Testes web: chamadas não mapeadas no `mockApi` dão 404; `GET /api/licenca` com 404 = "sem licença" (nenhum banner).

---

## 10. Estado atual e próximo passo (ATUALIZE AO TERMINAR)

**Última atualização:** 2026-10-15

**Releases:** v0.1.0 → v0.11.0 lançadas; **v0.12.0 (estoque, parte 2)** pronta para lançar. Próxima: **v0.13.0** (financeiro).
Plano de releases em `LEVANTAMENTO.md` → Plano de releases (vendas v0.14.0, crediário/condicional v0.15.0, dashboard/mobile v0.16.0).

**O que existe hoje:**
- Fundação: login (JWT + refresh rotativo), empresa ativa (`X-Empresa-Id`), histórico de alterações, notificações (SignalR),
  pesquisa global, exportação PDF/Excel/CSV, licenciamento + Painel Administrativo (`/admin-api`, `src/admin`).
- Cadastros (v0.7–v0.10): empresas/usuários, pessoas, vendedores, grades/cores/produtos com SKUs, importação por planilha
  (genérica em `Kamus.Infrastructure.Importacao`; tipos clientes, fornecedores, produtos, estoque-inicial).
- Estoque parte 1 (v0.11.0): lojas (limite do plano), saldo por loja × SKU com custo médio, entradas, ajustes, transferências,
  kardex imutável, posição em matriz, relatório `posicao-de-estoque`.
- **Estoque parte 2 (v0.12.0):**
  - **Etiquetas:** `Kamus.Infrastructure.Etiquetas` (`DesenhoDeCodigoDeBarras` SVG; `GeradorDeEtiquetas` QuestPDF). Endpoints
    `POST /api/estoque/etiquetas` (SKUs e quantidades) e `GET /api/estoque/etiquetas/operacao/{id}?formato=` (uma por peça da entrada).
  - **Inventário:** `Inventario` + `ItemDeInventario` (foto na abertura, contagem cega). `/api/estoque/inventarios` (abrir, contagens,
    leituras por código, divergências, concluir — Dono/Gerente —, cancelar). Ajuste = contado − foto sobre o saldo atual (`TipoDeMovimento.Inventario`,
    `SaldoDeEstoque.Inventariar`, nunca negativo). Relatório `inventario`. Evento `inventario_concluido` no histórico.
  - **NF-e:** `NotasFiscais/LeitorDeNfe` (modelo 55, nfeProc ou NFe, XXE bloqueado, custo de aquisição por item, duplicatas).
    `NotaDeEntrada`/`ItemDaNota`/`DistribuicaoDoItem`/`DuplicataDaNota`/`VinculoDeProduto` (de-para). `/api/estoque/notas`
    (importar multipart, conferir item, lançar, descartar). Vinculação automática: de-para do fornecedor → EAN. Fornecedor via
    `IDiretorioDeFornecedores` (SharedKernel, implementado no Cadastros). XML de exemplo em `docs/exemplos/nfe-entrada-exemplo.xml`.
  - Web: `estoque/NotasPage` (importar, conferir com a grade e fator, lançar, etiquetas), `InventarioPage` (grade cega, leitor,
    divergências, concluir), `MovimentacaoPage modo="etiquetas"`, `BotaoEtiquetas`, `BuscaDeProduto`; `useApi().baixar(caminho, corpo?)`.
- Testes: 416 backend, 129 web, 10 admin. E2E em localhost com o XML de exemplo (vincular na grade + caixa × 12 + ignorar, lançar,
  etiquetas, reimportação recusada, fornecedor criado, inventário com grade e leitor, conclusão, etiquetas térmicas, tema escuro), 3 execuções.
  Código de barras do PDF conferido por um decodificador EAN-13.

**Próximo passo — v0.13.0: financeiro** (ver `LEVANTAMENTO.md` 4.3):
1. Contas financeiras (caixa da loja, banco, carteira) e títulos a pagar/receber com parcelas e baixas (parciais), juros/multa.
2. Contas a pagar geradas a partir das duplicatas da NF-e lançada (`DuplicataDaNota`, já gravadas).
3. Fluxo de caixa previsto × realizado; recebíveis de cartão com taxa e prazo.

**Pendências conhecidas:** ver "Dúvidas em aberto e débitos técnicos" no `README.md` (lista viva — acrescente lá).
Também: branch órfã `release-please--branches--main--components--kamus` no GitHub (pode ser apagada pela interface).
