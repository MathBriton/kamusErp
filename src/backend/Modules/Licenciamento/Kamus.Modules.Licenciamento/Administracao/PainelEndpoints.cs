using System.Security.Claims;
using System.Security.Cryptography;
using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Dominio;
using Kamus.Modules.Licenciamento.Persistencia;
using Kamus.SharedKernel.Clientes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Kamus.Modules.Licenciamento.Administracao;

/// <summary>API do Painel Administrativo (operador do SaaS). Autenticação própria; nada aqui é visível ao cliente.</summary>
internal static class PainelEndpoints
{
    public const string PoliticaDeLimiteDoLogin = "admin-login";

    public sealed record LoginRequest(string? Email, string? Senha);
    public sealed record ResumoDto(int Ativas, int Trial, int EmCarencia, int Suspensas, int Canceladas, decimal ReceitaMensal);
    public sealed record ClienteDto(
        Guid TenantId, string Nome, string? EmailDoDono, int Usuarios, DateTimeOffset CriadoEm,
        string Plano, string Estado, DateOnly? Vencimento, DateOnly? FimDaCarencia, int? DiasParaVencer, decimal ValorMensal,
        int DiasDeCarencia, int? LimiteDeCnpjs, int? LimiteDeLojas, int? LimiteDeUsuarios, DateOnly? UltimoPagamentoEm);
    public sealed record PagamentoDto(Guid Id, decimal Valor, int Meses, DateOnly PagoEm, DateOnly EstendeuAte, string? Forma, string? Observacao, string RegistradoPor, DateTimeOffset RegistradoEm);
    public sealed record AcaoDto(Guid Id, DateTimeOffset OcorridoEm, string Acao, string Descricao, string AdministradorEmail, Guid? TenantId, string? Ip);
    public sealed record DetalheDto(ClienteDto Cliente, IReadOnlyList<PagamentoDto> Pagamentos, IReadOnlyList<AcaoDto> Acoes);
    public sealed record NovoClienteRequest(string? Nome, string? RazaoSocial, string? NomeFantasia, string? Cnpj, string? NomeDoDono, string? EmailDoDono, decimal? ValorMensal, int? DiasDeTrial);
    public sealed record NovoClienteResponse(Guid TenantId, string EmailDoDono, string SenhaProvisoria);
    public sealed record PagamentoRequest(decimal Valor, int? Meses, DateOnly? PagoEm, string? Forma, string? Observacao);
    public sealed record LicencaRequest(decimal ValorMensal, int? DiasDeCarencia, int? LimiteDeCnpjs, int? LimiteDeLojas, int? LimiteDeUsuarios);

    public static IEndpointRouteBuilder MapPainelAdministrativo(this IEndpointRouteBuilder app)
    {
        var raiz = app.MapGroup(AdminOptions.Prefixo).WithTags("Painel Administrativo");
        raiz.MapPost("/login", Entrar).AllowAnonymous().RequireRateLimiting(PoliticaDeLimiteDoLogin)
            .WithSummary("Login do SuperAdmin (token próprio, não vale na API do cliente)");

        var grupo = raiz.MapGroup("").RequireAuthorization(AdminOptions.Politica);
        grupo.MapGet("/resumo", Resumo).WithSummary("Quantidade de clientes por situação e receita mensal");
        grupo.MapGet("/clientes", Clientes).WithSummary("Clientes com a situação da licença");
        grupo.MapGet("/clientes/{tenantId:guid}", Detalhe).WithSummary("Cliente, pagamentos e ações administrativas");
        grupo.MapPost("/clientes", NovoCliente).WithSummary("Cadastra cliente (empresa + Dono) com licença em avaliação");
        grupo.MapPost("/clientes/{tenantId:guid}/pagamentos", RegistrarPagamento).WithSummary("Registra mensalidade: libera na hora");
        grupo.MapPut("/clientes/{tenantId:guid}/licenca", AlterarLicenca).WithSummary("Valor, carência e limites");
        grupo.MapPost("/clientes/{tenantId:guid}/cancelar", Cancelar).WithSummary("Cancela a licença (bloqueia o cliente)");
        grupo.MapPost("/clientes/{tenantId:guid}/reativar", Reativar).WithSummary("Reativa uma licença cancelada");
        grupo.MapGet("/acoes", Acoes).WithSummary("Últimas ações administrativas");
        return app;
    }

