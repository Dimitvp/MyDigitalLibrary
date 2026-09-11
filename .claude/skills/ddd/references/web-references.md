# Web References — DDD Architecture & Patterns

## 1. DDD Architecture Diagram (Highly Recommended Visual)
**URL:** https://domaindrivendesign.org/architecture-ddd/
**Why it matters:** Contains a detailed layered architecture diagram showing how DDD
layers (Domain, Application, Infrastructure, Presentation) relate to each other visually.
From a DDD perspective, the architecture choice itself is secondary — DDD is primarily
about language and communication, but it requires a clean architectural boundary to
protect the domain model.

> Always open this link when reviewing project structure violations — the diagram
> makes the correct layer dependency direction immediately obvious.

---

## 2. DDD + Clean Architecture + Hexagonal Architecture — Comparison
**URL:** https://medium.com/@ignatovich.dm/understanding-software-architecture-ddd-clean-architecture-and-hexagonal-architecture-13758e59c951

### Key points to use in reviews:

**DDD Core Concepts (quick reference):**
- **Domain** — the problem area (banking, healthcare, e-commerce)
- **Entities** — objects with unique identity persisting over time (User, Order, Invoice)
- **Value Objects** — data holders with no unique identity (Address, Money)
- **Aggregates** — clusters of entities/value objects as a single consistency unit
- **Repositories** — interfaces for retrieving and storing aggregates
- **Services** — business logic that doesn't belong to an entity or value object
- **Bounded Contexts** — logical boundaries where a domain model is valid
- **Ubiquitous Language** — shared language between developers and domain experts

**When DDD is appropriate:**
- Complex business applications with intricate rules (banking, e-commerce)
- Long-lived systems where maintainability is a priority
- Large teams where separation of concerns is essential

**When DDD is overkill:**
- Simple CRUD applications
- Short-lived systems with few business rules
- Small teams with simple domains

**Clean Architecture layers (for comparison with DDD layering):**
```
+----------------------------+
|      Entities              |  ← Business rules (= DDD Domain layer)
+----------------------------+
|   Use Cases / Interactors  |  ← Application logic (= DDD Application layer)
+----------------------------+
| Interface Adapters         |  ← Controllers, Presenters (= DDD Presentation)
+----------------------------+
| Frameworks & External APIs |  ← Database, UI (= DDD Infrastructure layer)
+----------------------------+
```

**Hexagonal Architecture (Ports & Adapters) mapping to DDD:**
- **Ports** = domain interfaces (IRepository, IEmailService in Domain layer)
- **Adapters** = infrastructure implementations (SqlRepository, SmtpEmailService)
- The domain never knows about the adapters — identical to DDD's dependency rule

**DDD in Angular (relevant for .NET + Angular projects):**
- Bounded contexts map to Angular feature modules
- Domain entities/value objects can model complex UI state
- Clear separation: components handle presentation, services handle application logic
- Angular services act as Application Services in DDD terms

---

## 3. GeeksForGeeks — DDD Strategic & Tactical Patterns
**URL:** https://www.geeksforgeeks.org/system-design/domain-driven-design-ddd/

### Strategic Design concepts to reference:

**Bounded Contexts:**
- A specific area where a particular model/language is consistently used
- Sets clear boundaries for terms that may mean different things in different parts
- Breaks large complex domains into smaller manageable parts

**Context Mapping patterns:**
- **Partnership** — two teams coordinate closely
- **Shared Kernel** — common subset shared between contexts (use carefully — introduces coupling)
- **Customer-Supplier** — upstream/downstream relationship between contexts
- **Anti-Corruption Layer (ACL)** — translation layer protecting core domain from external models

**Anti-Corruption Layer — when to flag its absence:**
- Any service that directly uses an external API's model inside domain logic
- Any class that translates between models inline instead of via a dedicated ACL class
- Any shared `Customer` or `User` class used across multiple bounded contexts

**Shared Kernel — violation signals:**
- A `Common` or `Shared` project that grows and accumulates domain-specific logic
- Changes to the shared kernel requiring coordination across multiple bounded contexts
- Business-rule-specific code placed in what should be a pure infrastructure shared library

### Tactical Design patterns (quick reference card):

| Pattern | What it is | Violation signal |
|---------|-----------|-----------------|
| Entity | Object with unique identity | No Id, or equality by reference only |
| Value Object | Immutable, no identity | Has Id field, has public setters |
| Aggregate | Consistency boundary root | External code holds reference to internal entity |
| Repository | Domain interface for persistence | Returns IQueryable, lives in Infrastructure |
| Domain Service | Stateless domain logic | Has infrastructure dependencies |
| Application Service | Orchestrates use cases | Contains business rules or conditionals |
| Domain Event | Something that happened | Mutable, present tense name, dispatched before save |
| Factory | Creates complex aggregates | New'ed up inline in application services |
