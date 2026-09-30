using System.Reflection;
using System.Text.Json;
using BuildingBlocks.Messaging;
using BuildingBlocks.MultiTenancy;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Outbox;

public sealed class OutboxProcessorOptions
{
    /// <summary>Set to false on replicas that should serve HTTP only (run exactly one outbox worker per deployment).</summary>
    public bool Enabled { get; set; } = true;

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int BatchSize { get; set; } = 20;

    /// <summary>After this many failed deliveries a message is parked (left unprocessed with its last error) for an operator to inspect.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Integration event type name -> CLR type, used to deserialize outbox content.
    /// Populated by each module's registration (<see cref="RegisterEventsFromAssembly"/>)
    /// so the processor never needs a compile-time reference to any module's event types.
    /// </summary>
    public Dictionary<string, Type> EventTypes { get; } = new();

    /// <summary>Registers every concrete <see cref="IIntegrationEvent"/> in a module's Contracts assembly.</summary>
    public OutboxProcessorOptions RegisterEventsFromAssembly(Assembly contractsAssembly)
    {
        var eventTypes = contractsAssembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IIntegrationEvent).IsAssignableFrom(t));

        foreach (var eventType in eventTypes)
        {
            EventTypes[eventType.FullName!] = eventType;
        }

        return this;
    }
}

/// <summary>
/// Polls every registered <see cref="IOutboxStore"/> (one per module) and republishes
/// due messages in-process via MediatR. This is the mechanism that lets Inventory react
/// to a Sales event without Sales ever referencing Inventory's Application/Infrastructure.
/// Broker-free by design: swapping in RabbitMQ/Azure Service Bus later only changes what
/// happens inside DispatchAsync, not the outbox schema or the write side.
///
/// Delivery is at-least-once: a message whose handlers partly succeed is retried as a
/// whole, so every consumer must be idempotent (keyed on OrderId, EventId, etc.).
/// </summary>
public sealed class OutboxProcessor(
    IServiceProvider serviceProvider,
    IOptions<OutboxProcessorOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly OutboxProcessorOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Outbox processor disabled on this instance");
            return;
        }

        using var timer = new PeriodicTimer(_options.PollingInterval);

        do
        {
            await ProcessAllStoresAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessAllStoresAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var stores = scope.ServiceProvider.GetServices<IOutboxStore>();

        foreach (var store in stores)
        {
            try
            {
                await ProcessStoreAsync(store, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A single module's outbox being unreachable (e.g. mid-deploy schema
                // migration) must never stop the processor from polling every other
                // module — and must never bring down the whole host, since this is a
                // BackgroundService and an unobserved fault here stops the app.
                logger.LogError(ex, "Failed to poll outbox for module {Module}", store.ModuleName);
            }
        }
    }

    private async Task ProcessStoreAsync(IOutboxStore store, CancellationToken cancellationToken)
    {
        var messages = await store.GetUnprocessedAsync(_options.BatchSize, _options.MaxAttempts, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await DispatchAsync(message, cancellationToken);
                await store.MarkProcessedAsync(message.Id, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to process outbox message {MessageId} ({MessageType}) from module {Module}",
                    message.Id, message.Type, store.ModuleName);
                await store.MarkFailedAsync(message.Id, ex.Message, cancellationToken);
            }
        }
    }

    private async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!_options.EventTypes.TryGetValue(message.Type, out var eventType))
        {
            throw new InvalidOperationException($"No registered CLR type for outbox message type '{message.Type}'.");
        }

        var @event = (IIntegrationEvent?)JsonSerializer.Deserialize(message.Content, eventType)
            ?? throw new InvalidOperationException($"Failed to deserialize outbox message {message.Id} as {eventType.Name}");

        // A fresh scope per message: each handler gets clean DbContexts, and the
        // tenant the event was raised in is restored so tenant query filters apply
        // exactly as they would for the HTTP request that caused it.
        using var dispatchScope = serviceProvider.CreateScope();
        dispatchScope.ServiceProvider.GetService<ITenantSetter>()?.SetTenant(@event.TenantId);

        using (logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = @event.TenantId, ["EventId"] = @event.EventId }))
        {
            await dispatchScope.ServiceProvider.GetRequiredService<IPublisher>().Publish(@event, cancellationToken);
        }
    }
}
