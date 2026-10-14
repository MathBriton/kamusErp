using Kamus.Infrastructure.MultiTenancy;
using Kamus.Modules.Identidade.Dominio;
using Kamus.Modules.Identidade.Persistencia;
using Kamus.SharedKernel.Clientes;
using Kamus.SharedKernel.Documentos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Modules.Identidade.Aplicacao;

internal sealed class CadastroDeClientes(IdentidadeDbContext db, IServiceScopeFactory escopos, IPasswordHasher<Usuario> hasher, TimeProvider relogio)
    : ICadastroDeClientes
{
    public async Task<ResultadoDoCadastro> CriarAsync(NovoCliente c, CancellationToken ct = default)
    {
        if (new[] { c.Nome, c.RazaoSocial, c.NomeFantasia, c.NomeDoDono }.Any(string.IsNullOrWhiteSpace))
            return ResultadoDoCadastro.Recusado("Preencha nome do cliente, razão social, nome fantasia e nome do dono.");
        if (!Cnpj.TryParse(c.Cnpj, out var cnpj))
            return ResultadoDoCadastro.Recusado("CNPJ inválido.");
        if (Usuario.NormalizarEmail(c.EmailDoDono) is not { } email)
            return ResultadoDoCadastro.Recusado("E-mail do dono inválido.");
        if (string.IsNullOrWhiteSpace(c.Senha) || c.Senha.Length < 8)
            return ResultadoDoCadastro.Recusado("A senha inicial precisa ter pelo menos 8 caracteres.");
        if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            return ResultadoDoCadastro.Recusado("Já existe um usuário com este e-mail.", conflito: true);

        // Empresa e acesso são do tenant novo: gravados num escopo com esse tenant definido (filtro + RLS).
        var tenant = Tenant.Criar(c.Nome, relogio.GetUtcNow());
        await using var escopo = escopos.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<TenantContext>().Definir(tenant.Id);
        var dbDoTenant = escopo.ServiceProvider.GetRequiredService<IdentidadeDbContext>();

        dbDoTenant.Tenants.Add(tenant);
        var empresa = Empresa.Criar(c.RazaoSocial, c.NomeFantasia, cnpj);
        var dono = Usuario.Criar(tenant.Id, c.NomeDoDono, email);
        dono.DefinirSenhaProvisoria(hasher.HashPassword(dono, c.Senha));
        dbDoTenant.Empresas.Add(empresa);
        dbDoTenant.Usuarios.Add(dono);
        dbDoTenant.AcessosEmpresa.Add(AcessoEmpresa.Criar(dono.Id, empresa.Id, Perfil.Dono));
        await dbDoTenant.SaveChangesAsync(ct);

        return new ResultadoDoCadastro(tenant.Id);
    }
}
