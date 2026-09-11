# Creational Patterns — C# Reference
## Source: https://refactoring.guru/design-patterns/creational-patterns/csharp/example

---

## Factory Method
**Intent:** Define an interface for creating an object, but let subclasses decide which class to instantiate.
**refactoring.guru:** https://refactoring.guru/design-patterns/factory-method/csharp/example

**When to use:**
- You don't know ahead of time what class you need to instantiate
- You want subclasses to specify the type of objects they create
- You want to provide a library of products and reveal only their interfaces, not implementation

**When NOT to use:**
- You only have one concrete type now and the variation is hypothetical (YAGNI)
- Simple `new ConcreteClass()` is clear and sufficient

```csharp
// Product interface
public interface INotification
{
    void Send(string message, string recipient);
}

// Concrete products
public sealed class EmailNotification : INotification
{
    public void Send(string message, string recipient)
        => Console.WriteLine($"Email to {recipient}: {message}");
}

public sealed class SmsNotification : INotification
{
    public void Send(string message, string recipient)
        => Console.WriteLine($"SMS to {recipient}: {message}");
}

// Creator — declares the factory method
public abstract class NotificationSender
{
    // Factory method — subclasses override this
    protected abstract INotification CreateNotification();

    // Template method using the factory method
    public void Notify(string message, string recipient)
    {
        var notification = CreateNotification();
        notification.Send(message, recipient);
    }
}

// Concrete creators
public sealed class EmailNotificationSender : NotificationSender
{
    protected override INotification CreateNotification() => new EmailNotification();
}

public sealed class SmsNotificationSender : NotificationSender
{
    protected override INotification CreateNotification() => new SmsNotification();
}

// Usage
NotificationSender sender = new EmailNotificationSender();
sender.Notify("Your order shipped!", "alice@example.com");
```

---

## Abstract Factory
**Intent:** Produce families of related objects without specifying their concrete classes.
**refactoring.guru:** https://refactoring.guru/design-patterns/abstract-factory/csharp/example

**When to use:**
- System must be independent of how its products are created
- System needs to work with multiple families of products (e.g. SQL Server vs PostgreSQL, Stripe vs PayPal)
- You want to enforce that products from one family are used together

```csharp
// Abstract products
public interface IButton   { void Render(); }
public interface ICheckbox { void Render(); }

// Concrete products — Windows family
public sealed class WindowsButton   : IButton   { public void Render() => Console.WriteLine("Windows Button"); }
public sealed class WindowsCheckbox : ICheckbox { public void Render() => Console.WriteLine("Windows Checkbox"); }

// Concrete products — macOS family
public sealed class MacButton   : IButton   { public void Render() => Console.WriteLine("Mac Button"); }
public sealed class MacCheckbox : ICheckbox { public void Render() => Console.WriteLine("Mac Checkbox"); }

// Abstract factory
public interface IUIFactory
{
    IButton   CreateButton();
    ICheckbox CreateCheckbox();
}

// Concrete factories
public sealed class WindowsUIFactory : IUIFactory
{
    public IButton   CreateButton()   => new WindowsButton();
    public ICheckbox CreateCheckbox() => new WindowsCheckbox();
}

public sealed class MacUIFactory : IUIFactory
{
    public IButton   CreateButton()   => new MacButton();
    public ICheckbox CreateCheckbox() => new MacCheckbox();
}

// Client — works with factories and products only through interfaces
public class Application
{
    private readonly IButton   _button;
    private readonly ICheckbox _checkbox;

    public Application(IUIFactory factory)
    {
        _button   = factory.CreateButton();
        _checkbox = factory.CreateCheckbox();
    }
}

// DI Registration — swap the factory to switch the entire UI family
builder.Services.AddSingleton<IUIFactory, WindowsUIFactory>();
```

---

## Builder
**Intent:** Construct complex objects step by step.
**refactoring.guru:** https://refactoring.guru/design-patterns/builder/csharp/example

