## O que muda

<!-- Resumo da mudança e motivação -->

## Como foi testado

- [ ] Testes escritos antes da implementação (TDD)
- [ ] `dotnet test --solution Kamus.slnx` verde
- [ ] `npm test` / `npm run lint` / `npm run typecheck` verdes (se mexeu no web)

## Checklist

- [ ] Entidades de negócio novas implementam `ITenantOwned` e a migration habilita RLS
- [ ] Componentes usam apenas tokens de cor (funcionam no claro e no escuro)
- [ ] `LEVANTAMENTO.md` atualizado se alguma decisão mudou
