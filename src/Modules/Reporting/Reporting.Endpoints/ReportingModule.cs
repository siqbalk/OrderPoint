using BuildingBlocks.Modules;
using BuildingBlocks.Persistence;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Abstractions;
using Reporting.Contracts;
using Reporting.Endpoints.Features.GetSalesReport;
using Reporting.Endpoints.Features.GetTopOrders;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Endpoints;

/// <summary>
/// Read-side analytics built from other modules' integration events (CQRS read model).
/// Owns its data outright instead of querying other modules' schemas.
/// </summary>
public sealed class ReportingModule : IModule
{
    public string Name => "Reporting";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(ReportingPermissions.SalesRead, "View sales and revenue reports", TenantRole.Admin),
    ];

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(typeof(IReportingDbContext).Assembly);
        services.AddModuleDbContext<ReportingDbContext>(configuration, Name, ReportingDbContext.SchemaName);
        services.AddScoped<IReportingDbContext>(sp => sp.GetRequiredService<ReportingDbContext>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reporting").WithTags("Reporting");

        group.MapGetSalesReport();
        group.MapGetTopOrders();
    }
}
