# Structural Patterns — C# Reference
## Source: https://refactoring.guru/design-patterns/structural-patterns/csharp/example

---

## Adapter
**Intent:** Allow objects with incompatible interfaces to collaborate.
**refactoring.guru:** https://refactoring.guru/design-patterns/adapter/csharp/example

**When to use:**
- Integrating a third-party library or legacy system with your interface
- You want to reuse an existing class but its interface doesn't match what you need
- Building an Anti-Corruption Layer between bounded contexts

```csharp
// Your domain interface — what YOUR system expects
public interface IPaymentProvider
{
    Task<PaymentResult> ChargeAsync(decimal amount, string currency, string cardToken);
}

// Third-party SDK — incompatible interface you can't change
public class StripeClient
{
    public StripeChargeResponse CreateCharge(StripeChargeRequest request) { ... }
}

// Adapter — makes StripeClient conform to IPaymentProvider
public sealed class StripeAdapter : IPaymentProvider
{
    private readonly StripeClient _stripe;

    public StripeAdapter(StripeClient stripe) { _stripe = stripe; }

    public Task<PaymentResult> ChargeAsync(decimal amount, string currency, string cardToken)
    {
        var request  = new StripeChargeRequest
        {
            Amount   = (int)(amount * 100),   // Stripe uses cents
            Currency = currency.ToLower(),
            Source   = cardToken
        };
        var response = _stripe.CreateCharge(request);
        return Task.FromResult(new PaymentResult(response.Id, response.Status == "succeeded"));
    }
}

// Registration — your code never knows about Stripe
builder.Services.AddScoped<IPaymentProvider, StripeAdapter>();
```

---

## Decorator
**Intent:** Attach new behaviours to objects by placing them inside wrapper objects.
**refactoring.guru:** https://refactoring.guru/design-patterns/decorator/csharp/example

**When to use:**
- Adding responsibilities to objects without subclassing
- Cross-cutting concerns: caching, logging, retry, validation around a service
- When inheritance is impractical (sealed classes, multiple behaviours needed)

```csharp
// Component interface
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct);
    Task         AddAsync(Order order, CancellationToken ct);
}

// Concrete component
public sealed class SqlOrderRepository : IOrderRepository { ... }

// Decorator — adds caching transparently
public sealed class CachedOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly IMemoryCache     _cache;

    public CachedOrderRepository(IOrderRepository inner, IMemoryCache cache)
    { _inner = inner; _cache = cache; }

    public Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct) =>
        _cache.GetOrCreateAsync($"order:{id}", _ => _inner.GetByIdAsync(id, ct));

    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await _inner.AddAsync(order, ct);
        _cache.Remove($"order:{order.Id}");  // invalidate on write
    }
}

// Registration with Scrutor — wraps transparently
services.AddScoped<IOrderRepository, SqlOrderRepository>();
services.Decorate<IOrderRepository, CachedOrderRepository>();
```

---

## Facade
**Intent:** Provide a simplified interface to a complex subsystem.
**refactoring.guru:** https://refactoring.guru/design-patterns/facade/csharp/example

**When to use:**
- Hiding complexity of multiple collaborating services behind one entry point
- Providing a simple API to a complex subsystem for a specific use case
- Reducing dependencies between client code and subsystem internals

```csharp
// Complex subsystem — many services clients must orchestrate
public class InventoryService  { public Task ReserveAsync(OrderId id) { ... } }
public class PaymentService    { public Task ChargeAsync(OrderId id) { ... } }
public class ShippingService   { public Task ScheduleAsync(OrderId id) { ... } }
public class NotificationService { public Task NotifyAsync(OrderId id) { ... } }

// Facade — single entry point for the "place order" workflow
public sealed class OrderFacade
{
    private readonly InventoryService    _inventory;
    private readonly PaymentService      _payment;
    private readonly ShippingService     _shipping;
    private readonly NotificationService _notify;

    public OrderFacade(InventoryService i, PaymentService p,
                       ShippingService s, NotificationService n)
    { _inventory = i; _payment = p; _shipping = s; _notify = n; }

    public async Task PlaceOrderAsync(OrderId orderId)
    {
        await _inventory.ReserveAsync(orderId);
        await _payment.ChargeAsync(orderId);
        await _shipping.ScheduleAsync(orderId);
        await _notify.NotifyAsync(orderId);
    }
}

// Client sees only one method — subsystem complexity is hidden
await facade.PlaceOrderAsync(orderId);
```

---

## Proxy
**Intent:** Provide a substitute that controls access to the original object.
**refactoring.guru:** https://refactoring.guru/design-patterns/proxy/csharp/example

