# Polymorphism — Reference

## Strategy Pattern — Most Common Polymorphism Fix

When you have `if/switch` on type — replace with a strategy:

```csharp
// ❌ Switch on type — grows with every new payment method
public class PaymentProcessor
{
    public async Task ProcessAsync(PaymentRequest request)
    {
        switch (request.Method)
        {
            case "stripe":   await ProcessStripe(request);   break;
            case "paypal":   await ProcessPayPal(request);   break;
            case "braintree":await ProcessBraintree(request);break;
            default: throw new ArgumentException($"Unknown: {request.Method}");
        }
    }
}

// ✅ Strategy — add new methods by adding a new class, not editing existing code
public interface IPaymentStrategy
{
    string Method { get; }
    Task<PaymentResult> ProcessAsync(PaymentRequest request, CancellationToken ct);
}

public sealed class StripePaymentStrategy : IPaymentStrategy
{
    public string Method => "stripe";
    public Task<PaymentResult> ProcessAsync(PaymentRequest request, CancellationToken ct) { ... }
}

// Registration — keyed services (.NET 8):
builder.Services.AddKeyedScoped<IPaymentStrategy, StripePaymentStrategy>("stripe");
builder.Services.AddKeyedScoped<IPaymentStrategy, PayPalPaymentStrategy>("paypal");

// Resolver — no switch, no if
public sealed class PaymentProcessor
{
    private readonly IServiceProvider _sp;
    public PaymentProcessor(IServiceProvider sp) { _sp = sp; }

    public Task<PaymentResult> ProcessAsync(PaymentRequest request, CancellationToken ct)
    {
        var strategy = _sp.GetKeyedService<IPaymentStrategy>(request.Method)
            ?? throw new NotSupportedException($"Payment method '{request.Method}' not supported.");
        return strategy.ProcessAsync(request, ct);
    }
}
```

---

## Virtual / Override / Abstract — Correct Usage

```csharp
public abstract class Notification
{
    // abstract — must override, no default
    public abstract string BuildMessage(string recipient);

    // virtual — can override, has sensible default
    public virtual string Subject => "Notification";

    // non-virtual sealed — same for all — no override possible
    public string FormatWithTimestamp(string message) =>
        $"[{DateTime.UtcNow:u}] {message}";

    // Template method — calls abstract/virtual members
    public void Send(string recipient)
    {
        var msg = BuildMessage(recipient);
        var sub = Subject;
        Deliver(subject: sub, body: msg, recipient: recipient);
    }

    protected abstract void Deliver(string subject, string body, string recipient);
}

public sealed class EmailNotification : Notification
{
    private readonly ISmtpClient _smtp;
    public override string Subject => "You have a new email notification";

    public override string BuildMessage(string recipient) =>
        $"Hello {recipient}, you have a new notification.";

    protected override void Deliver(string subject, string body, string recipient) =>
        _smtp.Send(recipient, subject, body);
}
```

---

## Pattern Matching — When It's Fine vs When It's a Violation

### Fine — discriminating over data without behaviour
```csharp
// ✅ Pattern matching on data union (closed set, no behaviour belongs to types)
string Summarise(Event e) => e switch
{
    OrderCreated c  => $"Order {c.OrderId} created",
    OrderCancelled x => $"Order {x.OrderId} cancelled: {x.Reason}",
    OrderShipped s  => $"Order {s.OrderId} shipped to {s.Address}",
};
// These are DTOs/records — adding Summarise() to each would pollute them
```

### Violation — behaviour belongs on the type
```csharp
// ❌ Each shape knows how to draw itself — move to the type
string Render(Shape shape) => shape switch
{
    Circle c    => $"<circle cx='0' cy='0' r='{c.Radius}'/>",
    Rectangle r => $"<rect w='{r.Width}' h='{r.Height}'/>",
    Polygon p   => $"<polygon points='{string.Join(" ", p.Points)}'/>"
};

// ✅ Behaviour belongs on the type
public abstract class Shape { public abstract string Render(); }
public sealed class Circle    : Shape { public override string Render() => $"<circle r='{Radius}'/>"; }
public sealed class Rectangle : Shape { public override string Render() => $"<rect w='{Width}' h='{Height}'/>"; }
```

---

## Covariance and Contravariance

```csharp
// Covariance (out) — return type can be more derived
public interface IProducer<out T> { T Produce(); }
// IProducer<Dog> can be assigned to IProducer<Animal>

// Contravariance (in) — parameter type can be more general
public interface IConsumer<in T> { void Consume(T item); }
// IConsumer<Animal> can be assigned to IConsumer<Dog>

// Common use in .NET:
IEnumerable<string>  → IEnumerable<object>  ✅ (covariant)
Action<object>       → Action<string>        ✅ (contravariant)
Func<string, string> → Func<object, string>  ✅ (contravariant on input, covariant on output)
```

---

## Angular Dynamic Components — Polymorphism in UI

```typescript
// ✅ Polymorphic component rendering — no ngIf chain on type
// Each notification type is a component, rendered dynamically

// Step 1: Define a shared interface/token
export abstract class NotificationComponent {
    @Input() abstract notification: Notification;
}

// Step 2: Implement per-type components
@Component({ selector: 'app-email-notif', template: `<p>Email: {{notification.message}}</p>` })
export class EmailNotificationComponent extends NotificationComponent {
    @Input() notification!: EmailNotification;
}

// Step 3: Map type → component
export const NOTIFICATION_COMPONENT_MAP: Record<string, Type<NotificationComponent>> = {
    email: EmailNotificationComponent,
    sms:   SmsNotificationComponent,
    push:  PushNotificationComponent,
};

// Step 4: Host component uses NgComponentOutlet
@Component({
    template: `<ng-container *ngComponentOutlet="componentType; inputs: {notification}"/>`
})
export class NotificationHostComponent {
    @Input() set notification(n: Notification) {
        this._notification = n;
        this.componentType = NOTIFICATION_COMPONENT_MAP[n.type];
    }
    get notification(): Notification { return this._notification; }

    private _notification!: Notification;
    componentType!: Type<NotificationComponent>;
}
```

---

## `new` Keyword Hiding — Silent Polymorphism Killer

```csharp
// ❌ new hides the base method — does NOT participate in polymorphism
public class BaseService
{
    public string GetName() => "Base";
}
public class DerivedService : BaseService
{
    public new string GetName() => "Derived";  // hides, not overrides
}

BaseService svc = new DerivedService();
svc.GetName();  // returns "Base" — shocking to callers who expect polymorphism

// ✅ Use virtual + override for polymorphic behaviour
public class BaseService
{
    public virtual string GetName() => "Base";
}
public class DerivedService : BaseService
{
    public override string GetName() => "Derived";
}
// Now svc.GetName() returns "Derived" as expected
```
