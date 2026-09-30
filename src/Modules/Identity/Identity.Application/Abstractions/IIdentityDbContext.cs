using BuildingBlocks.Outbox;
using Identity.Domain;

namespace Identity.Application.Abstractions;

/// <summary>
/// Identity manages tenancy itself, so unlike other modules its tables are not
/// filtered by the ambient tenant: sign-in must find a user before any tenant
/// is known. Every tenant-specific query here takes the tenant id explicitly.
/// </summary>
public interface IIdentityDbContext
{
    void AddTenant(Tenant tenant);

    void AddUser(User user);

    Task<Tenant?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<User?> FindUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<User?> FindUserByInvitationTokenHashAsync(string invitationTokenHash, CancellationToken cancellationToken);

    Task<int> CountUsersAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> ListUsersAsync(Guid tenantId, CancellationToken cancellationToken);

    IOutboxWriter Outbox { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
