using Catalog.Contracts;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.InternalServices;

/// <summary>Implementation of Catalog's published <see cref="IProductCatalog"/> contract.</summary>
public sealed class ProductCatalog(CatalogDbContext dbContext) : IProductCatalog
{
    public async Task<IReadOnlyDictionary<string, ProductSnapshot>> GetBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
    {
        var normalized = skus.Select(Product.NormalizeSku).Distinct().ToArray();

        return await dbContext.Products
            .AsNoTracking()
            .Where(p => normalized.Contains(p.Sku))
            .Select(p => new ProductSnapshot(p.Id, p.Sku, p.Name, p.Price, p.IsActive))
            .ToDictionaryAsync(p => p.Sku, cancellationToken);
    }
}
