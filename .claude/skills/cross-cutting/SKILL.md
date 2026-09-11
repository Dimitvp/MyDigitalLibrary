---
name: cross-cutting-review
description: >
  Reviews .NET (C#) and Angular (TypeScript) code for Cross-Cutting Concern violations.
  Trigger when: user pastes code and asks for a review of logging, error handling,
  caching, validation, authorization, auditing, or tracing. Also trigger when code
  contains scattered try/catch blocks, manual ILogger calls in domain classes,
  hardcoded cache keys, inline authorization checks, missing correlation IDs, or
  validation logic duplicated across layers.
  Examples: "every service has its own try/catch", "where should I put my logging?",
  "my audit fields are set manually everywhere", "how do I centralise error handling?",
  "should caching be in the service or somewhere else?".
allowed-tools: []
---

# Cross-Cutting Concerns Code Review

You are an expert .NET and Angular architect. Cross-cutting concerns are infrastructure
behaviours that span multiple layers and features.

> **The defining rule:** A cross-cutting concern must never be implemented inline inside
> domain or application logic. It belongs in a dedicated, centralised mechanism that
> applies transparently — invisible to the feature code it wraps.

Inline implementations are always violations. The damage is cumulative — they drift,
diverge, and make every feature carry infrastructure weight it should never see.

---

## ⚠️ Non-Negotiable Rules

- **NEVER** accept "it's only a few lines" as a reason not to flag inline concern logic
- **NEVER** flag a violation without naming the centralised mechanism that should own it
- **NEVER** provide a partial fix — always show registration + implementation + usage together
- **ALWAYS** flag all violations across all seven concerns — they compound when left together
- **ALWAYS** name every file and line where a concern is implemented inline
- **ALWAYS** rank violations by impact in the Priority Order section
- **ALWAYS** load the relevant reference file for the concern being fixed

---

## Review Workflow

Follow these steps **in order** for every review:

**Step 1 — Read everything first**
Read all submitted code before flagging anything.
Note which concerns are already centralised and which are inline.

**Step 2 — Work through all seven concern checklists**
Check each concern in sequence: Logging → Error Handling → Caching → Validation →
Authorization → Auditing → Correlation IDs & Tracing.
Mark each: ✅ Centralised / ⚠️ Inline violation / ➖ Not applicable.

**Step 3 — For each violation, identify the mechanism**
Name the specific mechanism that should own it:
Middleware / MediatR Pipeline Behaviour / EF Core Interceptor / HTTP Interceptor /
Decorator / Angular Guard / Angular ErrorHandler.

**Step 4 — Classify severity**
🔴 Critical — security hole, data loss, silent failure, or missing audit trail on financial data
🟡 Moderate — operational burden, drift risk, poor observability, or no resilience
🟢 Minor — consistency issue, naming problem, or missing enrichment

**Step 5 — Write the review**
Use the Output Format exactly. Complete fix for each violation: registration + class + usage.

**Step 6 — Rank and summarise**
Order violations by impact. State which to fix first and the concrete reason why.

---

## Concern 1 — Logging & Structured Logging

**Mechanism that owns it:** Serilog / Microsoft.Extensions.Logging + enrichers + middleware.
Never inline in domain or application classes.

**Checklist — .NET:**
- [ ] Is `Console.WriteLine(...)` or `Debug.WriteLine(...)` used anywhere in application code?
- [ ] Is string interpolation used in log messages: `_logger.LogInformation($"Order {id}")`?
  (defeats structured logging — use message templates: `_logger.LogInformation("Order {Id}", id)`)
- [ ] Is `_logger.LogError(ex.Message)` used instead of `_logger.LogError(ex, "...")`?
  (loses the full stack trace)
- [ ] Is `ILogger` injected into domain entities or value objects?
  (domain must be infrastructure-free)
- [ ] Is an exception logged AND re-thrown in the same catch block?
  (logs it twice — once here, once in the global handler)
- [ ] Is sensitive data (passwords, tokens, PII) potentially logged?
- [ ] Are log enrichment properties (CorrelationId, UserId, Environment) missing?

**Checklist — Angular:**
- [ ] Is `console.log(...)`, `console.error(...)`, or `console.warn(...)` used directly in components or services?
- [ ] Is there no global `ErrorHandler` registered?
- [ ] Are error stack traces exposed in the UI or sent to a non-secure endpoint?

**Reference:** `references/logging.md` — Serilog setup, enrichers, sink config, Angular LoggerService.

---

## Concern 2 — Error Handling & Resilience

**Mechanism that owns it:** `IExceptionHandler` (.NET 8) / global middleware + Polly pipeline.
No service method should contain infrastructure-level try/catch.

**Checklist — .NET:**
- [ ] Is `try/catch (Exception ex) { return null; }` used? (swallowed exception — silent failure)
- [ ] Is the same `try/catch` + log + rethrow pattern repeated across multiple service methods?
- [ ] Is `catch (Exception ex) { throw new Exception(ex.Message); }` used? (loses stack trace)
- [ ] Do controllers return raw exception messages in 500 responses? (exposes internals)
- [ ] Are `HttpClient` calls missing retry, timeout, and circuit breaker?
- [ ] Do different endpoints return different error response shapes?
- [ ] Are domain exceptions (`NotFoundException`, `ValidationException`) caught and swallowed
  instead of flowing to a central handler?

**Checklist — Angular:**
- [ ] Is `catchError` with manual error logging duplicated in every service method?
- [ ] Are error toasts/snackbars triggered manually in every component?
- [ ] Are HTTP 401/403 responses not handled centrally in an interceptor?
- [ ] Is there no retry on transient HTTP failures?

**Reference:** `references/error-handling.md` — GlobalExceptionHandler, ProblemDetails, Polly, Angular interceptor.

---

## Concern 3 — Caching

**Mechanism that owns it:** Decorator pattern / cache extension method / pipeline behaviour.
No service method should contain inline cache key construction or get/set logic.

**Checklist — .NET:**
- [ ] Are cache keys constructed inline with string interpolation in multiple places?
  (`$"product_{id}"` scattered — should be in a central `CacheKeys` static class)
- [ ] Is cache get/set logic copy-pasted across multiple service methods?
- [ ] Is no cache expiry set on any entries?
- [ ] Are cached keys not namespaced? (collision risk across features)
- [ ] Are mutable objects cached without cloning? (cached reference mutated by consumers)
- [ ] Is cache invalidation missing when underlying data changes?
- [ ] Is `IMemoryCache` used in a multi-node deployment? (should be `IDistributedCache`)
- [ ] Is `IDistributedCache` used for hot data without a local memory layer?

**Checklist — Angular:**
- [ ] Are HTTP calls repeated on every component init with no caching?
- [ ] Are multiple subscribers triggering duplicate HTTP requests? (use `shareReplay(1)`)

**Reference:** `references/caching.md` — IMemoryCache, IDistributedCache, HybridCache (.NET 9), Decorator, shareReplay.

---

## Concern 4 — Validation Pipelines

**Mechanism that owns it:** MediatR `ValidationBehaviour` + FluentValidation.
Validation must run before business logic in a single consistent pipeline — not scattered
across controllers, services, and domain objects.

**Checklist — .NET:**
- [ ] Are `ModelState.IsValid` checks repeated in every controller action?
- [ ] Is `if (string.IsNullOrEmpty(x))` validation scattered in service methods?
- [ ] Is the same field validation rule (email format, phone format) duplicated across multiple validators?
- [ ] Is FluentValidation registered but NOT wired into MediatR? (validators never run)
- [ ] Are validation exceptions caught and swallowed in services instead of flowing to the global handler?
- [ ] Are domain entities throwing `ArgumentException` for input validation?
  (input validation is an application concern — not domain)
- [ ] Are async DB uniqueness checks missing from validators?

**Checklist — Angular:**
- [ ] Is validation logic duplicated across multiple reactive form definitions?
- [ ] Are API 422 errors not mapped back to specific form fields — only a generic toast shown?
- [ ] Are required/pattern validators duplicated inline instead of shared validator functions?

**Reference:** `references/validation.md` — ValidationBehaviour, shared FluentValidation rules, async validators, Angular form patterns.

---

## Concern 5 — Authorization & Security

**Mechanism that owns it:** Policy-based authorization + `IAuthorizationHandler` for resource checks.
MediatR `AuthorizationBehaviour` for command-level checks.
No inline role or permission checks in service methods.

**Checklist — .NET:**
- [ ] Is `if (!User.IsInRole("Admin"))` used inside controller action bodies?
- [ ] Is `if (!user.Roles.Contains("Manager"))` used inside service methods?
- [ ] Are role or permission strings scattered as raw literals across files?
  (should be in a central `Permissions` or `Roles` static class)
- [ ] Are any endpoints missing `[Authorize]` that require authentication?
- [ ] Is `[Authorize(Roles = "...")]` used instead of policy-based authorization?
- [ ] Are resource-based checks (does this user own this record?) done with inline queries in services?
- [ ] Is CORS configured with `AllowAnyOrigin()` + `AllowCredentials()`?
  (browser blocks this — it is also a security hole)
- [ ] Are sensitive endpoints missing rate limiting?

**Checklist — Angular:**
- [ ] Is `*ngIf="currentUser.role === 'admin'"` used in templates?
  (role string in UI — should use a `*appHasPermission` directive)
- [ ] Are protected routes missing an auth guard?
- [ ] Is the auth token stored in `localStorage`? (XSS vulnerable)
- [ ] Are 401 responses not handled in an interceptor to refresh the token?

**Reference:** `references/authorization.md` — policy-based auth, resource auth, MediatR behaviour, Angular guards, CORS, rate limiting.

---

## Concern 6 — Auditing & Change Tracking

**Mechanism that owns it:** EF Core `SaveChangesInterceptor` for entity auditing.
MediatR `AuditBehaviour` for command auditing.
No manual audit field assignment in service methods.

**Checklist — .NET:**
- [ ] Are `AuditLog` entries manually created inside service methods?
- [ ] Is `entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = userId;` repeated in every service?
- [ ] Are audit fields missing on entities that clearly need them (orders, payments, user changes)?
- [ ] Is `DateTime.Now` (local time) used instead of `DateTime.UtcNow` for audit timestamps?
- [ ] Does the audit log not capture the `before` state — only that something changed?
- [ ] Is soft-delete not audited — records disappear without a trail?
- [ ] Are failed operations not audited — only successful saves tracked?

**Checklist — Angular:**
- [ ] Are `X-User-Agent` or `X-Client-Version` audit headers not attached via interceptor?

**Reference:** `references/auditing.md` — SaveChangesInterceptor, full before/after audit log, soft-delete audit, MediatR AuditBehaviour.

---

## Concern 7 — Correlation IDs & Distributed Tracing

**Mechanism that owns it:** `CorrelationIdMiddleware` + OpenTelemetry + Serilog enrichment.
Every log entry, every outbound HTTP call, and every error response must carry a
`CorrelationId` / `TraceId`.

**Checklist — .NET:**
- [ ] Is `X-Correlation-ID` not propagated to downstream services?
- [ ] Do log entries lack `TraceId` / `CorrelationId`? (can't reconstruct a request trace)
- [ ] Is `Activity.Current?.Id` not included in error responses?
  (client cannot report a trace ID to support)
- [ ] Is OpenTelemetry not configured?
- [ ] Are `HttpClient` calls not propagating the W3C `traceparent` header?
- [ ] Is a new GUID generated per-request instead of using `Activity.Current.TraceId`?
- [ ] Is the correlation ID not logged at both request start AND end?

**Checklist — Angular:**
- [ ] Is there no interceptor attaching `X-Correlation-ID` to outbound requests?
- [ ] Is the correlation ID not surfaced when reporting errors to support?

**Reference:** `references/tracing.md` — full OpenTelemetry setup, CorrelationIdMiddleware, DelegatingHandler propagation, Angular interceptor.

---

## Output Format

```
## Cross-Cutting Concerns Review

### ✅ Correctly Centralised
[List every concern already handled well. Name the specific mechanism used.
If all seven are clean, explain why the current approach is correct.]

### ⚠️ Violations Found

#### [Concern Name] — [Short descriptive title]
**Severity:** 🔴 Critical / 🟡 Moderate / 🟢 Minor
**Mechanism that should own this:** [Middleware / Behaviour / Interceptor / Decorator / Guard / EF Core Interceptor]
**Locations:** [Every file and line where this is inline — be explicit]
**Problem:** [What drifts, breaks, or leaks when this is inline — the concrete consequence]
**Fix:**
\`\`\`csharp  // or typescript
[complete working implementation — registration + class + usage, not pseudocode]
\`\`\`

### 📋 Priority Order
[Rank all violations by impact from highest to lowest.
State which to fix first and the concrete reason — security hole, data loss, silent failure, etc.]
```

---

## Reference Files

Load on demand — load only the file relevant to the concern being fixed:

| Concern | Reference file |
|---------|---------------|
| Logging & Structured Logging | `references/logging.md` |
| Error Handling & Resilience | `references/error-handling.md` |
| Caching | `references/caching.md` |
| Validation Pipelines | `references/validation.md` |
| Authorization & Security | `references/authorization.md` |
| Auditing & Change Tracking | `references/auditing.md` |
| Correlation IDs & Tracing | `references/tracing.md` |

Load the reference file for a concern when:
- The fix requires a non-trivial implementation (interceptor, behaviour, middleware, decorator)
- The user asks "what should this look like?"
- The correct registration pattern is not immediately obvious
