using BuildingBlocks.Web;
using Identity.Application.Features.EnableUser;
using Identity.Application.Features.ListUsers;
using Identity.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.EnableUser;

public static class EnableUserEndpoint
{
    public static void MapEnableUser(this IEndpointRouteBuilder app)
    {
        app.MapPost("/users/{userId:guid}/enable", async (Guid userId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new EnableUserCommand(userId), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("EnableUser")
        .WithSummary("Restore a disabled user's access; they take a seat on the plan again")
        .Produces<UserResponse>()
        .RequireAuthorization(IdentityPermissions.UsersManage);
    }
}
