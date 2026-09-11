# OAuth 2.0 Security Best Practices — Reference
## Based on RFC 9700 & Duende Security Guidelines

## Token Security Rules

### Access Token Lifetime
```
Short-lived = security win.
If token is stolen, attacker access is limited to the lifetime.

Recommended:
  API access token:    5–60 minutes (15 mins is a good default)
  ID token:            5–10 minutes (consumed immediately by client)
  Refresh token:       7–90 days depending on sensitivity
  Machine-to-machine:  15–60 minutes (no refresh token needed — re-request)
```

### Refresh Token Rotation
```csharp
// ✅ One-time-use rotation — each refresh issues a new refresh token
// Stolen refresh tokens become invalid after first legitimate use
new Client
{
    RefreshTokenUsage      = TokenUsage.OneTimeOnly,    // ← rotation enabled
    RefreshTokenExpiration = TokenExpiration.Sliding,
    SlidingRefreshTokenLifetime  = 1296000,  // 15 days — reset on each use
    AbsoluteRefreshTokenLifetime = 2592000,  // 30 days — hard max
}
```

### Token Binding (Advanced)
For high-security scenarios — prevents token theft entirely:
- **DPoP (Demonstration of Proof of Possession)** — RFC 9449
  Token is bound to a specific key pair; attacker with stolen token can't use it
  without the private key
- **Mutual TLS (mTLS)** — RFC 8705
  Token bound to client certificate

---

## Common Vulnerabilities

### 1. CSRF (Cross-Site Request Forgery)
**Risk:** Attacker tricks user's browser into making authenticated requests

**Mitigations:**
- Use `SameSite=Strict` on session cookies (BFF pattern)
- Include CSRF token in custom header (`X-CSRF: 1`)
- Validate `state` parameter in authorization code flow
- Use PKCE — code_verifier binds the flow to the initiating client

```csharp
// ✅ ASP.NET Core CSRF for BFF
options.Cookie.SameSite = SameSiteMode.Strict;
// Angular sends X-CSRF: 1 — BFF validates this header
```

### 2. Open Redirect
**Risk:** Attacker crafts a redirect_uri that sends the auth code to their server

**Mitigation — exact URI matching only:**
```csharp
// ✅ Exact match — no wildcards
new Client
{
    RedirectUris = { "https://app.mycompany.com/callback" }
    // ❌ Never: "https://app.mycompany.com/*"
    // ❌ Never: "http://app.mycompany.com/callback" (no HTTPS)
}
```

### 3. Token Leakage via Logs
```csharp
// ❌ Token in logs
logger.LogInformation("Request with token: {Token}", accessToken);

// ✅ Log only the token prefix (for debugging) or nothing at all
logger.LogDebug("Request authenticated, token prefix: {Prefix}", accessToken[..8]);

// ✅ Structured logging — don't accidentally log Authorization header
// Configure Serilog to mask sensitive headers
```

### 4. Authorization Code Injection
**Risk:** Attacker injects a stolen auth code into a legitimate session

**Mitigation:**
- PKCE (code_verifier binds code to the session that requested it)
- State parameter validation

### 5. Mix-Up Attacks
**Risk:** Client sends code to wrong auth server

**Mitigation:**
- Validate `iss` claim in the authorization response (RFC 9207)
- Use authorization server metadata discovery

### 6. Token Substitution
**Risk:** Attacker substitutes one token type for another

**Mitigation:**
- API validates `aud` claim — access token for API must have correct audience
- Don't accept ID tokens at the API
- Don't accept access tokens as ID tokens

---

## Client Secret Management

```csharp
// ❌ Secret in appsettings.json (source control risk)
"ClientSecrets": [{ "Value": "my-secret-in-plaintext" }]

// ✅ Secret from environment variable
builder.Configuration["Auth:ClientSecret"]  // backed by Key Vault

// ✅ Hash the secret before storing in IdentityServer config
new Secret("very-long-random-generated-secret".Sha256())

// ✅ Ideal — use private key JWT instead of shared secret
// Client signs a JWT with its private key, IdentityServer verifies with public key
// No shared secret to steal
new Client
{
    ClientSecrets =
    {
        new Secret
        {
            Type  = IdentityServerConstants.SecretTypes.JsonWebKey,
            Value = JsonSerializer.Serialize(publicKey)
        }
    }
}
```

---

## Security Headers for IdentityServer UI

```csharp
// Add to IdentityServer's Startup/Program.cs
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"]     = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]            = "SAMEORIGIN";
    ctx.Response.Headers["X-XSS-Protection"]           = "1; mode=block";
    ctx.Response.Headers["Referrer-Policy"]            = "no-referrer";
    ctx.Response.Headers["Content-Security-Policy"]    =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'";
    ctx.Response.Headers["Permissions-Policy"]         = "geolocation=(), microphone=()";
    await next();
});
```

---

## HTTPS Everywhere

```csharp
// ❌ HTTP allowed — tokens travel in plaintext
options.RedirectUris = { "http://app.mycompany.com/callback" }

// ✅ HTTPS only in production
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// IdentityServer — reject non-HTTPS in production
builder.Services.AddIdentityServer(options =>
{
    options.Authentication.RequireAuthenticatedUserForSignOutMessage = true;
});
```

---

## Discovery Document — What to Verify

Every OAuth client should discover endpoints from `.well-known/openid-configuration`:

```
https://auth.mycompany.com/.well-known/openid-configuration

Returns:
{
  "issuer": "https://auth.mycompany.com",
  "authorization_endpoint": "https://auth.mycompany.com/connect/authorize",
  "token_endpoint": "https://auth.mycompany.com/connect/token",
  "userinfo_endpoint": "https://auth.mycompany.com/connect/userinfo",
  "jwks_uri": "https://auth.mycompany.com/.well-known/openid-configuration/jwks",
  "end_session_endpoint": "https://auth.mycompany.com/connect/endsession",
  "scopes_supported": ["openid", "profile", "email", "offline_access"],
  "grant_types_supported": ["authorization_code", "client_credentials", "refresh_token"],
  "response_types_supported": ["code"],
  "code_challenge_methods_supported": ["S256"]
}
```

**Never hardcode endpoint URLs** — use discovery so they update automatically
when the auth server changes its configuration.
