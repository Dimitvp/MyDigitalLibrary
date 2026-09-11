# Correlation IDs & Distributed Tracing — Reference

## Full OpenTelemetry Setup (.NET 8)

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(
            serviceName:    builder.Configuration["ServiceName"] ?? "MyApp",
            serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString()))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(opts =>
        {
            opts.RecordException = true;
            opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(opts => opts.SetDbStatementForText = true)
        .AddSource("MyApp.*")                              // custom Activity sources
        .AddOtlpExporter(opts =>                           // Jaeger / Grafana Tempo / Seq
            opts.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"]!)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

app.MapPrometheusScrapingEndpoint("/metrics");
```

## Serilog Enriched with Trace IDs

```csharp
// Program.cs — every log line carries TraceId and SpanId automatically
builder.Host.UseSerilog((ctx, services, config) => config
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", ctx.Configuration["ServiceName"])
    .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
    // OpenTelemetry TraceId + SpanId from Activity.Current — requires Serilog.Enrichers.OpenTelemetry
    .Enrich.WithOpenTelemetryTraceId()
    .Enrich.WithOpenTelemetrySpanId()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));
```

## Correlation ID Middleware

```csharp
// CorrelationIdMiddleware.cs
public sealed class CorrelationIdMiddleware
{
    private const string Header = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) { _next = next; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Accept incoming correlation ID (from upstream service or client)
        // or generate one tied to the current W3C trace
        var correlationId =
            ctx.Request.Headers[Header].FirstOrDefault()
            ?? Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString("N");

        // Propagate on response so the client can report it
        ctx.Response.Headers[Header] = correlationId;

        // Add to Serilog context — all log entries in this request carry it
        using (LogContext.PushProperty("CorrelationId", correlationId))
        // Add to Activity tags — visible in distributed traces
        {
            Activity.Current?.SetTag("correlation.id", correlationId);
            await _next(ctx);
        }
    }
}

// Register BEFORE all other middleware
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
```

## Propagating Correlation ID to Downstream HttpClient Calls

```csharp
// CorrelationIdDelegatingHandler.cs
public sealed class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private const string Header = "X-Correlation-ID";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        // W3C traceparent is added automatically by AddHttpClientInstrumentation
        // Add X-Correlation-ID explicitly for services that read it
        var correlationId = Activity.Current?.TraceId.ToString()
                            ?? Guid.NewGuid().ToString("N");

        request.Headers.TryAddWithoutValidation(Header, correlationId);
        return base.SendAsync(request, ct);
    }
}

// Registration — added to every typed/named client
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();
builder.Services.AddHttpClient<PaymentApiClient>()
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
    .AddStandardResilienceHandler();
```

## Custom Activity Source — Instrumenting Business Operations

```csharp
// Telemetry.cs
public static class Telemetry
{
    public static readonly ActivitySource Orders =
        new ActivitySource("MyApp.Orders", "1.0.0");
}

// Usage in a command handler — creates a child span in the trace
public async Task Handle(PlaceOrderCommand cmd, CancellationToken ct)
{
    using var activity = Telemetry.Orders.StartActivity("PlaceOrder");
    activity?.SetTag("order.customerId", cmd.CustomerId);
    activity?.SetTag("order.itemCount",  cmd.Items.Count);

    try
    {
        var order = await _service.CreateAsync(cmd, ct);
        activity?.SetTag("order.id", order.Id);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return order;
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

## Angular — Correlation ID Interceptor

```typescript
// correlation-id.interceptor.ts
export const correlationIdInterceptor: HttpInterceptorFn = (req, next) => {
    const correlationId = crypto.randomUUID();

    const cloned = req.clone({
        setHeaders: { 'X-Correlation-ID': correlationId }
    });

    return next(cloned).pipe(
        tap({
            error: (err: HttpErrorResponse) => {
                // Log correlation ID with error so user can report it to support
                const serverCorrelationId = err.headers.get('X-Correlation-ID');
                inject(LoggerService).error(
                    `HTTP ${err.status} — Correlation ID: ${serverCorrelationId ?? correlationId}`
                );
            }
        })
    );
};

// app.config.ts
provideHttpClient(
    withInterceptors([correlationIdInterceptor, errorInterceptor])
)
```

## Health Checks with Tracing

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database")
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, "redis")
    .AddUrlGroup(new Uri(builder.Configuration["ExternalApi:BaseUrl"]!), "external-api");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapHealthChecks("/health/ready",  new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // liveness check — always returns 200 if app is running
});
```
