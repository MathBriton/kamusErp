namespace Kamus.SharedKernel.Auditoria;

/// <summary>
/// Audita uma entidade que não é <c>ITenantOwned</c> (as que são, já são auditadas automaticamente).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditavelAttribute : Attribute;

/// <summary>
/// Nunca grava no histórico: na classe, ignora a entidade inteira; na propriedade, ignora o campo
/// (senhas, tokens, contadores técnicos).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false)]
public sealed class NaoAuditarAttribute : Attribute;
