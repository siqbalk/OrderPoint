using Catalog.Domain;

namespace Catalog.Application.Features;

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    bool IsActive,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? UpdatedOnUtc)
{
    public static ProductResponse From(Product p)
        => new(p.Id, p.Sku, p.Name, p.Description, p.Price, p.IsActive, p.CreatedOnUtc, p.UpdatedOnUtc);
}