**When to use:**
- Object construction is complex and has many optional parameters (avoids telescoping constructors)
- You want to produce different representations of the same product
- You need to build a product step by step, controlling the process

```csharp
// Product
public sealed class QueryRequest
{
    public string Table      { get; init; } = "";
    public List<string> Columns { get; init; } = new();
    public string? WhereClause { get; init; }
    public string? OrderBy     { get; init; }
    public int?    Limit        { get; init; }
}

// Builder interface
public interface IQueryBuilder
{
    IQueryBuilder FromTable(string table);
    IQueryBuilder SelectColumns(params string[] columns);
    IQueryBuilder Where(string clause);
    IQueryBuilder OrderBy(string column);
    IQueryBuilder Limit(int count);
    QueryRequest Build();
}

// Concrete builder
public sealed class QueryBuilder : IQueryBuilder
{
    private string          _table   = "";
    private List<string>    _columns = new();
    private string?         _where;
    private string?         _orderBy;
    private int?            _limit;

    public IQueryBuilder FromTable(string table)          { _table   = table;   return this; }
    public IQueryBuilder SelectColumns(params string[] c) { _columns.AddRange(c); return this; }
    public IQueryBuilder Where(string clause)             { _where   = clause;  return this; }
    public IQueryBuilder OrderBy(string column)           { _orderBy = column;  return this; }
    public IQueryBuilder Limit(int count)                 { _limit   = count;   return this; }

    public QueryRequest Build() => new QueryRequest
    {
        Table       = _table,
        Columns     = _columns,
        WhereClause = _where,
        OrderBy     = _orderBy,
        Limit       = _limit
    };
}

// Usage — readable, no 7-parameter constructor
var query = new QueryBuilder()
    .FromTable("Orders")
    .SelectColumns("Id", "Status", "Total")
    .Where("Status = 'Pending'")
    .OrderBy("CreatedAt DESC")
    .Limit(100)
    .Build();
```

---

## Singleton
**Intent:** Ensure a class has only one instance, provide a global access point.
**refactoring.guru:** https://refactoring.guru/design-patterns/singleton/csharp/example

**When to use:** Shared stateful resource with strict single-instance requirement.
**In .NET:** Prefer `AddSingleton<T>()` in DI over implementing Singleton manually.
**Warning:** Singleton is often overused and hides dependencies — always prefer DI.

```csharp
// ✅ Thread-safe lazy Singleton (only if DI is genuinely not available)
public sealed class ConnectionPool
{
    private static readonly Lazy<ConnectionPool> _instance =
        new(() => new ConnectionPool(), LazyThreadSafetyMode.ExecutionAndPublication);

    private ConnectionPool() { }  // private constructor

    public static ConnectionPool Instance => _instance.Value;
}

// ✅ PREFERRED — let the DI container manage singleton lifetime
builder.Services.AddSingleton<IConnectionPool, ConnectionPool>();
```

---

## Prototype
**Intent:** Clone existing objects without depending on their concrete classes.
**refactoring.guru:** https://refactoring.guru/design-patterns/prototype/csharp/example

**When to use:**
- Object creation is expensive (DB lookup, complex calculation) and you need variations of it
- You need many similar objects that differ only in a few fields

```csharp
// C# records make Prototype trivial with non-destructive mutation
public sealed record OrderTemplate
{
    public Currency Currency  { get; init; }
    public Address  ShipFrom  { get; init; } = null!;
    public string   Terms     { get; init; } = "";
}

// Prototype — clone and change only what differs
var standardTemplate = new OrderTemplate
{
    Currency = Currency.EUR,
    ShipFrom = Address.Warehouse,
    Terms    = "Net 30"
};

var expressTemplate = standardTemplate with { Terms = "Express — immediate payment" };
var usdTemplate     = standardTemplate with { Currency = Currency.USD };
```
