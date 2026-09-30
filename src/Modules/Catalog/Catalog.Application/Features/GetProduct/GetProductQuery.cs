using BuildingBlocks.Results;
using MediatR;

namespace Catalog.Application.Features.GetProduct;

public sealed record GetProductQuery(Guid ProductId) : IRequest<Result<ProductResponse>>;
