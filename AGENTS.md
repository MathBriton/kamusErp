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
src/backend/Modules/Financeiro/...            Contas financeiras, categorias, títulos/parcelas/baixas, movimentos, fluxo de caixa — /api/financeiro/...
src/backend/Modules/Vendas/...                Formas de pagamento, vendas, trocas, vales-troca, comissões e fechamentos — /api/vendas/...
src/web/src                                   auth/, api/, busca/, cadastros/, configuracoes/, empresa/, estoque/, financeiro/, layout/, licenca/, navegacao/, notificacoes/, pages/, relatorios/, theme/, vendas/, test/
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
(id, perfil, nome e CNPJ da empresa ativa), `IUsuarioAtual`, `ICatalogoDeProdutos` e `IDiretorioDeFornecedores` (produtos/SKUs e fornecedores pelo CNPJ, implementados pelo Cadastros), `IDiretorioDeVendedores` (Cadastros), `IContasAPagar` (compras, contas avulsas e cancelamento pela origem) e `IContasAReceber` (recebíveis de venda/troca), implementados pelo Financeiro, `IMovimentacaoDeEstoque` (baixa/estorno de venda e devolução de troca, implementado pelo Estoque), `IDiretorioDeClientes` (tenants e Donos) e
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

---

## 10. Estado atual e próximo passo (ATUALIZE AO TERMINAR)

**Última atualização:** 2026-10-18

**Releases:** v0.1.0 → v0.14.0 lançadas; **v0.15.0** (trocas, vale-troca e fechamento de comissões) pronta para lançar. Próxima: **v0.16.0** (crediário e condicional).
Plano de releases em `LEVANTAMENTO.md` → Plano de releases (dashboard/mobile v0.17.0, v1.0.0 produção).

**O que existe hoje:**
- Fundação: login (JWT + refresh rotativo), empresa ativa (`X-Empresa-Id`), histórico de alterações, notificações (SignalR),
  pesquisa global, exportação PDF/Excel/CSV, licenciamento + Painel Administrativo (`/admin-api`, `src/admin`).
- Cadastros (v0.7–v0.10): empresas/usuários, pessoas, vendedores, grades/cores/produtos com SKUs, importação por planilha.
- Estoque (v0.11–v0.12): lojas, saldo × SKU com custo médio, entradas/ajustes/transferências, kardex, posição em matriz (célula traz `preco`),
  estoque inicial por planilha, etiquetas (PDF), inventário (contagem cega), entrada por XML da NF-e. Movimentos `Venda`, `EstornoDeVenda`, `Devolucao`.
- Financeiro (v0.13): contas financeiras, categorias, títulos → parcelas → baixas, transferências, extrato, fluxo de caixa; NF-e gera contas a pagar.
- Vendas (v0.14): formas de pagamento (taxa/prazo/conta), registro de venda (leitor/grade, descontos, vários pagamentos, troco), baixa de estoque,
  caixa/recebíveis, cancelamento, comissão por venda, relatórios `vendas`/`comissoes`. Ver `Kamus.Modules.Vendas` (schema `vendas`).
- **Trocas, vale-troca e fechamento (v0.15.0)** — tabelas novas com RLS: trocas, itens_devolvidos, itens_levados, pagamentos_da_troca, vales_troca, fechamentos_de_comissao:
  - Domínio: `Dinheiro` (arredondamento meio-para-cima, `Distribuir` pagamentos/troco — usado por venda e troca); `Venda.ValoresPagosPorSku()` (rateio do
    desconto; a soma fecha o total); `Troca.Criar` (valida quantidades contra vendidas − já devolvidas, diferença paga ou vale, comissão = diferença × %);
    `ValeTroca` (código `VT`+6, `Situacao(hoje)`, `Usar`, `Devolver`, `Cancelar`); `FechamentoDeComissao.Fechar/Reabrir` (marca `FechamentoId` em vendas/trocas);
    `ConfiguracaoDeVendas` ganhou `PrazoDeTrocaDias` (30) e `ValidadeDoValeDias` (90); `TipoDePagamento.ValeTroca` (forma padrão "Vale-troca").
  - API: `GET /api/vendas/devolviveis?numero=` e `/{id}/devolviveis` (prazo, valor pago por peça), `POST/GET /api/vendas/trocas` (estoque: saída dos
    levados + `RegistrarDevolucaoAsync`; financeiro: diferença como recebível origem "Troca"; vale emitido), `GET /api/vendas/vales` (+ `/por-codigo/{codigo}`,
    `/{id}/cancelar`), pagamento com `valeCodigo` em venda/troca (`Aplicacao/Pagamentos.ResolverAsync` e `.Recebiveis`), `GET /api/vendas/comissoes`
    (com trocas e `ComissaoPendente`), `/api/vendas/comissoes/fechamentos` (POST fecha + conta a pagar via `IContasAPagar.RegistrarContaAsync`; reabrir cancela
    a conta via `CancelarOrigemAsync`, 409 se já paga). Venda com troca ou fechada não cancela (409). Relatório `vales-troca`. Filtro `clienteId` em vendas e vales.
  - Web: componentes `EntradaDeItens` e `EditorDePagamentos` (+ `itens.ts`) compartilhados por `RegistroDeVendaPage` e `TrocasPage`; `ValesPage`;
    `ComissoesPage` com fechar/reabrir; regras (prazo/validade) em `FormasDePagamentoPage`; `HistoricoDoCliente` na ficha do cliente.
- Testes: 479 backend, 157 web, 10 admin. E2E em localhost (troca com defeito → vale, venda paga com vale + dinheiro, troca levando peça mais cara com troco,
  fora do prazo só com Dono, vales, fechamento com conta a pagar e reabertura, ficha do cliente, kardex, tema escuro), 3 execuções.

**Próximo passo — v0.16.0: crediário e condicional** (ver `LEVANTAMENTO.md` 4.7.7 e 4.7.8):
1. Crediário: limite por cliente (Dono/Gerente), forma de pagamento "Crediário" gerando parcelas a receber vinculadas ao cliente, carnê/recibo em PDF,
   recebimento com multa/juros configuráveis, bloqueio por atraso, aging de inadimplência; comissão sobre recebido como opção (dúvida 4).
2. Condicional: saída para local virtual "em condicional" com prazo e limite por cliente, termo em PDF, retorno (devolve ou vira venda), alertas e bloqueio.
3. Ficha do cliente com crediário e condicionais abertas.

**Pendências conhecidas:** ver "Dúvidas em aberto e débitos técnicos" no `README.md` (lista viva — acrescente lá).
Também: branch órfã `release-please--branches--main--components--kamus` no GitHub (pode ser apagada pela interface).
