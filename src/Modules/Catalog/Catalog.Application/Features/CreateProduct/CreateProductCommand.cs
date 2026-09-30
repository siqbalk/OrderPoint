using BuildingBlocks.Results;
using MediatR;

namespace Catalog.Application.Features.CreateProduct;

public sealed record CreateProductCommand(string Sku, string Name, string? Description, decimal Price) : IRequest<Result<ProductResponse>>;
