using Kamus.Modules.Cadastros.Dominio;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Modules.Cadastros.Endpoints;

/// <summary>Clientes e fornecedores (<c>/api/pessoas</c>): a mesma ficha, com papéis.</summary>
internal static class PessoasEndpoints
{
    public sealed record PessoaRequest(
        string? Tipo, string? Nome, string? NomeFantasia, string? Documento, bool EhCliente, bool EhFornecedor,
        string? Telefone, string? Email, DateOnly? DataNascimento, Endereco? Endereco, PreferenciasDeModa? Preferencias,
        string? Observacoes, bool AceitaMarketing);

    public sealed record PessoaDto(
        Guid Id, string Tipo, string Nome, string? NomeFantasia, string? Documento, bool EhCliente, bool EhFornecedor,
        string? Telefone, string? WhatsApp, string? Email, DateOnly? DataNascimento, Endereco Endereco, PreferenciasDeModa Preferencias,
        string? Observacoes, bool AceitaMarketing, DateTimeOffset? ConsentimentoMarketingEm, bool Ativo, DateTimeOffset CriadoEm, DateTimeOffset? AnonimizadoEm)
    {
        public static PessoaDto De(Pessoa p) => new(p.Id, p.Tipo.ToString(), p.Nome, p.NomeFantasia, p.DocumentoFormatado, p.EhCliente, p.EhFornecedor,
            p.Telefone, p.WhatsApp, p.Email, p.DataNascimento, p.Endereco, p.Preferencias, p.Observacoes,
            p.ConsentimentoMarketingEm is not null, p.ConsentimentoMarketingEm, p.Ativo, p.CriadoEm, p.AnonimizadoEm);
    }

    public sealed record PaginaDto(IReadOnlyList<PessoaDto> Itens, int Total, int Pagina, int Tamanho);
    public sealed record AniversarianteDto(Guid Id, string Nome, DateOnly DataNascimento, int Idade, string? WhatsApp, bool AceitaMarketing);

