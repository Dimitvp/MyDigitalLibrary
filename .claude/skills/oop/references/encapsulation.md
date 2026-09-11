# Encapsulation — Reference

## Access Modifier Rules in C#

```
private   → accessible only within the declaring class
protected → accessible within the class and derived classes
internal  → accessible within the same assembly
public    → accessible from anywhere
```

**Default to the most restrictive access that still works.**

| Member type | Default to | Promote to public only when |
|-------------|------------|---------------------------|
| Fields | `private readonly` | Never — always use a property |
| Properties | `public { get; private set; }` | `public { get; set; }` only for DTOs/models |
| Methods | `private` | When part of the public contract |
| Classes | `internal` | When consumed outside the assembly |

---

## Property Design Patterns

### Immutable after construction
```csharp
// For entities and value objects — state set once in constructor
public class Product
{
    public ProductId Id    { get; }
    public string    Name  { get; }
    public Money     Price { get; private set; }  // private set = mutable only by class

    public Product(ProductId id, string name, Money price)
    {
        Id    = id;
        Name  = Guard.AgainstNullOrEmpty(name, nameof(name));
        Price = price;
    }

    public void UpdatePrice(Money newPrice)
    {
        if (newPrice.Amount <= 0) throw new DomainException("Price must be positive.");
        Price = newPrice;
    }
}
```

### C# 9+ `init` — immutable after object initializer
```csharp
// For DTOs, records, and input models — settable only in object initializer
public class CreateOrderRequest
{
    public required CustomerId CustomerId { get; init; }
    public required IReadOnlyList<OrderLineRequest> Lines { get; init; }
}

// Usage:
var req = new CreateOrderRequest
{
    CustomerId = CustomerId.From(id),
    Lines = lines
};
// req.CustomerId = ... ❌ compile error after construction
```

### C# 11 `required` — enforced by compiler
```csharp
public class UserDto
{
    public required string Email    { get; init; }
    public required string UserName { get; init; }
    public string? DisplayName      { get; init; }  // optional
}
// Forgetting Email or UserName → compile error, not runtime NullReference
```

---

## Collection Encapsulation

```csharp
// ❌ Mutable collection exposed — callers can bypass business logic
public class Order
{
    public List<OrderLine> Lines { get; set; } = new();
}
// order.Lines.Add(line);       // bypasses validation
// order.Lines.Clear();         // destroys state silently

// ✅ Read-only exposure — mutations go through controlled methods only
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderLine> _lines = new();

    // Option 1: AsReadOnly — zero allocation, but caller holds a reference to the list
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    // Option 2: AsEnumerable — most restrictive, caller gets only enumeration
    // public IEnumerable<OrderLine> Lines => _lines;

    public void AddLine(OrderLineRequest request)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Cannot add lines to a non-draft order.");
        _lines.Add(OrderLine.Create(request));
        RecalculateTotal();
    }

    public void RemoveLine(OrderLineId lineId)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new NotFoundException(nameof(OrderLine), lineId);
        _lines.Remove(line);
        RecalculateTotal();
    }
}
```

---

## Static State — the Worst Encapsulation Violation

```csharp
// ❌ Global mutable state — shared across all requests, thread-unsafe
public static class SessionManager
{
    public static Dictionary<string, User> ActiveSessions = new();  // race condition
}

// ✅ Inject a scoped service instead
public interface ISessionStore
{
    Task<User?> GetSessionAsync(string token, CancellationToken ct);
    Task StoreSessionAsync(string token, User user, CancellationToken ct);
}
// Register as Scoped — each request gets its own instance
```

---

## Angular/TypeScript Encapsulation Patterns

### BehaviorSubject — never expose directly
```typescript
// ❌ Callers can push arbitrary values
export class UserService {
    public currentUser$ = new BehaviorSubject<User | null>(null);
}
// component: userService.currentUser$.next(fakeUser);  ← bypasses any logic

// ✅ Encapsulated — only the service controls user state
export class UserService {
    private readonly _currentUser$ = new BehaviorSubject<User | null>(null);
    readonly currentUser$: Observable<User | null> = this._currentUser$.asObservable();

    setUser(user: User): void {
        // validation, side effects, logging here
        this._currentUser$.next(user);
    }

    clearUser(): void {
        this._currentUser$.next(null);
    }
}
```

### Component access modifiers
```typescript
@Component({ ... })
export class ProductCardComponent {
    @Input() product!: Product;          // public — template binding
    @Output() selected = new EventEmitter<Product>();  // public — template binding

    // These should be private — only used in template
    // Angular templates can access private members, so mark them private
    protected get formattedPrice(): string {  // protected = accessible in template
        return `${this.product.price.toFixed(2)} ${this.product.currency}`;
    }

    private calculateDiscount(): number {  // private — not used in template
        return this.product.originalPrice - this.product.price;
    }
}
```
