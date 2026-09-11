# DI Examples — Grounded in Seemann/van Deursen

## Section 1 — Captive Dependencies (Book: Ch. 8.4.1, p. 266)

### ❌ Scoped captured inside Singleton (most common form)
```csharp
// ReportingService registered as Singleton
public class ReportingService
{
    private readonly AppDbContext _db;  // Scoped — captured ONCE at first resolve

    public ReportingService(AppDbContext db) { _db = db; }
    // _db is the same instance for the entire application lifetime.
    // After the first request ends, _db is disposed — next call throws ObjectDisposedException.
}
```

### ✅ Fixed — IServiceScopeFactory (book's recommended pattern)
```csharp
public class ReportingService  // Singleton
{
    private readonly IServiceScopeFactory _factory;

    public ReportingService(IServiceScopeFactory factory) { _factory = factory; }

    public async Task GenerateAsync()
    {
        await using var scope = _factory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await db.Orders.ToListAsync();
        // scope disposed here — db correctly cleaned up after each call
    }
}
```

---

### ❌ Scoped service in BackgroundService (Singleton lifecycle)
```csharp
// IHostedService is Singleton — injecting AppDbContext (Scoped) is a Captive Dependency
public class CleanupJob : BackgroundService
{
    private readonly AppDbContext _db;  // Captured forever

    public CleanupJob(AppDbContext db) { _db = db; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // _db is disposed after first request cycle — ObjectDisposedException waiting
            _db.OldRecords.RemoveRange(_db.OldRecords.Where(r => r.ExpiredAt < DateTime.UtcNow));
            await _db.SaveChangesAsync(ct);
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
```

### ✅ Fixed
```csharp
public class CleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _factory;

    public CleanupJob(IServiceScopeFactory factory) { _factory = factory; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await using (var scope = _factory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.OldRecords.RemoveRange(db.OldRecords.Where(r => r.ExpiredAt < DateTime.UtcNow));
                await db.SaveChangesAsync(ct);
            }  // fresh scope and db disposed cleanly each cycle
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
```

---

## Section 2 — Control Freak (Book: Ch. 5.1, p. 127)

### ❌ Classic Control Freak — newing up a Volatile Dependency
```csharp
// Seemann/van Deursen listing 5.1 equivalent
public class HomeController : Controller
{
    public ViewResult Index()
    {
        var service = new ProductService();  // Control Freak — owns a Volatile Dependency
        var products = service.GetFeaturedProducts();
        return View(products);
    }
}
```

### ❌ Subtler Control Freak — interface declared but still newed up in constructor
```csharp
public class OrderService
{
    private readonly IOrderRepository _repo;

    public OrderService()
    {
        _repo = new SqlOrderRepository();  // field is an interface — but still Control Freak
        // The interface is cosmetic. SqlOrderRepository cannot be swapped or mocked.
    }
}
```

### ✅ Fixed — Constructor Injection (book's preferred pattern, Ch. 4.2, p. 95)
```csharp
public class OrderService
{
    private readonly IOrderRepository _repo;

    public OrderService(IOrderRepository repo)  // Dependency explicit, injectable, mockable
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }
}
```

---

## Section 3 — Service Locator (Book: Ch. 5.2, p. 138)

### ❌ Service Locator in business logic
```csharp
public class OrderService
{
    private readonly IServiceProvider _sp;  // The Locator

    public OrderService(IServiceProvider sp) { _sp = sp; }

    public async Task ProcessAsync(Order order)
    {
        // All dependencies hidden — reading the constructor tells you nothing
        var repo    = _sp.GetRequiredService<IOrderRepository>();
        var pricing = _sp.GetRequiredService<IPricingService>();
        var email   = _sp.GetRequiredService<IEmailService>();

        order.Total = pricing.Calculate(order);
        await repo.SaveAsync(order);
        await email.SendConfirmationAsync(order);
    }
}
```

### ✅ Fixed — explicit dependencies, fully testable
```csharp
public class OrderService
{
    private readonly IOrderRepository _repo;
    private readonly IPricingService  _pricing;
    private readonly IEmailService    _email;

    public OrderService(IOrderRepository repo, IPricingService pricing, IEmailService email)
    {
        _repo    = repo    ?? throw new ArgumentNullException(nameof(repo));
        _pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
        _email   = email   ?? throw new ArgumentNullException(nameof(email));
    }

    public async Task ProcessAsync(Order order)
    {
        order.Total = _pricing.Calculate(order);
        await _repo.SaveAsync(order);
        await _email.SendConfirmationAsync(order);
    }
}
// Unit test: just pass Moq/NSubstitute mocks — no container needed
```

---

## Section 3b — Ambient Context (Book: Ch. 5.3, p. 146)

### ❌ DateTime.Now used directly in business logic
```csharp
public class SubscriptionService
{
    public bool IsExpired(Subscription sub)
    {
        return sub.ExpiresAt < DateTime.UtcNow;  // Ambient Context — untestable
        // Can't write a unit test that controls "now"
    }
}
```