    public static IEndpointRouteBuilder MapPessoas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/pessoas").RequireAuthorization().WithTags("Clientes e fornecedores");
        g.MapGet("", Listar).WithSummary("Clientes e/ou fornecedores (papel=cliente|fornecedor, busca sem acento, situacao=ativos|inativos|todos)")
            .Produces<PaginaDto>();
        g.MapGet("/aniversariantes", Aniversariantes).WithSummary("Clientes ativos que fazem aniversário (periodo=hoje|semana|mes)")
            .Produces<IReadOnlyList<AniversarianteDto>>();
        g.MapGet("/{id:guid}", Obter).WithSummary("Ficha completa").Produces<PessoaDto>();
        g.MapPost("", Criar).WithSummary("Cadastrar cliente/fornecedor").Produces<PessoaDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        g.MapPut("/{id:guid}", Editar).WithSummary("Alterar a ficha").Produces<PessoaDto>();
        g.MapPost("/{id:guid}/inativar", Inativar).WithSummary("Inativar (Dono/Gerente)").Produces(StatusCodes.Status204NoContent);
        g.MapPost("/{id:guid}/reativar", Reativar).WithSummary("Reativar (Dono/Gerente)").Produces(StatusCodes.Status204NoContent);
        g.MapPost("/{id:guid}/anonimizar", Anonimizar).WithSummary("LGPD: apagar dados pessoais mantendo o registro (Dono)")
            .Produces(StatusCodes.Status204NoContent);
        return app;
    }

    private static IResult Invalido(string detalhe) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Dados inválidos", detail: detalhe);
    private static IResult NaoEncontrada() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Não encontrado", detail: "Cadastro não encontrado.");

    /// <summary>Busca sem acento por nome, fantasia e e-mail; por dígitos no documento e no telefone.</summary>
    internal static bool Corresponde(Pessoa p, string termo) =>
        TextoParaBusca.Contem(p.Nome, termo) || TextoParaBusca.Contem(p.NomeFantasia, termo) || TextoParaBusca.Contem(p.Email, termo)
        || TextoParaBusca.ContemDigitos(p.Documento, termo) || TextoParaBusca.ContemDigitos(p.Telefone, termo);

    /// <summary>Pessoas que o perfil pode ver, já filtradas por papel, situação e busca (volumes de loja: filtro em memória).</summary>
    internal static async Task<List<Pessoa>> FiltrarAsync(CadastrosDbContext db, string? perfil, string? papel, string? situacao, string? busca, CancellationToken ct)
    {
        var consulta = db.Pessoas.AsNoTracking();
        consulta = papel?.ToLowerInvariant() switch
        {
            "cliente" => consulta.Where(p => p.EhCliente),
            "fornecedor" => consulta.Where(p => p.EhFornecedor),
            _ => consulta,
        };
        consulta = situacao?.ToLowerInvariant() switch
        {
            "inativos" => consulta.Where(p => !p.Ativo),
            "todos" => consulta,
            _ => consulta.Where(p => p.Ativo),
        };
        var pessoas = (await consulta.ToListAsync(ct)).Where(p => Permissoes.PodeVer(perfil, p));
        if (!string.IsNullOrWhiteSpace(busca)) pessoas = pessoas.Where(p => Corresponde(p, busca));
        return pessoas.OrderBy(p => p.Nome, StringComparer.CurrentCulture).ToList();
    }

    private static async Task<IResult> Listar(IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct,
        string? papel = null, string? busca = null, string? situacao = null, int pagina = 1, int tamanho = 50)
    {
        if (papel?.ToLowerInvariant() == "cliente" && !Permissoes.PodeClientes(empresa.Perfil)) return Permissoes.Negado();
        if (papel?.ToLowerInvariant() == "fornecedor" && !Permissoes.PodeFornecedores(empresa.Perfil)) return Permissoes.Negado();

        var todas = await FiltrarAsync(db, empresa.Perfil, papel, situacao, busca, ct);
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 200);
        var itens = todas.Skip((pagina - 1) * tamanho).Take(tamanho).Select(PessoaDto.De).ToList();
        return Results.Ok(new PaginaDto(itens, todas.Count, pagina, tamanho));
    }

    private static async Task<IResult> Obter(Guid id, IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct)
    {
        var pessoa = await db.Pessoas.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (pessoa is null) return NaoEncontrada();
        return Permissoes.PodeVer(empresa.Perfil, pessoa) ? Results.Ok(PessoaDto.De(pessoa)) : Permissoes.Negado();
    }

    private static (DadosDaPessoa? Dados, string? Erro) Ler(PessoaRequest r)
    {
        if (!Enum.TryParse<TipoPessoa>(r.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo) || int.TryParse(r.Tipo, out _))
            return (null, "Tipo inválido: use Fisica ou Juridica.");
        return (new DadosDaPessoa
        {
            Tipo = tipo,
            Nome = r.Nome,
            NomeFantasia = r.NomeFantasia,
            Documento = r.Documento,
            EhCliente = r.EhCliente,
            EhFornecedor = r.EhFornecedor,
            Telefone = r.Telefone,
            Email = r.Email,
            DataNascimento = r.DataNascimento,
            Endereco = r.Endereco,
            Preferencias = r.Preferencias,
            Observacoes = r.Observacoes,
            AceitaMarketing = r.AceitaMarketing,
        }, null);
    }

    private static async Task<IResult?> DocumentoRepetidoAsync(CadastrosDbContext db, DadosDaPessoa dados, Guid? exceto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dados.Documento) || Pessoa.NormalizarDocumento(dados.Tipo, dados.Documento) is not { } documento) return null;
        var existente = await db.Pessoas.AsNoTracking().FirstOrDefaultAsync(p => p.Documento == documento && p.Id != exceto, ct);
        return existente is null ? null : Results.Problem(
            statusCode: StatusCodes.Status409Conflict, title: "Já cadastrado",
            detail: $"Já existe um cadastro com este {(dados.Tipo == TipoPessoa.Fisica ? "CPF" : "CNPJ")}: {existente.Nome}.",
            extensions: new Dictionary<string, object?> { ["existenteId"] = existente.Id });
    }

    private static async Task<IResult> Criar(PessoaRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var (dados, erro) = Ler(r);
        if (dados is null) return Invalido(erro!);
        if ((dados.EhCliente || dados.EhFornecedor) && !Permissoes.PodePapeis(empresa.Perfil, dados.EhCliente, dados.EhFornecedor)) return Permissoes.Negado();
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var erros = Pessoa.Validar(dados, hoje);
        if (erros.Count > 0) return Invalido(string.Join(" ", erros));
        if (await DocumentoRepetidoAsync(db, dados, null, ct) is { } repetido) return repetido;

        var pessoa = Pessoa.Criar(dados, relogio.GetUtcNow(), hoje);
        db.Pessoas.Add(pessoa);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/pessoas/{pessoa.Id}", PessoaDto.De(pessoa));
    }

    private static async Task<IResult> Editar(Guid id, PessoaRequest r, IEmpresaAtiva empresa, CadastrosDbContext db, TimeProvider relogio, CancellationToken ct)
    {
        var pessoa = await db.Pessoas.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (pessoa is null) return NaoEncontrada();
        var (dados, erro) = Ler(r);
        if (dados is null) return Invalido(erro!);
        if (!Permissoes.PodePapeis(empresa.Perfil, pessoa.EhCliente || dados.EhCliente, pessoa.EhFornecedor || dados.EhFornecedor)) return Permissoes.Negado();
        if (pessoa.AnonimizadoEm is not null)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Cadastro anonimizado", detail: "Cadastro anonimizado não pode ser alterado.");
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var erros = Pessoa.Validar(dados, hoje);
        if (erros.Count > 0) return Invalido(string.Join(" ", erros));
        if (await DocumentoRepetidoAsync(db, dados, id, ct) is { } repetido) return repetido;

        pessoa.Atualizar(dados, relogio.GetUtcNow(), hoje);
        await db.SaveChangesAsync(ct);
        return Results.Ok(PessoaDto.De(pessoa));
    }

    private static async Task<IResult> Mudar(Guid id, IEmpresaAtiva empresa, CadastrosDbContext db, string[] perfis, Action<Pessoa> acao, CancellationToken ct)
    {
        if (!perfis.Contains(empresa.Perfil)) return Permissoes.Negado();
        var pessoa = await db.Pessoas.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (pessoa is null) return NaoEncontrada();
        try
        {
            acao(pessoa);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Não permitido", detail: ex.Message);
        }
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static Task<IResult> Inativar(Guid id, IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct) =>
        Mudar(id, empresa, db, Permissoes.Gestao, p => p.Inativar(), ct);

    private static Task<IResult> Reativar(Guid id, IEmpresaAtiva empresa, CadastrosDbContext db, CancellationToken ct) =>
        Mudar(id, empresa, db, Permissoes.Gestao, p => p.Reativar(), ct);

    private static Task<IResult> Anonimizar(Guid id, IEmpresaAtiva empresa, CadastrosDbContext db, TimeProvider relogio, CancellationToken ct) =>
        Mudar(id, empresa, db, ["Dono"], p => p.Anonimizar(relogio.GetUtcNow()), ct);

    private static async Task<IResult> Aniversariantes(IEmpresaAtiva empresa, CadastrosDbContext db, TimeProvider relogio, CancellationToken ct, string? periodo = "semana")
    {
        if (!Permissoes.PodeClientes(empresa.Perfil)) return Permissoes.Negado();
        var hoje = CalendarioBrasilia.Hoje(relogio);
        var (inicio, fim) = periodo?.ToLowerInvariant() switch
        {
            "hoje" => (hoje, hoje),
            "mes" => (new DateOnly(hoje.Year, hoje.Month, 1), new DateOnly(hoje.Year, hoje.Month, DateTime.DaysInMonth(hoje.Year, hoje.Month))),
            _ => (hoje, hoje.AddDays(6)),
        };

        var clientes = await db.Pessoas.AsNoTracking().Where(p => p.EhCliente && p.Ativo && p.DataNascimento != null).ToListAsync(ct);
        return Results.Ok(clientes
            .Where(p => p.FazAniversarioEntre(inicio, fim))
            .Select(p => (Pessoa: p, Dia: ProximoAniversario(p.DataNascimento!.Value, inicio)))
            .OrderBy(x => x.Dia)
            .Select(x => new AniversarianteDto(x.Pessoa.Id, x.Pessoa.Nome, x.Pessoa.DataNascimento!.Value, x.Dia.Year - x.Pessoa.DataNascimento!.Value.Year,
                x.Pessoa.WhatsApp, x.Pessoa.ConsentimentoMarketingEm is not null))
            .ToList());
    }

    private static DateOnly ProximoAniversario(DateOnly nascimento, DateOnly aPartirDe)
    {
        for (var ano = aPartirDe.Year; ; ano++)
        {
            var dia = nascimento.Month == 2 && nascimento.Day == 29 && !DateTime.IsLeapYear(ano) ? new DateOnly(ano, 2, 28) : new DateOnly(ano, nascimento.Month, nascimento.Day);
            if (dia >= aPartirDe) return dia;
        }
    }
}
