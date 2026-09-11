# .NET Testing Patterns — Reference

## xUnit Fundamentals

```csharp
// [Fact] — single test case
[Fact]
public void CalculateTotal_WithTwoLines_ReturnsSumOfLineTotals()
{
    // Arrange
    var line1 = OrderLine.Create(ProductId.New(), quantity: 2, unitPrice: Money.From(10m, Currency.EUR));
    var line2 = OrderLine.Create(ProductId.New(), quantity: 1, unitPrice: Money.From(15m, Currency.EUR));
    var order = Order.Place(CustomerId.New(), new[] { line1, line2 });

    // Act
    var total = order.Total;

    // Assert
    total.Should().Be(Money.From(35m, Currency.EUR));
}

// [Theory] + [InlineData] — parameterised tests
[Theory]
[InlineData(0)]
[InlineData(-1)]
[InlineData(-100)]
public void Deposit_WithNonPositiveAmount_ThrowsDomainException(decimal invalidAmount)
{
    var account = BankAccount.Open(OwnerId.New(), initialDeposit: Money.From(100m, Currency.EUR));

    var act = () => account.Deposit(Money.From(invalidAmount, Currency.EUR));

    act.Should().Throw<DomainException>();
}

// [Theory] + [MemberData] — complex parameterised data
public static IEnumerable<object[]> InvalidOrderScenarios =>
    new[]
    {
        new object[] { Array.Empty<OrderLineRequest>(), "at least one line" },
        new object[] { null,                             "cannot be null" },
    };

[Theory]
[MemberData(nameof(InvalidOrderScenarios))]
public void PlaceOrder_WithInvalidLines_ThrowsDomainException(
    OrderLineRequest[] lines, string expectedMessage)
{
    var act = () => Order.Place(CustomerId.New(), lines);
    act.Should().Throw<DomainException>().WithMessage($"*{expectedMessage}*");
}
```

---

## NSubstitute — Stubs and Mocks

```csharp
// Stub — controls return value, no verification
var repo = Substitute.For<IOrderRepository>();
repo.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
    .Returns(new Order(...));

// Mock — verify a call was made (use only when the call IS the behaviour)
var emailService = Substitute.For<IEmailService>();

await sut.ProcessAsync(order, CancellationToken.None);

await emailService.Received(1)
    .SendConfirmationAsync(Arg.Is<Order>(o => o.Id == orderId), Arg.Any<CancellationToken>());

// Fake — lightweight working implementation (prefer over mocks where possible)
public class FakeOrderRepository : IOrderRepository
{
    private readonly List<Order> _store = new();

    public Task AddAsync(Order order, CancellationToken ct)
    {
        _store.Add(order);
        return Task.CompletedTask;
    }

    public Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
        => Task.FromResult(_store.FirstOrDefault(o => o.Id == id));
}
```

---

## FluentAssertions — Readable Assertions

```csharp
// Primitives
result.Should().Be(42);
result.Should().BeGreaterThan(0).And.BeLessThan(100);
result.Should().BeApproximately(3.14m, precision: 0.01m);

// Strings
name.Should().Be("Alice");
name.Should().StartWith("Al").And.HaveLength(5);
name.Should().NotBeNullOrEmpty();

// Collections
items.Should().HaveCount(3);
items.Should().Contain(x => x.Id == expectedId);
items.Should().BeInAscendingOrder(x => x.CreatedAt);
items.Should().NotContainNulls();
items.Should().OnlyContain(x => x.IsActive);

// Exceptions
var act = () => sut.DoSomething(badInput);
act.Should().Throw<DomainException>()
   .WithMessage("*invalid*")
   .Where(e => e.Field == "Email");

// Async
var act = async () => await sut.ProcessAsync(order, ct);
await act.Should().ThrowAsync<NotFoundException>();

// Objects
result.Should().BeEquivalentTo(expected, opts =>
    opts.Excluding(x => x.CreatedAt));  // ignore timestamp in comparison
```

---

## Test Data Builder Pattern

```csharp
// Builder — avoids long Arrange sections, reveals intent
public class OrderBuilder
{
    private CustomerId _customerId = CustomerId.New();
    private List<OrderLineRequest> _lines = new();

    public OrderBuilder WithCustomer(CustomerId id) { _customerId = id; return this; }

    public OrderBuilder WithLine(ProductId productId, int qty, decimal price)
    {
        _lines.Add(new OrderLineRequest(productId, qty, Money.From(price, Currency.EUR)));
        return this;
    }

    public OrderBuilder WithDefaultLines()
    {
        _lines.Add(new OrderLineRequest(ProductId.New(), 1, Money.From(10m, Currency.EUR)));
        return this;
    }

    public Order Build() => Order.Place(_customerId, _lines);
}

// Usage — test reveals only what matters
[Fact]
public void Ship_WhenNotConfirmed_ThrowsDomainException()
{
    var order = new OrderBuilder().WithDefaultLines().Build();  // status = Pending

    var act = () => order.Ship(TrackingNumber.New());

    act.Should().Throw<DomainException>().WithMessage("*confirmed*");
}
```

---

## Shared Test Fixtures (xUnit)

```csharp
// IClassFixture — one instance per test class (expensive setup: DB, containers)
public class OrderServiceTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;
    public OrderServiceTests(DatabaseFixture db) { _db = db; }
}

// ICollectionFixture — one instance shared across multiple test classes
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

[Collection("Database")]
public class OrderQueryTests { ... }

// DatabaseFixture example
public class DatabaseFixture : IAsyncLifetime
{
    public AppDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Start real DB (Testcontainers) or use in-memory
        DbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("TestDb").Options);
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await DbContext.DisposeAsync();
}
```
