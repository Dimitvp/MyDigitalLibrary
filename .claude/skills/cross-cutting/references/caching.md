# Caching — Reference

## Cache Key Strategy — Centralised

```csharp
// CacheKeys.cs — single source of truth for all cache keys
public static class CacheKeys
{
    // Versioned keys — bump version to invalidate all entries on deploy
    public static string Product(int id)         => $"products:v1:{id}";
    public static string ProductList(int page)   => $"products:v1:list:{page}";
    public static string Category(int id)        => $"categories:v1:{id}";
    public static string UserPermissions(string userId) => $"auth:v1:permissions:{userId}";
    public static string Settings()              => "settings:v1";
}
```

## IMemoryCache — Single Node

```csharp
// Generic extension — eliminates all inline get/set patterns
public static class MemoryCacheExtensions
{
    public static Task<T?> GetOrCreateAsync<T>(
        this IMemoryCache cache,
        string key,
        Func<Task<T?>> factory,
        TimeSpan? absoluteExpiry = null,
        TimeSpan? slidingExpiry  = null) where T : class
    {
        return cache.GetOrCreateAsync(key, entry =>
        {
            if (absoluteExpiry.HasValue) entry.AbsoluteExpirationRelativeToNow = absoluteExpiry;
            if (slidingExpiry.HasValue)  entry.SlidingExpiration = slidingExpiry;
            return factory();
        });
    }
}

// Usage — one line per cached value
var product = await _cache.GetOrCreateAsync(
    CacheKeys.Product(id),
    () => _db.Products.FindAsync(id).AsTask(),
    absoluteExpiry: TimeSpan.FromMinutes(10));
```

## IDistributedCache — Multi-Node (Redis)

```csharp
// Wrapper that handles serialisation and null safety
public sealed class DistributedCacheService
{
    private readonly IDistributedCache _cache;
    public DistributedCacheService(IDistributedCache cache) { _cache = cache; }

    public async Task<T?> GetOrCreateAsync<T>(
        string key, Func<Task<T?>> factory, TimeSpan expiry,
        CancellationToken ct = default)
    {
        var bytes = await _cache.GetAsync(key, ct);
        if (bytes is not null)
            return JsonSerializer.Deserialize<T>(bytes);

        var value = await factory();
        if (value is not null)
            await _cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry }, ct);

        return value;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default) =>
        _cache.RemoveAsync(key, ct);
}

// Registration
builder.Services.AddStackExchangeRedisCache(opts =>
    opts.Configuration = builder.Configuration.GetConnectionString("Redis"));
builder.Services.AddSingleton<DistributedCacheService>();
```

## HybridCache (.NET 9) — Best of Both

```csharp
// Program.cs
builder.Services.AddHybridCache(opts =>
{
    opts.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration         = TimeSpan.FromMinutes(10),  // L2 (Redis) expiry
        LocalCacheExpiration = TimeSpan.FromMinutes(1)  // L1 (memory) expiry
    };
});
builder.Services.AddStackExchangeRedisCache(...); // HybridCache uses it automatically

// Usage — single API for both L1 + L2, stampede-safe
public async Task<Product?> GetProductAsync(int id, CancellationToken ct)
    => await _cache.GetOrCreateAsync(
        CacheKeys.Product(id),
        async token => await _db.Products.FindAsync(id, token),
        cancellationToken: ct);
```

## Decorator Pattern for Transparent Caching

```csharp
// CachedProductRepository.cs
public sealed class CachedProductRepository : IProductRepository
{
    private readonly IProductRepository _inner;
    private readonly IMemoryCache       _cache;

    public CachedProductRepository(IProductRepository inner, IMemoryCache cache)
    { _inner = inner; _cache = cache; }

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct) =>
        _cache.GetOrCreateAsync(CacheKeys.Product(id), () => _inner.GetByIdAsync(id, ct),
            absoluteExpiry: TimeSpan.FromMinutes(10));

    public async Task UpdateAsync(Product product, CancellationToken ct)
    {
        await _inner.UpdateAsync(product, ct);
        _cache.Remove(CacheKeys.Product(product.Id)); // invalidate on write
    }
}

// Registration with Scrutor
services.AddScoped<IProductRepository, SqlProductRepository>();
services.Decorate<IProductRepository, CachedProductRepository>();
```

## Angular — shareReplay for Observable Caching

```typescript
@Injectable({ providedIn: 'root' })
export class ProductService {
    private readonly cache = new Map<number, Observable<Product>>();

    constructor(private http: HttpClient) {}

    getProduct(id: number): Observable<Product> {
        if (!this.cache.has(id)) {
            this.cache.set(id,
                this.http.get<Product>(`/api/products/${id}`).pipe(
                    shareReplay(1)   // cache the last emission, share among all subscribers
                )
            );
        }
        return this.cache.get(id)!;
    }

    invalidate(id: number): void {
        this.cache.delete(id);  // force fresh fetch on next call
    }
}
```
