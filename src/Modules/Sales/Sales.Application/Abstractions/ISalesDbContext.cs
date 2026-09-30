using BuildingBlocks.Outbox;
using BuildingBlocks.Pagination;
using Sales.Domain;

namespace Sales.Application.Abstractions;

/// <summary>
/// Application layer depends on this abstraction, not the concrete EF Core
/// DbContext — keeps Sales.Application free of any EF Core / Infrastructure
/// reference. Reads are purpose-built methods rather than an exposed IQueryable,
/// and are automatically limited to the current tenant by the DbContext.
/// </summary>
public interface ISalesDbContext
{
    void AddOrder(Order order);

    Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<PagedResult<Order>> ListOrdersAsync(OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    IOutboxWriter Outbox { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
