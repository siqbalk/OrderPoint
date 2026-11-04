using BuildingBlocks.Web;
using Identity.Application.Features.DisableUser;
using Identity.Application.Features.ListUsers;
using Identity.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.DisableUser;

public static class DisableUserEndpoint
{
    public static void MapDisableUser(this IEndpointRouteBuilder app)
    {
        app.MapPost("/users/{userId:guid}/disable", async (Guid userId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DisableUserCommand(userId), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("DisableUser")
        .WithSummary("Revoke a user's access and free their seat; also cancels a pending invitation")
        .Produces<UserResponse>()
        .RequireAuthorization(IdentityPermissions.UsersManage);
    }
}