**When to use:**
- Lazy initialisation (virtual proxy) — delay expensive object creation
- Access control (protection proxy) — check permissions before delegating
- Logging/auditing around an object (logging proxy)
- Caching remote results (caching proxy)

**Proxy vs Decorator:** Proxy controls ACCESS. Decorator ADDS BEHAVIOUR.
Proxy usually creates its own subject. Decorator receives it from outside.

```csharp
// Subject interface
public interface IReportService
{
    Task<Report> GenerateAsync(ReportRequest request);
}

// Real subject — expensive
public sealed class ReportService : IReportService
{
    public async Task<Report> GenerateAsync(ReportRequest request) { ... }
}

// Protection + Caching Proxy
public sealed class SecureReportProxy : IReportService
{
    private readonly IReportService      _real;
    private readonly IAuthorizationService _auth;
    private readonly IMemoryCache        _cache;

    public SecureReportProxy(IReportService real, IAuthorizationService auth, IMemoryCache cache)
    { _real = real; _auth = auth; _cache = cache; }

    public async Task<Report> GenerateAsync(ReportRequest request)
    {
        // Access control
        var authResult = await _auth.AuthorizeAsync(request.User, "ViewReports");
        if (!authResult.Succeeded) throw new UnauthorizedException();

        // Caching
        var key = $"report:{request.Type}:{request.DateRange}";
        return await _cache.GetOrCreateAsync(key, _ => _real.GenerateAsync(request));
    }
}
```

---

## Composite
**Intent:** Compose objects into tree structures and treat individual objects and compositions uniformly.
**refactoring.guru:** https://refactoring.guru/design-patterns/composite/csharp/example

**When to use:**
- Hierarchical data: file systems, menus, categories, organisational charts, UI component trees
- You want to treat leaf and container objects the same way

```csharp
// Component
public abstract class MenuComponent
{
    public string Name { get; init; } = "";
    public abstract decimal GetPrice();
    public virtual  void    Display(int depth = 0)
        => Console.WriteLine(new string('-', depth) + Name);
}

// Leaf — no children
public sealed class MenuItem : MenuComponent
{
    public decimal Price { get; init; }
    public override decimal GetPrice() => Price;
}

// Composite — has children
public sealed class MenuCategory : MenuComponent
{
    private readonly List<MenuComponent> _children = new();

    public void Add(MenuComponent c)    => _children.Add(c);
    public void Remove(MenuComponent c) => _children.Remove(c);

    public override decimal GetPrice()  => _children.Sum(c => c.GetPrice());

    public override void Display(int depth = 0)
    {
        base.Display(depth);
        foreach (var child in _children) child.Display(depth + 2);
    }
}

// Usage — treat leaf and branch the same way
var menu     = new MenuCategory { Name = "Main Menu" };
var drinks   = new MenuCategory { Name = "Drinks" };
drinks.Add(new MenuItem { Name = "Coffee", Price = 2.50m });
drinks.Add(new MenuItem { Name = "Tea",    Price = 1.80m });
menu.Add(drinks);
menu.Add(new MenuItem { Name = "Sandwich", Price = 4.50m });

Console.WriteLine(menu.GetPrice());  // 8.80 — treats all as MenuComponent
```

---

## Bridge
**Intent:** Decouple an abstraction from its implementation so both can vary independently.
**refactoring.guru:** https://refactoring.guru/design-patterns/bridge/csharp/example

**Bridge vs Adapter:** Adapter makes incompatible things work together (after the fact).
Bridge is designed up front to let both sides evolve independently.

```csharp
// Implementation interface
public interface IMessageSender
{
    Task SendAsync(string to, string content);
}

// Concrete implementations
public sealed class EmailSender : IMessageSender { ... }
public sealed class SmsSender   : IMessageSender { ... }

// Abstraction — uses the implementation
public abstract class Notification
{
    protected readonly IMessageSender Sender;
    protected Notification(IMessageSender sender) { Sender = sender; }
    public abstract Task NotifyAsync(string recipient, string message);
}

// Refined abstractions — vary independently of sender
public sealed class UrgentNotification : Notification
{
    public UrgentNotification(IMessageSender sender) : base(sender) { }
    public override Task NotifyAsync(string recipient, string message)
        => Sender.SendAsync(recipient, $"URGENT: {message}");
}

public sealed class ScheduledNotification : Notification
{
    private readonly DateTime _sendAt;
    public ScheduledNotification(IMessageSender sender, DateTime sendAt) : base(sender)
        => _sendAt = sendAt;
    public override async Task NotifyAsync(string recipient, string message)
    {
        await Task.Delay(_sendAt - DateTime.UtcNow);
        await Sender.SendAsync(recipient, message);
    }
}

// Mix and match freely — 2 abstractions × N implementations, no class explosion
var notification = new UrgentNotification(new SmsSender());
```
