using Kamus.SharedKernel.MultiTenancy;

namespace Kamus.Infrastructure.MultiTenancy;

public sealed class EmpresaAtiva : IEmpresaAtiva
{
    public bool Definida { get; private set; }
    public bool Consolidado { get; private set; }
    public Guid? EmpresaId { get; private set; }
    public IReadOnlyList<Guid> EmpresaIds { get; private set; } = [];
    public string? Perfil { get; private set; }

    public void Definir(Guid empresaId, string perfil)
    {
        GarantirNaoDefinida();
        (Definida, Consolidado, EmpresaId, EmpresaIds, Perfil) = (true, false, empresaId, [empresaId], perfil);
    }

    public void DefinirConsolidado(IReadOnlyList<Guid> empresaIds)
    {
        GarantirNaoDefinida();
        (Definida, Consolidado, EmpresaId, EmpresaIds, Perfil) = (true, true, null, empresaIds, "Dono");
    }

    private void GarantirNaoDefinida()
    {
        if (Definida) throw new InvalidOperationException("A empresa ativa desta requisição já foi definida.");
    }
}
