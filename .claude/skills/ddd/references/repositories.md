# Repositories — Reference

## Domain Interface Rules

```csharp
// Domain layer — pure domain types, no EF Core, no infrastructure
public interface IOrderRepository
{
    Task<Order?>                   GetByIdAsync(OrderId id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>>     GetByCustomerAsync(CustomerId id, CancellationToken ct = default);
    Task<bool>                     ExistsAsync(OrderId id, CancellationToken ct = default);
    Task                           AddAsync(Order order, CancellationToken ct = default);
    Task                           UpdateAsync(Order order, CancellationToken ct = default);
    Task                           DeleteAsync(Order order, CancellationToken ct = default);
}
// ❌ Never return IQueryable<Order> — leaks EF Core into domain
// ❌ Never have IOrderLineRepository — OrderLine is not an Aggregate Root
```

## Specification Pattern — Encapsulate Query Logic

```csharp
// ISpecification.cs — domain layer
public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria     { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    Expression<Func<T, object>>? OrderBy  { get; }
    bool IsOrderByDescending              { get; }
    int? Take                             { get; }
    int? Skip                             { get; }
}

// PendingOrdersForCustomerSpec.cs — domain layer, named after business concept
public sealed class PendingOrdersForCustomerSpec : BaseSpecification<Order>
{
    public PendingOrdersForCustomerSpec(CustomerId customerId)
    {
        AddCriteria(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending);
        AddInclude(o => o.Lines);
        AddOrderBy(o => o.CreatedAt, descending: true);
    }
}

// Usage in application layer
var pendingOrders = await _repo.ListAsync(new PendingOrdersForCustomerSpec(customerId), ct);
```

## CQRS Read Side — Bypassing Repository for Queries

```csharp
// For complex reads: use Dapper / raw SQL directly — no domain model needed
// IOrderQueryService.cs — Application layer (not domain)
public interface IOrderQueryService
{
    Task<PagedResult<OrderSummaryDto>> GetOrderSummariesAsync(
        OrderFilter filter, Pagination pagination, CancellationToken ct);
    Task<OrderDetailDto?> GetOrderDetailAsync(OrderId id, CancellationToken ct);
}

// SqlOrderQueryService.cs — Infrastructure layer, Dapper
public sealed class SqlOrderQueryService : IOrderQueryService
{
    private readonly IDbConnection _db;

    public async Task<PagedResult<OrderSummaryDto>> GetOrderSummariesAsync(
        OrderFilter filter, Pagination pagination, CancellationToken ct)
    {
        const string sql = """
            SELECT o.Id, o.Status, o.Total, o.CreatedAt, c.Name AS CustomerName
            FROM Orders o
            INNER JOIN Customers c ON c.Id = o.CustomerId
            WHERE (@Status IS NULL OR o.Status = @Status)
              AND (@CustomerId IS NULL OR o.CustomerId = @CustomerId)
            ORDER BY o.CreatedAt DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

            SELECT COUNT(*)
            FROM Orders o
            WHERE (@Status IS NULL OR o.Status = @Status)
            """;

        using var multi = await _db.QueryMultipleAsync(sql, new
        {
            filter.Status,
            CustomerId = filter.CustomerId?.Value,
            pagination.Skip,
            pagination.Take
        });

        var items = (await multi.ReadAsync<OrderSummaryDto>()).ToList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<OrderSummaryDto>(items, total, pagination);
    }
}
```

## Unit of Work

```csharp
// IUnitOfWork.cs — Domain or Application layer
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// AppDbContext implements both DbContext and IUnitOfWork
public class AppDbContext : DbContext, IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct)
        => base.SaveChangesAsync(ct);
}

// Application handler using UoW explicitly
public async Task Handle(ApproveOrderCommand cmd, CancellationToken ct)
{
    var order = await _repo.GetByIdAsync(cmd.OrderId, ct)
        ?? throw new NotFoundException(nameof(Order), cmd.OrderId);

    order.Approve();                            // domain behaviour
    await _unitOfWork.SaveChangesAsync(ct);     // single save, not per-repository
    await _dispatcher.DispatchAsync([order], ct);
}
```
