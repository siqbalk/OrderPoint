using BuildingBlocks.Results;
using MediatR;

namespace Catalog.Application.Features.GetProductBySku;

/// <summary>Looks up a product by its SKU (case-insensitive, surrounding whitespace ignored).</summary>
public sealed record GetProductBySkuQuery(string Sku) : IRequest<Result<ProductResponse>>;
