# Inheritance — Reference

## Composition vs Inheritance — Decision Tree

```
Should I use inheritance?

1. Is this truly an "is-a" relationship?
   (Not just "shares some code with")
        │
        NO ──────────────────────────────────→ USE COMPOSITION
        │
        YES
        ▼
2. Can a derived instance always substitute a base instance?
   (Liskov Substitution Principle)
        │
        NO ──────────────────────────────────→ USE COMPOSITION
        │
        YES
        ▼
3. Is there meaningful shared implementation to reuse?
        │
        NO ───────────────────────────────→ USE INTERFACE only
        │
        YES
        ▼
4. Will the hierarchy stay shallow (≤ 2 levels)?
        │
        NO ──────────────────────────────────→ USE COMPOSITION
        │
        YES
        ▼
   ✅ INHERITANCE IS APPROPRIATE
   Use abstract class with shared implementation
```

---

## Composition Pattern — Replacing Inheritance

```csharp
// ❌ Inheritance for code reuse — Logger "is not a" BaseService
public class UserService : BaseService
{
    public UserService(AppDbContext db, ILogger<UserService> logger) : base(db, logger) { }
}

// ✅ Composition — UserService HAS a logger and a db context
public sealed class UserService
{
    private readonly IUserRepository            _repo;
    private readonly ILogger<UserService>       _logger;

    public UserService(IUserRepository repo, ILogger<UserService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }
}
```

### Decorator Pattern — extending behaviour without inheritance
```csharp
// ✅ Add caching to any IProductRepository without subclassing
public sealed class CachedProductRepository : IProductRepository
{
    private readonly IProductRepository _inner;   // composition
    private readonly IMemoryCache       _cache;

    public CachedProductRepository(IProductRepository inner, IMemoryCache cache)
    { _inner = inner; _cache = cache; }

    public Task<Product?> GetByIdAsync(ProductId id, CancellationToken ct) =>
        _cache.GetOrCreateAsync(CacheKeys.Product(id.Value), () => _inner.GetByIdAsync(id, ct));

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct) =>
        _inner.GetAllAsync(ct);   // passthrough for non-cached methods
}

// Registration: services.Decorate<IProductRepository, CachedProductRepository>();
```

---

## Sealed Classes — When and Why

```csharp
// ✅ Seal concrete leaf classes — prevents fragile base class problem
// and communicates design intent: "do not inherit from this"
public sealed class OrderValidator { ... }
public sealed class SmtpEmailService : IEmailService { ... }
public sealed class SqlOrderRepository : IOrderRepository { ... }

// ✅ Seal individual override methods
public abstract class Shape { public abstract double Area(); }
public class Circle : Shape
{
    public double Radius { get; init; }
    public sealed override double Area() => Math.PI * Radius * Radius;
    // No further override of Area() is valid for Circle — seal it
}
```

---

## Correct Inheritance Patterns

### Abstract base with protected helpers (Template Method)
```csharp
public abstract class BaseValidator<T>
{
    private readonly List<string> _errors = new();

    // Template method — subclasses fill in their rules
    public IReadOnlyList<string> Validate(T entity)
    {
        _errors.Clear();
        AddRules(entity);
        return _errors.AsReadOnly();
    }

    protected abstract void AddRules(T entity);

    protected void Require(bool condition, string errorMessage)
    {
        if (!condition) _errors.Add(errorMessage);
    }

    protected void RequireNotEmpty(string? value, string fieldName)
        => Require(!string.IsNullOrWhiteSpace(value), $"{fieldName} is required.");
}

public sealed class OrderValidator : BaseValidator<Order>
{
    protected override void AddRules(Order order)
    {
        Require(order.Lines.Any(), "Order must have at least one line.");
        Require(order.Total.Amount > 0, "Order total must be positive.");
        RequireNotEmpty(order.CustomerId.ToString(), "Customer ID");
    }
}
```

---

## Common Inheritance Anti-Patterns

### Fragile Base Class
```csharp
// Base class change breaks all derived classes
public class BaseList<T>
{
    private int _count;
    public virtual void Add(T item) { _count++; /* ... */ }
    public virtual void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items) Add(item);  // calls virtual Add
    }
}

public class CountingList<T> : BaseList<T>
{
    private int _addCount;
    public override void Add(T item) { _addCount++; base.Add(item); }
    // AddRange calls Add() → _addCount correctly incremented? DEPENDS on base implementation
    // If base changes AddRange to not call Add() → silent bug in derived class
}
```

### Calling Virtual Methods in Constructor
```csharp
// ❌ Virtual call during construction — derived class not yet initialized
public abstract class Animal
{
    public string Name { get; }
    protected Animal(string name)
    {
        Name = name;
        Speak();  // ← virtual call! If derived class's Speak() uses derived fields → NullReference
    }
    public abstract void Speak();
}

public class Dog : Animal
{
    private readonly string _breed;  // not yet set when Speak() is called from base ctor
    public Dog(string name, string breed) : base(name) { _breed = breed; }
    public override void Speak() => Console.WriteLine($"{_breed} says woof");  // _breed is null!
}

// ✅ Fix: move virtual call out of constructor; use factory method or Initialize() called by consumer
```

---

## Angular Inheritance Patterns

### Acceptable — abstract base component
```typescript
// ✅ Shared subscription management, form logic, or lifecycle code
export abstract class BaseListComponent<T> implements OnInit, OnDestroy {
    items: T[] = [];
    isLoading = false;
    error: string | null = null;

    private readonly destroy$ = new Subject<void>();

    ngOnInit(): void {
        this.isLoading = true;
        this.loadItems()
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next:  items => { this.items = items; this.isLoading = false; },
                error: err   => { this.error = 'Failed to load'; this.isLoading = false; }
            });
    }

    ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

    protected abstract loadItems(): Observable<T[]>;
}

@Component({ selector: 'app-products', templateUrl: './products.component.html' })
export class ProductsComponent extends BaseListComponent<Product> {
    constructor(private productService: ProductService) { super(); }
    protected loadItems(): Observable<Product[]> { return this.productService.getAll(); }
}
```

### Never — service extending service
```typescript
// ❌ Services should never extend other services
@Injectable({ providedIn: 'root' })
export class EnhancedProductService extends ProductService {
    // This breaks: if ProductService changes, EnhancedProductService breaks
    // Fix: inject ProductService and delegate
}

// ✅ Delegation instead
@Injectable({ providedIn: 'root' })
export class EnhancedProductService {
    constructor(private readonly productService: ProductService) {}

    getWithDiscount(id: number): Observable<Product> {
        return this.productService.getById(id).pipe(
            map(p => ({ ...p, price: p.price * 0.9 }))
        );
    }
}
```
