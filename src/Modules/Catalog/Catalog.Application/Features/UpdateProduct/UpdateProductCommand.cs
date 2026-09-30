using BuildingBlocks.Results;
using MediatR;

namespace Catalog.Application.Features.UpdateProduct;

/// <summary>Changes name, description, and price. Existing orders keep the price they were placed at.</summary>
public sealed record UpdateProductCommand(Guid ProductId, string Name, string? Description, decimal Price, bool IsActive)
    : IRequest<Result<ProductResponse>>;
