using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using Catalog.Contracts;
using Inventory.Contracts;
using MediatR;
using Sales.Application.Abstractions;
using Sales.Contracts.IntegrationEvents;
using Sales.Domain;

namespace Sales.Application.Features.PlaceOrder;

/// <summary>
/// Uses both allowed shapes of cross-module communication:
/// synchronous contracts for pricing (Catalog) and a fast availability
/// pre-check (Inventory), then an integration event through Sales' outbox so
/// Inventory makes the authoritative reservation asynchronously.
/// </summary>
public sealed class PlaceOrderHandler(
    ISalesDbContext dbContext,
    IProductCatalog productCatalog,
    IInventoryAvailabilityChecker availabilityChecker,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
    : IRequestHandler<PlaceOrderCommand, Result<OrderResponse>>
{
    public async Task<Result<OrderResponse>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var products = await productCatalog.GetBySkusAsync(request.Lines.Select(l => l.Sku).ToList(), cancellationToken);

        var drafts = new List<OrderLineDraft>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            if (!products.TryGetValue(line.Sku.Trim().ToUpperInvariant(), out var product) || !product.IsActive)
            {
                return Result.Failure<OrderResponse>(
                    Error.Validation("Orders.UnknownProduct", $"SKU '{line.Sku}' is not an active product in the catalog."));
            }

            drafts.Add(new OrderLineDraft(product.Sku, product.Name, line.Quantity, product.Price));
        }

        var unavailable = await availabilityChecker.FindUnavailableAsync(
            drafts.Select(d => new StockRequirement(d.Sku, d.Quantity)).ToList(), cancellationToken);
        if (unavailable.Count != 0)
        {
            return Result.Failure<OrderResponse>(Error.Conflict(
                "Orders.InsufficientStock", $"Not enough stock for: {string.Join(", ", unavailable)}."));
        }

        var now = timeProvider.GetUtcNow();
        var order = Order.Place(request.CustomerName, request.CustomerEmail, drafts, now);
        dbContext.AddOrder(order);

        dbContext.Outbox.Enqueue(new OrderPlacedIntegrationEvent(
            Guid.NewGuid(),
            now,
            tenantContext.RequiredTenantId,
            order.Id,
            order.CustomerName,
            order.CustomerEmail,
            order.Total,
            order.ToLineItems()));

        await dbContext.SaveChangesAsync(cancellationToken); // atomic: order + outbox row, or neither

        return OrderResponse.From(order);
    }
}
