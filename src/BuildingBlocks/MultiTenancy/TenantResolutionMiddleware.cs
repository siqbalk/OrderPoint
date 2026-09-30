using BuildingBlocks.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.MultiTenancy;

/// <summary>
/// Establishes the request's tenant from the authenticated user's
/// <c>tenant_id</c> claim. The tenant always comes from the signed token, never
/// from a header or route value a caller could tamper with.
/// Must run after UseAuthentication and before UseAuthorization.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITenantSetter tenantSetter)
    {
        var claim = context.User.FindFirst(ClaimNames.TenantId)?.Value;
        if (context.User.Identity?.IsAuthenticated == true && Guid.TryParse(claim, out var tenantId))
        {
            tenantSetter.SetTenant(tenantId);

            using (logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenantId }))
            {
                await next(context);
            }

            return;
        }

        await next(context);
    }
}
