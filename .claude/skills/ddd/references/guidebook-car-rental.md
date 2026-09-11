# DDD Car Rental System — Guidebook Reference
**Source:** `assets/ddd-car-rental-guide.pdf`
**Author:** Ivaylo Kenov — Code It Up Initiative
**Stack:** ASP.NET Core 3.1 + EF Core 3.1 + Angular (concepts apply to .NET 6/7/8)

> This is a practical walkthrough of building a DDD system from scratch.
> The .NET version is older (Core 3.1) but the DDD concepts, patterns, and
> project structure rules are timeless. Use it as a concrete reference for
> how each pattern looks in a real working solution.

---

## Domain Model Rules (Ch. 1 — Defining the Initial Domain Model)

The book defines these rules for every domain class — use them as a checklist:

**Immutability:**
- All domain objects must be immutable and read-only through their properties
- No public setters anywhere in the domain layer
- Collections exposed only as `IReadOnlyCollection<T>` not `List<T>`

**Constructors:**
- Constructors must always create a **valid object** — no partially constructed state
- Only the **Aggregate Root** constructors should be `public`
- All other entity constructors (internal to the aggregate) should be `internal`
- A private parameterless constructor is acceptable for EF Core binding only

**Mutations:**
- All mutating operations must be through **methods**, not property setters
  - ❌ `carAd.IsAvailable = false`
  - ✅ `carAd.ChangeAvailability()`

**Relationships:**
- Do NOT create two-way relationships — they are not needed in DDD
- Example: `CarAd` may have a `Manufacturer` property, but `Manufacturer` must NOT
  have a `CarAds` collection — relationships follow business logic direction only

**Exceptions:**
- Create a specific exception class **per aggregate** — not generic exceptions
  - `InvalidCarAdException`, `InvalidDealerException`, `InvalidPhoneNumberException`
  - All extend `BaseDomainException`
  - Use domain-specific exceptions in Guard clauses:
    `Guard.ForStringLength<InvalidCarAdException>(name, MinNameLength, MaxNameLength)`

**Markers:**
- Mark every class explicitly: `Entity<TId>`, `ValueObject`, `Enumeration`
- Mark aggregate roots: `public class CarAd : Entity<int>, IAggregateRoot`

---

## Sample Domain Model — Car Rental System

```
Domain/
├── Common/
│   ├── Entity.cs              ← base with Id + equality
│   ├── ValueObject.cs         ← equality by components
│   ├── Enumeration.cs         ← smart enum pattern
│   ├── Guard.cs               ← validation helpers
│   └── IAggregateRoot.cs      ← empty marker interface
├── Exceptions/
│   ├── BaseDomainException.cs
│   ├── InvalidCarAdException.cs
│   ├── InvalidDealerException.cs
│   ├── InvalidOptionsException.cs
│   └── InvalidPhoneNumberException.cs
└── Models/
    ├── CarAds/                 ← CarAd aggregate
    │   ├── CarAd.cs            ← Aggregate Root : Entity<int>, IAggregateRoot
    │   ├── Category.cs         ← Entity (internal to aggregate)
    │   ├── Manufacturer.cs     ← Entity (internal to aggregate)
    │   ├── Options.cs          ← Value Object
    │   └── TransmissionType.cs ← Enumeration
    └── Dealers/                ← Dealer aggregate
        ├── Dealer.cs           ← Aggregate Root : Entity<int>, IAggregateRoot
        └── PhoneNumber.cs      ← Value Object
```

---

## Infrastructure Rules (Ch. 4 — Infrastructure Layer and Persistence)

**EF Core configuration rules:**
- Domain entities must NOT have data annotations (`[Required]`, `[MaxLength]`, etc.)
- Use EF Core Fluent API in `IEntityTypeConfiguration<T>` classes in Infrastructure only
- `DbContext` must be marked `internal` — it is a persistence detail, invisible outside Infrastructure
- Value Objects map via `OwnsOne()` — they do not get their own table or Id column
- Private collection fields mapped explicitly: `.HasMany(...).UsePropertyAccessMode(PropertyAccessMode.Field)`
- For relationships without two-way navigation: configure with string-based foreign keys

**EF Core private constructor pattern:**
- Add a `private` constructor that binds only primitive properties (not navigation properties)
- Use `default!` null-forgiving operator for navigation properties in private constructor
- This is acceptable "infrastructure dirt" — keep it minimal and clearly commented

```csharp
// Private EF Core constructor — infrastructure concern only
private CarAd(string model, string imageUrl, decimal pricePerDay, bool isAvailable)
{
    this.Model        = model;
    this.ImageUrl     = imageUrl;
    this.PricePerDay  = pricePerDay;
    this.IsAvailable  = isAvailable;
    this.Manufacturer = default!;   // EF Core will populate
    this.Category     = default!;   // EF Core will populate
    this.Options      = default!;   // EF Core will populate
}
```

---

## Application Layer Rules (Ch. 5, 8, 10)

