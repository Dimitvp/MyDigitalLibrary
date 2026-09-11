# DRY Examples — .NET & Angular

## Section 1 — Duplicated Business Logic

### ❌ Violation: Validation scattered across layers (C#)
```csharp
// In OrderController.cs
if (string.IsNullOrEmpty(order.CustomerEmail) || !order.CustomerEmail.Contains("@"))
    return BadRequest("Invalid email");

// In OrderService.cs
if (string.IsNullOrEmpty(order.CustomerEmail) || !order.CustomerEmail.Contains("@"))
    throw new ArgumentException("Invalid email");

// In InvoiceService.cs
if (string.IsNullOrEmpty(invoice.BillingEmail) || !invoice.BillingEmail.Contains("@"))
    throw new ValidationException("Billing email is invalid");
```

### ✅ Fixed: Single authoritative validator
```csharp
// EmailValidator.cs — one place for this knowledge
public static class EmailValidator
{
    public static bool IsValid(string email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains("@") && email.Contains(".");

    public static void EnsureValid(string email, string fieldName = "Email")
    {
        if (!IsValid(email))
            throw new ValidationException($"{fieldName} is not a valid email address.");
    }
}

// Usage everywhere:
EmailValidator.EnsureValid(order.CustomerEmail);
EmailValidator.EnsureValid(invoice.BillingEmail, "Billing Email");
```

---

### ❌ Violation: Transformation duplicated in Angular components
```typescript
// orders-list.component.ts
get formattedOrders() {
  return this.orders.map(o => ({
    ...o,
    displayDate: new Date(o.createdAt).toLocaleDateString('en-GB'),
    statusLabel: o.status === 'pending' ? 'Awaiting Payment' : 'Completed'
  }));
}

// invoice-list.component.ts
get formattedInvoices() {
  return this.invoices.map(i => ({
    ...i,
    displayDate: new Date(i.issuedAt).toLocaleDateString('en-GB'),
    statusLabel: i.status === 'pending' ? 'Awaiting Payment' : 'Completed'
  }));
}
```

### ✅ Fixed: Shared pipe + constants
```typescript
// status-label.pipe.ts
@Pipe({ name: 'statusLabel', standalone: true })
export class StatusLabelPipe implements PipeTransform {
  private readonly labels: Record<string, string> = {
    pending: 'Awaiting Payment',
    completed: 'Completed',
    cancelled: 'Cancelled'
  };
  transform(status: string): string {
    return this.labels[status] ?? status;
  }
}

// In templates: {{ order.status | statusLabel }}
// In templates: {{ invoice.status | statusLabel }}
// Date: use Angular's built-in DatePipe: {{ order.createdAt | date:'dd/MM/yyyy' }}
```

---

## Section 2 — Duplicated Data Access Patterns

### ❌ Violation: Pagination repeated in every repository (C#)
```csharp
// ProductRepository.cs
public async Task<(List<Product> Items, int Total)> GetPagedAsync(int page, int size)
{
    var query = _db.Products.Where(p => !p.IsDeleted);
    var total = await query.CountAsync();
    var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
    return (items, total);
}

// OrderRepository.cs
public async Task<(List<Order> Items, int Total)> GetPagedAsync(int page, int size)
{
    var query = _db.Orders.Where(o => !o.IsDeleted);
    var total = await query.CountAsync();
    var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
    return (items, total);
}
```

### ✅ Fixed: Generic paged extension method
```csharp
// PaginationExtensions.cs
public static class PaginationExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(
        this IQueryable<T> query, int page, int pageSize)
    {
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<T>(items, total, page, pageSize);
    }
}

// Usage in any repository:
return await _db.Products.Where(p => !p.IsDeleted).ToPagedAsync(page, size);
return await _db.Orders.Where(o => !o.IsDeleted).ToPagedAsync(page, size);

// Bonus: register global soft-delete filter instead of repeating Where(!IsDeleted):
// modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
```

---

### ❌ Violation: HTTP boilerplate in every Angular service method
```typescript
// product.service.ts
getProducts(): Observable<Product[]> {
  return this.http.get<Product[]>('/api/products', {
    headers: { Authorization: `Bearer ${this.auth.token}`, 'X-Correlation-ID': uuid() }
  }).pipe(
    retry(2),
    catchError(err => { this.logger.error(err); return throwError(() => err); })
  );
}

// order.service.ts
getOrders(): Observable<Order[]> {
  return this.http.get<Order[]>('/api/orders', {
    headers: { Authorization: `Bearer ${this.auth.token}`, 'X-Correlation-ID': uuid() }
  }).pipe(
    retry(2),
    catchError(err => { this.logger.error(err); return throwError(() => err); })
  );
}
```

### ✅ Fixed: HTTP Interceptor handles cross-cutting concerns
```typescript
// api.interceptor.ts
@Injectable()
export class ApiInterceptor implements HttpInterceptor {
  constructor(private auth: AuthService, private logger: LoggerService) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${this.auth.token}`,
        'X-Correlation-ID': crypto.randomUUID()
      }
    });
    return next.handle(authReq).pipe(
      retry(2),
      catchError(err => { this.logger.error('HTTP error', err); return throwError(() => err); })
    );
  }
}

