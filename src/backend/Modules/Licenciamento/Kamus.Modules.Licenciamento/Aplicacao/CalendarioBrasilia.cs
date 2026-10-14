namespace Kamus.Modules.Licenciamento.Aplicacao;

/// <summary>
/// "Hoje" para vencimentos é o dia em Brasília (UTC−3, sem horário de verão desde 2019) — não o dia em UTC,
/// que vira às 21h. Fuso fixo para não depender do banco de fusos do sistema operacional da imagem.
/// </summary>
public static class CalendarioBrasilia
{
    public static readonly TimeSpan Fuso = TimeSpan.FromHours(-3);

    public static DateOnly Hoje(TimeProvider relogio) => DateOnly.FromDateTime(relogio.GetUtcNow().ToOffset(Fuso).DateTime);
}