**IRepository (base — anti-corruption layer):**
```csharp
// Domain/Contracts/IRepository.cs
public interface IRepository<out TEntity> where TEntity : IAggregateRoot
{
    // Minimal — just the marker constraint
    // Specific repositories extend this with their own methods
}
```

**DataRepository (Infrastructure — generic base):**
```csharp
internal abstract class DataRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, IAggregateRoot
{
    protected CarRentalDbContext Data { get; }
    protected IQueryable<TEntity> All() => Data.Set<TEntity>();
}
```

**CQRS with MediatR rules (Ch. 8):**
- Commands change state → `IRequest<Result>` or `IRequest<Result<TOutputModel>>`
- Queries read state → `IRequest<TOutputModel>` (no Result wrapper needed)
- Handler as inner class of the Command/Query class
- Input/Output models: never inherit domain models, never reuse between scenarios
- Output models: private setters (AutoMapper works with them), no constructor needed
- Never use `IQueryable` in repository interfaces — return `IEnumerable` or specific types
- Repositories: mark as `internal`, register via Scrutor assembly scanning

**Validation (Ch. 10):**
- FluentValidation validators per command/query
- `RequestValidationBehavior` MediatR pipeline behaviour catches all validation errors
- Async validators for DB uniqueness/existence checks (inject repository into validator)
- Three error response types:
  1. Exception → single error message collection
  2. Logic error → `Result`/`Result<T>` with error collection
  3. Validation error → per-property error collection with `ValidationDetails` wrapper

---

## Factory Pattern (Ch. 7 — Builder Factories)

When aggregates have many properties, use the Builder Factory pattern instead of
long constructors:

```csharp
// Domain/Factories/ICarAdFactory.cs
public interface ICarAdFactory : IFactory<CarAd>
{
    ICarAdFactory WithManufacturer(string name);
    ICarAdFactory WithManufacturer(Manufacturer manufacturer);
    ICarAdFactory WithModel(string model);
    ICarAdFactory WithCategory(string name, string description);
    ICarAdFactory WithCategory(Category category);
    ICarAdFactory WithImageUrl(string imageUrl);
    ICarAdFactory WithPricePerDay(decimal pricePerDay);
    ICarAdFactory WithOptions(bool hasClimateControl, int numberOfSeats, TransmissionType transmission);
}

// Usage in Application Service:
var carAd = this.carAdFactory
    .WithManufacturer(request.Manufacturer)
    .WithModel(request.Model)
    .WithCategory(category)
    .WithPricePerDay(request.PricePerDay)
    .WithOptions(request.ClimateControl, request.NumberOfSeats, transmissionType)
    .Build();
```

Register via Scrutor automatic scanning — no manual registration per factory.

---

## Specification Pattern (Ch. 11 — Query Enhancements)

Use for complex queries with multiple optional filters — avoids long `if/else` chains:

```csharp
// Domain/Specifications/CarAds/CarAdByManufacturerSpecification.cs
public class CarAdByManufacturerSpecification : Specification<CarAd>
{
    private readonly string? manufacturer;
    public CarAdByManufacturerSpecification(string? manufacturer)
        => this.manufacturer = manufacturer;

    // Only apply this spec when manufacturer is provided
    protected override bool Include => this.manufacturer != null;

    public override Expression<Func<CarAd, bool>> ToExpression()
        => carAd => carAd.Manufacturer.Name.ToLower().Contains(this.manufacturer!.ToLower());
}

// Combine specifications in the query handler:
var carAdSpecification = new CarAdByManufacturerSpecification(request.Manufacturer)
    .And(new CarAdByCategorySpecification(request.Category))
    .And(new CarAdByPricePerDaySpecification(request.MinPricePerDay, request.MaxPricePerDay));
```

**When NOT to use Specification pattern:** simple single-condition queries like
`GetById`, `GetByName` — overkill for those cases.

---

## AutoMapper Rules (Ch. 11)

- NEVER map from input objects → domain entities (always use domain methods)
- CAN map from domain objects → output models (DTOs, listing models)
- Use `IMapFrom<TDomainEntity>` marker interface on output models for convention-based mapping
- Override `Mapping(Profile mapper)` only when custom member mapping is needed

---

## Testing Patterns from the Book

**Domain unit tests (co-located with source, excluded from Release build):**
```xml
<ItemGroup Condition="'$(Configuration)' == 'Release'">
  <Compile Remove="**\*.Specs.cs" />
  <Compile Remove="**\*.Fakes.cs" />
</ItemGroup>
```

**Fake factories (`*.Fakes.cs`):**
- `IDummyFactory` from FakeItEasy for creating valid aggregate instances in tests
- One `Fakes.cs` file per aggregate, co-located with the domain class

**Integration tests (MyTested.AspNetCore.Mvc):**
- Separate `TestStartup : Startup` class that replaces hard-to-test services with fakes
- `ValidateServices(services)` call verifies all required registrations exist
- Replace `UserManager<User>` and external token generators with fakes in test startup
