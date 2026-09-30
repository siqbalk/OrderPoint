using BuildingBlocks.Web;
using Identity.Application.Features.ListUsers;
using Identity.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.ListUsers;

public static class ListUsersEndpoint
{
    public static void MapListUsers(this IEndpointRouteBuilder app)
    {
        app.MapGet("/users", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ListUsersQuery(), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("ListUsers")
        .Produces<IReadOnlyList<UserResponse>>()
        .RequireAuthorization(IdentityPermissions.UsersRead);
    }
}
