# Behavioral Patterns — C# Reference
## Source: https://refactoring.guru/design-patterns/behavioral-patterns/csharp/example

---

## Strategy
**Intent:** Define a family of algorithms, put each in a class, make them interchangeable.
**refactoring.guru:** https://refactoring.guru/design-patterns/strategy/csharp/example

**When to use:**
- Eliminate a growing `switch` or `if/else` chain on a type
- Switch algorithms at runtime without changing the context
- Isolate business rules from the code that uses them

```csharp
// Strategy interface
public interface IDiscountStrategy
{
    decimal Apply(decimal orderTotal);
}

// Concrete strategies
public sealed class NoDiscount        : IDiscountStrategy { public decimal Apply(decimal t) => t; }
public sealed class PercentageDiscount : IDiscountStrategy
{
    private readonly decimal _percent;
    public PercentageDiscount(decimal percent) { _percent = percent; }
    public decimal Apply(decimal t) => t * (1 - _percent / 100);
}
public sealed class FixedDiscount      : IDiscountStrategy
{
    private readonly decimal _amount;
    public FixedDiscount(decimal amount) { _amount = amount; }
    public decimal Apply(decimal t) => Math.Max(0, t - _amount);
}

// Context — uses whichever strategy is injected
public sealed class OrderPricer
{
    private readonly IDiscountStrategy _strategy;
    public OrderPricer(IDiscountStrategy strategy) { _strategy = strategy; }
    public decimal Price(decimal total) => _strategy.Apply(total);
}

// .NET 8 Keyed Services — select strategy by key
builder.Services.AddKeyedScoped<IDiscountStrategy, NoDiscount>("none");
builder.Services.AddKeyedScoped<IDiscountStrategy, PercentageDiscount>("vip");
builder.Services.AddKeyedScoped<IDiscountStrategy, FixedDiscount>("promo");
```

---

## Observer
**Intent:** Define a one-to-many dependency — when one object changes state, all dependents are notified.
**refactoring.guru:** https://refactoring.guru/design-patterns/observer/csharp/example

**In .NET:** Domain Events + MediatR IS the Observer pattern. INotification = Subject, INotificationHandler = Observer.

```csharp
// Subject interface
public interface IStockSubject
{
    void Subscribe(IStockObserver observer);
    void Unsubscribe(IStockObserver observer);
    void NotifyObservers();
}

// Observer interface
public interface IStockObserver
{
    void Update(string symbol, decimal price);
}

// Concrete subject
public sealed class StockMarket : IStockSubject
{
    private readonly List<IStockObserver> _observers = new();
    private readonly Dictionary<string, decimal> _prices = new();

    public void Subscribe(IStockObserver o)   => _observers.Add(o);
    public void Unsubscribe(IStockObserver o) => _observers.Remove(o);

    public void UpdatePrice(string symbol, decimal price)
    {
        _prices[symbol] = price;
        NotifyObservers();
    }

    public void NotifyObservers()
    {
        foreach (var (symbol, price) in _prices)
            foreach (var observer in _observers)
                observer.Update(symbol, price);
    }
}

// Domain Events (preferred .NET approach — MediatR Observer)
public sealed record StockPriceChangedEvent(string Symbol, decimal Price) : IDomainEvent;

public sealed class AlertHandler : INotificationHandler<StockPriceChangedEvent>
{
    public Task Handle(StockPriceChangedEvent evt, CancellationToken ct)
    {
        if (evt.Price < 10) Console.WriteLine($"ALERT: {evt.Symbol} below threshold!");
        return Task.CompletedTask;
    }
}
```

---

## Command
**Intent:** Encapsulate a request as an object — enables queuing, undo/redo, logging.
**refactoring.guru:** https://refactoring.guru/design-patterns/command/csharp/example

**In .NET:** MediatR IRequest + IRequestHandler IS the Command pattern.

