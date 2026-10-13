# KamusERP — Levantamento do Projeto

> Documento vivo. Vamos atualizando a cada conversa. Seções marcadas com **[ABERTO]** ainda precisam de decisão.

**Última atualização:** 2026-10-13

---

## 1. Visão geral

SaaS no estilo ERP voltado para varejo de pequeno e médio porte (a confirmar o segmento), com:

- **Controle de estoque**
- **Controle financeiro**
- **PDV (Ponto de Venda)**
- **Web** (operação completa) + **Mobile** (somente leitura, para o dono acompanhar a empresa em tempo real)

Por ser SaaS, **multi-tenant** desde o primeiro dia: várias empresas (clientes) usando a mesma instalação, com dados isolados.

---

## 2. Stack

| Camada | Escolha | Observações |
|---|---|---|
| Backend | .NET (ASP.NET Core Web API, versão LTS mais recente) | Monólito modular (ver seção 3) |
| Banco | PostgreSQL | EF Core + Npgsql; migrations versionadas |
| Frontend web | React + TypeScript + Tailwind | Vite como bundler |
| Mobile | React Native (Expo) + TypeScript | Somente leitura no início |
| Infra local | Docker Compose | api, db, web, (pgAdmin opcional) |
| Hospedagem | VPS (Hetzner/DigitalOcean/AWS Lightsail ou EC2) | Imagens Docker publicadas no GHCR |
| CI/CD | GitHub Actions | Build + testes obrigatórios em todo PR |
| Metodologia | **TDD first** | Teste antes do código, sempre |

**[ABERTO]**
- Autenticação: Identity próprio (ASP.NET Core Identity + JWT) vs. provedor externo (Keycloak, Auth0, Clerk). Sugestão inicial: ASP.NET Identity + JWT/refresh token, para não depender de terceiros no começo.
- Monorepo (tudo neste repositório) vs. repositórios separados. Sugestão: **monorepo** (ver seção 9).

---

## 3. Arquitetura

### 3.1 Monólito modular

Começar com **um único deploy** do backend, mas dividido em módulos com fronteiras claras (Estoque, Financeiro, Vendas/PDV, Cadastros, Identidade). Cada módulo tem seu próprio domínio, aplicação e persistência. Se um dia precisar virar microserviço, a fronteira já existe. Microserviços agora seriam custo sem benefício.

Dentro de cada módulo, camadas no estilo Clean Architecture:

```
Modulo.Domain          -> entidades, value objects, regras de negócio (sem dependências)
Modulo.Application     -> casos de uso (commands/queries), interfaces
Modulo.Infrastructure  -> EF Core, integrações externas
Api                    -> endpoints, auth, composição
```

Comunicação entre módulos: via **eventos de domínio** (ex.: `VendaFinalizada` → Estoque baixa saldo, Financeiro gera recebível). No início, eventos em memória; depois, se necessário, outbox + fila.

### 3.2 Multi-tenancy

| Estratégia | Prós | Contras |
|---|---|---|
| **Banco compartilhado + coluna `tenant_id`** | Simples, barato, uma migration só | Risco de vazamento se esquecer o filtro |
| Schema por tenant | Isolamento melhor | Migrations N vezes, mais complexo |
| Banco por tenant | Isolamento máximo | Caro e trabalhoso para SaaS pequeno |

**Sugestão:** banco compartilhado com `tenant_id` em todas as tabelas de negócio, protegido em duas camadas:
1. **Global Query Filter** do EF Core (filtro automático por tenant).
2. **Row Level Security (RLS)** do PostgreSQL como rede de segurança.

E um teste automatizado que garante que nenhuma entidade de negócio existe sem `tenant_id`.

### 3.3 Hierarquia

```
Tenant (cliente do SaaS / assinatura)
 └── Empresa (CNPJ)
      └── Loja/Filial (estoque e caixa são por loja)
           └── Usuários com perfis (Dono, Gerente, Operador de caixa, Estoquista, Financeiro)
```

**[ABERTO]** Um tenant pode ter mais de um CNPJ? (Ex.: dono com duas lojas em CNPJs diferentes.) Sugestão: sim, já modelar assim.

---

## 4. Módulos e regras de negócio

### 4.1 Cadastros base

