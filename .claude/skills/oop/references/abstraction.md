# Abstraction — Reference

## Interface vs Abstract Class — Decision Guide

```
Question 1: Does it share implementation (code, not just contract)?
├── YES → Consider abstract class
└── NO  → Use interface

Question 2: Can a class need MULTIPLE of these?
├── YES → Must be interface (C# has no multiple inheritance)
└── NO  → Either works; prefer interface for flexibility

Question 3: Will implementations be unrelated types?
├── YES → Interface (they just share the contract)
└── NO  → Abstract class if they share lineage

Question 4: Is this a "can do" relationship or "is a" relationship?
├── CAN DO → Interface (IEmailSender, IPaymentProcessor, ISortable)
└── IS A   → Abstract class (BaseRepository, BaseNotificationService)
```

---

## Interface Design Patterns

### Single-responsibility interfaces
```csharp
// ❌ Fat interface — forces implementations to carry unrelated methods
public interface IOrderService
{
    Task<Order> CreateAsync(CreateOrderCommand cmd);
    Task<Order?> GetByIdAsync(OrderId id);
    Task<List<Order>> SearchAsync(OrderFilter filter);
    Task<Invoice> GenerateInvoiceAsync(OrderId id);
    Task SendConfirmationEmailAsync(OrderId id);
    Task UpdateInventoryAsync(OrderId id);
}

// ✅ Segregated — each interface has one reason to change
public interface IOrderWriter   { Task<Order> CreateAsync(CreateOrderCommand cmd); }
public interface IOrderReader   { Task<Order?> GetByIdAsync(OrderId id); Task<List<Order>> SearchAsync(OrderFilter filter); }
public interface IInvoiceGenerator { Task<Invoice> GenerateAsync(OrderId id); }
// (Note: this overlaps with ISP from SOLID — both skills should flag this)
```

### Explicit interface implementation — when to use
```csharp
// Use when a class implements two interfaces with conflicting method names
// or when you want to hide an interface method from the public API
public class OrderRepository : IOrderRepository, IDisposable
{
    // IDisposable.Dispose is infrastructure concern — hide from main API
    void IDisposable.Dispose() => _db.Dispose();

    // IOrderRepository methods are part of the public API
    public Task<Order?> GetByIdAsync(OrderId id) => ...;
}
```

---

## Abstract Class Patterns

### Abstract class with shared implementation
```csharp
// ✅ Abstract class earns its existence — shared logger, shared retry logic
public abstract class BaseApiClient
{
    private readonly HttpClient      _http;
    private readonly ILogger         _logger;
    private readonly ResiliencePipeline _pipeline;

    protected BaseApiClient(HttpClient http, ILogger logger, ResiliencePipeline pipeline)
    {
        _http     = http;
        _logger   = logger;
        _pipeline = pipeline;
    }

    protected async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        _logger.LogDebug("GET {Path}", path);
        return await _pipeline.ExecuteAsync(async token =>
            await _http.GetFromJsonAsync<T>(path, token), ct);
    }

    protected async Task PostAsync<T>(string path, T body, CancellationToken ct) { ... }
}

// Derived classes add only what's unique to their API
public sealed class PaymentApiClient : BaseApiClient
{
    public PaymentApiClient(HttpClient http, ILogger<PaymentApiClient> logger, ResiliencePipeline pipeline)
        : base(http, logger, pipeline) { }

    public Task<PaymentResult?> ChargeAsync(ChargeRequest req, CancellationToken ct)
        => PostAsync<PaymentResult>("charge", req, ct);
}
```

### Template Method Pattern — abstract class defining algorithm skeleton
```csharp
public abstract class ReportGenerator
{
    // Template method — defines the algorithm skeleton
    public sealed Report Generate(ReportRequest request)
    {
        var data    = FetchData(request);       // step 1 — abstract
        var cleaned = CleanData(data);          // step 2 — virtual with default
        var report  = BuildReport(cleaned);     // step 3 — abstract
        AddMetadata(report, request);           // step 4 — sealed, always same
        return report;
    }

    protected abstract IEnumerable<RawRecord> FetchData(ReportRequest request);
    protected abstract Report BuildReport(IEnumerable<CleanRecord> data);

    // Virtual with default — derived classes can override but don't have to
    protected virtual IEnumerable<CleanRecord> CleanData(IEnumerable<RawRecord> raw)
        => raw.Where(r => r.IsValid).Select(r => r.ToClean());

    private void AddMetadata(Report report, ReportRequest request)
    {
        report.GeneratedAt = DateTime.UtcNow;
        report.RequestedBy = request.UserId;
    }
}
```

---

## Over-Abstraction Violations

```csharp
// ❌ Single-implementation interface with no polymorphism need
// If IOrderValidator will only ever have ONE implementation and
// there are no tests that mock it, this is over-engineering
public interface IOrderValidator { bool Validate(Order order); }
public class OrderValidator : IOrderValidator { ... }

// ✅ When this IS justified:
// - You need to mock it in tests (most common valid reason)
// - You anticipate multiple implementations (A/B, strategy, environment-specific)
// - You're crossing an architectural boundary (domain → infrastructure)

// ❌ Abstract base with no shared implementation — should be an interface
public abstract class AbstractEmailService
{
    public abstract Task SendAsync(string to, string subject, string body);
    // No shared fields, no shared methods — this is just a class pretending to be an interface
}
// Fix: make it an interface
```

---

## Angular Abstraction Patterns

### Injection token as abstraction
```typescript
// Use InjectionToken when you want a contract without a concrete base class
export interface LoggerService {
    log(message: string): void;
    error(message: string, error?: unknown): void;
}

export const LOGGER_SERVICE = new InjectionToken<LoggerService>('LOGGER_SERVICE');

// Multiple implementations behind one token
providers: [
    { provide: LOGGER_SERVICE, useClass: environment.production ? RemoteLoggerService : ConsoleLoggerService }
]
```

### Abstract service class (Angular)
```typescript
// ✅ Abstract base with shared implementation in Angular
export abstract class BaseDataService<T> {
    protected constructor(
        protected readonly http: HttpClient,
        protected readonly apiUrl: string
    ) {}

    getAll(): Observable<T[]> {
        return this.http.get<T[]>(this.apiUrl);
    }

    getById(id: number): Observable<T> {
        return this.http.get<T>(`${this.apiUrl}/${id}`);
    }

    abstract create(item: Partial<T>): Observable<T>;   // must override
    abstract update(id: number, item: Partial<T>): Observable<T>;
}

@Injectable({ providedIn: 'root' })
export class ProductService extends BaseDataService<Product> {
    constructor(http: HttpClient) {
        super(http, '/api/products');
    }

    create(product: Partial<Product>): Observable<Product> {
        return this.http.post<Product>(this.apiUrl, product);
    }

    update(id: number, product: Partial<Product>): Observable<Product> {
        return this.http.put<Product>(`${this.apiUrl}/${id}`, product);
    }
}
```
