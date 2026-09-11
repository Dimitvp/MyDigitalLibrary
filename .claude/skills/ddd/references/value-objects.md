# Value Objects — Reference

## C# record — Preferred for Simple Value Objects

```csharp
// C# record gives structural equality, immutability, and ToString for free
public sealed record Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email cannot be empty.");
        if (!value.Contains('@') || !value.Contains('.'))
            throw new DomainException($"'{value}' is not a valid email address.");
        Value = value.ToLowerInvariant().Trim();
    }

    public static implicit operator string(Email email) => email.Value;
    public override string ToString() => Value;
}

// Usage — clean, no primitive obsession
public sealed class User : AggregateRoot<UserId>
{
    public Email Email { get; private set; }
    // ...
}
```

## Complex Value Object with Behaviour

```csharp
public sealed record Money
{
    public decimal  Amount   { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        if (amount < 0)
            throw new DomainException("Money amount cannot be negative.");
        Amount   = Math.Round(amount, 2);
        Currency = currency;
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        if (other.Amount > Amount)
            throw new DomainException("Cannot subtract: result would be negative.");
        return new(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public bool IsGreaterThan(Money other)  { EnsureSameCurrency(other); return Amount > other.Amount; }
    public bool IsZero                      => Amount == 0;

    public static Money Zero(Currency currency) => new(0, currency);
    public static Money Sum(IEnumerable<Money> values)
        => values.Aggregate((a, b) => a.Add(b));

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException($"Cannot operate on {Currency} and {other.Currency}.");
    }

    public override string ToString() => $"{Amount:N2} {Currency}";
}
```

## Address Value Object

```csharp
public sealed record Address
{
    public string Street  { get; }
    public string City    { get; }
    public string Country { get; }
    public string ZipCode { get; }

    public Address(string street, string city, string country, string zipCode)
    {
        Street  = Guard.AgainstNullOrEmpty(street,  nameof(street));
        City    = Guard.AgainstNullOrEmpty(city,    nameof(city));
        Country = Guard.AgainstNullOrEmpty(country, nameof(country));
        ZipCode = Guard.AgainstNullOrEmpty(zipCode, nameof(zipCode));
    }

    public Address WithCity(string newCity) => this with { City = newCity };  // non-destructive update

    public override string ToString() => $"{Street}, {City}, {ZipCode}, {Country}";
}
```

## EF Core Owned Entity Mapping

```csharp
// All Value Objects mapped as Owned Entities — no separate table, no Id column
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.OwnsOne(c => c.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email")
                .HasMaxLength(256)
                .IsRequired();
        });

        builder.OwnsOne(c => c.Address, address =>
        {
            address.Property(a => a.Street).HasColumnName("Street").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("City").HasMaxLength(100);
            address.Property(a => a.Country).HasColumnName("Country").HasMaxLength(100);
            address.Property(a => a.ZipCode).HasColumnName("ZipCode").HasMaxLength(20);
        });

        builder.OwnsOne(c => c.BillingAddress, address =>
        {
            address.Property(a => a.Street).HasColumnName("BillingStreet").HasMaxLength(200);
            // etc.
        });
    }
}
```

## Value Object Base Class (when record is not suitable)

```csharp
// For Value Objects needing custom equality on a subset of properties
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        return ((ValueObject)obj).GetEqualityComponents()
            .SequenceEqual(GetEqualityComponents());
    }

    public override int GetHashCode()
        => GetEqualityComponents()
            .Aggregate(0, (hash, obj) => HashCode.Combine(hash, obj));

    public static bool operator ==(ValueObject? a, ValueObject? b)
        => a is null ? b is null : a.Equals(b);

    public static bool operator !=(ValueObject? a, ValueObject? b) => !(a == b);
}
```
