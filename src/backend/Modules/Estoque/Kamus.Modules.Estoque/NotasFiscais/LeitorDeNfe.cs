using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Kamus.Modules.Estoque.NotasFiscais;

public sealed class NfeInvalidaException(string mensagem) : Exception(mensagem);

public sealed record EmitenteDaNfe(string Documento, string RazaoSocial, string? NomeFantasia, string? Telefone, string? Cep, string? Logradouro,
    string? Numero, string? Bairro, string? Cidade, string? Uf);

/// <param name="Ean">GTIN do item; nulo quando a nota traz "SEM GTIN".</param>
/// <param name="CustoTotal">Custo de aquisição do item: valor + frete + seguro + outras despesas − desconto + IPI + ICMS-ST (+ FCP-ST).</param>
public sealed record ItemDaNfe(int Numero, string Codigo, string? Ean, string Descricao, string? Ncm, string Unidade, decimal Quantidade,
    decimal ValorTotal, decimal CustoTotal);

public sealed record DuplicataDaNfe(string Numero, DateOnly Vencimento, decimal Valor);

/// <param name="Autorizada">Tem protocolo de autorização da SEFAZ (cStat 100 ou 150). Sem protocolo = só o XML da NF-e.</param>
public sealed record NfeLida(string Chave, string Numero, string Serie, DateTimeOffset EmitidaEm, EmitenteDaNfe Emitente, string? DestinatarioDocumento,
    decimal ValorTotal, bool Autorizada, IReadOnlyList<ItemDaNfe> Itens, IReadOnlyList<DuplicataDaNfe> Duplicatas);

/// <summary>
/// Lê o XML da NF-e (modelo 55, leiaute 4.00) recebido do fornecedor: aceita o <c>nfeProc</c> (com protocolo) ou só a <c>NFe</c>.
/// Não valida a assinatura digital nem consulta a SEFAZ. DTD/entidades externas são recusadas (proteção contra XXE).
/// </summary>
public static class LeitorDeNfe
{
    public const long TamanhoMaximo = 2 * 1024 * 1024;
    private static readonly XNamespace Ns = "http://www.portalfiscal.inf.br/nfe";

    public static NfeLida Ler(Stream xml)
    {
        XDocument doc;
        try
        {
            var configuracao = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = TamanhoMaximo * 2 };
            using var leitor = XmlReader.Create(xml, configuracao);
            doc = XDocument.Load(leitor);
        }
        catch (XmlException)
        {
            throw new NfeInvalidaException("O arquivo não é um XML válido. Envie o XML da NF-e que o fornecedor mandou (arquivo .xml).");
        }

        var inf = doc.Descendants(Ns + "infNFe").FirstOrDefault()
            ?? throw new NfeInvalidaException("O arquivo não é o XML de uma NF-e (não encontrei o grupo infNFe).");
        try
        {
            return Montar(doc, inf);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or NullReferenceException or InvalidOperationException)
        {
            throw new NfeInvalidaException("O XML da NF-e está incompleto ou com valores inválidos.");
        }
    }

    private static NfeLida Montar(XDocument doc, XElement inf)
    {
        var ide = Filho(inf, "ide");
        if (Texto(ide, "mod") != "55") throw new NfeInvalidaException("Só a NF-e (modelo 55) é aceita. Cupom/NFC-e não serve para entrada de mercadoria.");
        var chave = (string?)inf.Attribute("Id") is { } id && id.StartsWith("NFe", StringComparison.Ordinal) ? id[3..] : "";
        if (chave.Length != 44 || !chave.All(char.IsAsciiDigit)) throw new NfeInvalidaException("A chave de acesso da NF-e é inválida.");

        var protocolo = doc.Descendants(Ns + "infProt").FirstOrDefault();
        var status = protocolo is null ? null : Texto(protocolo, "cStat");
        if (status is not null and not "100" and not "150")
            throw new NfeInvalidaException($"Esta NF-e não está autorizada pela SEFAZ (situação {status}: {Texto(protocolo!, "xMotivo")}).");

        var emit = Filho(inf, "emit");
        var ender = emit.Element(Ns + "enderEmit");
        var emitente = new EmitenteDaNfe(Texto(emit, "CNPJ") ?? Texto(emit, "CPF") ?? throw new NfeInvalidaException("A NF-e não traz o CNPJ do fornecedor."),
            Texto(emit, "xNome") ?? "", Texto(emit, "xFant"), Texto(ender, "fone"), Texto(ender, "CEP"), Texto(ender, "xLgr"), Texto(ender, "nro"),
            Texto(ender, "xBairro"), Texto(ender, "xMun"), Texto(ender, "UF"));
        var dest = inf.Element(Ns + "dest");

        var itens = inf.Elements(Ns + "det").Select(det =>
        {
            var prod = Filho(det, "prod");
            var imposto = det.Element(Ns + "imposto");
            var valor = Decimal(prod, "vProd");
            var custo = valor + Decimal(prod, "vFrete") + Decimal(prod, "vSeg") + Decimal(prod, "vOutro") - Decimal(prod, "vDesc")
                + Soma(imposto, "vIPI") + Soma(imposto, "vICMSST") + Soma(imposto, "vFCPST");
            var ean = Texto(prod, "cEAN");
            return new ItemDaNfe(int.Parse((string)det.Attribute("nItem")!, CultureInfo.InvariantCulture), Texto(prod, "cProd") ?? "", ean is null or "SEM GTIN" ? null : ean,
                Texto(prod, "xProd") ?? "", Texto(prod, "NCM"), Texto(prod, "uCom") ?? "UN", Decimal(prod, "qCom"), valor, Math.Round(custo, 2));
        }).ToList();
        if (itens.Count == 0) throw new NfeInvalidaException("A NF-e não tem itens.");

        var duplicatas = inf.Element(Ns + "cobr")?.Elements(Ns + "dup").Select(d => new DuplicataDaNfe(Texto(d, "nDup") ?? "",
            DateOnly.ParseExact(Texto(d, "dVenc")!, "yyyy-MM-dd", CultureInfo.InvariantCulture), Decimal(d, "vDup"))).ToList() ?? [];

        return new NfeLida(chave, Texto(ide, "nNF") ?? "", Texto(ide, "serie") ?? "", DateTimeOffset.Parse(Texto(ide, "dhEmi") ?? Texto(ide, "dEmi")!, CultureInfo.InvariantCulture),
            emitente, Texto(dest, "CNPJ") ?? Texto(dest, "CPF"), Decimal(inf.Element(Ns + "total")?.Element(Ns + "ICMSTot"), "vNF"), status is not null, itens, duplicatas);
    }

    private static XElement Filho(XElement pai, string nome) =>
        pai.Element(Ns + nome) ?? throw new NfeInvalidaException($"O XML da NF-e está incompleto (falta o grupo {nome}).");

    private static string? Texto(XElement? pai, string nome) => pai?.Element(Ns + nome)?.Value.Trim() is { Length: > 0 } v ? v : null;

    private static decimal Decimal(XElement? pai, string nome) =>
        Texto(pai, nome) is { } v ? decimal.Parse(v, NumberStyles.Number, CultureInfo.InvariantCulture) : 0m;

    private static decimal Soma(XElement? pai, string nome) =>
        pai?.Descendants(Ns + nome).Sum(e => decimal.Parse(e.Value, NumberStyles.Number, CultureInfo.InvariantCulture)) ?? 0m;
}