// Services become clean:
getProducts(): Observable<Product[]> {
  return this.http.get<Product[]>('/api/products');
}
getOrders(): Observable<Order[]> {
  return this.http.get<Order[]>('/api/orders');
}
```

---

## Section 3 — Duplicated Configuration & Constants

### ❌ Violation: Role strings scattered everywhere (C#)
```csharp
// AuthController.cs
[Authorize(Roles = "Admin")]

// UserService.cs
if (!user.Roles.Contains("Admin") && !user.Roles.Contains("Manager"))
    throw new UnauthorizedException();

// ReportController.cs
[Authorize(Roles = "Admin,Manager")]
```

### ✅ Fixed: Central constants class
```csharp
// Roles.cs
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";

    public static readonly string AdminOrManager = $"{Admin},{Manager}";
}

// Usage:
[Authorize(Roles = Roles.Admin)]
if (!user.Roles.Contains(Roles.Admin) && !user.Roles.Contains(Roles.Manager))
[Authorize(Roles = Roles.AdminOrManager)]
```

---

### ❌ Violation: API URLs hardcoded in Angular services
```typescript
// user.service.ts
return this.http.get('/api/v1/users');

// product.service.ts
return this.http.get('/api/v1/products');

// If the API version changes to v2, you must hunt down every service
```

### ✅ Fixed: Injection token for API base URL
```typescript
// api-config.token.ts
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

// app.config.ts
providers: [
  { provide: API_BASE_URL, useValue: environment.apiBaseUrl }
]

// user.service.ts
constructor(private http: HttpClient, @Inject(API_BASE_URL) private apiUrl: string) {}
getUsers() { return this.http.get(`${this.apiUrl}/users`); }
```

---

## Section 4 — Duplicated Structural Patterns

### ❌ Violation: Same CRUD shape copy-pasted for every entity (C#)
```csharp
public class ProductService
{
    public async Task<Product> GetByIdAsync(int id) => await _repo.GetByIdAsync(id)
        ?? throw new NotFoundException($"Product {id} not found");
    public async Task<List<Product>> GetAllAsync() => await _repo.GetAllAsync();
    public async Task CreateAsync(Product entity) { await _repo.AddAsync(entity); await _repo.SaveAsync(); }
    public async Task DeleteAsync(int id) { _repo.Remove(id); await _repo.SaveAsync(); }
}
// ...identical pattern copy-pasted for OrderService, InvoiceService, CustomerService
```

### ✅ Fixed: Generic base service
```csharp
public abstract class CrudService<T> where T : class, IEntity
{
    protected readonly IRepository<T> Repository;

    protected CrudService(IRepository<T> repository) => Repository = repository;

    public virtual async Task<T> GetByIdAsync(int id) =>
        await Repository.GetByIdAsync(id) ?? throw new NotFoundException(typeof(T).Name, id);

    public virtual async Task<List<T>> GetAllAsync() => await Repository.GetAllAsync();

    public virtual async Task CreateAsync(T entity)
    {
        await Repository.AddAsync(entity);
        await Repository.SaveAsync();
    }

    public virtual async Task DeleteAsync(int id)
    {
        Repository.Remove(id);
        await Repository.SaveAsync();
    }
}

// Domain-specific services only override what's different:
public class ProductService : CrudService<Product>
{
    public ProductService(IRepository<Product> repo) : base(repo) {}

    // Only override when Product needs special behavior:
    public override async Task CreateAsync(Product product)
    {
        product.Slug = SlugGenerator.Generate(product.Name);
        await base.CreateAsync(product);
    }
}
```

---

### ❌ Violation: takeUntil/destroy pattern repeated in every Angular component
```typescript
// component-a.component.ts
private destroy$ = new Subject<void>();
ngOnDestroy() { this.destroy$.next(); this.destroy$.complete(); }
someObs$.pipe(takeUntil(this.destroy$)).subscribe(...);

// component-b.component.ts — exact copy
private destroy$ = new Subject<void>();
ngOnDestroy() { this.destroy$.next(); this.destroy$.complete(); }
someObs$.pipe(takeUntil(this.destroy$)).subscribe(...);
```

### ✅ Fixed: Use Angular's built-in `takeUntilDestroyed` (Angular 16+)
```typescript
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({...})
export class ComponentA {
  private destroyRef = inject(DestroyRef); // Angular 16+

  ngOnInit() {
    someObs$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(...);
    // No Subject, no ngOnDestroy boilerplate needed
  }
}
```

---

## Section 5 — Duplicated Cross-Cutting Concerns

### ❌ Violation: Caching logic copy-pasted in every service method (C#)
```csharp
public async Task<Product> GetProductAsync(int id)
{
    var key = $"product_{id}";
    if (_cache.TryGetValue(key, out Product cached)) return cached;
    var product = await _repo.GetByIdAsync(id);
    _cache.Set(key, product, TimeSpan.FromMinutes(10));
    return product;
}

// Same 4 lines duplicated in GetCategoryAsync, GetUserAsync, GetSettingsAsync...
```

### ✅ Fixed: Generic cache helper
```csharp
public static class CacheExtensions
{
    public static async Task<T> GetOrSetAsync<T>(
        this IMemoryCache cache,
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null)
    {
        if (cache.TryGetValue(key, out T cached)) return cached;
        var value = await factory();
        cache.Set(key, value, expiry ?? TimeSpan.FromMinutes(10));
        return value;
    }
}

// Usage — one line per method:
public Task<Product> GetProductAsync(int id) =>
    _cache.GetOrSetAsync($"product_{id}", () => _repo.GetByIdAsync(id));
```
