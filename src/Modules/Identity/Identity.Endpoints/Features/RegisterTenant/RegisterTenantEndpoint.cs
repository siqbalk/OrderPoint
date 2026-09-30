using BuildingBlocks.Web;
using Identity.Application.Features;
using Identity.Application.Features.RegisterTenant;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.RegisterTenant;

public sealed record RegisterTenantRequest(string TenantName, string OwnerEmail, string OwnerDisplayName, string Password);

public static class RegisterTenantEndpoint
{
    public static void MapRegisterTenant(this IEndpointRouteBuilder app)
    {
        app.MapPost("/tenants/register", async (RegisterTenantRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new RegisterTenantCommand(request.TenantName, request.OwnerEmail, request.OwnerDisplayName, request.Password),
                cancellationToken);

            return result.ToHttpResult(token => Results.Created("/api/identity/me", token));
        })
        .WithName("RegisterTenant")
        .WithSummary("Sign up a new organisation and its owner")
        .Produces<AuthTokenResponse>(StatusCodes.Status201Created)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicies.Authentication);
    }
}