    private static IResult Problema(int status, string titulo, string detalhe) => Results.Problem(statusCode: status, title: titulo, detail: detalhe);
    private static IResult NaoEncontrado() => Problema(StatusCodes.Status404NotFound, "Não encontrado", "Cliente sem licença cadastrada.");
    private static string? Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString();

    private static async Task<IResult> Entrar(LoginRequest request, AutenticacaoAdmin auth, HttpContext http, CancellationToken ct)
    {
        var (sessao, erro) = await auth.EntrarAsync(request.Email, request.Senha, Ip(http), ct);
        return sessao is null ? Problema(StatusCodes.Status401Unauthorized, "Acesso negado", erro!) : Results.Ok(sessao);
    }

    private static async Task<IResult> Resumo(LicenciamentoDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var licencas = await db.Licencas.AsNoTracking().ToListAsync(ct);
        var porEstado = licencas.GroupBy(l => l.EstadoEm(hoje)).ToDictionary(g => g.Key, g => g.ToList());
        int Contar(EstadoLicenca e) => porEstado.TryGetValue(e, out var lista) ? lista.Count : 0;
        var receita = licencas.Where(l => l.EstadoEm(hoje) is EstadoLicenca.Ativa or EstadoLicenca.Carencia).Sum(l => l.ValorMensal);
        return Results.Ok(new ResumoDto(Contar(EstadoLicenca.Ativa), Contar(EstadoLicenca.Trial), Contar(EstadoLicenca.Carencia),
            Contar(EstadoLicenca.Suspensa), Contar(EstadoLicenca.Cancelada), receita));
    }

