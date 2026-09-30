using BuildingBlocks.Web;
using Identity.Application.Features.GetCurrentUser;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.GetCurrentUser;

public static class GetCurrentUserEndpoint
{
    public static void MapGetCurrentUser(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetCurrentUserQuery(), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetCurrentUser")
        .WithSummary("The signed-in user, their tenant, plan usage and permissions")
        .Produces<CurrentUserResponse>()
        .RequireAuthorization();
    }
}
