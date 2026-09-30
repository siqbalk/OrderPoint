using BuildingBlocks.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Modules;

/// <summary>
/// Composition-root contract every module implements once. Host discovers and
/// calls these instead of each module wiring itself into Program.cs directly —
/// keeps Program.cs from growing module-specific knowledge as modules are added.
/// </summary>
public interface IModule
{
    string Name { get; }

    void AddModule(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);

    /// <summary>
    /// Permissions this module owns, named <c>{module}:{resource}:{action}</c>.
    /// Host aggregates them into the <see cref="IPermissionCatalog"/> (which
    /// Identity uses to build tokens) and into authorization policies.
    /// </summary>
    IReadOnlyCollection<PermissionDefinition> Permissions => [];

    /// <summary>
    /// Coarse-grained authorization policies this module owns (see DESIGN.md
    /// section 6). Host aggregates these from every module at startup. By
    /// default each permission becomes a policy of the same name requiring
    /// that permission claim; override only for policies that need more.
    /// </summary>
    void AddAuthorizationPolicies(AuthorizationOptions options)
    {
        foreach (var permission in Permissions)
        {
            options.AddPolicy(permission.Name, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimNames.Permission, permission.Name));
        }
    }
}