    private static async Task<IReadOnlyList<ClienteDto>> ListarAsync(LicenciamentoDbContext db, IDiretorioDeClientes diretorio, DateOnly hoje, Guid? somente, CancellationToken ct)
    {
        var clientes = (await diretorio.ListarAsync(ct)).Where(c => somente is null || c.TenantId == somente).ToList();
        var licencas = await db.Licencas.AsNoTracking().ToDictionaryAsync(l => l.TenantId, ct);
        var ultimos = await db.Pagamentos.AsNoTracking().GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Ultimo = g.Max(p => p.PagoEm) }).ToDictionaryAsync(x => x.TenantId, x => x.Ultimo, ct);

        return clientes.Select(c =>
        {
            licencas.TryGetValue(c.TenantId, out var l);
            DateOnly? ultimo = ultimos.TryGetValue(c.TenantId, out var u) ? u : null;
            if (l is null)
                return new ClienteDto(c.TenantId, c.Nome, c.EmailDoDono, c.Usuarios, c.CriadoEm, "—", "SemLicenca", null, null, null, 0, 0, null, null, null, ultimo);
            var s = SituacaoDaLicenca.De(l, hoje);
            return new ClienteDto(c.TenantId, c.Nome, c.EmailDoDono, c.Usuarios, c.CriadoEm, l.Plano, s.Estado, s.Vencimento, s.FimDaCarencia,
                s.DiasParaVencer, l.ValorMensal, l.DiasDeCarencia, l.LimiteDeCnpjs, l.LimiteDeLojas, l.LimiteDeUsuarios, ultimo);
        }).ToList();
    }

    private static async Task<IResult> Clientes(LicenciamentoDbContext db, IDiretorioDeClientes diretorio, TimeProvider relogio, CancellationToken ct) =>
        Results.Ok(await ListarAsync(db, diretorio, CalendarioBrasilia.Hoje(relogio), null, ct));

    private static async Task<IResult> Detalhe(Guid tenantId, LicenciamentoDbContext db, IDiretorioDeClientes diretorio, TimeProvider relogio, CancellationToken ct)
    {
        var cliente = (await ListarAsync(db, diretorio, CalendarioBrasilia.Hoje(relogio), tenantId, ct)).SingleOrDefault();
        if (cliente is null) return Problema(StatusCodes.Status404NotFound, "Não encontrado", "Cliente não encontrado.");

        var pagamentos = await db.Pagamentos.AsNoTracking().Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.PagoEm).ThenByDescending(p => p.RegistradoEm)
            .Select(p => new PagamentoDto(p.Id, p.Valor, p.Meses, p.PagoEm, p.EstendeuAte, p.Forma, p.Observacao, p.RegistradoPor, p.RegistradoEm))
            .ToListAsync(ct);
        var acoes = await ConsultarAcoes(db, tenantId, 50, ct);
        return Results.Ok(new DetalheDto(cliente, pagamentos, acoes));
    }

    private static Task<List<AcaoDto>> ConsultarAcoes(LicenciamentoDbContext db, Guid? tenantId, int limite, CancellationToken ct) =>
        db.AcoesAdministrativas.AsNoTracking()
            .Where(a => tenantId == null || a.TenantId == tenantId)
            .OrderByDescending(a => a.OcorridoEm).Take(Math.Clamp(limite, 1, 200))
            .Select(a => new AcaoDto(a.Id, a.OcorridoEm, a.Acao, a.Descricao, a.AdministradorEmail, a.TenantId, a.Ip))
            .ToListAsync(ct);

    private static async Task<IResult> Acoes(LicenciamentoDbContext db, CancellationToken ct, Guid? tenantId = null, int limite = 50) =>
        Results.Ok(await ConsultarAcoes(db, tenantId, limite, ct));

    private static void Registrar(LicenciamentoDbContext db, ClaimsPrincipal user, HttpContext http, string acao, Guid? tenantId, string descricao, TimeProvider relogio)
    {
        Guid? id = Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var g) ? g : null;
        var email = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? "?";
        db.AcoesAdministrativas.Add(AcaoAdministrativa.Criar(id, email, acao, tenantId, descricao, Ip(http), relogio.GetUtcNow()));
    }

    private static async Task<IResult> NovoCliente(
        NovoClienteRequest r, ICadastroDeClientes cadastro, LicenciamentoDbContext db, TimeProvider relogio, ClaimsPrincipal user, HttpContext http, CancellationToken ct)
    {
        var valor = r.ValorMensal ?? 199.90m;
        if (valor < 0) return Problema(StatusCodes.Status400BadRequest, "Dados inválidos", "O valor mensal não pode ser negativo.");
        var dias = r.DiasDeTrial ?? Licenca.DiasDeTrialPadrao;
        if (dias is < 0 or > 90) return Problema(StatusCodes.Status400BadRequest, "Dados inválidos", "O período de avaliação deve ter de 0 a 90 dias.");

        var senha = GerarSenhaProvisoria();
        var resultado = await cadastro.CriarAsync(new NovoCliente(r.Nome ?? "", r.RazaoSocial ?? "", r.NomeFantasia ?? "", r.Cnpj ?? "",
            r.NomeDoDono ?? "", r.EmailDoDono ?? "", senha), ct);
        if (resultado.TenantId is not { } tenantId)
            return Problema(resultado.Conflito ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest, "Cadastro recusado", resultado.Erro!);

        var hoje = CalendarioBrasilia.Hoje(relogio);
        db.Licencas.Add(Licenca.CriarTrial(tenantId, hoje, relogio.GetUtcNow(), valor, dias));
        Registrar(db, user, http, "cliente_criado", tenantId, $"Cliente {r.Nome!.Trim()} criado (dono {r.EmailDoDono!.Trim()}), avaliação de {dias} dias", relogio);
        await db.SaveChangesAsync(ct);
        return Results.Created($"{AdminOptions.Prefixo}/clientes/{tenantId}", new NovoClienteResponse(tenantId, r.EmailDoDono.Trim().ToLowerInvariant(), senha));
    }

    /// <summary>12 caracteres sem ambiguidade (sem 0/O, 1/l/I), com maiúscula, minúscula e número.</summary>
    private static string GerarSenhaProvisoria()
    {
        const string maiusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ", minusculas = "abcdefghijkmnopqrstuvwxyz", numeros = "23456789";
        var corpo = RandomNumberGenerator.GetItems<char>(maiusculas + minusculas + numeros, 9);
        return new string([.. corpo, RandomNumberGenerator.GetItems<char>(maiusculas, 1)[0],
            RandomNumberGenerator.GetItems<char>(minusculas, 1)[0], RandomNumberGenerator.GetItems<char>(numeros, 1)[0]]);
    }

    private static async Task<IResult> RegistrarPagamento(
        Guid tenantId, PagamentoRequest r, LicenciamentoDbContext db, CacheDeLicencas cache, AvisoAosDonos avisos, TimeProvider relogio,
        ClaimsPrincipal user, HttpContext http, ILoggerFactory logs, CancellationToken ct)
    {
        var licenca = await db.Licencas.SingleOrDefaultAsync(l => l.TenantId == tenantId, ct);
        if (licenca is null) return NaoEncontrado();

        var hoje = CalendarioBrasilia.Hoje(relogio);
        var email = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? "?";
        Pagamento pagamento;
        try
        {
            pagamento = licenca.Pagar(r.Valor, r.Meses ?? 1, r.PagoEm ?? hoje, r.Forma, r.Observacao, email, relogio.GetUtcNow());
        }
        catch (ArgumentOutOfRangeException)
        {
            return Problema(StatusCodes.Status400BadRequest, "Dados inválidos", "Informe um valor maior que zero e de 1 a 12 meses.");
        }
        catch (InvalidOperationException ex)
        {
            return Problema(StatusCodes.Status409Conflict, "Licença cancelada", ex.Message);
        }

        db.Pagamentos.Add(pagamento);
        Registrar(db, user, http, "pagamento_registrado", tenantId,
            $"Pagamento de {SituacaoDaLicenca.Moeda(r.Valor)} ({pagamento.Meses} {(pagamento.Meses == 1 ? "mês" : "meses")}) — em dia até {SituacaoDaLicenca.Data(pagamento.EstendeuAte)}", relogio);
        await db.SaveChangesAsync(ct);
        cache.Invalidar(tenantId);

        try
        {
            await avisos.EnviarAsync(tenantId, "licenca_paga", "Pagamento confirmado",
                $"Obrigado! Sua licença está em dia até {SituacaoDaLicenca.Data(pagamento.EstendeuAte)}.", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logs.CreateLogger("Kamus.Licenciamento").FalhaAoAvisar(ex, tenantId);
        }

        return Results.Ok(SituacaoDaLicenca.De(licenca, hoje));
    }

    private static async Task<IResult> AlterarLicenca(
        Guid tenantId, LicencaRequest r, LicenciamentoDbContext db, CacheDeLicencas cache, TimeProvider relogio, ClaimsPrincipal user, HttpContext http, CancellationToken ct)
    {
        var licenca = await db.Licencas.SingleOrDefaultAsync(l => l.TenantId == tenantId, ct);
        if (licenca is null) return NaoEncontrado();
        try
        {
            licenca.DefinirValorMensal(r.ValorMensal);
            if (r.DiasDeCarencia is { } dias) licenca.DefinirCarencia(dias);
            licenca.DefinirLimites(r.LimiteDeCnpjs, r.LimiteDeLojas, r.LimiteDeUsuarios);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Problema(StatusCodes.Status400BadRequest, "Dados inválidos", "Valor não pode ser negativo, carência vai de 0 a 60 dias e limites devem ser maiores que zero.");
        }

        Registrar(db, user, http, "licenca_alterada", tenantId,
            $"Valor {SituacaoDaLicenca.Moeda(r.ValorMensal)}, carência {licenca.DiasDeCarencia} dias, limites CNPJs {Limite(r.LimiteDeCnpjs)}, " +
            $"lojas {Limite(r.LimiteDeLojas)}, usuários {Limite(r.LimiteDeUsuarios)}", relogio);
        await db.SaveChangesAsync(ct);
        cache.Invalidar(tenantId);
        return Results.NoContent();
    }

    private static string Limite(int? valor) => valor?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";

    private static Task<IResult> Cancelar(Guid tenantId, LicenciamentoDbContext db, CacheDeLicencas cache, TimeProvider relogio, ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
        MudarAsync(tenantId, db, cache, relogio, user, http, "licenca_cancelada", "Licença cancelada", l => l.Cancelar(relogio.GetUtcNow()), ct);

    private static Task<IResult> Reativar(Guid tenantId, LicenciamentoDbContext db, CacheDeLicencas cache, TimeProvider relogio, ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
        MudarAsync(tenantId, db, cache, relogio, user, http, "licenca_reativada", "Licença reativada", l => l.Reativar(), ct);

    private static async Task<IResult> MudarAsync(Guid tenantId, LicenciamentoDbContext db, CacheDeLicencas cache, TimeProvider relogio,
        ClaimsPrincipal user, HttpContext http, string acao, string descricao, Action<Licenca> mudar, CancellationToken ct)
    {
        var licenca = await db.Licencas.SingleOrDefaultAsync(l => l.TenantId == tenantId, ct);
        if (licenca is null) return NaoEncontrado();
        mudar(licenca);
        Registrar(db, user, http, acao, tenantId, descricao, relogio);
        await db.SaveChangesAsync(ct);
        cache.Invalidar(tenantId);
        return Results.NoContent();
    }
}
