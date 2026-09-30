namespace BuildingBlocks.Security;

/// <summary>
/// Built-in tenant roles. Each role includes every permission granted to the
/// roles below it (Owner ⊇ Admin ⊇ Member).
/// </summary>
public enum TenantRole
{
    Member = 0,
    Admin = 1,
    Owner = 2,
}

/// <summary>
/// A permission a module owns, named <c>{module}:{resource}:{action}</c>.
/// <paramref name="GrantedTo"/> is the lowest built-in role that receives it.
/// </summary>
public sealed record PermissionDefinition(string Name, string Description, TenantRole GrantedTo = TenantRole.Admin);

/// <summary>
/// Every permission declared by every module, aggregated by Host at startup.
/// Identity uses it to decide which permission claims to put in a user's token,
/// without knowing anything about the modules that own them.
/// </summary>
public interface IPermissionCatalog
{
    IReadOnlyCollection<PermissionDefinition> All { get; }

    IReadOnlyCollection<string> ForRole(TenantRole role);
}

public sealed class PermissionCatalog(IEnumerable<PermissionDefinition> permissions) : IPermissionCatalog
{
    public IReadOnlyCollection<PermissionDefinition> All { get; } = permissions.ToArray();

    public IReadOnlyCollection<string> ForRole(TenantRole role)
        => All.Where(p => p.GrantedTo <= role).Select(p => p.Name).ToArray();
}