```csharp
// Command interface
public interface ICommand
{
    void Execute();
    void Undo();
}

// Concrete commands — undo/redo example
public sealed class MoveShapeCommand : ICommand
{
    private readonly Shape _shape;
    private readonly Point _from;
    private readonly Point _to;

    public MoveShapeCommand(Shape shape, Point to)
    {
        _shape = shape;
        _from  = shape.Position;
        _to    = to;
    }

    public void Execute() => _shape.MoveTo(_to);
    public void Undo()    => _shape.MoveTo(_from);
}

// Invoker — manages command history
public sealed class CommandHistory
{
    private readonly Stack<ICommand> _history = new();

    public void Execute(ICommand cmd)
    {
        cmd.Execute();
        _history.Push(cmd);
    }

    public void Undo()
    {
        if (_history.TryPop(out var cmd))
            cmd.Undo();
    }
}
```

---

## Chain of Responsibility
**Intent:** Pass requests along a chain of handlers — each decides to process or pass on.
**refactoring.guru:** https://refactoring.guru/design-patterns/chain-of-responsibility/csharp/example

**In .NET:** ASP.NET Core Middleware IS Chain of Responsibility.
MediatR pipeline behaviours IS Chain of Responsibility.

```csharp
// Handler interface
public abstract class OrderValidator
{
    protected OrderValidator? Next { get; private set; }

    public OrderValidator SetNext(OrderValidator next)
    {
        Next = next;
        return next;  // enables fluent chaining
    }

    public abstract ValidationResult Handle(Order order);

    protected ValidationResult PassToNext(Order order)
        => Next?.Handle(order) ?? ValidationResult.Success();
}

// Concrete handlers
public sealed class StockValidator : OrderValidator
{
    public override ValidationResult Handle(Order order) =>
        order.Lines.All(l => l.IsInStock)
            ? PassToNext(order)
            : ValidationResult.Failure("One or more items are out of stock.");
}

public sealed class PaymentValidator : OrderValidator
{
    public override ValidationResult Handle(Order order) =>
        order.Customer.HasValidPaymentMethod
            ? PassToNext(order)
            : ValidationResult.Failure("No valid payment method on file.");
}

// Build the chain
var chain = new StockValidator();
chain.SetNext(new PaymentValidator())
     .SetNext(new AddressValidator());

var result = chain.Handle(order);
```

---

## State
**Intent:** Let an object alter its behaviour when its internal state changes.
**refactoring.guru:** https://refactoring.guru/design-patterns/state/csharp/example

**Strategy vs State:** Strategy = externally swapped algorithm. State = self-managing internal transitions.

```csharp
// State interface
public interface IOrderState
{
    void Confirm(Order order);
    void Ship(Order order);
    void Cancel(Order order);
}

// Concrete states
public sealed class PendingState : IOrderState
{
    public void Confirm(Order order) => order.TransitionTo(new ConfirmedState());
    public void Ship(Order order)    => throw new DomainException("Cannot ship a pending order.");
    public void Cancel(Order order)  => order.TransitionTo(new CancelledState());
}

public sealed class ConfirmedState : IOrderState
{
    public void Confirm(Order order) => throw new DomainException("Already confirmed.");
    public void Ship(Order order)    => order.TransitionTo(new ShippedState());
    public void Cancel(Order order)  => order.TransitionTo(new CancelledState());
}

public sealed class ShippedState : IOrderState
{
    public void Confirm(Order order) => throw new DomainException("Already shipped.");
    public void Ship(Order order)    => throw new DomainException("Already shipped.");
    public void Cancel(Order order)  => throw new DomainException("Cannot cancel shipped order.");
}

// Context
public sealed class Order
{
    private IOrderState _state = new PendingState();

    public void TransitionTo(IOrderState state) => _state = state;
    public void Confirm() => _state.Confirm(this);
    public void Ship()    => _state.Ship(this);
    public void Cancel()  => _state.Cancel(this);
}
```

---

## Template Method
**Intent:** Define the skeleton of an algorithm in a base class, let subclasses fill in the steps.
**refactoring.guru:** https://refactoring.guru/design-patterns/template-method/csharp/example

**Template Method vs Strategy:** Template = inheritance-based partial override. Strategy = full algorithm replacement via composition.