- **Produtos:** código interno, código de barras (EAN/GTIN), descrição, unidade, preço de venda, preço de custo, categoria, marca, dados fiscais (NCM, CEST, origem), ativo/inativo.
- **Variações (grade):** ex.: camiseta P/M/G × cor. Cada combinação é um **SKU** com estoque próprio. **[ABERTO]** precisa de grade no MVP? (depende do segmento — roupa/calçado sim, mercadinho não.)
- **Unidades e conversão:** compra em CX com 12, vende em UN.
- **Clientes e fornecedores:** pessoa física/jurídica, CPF/CNPJ, contatos, endereço. Normalmente uma única entidade "Pessoa" com papéis.
- **Usuários e permissões:** perfis + permissões granulares (ex.: "dar desconto acima de 10%", "cancelar venda").

### 4.2 Estoque

**Princípio central:** o saldo **não é um campo que se edita**. O saldo é a soma de **movimentações** imutáveis (um livro-razão). Isso dá rastreabilidade total ("por que o saldo está 7?").

**Tipos de movimentação:**

| Tipo | Origem típica | Efeito |
|---|---|---|
| Entrada por compra | Nota fiscal do fornecedor (importar XML da NF-e) | + saldo, recalcula custo |
| Saída por venda | PDV / pedido | − saldo |
| Devolução de cliente | Troca/devolução | + saldo |
| Devolução a fornecedor | Avaria/erro | − saldo |
| Ajuste | Perda, quebra, furto, vencimento (com motivo obrigatório) | ± saldo |
| Transferência | Loja A → Loja B | − em A, + em B |
| Inventário | Contagem física | Gera ajuste pela diferença |

**Regras importantes:**
- **Custo médio ponderado:** é o método mais usado no Brasil (e aceito pelo fisco). A cada entrada: `novo_custo = (saldo_atual × custo_atual + qtd_entrada × custo_entrada) / (saldo_atual + qtd_entrada)`. Base do lucro bruto e do CMV.
- **Estoque mínimo / ponto de reposição:** alerta quando o saldo cai abaixo do limite.
- **Estoque negativo:** permitir ou bloquear? No PDV costuma-se **permitir com alerta** (não travar a fila do caixa por cadastro errado). **[ABERTO]** configurável por empresa.
- **Reserva:** pedido aprovado reserva quantidade (saldo disponível = físico − reservado). Pode ficar para depois do MVP.
- **Lote e validade:** essencial para farmácia/alimentos. **[ABERTO]** depende do segmento.
- **Inventário:** congela uma "foto" do saldo, contagem (pode ser por coletor/celular), comparação, aprovação, ajuste.

**Relatórios:** posição de estoque, kardex (extrato por produto), curva ABC, produtos parados, valor total em estoque.

### 4.3 Financeiro

**Conceito central:** **títulos** (contas a pagar e a receber) com **parcelas**, e **baixas** (pagamentos/recebimentos, podendo ser parciais).

- **Contas a receber:** geradas por vendas a prazo, cartão (o dinheiro cai depois), boleto, crediário.
- **Contas a pagar:** fornecedores (geradas da entrada de nota), despesas fixas (aluguel, luz), impostos, folha.
- **Contas financeiras:** caixa da loja, conta bancária, carteira digital. Toda baixa movimenta uma conta financeira.
- **Plano de contas:** categorização hierárquica (Receitas > Vendas; Despesas > Ocupação > Aluguel). Base do DRE.
- **Centro de custo:** opcional (ex.: por loja).
- **Juros, multa e desconto** na baixa.
- **Recorrência:** despesas fixas geradas automaticamente todo mês.
- **Conciliação bancária:** importar extrato (OFX) e casar com os lançamentos. Pós-MVP.

**Regime de caixa × competência** (precisa entender bem):
- **Caixa:** conta quando o dinheiro entra/sai → base do **Fluxo de Caixa**.
- **Competência:** conta quando o fato acontece (venda feita hoje, mesmo recebendo em 30 dias) → base do **DRE**.
- O sistema precisa guardar **as duas datas** (data de competência e data de vencimento/pagamento).

**Cartão de crédito/débito (detalhe que pega muita gente):**
- Venda de R$ 100 no crédito 3x não é R$ 100 no caixa hoje: é um recebível de 3 parcelas, **menos a taxa (MDR)** da maquininha, com prazo (ex.: D+30, D+60, D+90) — ou antecipado com outra taxa.
- Cadastro de **formas de pagamento** com taxa e prazo por bandeira/parcelas, para projetar o fluxo de caixa corretamente.
- PIX e dinheiro: entram na hora (PIX pode ter taxa dependendo do banco).

