# Backend-for-Frontend (BFF) Pattern — Reference

## Why BFF for Angular + .NET?

The BFF pattern is the **recommended** approach for securing SPAs.
Source: https://docs.duendesoftware.com/bff/

**Problem with tokens in the browser:**
- localStorage/sessionStorage → XSS steals tokens
- In-memory → lost on page refresh, can't be shared across tabs
- Any JavaScript on the page can read the token

**BFF Solution:**
- Tokens are stored server-side (in the .NET BFF)
- Angular talks to the BFF via session cookie (HttpOnly, Secure, SameSite=Strict)
- BFF proxies API requests, attaching the token transparently
- Angular NEVER sees a token — XSS has nothing to steal

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Browser                                                         │
│                                                                   │
│  ┌────────────────────────────────┐                              │
│  │  Angular SPA                   │                              │
│  │  - No tokens stored            │  ← HttpOnly session cookie   │
│  │  - Calls /bff/... endpoints    │ ──────────────────────────── │
│  └────────────────────────────────┘                              │
└─────────────────────────────────────────────────────────────────┘
         │ session cookie (HttpOnly, Secure)
         ▼
┌─────────────────────────────────────────────────────────────────┐
│  .NET BFF (ASP.NET Core)                                         │
│  - Holds tokens server-side                                      │
│  - Handles auth code exchange                                    │
│  - Proxies API requests with Bearer token                        │
│  - Manages token refresh automatically                           │
└─────────────────────────────────────────────────────────────────┘
         │ Bearer token (server-to-server)
         ▼
┌─────────────────────────────────────────────────────────────────┐
│  .NET Resource API                                               │
│  - Validates JWT from BFF                                        │
│  - Never communicates directly with Angular                      │
└─────────────────────────────────────────────────────────────────┘
         │ Authorization Code + PKCE
         ▼
┌─────────────────────────────────────────────────────────────────┐
│  Duende IdentityServer                                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Duende BFF Setup

```csharp
// Install: dotnet add package Duende.BFF
// Install: dotnet add package Duende.BFF.Yarp  (for reverse proxy)

// Program.cs
builder.Services.AddBff(options =>
{
    options.ManagementBasePath = "/bff";
})
.AddRemoteApis();  // enables API endpoint proxying

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme          = "Cookies";
        options.DefaultChallengeScheme = "oidc";
        options.DefaultSignOutScheme   = "oidc";
    })
    .AddCookie("Cookies", options =>
    {
        options.Cookie.Name     = "__Host-bff";  // __Host- prefix = secure binding
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority    = builder.Configuration["Auth:Authority"];
        options.ClientId     = "bff-client";
        options.ClientSecret = builder.Configuration["Auth:ClientSecret"];

        options.ResponseType = "code";
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("products.read");
        options.Scope.Add("offline_access");

        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;  // BFF saves tokens in the session

        options.TokenValidationParameters.NameClaimType   = "name";
        options.TokenValidationParameters.RoleClaimType   = "role";
    });

app.UseRouting();
app.UseAuthentication();
app.UseBff();          // ← must be between Authentication and Authorization
app.UseAuthorization();

app.MapBffManagementEndpoints();  // /bff/login, /bff/logout, /bff/user, /bff/silent-login

// Proxy API calls — BFF attaches token transparently
app.MapRemoteBffApiEndpoint("/api/products", "https://products-api.mycompany.com/api/products")
    .RequireAccessToken(TokenType.User);
```

---

## IdentityServer Client for BFF

```csharp
new Client
{
    ClientId          = "bff-client",
    ClientName        = "Angular BFF Client",

    // BFF is a confidential client — it has a secret
    ClientSecrets     = { new Secret("replace-with-vault-secret".Sha256()) },
    AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
    RequirePkce       = true,

    RedirectUris           = { "https://app.mycompany.com/bff/signin-oidc" },
    PostLogoutRedirectUris  = { "https://app.mycompany.com/" },
    FrontChannelLogoutUri  = "https://app.mycompany.com/bff/frontchannel-logout",

    AllowedScopes    = { "openid", "profile", "email", "products.read", "orders.place" },
    AllowOfflineAccess = true,  // refresh tokens

    // BFF handles silent refresh via back-channel
    BackChannelLogoutUri = "https://app.mycompany.com/bff/backchannel-logout",

    AccessTokenLifetime              = 3600,
    AbsoluteRefreshTokenLifetime     = 2592000,
    RefreshTokenUsage                = TokenUsage.OneTimeOnly,
}
```

---

## Angular Side — BFF Client

```typescript
// Angular calls BFF endpoints — no token handling needed
@Injectable({ providedIn: 'root' })
export class ProductService {
    private readonly http = inject(HttpClient);

    // ✅ Calls /api/products → BFF proxies with Bearer token
    getAll(): Observable<Product[]> {
        return this.http.get<Product[]>('/api/products');
    }
}

// Login/logout via BFF management endpoints
@Injectable({ providedIn: 'root' })
export class BffAuthService {
    login():  void { window.location.href = '/bff/login'; }
    logout(): void { window.location.href = '/bff/logout'; }

    // Get current user from BFF — returns claims from the session
    getUser(): Observable<BffUser | null> {
        return this.http.get<BffUser>('/bff/user', {
            headers: { 'X-CSRF': '1' }  // BFF requires CSRF header on all calls
        }).pipe(catchError(() => of(null)));
    }
}

// Angular HTTP interceptor for BFF — just add CSRF header
export const bffCsrfInterceptor: HttpInterceptorFn = (req, next) => {
    // BFF requires X-CSRF: 1 header to prevent CSRF attacks on API calls
    if (req.url.startsWith('/api') || req.url.startsWith('/bff')) {
        return next(req.clone({ setHeaders: { 'X-CSRF': '1' } }));
    }
    return next(req);
};
```

---

## BFF vs SPA-Direct Comparison

| | SPA Direct (tokens in browser) | BFF Pattern |
|---|---|---|
| Token storage | localStorage / memory | Server-side session |
| XSS risk | High — tokens stealable | None — tokens never in JS |
| CSRF risk | Low | Mitigated via SameSite + CSRF header |
| Complexity | Lower | Higher (extra .NET project) |
| Token refresh | Client-side | Handled by BFF automatically |
| Logout | Clear local storage | Server session + OIDC sign-out |
| Recommended by | ❌ OAuth RFC 9700 discourages | ✅ Duende, OAuth RFC 9700 |
