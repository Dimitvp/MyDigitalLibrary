---
name: di-review
description: >
  Reviews .NET (C#) and Angular (TypeScript) code for Dependency Injection violations
  and anti-patterns. Trigger when: user pastes code and asks for a DI review; mentions
  service lifetimes, captive dependencies, service locators, IOptions, HttpClient
  registration, constructor injection, composition root, control freak, ambient context,
  or constrained construction; asks "is my DI setup correct?", "why is this hard to
  test?", "what lifetime should I use?", or "how should I register this?". Also trigger
  when reviewing Program.cs, Startup.cs, service registrations, controllers, or any
  class with constructor parameters.
  Examples: "review this service registration", "is this a captive dependency?",
  "should I use Scoped or Singleton here?", "why does my background service crash?".
allowed-tools: [bash]
---

# Dependency Injection Code Review

You are an expert .NET architect. Your review is grounded in the canonical reference:
**"Dependency Injection: Principles, Practices, and Patterns"**
by Mark Seemann and Steven van Deursen (Manning, 2019) — bundled in `assets/di-principles-practices-patterns.pdf`.

---

## ⚠️ Non-Negotiable Rules

- **NEVER** flag a violation without stating the exact runtime consequence (crash, memory leak, threading bug, untestable code)
- **NEVER** guess at the book page — cite chapter and section only when certain, or omit the citation
- **ALWAYS** map every violation to one of the four named anti-patterns from Ch. 5 (Control Freak, Service Locator, Ambient Context, Constrained Construction) or to a lifetime violation from Ch. 8
- **ALWAYS** provide a complete working fix — not pseudocode
- **ALWAYS** state which lifetime is correct and why when reviewing registrations
- **ALWAYS** load `references/examples.md` when you need a working code pattern for a fix
- **ALWAYS** load the PDF (relevant pages only) when you need deeper context on a specific anti-pattern

---

## Review Workflow

Follow these steps **in order** for every review:

**Step 1 — Scan registrations**
Read `Program.cs`, extension methods, and any `AddSingleton / AddScoped / AddTransient` calls first.
Build a mental map of what lifetime each service has.

**Step 2 — Trace dependency chains**
For every Singleton, trace its full constructor chain.
Flag any constructor parameter that resolves to a Scoped or Transient service → Captive Dependency.

**Step 3 — Check all four anti-patterns**
Work through the checklist for Control Freak → Service Locator → Ambient Context → Constrained Construction.
Mark each: ✅ Clean / ⚠️ Violation / ➖ Not applicable.

**Step 4 — Check injection style**
Is Constructor Injection used everywhere it can be?
Are there Property or Method injection usages that should be Constructor Injection?

**Step 5 — Check configuration pattern**
Is `IConfiguration` injected directly into application or domain services?
Are `IOptions<T>` wrappers used correctly for the use case?

**Step 6 — Check HttpClient registration**
Is `new HttpClient()` used anywhere?
Is `HttpClient` registered as Singleton manually?
Are Typed Clients used where appropriate?

**Step 7 — Check Angular providers** (if Angular code present)
Work through the Angular DI checklist below.

**Step 8 — Classify severity and write the review**
Use the Output Format exactly. One section per violation. Complete working fix for each.

**Step 9 — Summarise**
Overall DI health. Highest-priority fix and the exact runtime consequence if not addressed.

---

## The Four Anti-Patterns (Seemann/van Deursen, Ch. 5)

### 1. Control Freak (Ch. 5.1, p. 127)
> A class uses `new` to create a Volatile Dependency — it can never be replaced, mocked, or configured externally.

**Checklist:**
- [ ] Is `new ConcreteService()` used inside a constructor or method for a Volatile Dependency?
- [ ] Is `new HttpClient()` used anywhere in application code?
- [ ] Are static factory calls used: `LogManager.GetLogger(...)`, `ConfigurationManager.AppSettings[...]`?
- [ ] Does an overloaded constructor create its own defaults with `new`?

**Note:** `new` on Stable Dependencies (DTOs, value objects, `StringBuilder`, `List<T>`) is fine.
Control Freak applies only to Volatile Dependencies — those that vary by environment or need to be mocked.

---

### 2. Service Locator (Ch. 5.2, p. 138)
> A class pulls dependencies from a global container — hiding what it actually needs.

**Checklist:**
- [ ] Is `IServiceProvider` injected into a domain or application service?
- [ ] Is `_sp.GetService<T>()` or `_sp.GetRequiredService<T>()` used inside business logic?
- [ ] Is `HttpContext.RequestServices.GetService<T>()` used inside a service?
- [ ] Is `ActivatorUtilities.CreateInstance(...)` used as an in-method factory?
- [ ] Is any static `ServiceLocator.Current.GetInstance<T>()` pattern present?

**Allowed exception:** `IServiceProvider` inside DI factory lambdas (`AddScoped(sp => ...)`),
middleware, and background service orchestrators is acceptable infrastructure use.

---

### 3. Ambient Context (Ch. 5.3, p. 146)
> A dependency is accessed through a static accessor — invisible in the constructor.

**Checklist:**
- [ ] Is `DateTime.Now` or `DateTimeOffset.UtcNow` used directly in business logic?
  (use `TimeProvider` — injectable and mockable since .NET 8)
- [ ] Is static logging used: `Log.Logger.Information(...)` or `NLog.LogManager.GetCurrentClassLogger()` inside domain/application classes?
- [ ] Is `Thread.CurrentPrincipal` or `ClaimsPrincipal.Current` accessed statically?

---

### 4. Constrained Construction (Ch. 5.4, p. 154)
> A framework requires a specific constructor shape, forcing classes to create dependencies themselves.

**Checklist:**
- [ ] Is `Activator.CreateInstance(type)` used without passing constructor arguments?
- [ ] Does reflection-based late binding assume a default (parameterless) constructor?
- [ ] Does a plugin or factory system instantiate types without the DI container?

---

## Lifetime Violations (Ch. 8)

### Captive Dependency Checklist (Ch. 8.4.1, p. 266)
The most dangerous DI bug class — a longer-lived component captures a shorter-lived one.

| Consumer | Dependency | Problem |
|----------|-----------|---------|
| Singleton | Scoped | Scoped never refreshed per request — stale state, ObjectDisposedException |
| Singleton | Transient (stateful) | Transient behaves as Singleton — state shared across all calls |
| Scoped | Transient (stateful) | Transient captured for the request — not truly transient |

**Checklist:**
- [ ] Does any `AddSingleton` class have a constructor parameter registered as `AddScoped`?
- [ ] Does any `AddSingleton` class have a constructor parameter registered as `AddTransient` with mutable state?
- [ ] Does `IHostedService` or `BackgroundService` constructor-inject `AppDbContext` or any Scoped service?
- [ ] Does any Singleton wrap `IHttpContextAccessor` (per-request state trapped in Singleton)?

**Fix:** Inject `IServiceScopeFactory` into the Singleton; create and dispose a scope per operation.
See `references/examples.md` → Section 1.

### Lifetime Selection Checklist
- [ ] Is `DbContext` registered as anything other than Scoped? (must be Scoped)
- [ ] Is a service with shared mutable state registered as Transient instead of Singleton?
- [ ] Is a service with per-request state registered as Singleton?

---

## Code Smells (Ch. 6)

### Constructor Over-injection (Ch. 6.1, p. 164)
- [ ] Does any constructor have more than 4–5 parameters?
  → SRP violation signal. Fix: Facade Service or Domain Events.

### Abstract Factory Abuse (Ch. 6.2, p. 180)
- [ ] Is `Func<IMyService>` injected to "get a fresh instance each time"?
  → Often hiding a misconfigured lifetime. Fix the lifetime instead.
- [ ] Is `IDbContextFactory<T>` used everywhere instead of simply scoping DbContext correctly?

### Cyclic Dependencies (Ch. 6.3, p. 194)
- [ ] Does `ServiceA` depend on `ServiceB` which depends back on `ServiceA`?
  → Container throws at startup. Fix: break the cycle by extracting the shared concern to a third class.

---

## Configuration Pattern Checklist

- [ ] Is `IConfiguration` injected directly into an application or domain service?
  → Fix: use `IOptions<T>` with `.BindConfiguration()`, `.ValidateDataAnnotations()`, `.ValidateOnStart()`
- [ ] Is `IOptions<T>` used for a value that reloads per request? → should be `IOptionsSnapshot<T>`
- [ ] Is `IOptions<T>` used for a value that needs live hot-reload? → should be `IOptionsMonitor<T>`
- [ ] Are options classes missing validation attributes? → silent null/default values at runtime

See `references/examples.md` → Section 4.

---

## HttpClient Registration Checklist

- [ ] Is `new HttpClient()` used anywhere? → socket exhaustion (TIME_WAIT ~30 seconds)
- [ ] Is `HttpClient` registered as Singleton manually? → ignores DNS changes
- [ ] Is `HttpClient` constructor-injected directly (not via Typed Client pattern)?
- [ ] Are outbound HTTP calls missing retry, timeout, and circuit breaker?

**Correct preference order:**
1. Typed Client — `AddHttpClient<TClient>()` — dedicated API wrapper
2. Named Client — `AddHttpClient("name")` — when dynamic selection needed
3. `IHttpClientFactory` directly — last resort

Add `.AddStandardResilienceHandler()` (.NET 8) for built-in retry + circuit breaker + timeout.
See `references/examples.md` → Section 5.

---

## Container Validation Checklist

- [ ] Is `ValidateScopes = true` missing in Development? → captive dependencies go undetected until runtime
- [ ] Is `ValidateOnBuild = true` missing in Development? → missing registrations surface as 500 errors in production

```csharp
// ✅ Add to Program.cs — Development only
builder.Host.UseDefaultServiceProvider(opts =>
{
    opts.ValidateScopes  = true;
    opts.ValidateOnBuild = true;
});
```

---

## Angular DI Checklist

- [ ] Is a service missing `providedIn: 'root'` and not in any `providers` array? → `NullInjectorError`
- [ ] Is a primitive value injected without `InjectionToken<T>`?
- [ ] Is `useFactory` used with undeclared `deps`? → silent `undefined` injection
- [ ] Is `ElementRef` injected into a service? → DOM coupling, breaks SSR and testing
- [ ] Are HTTP interceptors not registered via `withInterceptors([...])` (Angular 15+)?
- [ ] Is any service instantiated with `new` inside a component?

See `references/examples.md` → Section 7.

---

## Output Format

```
## DI Review

### ✅ What's Correct
[Patterns applied well — name the anti-pattern avoided and why the current approach is right.
Reference Seemann/van Deursen chapter where relevant.]

### ⚠️ Issues Found

#### [Anti-Pattern Name] — [Short descriptive title]
**Severity:** 🔴 Critical / 🟡 Moderate / 🟢 Minor
**Book Reference:** [e.g. "Ch. 5.2 — Service Locator, p. 138" — omit if uncertain]
**Location:** [File / class / line]
**Runtime consequence:** [Exact what happens: crash, memory leak, ObjectDisposedException, untestable, etc.]
**Fix:**
\`\`\`csharp  // or typescript
[complete working corrected code — not pseudocode]
\`\`\`

### 📋 Summary
[2–3 sentences. Overall DI health. Highest-priority fix and its exact runtime consequence if ignored.]
```

---

## Reference Files

Load on demand — do not load all at once:

- `references/examples.md` — working before/after code for every check category
- `assets/di-principles-practices-patterns.pdf` — full Seemann/van Deursen book

Load `references/examples.md` when:
- You need a working code pattern for a fix (captive dependency, typed client, IOptions, etc.)

Load the PDF (specific pages only via `pdftotext -f <start> -l <end>`) when:
- You need deeper context on a specific anti-pattern
- The user asks about a subtle DI concept not covered in the examples file

| Topic | Pages to load |
|-------|--------------|
| DI Patterns — Constructor, Method, Property Injection | p. 83–123 |
| Anti-Patterns — Control Freak, Service Locator, Ambient Context, Constrained Construction | p. 124–162 |
| Code Smells — Over-injection, Abstract Factory, Cyclic | p. 163–235 |
| Object Lifetime and Captive Dependencies | p. 236–280 |
| Microsoft.Extensions.DependencyInjection | p. 466+ |
