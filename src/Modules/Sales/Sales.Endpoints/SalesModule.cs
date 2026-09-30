using BuildingBlocks.Modules;
using BuildingBlocks.Outbox;
using BuildingBlocks.Persistence;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sales.Application.Abstractions;
using Sales.Contracts;
using Sales.Contracts.IntegrationEvents;
using Sales.Endpoints.Features.CancelOrder;
using Sales.Endpoints.Features.GetOrder;
using Sales.Endpoints.Features.ListOrders;
using Sales.Endpoints.Features.PlaceOrder;
using Sales.Infrastructure.Persistence;

namespace Sales.Endpoints;

public sealed class SalesModule : IModule
{
    public string Name => "Sales";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(SalesPermissions.OrdersRead, "View orders", TenantRole.Member),
        new(SalesPermissions.OrdersCreate, "Place orders", TenantRole.Member),
        new(SalesPermissions.OrdersCancel, "Cancel orders", TenantRole.Admin),
    ];

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(typeof(ISalesDbContext).Assembly);
        services.AddModuleDbContext<SalesDbContext>(configuration, Name, SalesDbContext.SchemaName);
        services.AddScoped<ISalesDbContext>(sp => sp.GetRequiredService<SalesDbContext>());

        // Sales.Application depends on IInventoryAvailabilityChecker (Inventory.Contracts) and
        // IProductCatalog (Catalog.Contracts) by constructor injection; those modules register the
        // concrete implementations, so no compile-time link exists between the modules' internals.

        services.Configure<OutboxProcessorOptions>(o => o.RegisterEventsFromAssembly(typeof(OrderPlacedIntegrationEvent).Assembly));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/sales/orders").WithTags("Sales");

        group.MapPlaceOrder();
        group.MapGetOrder();
        group.MapListOrders();
        group.MapCancelOrder();
    }
}
