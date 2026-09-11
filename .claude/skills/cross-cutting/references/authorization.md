# Authorization & Security — Reference

## Policy-Based Authorization (.NET)

```csharp
// Permissions.cs — single source of truth
public static class Permissions
{
    public const string ViewReports    = "reports.view";
    public const string ManageProducts = "products.manage";
    public const string ManageUsers    = "users.manage";
    public const string ProcessOrders  = "orders.process";
    public const string ViewAuditLog   = "audit.view";
}

// Program.cs — policies defined centrally
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("ViewReports",    p => p.RequireClaim("permission", Permissions.ViewReports));
    opts.AddPolicy("ManageProducts", p => p.RequireClaim("permission", Permissions.ManageProducts));
    opts.AddPolicy("ManageUsers",    p => p.RequireClaim("permission", Permissions.ManageUsers));

    // Fallback — every endpoint requires auth unless explicitly [AllowAnonymous]
    opts.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

## Resource-Based Authorization

```csharp
// OwnerRequirement.cs
public sealed class OwnerRequirement : IAuthorizationRequirement {}

// OrderAuthorizationHandler.cs
public sealed class OrderAuthorizationHandler
    : AuthorizationHandler<OwnerRequirement, Order>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, OwnerRequirement req, Order order)
    {
        if (order.OwnerId == ctx.User.GetUserId())
            ctx.Succeed(req);
        // If not owner — ctx remains unsatisfied → 403
        return Task.CompletedTask;
    }
}

// Usage in a command handler (not in the controller)
public sealed class DeleteOrderHandler : IRequestHandler<DeleteOrderCommand>
{
    private readonly IOrderRepository      _repo;
    private readonly IAuthorizationService _auth;
    private readonly IHttpContextAccessor  _http;

    public async Task Handle(DeleteOrderCommand cmd, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(cmd.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), cmd.OrderId);

        var result = await _auth.AuthorizeAsync(
            _http.HttpContext!.User, order, new OwnerRequirement());

        if (!result.Succeeded)
            throw new UnauthorizedException();

        await _repo.DeleteAsync(order, ct);
    }
}
```

## MediatR Authorization Behaviour

```csharp
// AuthorizationBehaviour.cs — applies [Authorize] attribute on commands/queries
public sealed class AuthorizationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly ICurrentUserService   _user;
    private readonly IAuthorizationService _auth;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var authorizeAttributes = request.GetType()
            .GetCustomAttributes<AuthorizeAttribute>().ToList();

        if (!authorizeAttributes.Any()) return await next();

        if (_user.UserId is null)
            throw new UnauthorizedException();

        foreach (var attr in authorizeAttributes.Where(a => !string.IsNullOrEmpty(a.Policy)))
        {
            var result = await _auth.AuthorizeAsync(_user.ClaimsPrincipal, attr.Policy!);
            if (!result.Succeeded) throw new UnauthorizedException();
        }

        return await next();
    }
}
```

## CORS — Correct Setup

```csharp
// ❌ NEVER combine AllowAnyOrigin + AllowCredentials — browsers block it
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().AllowCredentials());

// ✅ Explicit allowed origins
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()!)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});
app.UseCors("AllowFrontend");
```

## Rate Limiting (.NET 8)

```csharp
// Program.cs
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("auth-endpoints", limiter =>
    {
        limiter.PermitLimit         = 10;
        limiter.Window              = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit          = 0;
    });
    opts.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = 429, Title = "Too many requests." }, ct);
    };
});
app.UseRateLimiter();

// Apply to login / registration endpoints
[EnableRateLimiting("auth-endpoints")]
[AllowAnonymous]
[HttpPost("login")]
public Task<IActionResult> Login(LoginCommand cmd) => ...
```

## Angular — Route Guards & Permission Directive

```typescript
// auth.guard.ts
export const authGuard: CanActivateFn = (route) => {
    const auth   = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
        router.navigate(['/login'], { queryParams: { returnUrl: route.url } });
        return false;
    }

    const required = route.data['permission'] as string | undefined;
    if (required && !auth.hasPermission(required)) {
        router.navigate(['/forbidden']);
        return false;
    }

    return true;
};

// app.routes.ts
{
    path: 'admin',
    canActivate: [authGuard],
    data: { permission: Permissions.ManageUsers },
    component: AdminComponent
}

// has-permission.directive.ts — controls visibility without role strings in templates
@Directive({ selector: '[appHasPermission]', standalone: true })
export class HasPermissionDirective implements OnInit {
    @Input() appHasPermission!: string;
    constructor(private auth: AuthService, private vcr: ViewContainerRef,
                private tmpl: TemplateRef<unknown>) {}

    ngOnInit() {
        if (this.auth.hasPermission(this.appHasPermission))
            this.vcr.createEmbeddedView(this.tmpl);
    }
}

// Usage in template
<button *appHasPermission="Permissions.ManageProducts">Delete</button>
```

## Token Storage — Security Comparison

| Storage | XSS risk | CSRF risk | Recommendation |
|---------|----------|-----------|----------------|
| `localStorage` | High | None | ❌ Avoid for auth tokens |
| `sessionStorage` | High | None | ❌ Avoid for auth tokens |
| `HttpOnly` cookie | None | Medium | ✅ Preferred — add CSRF token |
| In-memory (variable) | Low | None | ✅ Good — lost on page refresh |
