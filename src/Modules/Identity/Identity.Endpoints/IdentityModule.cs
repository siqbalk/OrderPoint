using BuildingBlocks.Modules;
using BuildingBlocks.Outbox;
using BuildingBlocks.Persistence;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Contracts;
using Identity.Contracts.IntegrationEvents;
using Identity.Endpoints.Features.AcceptInvitation;
using Identity.Endpoints.Features.ChangePlan;
using Identity.Endpoints.Features.GetCurrentUser;
using Identity.Endpoints.Features.InviteUser;
using Identity.Endpoints.Features.ListUsers;
using Identity.Endpoints.Features.Login;
using Identity.Endpoints.Features.RegisterTenant;
using Identity.Infrastructure.InternalServices;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Endpoints;

/// <summary>
/// Identity &amp; tenancy: self-service sign-up, sign-in, user invitations,
/// subscription plans. The only module that issues tokens.
/// </summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(IdentityPermissions.UsersRead, "View the users in the organisation", TenantRole.Member),
        new(IdentityPermissions.UsersManage, "Invite users and manage their access", TenantRole.Admin),
        new(IdentityPermissions.TenantManage, "Change the subscription plan and organisation settings", TenantRole.Owner),
    ];

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(typeof(IIdentityDbContext).Assembly);
        services.AddModuleDbContext<IdentityDbContext>(configuration, Name, IdentityDbContext.SchemaName);
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        // Published synchronous contract other modules consume (Catalog's plan limits).
        services.AddScoped<ITenantPlanProvider, TenantPlanProvider>();

        services.Configure<OutboxProcessorOptions>(o => o.RegisterEventsFromAssembly(typeof(TenantRegisteredIntegrationEvent).Assembly));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity").WithTags("Identity");

        group.MapRegisterTenant();
        group.MapLogin();
        group.MapAcceptInvitation();
        group.MapGetCurrentUser();
        group.MapListUsers();
        group.MapInviteUser();
        group.MapChangePlan();
    }
}
