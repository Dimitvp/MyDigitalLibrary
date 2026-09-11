# Error Handling & Resilience — Reference

## Global Exception Handler (.NET 8)

```csharp
// GlobalExceptionHandler.cs
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "Unhandled exception {Method} {Path}",
            ctx.Request.Method, ctx.Request.Path);

        var (status, title) = ex switch
        {
            NotFoundException       => (StatusCodes.Status404NotFound,    "Resource not found"),
            ValidationException v   => (StatusCodes.Status422UnprocessableEntity, "Validation failed"),
            UnauthorizedException   => (StatusCodes.Status403Forbidden,   "Access denied"),
            ConflictException       => (StatusCodes.Status409Conflict,    "Conflict"),
            _                       => (StatusCodes.Status500InternalServerError, "Server error")
        };

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status   = status,
            Title    = title,
            Detail   = ex is ValidationException ve ? string.Join("; ", ve.Errors.Select(e => e.ErrorMessage)) : null,
            Extensions = { ["traceId"] = Activity.Current?.Id ?? ctx.TraceIdentifier }
        }, ct);

        return true;
    }
}

// Program.cs
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
app.UseExceptionHandler();
```

## Domain Exception Hierarchy

```csharp
// Base — all domain exceptions extend this
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) {}
}

public sealed class NotFoundException      : DomainException { public NotFoundException(string entity, object id) : base($"{entity} {id} was not found.") {} }
public sealed class ValidationException    : DomainException { public IReadOnlyList<ValidationFailure> Errors { get; } ... }
public sealed class UnauthorizedException  : DomainException { public UnauthorizedException() : base("Access denied.") {} }
public sealed class ConflictException      : DomainException { public ConflictException(string message) : base(message) {} }
```

## Polly Resilience (.NET 8 — Microsoft.Extensions.Resilience)

```csharp
// Program.cs — named resilience pipeline
builder.Services.AddResiliencePipeline("external-api", pipeline => pipeline
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay            = TimeSpan.FromSeconds(1),
        BackoffType      = DelayBackoffType.Exponential,
        UseJitter        = true,
        ShouldHandle     = new PredicateBuilder().Handle<HttpRequestException>()
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio          = 0.5,
        SamplingDuration      = TimeSpan.FromSeconds(30),
        MinimumThroughput     = 10,
        BreakDuration         = TimeSpan.FromSeconds(15)
    })
    .AddTimeout(TimeSpan.FromSeconds(10)));

// Usage in a Typed HttpClient
public class PaymentApiClient
{
    private readonly HttpClient              _http;
    private readonly ResiliencePipeline      _pipeline;

    public PaymentApiClient(HttpClient http, ResiliencePipelineProvider<string> provider)
    {
        _http     = http;
        _pipeline = provider.GetPipeline("external-api");
    }

    public Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct) =>
        _pipeline.ExecuteAsync(async token =>
            await _http.PostAsJsonAsync<PaymentResult>("charge", req, token), ct);
}

// Or shortcut for HttpClient — AddStandardResilienceHandler (.NET 8)
builder.Services.AddHttpClient<PaymentApiClient>()
    .AddStandardResilienceHandler();  // retry + circuit breaker + timeout, sensible defaults
```

## Angular — HTTP Error Interceptor

```typescript
// error.interceptor.ts
export const errorInterceptor: HttpInterceptorFn = (req, next) =>
    next(req).pipe(
        catchError((err: HttpErrorEvent) => {
            if (err.status === 401) inject(AuthService).refreshToken();
            if (err.status === 403) inject(Router).navigate(['/forbidden']);
            if (err.status >= 500) inject(NotificationService).showError('Server error. Please try again.');
            return throwError(() => err);
        }),
        retry({ count: 2, delay: 1000, resetOnSuccess: true })
    );

// app.config.ts
provideHttpClient(withInterceptors([correlationInterceptor, errorInterceptor]))
```
