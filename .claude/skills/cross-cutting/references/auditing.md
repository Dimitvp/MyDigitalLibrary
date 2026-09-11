# Auditing & Change Tracking — Reference

## EF Core Save Changes Interceptor

```csharp
// AuditableEntity.cs — base class for all auditable entities
public abstract class AuditableEntity
{
    public DateTime  CreatedAt  { get; set; }
    public string    CreatedBy  { get; set; } = "";
    public DateTime? UpdatedAt  { get; set; }
    public string?   UpdatedBy  { get; set; }
}

// ICurrentUserService.cs — abstraction over HttpContext user
public interface ICurrentUserService
{
    string UserId  { get; }
    string UserName { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUserService(IHttpContextAccessor accessor) { _accessor = accessor; }
    public string UserId   => _accessor.HttpContext?.User.GetUserId()   ?? "system";
    public string UserName => _accessor.HttpContext?.User.GetUserName() ?? "system";
}

// AuditSaveChangesInterceptor.cs
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider        _time;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUser, TimeProvider time)
    { _currentUser = currentUser; _time = time; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
    {
        StampEntities(data.Context!);
        return base.SavingChangesAsync(data, result, ct);
    }

    private void StampEntities(DbContext context)
    {
        var now    = _time.GetUtcNow().UtcDateTime;
        var userId = _currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    entry.Property(e => e.CreatedAt).IsModified = false; // protect
                    entry.Property(e => e.CreatedBy).IsModified = false; // protect
                    break;
            }
        }
    }
}

// Registration
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<AppDbContext>((sp, opts) =>
    opts.UseSqlServer(connStr)
        .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
```

## Full Audit Log — Before/After State

```csharp
// AuditLog.cs — entity to record every change
public class AuditLog
{
    public int       Id           { get; set; }
    public string    EntityName   { get; set; } = "";
    public string    EntityId     { get; set; } = "";
    public string    Action       { get; set; } = ""; // Created / Updated / Deleted
    public string?   OldValues    { get; set; }       // JSON snapshot before
    public string?   NewValues    { get; set; }       // JSON snapshot after
    public string    ChangedBy    { get; set; } = "";
    public DateTime  ChangedAt    { get; set; }
    public string?   CorrelationId { get; set; }
}

// FullAuditInterceptor.cs
public sealed class FullAuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _user;
    private readonly TimeProvider        _time;
    private List<AuditEntry>             _pending = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData data, InterceptionResult<int> result)
    {
        _pending = data.Context!.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new AuditEntry(e, _user.UserId, _time.GetUtcNow().UtcDateTime))
            .ToList();
        return base.SavingChanges(data, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
    {
        if (_pending.Count > 0 && data.Context is AppDbContext ctx)
        {
            ctx.AuditLogs.AddRange(_pending.Select(e => e.ToAuditLog()));
            await ctx.SaveChangesAsync(ct);
        }
        return await base.SavedChangesAsync(data, result, ct);
    }
}
```

## Soft Delete with Audit Trail

```csharp
// ISoftDeletable.cs
public interface ISoftDeletable
{
    bool     IsDeleted  { get; set; }
    DateTime? DeletedAt { get; set; }
    string?  DeletedBy  { get; set; }
}

// SoftDeleteInterceptor.cs
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _user;
    private readonly TimeProvider        _time;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
    {
        foreach (var entry in data.Context!.ChangeTracker.Entries<ISoftDeletable>()
                     .Where(e => e.State == EntityState.Deleted))
        {
            entry.State              = EntityState.Modified;
            entry.Entity.IsDeleted   = true;
            entry.Entity.DeletedAt   = _time.GetUtcNow().UtcDateTime;
            entry.Entity.DeletedBy   = _user.UserId;
        }
        return base.SavingChangesAsync(data, result, ct);
    }
}

// Global query filter — soft-deleted records never appear in queries
modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
```

## MediatR Command Audit Behaviour

```csharp
// AuditBehaviour.cs — logs every command execution after success
public sealed class AuditBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse> where TRequest : ICommand
{
    private readonly IAuditLogger        _audit;
    private readonly ICurrentUserService _user;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next();  // only audit on success

        await _audit.RecordAsync(new AuditRecord
        {
            CommandName = typeof(TRequest).Name,
            Payload     = JsonSerializer.Serialize(request),
            UserId      = _user.UserId,
            ExecutedAt  = DateTime.UtcNow
        }, ct);

        return response;
    }
}
```
