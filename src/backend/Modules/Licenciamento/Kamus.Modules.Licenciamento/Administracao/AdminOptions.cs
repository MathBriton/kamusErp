namespace Kamus.Modules.Licenciamento.Administracao;

/// <summary>
/// Configuração do Painel Administrativo (seção <c>Admin</c>). Sem <see cref="Chave"/> o painel não existe
/// (as rotas <c>/admin-api</c> respondem 404).
/// </summary>
public sealed class AdminOptions
{
    public const string Secao = "Admin";
    public const string Esquema = "Admin";
    public const string Politica = "SuperAdmin";
    public const string Prefixo = "/admin-api";

    /// <summary>Chave HMAC própria (≥ 32 caracteres), diferente da do cliente: um token não serve no outro.</summary>
    public string Chave { get; set; } = "";
    public string Emissor { get; set; } = "kamus-admin";
    public string Audiencia { get; set; } = "kamus-admin";
    public int HorasDoToken { get; set; } = 8;

    /// <summary>IPs separados por vírgula. Vazio = qualquer IP (desenvolvimento). Fora da lista, o painel responde 404.</summary>
    public string IpsPermitidos { get; set; } = "";

    /// <summary>Primeiro SuperAdmin, criado na subida se ainda não houver nenhum (fora de Development).</summary>
    public string EmailInicial { get; set; } = "";
    public string SenhaInicial { get; set; } = "";

    public bool Habilitado => !string.IsNullOrWhiteSpace(Chave);

    public void Validar()
    {
        if (Habilitado && Chave.Length < 32)
            throw new InvalidOperationException("'Admin:Chave' precisa ter pelo menos 32 caracteres (variável de ambiente Admin__Chave).");
    }

    public IReadOnlyList<string> ListaDeIps() =>
        IpsPermitidos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
