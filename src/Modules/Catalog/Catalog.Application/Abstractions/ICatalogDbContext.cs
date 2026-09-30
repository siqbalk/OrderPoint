using BuildingBlocks.Outbox;
using BuildingBlocks.Pagination;
using Catalog.Domain;

namespace Catalog.Application.Abstractions;

/// <summary>All reads are automatically limited to the current tenant by the DbContext's query filter.</summary>
public interface ICatalogDbContext
{
    void AddProduct(Product product);

    Task<Product?> FindByIdAsync(Guid productId, CancellationToken cancellationToken);

    Task<bool> SkuExistsAsync(string normalizedSku, CancellationToken cancellationToken);

    Task<int> CountProductsAsync(CancellationToken cancellationToken);

    Task<PagedResult<Product>> ListAsync(string? search, bool includeInactive, int page, int pageSize, CancellationToken cancellationToken);

    IOutboxWriter Outbox { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
