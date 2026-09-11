# C# Naming Conventions — Reference
## Source: Microsoft docs.microsoft.com/dotnet/csharp/fundamentals/coding-style

## Official Microsoft Naming Table

| Identifier type | Convention | Prefix/Suffix | Example |
|----------------|-----------|--------------|---------|
| Class | PascalCase | — | `CustomerService` |
| Interface | PascalCase | `I` prefix | `IOrderRepository` |
| Record | PascalCase | — | `OrderSummary` |
| Struct | PascalCase | — | `Point3D` |
| Enum | PascalCase (singular) | — | `OrderStatus` |
| Flags enum | PascalCase (plural) | — | `FilePermissions` |
| Delegate | PascalCase | — | `EventHandler` |
| Event | PascalCase | — | `OrderPlaced` |
| Public property | PascalCase | — | `CustomerName` |
| Public field | PascalCase | — | `MaxRetryCount` (rare) |
| Constant | PascalCase | — | `DefaultTimeout` (NOT `DEFAULT_TIMEOUT`) |
| Private field | camelCase | `_` prefix | `_orderCount` |
| Static private | camelCase | `s_` prefix | `s_instance` |
| Thread-static | camelCase | `t_` prefix | `t_currentUser` |
| Local variable | camelCase | — | `totalOrders` |
| Method parameter | camelCase | — | `customerId` |
| Generic type param | PascalCase | `T` prefix | `T`, `TEntity`, `TResult` |
| Async method | PascalCase | `Async` suffix | `GetOrderAsync` |

---

## Boolean Naming Conventions

```csharp
// ✅ Use Is, Has, Can, Should, Was prefixes
bool IsAvailable { get; }
bool HasItems    { get; }
bool CanProcess  { get; }
bool ShouldRetry { get; }
bool WasProcessed { get; }

// ❌ Avoid
bool Available;    // ambiguous
bool Processed;    // past tense without Was prefix
bool NotExpired;   // negative naming — hard to reason about double negatives
```

---

## Common Naming Mistakes

```csharp
// ❌ Type in name
List<Customer> customerList;
string nameString;
int countInt;

// ✅ Names express meaning, not type
List<Customer> customers;
string name;
int count;

// ❌ Abbreviations
Cust cust;
Mgr mgr;
proc proc;
int d;   // days? department? data?

// ✅ Descriptive names
Customer customer;
OrderManager orderManager;  // still bad if it does too much
int departmentId;

// ❌ Constants in ALL_CAPS (Java style — not .NET convention)
public const int MAX_RETRY_COUNT = 3;
public const string DEFAULT_CURRENCY = "EUR";

// ✅ Constants in PascalCase (.NET convention)
public const int    MaxRetryCount    = 3;
public const string DefaultCurrency  = "EUR";

// ❌ Async method without Async suffix
public Task GetOrder(Guid id) { }

// ✅ Always suffix async methods
public Task<Order> GetOrderAsync(Guid id) { }
```

---

## Angular/TypeScript Naming

```typescript
// Classes — PascalCase
class ProductCardComponent { }
class OrderService { }

// Files — kebab-case
// product-card.component.ts
// order.service.ts
// auth.guard.ts

// Observables — $ suffix
products$:    Observable<Product[]>;
currentUser$: Observable<User | null>;

// Signals — NO $ suffix
products:    Signal<Product[]>;   // ✅
products$:   Signal<Product[]>;   // ❌ $ is for observables only

// Constants — UPPER_SNAKE_CASE for module-level
export const API_BASE_URL  = new InjectionToken<string>('API_BASE_URL');
export const MAX_FILE_SIZE = 10 * 1024 * 1024;

// Interfaces — do NOT prefix with I in TypeScript (unlike C#)
interface Product { }      // ✅
interface IProduct { }     // ❌ not TypeScript convention

// Enums — PascalCase values
enum OrderStatus {
    Pending   = 'PENDING',
    Confirmed = 'CONFIRMED',
    Shipped   = 'SHIPPED',
}
```

---

## Namespace and Project Naming

```
Company.Product.Feature

✅ MyCompany.Ordering.Domain
✅ MyCompany.Ordering.Application
✅ MyCompany.Ordering.Infrastructure
✅ MyCompany.Shared.Domain

❌ MyCompany.OrderingDomain  (no separation)
❌ MyCompany.Stuff           (meaningless)
❌ Order                     (too short, too generic)
```
