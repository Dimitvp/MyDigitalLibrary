# Project Structure — DDD + Clean Architecture

## Recommended Solution Layout

```
MyApp.sln
│
├── src/
│   │
│   ├── SharedKernel/                          ← NuGet package or shared project
│   │   ├── Abstractions/
│   │   │   ├── AggregateRoot.cs
│   │   │   ├── Entity.cs
│   │   │   └── ValueObject.cs
│   │   ├── Events/
│   │   │   ├── IDomainEvent.cs
│   │   │   └── IIntegrationEvent.cs
│   │   └── Common/
│   │       ├── Guard.cs
│   │       ├── PagedResult.cs
│   │       └── Result.cs
│   │
│   ├── Ordering/
│   │   │
│   │   ├── Ordering.Domain/                   ← No external dependencies (pure C#)
│   │   │   ├── Orders/
│   │   │   │   ├── Order.cs                   ← Aggregate Root
│   │   │   │   ├── OrderId.cs                 ← Strongly-typed ID
│   │   │   │   ├── OrderLine.cs               ← Entity (internal to Order)
│   │   │   │   ├── OrderStatus.cs             ← Enum or Value Object
│   │   │   │   ├── Events/
│   │   │   │   │   ├── OrderPlacedEvent.cs
│   │   │   │   │   └── OrderShippedEvent.cs
│   │   │   │   └── IOrderRepository.cs        ← Interface defined in Domain
│   │   │   ├── Customers/
│   │   │   │   ├── CustomerId.cs
│   │   │   │   └── OrderCustomer.cs           ← Value Object (Ordering's view of Customer)
│   │   │   └── Common/
│   │   │       ├── Money.cs
│   │   │       └── Address.cs
│   │   │
│   │   ├── Ordering.Application/              ← Depends on Domain only
│   │   │   ├── Orders/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── PlaceOrder/
│   │   │   │   │   │   ├── PlaceOrderCommand.cs
│   │   │   │   │   │   ├── PlaceOrderHandler.cs
│   │   │   │   │   │   └── PlaceOrderValidator.cs
│   │   │   │   │   └── ShipOrder/
│   │   │   │   │       ├── ShipOrderCommand.cs
│   │   │   │   │       └── ShipOrderHandler.cs
│   │   │   │   ├── Queries/
│   │   │   │   │   └── GetOrderDetail/
│   │   │   │   │       ├── GetOrderDetailQuery.cs
│   │   │   │   │       ├── GetOrderDetailHandler.cs
│   │   │   │   │       └── OrderDetailDto.cs
│   │   │   │   └── EventHandlers/
│   │   │   │       └── SendOrderConfirmationHandler.cs
│   │   │   ├── Behaviours/
│   │   │   │   ├── ValidationBehaviour.cs
│   │   │   │   ├── LoggingBehaviour.cs
│   │   │   │   ├── AuthorizationBehaviour.cs
│   │   │   │   └── AuditBehaviour.cs
│   │   │   └── Abstractions/
│   │   │       ├── IOrderQueryService.cs
│   │   │       └── ICatalogClient.cs          ← Port to external context
│   │   │
│   │   ├── Ordering.Infrastructure/           ← Depends on Application + Domain
│   │   │   ├── Persistence/
│   │   │   │   ├── AppDbContext.cs
│   │   │   │   ├── Configurations/
│   │   │   │   │   └── OrderConfiguration.cs
│   │   │   │   └── Repositories/
│   │   │   │       └── SqlOrderRepository.cs
│   │   │   ├── Queries/
│   │   │   │   └── SqlOrderQueryService.cs    ← Dapper for reads
│   │   │   ├── ExternalServices/
│   │   │   │   └── HttpCatalogClient.cs       ← ACL implementation
│   │   │   └── DependencyInjection.cs         ← Extension method to register all
│   │   │
│   │   └── Ordering.Api/                      ← Depends on Application only
│   │       ├── Controllers/
│   │       │   └── OrdersController.cs
│   │       ├── Requests/
│   │       │   └── PlaceOrderRequest.cs
│   │       └── Program.cs
```

## Dependency Direction Rules

```
✅ ALLOWED:
  Api             → Application
  Api             → SharedKernel
  Application     → Domain
  Application     → SharedKernel
  Infrastructure  → Application
  Infrastructure  → Domain
  Infrastructure  → SharedKernel
  Domain          → SharedKernel

❌ NEVER:
  Domain          → Application       (domain can't know about use cases)
  Domain          → Infrastructure    (domain can't know about EF, HTTP, etc.)
  Application     → Infrastructure    (no concrete infra types in application)
  Application     → Api               (application doesn't know about controllers)
  Context A.Domain → Context B.Domain (cross-context domain coupling)
```

## csproj Reference Check

```xml
<!-- Ordering.Domain.csproj — zero external NuGet packages except SharedKernel -->
<ItemGroup>
  <ProjectReference Include="..\..\SharedKernel\SharedKernel.csproj" />
  <!-- Nothing else. No EF Core. No MediatR. No Newtonsoft. -->
</ItemGroup>

<!-- Ordering.Application.csproj -->
<ItemGroup>
  <ProjectReference Include="..\Ordering.Domain\Ordering.Domain.csproj" />
  <PackageReference Include="MediatR" Version="12.*" />
  <PackageReference Include="FluentValidation" Version="11.*" />
  <!-- No EF Core. No HTTP clients. -->
</ItemGroup>

<!-- Ordering.Infrastructure.csproj -->
<ItemGroup>
  <ProjectReference Include="..\Ordering.Application\Ordering.Application.csproj" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.*" />
  <PackageReference Include="Dapper" Version="2.*" />
  <PackageReference Include="Serilog.AspNetCore" Version="8.*" />
</ItemGroup>
```
