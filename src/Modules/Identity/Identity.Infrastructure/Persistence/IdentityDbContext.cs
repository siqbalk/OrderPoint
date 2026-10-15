using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Outbox;
using BuildingBlocks.Persistence;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Identity owns the "identity" schema. Its entities are deliberately not
/// tenant-scoped (see <see cref="IIdentityDbContext"/>); every query below
/// filters by tenant explicitly instead.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), IIdentityDbContext
{
    public const string SchemaName = "identity";

    protected override string Schema => SchemaName;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();

    IOutboxWriter IIdentityDbContext.Outbox => this;

    public void AddTenant(Tenant tenant) => Tenants.Add(tenant);

    public void AddUser(User user) => Users.Add(user);

    public Task<Tenant?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        => Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public Task<User?> FindUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        => Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        => Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken);

    public Task<User?> FindUserByInvitationTokenHashAsync(string invitationTokenHash, CancellationToken cancellationToken)
        => Users.FirstOrDefaultAsync(u => u.InvitationTokenHash == invitationTokenHash, cancellationToken);

    public Task<int> CountUsersAsync(Guid tenantId, CancellationToken cancellationToken)
        => Users.CountAsync(u => u.TenantId == tenantId && u.Status != UserStatus.Disabled, cancellationToken);

    public async Task<IReadOnlyList<User>> ListUsersAsync(
        Guid tenantId, TenantRole? role, UserStatus? status, CancellationToken cancellationToken)
    {
        var query = Users.AsNoTracking().Where(u => u.TenantId == tenantId);
        if (role is { } r)
        {
            query = query.Where(u => u.Role == r);
        }

        if (status is { } s)
        {
            query = query.Where(u => u.Status == s);
        }

        return await query.OrderBy(u => u.CreatedOnUtc).ToListAsync(cancellationToken);
    }
}
