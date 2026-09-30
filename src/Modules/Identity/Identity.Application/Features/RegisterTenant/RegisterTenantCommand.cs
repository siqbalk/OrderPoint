using BuildingBlocks.Results;
using MediatR;

namespace Identity.Application.Features.RegisterTenant;

/// <summary>Self-service sign-up: creates a tenant on the Free plan and its owner, and signs the owner in.</summary>
public sealed record RegisterTenantCommand(
    string TenantName,
    string OwnerEmail,
    string OwnerDisplayName,
    string Password) : IRequest<Result<AuthTokenResponse>>;