**Relatórios:** fluxo de caixa (previsto × realizado), DRE simplificado, contas vencidas/a vencer, inadimplência.

### 4.4 PDV (Ponto de Venda)

Fluxo de um dia típico de caixa:

1. **Abertura de caixa:** operador informa o **fundo de troco** (valor inicial em dinheiro).
2. **Venda:**
   - Adicionar itens (leitor de código de barras, busca por nome, quantidade, balança para itens pesados).
   - Identificar cliente (opcional — CPF na nota).
   - Descontos por item ou no total (limite por perfil; acima disso pede senha do gerente).
   - **Cancelamento de item** (com permissão e registro de auditoria).
   - **Pagamento:** uma ou várias formas na mesma venda (ex.: R$ 50 no dinheiro + resto no cartão), cálculo de troco.
   - **Emissão do cupom fiscal (NFC-e)** — ver 4.5.
3. **Sangria:** retirada de dinheiro do caixa durante o dia (segurança).
4. **Suprimento:** reforço de troco.
5. **Fechamento de caixa:** operador conta e informa os valores por forma de pagamento. Idealmente **fechamento cego** (o operador não vê o valor esperado antes de informar). Sistema mostra a diferença (sobra/falta).
6. **Cancelamento de venda / devolução / troca:** com regras de prazo e permissão; cancelamento fiscal tem prazo legal curto após a emissão.

**Integrações do PDV:**
- **Pagamento:** TEF ou POS integrado (Stone, Cielo, Rede, PagSeguro, Mercado Pago...) e **PIX com QR Code dinâmico** (confirmação automática).
- **Hardware:** leitor de código de barras (age como teclado — fácil), impressora térmica de cupom, balança, gaveta.

**Ao finalizar uma venda, o sistema deve (efeitos colaterais):**
- Baixar o estoque (movimentação de saída).
- Lançar no caixa (dinheiro/PIX) ou gerar recebível (cartão/crediário).
- Emitir o documento fiscal.

**[ABERTO] Ponto crítico — PDV offline:** loja não pode parar de vender se a internet cair. Opções:
- **A)** PDV 100% web (online obrigatório) — mais simples, mas arriscado.
- **B)** PDV web com fila local (IndexedDB/Service Worker) e sincronização posterior + NFC-e em contingência.
- **C)** PDV como app desktop (Electron/Tauri) com banco local.

Sugestão: MVP com **A**, já desenhando os comandos de venda para serem **idempotentes** (ID gerado no cliente), o que facilita evoluir para **B**.

### 4.5 Fiscal (Brasil) — atenção especial

É a parte mais complexa e a que mais muda. Itens envolvidos:
- **NFC-e** (cupom fiscal eletrônico, venda ao consumidor no PDV) e **NF-e** (venda para empresa, entrada de mercadoria).
- Dados fiscais por produto: **NCM, CEST, CFOP, CST/CSOSN**, alíquotas.
- **Regime tributário** da empresa: Simples Nacional, Lucro Presumido, Lucro Real.
- **Certificado digital A1** da empresa.
- **Contingência** quando a SEFAZ está fora.
- **Reforma tributária (IBS/CBS):** transição começando em 2026 e indo até 2033. O sistema precisa nascer preparado para conviver com os dois modelos.

**Sugestão forte:** **não implementar a comunicação com a SEFAZ do zero.** Usar uma API fiscal terceirizada (ex.: Focus NFe, PlugNotas, NFE.io, Nuvem Fiscal) atrás de uma interface própria (`IEmissorFiscal`), para poder trocar de fornecedor. Validar regras tributárias com um contador.

**[ABERTO]** Fiscal entra no MVP? Sem NFC-e, o PDV não pode ser usado "de verdade" pela maioria dos varejistas.

### 4.6 Dashboard / Mobile (somente leitura)

O que o dono quer ver no celular:
- **Vendas do dia** (total, quantidade, ticket médio) — comparação com ontem/semana passada.
- Vendas por loja, por vendedor, por forma de pagamento.
- **Caixas abertos** e status.
- **Saldo financeiro** atual, contas a pagar/receber de hoje e da semana, vencidas.
- **Alertas:** estoque baixo, produtos sem giro, diferenças de caixa, cancelamentos acima do normal.
- Produtos mais vendidos.

