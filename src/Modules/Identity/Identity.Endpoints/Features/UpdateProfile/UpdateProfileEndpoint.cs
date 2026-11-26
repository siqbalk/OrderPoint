using BuildingBlocks.Web;
using Identity.Application.Features.ListUsers;
using Identity.Application.Features.UpdateProfile;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.UpdateProfile;

public sealed record UpdateProfileRequest(string DisplayName);

public static class UpdateProfileEndpoint
{
    public static void MapUpdateProfile(this IEndpointRouteBuilder app)
    {
        app.MapPut("/me", async (UpdateProfileRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new UpdateProfileCommand(request.DisplayName), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("UpdateProfile")
        .WithSummary("Change the signed-in user's display name; the access token shows it from the next sign-in")
        .Produces<UserResponse>()
        .RequireAuthorization();
    }
}
