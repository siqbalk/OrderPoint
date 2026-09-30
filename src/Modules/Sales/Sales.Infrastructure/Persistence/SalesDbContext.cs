using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Outbox;
using BuildingBlocks.Pagination;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Sales.Application.Abstractions;
using Sales.Domain;

namespace Sales.Infrastructure.Persistence;

/// <summary>
/// Sales owns this schema exclusively. No other module's DbContext ever maps
/// an entity into the "sales" schema, and Sales never maps into another
/// module's schema — that boundary is what keeps a later microservice split
/// viable even though all schemas live in one physical database.
/// </summary>
public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), ISalesDbContext
{
    public const string SchemaName = "sales";

    protected override string Schema => SchemaName;

    public DbSet<Order> Orders => Set<Order>();

    IOutboxWriter ISalesDbContext.Outbox => this;

    void ISalesDbContext.AddOrder(Order order) => Orders.Add(order);

    public Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
        => Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    public async Task<PagedResult<Order>> ListOrdersAsync(OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Orders.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(o => o.Status == s);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.PlacedOnUtc)
            .ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, page, pageSize, total);
    }
}
