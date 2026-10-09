using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Outbox;
using BuildingBlocks.Pagination;
using BuildingBlocks.Persistence;
using Catalog.Application.Abstractions;
using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

/// <summary>Catalog owns the "catalog" schema exclusively; products are tenant-scoped.</summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), ICatalogDbContext
{
    public const string SchemaName = "catalog";

    protected override string Schema => SchemaName;

    public DbSet<Product> Products => Set<Product>();

    IOutboxWriter ICatalogDbContext.Outbox => this;

    public void AddProduct(Product product) => Products.Add(product);

    public Task<Product?> FindByIdAsync(Guid productId, CancellationToken cancellationToken)
        => Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

    public Task<Product?> FindBySkuAsync(string normalizedSku, CancellationToken cancellationToken)
        => Products.AsNoTracking().FirstOrDefaultAsync(p => p.Sku == normalizedSku, cancellationToken);

    public Task<bool> SkuExistsAsync(string normalizedSku, CancellationToken cancellationToken)
        => Products.AnyAsync(p => p.Sku == normalizedSku, cancellationToken);

    public Task<int> CountProductsAsync(CancellationToken cancellationToken)
        => Products.CountAsync(cancellationToken);

    public async Task<PagedResult<Product>> ListAsync(
        string? search, bool includeInactive, decimal? minPrice, decimal? maxPrice, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Products.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Sku, pattern));
        }

        if (minPrice is { } min)
        {
            query = query.Where(p => p.Price >= min);
        }

        if (maxPrice is { } max)
        {
            query = query.Where(p => p.Price <= max);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, page, pageSize, total);
    }

    private static string EscapeLike(string value)
        => value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