**Técnico:**
- Endpoints de leitura dedicados (`GET`), com **read models** otimizados (consultas agregadas / views materializadas), separados do modelo de escrita (CQRS leve).
- **"Tempo real":** começar com refresh ao abrir/puxar a tela + polling curto; evoluir para **SignalR** (WebSocket) se fizer diferença.
- **Push notifications** (Expo Notifications) para alertas importantes.
- Mesmo backend e mesma autenticação da web; perfil "Dono" com acesso de leitura a tudo.

---

## 5. Fluxo integrado

```mermaid
flowchart LR
    Compra[Entrada NF-e fornecedor] -->|+ estoque, custo médio| Estoque
    Compra -->|gera conta a pagar| Financeiro
    PDV[Venda no PDV] -->|- estoque| Estoque
    PDV -->|caixa ou recebível| Financeiro
    PDV -->|NFC-e| Fiscal[API Fiscal / SEFAZ]
    Estoque --> Dashboard
    Financeiro --> Dashboard
    PDV --> Dashboard
    Dashboard --> Web
    Dashboard --> Mobile[App mobile - leitura]
```

---

## 6. Qualidade — TDD first

**Ciclo:** Red → Green → Refactor. Nenhum código de produção sem um teste que falhe antes.

| Nível | Backend (.NET) | Frontend (React) |
|---|---|---|
| Unitário | xUnit + Shouldly/AwesomeAssertions + NSubstitute | Vitest + Testing Library |
| Integração | WebApplicationFactory + **Testcontainers** (PostgreSQL real em container) | MSW (mock de API) |
| Arquitetura | NetArchTest (garante fronteiras entre módulos e camadas) | — |
| E2E | — | Playwright (fluxos críticos: venda no PDV, entrada de nota) |

- Domínio puro (regras de estoque, custo médio, cálculo de troco, parcelas) é o lugar ideal para TDD — sem banco, sem HTTP.
- Testes de integração com banco **real** (não in-memory), para pegar problemas de SQL, RLS e migrations.
- Cobertura como indicador, não como meta.

> Nota: FluentAssertions passou a ter licença comercial a partir da v8; por isso a sugestão de Shouldly ou AwesomeAssertions (fork aberto).

---

## 7. GitHub e CI/CD

**Fluxo de trabalho:**
- Branch `main` protegida: merge só via PR, com CI verde.
- Branches curtas por feature (`feat/...`, `fix/...`).
- **Conventional Commits** (`feat:`, `fix:`, `test:`, `refactor:`...) — permite changelog automático.
- Templates de PR e issue; Dependabot para dependências.

**Workflows (GitHub Actions):**

| Workflow | Gatilho | Passos |
|---|---|---|
| `backend-ci` | PR / push em `main` (paths `src/backend/**`) | restore, build, format check, testes (com Testcontainers) |
| `web-ci` | PR / push (paths `src/web/**`) | install, lint, typecheck, testes, build |
| `mobile-ci` | PR / push (paths `src/mobile/**`) | lint, typecheck, testes |
| `release` | push em `main` / tag | build das imagens Docker → push no GHCR |
| `deploy` | manual ou tag | SSH na VPS → `docker compose pull && up -d` → migrations |

---

## 8. Infraestrutura

**Local (Docker Compose):** `api`, `web`, `postgres`, `pgadmin` (opcional), volumes para dados.

**Produção (VPS):**
- Docker Compose + **reverse proxy** com HTTPS automático (Caddy ou Traefik).
- Backups automáticos do PostgreSQL (pg_dump diário para storage externo, ex.: S3) — **testar restauração**.
- Logs e monitoramento: Serilog + Seq/Grafana Loki; health checks; uptime monitor.
- Segredos via variáveis de ambiente / GitHub Secrets — nunca no repositório.
- LGPD: dados pessoais de clientes (CPF, telefone) — controle de acesso, logs de auditoria, política de retenção.

---

## 9. Estrutura proposta do repositório (monorepo)

```
kamusErp/
├── .github/
│   ├── workflows/
│   └── pull_request_template.md
├── docs/
│   └── adr/                    # registros de decisões de arquitetura
├── src/
│   ├── backend/
│   │   ├── Kamus.Api/
│   │   ├── Modules/
│   │   │   ├── Cadastros/
│   │   │   ├── Estoque/
│   │   │   ├── Financeiro/
│   │   │   ├── Vendas/         # PDV
│   │   │   └── Identidade/
│   │   └── Shared/             # kernel compartilhado (Result, tenant, eventos)
│   ├── web/                    # React + TS + Tailwind (Vite)
│   └── mobile/                 # React Native (Expo)
├── tests/
│   ├── backend/                # unit, integration, architecture
│   └── e2e/                    # Playwright
├── docker-compose.yml
├── LEVANTAMENTO.md
└── README.md
```

