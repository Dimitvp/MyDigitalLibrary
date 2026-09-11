# Integration Testing — Reference

## WebApplicationFactory Pattern

```csharp
// CustomWebApplicationFactory.cs
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real DB with in-memory
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseInMemoryDatabase("IntegrationTestDb"));

            // Replace external services with fakes
            services.RemoveAll<IEmailService>();
            services.AddScoped<IEmailService, FakeEmailService>();

            services.RemoveAll<IPaymentProvider>();
            services.AddScoped<IPaymentProvider, FakePaymentProvider>();
        });
    }
}

// Test class
public class OrdersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrdersApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        // Add auth header if needed:
        // _client.DefaultRequestHeaders.Authorization =
        //     new AuthenticationHeaderValue("Bearer", TestJwtGenerator.Generate());
    }

    [Fact]
    public async Task POST_Orders_WithValidRequest_Returns201AndLocationHeader()
    {
        // Arrange
        var request = new PlaceOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Lines = new[] { new OrderLineRequest { ProductId = 1, Quantity = 2 } }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }
}
```

---

## Testcontainers — Real Database in Tests

```csharp
// Install: dotnet add package Testcontainers.MsSql
public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // Run migrations
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString);
        using var ctx = new AppDbContext(optionsBuilder.Options);
        await ctx.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

// Using with Respawn to clean between tests
public class OrderRepositoryTests : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private readonly AppDbContext _db;
    private Respawner _respawner = null!;

    public OrderRepositoryTests(SqlServerFixture fixture)
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(fixture.ConnectionString).Options);
    }

    public async Task InitializeAsync()
    {
        _respawner = await Respawner.CreateAsync(_db.Database.GetConnectionString()!,
            new RespawnerOptions { DbAdapter = DbAdapter.SqlServer });
    }

    public async Task DisposeAsync()
    {
        await _respawner.ResetAsync(_db.Database.GetConnectionString()!);
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_SavesOrderToDatabase()
    {
        var repo  = new SqlOrderRepository(_db);
        var order = new OrderBuilder().WithDefaultLines().Build();

        await repo.AddAsync(order, CancellationToken.None);

        var saved = await _db.Orders.FindAsync(order.Id);
        saved.Should().NotBeNull();
        saved!.Lines.Should().HaveCount(order.Lines.Count);
    }
}
```

---

## MediatR Handler Testing

```csharp
// Test the handler directly — no WebApplicationFactory needed for unit tests
[Fact]
public async Task PlaceOrderHandler_WithValidCommand_CreatesOrderAndRaisesEvent()
{
    // Arrange
    var repo          = Substitute.For<IOrderRepository>();
    var dispatcher    = Substitute.For<DomainEventDispatcher>();
    var handler       = new PlaceOrderHandler(repo, dispatcher);
    var command       = new PlaceOrderCommand(CustomerId.New(), new[] { ValidLine() });

    // Act
    var orderId = await handler.Handle(command, CancellationToken.None);

    // Assert
    orderId.Should().NotBe(OrderId.Empty);
    await repo.Received(1).AddAsync(Arg.Is<Order>(o => o.CustomerId == command.CustomerId),
        Arg.Any<CancellationToken>());
}
```
