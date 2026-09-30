using BuildingBlocks.Modules;
using BuildingBlocks.Persistence;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Abstractions;
using Notifications.Application.Features;
using Notifications.Contracts;
using Notifications.Endpoints.Features.ListNotifications;
using Notifications.Infrastructure.Email;
using Notifications.Infrastructure.Persistence;

namespace Notifications.Endpoints;

/// <summary>
/// Sends email in reaction to other modules' integration events (sign-up,
/// invitations, order lifecycle) and keeps a per-tenant log of what was sent.
/// </summary>
public sealed class NotificationsModule : IModule
{
    public string Name => "Notifications";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(NotificationsPermissions.NotificationsRead, "View the log of emails sent by the organisation", TenantRole.Admin),
    ];

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(typeof(INotificationsDbContext).Assembly);
        services.AddModuleDbContext<NotificationsDbContext>(configuration, Name, NotificationsDbContext.SchemaName);
        services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());

        services.Configure<NotificationsOptions>(configuration.GetSection(NotificationsOptions.SectionName));
        services.AddScoped<EmailDispatcher>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapListNotifications();
    }
}