```csharp
// Abstract class defines the algorithm skeleton
public abstract class ReportExporter
{
    // Template method — sealed so subclasses cannot change the overall flow
    public sealed void Export(ReportData data, string path)
    {
        var formatted = Format(data);   // abstract — must override
        var header    = AddHeader();    // virtual — optional override
        WriteToFile(header + formatted, path);  // final — same for all
    }

    protected abstract string Format(ReportData data);

    protected virtual string AddHeader() => $"Generated: {DateTime.UtcNow:u}\n";

    private void WriteToFile(string content, string path)
        => File.WriteAllText(path, content);
}

// Concrete implementations fill in only what differs
public sealed class CsvExporter : ReportExporter
{
    protected override string Format(ReportData data)
        => string.Join("\n", data.Rows.Select(r => string.Join(",", r.Values)));
}

public sealed class JsonExporter : ReportExporter
{
    protected override string Format(ReportData data)
        => JsonSerializer.Serialize(data.Rows);

    protected override string AddHeader() => "";  // JSON has no header
}
```

---

## Mediator
**Intent:** Reduce dependencies between objects by routing all communication through a central hub.
**refactoring.guru:** https://refactoring.guru/design-patterns/mediator/csharp/example

**In .NET:** MediatR library IS the Mediator pattern.

```csharp
// Without Mediator — tight coupling, components know each other
orderForm.Submit(); // → calls PaymentService directly, InventoryService directly, NotificationService...

// With MediatR Mediator — components only know the mediator
public sealed record PlaceOrderCommand(CustomerId CustomerId, OrderLineRequest[] Lines)
    : IRequest<OrderId>;

public sealed class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, OrderId>
{
    // Only knows about what it needs — no direct coupling to other handlers
    private readonly IOrderRepository _repo;
    public PlaceOrderHandler(IOrderRepository repo) { _repo = repo; }

    public async Task<OrderId> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Place(cmd.CustomerId, cmd.Lines);
        await _repo.AddAsync(order, ct);
        return order.Id;
    }
}

// Sender — the mediator
await _sender.Send(new PlaceOrderCommand(customerId, lines));
// MediatR routes to the handler — caller doesn't know who handles it
```

---

## Visitor
**Intent:** Separate an algorithm from the object structure it operates on.
**refactoring.guru:** https://refactoring.guru/design-patterns/visitor/csharp/example

**When to use:**
- You need to perform many distinct operations on an object hierarchy without polluting the classes
- The object structure is stable but you frequently add new operations

```csharp
// Element interface
public interface IReportElement { void Accept(IReportVisitor visitor); }

// Concrete elements
public sealed class TextBlock  : IReportElement { public void Accept(IReportVisitor v) => v.Visit(this); public string Content { get; init; } = ""; }
public sealed class ImageBlock : IReportElement { public void Accept(IReportVisitor v) => v.Visit(this); public string Url     { get; init; } = ""; }
public sealed class TableBlock : IReportElement { public void Accept(IReportVisitor v) => v.Visit(this); public string[][] Data { get; init; } = Array.Empty<string[]>(); }

// Visitor interface — one Visit per element type
public interface IReportVisitor
{
    void Visit(TextBlock  block);
    void Visit(ImageBlock block);
    void Visit(TableBlock block);
}

// Concrete visitors — add new operations without touching elements
public sealed class HtmlExportVisitor : IReportVisitor
{
    public void Visit(TextBlock  b) => Console.WriteLine($"<p>{b.Content}</p>");
    public void Visit(ImageBlock b) => Console.WriteLine($"<img src='{b.Url}'/>");
    public void Visit(TableBlock b) => Console.WriteLine("<table>...</table>");
}

public sealed class WordCountVisitor : IReportVisitor
{
    public int Count { get; private set; }
    public void Visit(TextBlock  b) => Count += b.Content.Split(' ').Length;
    public void Visit(ImageBlock b) { }  // images have no words
    public void Visit(TableBlock b) => Count += b.Data.Sum(r => r.Length);
}
```
