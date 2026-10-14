using System.Security.Cryptography;

namespace Kamus.Modules.Identidade.Dominio;

/// <summary>Regras mínimas de senha: 8+ caracteres, com letras e números.</summary>
public static class PoliticaDeSenha
{
    public const int TamanhoMinimo = 8;

    /// <returns>O motivo da recusa (pronto para exibir) ou <c>null</c> se a senha é aceitável.</returns>
    public static string? Validar(string? senha)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length < TamanhoMinimo)
            return $"A senha precisa ter pelo menos {TamanhoMinimo} caracteres.";
        if (!senha.Any(char.IsLetter) || !senha.Any(char.IsDigit))
            return "A senha precisa ter letras e números.";
        return null;
    }

    /// <summary>12 caracteres sem ambiguidade (sem 0/O, 1/l/I), com maiúscula, minúscula e número.</summary>
    public static string GerarProvisoria()
    {
        const string maiusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ", minusculas = "abcdefghijkmnopqrstuvwxyz", numeros = "23456789";
        var corpo = RandomNumberGenerator.GetItems<char>(maiusculas + minusculas + numeros, 9);
        char[] senha = [.. corpo, RandomNumberGenerator.GetItems<char>(maiusculas, 1)[0],
            RandomNumberGenerator.GetItems<char>(minusculas, 1)[0], RandomNumberGenerator.GetItems<char>(numeros, 1)[0]];
        RandomNumberGenerator.Shuffle(senha.AsSpan());
        return new string(senha);
    }
}
