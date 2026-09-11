# Pattern Comparisons — Decision Guides

## Strategy vs State

| | Strategy | State |
|--|---------|-------|
| **Who switches** | External caller injects the algorithm | Object transitions itself internally |
| **Object knows states** | No — algorithm is opaque | Yes — each state knows valid transitions |
| **Mutual awareness** | Strategies don't know each other | States know next/valid states |
| **Use when** | Switching algorithms from outside | Object changes behaviour based on its own condition |
| **Example** | Payment processor changes per user choice | Order status (Pending → Confirmed → Shipped) |

```
User picks "PayPal" → inject PayPalStrategy (Strategy)
Order ships itself  → transitions to ShippedState (State)
```

---

## Strategy vs Template Method

| | Strategy | Template Method |
|--|---------|----------------|
| **Mechanism** | Composition — algorithm injected | Inheritance — algorithm inherited |
| **Varies** | Entire algorithm | Individual steps of the algorithm |
| **Runtime change** | Yes — swap strategy at runtime | No — fixed at compile time |
| **Use when** | Need to completely replace the algorithm | Algorithm skeleton is fixed, only steps vary |

```
Strategy:         new OrderPricer(new VipDiscountStrategy())  ← full algorithm swapped
Template Method:  override Format() in CsvExporter            ← one step varied
```

---

## Decorator vs Proxy

| | Decorator | Proxy |
|--|---------|-------|
| **Intent** | Add new behaviour | Control access to existing behaviour |
| **Subject creation** | Receives subject from outside | Usually creates its own subject |
| **Stacking** | Multiple decorators stacked | Usually one proxy per subject |
| **Examples** | Caching + Logging + Retry on a repository | Permission check before the real service |

```
Decorator: CachedRepo(LoggingRepo(SqlRepo))  ← layers of added behaviour
Proxy:     SecureProxy(controls access to real ReportService)
```

---

## Decorator vs Inheritance

| | Decorator (Composition) | Inheritance |
|--|--------|------------|
| **When** | Runtime — wrap what you have | Compile-time — extend the class |
| **Sealed classes** | Works with sealed | Cannot inherit sealed |
| **Multiple behaviours** | Stack decorators freely | Class explosion (CachedLoggingRetryRepo) |
| **OCP** | Open for extension, closed for modification | Modifying base affects all derived |

**Default rule:** Prefer Decorator (composition) over inheritance for cross-cutting behaviour.

---

## Factory Method vs Abstract Factory

| | Factory Method | Abstract Factory |
|--|--------------|-----------------|
| **Creates** | One product | Family of related products |
| **Mechanism** | Subclass overrides a method | Interface with multiple create methods |
| **Use when** | Single product type varies by subclass | Entire product family must be consistent |
| **Example** | `CreateNotification()` → Email or SMS | `CreateButton()` + `CreateCheckbox()` → Windows or Mac family |

```
Factory Method:   one method, one product type, subclass decides which
Abstract Factory: one factory, multiple product types, all from same family
```

---

## Facade vs Adapter

| | Facade | Adapter |
|--|--------|---------|
| **Intent** | Simplify a complex subsystem | Make incompatible interfaces work |
| **When created** | Designed in advance to reduce complexity | Applied after the fact to bridge a gap |
| **Changes interface** | Creates new simplified interface | Translates one interface to another |
| **Example** | `OrderFacade.PlaceOrder()` hides 5 services | `StripeAdapter` maps Stripe SDK to `IPaymentProvider` |

---

## Observer vs Mediator

| | Observer | Mediator |
|--|---------|---------|
| **Direction** | Subject → all observers (broadcast) | Many objects → hub → specific target |
| **Coupling** | Subject knows about observer interface | Objects only know about the mediator |
| **Example** | Domain Event → all handlers (MediatR Publish) | MediatR Send → specific handler |

```
Observer:  Publish(new OrderShippedEvent()) → ALL handlers that subscribed
Mediator:  Send(new GetOrderQuery(id))      → ONE specific handler
```

---

## Command vs Strategy

| | Command | Strategy |
|--|---------|---------|
| **Intent** | Encapsulate a request for queuing/undo | Encapsulate an algorithm for swapping |
| **State** | Often stateful (knows how to undo) | Usually stateless |
| **Use when** | Undo/redo, queuing, logging operations | Switching algorithms at runtime |
| **Example** | `MoveShapeCommand` with `Undo()` | `VipDiscountStrategy` applied to pricing |

---

## Builder vs Factory

| | Builder | Factory (Method / Abstract) |
|--|---------|--------------------------|
| **Process** | Step-by-step, many optional parameters | Single-step creation |
| **Product complexity** | Complex, many parts | Usually simpler |
| **Result control** | Caller controls construction steps | Factory controls everything |
| **Use when** | Many optional/required fields, construction has steps | Just want to abstract which class is created |

```
Builder:  QueryBuilder().FromTable("X").Where("Y").Limit(10).Build()
Factory:  notificationFactory.Create(NotificationType.Email)
```

---

## Composite vs Decorator

| | Composite | Decorator |
|--|---------|---------|
| **Structure** | Tree (parent has children) | Linear (wrapper around one object) |
| **Multiplicity** | One component has MANY children | One decorator wraps ONE component |
| **Intent** | Treat leaf and branch uniformly | Add behaviour to a single object |
| **Example** | Menu with sub-menus and items | `CachedRepo` wrapping `SqlRepo` |

---

## When NO Pattern Is Needed

Recognise these signals that a pattern is being considered prematurely:

```
Signal                               Instead of pattern
──────────────────────────────────────────────────────
Only 2 switch cases, unlikely to grow  → if/else is fine
One concrete class, no substitution    → no factory needed
Simple method extraction solves it     → no pattern needed
Team unfamiliar with the pattern       → simpler is better first
Pattern adds more code than it removes → YAGNI, don't apply it
```

> **The goal is solving the problem, not applying patterns.**
> Always show the simple version first and introduce a pattern only
> when the complexity it manages exceeds the complexity it adds.
