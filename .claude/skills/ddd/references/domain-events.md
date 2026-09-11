# Domain Events — Reference

## Event Interface & Base

```csharp
// IDomainEvent.cs — marker interface in Domain layer
public interface IDomainEvent : INotification  // INotification = MediatR
{
    DateTime OccurredAt { get; }
}

// Base record for all domain events
public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

## Raising & Dispatching — Save-First Pattern

```csharp
// DomainEventDispatcher.cs — Application layer
public sealed class DomainEventDispatcher
{
    private readonly IPublisher _publisher;  // MediatR
    public DomainEventDispatcher(IPublisher publisher) { _publisher = publisher; }

    public async Task DispatchAsync(
        IEnumerable<AggregateRoot> aggregates, CancellationToken ct)
    {
        var events = aggregates
            .SelectMany(a => a.DomainEvents)
            .OrderBy(e => e.OccurredAt)
            .ToList();

        foreach (var agg in aggregates)
            agg.ClearDomainEvents();

        foreach (var @event in events)
            await _publisher.Publish(@event, ct);
    }
}

// Application handler — save FIRST, dispatch AFTER
public sealed class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, OrderId>
{
    private readonly IOrderRepository      _repo;
    private readonly DomainEventDispatcher _dispatcher;

    public async Task<OrderId> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Place(cmd.CustomerId, cmd.Lines);

        await _repo.AddAsync(order, ct);               // 1. persist — if this fails, no events fired
        await _dispatcher.DispatchAsync([order], ct);  // 2. dispatch after successful save

        return order.Id;
    }
}
```

## Domain Event Handlers

```csharp
// Each handler has one responsibility — decoupled side effect
public sealed class SendOrderConfirmationHandler
    : INotificationHandler<OrderPlacedEvent>
{
    private readonly IEmailService _email;
    private readonly ICustomerRepository _customers;

    public async Task Handle(OrderPlacedEvent evt, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(evt.CustomerId, ct);
        await _email.SendOrderConfirmationAsync(customer.Email, evt.OrderId, ct);
    }
}

public sealed class ReserveInventoryHandler
    : INotificationHandler<OrderPlacedEvent>
{
    private readonly IInventoryService _inventory;

    public async Task Handle(OrderPlacedEvent evt, CancellationToken ct)
        => await _inventory.ReserveForOrderAsync(evt.OrderId, evt.Lines, ct);
}
```

## Outbox Pattern — Guaranteed Delivery

For events that must survive a process crash (cross-service integration events):

```csharp
// OutboxMessage.cs — stored in same transaction as the aggregate
public sealed class OutboxMessage
{
    public Guid     Id           { get; set; } = Guid.NewGuid();
    public string   Type         { get; set; } = "";   // fully qualified event type name
    public string   Payload      { get; set; } = "";   // JSON-serialised event
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }         // null = not yet processed
}

// OutboxInterceptor.cs — saves events as messages in same DB transaction
public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData data, InterceptionResult<int> result, CancellationToken ct)
    {
        var outboxMessages = data.Context!.ChangeTracker
            .Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.DomainEvents)
            .Select(evt => new OutboxMessage
            {
                Type    = evt.GetType().AssemblyQualifiedName!,
                Payload = JsonSerializer.Serialize(evt, evt.GetType())
            })
            .ToList();

        data.Context.Set<OutboxMessage>().AddRange(outboxMessages);
        return base.SavingChangesAsync(data, result, ct);
    }
}

// OutboxProcessor.cs — background job reads and publishes outbox messages
public sealed class OutboxProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await using var scope = _factory.CreateAsyncScope();
            var db        = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

            var pending = await db.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .Take(20)
                .ToListAsync(ct);

            foreach (var msg in pending)
            {
                var type  = Type.GetType(msg.Type)!;
                var @event = (IDomainEvent)JsonSerializer.Deserialize(msg.Payload, type)!;
                await publisher.Publish(@event, ct);
                msg.ProcessedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
```
