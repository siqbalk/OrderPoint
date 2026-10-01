using BuildingBlocks.Modules;
using BuildingBlocks.Outbox;
using BuildingBlocks.Persistence;
using BuildingBlocks.Security;
using Catalog.Application.Abstractions;
using Catalog.Contracts;
using Catalog.Contracts.IntegrationEvents;
using Catalog.Endpoints.Features.CreateProduct;
using Catalog.Endpoints.Features.GetProduct;
using Catalog.Endpoints.Features.GetProductBySku;
using Catalog.Endpoints.Features.ListProducts;
using Catalog.Endpoints.Features.UpdateProduct;
using Catalog.Infrastructure.InternalServices;
using Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Endpoints;

public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(CatalogPermissions.ProductsRead, "Browse the product catalog", TenantRole.Member),
        new(CatalogPermissions.ProductsManage, "Create and edit products and prices", TenantRole.Admin),
    ];

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(typeof(ICatalogDbContext).Assembly);
        services.AddModuleDbContext<CatalogDbContext>(configuration, Name, CatalogDbContext.SchemaName);
        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());

        // Published synchronous contract Sales consumes to price orders.
        services.AddScoped<IProductCatalog, ProductCatalog>();

        services.Configure<OutboxProcessorOptions>(o => o.RegisterEventsFromAssembly(typeof(ProductCreatedIntegrationEvent).Assembly));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/catalog/products").WithTags("Catalog");

        group.MapCreateProduct();
        group.MapUpdateProduct();
        group.MapGetProduct();
        group.MapGetProductBySku();
        group.MapListProducts();
    }
}
