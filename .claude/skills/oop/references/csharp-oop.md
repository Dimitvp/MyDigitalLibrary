# C# OOP Patterns — Reference

## Records — Value Semantics OOP

```csharp
// record — immutable, value equality, with-expression
public sealed record Money(decimal Amount, Currency Currency)
{
    // Validation in compact record constructor
    public Money
    {
        if (Amount < 0) throw new ArgumentException("Amount cannot be negative.");
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency) throw new InvalidOperationException("Currency mismatch.");
        return this with { Amount = Amount + other.Amount };  // non-destructive mutation
    }
}

// record class vs record struct
public record class  Address(string Street, string City);  // reference type — nullable
public record struct Point(int X, int Y);                  // value type — no null, stack-allocated

// When to use record vs class:
// record  → value objects, DTOs, events, immutable data
// class   → entities, services, mutable domain objects
// struct  → tiny value types (coordinates, colors, handles)
```

---

## Sealed, Abstract, Virtual — Quick Reference

```csharp
// sealed class — cannot be inherited
public sealed class PaymentConfirmation { ... }

// abstract class — cannot be instantiated, must be subclassed
public abstract class BaseRepository<T> { ... }

// sealed method — cannot be further overridden by derived classes
public class ConcreteService : BaseService
{
    public sealed override void Process() { ... }
}

// virtual method — can be overridden (default: not overridable)
public class Shape { public virtual double Area() => 0; }

// abstract method — must be overridden
public abstract class Shape { public abstract double Area(); }

// new (hiding) — shadows base method, NOT polymorphic — almost always wrong
public class Derived : Base { public new void Method() { ... } }  // ❌ avoid
```

---

## Generic Constraints — Type-Safe OOP

```csharp
// where T : class       — reference type only
// where T : struct      — value type only
// where T : new()       — must have parameterless constructor
// where T : IEntity     — must implement IEntity
// where T : BaseClass   — must inherit from BaseClass
// where T : notnull     — non-nullable

public class Repository<T> where T : class, IEntity, new()
{
    public T CreateNew() => new T();  // allowed because of new() constraint
    public Task<T?> FindAsync(int id) { ... }
}

// Combining constraints
public class EventHandler<TEvent, THandler>
    where TEvent   : IDomainEvent
    where THandler : IEventHandler<TEvent>, new()
{ ... }
```

---

## Covariant Return Types (C# 9+)

```csharp
public abstract class Animal
{
    public abstract Animal Clone();  // base return type
}

public sealed class Dog : Animal
{
    public override Dog Clone() => new Dog();  // ✅ C# 9+ — more specific return type
    // Before C# 9: had to return Animal and cast
}
```

---

## Interface Default Implementations (C# 8+)

```csharp
public interface ILogger
{
    void Log(string message, LogLevel level);

    // Default implementation — all implementors get this for free
    void LogInfo(string message)  => Log(message, LogLevel.Information);
    void LogError(string message) => Log(message, LogLevel.Error);
    void LogDebug(string message) => Log(message, LogLevel.Debug);
}

// ✅ Use for: evolving interfaces without breaking existing implementations
// ❌ Don't use for: shared state, complex logic that belongs in an abstract class
```

---

## `required` Members and `init` (C# 11/9+)

```csharp
// required — compiler enforces initialization at construction site
public class OrderDto
{
    public required OrderId    Id          { get; init; }
    public required CustomerId CustomerId  { get; init; }
    public required Money      Total       { get; init; }
    public string?             Notes       { get; init; }  // optional
}

// ✅ Usage — compiler error if required members omitted
var dto = new OrderDto
{
    Id         = OrderId.From(id),
    CustomerId = CustomerId.From(custId),
    Total      = Money.From(100m, Currency.USD)
    // Notes omitted — fine, it's optional
};
```

---

## Operator Overloading — When It Makes Sense

```csharp
// ✅ Sensible for value objects — makes domain code read naturally
public sealed record Money(decimal Amount, Currency Currency)
{
    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency) throw new InvalidOperationException();
        return a with { Amount = a.Amount + b.Amount };
    }

    public static Money operator *(Money money, decimal factor) =>
        money with { Amount = money.Amount * factor };

    public static bool operator >(Money a, Money b)  { ... }
    public static bool operator <(Money a, Money b)  { ... }
    public static bool operator >=(Money a, Money b) { ... }
    public static bool operator <=(Money a, Money b) { ... }
}

// Usage reads like business language:
var total    = unitPrice * quantity;
var discount = total * 0.1m;
var final    = total - discount;
if (final > budget) throw new BudgetExceededException();
```

---

## Extension Methods — Adding Behaviour Without Inheritance

```csharp
// ✅ Add behaviour to sealed/third-party types without subclassing
public static class StringExtensions
{
    public static bool IsValidEmail(this string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Contains('.');

    public static string ToSlug(this string text) =>
        Regex.Replace(text.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-");
}

// ✅ Add domain behaviour to IQueryable
public static class OrderQueryExtensions
{
    public static IQueryable<Order> WhereActive(this IQueryable<Order> query) =>
        query.Where(o => !o.IsDeleted && o.Status != OrderStatus.Cancelled);

    public static IQueryable<Order> ForCustomer(this IQueryable<Order> query, CustomerId id) =>
        query.Where(o => o.CustomerId == id);
}
// Usage: _db.Orders.WhereActive().ForCustomer(customerId).ToListAsync()

// ❌ Don't use extension methods to work around encapsulation
// (accessing state that should be private via public properties just for the extension)
```