---

## 10. Roadmap sugerido (a validar)

| Fase | Entrega |
|---|---|
| **0 — Fundação** | Repositório, solução .NET, Docker Compose, CI, auth, multi-tenancy, primeiro teste verde |
| **1 — Cadastros** | Empresa/loja, usuários e perfis, produtos, clientes/fornecedores |
| **2 — Estoque** | Movimentações, saldo, custo médio, ajustes, kardex, alertas de mínimo |
| **3 — PDV** | Abertura/fechamento de caixa, venda, pagamentos múltiplos, sangria/suprimento (sem fiscal) |
| **4 — Financeiro** | Contas a pagar/receber, baixas, contas financeiras, formas de pagamento com taxa, fluxo de caixa |
| **5 — Dashboard + Mobile** | Read models, KPIs na web, app mobile somente leitura |
| **6 — Fiscal** | NFC-e/NF-e via API terceira, importação de XML de entrada |
| **7 — Produção** | Deploy na VPS, backups, monitoramento, primeiro cliente piloto |

---

## 11. Perguntas em aberto (para a próxima conversa)

1. **Segmento-alvo:** qual tipo de loja? (roupas, mercado, materiais de construção, farmácia, autopeças...) Isso decide grade, lote/validade, balança, etc.
2. **Tamanho do cliente:** loja única ou redes com várias filiais?
3. **Fiscal no MVP:** sim ou não? Tem contato com um contador para validar regras?
4. **PDV offline:** aceita PDV online-only no começo?
5. **Pagamentos:** qual adquirente/maquininha integrar primeiro? PIX direto com banco ou via intermediador?
6. **Vendas fora do PDV:** haverá pedidos/orçamentos (venda balcão com entrega, venda para empresas)?
7. **Compras:** precisa de pedido de compra ao fornecedor, ou só a entrada da nota?
8. **Modelo de cobrança do SaaS:** por loja, por usuário, por plano/módulos? (afeta a modelagem de tenant e permissões)
9. **Idioma/moeda:** só Brasil (pt-BR, BRL) ou pensar em internacionalização?
10. **Autenticação:** própria ou provedor externo?

---

## 12. Glossário

| Termo | Significado |
|---|---|
| **SKU** | Unidade de controle de estoque (produto + variação) |
| **Kardex** | Extrato de movimentações de um produto |
| **CMV** | Custo da Mercadoria Vendida |
| **Curva ABC** | Classificação de produtos pela relevância no faturamento |
| **MDR** | Taxa cobrada pela adquirente sobre vendas no cartão |
| **D+N** | Prazo em dias para o dinheiro do cartão cair na conta |
| **Sangria / Suprimento** | Retirada / reforço de dinheiro no caixa |
| **Fechamento cego** | Operador informa o valor contado sem ver o esperado |
| **DRE** | Demonstração do Resultado do Exercício (lucro/prejuízo por competência) |
| **NF-e / NFC-e** | Nota fiscal eletrônica / Nota fiscal de consumidor eletrônica |
| **NCM / CFOP / CST / CSOSN** | Códigos fiscais de classificação do produto e da operação |
| **SEFAZ** | Secretaria da Fazenda estadual (autoriza as notas) |
| **TEF** | Transferência Eletrônica de Fundos (cartão integrado ao PDV) |
| **IBS / CBS** | Novos tributos da reforma tributária |
| **Tenant** | Cliente do SaaS (cada empresa assinante) |
| **RLS** | Row Level Security (isolamento por linha no PostgreSQL) |

---

## 13. Registro de decisões

| Data | Decisão | Status |
|---|---|---|
| 2026-10-13 | Stack: .NET + PostgreSQL + Docker Compose + React/TS/Tailwind + React Native | Aceita |
| 2026-10-13 | TDD first em todo o projeto | Aceita |
| 2026-10-13 | CI/CD com GitHub Actions | Aceita |
| 2026-10-13 | Mobile somente leitura na primeira versão | Aceita |
| 2026-10-13 | Monólito modular | Proposta |
| 2026-10-13 | Multi-tenant com `tenant_id` + Global Query Filter + RLS | Proposta |
| 2026-10-13 | Monorepo | Proposta |
| 2026-10-13 | Fiscal via API terceirizada atrás de interface própria | Proposta |
