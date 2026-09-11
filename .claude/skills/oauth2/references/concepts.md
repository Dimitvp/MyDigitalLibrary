# OAuth 2.0 Core Concepts — Reference

## The Four Roles (RFC 6749 §1.1)

```
Resource Owner  →  The USER who owns the data and grants access
Client          →  The APPLICATION that wants access (SPA, mobile app, service)
Authorization Server → Issues tokens (Duende IdentityServer, Auth0, Entra ID)
Resource Server →  Hosts the protected data (your .NET API)
```

## Why OAuth Exists — The Problem It Solves

**Before OAuth:** To give a third-party app access to your Gmail, you gave it your Gmail password. Problems:
- App can do anything you can do — no scope limitation
- You can't revoke access without changing your password
- If the app is compromised, your credentials are too

**After OAuth:** You never give your password to the third party. Instead:
1. App redirects you to Google
2. You authenticate directly with Google
3. Google asks "Allow this app to read your emails?"
4. You consent — Google issues a limited-scope access token to the app
5. App uses that token — never saw your password

---

## Token Types

### Access Token
- Short-lived credential to access a specific resource
- Sent as `Authorization: Bearer <token>` header
- Resource Server validates it to grant/deny access
- Lifetime: typically 15 minutes to 1 hour
- **For the Resource Server** — not for the client to read (unless it needs to)

### ID Token (OpenID Connect)
- Proves who the user IS (authentication)
- JWT format, signed by the Authorization Server
- Contains claims: `sub`, `name`, `email`, `email_verified`
- **For the Client only** — never sent to the Resource Server
- Lifetime: typically 5 minutes (client reads it once)

### Refresh Token
- Long-lived credential to get new access tokens
- Used when access token expires — no re-authentication needed
- Must be stored securely — if stolen, attacker has long-term access
- Should use rotation (each use issues a new refresh token, invalidates old one)
- Lifetime: days to months depending on security requirements

### Relationship Between Token Types
```
┌────────────────────────────────────────────────────────────┐
│              Authorization Server                          │
│  /connect/authorize  →  returns auth code                  │
│  /connect/token      →  returns access_token + id_token    │
│                          + refresh_token (if offline_access)│
│  /connect/userinfo   →  returns user claims from auth token│
└────────────────────────────────────────────────────────────┘
         │                    │
    id_token              access_token
    (for Client)          (for Resource Server)
         │                    │
    Who am I?           What can I do?
```

---

## Scopes

Scopes define the level of access a client is requesting.

**OIDC standard scopes:**
- `openid` — required for OIDC, enables ID token
- `profile` — name, family_name, given_name, picture
- `email` — email, email_verified
- `offline_access` — enables refresh tokens

**Custom API scopes (you define these):**
```
products.read      → read product catalogue
products.write     → create/update products
orders.place       → create orders
orders.manage      → full order management
reports.view       → view analytics reports
```

**Principle of least privilege:** Request only the scopes needed for the current operation.

---

## Claims

Claims are key-value pairs inside a JWT token.

**Standard claims (RFC 7519):**
```json
{
  "iss": "https://auth.mycompany.com",   // issuer — who created the token
  "sub": "user123",                       // subject — unique user ID
  "aud": "my-api",                        // audience — who the token is for
  "exp": 1714000000,                      // expiry timestamp
  "nbf": 1713996400,                      // not valid before
  "iat": 1713996400,                      // issued at
  "jti": "abc-123"                        // JWT ID — unique token identifier
}
```

**Validate ALL of these on the resource server:**
- `iss` — must match your IdentityServer URL
- `aud` — must match your API's identifier
- `exp` — must not be in the past
- `nbf` — must not be in the future

---

## JWT Structure

```
header.payload.signature

eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9    ← Base64 encoded header
.
eyJzdWIiOiJ1c2VyMTIzIiwic2NvcGUiOiJwcm9kdWN0cy5yZWFkIn0  ← Base64 encoded payload
.
<signature>                              ← RSA/ECDSA signature

Header: { "alg": "RS256", "typ": "JWT", "kid": "key-id" }
Payload: { "sub": "user123", "scope": "products.read", "aud": "my-api", ... }
```

**Important:** The payload is Base64 encoded, NOT encrypted.
Anyone who has the token can decode and read its claims.
**Never put sensitive data (passwords, SSNs, credit cards) in a JWT.**

---

## PKCE — Proof Key for Code Exchange (RFC 7636)

Protects the authorization code from being stolen and exchanged by an attacker.

```
1. Client generates:
   code_verifier  = random string (43-128 characters)
   code_challenge = BASE64URL(SHA256(code_verifier))

2. Authorization request:
   GET /connect/authorize
     ?code_challenge=<hash>
     &code_challenge_method=S256
     &...

3. Auth server stores {auth_code → code_challenge}

4. Token request:
   POST /connect/token
   code=<auth_code>
   &code_verifier=<plain text>

5. Auth server verifies:
   SHA256(code_verifier) == stored code_challenge
   ✅ match → issue tokens
   ❌ mismatch → reject (stolen code can't be exchanged without verifier)
```

**Why this matters:** In a SPA, the authorization code travels via browser redirect.
An attacker who intercepts the code URL cannot exchange it without the `code_verifier`
which was never transmitted over the network.

---

## State Parameter — CSRF Protection

```typescript
// ✅ Generate and validate state
const state = crypto.randomUUID();
sessionStorage.setItem('oauth_state', state);

// Include in authorization request
const authUrl = `${authServer}/connect/authorize?state=${state}&...`;

// On callback — MUST validate before using the code
const returnedState = new URLSearchParams(window.location.search).get('state');
const savedState    = sessionStorage.getItem('oauth_state');
sessionStorage.removeItem('oauth_state');

if (returnedState !== savedState) {
    // CSRF attack — reject and redirect to login
    throw new Error('State mismatch — possible CSRF attack');
}
```
