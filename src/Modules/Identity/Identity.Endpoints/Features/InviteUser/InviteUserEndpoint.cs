using BuildingBlocks.Security;
using BuildingBlocks.Web;
using Identity.Application.Features.InviteUser;
using Identity.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.InviteUser;

public sealed record InviteUserRequest(string Email, string DisplayName, TenantRole Role);

public static class InviteUserEndpoint
{
    public static void MapInviteUser(this IEndpointRouteBuilder app)
    {
        app.MapPost("/users/invitations", async (InviteUserRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new InviteUserCommand(request.Email, request.DisplayName, request.Role), cancellationToken);
            return result.ToHttpResult(invitation => Results.Accepted(value: invitation));
        })
        .WithName("InviteUser")
        .WithSummary("Invite someone to the organisation; they receive an email with an accept link")
        .Produces<InviteUserResponse>(StatusCodes.Status202Accepted)
        .RequireAuthorization(IdentityPermissions.UsersManage);
    }
}
