using Kamus.Modules.Estoque.NotasFiscais;
using Shouldly;

namespace Kamus.UnitTests.Estoque;

/// <summary>O XML de exemplo em docs/exemplos precisa continuar legível pelo sistema (o dono testa com ele em localhost).</summary>
public class ExemploDaDocumentacaoTests
{
    [Fact]
    public void NfeDeExemplo_LeComTresItens()
    {
        var raiz = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(raiz, "Kamus.slnx"))) raiz = Directory.GetParent(raiz)!.FullName;
        using var arquivo = File.OpenRead(Path.Combine(raiz, "docs", "exemplos", "nfe-entrada-exemplo.xml"));

        var nota = LeitorDeNfe.Ler(arquivo);

        nota.DestinatarioDocumento.ShouldBe("12345678000195");
        nota.Itens.Select(i => i.Codigo).ShouldBe(["AUR-100", "AUR-900", "EMB-01"]);
        nota.Itens[0].CustoTotal.ShouldBe(312m); // 12 × 25 + 12 de frete
    }
}
