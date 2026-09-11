# Logging & Structured Logging — Reference

## Serilog Setup (.NET 8)

```csharp
// Program.cs
builder.Host.UseSerilog((ctx, services, config) => config
    .ReadFrom.Configuration(ctx.Configuration)   // appsettings.json sinks/levels
    .ReadFrom.Services(services)                 // DI-registered enrichers
    .Enrich.FromLogContext()                     // picks up LogContext.PushProperty(...)
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithCorrelationId()                  // SerilogCorrelationId NuGet
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.Seq("http://localhost:5341"));       // or Splunk, ELK, etc.

// Middleware — log every request with timing
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("UserId",        ctx.User.GetUserId());
        diag.Set("CorrelationId", ctx.Response.Headers["X-Correlation-ID"].ToString());
    };
});
```

## appsettings.json — Log Levels by Namespace
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

## Log Level Guidelines
| Level | Use for |
|-------|---------|
| `Trace` | Extremely detailed — loop iterations, raw values. Dev only. |
| `Debug` | Diagnostic info useful in development — branch decisions, computed values |
| `Information` | Normal business events — order placed, user logged in |
| `Warning` | Unexpected but handled — retry triggered, fallback used, deprecated API called |
| `Error` | Failure that requires attention — exception caught, operation failed |
| `Critical` | System-level failure — DB unreachable, out of memory |

## Structured Logging Rules

```csharp
// ✅ Named properties — queryable in Seq/Splunk
_logger.LogInformation("Order {OrderId} placed for {CustomerId}", order.Id, order.CustomerId);

// ❌ String interpolation — no structured properties captured
_logger.LogInformation($"Order {order.Id} placed for {order.CustomerId}");

// ✅ Exception always passed as first argument (preserves stack trace)
_logger.LogError(ex, "Failed to process payment for order {OrderId}", order.Id);

// ❌ Only logs the message — stack trace lost
_logger.LogError(ex.Message);

// ✅ Log scope — all child log entries carry these properties
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["OrderId"] = order.Id,
    ["UserId"]  = userId
}))
{
    // everything logged here automatically carries OrderId + UserId
}
```

## Masking Sensitive Data

```csharp
// Use Destructurama.Attributed or custom destructuring policies
public class CreateUserCommand
{
    public string Email    { get; init; } = "";

    [NotLogged]                          // Destructurama.Attributed
    public string Password { get; init; } = "";
}

// Or via Serilog policy
Log.Logger = new LoggerConfiguration()
    .Destructure.ByTransforming<CreateUserCommand>(cmd => new
    {
        cmd.Email,
        Password = "***"
    })
    .CreateLogger();
```

## Angular — Centralised Logger

```typescript
// logger.service.ts
export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

@Injectable({ providedIn: 'root' })
export class LoggerService {
    private readonly isDev = !environment.production;

    debug(msg: string, ...args: unknown[]) {
        if (this.isDev) console.debug(`[DEBUG] ${msg}`, ...args);
    }
    info(msg: string, ...args: unknown[]) {
        console.info(`[INFO] ${msg}`, ...args);
    }
    warn(msg: string, ...args: unknown[]) {
        console.warn(`[WARN] ${msg}`, ...args);
    }
    error(msg: string, error?: unknown) {
        console.error(`[ERROR] ${msg}`, error);
        // also send to remote logging endpoint in production
    }
}

// Global error handler — catches all uncaught errors
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
    constructor(private logger: LoggerService) {}

    handleError(error: unknown): void {
        this.logger.error('Uncaught error', error);
    }
}

// app.config.ts
providers: [{ provide: ErrorHandler, useClass: GlobalErrorHandler }]
```
