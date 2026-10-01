using BuildingBlocks.Results;

namespace Catalog.Application.Features;

internal static class CatalogErrors
{
    public static Error ProductNotFound(Guid productId)
        => Error.NotFound("Catalog.ProductNotFound", $"Product '{productId}' was not found.");

    public static Error ProductSkuNotFound(string sku)
        => Error.NotFound("Catalog.ProductNotFound", $"Product with SKU '{sku}' was not found.");
}
