namespace Inventory.Contracts;

/// <summary>
/// Synchronous cross-module contract, owned and implemented by Inventory.
/// Sales references only this project (Inventory.Contracts) to call it —
/// never Inventory.Application/Domain/Infrastructure. The concrete
/// implementation is registered into DI by InventoryModule.
///
/// This is a fast pre-check that gives the caller immediate feedback. The
/// authoritative, race-free reservation happens asynchronously when Inventory
/// handles Sales' OrderPlacedIntegrationEvent.
/// </summary>
public interface IInventoryAvailabilityChecker
{
    /// <summary>The SKUs (normalised) whose available quantity is below what was requested. Empty when everything is available.</summary>
    Task<IReadOnlyCollection<string>> FindUnavailableAsync(IReadOnlyCollection<StockRequirement> requirements, CancellationToken cancellationToken);
}

public sealed record StockRequirement(string Sku, int Quantity);
