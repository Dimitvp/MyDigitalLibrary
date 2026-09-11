# Application Services & CQRS — Reference

## MediatR Command / Query Separation

```csharp
// Commands — change state, return minimal data
public sealed record PlaceOrderCommand(
    CustomerId              CustomerId,
    IReadOnlyList<OrderLineRequest> Lines
) : IRequest<OrderId>;

// Queries — read state, never change it
public sealed record GetOrderDetailQuery(OrderId OrderId)
    : IRequest<OrderDetailDto?>;

// Command Handler — orchestrates domain, no business logic here
public sealed class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, OrderId>
{
    private readonly IOrderRepository      _repo;
    private readonly ICustomerRepository   _customers;
    private readonly DomainEventDispatcher _dispatcher;

    public PlaceOrderHandler(
        IOrderRepository repo,
        ICustomerRepository customers,
        DomainEventDispatcher dispatcher)
    {
        _repo       = repo;
        _customers  = customers;
        _dispatcher = dispatcher;
    }

    public async Task<OrderId> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        // 1. Load — verify customer exists
        var customer = await _customers.GetByIdAsync(cmd.CustomerId, ct)
            ?? throw new NotFoundException(nameof(Customer), cmd.CustomerId);

        // 2. Execute domain behaviour — no if/else here
        var order = Order.Place(cmd.CustomerId, cmd.Lines);

        // 3. Persist
        await _repo.AddAsync(order, ct);

        // 4. Dispatch domain events after save
        await _dispatcher.DispatchAsync([order], ct);

        return order.Id;
    }
}

// Query Handler — reads only, uses query service / Dapper, not domain model
public sealed class GetOrderDetailHandler
    : IRequestHandler<GetOrderDetailQuery, OrderDetailDto?>
{
    private readonly IOrderQueryService _queries;

    public GetOrderDetailHandler(IOrderQueryService queries) { _queries = queries; }

    public Task<OrderDetailDto?> Handle(GetOrderDetailQuery q, CancellationToken ct)
        => _queries.GetOrderDetailAsync(q.OrderId, ct);
}
```

## MediatR Pipeline — Correct Behaviour Order

```csharp
// Program.cs — behaviours execute in registration order (outermost first)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

    // Order matters — wraps inside out:
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));       // 1st: log
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>)); // 2nd: authorize
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));    // 3rd: validate
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehaviour<,>));         // 4th: audit (after success)
});
```

## Thin Controller — No Logic, Just Delegation

```csharp
[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly ISender _sender;
    public OrdersController(ISender sender) { _sender = sender; }

    [HttpPost]
    [Authorize(Policy = "ProcessOrders")]
    public async Task<IActionResult> PlaceOrder(
        PlaceOrderRequest request, CancellationToken ct)
    {
        var cmd    = request.ToCommand();  // map DTO → Command
        var orderId = await _sender.Send(cmd, ct);
        return CreatedAtAction(nameof(GetOrder), new { id = orderId }, new { id = orderId });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken ct)
    {
        var dto = await _sender.Send(new GetOrderDetailQuery(OrderId.From(id)), ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
```

## Application vs Domain Service — Decision Table

| Concern | Belongs in | Why |
|---------|-----------|-----|
| Load aggregate from repo | Application Service | Infrastructure orchestration |
| Enforce business rule | Domain (Aggregate / Domain Service) | Core domain logic |
| Call external API | Application Service | Infrastructure concern |
| Coordinate two aggregates | Domain Service | Cross-aggregate domain rule |
| Map domain → DTO | Application Service | Output concern |
| Dispatch domain events | Application Service | After save orchestration |
| Send email | Application Service / Event Handler | Side effect, not domain |
| Calculate price | Domain (Aggregate or Domain Service) | Business rule |
