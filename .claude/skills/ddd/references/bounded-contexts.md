# Bounded Contexts — Reference

## Solution Structure — One Context per Module

```
src/
├── Ordering/                          ← Bounded Context
│   ├── Ordering.Domain/
│   ├── Ordering.Application/
│   ├── Ordering.Infrastructure/
│   └── Ordering.Api/
├── Billing/
│   ├── Billing.Domain/
│   ├── Billing.Application/
│   └── Billing.Infrastructure/
└── SharedKernel/                      ← Base classes + truly shared primitives ONLY
    ├── ValueObjects/                  ← Money, Address, Email
    ├── Events/                        ← IDomainEvent, IIntegrationEvent
    └── Common/                        ← AggregateRoot, Entity, ValueObject base classes
```

## Integration Events — Cross-Context Communication

```csharp
// Ordering context raises an Integration Event after saving
public sealed record OrderConfirmedIntegrationEvent(
    Guid    OrderId,
    Guid    CustomerId,
    decimal TotalAmount,
    string  Currency,
    DateTime ConfirmedAt) : IIntegrationEvent;

// Billing context subscribes — translates via ACL
public sealed class OrderConfirmedHandler
    : IIntegrationEventHandler<OrderConfirmedIntegrationEvent>
{
    private readonly IBillingService _billing;

    public async Task Handle(OrderConfirmedIntegrationEvent evt, CancellationToken ct)
    {
        var invoice = Invoice.CreateForOrder(
            orderId:    BillingOrderId.From(evt.OrderId),
            customerId: BillingCustomerId.From(evt.CustomerId),
            amount:     new Money(evt.TotalAmount, Currency.From(evt.Currency)),
            issuedAt:   evt.ConfirmedAt);

        await _billing.IssueInvoiceAsync(invoice, ct);
    }
}
```

## Anti-Corruption Layer (ACL)

```csharp
// Ordering consuming Catalog — ACL translates Catalog's model into Ordering's language
public sealed class CatalogAcl
{
    private readonly ICatalogClient _client;
    public CatalogAcl(ICatalogClient client) { _client = client; }

    public async Task<(ProductId, ProductName, Money)> GetOrderableProductAsync(
        Guid catalogProductId, CancellationToken ct)
    {
        var info = await _client.GetProductInfoAsync(catalogProductId, ct)
            ?? throw new NotFoundException("Catalog product", catalogProductId);

        return (
            ProductId.From(info.Id),
            new ProductName(info.Name),
            new Money(info.Price, Currency.From(info.CurrencyCode))
        );
    }
}
```

## Shared Kernel — What Belongs and What Doesn't

```
✅ IN SharedKernel:
  - AggregateRoot<TId>, Entity<TId>, ValueObject base classes
  - IDomainEvent, IIntegrationEvent, IRepository<T> interfaces
  - Truly cross-context value objects: Money, Email, Address
  - Guard clause helpers

❌ NOT IN SharedKernel:
  - Domain logic specific to one context
  - Application services, commands, queries
  - Infrastructure: DbContext, EF configurations
  - Anything that changes when one context's business rules change
```
