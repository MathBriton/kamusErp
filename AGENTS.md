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
| Mobile | React Native + Expo SDK 58 (`src/mobile`), testes com jest-expo + Testing Library (RNTL 14) |

## 4. Comandos

```bash
# Rodar (localhost, sem banco)
dotnet run --project src/backend/Kamus.Api        # API http://localhost:5249 (abre /scalar)
cd src/web && npm install && npm run dev          # Web http://localhost:5173 (proxy /api e /health → 5249)
cd src/admin && npm install && npm run dev        # Painel Administrativo http://localhost:5174 (proxy /admin-api → 5249)
cd src/mobile && npm install && npm run web       # App mobile no navegador http://localhost:8081 (chama a API em :5249; CORS liberado em dev)
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
cd src/mobile && npm ci && npm run lint && npm run typecheck && npm test && EXPO_OFFLINE=1 npx expo export --platform web --output-dir dist

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
src/backend/Modules/Financeiro/...            Contas financeiras, categorias, títulos/parcelas/baixas, movimentos, fluxo de caixa, crediário — /api/financeiro/...
src/backend/Modules/Vendas/...                Formas de pagamento, vendas, trocas, vales-troca, comissões, fechamentos, condicionais — /api/vendas/...
src/backend/Modules/Indicadores/...           Dashboard (/api/indicadores) e resumo diário (preferências, prévia, rotina) — só lê os módulos pelo SharedKernel
src/web/src                                   auth/, api/, busca/, dashboard/, cadastros/, crediario/, configuracoes/, empresa/, estoque/, financeiro/, layout/, licenca/, navegacao/, notificacoes/, pages/, relatorios/, theme/, vendas/, test/
src/admin/src                                 Painel Administrativo (operador do SaaS): login SuperAdmin, clientes, pagamentos
src/mobile                                    App Expo: App.tsx, src/auth (Sessao, armazenamento seguro), src/api, src/telas (Login, Principal com abas, Início, Vendas, Contas, Avisos), src/push.ts, src/tema.ts, src/test
tests/backend/Kamus.UnitTests                 Unitários (domínio puro)
tests/backend/Kamus.IntegrationTests          API via WebApplicationFactory; InMemory e PostgreSQL (Testcontainers)
tests/backend/Kamus.ArchitectureTests         Regras de dependência entre camadas
.github/workflows                             backend-ci, web-ci, admin-ci, mobile-ci, scripts-ci, pr-title, release
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
(id, perfil, nome e CNPJ da empresa ativa), `IUsuarioAtual`, `ICatalogoDeProdutos` e `IDiretorioDeFornecedores` (produtos/SKUs e fornecedores pelo CNPJ, implementados pelo Cadastros), `IDiretorioDeVendedores` (Cadastros), `IContasAPagar` (compras, contas avulsas e cancelamento pela origem), `IContasAReceber` (recebíveis de venda/troca, inclusive crediário) e `ICrediario` (limite e situação do cliente), implementados pelo Financeiro, `IMovimentacaoDeEstoque` (baixa/estorno de venda e devolução de troca, implementado pelo Estoque), `IIndicadoresDeVendas`/`IIndicadoresFinanceiros`/`IIndicadoresDeEstoque` (números do dashboard e do resumo diário, cada um no seu módulo), `IDiretorioDeClientes` (tenants, Donos e acessos por usuário) e
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
- Registro de venda (web): leitor e busca ficam desabilitados até as lojas carregarem — nos testes, espere o campo habilitar (`campoLiberado`).
- Datas "de hoje" no web: `hojeIso()` (financeiro/tipos.ts) usa Brasília (UTC−3), como a API; não use `new Date()` local para datas de negócio.
- Vendas: estoque, financeiro e venda gravam em DbContexts diferentes (sem transação); o endpoint compensa (estorna estoque e cancela a origem no financeiro) se algo falhar depois da baixa.
- Resumo diário: a rotina (`RotinaDoResumoDiario`, a cada 5 min) fica desligada nos testes (`Indicadores:RotinaAtiva=false` no `KamusApiFactory`); teste o `GeradorDeResumos.ExecutarAsync(agora)` direto.
- Dashboard (web): o título dos cartões vem de `dados.periodo` (a resposta), não do botão escolhido — senão aparece o título novo com os números antigos enquanto carrega.
- Mobile: o proxy deste ambiente bloqueia docs.expo.dev e a API de versões do Expo — use `EXPO_OFFLINE=1 npx expo install <pacote>` (versões do SDK instalado)
  e consulte os tipos em `node_modules` (e `node_modules/@testing-library/react-native/docs`). Expo muda a cada SDK: não confie na memória.
- Mobile/testes: RNTL 14 tem `render`/`fireEvent`/`act` assíncronos (`await render(...)`); módulos nativos (secure-store, notifications, device, constants,
  safe-area) são substituídos em `src/test/setup.ts`; `src/test/mockApi.ts` casa "MÉTODO /caminho?query" como no web.
- Push: `Push:Habilitado=false` no `KamusApiFactory` (sem internet nos testes); para testar o envio, troque o `IEnvioDePush` com `ConfigureTestServices`.
  No aparelho, o token só sai com `extra.eas.projectId` no app.json (projeto EAS) e, no Android, num development build (o Expo Go não recebe push).

---

## 10. Estado atual e próximo passo (ATUALIZE AO TERMINAR)

**Última atualização:** 2026-10-19

**Releases:** v0.1.0 → **v0.18.0** lançadas; **v0.19.0** (app mobile) pronta para lançar. Próxima: **v0.20.0** (central de relatórios).
Plano de releases em `LEVANTAMENTO.md` → Plano de releases (relatórios v0.20.0, v1.0.0 produção).

**O que existe hoje:**
- Fundação: login (JWT + refresh rotativo), empresa ativa (`X-Empresa-Id`), histórico de alterações, notificações (SignalR),
  pesquisa global, exportação PDF/Excel/CSV, licenciamento + Painel Administrativo (`/admin-api`, `src/admin`).
- Cadastros (v0.7–v0.10): empresas/usuários, pessoas, vendedores, grades/cores/produtos com SKUs, importação por planilha.
- Estoque (v0.11–v0.12): lojas, saldo × SKU com custo médio, entradas/ajustes/transferências, kardex, posição em matriz (célula traz `preco`),
  estoque inicial por planilha, etiquetas (PDF), inventário, entrada por XML da NF-e. Movimentos `Venda`, `EstornoDeVenda`, `Devolucao`.
- Financeiro (v0.13): contas financeiras, categorias, títulos → parcelas → baixas, transferências, extrato, fluxo de caixa; NF-e gera contas a pagar.
- Vendas (v0.14–v0.15): formas de pagamento, registro de venda (leitor/grade, descontos, vários pagamentos, troco), baixa de estoque, caixa/recebíveis,
  cancelamento, comissão; trocas (devolução ao estoque/avariada, diferença paga ou vale-troca), vales-troca como pagamento, fechamento de comissões
  com conta a pagar. Componentes web `EntradaDeItens`/`EditorDePagamentos` compartilhados. Ver `Kamus.Modules.Vendas` (schema `vendas`).
- **Crediário (v0.16.0)** — no módulo Financeiro (tabelas novas com RLS: `limites_de_credito`, `configuracoes_do_crediario`; coluna `titulos.crediario`):
  - Domínio (`Dominio/Crediario.cs`): `LimiteDeCredito` (por cliente, bloqueio manual), `ConfiguracaoDoCrediario` (multa ≤ 2%, juros a.m., dias para bloquear),
    `Encargos.Calcular` (multa uma vez + juros pro rata dia), `Credito.Avaliar` (limite − em aberto, motivo do bloqueio) e `Credito.Faixa` (aging).
    `Titulo.MarcarComoCrediario()` (receber + cliente). `ICrediario` (SharedKernel) implementado por `Aplicacao/Crediario`.
  - Vendas: `TipoDePagamento.Crediario` (forma padrão "Crediário": sem taxa/conta, até 10x, prazo 30 dias, vencimentos mensais via `FormaDePagamento.Recebimentos`);
    `Pagamentos.ResolverAsync` exige cliente e confere bloqueio/disponível; recebíveis com `RecebimentoDeVenda.Crediario = true`.
  - API `/api/financeiro/crediario`: `carteira` (faixas + clientes), `clientes/{id}` (situação + parcelas com encargos de hoje), `clientes/{id}/limite` (PUT, Dono/Gerente),
    `clientes/{id}/carne.pdf`, `parcelas/{id}/receber` (encargos sugeridos se não informados), `baixas/{id}/recibo.pdf`, `contas`, `configuracao`.
    Perfis: Dono/Gerente/Financeiro/Vendedor consultam e recebem. Relatório `inadimplencia`. PDFs em `Aplicacao/DocumentosDoCrediario` (QuestPDF).
  - Web `crediario/CrediarioPage` (carteira, ficha, receber com recibo, carnê, limite, regras); aviso de limite no `EditorDePagamentos` (prop `clienteId`);
    crediário na ficha do cliente (`HistoricoDoCliente`).
- **Condicional (v0.17.0)** — no módulo Vendas (`Dominio/Condicional.cs`, `/api/vendas/condicionais`, `vendas/CondicionaisPage`): cliente obrigatório, prazo e limites
  configuráveis, peças saem do estoque (`SaidaParaCondicional`/`RetornoDeCondicional`), termo PDF, fechamento devolvendo parte e vendendo o resto via `RegistroDeVendas`.
- **Dashboard e resumo diário (v0.18.0)** — módulo novo `Kamus.Modules.Indicadores` (schema `indicadores`, tabela `preferencias_do_resumo` com RLS):
  - SharedKernel `Indicadores/`: `IIndicadoresDeVendas` (vendas por período, por dia, por forma/vendedor, mais vendidos; condicionais na rua),
    `IIndicadoresFinanceiros` (saldos das contas, a pagar/receber hoje/amanhã/7 dias/vencido, crediário em atraso), `IIndicadoresDeEstoque` (peças, valor a custo
    e a preço de venda, SKUs negativos, quebras de grade). Implementações em `Aplicacao/Indicadores*.cs` de Vendas, Financeiro e Estoque.
    `IDiretorioDeClientes.AcessosDoTenantAtualAsync` (empresa e perfil de cada usuário ativo).
  - `Aplicacao/Painel` monta o `/api/indicadores?periodo=hoje|7dias|mes` (cartões com comparação, 30 dias, alertas com link) com blocos por perfil (`Aplicacao/Perfis`).
  - Resumo diário: `PreferenciasDoResumo` (hora de Brasília, dias em bits, blocos, enviar sem movimento, último envio), `ResumoDiario` (texto por usuário, cada bloco
    só nas empresas em que o perfil vê), `GeradorDeResumos` (percorre os tenants; padrão ligado às 20h para o Dono) e `RotinaDoResumoDiario`.
    Endpoints `GET/PUT /api/indicadores/resumo-diario`, `GET .../previa`, `POST .../enviar` (teste).
  - Web: `pages/DashboardPage` (cartões, gráfico SVG de 30 dias, alertas, saldos, mais vendidos, formas/vendedores, condicionais, estoque, primeiros passos só sem dados)
    e `dashboard/ResumoDiarioDialogo`; o sininho mostra mensagens com várias linhas. Menu "Relatórios" previsto para a v0.20.0.
- **App mobile (v0.19.0)** — `src/mobile` (Expo SDK 58), somente leitura:
  - Backend: `/api/auth/mobile/login|refresh|logout` (token de sessão no corpo, sempre "manter conectado"); CORS só para `Cors:Origens`
    (Development: http://localhost:8081); `Dispositivo` no módulo Notificações (tabela `dispositivos` com RLS, `PUT/DELETE /api/notificacoes/dispositivos`)
    e `IEnvioDePush` (`EnvioDePushExpo` → Expo Push API) chamado pelo `Notificador` a cada notificação; tokens `DeviceNotRegistered` saem da lista.
  - App: `auth/Sessao` (token de acesso em memória, token de sessão no SecureStore/localStorage no web, renova uma vez no 401), empresa ativa com "Todas
    as empresas" para quem é Dono em todas, abas por perfil (Início = `/api/indicadores`; Vendas de hoje; Contas a pagar/receber de 7 dias; Avisos com
    contador), `push.ts` (permissão + token do Expo → registra o aparelho), tema claro/escuro com os tokens da web. CI `mobile-ci`.
- Testes: 531 backend, 171 web, 10 admin, 7 mobile. E2E do app no navegador (login, períodos, consolidado, abas, marcar avisos, recarregar mantendo a sessão,
  sair, vendedora no tema escuro), 3 execuções.

**Próximo passo — v0.20.0: central de relatórios** (menu "Relatórios", hoje "previsto"; ver `LEVANTAMENTO.md` 4.6 e plano de releases):
1. Tela única com os relatórios por área (vendas por período/produto/cor/tamanho/vendedor, produtos sem giro, DRE gerencial, estoque, financeiro),
   reaproveitando o `IRelatorio` e o `BotaoExportar` (PDF/Excel/CSV, segundo plano).
2. DRE gerencial simples (receitas por categoria, CMV pelo custo das vendas, despesas por categoria) a partir do Financeiro e das vendas.
3. Produtos sem giro (sem venda há N dias, com estoque) e curva ABC de produtos.
Depois: v1.0.0 produção (depende de servidor); com o dono: projeto EAS para push real e publicação do app.

**Pendências conhecidas:** ver "Dúvidas em aberto e débitos técnicos" no `README.md` (lista viva — acrescente lá).
Também: branch órfã `release-please--branches--main--components--kamus` no GitHub (pode ser apagada pela interface).
