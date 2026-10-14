namespace Kamus.Modules.Identidade.Dominio;

/// <summary>Perfil do usuário em uma empresa. O mesmo usuário pode ter perfis diferentes em cada CNPJ.</summary>
public enum Perfil
{
    Dono = 1,
    Gerente = 2,
    Vendedor = 3,
    Estoquista = 4,
    Financeiro = 5,
}
