namespace Kamus.Modules.Identidade.Aplicacao;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    public string Emissor { get; set; } = "kamus";
    public string Audiencia { get; set; } = "kamus";

    /// <summary>Chave HMAC com pelo menos 32 caracteres. Em produção vem de variável de ambiente/secret.</summary>
    public string Chave { get; set; } = "";

    public int MinutosDoTokenDeAcesso { get; set; } = 15;
    public int DiasDaSessaoLembrada { get; set; } = 30;
    public int HorasDaSessaoNaoLembrada { get; set; } = 12;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Chave) || Chave.Length < 32)
            throw new InvalidOperationException("Configure 'Jwt:Chave' com pelo menos 32 caracteres (variável de ambiente Jwt__Chave).");
    }
}
