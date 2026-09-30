using BuildingBlocks.Web;
using Identity.Application.Features;
using Identity.Application.Features.Login;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.Login;

public sealed record LoginRequest(string Email, string Password);

public static class LoginEndpoint
{
    public static void MapLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async (LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("Login")
        .WithSummary("Exchange email and password for an access token")
        .Produces<AuthTokenResponse>()
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicies.Authentication);
    }
}
