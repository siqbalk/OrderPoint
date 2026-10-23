using BuildingBlocks.Results;
using MediatR;

namespace Catalog.Application.Features.GetProductQuota;

/// <summary>How much of the plan's product quota the tenant has used, e.g. to warn before creating more fails.</summary>
public sealed record GetProductQuotaQuery : IRequest<Result<ProductQuotaResponse>>;

/// <param name="Used">Every product counts, active or not, the same as when creating a product.</param>
/// <param name="MaxProducts"><c>null</c> means unlimited.</param>
/// <param name="Remaining"><c>null</c> means unlimited.</param>
public sealed record ProductQuotaResponse(string? Plan, int Used, int? MaxProducts, int? Remaining);
