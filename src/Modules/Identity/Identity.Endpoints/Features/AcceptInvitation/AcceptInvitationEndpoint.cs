using BuildingBlocks.Web;
using Identity.Application.Features;
using Identity.Application.Features.AcceptInvitation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.AcceptInvitation;

public sealed record AcceptInvitationRequest(string Token, string Password);

public static class AcceptInvitationEndpoint
{
    public static void MapAcceptInvitation(this IEndpointRouteBuilder app)
    {
        app.MapPost("/invitations/accept", async (AcceptInvitationRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new AcceptInvitationCommand(request.Token, request.Password), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("AcceptInvitation")
        .WithSummary("Set a password for an invited user and sign them in")
        .Produces<AuthTokenResponse>()
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicies.Authentication);
    }
}
