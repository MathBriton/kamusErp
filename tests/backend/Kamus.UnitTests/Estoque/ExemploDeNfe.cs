using System.Globalization;

namespace Kamus.UnitTests.Estoque;

/// <summary>XML de NF-e 4.00 de exemplo (estrutura real, valores fictícios).</summary>
internal static class ExemploDeNfe
{
    public const string Chave = "35261011222333000181550010000012341000012340";

    public sealed record Item(string Codigo, string Ean, string Descricao, string Unidade, decimal Quantidade, decimal ValorUnitario,
        decimal Frete = 0, decimal Desconto = 0, decimal Ipi = 0, decimal IcmsSt = 0);

    private static string N(decimal v) => v.ToString("0.00##", CultureInfo.InvariantCulture);

    public static string Xml(IReadOnlyList<Item> itens, string destinatario = "12345678000195", string? cStat = "100", string chave = Chave, string numero = "1234", bool comCobranca = true)
    {
        var dets = string.Concat(itens.Select((i, n) => $"""
            <det nItem="{n + 1}"><prod><cProd>{i.Codigo}</cProd><cEAN>{i.Ean}</cEAN><xProd>{i.Descricao}</xProd><NCM>61091000</NCM><CFOP>5102</CFOP>
            <uCom>{i.Unidade}</uCom><qCom>{N(i.Quantidade)}</qCom><vUnCom>{N(i.ValorUnitario)}</vUnCom><vProd>{N(i.Quantidade * i.ValorUnitario)}</vProd>
            <cEANTrib>{i.Ean}</cEANTrib><uTrib>{i.Unidade}</uTrib><qTrib>{N(i.Quantidade)}</qTrib><vUnTrib>{N(i.ValorUnitario)}</vUnTrib>
            {(i.Frete > 0 ? $"<vFrete>{N(i.Frete)}</vFrete>" : "")}{(i.Desconto > 0 ? $"<vDesc>{N(i.Desconto)}</vDesc>" : "")}<indTot>1</indTot></prod>
            <imposto><ICMS><ICMS10><orig>0</orig><CST>10</CST><vICMSST>{N(i.IcmsSt)}</vICMSST></ICMS10></ICMS><IPI><cEnq>999</cEnq><IPITrib><CST>50</CST><vIPI>{N(i.Ipi)}</vIPI></IPITrib></IPI></imposto></det>
            """));
        var total = itens.Sum(i => i.Quantidade * i.ValorUnitario + i.Frete - i.Desconto + i.Ipi + i.IcmsSt);
        var prot = cStat is null ? "" : $"<protNFe versao=\"4.00\"><infProt><chNFe>{chave}</chNFe><cStat>{cStat}</cStat><xMotivo>Autorizado o uso da NF-e</xMotivo></infProt></protNFe>";
        var nfe = $"""
            <NFe xmlns="http://www.portalfiscal.inf.br/nfe"><infNFe Id="NFe{chave}" versao="4.00">
            <ide><cUF>35</cUF><natOp>VENDA DE MERCADORIA</natOp><mod>55</mod><serie>1</serie><nNF>{numero}</nNF><dhEmi>2026-10-10T10:30:00-03:00</dhEmi><tpNF>1</tpNF></ide>
            <emit><CNPJ>11222333000181</CNPJ><xNome>Confecções Aurora LTDA</xNome><xFant>Aurora Moda</xFant>
            <enderEmit><xLgr>Rua Oriente</xLgr><nro>100</nro><xBairro>Brás</xBairro><xMun>São Paulo</xMun><UF>SP</UF><CEP>03016000</CEP><fone>1133334444</fone></enderEmit><CRT>3</CRT></emit>
            <dest><CNPJ>{destinatario}</CNPJ><xNome>Kamus Moda Centro LTDA</xNome></dest>
            {dets}
            <total><ICMSTot><vNF>{N(total)}</vNF></ICMSTot></total>
            {(comCobranca ? "" : "<!--")}<cobr><dup><nDup>001</nDup><dVenc>2026-11-10</dVenc><vDup>{N(Math.Round(total / 2, 2))}</vDup></dup><dup><nDup>002</nDup><dVenc>2026-12-10</dVenc><vDup>{N(total - Math.Round(total / 2, 2))}</vDup></dup></cobr>{(comCobranca ? "" : "-->")}
            </infNFe></NFe>
            """;
        return cStat is null ? nfe : $"<nfeProc xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"4.00\">{nfe}{prot}</nfeProc>";
    }
}
