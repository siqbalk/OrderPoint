using Catalog.Contracts.IntegrationEvents;
using Inventory.Application.Abstractions;
using Inventory.Domain;
using MediatR;

namespace Inventory.Application.Features.CreateStockItemOnProductCreated;

/// <summary>
/// Opens an empty stock record for every new catalog product, so it appears in
/// stock reports straight away. Idempotent, and safe if stock for the SKU is
/// received before this event is delivered.
/// </summary>
public sealed class CreateStockItemOnProductCreatedHandler(IInventoryDbContext dbContext)
    : INotificationHandler<ProductCreatedIntegrationEvent>
{
    public Task Handle(ProductCreatedIntegrationEvent notification, CancellationToken cancellationToken)
        => dbContext.GetOrCreateBySkuAsync(StockItem.NormalizeSku(notification.Sku), cancellationToken);
}
