using BuildingBlocks.Modules;
using BuildingBlocks.Outbox;
using BuildingBlocks.Persistence;
using BuildingBlocks.Security;
using Inventory.Application.Abstractions;
using Inventory.Contracts;
using Inventory.Contracts.IntegrationEvents;
using Inventory.Endpoints.Features.AddStock;
using Inventory.Endpoints.Features.AdjustStock;
using Inventory.Endpoints.Features.GetStockBySku;
using Inventory.Endpoints.Features.ListStock;
using Inventory.Infrastructure.InternalServices;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Endpoints;

public sealed class InventoryModule : IModule
{
    public string Name => "Inventory";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(InventoryPermissions.StockRead, "View stock levels", TenantRole.Member),
        new(InventoryPermissions.StockAdjust, "Receive stock and correct stock counts", TenantRole.Admin),
    ];

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(typeof(IInventoryDbContext).Assembly);
        services.AddModuleDbContext<InventoryDbContext>(configuration, Name, InventoryDbContext.SchemaName);
        services.AddScoped<IInventoryDbContext>(sp => sp.GetRequiredService<InventoryDbContext>());

        // Published synchronous contract Sales (and any future module) consumes.
        services.AddScoped<IInventoryAvailabilityChecker, InventoryAvailabilityChecker>();

        services.Configure<OutboxProcessorOptions>(o => o.RegisterEventsFromAssembly(typeof(StockReservedIntegrationEvent).Assembly));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/inventory/stock").WithTags("Inventory");

        group.MapAddStock();
        group.MapAdjustStock();
        group.MapGetStockBySku();
        group.MapListStock();
    }
}
