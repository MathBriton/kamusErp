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
| Produtos | `/api/grades`, `/api/cores` (com `/padrao`), `/api/produtos` (SKUs, `/por-codigo/{ean}`, `/sugestoes`) · Cadastros → Produtos (matriz cor × tamanho) / Grades e cores · relatório `produtos` |
| Importação por planilha | `/api/importacoes/{tipo}/modelo.xlsx`, `/{tipo}/validar` (multipart `arquivo`), `/{id}/confirmar` · Cadastros → Importar planilha |
| Clientes, fornecedores e vendedores | `/api/pessoas` (papel, busca, aniversariantes, anonimizar), `/api/vendedores` · Cadastros → Clientes / Fornecedores / Vendedores · relatórios `clientes` e `fornecedores` |
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
src/backend/Modules/                     Módulos: Identidade, Notificacoes, Licenciamento (+ API do painel), Cadastros
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
| 16 | Limites de CNPJs/lojas/usuários devem **impedir** cadastros acima do limite? | Implementado: **impedem** (409 `LIMITE_DA_LICENCA`) — CNPJs e usuários na v0.7.0, lojas na v0.11.0 |
| 17 | Avisos de vencimento: 7, 3, 1 e 0 dias antes, na carência e na suspensão, só para os Donos — está bom? | Provisório |
| 18 | Cobrança automática (Pix/boleto com baixa automática) ou registro manual no painel? | Provisório: **manual**; gateway depois |
| 19 | E-mail/WhatsApp de suporte exibido na tela de bloqueio | Provisório: `suporte@kamus.com.br` (fictício) |
| 20 | Cliente criado no painel recebe senha provisória: obrigar troca no primeiro acesso? | Provisório e implementado (v0.7.0): **sim**, para todo usuário com senha provisória |
| 21 | Gerente pode cadastrar/desativar usuários (ex.: vendedores da própria empresa)? | Provisório: **não**, só o Dono |
| 22 | "Loja" é diferente de "empresa (CNPJ)"? Ex.: duas lojas no mesmo CNPJ | Implementado (v0.11.0): loja = local de estoque de uma empresa; a primeira nasce com o nome fantasia |
| 23 | Cadastro de cliente: CPF obrigatório? (hoje opcional; ajuda no crediário) | Provisório: **opcional** |
| 24 | Vendedor pode cadastrar e editar clientes? | Provisório: **sim** (não inativa nem anonimiza) |
| 25 | Mensagem padrão de aniversário no WhatsApp — texto e se oferece desconto | Provisório: texto fixo ("Preparamos um mimo para você na loja") |
| 26 | Markup padrão por categoria (sugerir preço a partir do custo)? | Provisório: só exibe o markup; sem sugestão automática |
| 27 | Produto precisa de NCM/dados fiscais já agora? | Provisório: **não** (fiscal fora do piloto) |
| 28 | Fotos de produto (por produto e por cor) | Em aberto: precisa de armazenamento de arquivos (servidor) |
| 29 | Importação: estoque inicial, contas a pagar/receber e saldos de crediário também por planilha | Estoque inicial feito (v0.11.0); os demais nas releases de financeiro e crediário |
| 30 | Importação: casar cliente pelo **nome** quando não há CPF nem telefone pode juntar homônimos | Provisório: casa pelo nome exato (sem acento); revisar com a loja piloto |
| 31 | Transferência entre lojas de **CNPJs diferentes** (exige nota fiscal de transferência)? | Provisório: só entre lojas da **mesma empresa** |
| 32 | Estoque negativo: movimentos manuais (ajuste, transferência) podem deixar negativo? | Provisório: **não** (409 com o saldo disponível); a regra "permitir com alerta" fica para a venda (v0.14.0) |
| 33 | Quem vê custo médio e kardex? | Provisório: Dono, Gerente, Estoquista e Financeiro; Vendedor vê só quantidades |
| 34 | Ajuste: motivos fixos (perda, avaria, furto, correção, outro) bastam? | Provisório: sim; "outro" exige observação |
| 35 | Inventário: quem conclui (aplica os ajustes)? | Provisório: **Dono e Gerente**; Estoquista conta |
| 36 | Inventário: bloquear vendas/movimentos da loja durante a contagem? | Provisório: **não bloqueia** — o ajuste usa a diferença (contado − foto) sobre o saldo do momento |
| 37 | Etiquetas: formatos de papel da loja piloto (modelo da folha A4 e da impressora térmica) | Provisório: A4 3 × 10 (~63 × 26 mm) e térmica 40 × 25 mm; confirmar com a loja |
| 38 | Custo de aquisição da NF-e: o ICMS é recuperável para a loja (Lucro Real)? | Provisório: custo = valor + frete + seguro + outras − desconto + IPI + ICMS-ST (Simples Nacional, ICMS não recuperável); validar com o contador |
| 39 | XML de NF-e sem protocolo de autorização: aceitar? | Provisório: **aceita com aviso** na tela; protocolo de rejeição/denegação é recusado |
| 40 | Formas de pagamento com taxa e prazo da maquininha (crédito, débito, PIX) | Provisório: ficam para a v0.14.0 (vendas), onde a venda gera os recebíveis |
| 41 | Fluxo de caixa: contas vencidas e não pagas entram na projeção? | Provisório: **não** — aparecem à parte ("em atraso") até serem pagas ou renegociadas |
| 42 | Quem acessa o financeiro? | Provisório: Dono, Gerente e Financeiro |
| 43 | Avisos de contas vencendo (sininho) e resumo diário | Previsto com o resumo diário (v0.16.0) |

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
| Cache da licença por instância (1 min) e rotina de avisos em cada instância | Com várias instâncias: até 1 min para bloquear/liberar nas outras; avisos podem duplicar | Ao escalar (cache distribuído / rotina única) |
| Cliente **sem licença cadastrada não é bloqueado** | Um tenant criado fora do painel usa sem cobrança | Ao ir para produção: criar licença para todos os tenants existentes |
| Renovação de sessão simultânea em duas abas é tratada como roubo de token e encerra as sessões | O usuário pode ser deslogado ao abrir várias abas ao mesmo tempo (na mesma aba já foi resolvido no web) | Aceitar o token anterior por alguns segundos após a troca |
| Sem "esqueci minha senha" por e-mail | Quem esquece depende do Dono redefinir | Quando houver envio de e-mail |
| Busca e aniversariantes de clientes filtram **em memória** | Lento com dezenas de milhares de clientes | Com PostgreSQL em produção: `unaccent`/`pg_trgm` e consulta por mês/dia |
| Modelos de mensagem do WhatsApp fixos no código | A loja não personaliza o texto | Configurações de mensagens (junto com o resumo diário) |
| Ficha do cliente ainda sem histórico de compras, vale-troca e crediário | Visão 360° incompleta | Conforme vendas (v0.14.0) e crediário (v0.15.0) |
| Sem busca de endereço por CEP | Digitação manual | Integração com serviço de CEP (ViaCEP) |
| Busca de produtos em memória | Lenta com catálogos grandes | `pg_trgm` e índice por código de barras no PostgreSQL |
| Grade em uso não pode ganhar tamanho novo | Para um tamanho novo é preciso criar outra grade | Permitir acrescentar tamanhos ao produto |
| Planilha validada fica **em memória** por 30 min aguardando confirmação | Some se a API reiniciar (basta enviar de novo) | Com servidor: guardar em disco/armazenamento de objetos |
| Importação em segundo plano grava como "Sistema" no histórico | O histórico não mostra quem importou (o aviso vai para quem enviou) | Propagar o usuário para tarefas em segundo plano |
| Sequência do código interno calculada pelo maior existente | Duas criações simultâneas podem colidir (o índice único recusa uma) | Sequência no banco quando houver PostgreSQL em produção |
| Posição de estoque e estoque inicial carregam o catálogo **em memória** (`ICatalogoDeProdutos.TodosOsSkusAsync`) | Lento com dezenas de milhares de SKUs | Consulta paginada/projeção no PostgreSQL |
| Movimentos e saldos de estoque ficam sem histórico de alterações (o kardex é o histórico) | Ajuste de estoque também vira evento; demais movimentos só no kardex | — (decisão consciente) |
| Kardex devolve até 500 movimentos, sem paginação nem filtro por data | Produto muito movimentado mostra só os recentes | Paginação/período quando houver volume |
| Desativar SKU no cadastro não olha o saldo | Pode sobrar estoque num SKU desativado (aparece na posição; ajuste e transferência ainda funcionam) | Avisar no cadastro de produtos |
| NF-e: não valida a assinatura digital nem consulta a situação na SEFAZ | Um XML adulterado seria aceito (mitigado: destinatário precisa ser a empresa ativa e a chave não pode repetir) | Antes da produção: validar assinatura e/ou consultar a SEFAZ |
| NF-e: o fornecedor é cadastrado no lançamento, num banco/contexto separado da entrada | Se a gravação da entrada falhar, o fornecedor fica cadastrado (sem dano: é reaproveitado na próxima tentativa) | Transação única quando houver PostgreSQL em produção |
| Duplicatas da NF-e só ficam guardadas | Contas a pagar ainda não existem | v0.13.0 (financeiro): gerar títulos a partir das duplicatas |
| Etiquetas não têm layout configurável (tamanhos e campos fixos) | Lojas com outras etiquetas não conseguem imprimir no papel delas | Configuração de modelos de etiqueta |
| Leitor de código no inventário faz uma requisição por leitura | Lento com internet ruim e contagens grandes | Fila local no navegador / app mobile |
| Financeiro: listagens e fluxo de caixa calculados **em memória** (carregam todos os títulos da empresa) | Lento com dezenas de milhares de parcelas | Consultas agregadas no PostgreSQL / paginação |
| Conta a pagar da NF-e é criada depois da gravação da entrada (outro contexto) | Se falhar, a entrada fica sem a conta a pagar (relançar não duplica, mas a nota já está lançada) | Transação única / outbox quando houver PostgreSQL em produção |
| Sem DRE, conciliação bancária (OFX) nem centro de custo | Relatórios financeiros básicos | DRE na central de relatórios (v0.16.0); OFX pós-MVP |
| Pessoa do título é guardada pelo nome (o vínculo com o cadastro é opcional) | Renomear o fornecedor no cadastro não muda títulos antigos | Mostrar o nome atual quando houver vínculo |

## Multi-tenancy em uma frase

Toda entidade de negócio implementa `ITenantOwned`; o `KamusDbContext` filtra consultas e valida gravações pelo tenant atual, e o PostgreSQL reforça o isolamento com Row Level Security (`EnableTenantRowLevelSecurity` na migration de cada tabela). A aplicação conecta com o papel `kamus_app`, que não é superusuário — senão o RLS seria ignorado.