### ✅ Fixed — inject TimeProvider (.NET 8) or IDateTimeProvider
```csharp
// .NET 8+: TimeProvider is the official injectable abstraction
public class SubscriptionService
{
    private readonly TimeProvider _time;

    public SubscriptionService(TimeProvider time) { _time = time; }

    public bool IsExpired(Subscription sub) =>
        sub.ExpiresAt < _time.GetUtcNow();
}

// Register:
builder.Services.AddSingleton(TimeProvider.System);

// In tests:
var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
var sut = new SubscriptionService(fakeTime);
```

---

## Section 4 — IOptions<T> Pattern

### ❌ IConfiguration injected into application service
```csharp
public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config) { _config = config; }

    public void Send(string to, string body)
    {
        var host = _config["Smtp:Host"];          // magic string — typo = null at runtime
        var port = int.Parse(_config["Smtp:Port"]!); // null ref if key missing
    }
}
```

### ✅ Fixed — strongly-typed options with validation
```csharp
// SmtpOptions.cs
public class SmtpOptions
{
    [Required] public string Host     { get; init; } = "";
    [Range(1, 65535)] public int Port { get; init; } = 587;
    [Required] public string UserName { get; init; } = "";
}

// Program.cs
builder.Services
    .AddOptions<SmtpOptions>()
    .BindConfiguration("Smtp")
    .ValidateDataAnnotations()
    .ValidateOnStart();  // Fail at startup, not first email send

// EmailService.cs
public class EmailService
{
    private readonly SmtpOptions _opts;

    public EmailService(IOptions<SmtpOptions> opts) { _opts = opts.Value; }

    public void Send(string to, string body)
    {
        using var client = new SmtpClient(_opts.Host, _opts.Port); // type-safe, validated
    }
}
```

---

## Section 5 — HttpClient Registration

### ❌ new HttpClient() — socket exhaustion
```csharp
public class WeatherService
{
    public async Task<string> GetForecastAsync()
    {
        using var http = new HttpClient();  // new socket opened every call
        return await http.GetStringAsync("https://api.weather.com/forecast");
        // Sockets linger in TIME_WAIT for ~30s — under load: SocketException
    }
}
```

### ✅ Fixed — Typed Client (preferred pattern)
```csharp
// WeatherApiClient.cs
public class WeatherApiClient
{
    private readonly HttpClient _http;

    public WeatherApiClient(HttpClient http) { _http = http; }

    public Task<string> GetForecastAsync() =>
        _http.GetStringAsync("forecast");
}

// Program.cs
builder.Services.AddHttpClient<WeatherApiClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["WeatherApi:BaseUrl"]!);
    c.Timeout     = TimeSpan.FromSeconds(10);
})
.AddStandardResilienceHandler();  // .NET 8: retry + circuit breaker + timeout
```

---

## Section 6 — Constructor Over-injection Code Smell (Book: Ch. 6.1, p. 164)

### ❌ Too many constructor parameters — SRP violation signal
```csharp
public class OrderService
{
    public OrderService(
        IOrderRepository repo,
        IProductRepository products,
        IInventoryService inventory,
        IEmailService email,
        ISmsService sms,
        IPricingService pricing,
        IDiscountService discounts,
        IAuditLogger audit)  // 8 parameters — this class does too much
    { }
}
```

### ✅ Refactored — Facade Service groups cohesive dependencies
```csharp
// Group notification concerns into a facade
public class NotificationFacade
{
    private readonly IEmailService _email;
    private readonly ISmsService   _sms;

    public NotificationFacade(IEmailService email, ISmsService sms)
    { _email = email; _sms = sms; }

    public Task NotifyOrderConfirmedAsync(Order o) =>
        Task.WhenAll(_email.SendConfirmationAsync(o), _sms.SendConfirmationAsync(o));
}

// OrderService is now focused
public class OrderService
{
    public OrderService(
        IOrderRepository     repo,
        IInventoryService    inventory,
        IPricingService      pricing,
        NotificationFacade   notifications,
        IAuditLogger         audit)  // 5 — manageable, each cohesive
    { }
}
```

---

## Section 7 — Angular DI

### ❌ Missing provider — NullInjectorError at runtime
```typescript
@Injectable()  // no providedIn — not registered anywhere
export class DataService {}

@Component({ template: '' })
export class MyComponent {
    constructor(private data: DataService) {}
    // Runtime: NullInjectorError: No provider for DataService
}
```

### ✅ Fixed
```typescript
@Injectable({ providedIn: 'root' })  // tree-shakeable, available app-wide
export class DataService {}
```

---

### ❌ Primitive injected without InjectionToken
```typescript
// ❌ Cannot inject a plain string
constructor(private apiUrl: string) {}

// ✅ Declare an InjectionToken
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

// app.config.ts
providers: [
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl }
]

// service.ts
constructor(@Inject(API_BASE_URL) private apiUrl: string) {}
```

---

### ❌ useFactory with undeclared deps
```typescript
// ❌ logger will be undefined — no deps declared
{
    provide: MyService,
    useFactory: (logger: LoggerService) => new MyService(logger)
    // deps: [LoggerService]  ← missing!
}

// ✅
{
    provide: MyService,
    useFactory: (logger: LoggerService) => new MyService(logger),
    deps: [LoggerService]
}
```
