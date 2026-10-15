using BuildingBlocks.Security;
using BuildingBlocks.Web;
using Identity.Application.Features.ListUsers;
using Identity.Contracts;
using Identity.Domain;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.ListUsers;

public static class ListUsersEndpoint
{
    public static void MapListUsers(this IEndpointRouteBuilder app)
    {
        app.MapGet("/users", async (TenantRole? role, UserStatus? status, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ListUsersQuery(role, status), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("ListUsers")
        .WithSummary("The tenant's users, optionally filtered by role and status (e.g. pending invitations)")
        .Produces<IReadOnlyList<UserResponse>>()
        .RequireAuthorization(IdentityPermissions.UsersRead);
    }
}
