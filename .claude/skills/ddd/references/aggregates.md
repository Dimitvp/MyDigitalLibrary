# Aggregates — Reference

## AggregateRoot Base Class

```csharp
// Domain/Common/AggregateRoot.cs
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    public override bool Equals(object? obj)
        => obj is Entity<TId> entity && Id.Equals(entity.Id);

    public override int GetHashCode() => Id.GetHashCode();
}
```

## Strongly-Typed IDs — Prevent ID Confusion

```csharp
// ❌ Primitive IDs allow passing wrong ID to wrong parameter
public Task<Order?> GetByIdAsync(int orderId) { ... }
GetByIdAsync(customerId);   // compiles — wrong ID, silent bug

// ✅ Strongly-typed IDs — wrong ID is a compile error
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New()            => new(Guid.NewGuid());
    public static OrderId From(Guid value) => new(value);
    public override string ToString()      => Value.ToString();
}

public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New()            => new(Guid.NewGuid());
    public static CustomerId From(Guid value) => new(value);
}

// EF Core value converter for strongly-typed IDs
public class OrderIdConverter : ValueConverter<OrderId, Guid>
{
    public OrderIdConverter() : base(id => id.Value, value => OrderId.From(value)) {}
}
```

## Invariant Enforcement Patterns

```csharp
// Guard clause library (Ardalis.GuardClauses) or custom
public static class Guard
{
    public static T AgainstNull<T>(T? value, string paramName)
        => value ?? throw new DomainException($"{paramName} cannot be null.");

    public static string AgainstNullOrEmpty(string? value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new DomainException($"{paramName} cannot be empty.")
            : value;

    public static int AgainstNegative(int value, string paramName)
        => value < 0
            ? throw new DomainException($"{paramName} cannot be negative.")
            : value;
}

// Aggregate using guards
public sealed class Product : AggregateRoot<ProductId>
{
    public ProductName Name  { get; private set; }
    public Money       Price { get; private set; }
    public StockLevel  Stock { get; private set; }

    private Product() {}

    public static Product Create(ProductName name, Money price)
    {
        return new Product
        {
            Id    = ProductId.New(),
            Name  = Guard.AgainstNull(name,  nameof(name)),
            Price = Guard.AgainstNull(price, nameof(price)),
            Stock = StockLevel.Zero
        };
    }

    public void AdjustStock(int delta)
    {
        var newLevel = Stock.Adjust(delta);
        if (newLevel.IsOutOfStock)
            RaiseDomainEvent(new ProductOutOfStockEvent(Id));
        Stock = newLevel;
    }

    public void ChangePrice(Money newPrice)
    {
        if (newPrice.Amount <= 0)
            throw new DomainException("Price must be greater than zero.");
        var oldPrice = Price;
        Price = newPrice;
        RaiseDomainEvent(new ProductPriceChangedEvent(Id, oldPrice, newPrice));
    }
}
```

## EF Core Mapping — Protecting Domain Model

```csharp
// AppDbContext.cs — all EF configuration via Fluent API, never data annotations in domain
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}

// OrderConfiguration.cs — Infrastructure layer only
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasConversion<OrderIdConverter>();

        // Map private backing field _lines
        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey("OrderId");
        builder.Navigation(o => o.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(o => o.Status)
            .HasConversion<string>();

        // Owned entity for value objects
        builder.OwnsOne(o => o.CustomerId,
            cid => cid.Property(c => c.Value).HasColumnName("CustomerId"));
    }
}
```
