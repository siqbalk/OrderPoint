using BuildingBlocks.Web;
using Identity.Application.Features.GetUser;
using Identity.Application.Features.ListUsers;
using Identity.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.GetUser;

public static class GetUserEndpoint
{
    public static void MapGetUser(this IEndpointRouteBuilder app)
    {
        app.MapGet("/users/{userId:guid}", async (Guid userId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetUserQuery(userId), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetUser")
        .WithSummary("One user of the current tenant")
        .Produces<UserResponse>()
        .RequireAuthorization(IdentityPermissions.UsersRead);
    }
}
