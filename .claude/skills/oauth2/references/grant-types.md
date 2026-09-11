# Grant Types — Reference

## Authorization Code + PKCE (Use for ALL interactive apps)

**For:** Web apps, SPAs, mobile apps — anything with a user
**Why PKCE:** Prevents stolen authorization codes from being exchanged

```
1. User clicks "Login"
2. App redirects → GET /connect/authorize
     ?response_type=code
     &client_id=angular-spa
     &redirect_uri=https://app.com/callback
     &scope=openid profile products.read offline_access
     &state=<random>
     &code_challenge=<BASE64URL(SHA256(verifier))>
     &code_challenge_method=S256

3. User authenticates at IdentityServer

4. IdentityServer redirects → https://app.com/callback
     ?code=<auth_code>
     &state=<same random>

5. App validates state (CSRF check)

6. App exchanges code → POST /connect/token
     grant_type=authorization_code
     &code=<auth_code>
     &redirect_uri=https://app.com/callback
     &client_id=angular-spa
     &code_verifier=<plain verifier>
     (+ client_secret if confidential client)

7. IdentityServer returns:
     access_token, id_token, refresh_token, expires_in
```

**Duende IdentityServer client config:**
```csharp
new Client
{
    ClientId          = "angular-spa",
    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce       = true,
    RequireClientSecret = false,  // public client — SPA has no safe secret storage

    RedirectUris           = { "https://app.mycompany.com/callback" },
    PostLogoutRedirectUris  = { "https://app.mycompany.com" },
    AllowedCorsOrigins     = { "https://app.mycompany.com" },

    AllowedScopes    = { "openid", "profile", "email", "products.read" },
    AllowOfflineAccess = true,   // enables refresh_token

    AccessTokenLifetime = 3600,
    AbsoluteRefreshTokenLifetime = 2592000,
    RefreshTokenUsage  = TokenUsage.OneTimeOnly,  // rotation — more secure
}
```

---

## Client Credentials (Machine-to-Machine)

**For:** Services, background jobs, APIs calling other APIs — no user involved
**Never use in:** Browser — client secret would be exposed

```
POST /connect/token
  grant_type=client_credentials
  &client_id=reporting-service
  &client_secret=<secret>
  &scope=products.read orders.read
```

**Duende IdentityServer client config:**
```csharp
new Client
{
    ClientId          = "reporting-service",
    AllowedGrantTypes = GrantTypes.ClientCredentials,
    ClientSecrets     = { new Secret("replace-with-long-random-secret".Sha256()) },
    AllowedScopes     = { "products.read", "orders.read" },
    AccessTokenLifetime = 1800,  // 30 minutes
    // No refresh tokens for client credentials — just re-request
}
```

**ASP.NET Core usage with Duende Access Token Management:**
```csharp
// Automatically caches and renews tokens
builder.Services.AddClientCredentialsTokenManagement()
    .AddClient("reporting-service", client =>
    {
        client.TokenEndpoint = "https://auth.mycompany.com/connect/token";
        client.ClientId      = "reporting-service";
        client.ClientSecret  = builder.Configuration["Auth:ReportingSecret"];
        client.Scope         = "products.read orders.read";
    });

builder.Services.AddClientCredentialsHttpClient<IReportingApiClient, ReportingApiClient>(
    "reporting-service", client => client.BaseAddress = new Uri("https://api.mycompany.com/"));
```

---

## Device Authorization Grant (RFC 8628)

**For:** Smart TVs, CLI tools, IoT devices — no browser or limited keyboard

```
1. Device → POST /connect/deviceauthorization
     client_id=my-tv-app
     &scope=openid profile

   Response:
     device_code, user_code, verification_uri, expires_in, interval

2. Device displays: "Go to myapp.com/activate and enter: ABCD-1234"

3. Device polls: POST /connect/token
     grant_type=urn:ietf:params:oauth:grant-type:device_code
     &device_code=<device_code>
     &client_id=my-tv-app

4. User goes to URL, enters code, authenticates

5. Next poll returns: access_token, id_token, refresh_token
```

---

## ❌ Implicit Flow — DEPRECATED, Never Use

**Problem:** Access token returned in URL fragment — logged in browser history,
server logs, Referer headers. PKCE-less, no refresh tokens.

**Migration:** Replace with Authorization Code + PKCE.

---

## ❌ Resource Owner Password Credentials — DEPRECATED, Never Use

**Problem:** User credentials sent to the client app directly — defeats the entire
purpose of OAuth (delegated access without sharing credentials).

**The only "legitimate" old use case** (internal first-party apps) now has better
alternatives: Authorization Code + PKCE with no consent screen.

---

## Refresh Token Flow

```
POST /connect/token
  grant_type=refresh_token
  &refresh_token=<refresh_token>
  &client_id=angular-spa
  (+ client_secret if confidential client)

Response:
  access_token (new)
  refresh_token (new — if rotation is enabled)
  expires_in
```

**Angular implementation — transparent refresh:**
```typescript
@Injectable({ providedIn: 'root' })
export class TokenRefreshService {
    private refreshInProgress$: Observable<string> | null = null;

    refreshToken(): Observable<string> {
        // Prevent multiple simultaneous refresh calls
        if (!this.refreshInProgress$) {
            this.refreshInProgress$ = this.http.post<TokenResponse>(
                `${this.authUrl}/connect/token`,
                new HttpParams()
                    .set('grant_type',    'refresh_token')
                    .set('refresh_token', this.getRefreshToken())
                    .set('client_id',     environment.clientId)
            ).pipe(
                map(r => r.access_token),
                tap(token  => this.storeAccessToken(token)),
                catchError(() => { this.logout(); return EMPTY; }),
                share(),
                finalize(() => this.refreshInProgress$ = null)
            );
        }
        return this.refreshInProgress$;
    }
}
```
