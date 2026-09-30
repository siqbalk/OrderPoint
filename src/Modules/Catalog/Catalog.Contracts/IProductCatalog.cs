namespace Catalog.Contracts;

/// <summary>
/// Synchronous cross-module contract, owned and implemented by Catalog.
/// Sales uses it to price order lines from the catalog rather than from the
/// request. Scoped to the current tenant like every other tenant-owned read.
/// </summary>
public interface IProductCatalog
{
    /// <summary>Products for the given SKUs, keyed by normalised SKU. Unknown SKUs are simply absent.</summary>
    Task<IReadOnlyDictionary<string, ProductSnapshot>> GetBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken);
}

public sealed record ProductSnapshot(Guid ProductId, string Sku, string Name, decimal Price, bool IsActive);
