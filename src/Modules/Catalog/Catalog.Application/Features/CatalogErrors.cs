using BuildingBlocks.Results;

namespace Catalog.Application.Features;

internal static class CatalogErrors
{
    public static Error ProductNotFound(Guid productId)
        => Error.NotFound("Catalog.ProductNotFound", $"Product '{productId}' was not found.");
}
