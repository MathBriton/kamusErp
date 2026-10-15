using System.Globalization;
using System.Text;
using Kamus.SharedKernel.Documentos;

namespace Kamus.Infrastructure.Etiquetas;

/// <summary>
/// Desenha códigos de barras GTIN como SVG (barras pretas em fundo transparente): EAN-13 (e UPC-A, que é um EAN-13
/// com zero à esquerda), EAN-8 e ITF-14 (GTIN-14). Cada função de módulos devolve true = barra, false = espaço.
/// </summary>
public static class DesenhoDeCodigoDeBarras
{
    private static readonly string[] CodigoL = ["0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011"];
    private static readonly string[] Paridade = ["LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"];

    // ITF: cada dígito tem 5 elementos, 2 largos (1) e 3 estreitos (0).
    private static readonly string[] Itf5 = ["00110", "10001", "01001", "11000", "00101", "10100", "01100", "00011", "10010", "01010"];

    private static string CodigoR(int d) => string.Concat(CodigoL[d].Select(c => c == '0' ? '1' : '0'));

    private static string CodigoG(int d) => new(CodigoR(d).Reverse().ToArray());

    private static List<bool> Modulos(string bits) => bits.Select(c => c == '1').ToList();

    public static IReadOnlyList<bool> Ean13(string codigo)
    {
        Validar(codigo, 13);
        var sb = new StringBuilder("101");
        var paridade = Paridade[codigo[0] - '0'];
        for (var i = 1; i <= 6; i++)
            sb.Append(paridade[i - 1] == 'L' ? CodigoL[codigo[i] - '0'] : CodigoG(codigo[i] - '0'));
        sb.Append("01010");
        for (var i = 7; i <= 12; i++) sb.Append(CodigoR(codigo[i] - '0'));
        sb.Append("101");
        return Modulos(sb.ToString());
    }

    public static IReadOnlyList<bool> Ean8(string codigo)
    {
        Validar(codigo, 8);
        var sb = new StringBuilder("101");
        for (var i = 0; i < 4; i++) sb.Append(CodigoL[codigo[i] - '0']);
        sb.Append("01010");
        for (var i = 4; i < 8; i++) sb.Append(CodigoR(codigo[i] - '0'));
        sb.Append("101");
        return Modulos(sb.ToString());
    }

    /// <summary>ITF (2 de 5 intercalado) com largura larga = 3 módulos e zonas de guarda.</summary>
    public static IReadOnlyList<bool> Itf(string codigo)
    {
        Validar(codigo, 14);
        var modulos = new List<bool> { true, false, true, false }; // início: barra, espaço, barra, espaço (estreitos)
        for (var i = 0; i < codigo.Length; i += 2)
        {
            var (barras, espacos) = (Itf5[codigo[i] - '0'], Itf5[codigo[i + 1] - '0']);
            for (var k = 0; k < 5; k++)
            {
                modulos.AddRange(Enumerable.Repeat(true, barras[k] == '1' ? 3 : 1));
                modulos.AddRange(Enumerable.Repeat(false, espacos[k] == '1' ? 3 : 1));
            }
        }
        modulos.AddRange([true, true, true, false, true]); // fim: barra larga, espaço, barra
        return modulos;
    }

    /// <summary>SVG do código. Com largura e altura, ocupa a área centralizado e mantendo a proporção.</summary>
    public static string Svg(string codigo, float? largura = null, float? altura = null)
    {
        var d = CodigoDeBarras.Normalizar(codigo);
        if (d is null || !CodigoDeBarras.Valido(d)) throw new ArgumentException($"Código de barras inválido: {codigo}.", nameof(codigo));
        var modulos = d.Length switch
        {
            8 => Ean8(d),
            12 => Ean13("0" + d),
            13 => Ean13(d),
            _ when d[0] == '0' => Ean13(d[1..]),
            _ => Itf(d),
        };
        // Proporção larga (o leitor precisa de barras compridas na horizontal) e zona de silêncio de 10 módulos de cada lado.
        const int alturaDasBarras = 24, silencio = 10;
        var tamanho = largura is null || altura is null ? "" : string.Create(CultureInfo.InvariantCulture, $" width=\"{largura}\" height=\"{altura}\"");
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"{tamanho} viewBox=\"0 0 {modulos.Count + 2 * silencio} {alturaDasBarras}\" preserveAspectRatio=\"xMidYMid meet\">");
        for (var i = 0; i < modulos.Count; i++)
        {
            if (!modulos[i]) continue;
            var inicio = i;
            while (i + 1 < modulos.Count && modulos[i + 1]) i++;
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{inicio + silencio}\" y=\"0\" width=\"{i - inicio + 1}\" height=\"{alturaDasBarras}\" fill=\"#000\"/>");
        }
        return sb.Append("</svg>").ToString();
    }

    private static void Validar(string codigo, int tamanho)
    {
        if (codigo.Length != tamanho || !codigo.All(char.IsAsciiDigit) || !CodigoDeBarras.Valido(codigo))
            throw new ArgumentException($"Código de barras inválido: {codigo}.", nameof(codigo));
    }
}
