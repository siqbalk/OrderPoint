using BuildingBlocks.Web;
using Identity.Application.Features.ChangePlan;
using Identity.Contracts;
using Identity.Domain;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Endpoints.Features.ChangePlan;

public sealed record ChangePlanRequest(SubscriptionPlan Plan);

public static class ChangePlanEndpoint
{
    public static void MapChangePlan(this IEndpointRouteBuilder app)
    {
        app.MapPut("/tenant/plan", async (ChangePlanRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ChangePlanCommand(request.Plan), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("ChangePlan")
        .Produces<ChangePlanResponse>()
        .RequireAuthorization(IdentityPermissions.TenantManage);
    }
}
