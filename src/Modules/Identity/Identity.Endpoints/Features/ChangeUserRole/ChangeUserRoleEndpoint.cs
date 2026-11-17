using BuildingBlocks.Security;
using BuildingBlocks.Web;
using Identity.Application.Features.ChangeUserRole;
using Identity.Application.Features.ListUsers;
using Identity.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.ChangeUserRole;

public sealed record ChangeUserRoleRequest(TenantRole Role);

public static class ChangeUserRoleEndpoint
{
    public static void MapChangeUserRole(this IEndpointRouteBuilder app)
    {
        app.MapPut("/users/{userId:guid}/role", async (Guid userId, ChangeUserRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ChangeUserRoleCommand(userId, request.Role), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("ChangeUserRole")
        .WithSummary("Make a user an Admin or a Member; takes effect at their next sign-in")
        .Produces<UserResponse>()
        .RequireAuthorization(IdentityPermissions.UsersManage);
    }
}
