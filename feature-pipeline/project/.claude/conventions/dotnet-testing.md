# Testing convention — dotnet

<!-- Framework, where tests live, naming pattern (file + method), what every test must assert, what a vacuous test looks like here. The planner writes the Test plan from this; the reviewer scores tests against it. -->

## Framework

| Concern | Tool (2026-09) | Notes |
|---|---|---|
| Runner | xUnit v3 (`xunit.v3`) on Microsoft.Testing.Platform | `global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`; every test project in the solution is MTP |
| Assertions | AwesomeAssertions (`using AwesomeAssertions;`) or Shouldly | FluentAssertions only if already pinned `[7.0.0,8.0.0)`; ≥8 is commercial |
| Test doubles | NSubstitute | no Moq in new code |
| Integration host | `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) | `public partial class Program;` in the API |
| Database | Testcontainers (`Testcontainers.MsSql` / `Testcontainers.PostgreSql`) + Respawn | never EF InMemory, never SQLite for a SQL Server/Postgres app |
| Time | `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`) | |
| Data | builders; Bogus with fixed seed | |
| Snapshots | `Verify.XunitV3` (optional) | OpenAPI doc / large response bodies only |
| Coverage | `Microsoft.Testing.Extensions.CodeCoverage` | Cobertura output |

One test framework and one assertion library per repo. Versions come from `Directory.Packages.props`.

## Location and naming

- Unit tests: `Tests/Application/Features/<Feature>/<UseCase>/` (handlers, validators), `Tests/Domain/<Aggregate>/` (entities, value objects).
- Integration tests: `Tests/Integration/<Feature>/` in a single-test-project repo; `tests/<Solution>.IntegrationTests/<Feature>/` in repos with split test projects. Shared fixtures in `Tests/Integration/Infrastructure/` (`ApiFactory`, `DatabaseFixture`, `TestAuthHandler`).
- Builders in `Tests/Builders/<Entity>Builder.cs`.
- Test class per SUT: `CreateOrderCommandHandlerTests`, `CreateOrderCommandValidatorTests`, `OrderTests`, `OrdersEndpointTests`.
- Test method: `Method_Scenario_Expected` (`Handle_StockInsufficient_ThrowsBusinessRuleException`, `Post_OtherTenantOrder_Returns404`).
- One behaviour per test. Arrange / Act / Assert blocks separated by one blank line; no `// Arrange` comments needed.
- `[Theory]` + `[InlineData]` only when the cases differ by data alone; each row is one behaviour.

## Every test must

- Assert an observable outcome: returned result fields, thrown exception type **and** error code, persisted state, HTTP status **and** problem+json `code`, or a boundary call that *is* the behaviour (email sent, message published).
- Handler success: assert the result and `SaveChangesAsync` `Received(1)`. Every throwing branch: exception type + `ErrorCodes.X` + `SaveChangesAsync` `DidNotReceive()`.
- Validator: one passing case, one failing case per rule, asserting the rule's error code.
- Integration: go through HTTP (`HttpClient` from the factory), assert status, body, and database state read through a fresh `DbContext` scope.
- Be deterministic: `FakeTimeProvider` for time, seeded `Random`/Bogus, injected id generator; no `Thread.Sleep`/`Task.Delay` waits.
- Be independent: unique data per test (fresh ids/emails); no order dependence; runnable in parallel within its collection.
- Pass `TestContext.Current.CancellationToken` to async calls (xUnit v3).

## Never

- Edit, weaken, skip (`[Fact(Skip = "...")]`), or delete an existing test to make the build green. The orchestrator's test-integrity guard blocks it. If a test is wrong: stop and write `BLOCKED: <test> — <why>` in the report.
- Assert that a mock returns what it was configured to return (vacuous).
- Only check `Received()` on a repository read.
- Use `Microsoft.EntityFrameworkCore.InMemory` or SQLite as a stand-in for the relational database.
- Call real external services (payment, email, LLM, third-party APIs). Use fakes or a stub `HttpMessageHandler`.
- Read `DateTime.Now`/`UtcNow` or `Guid.NewGuid()` inside the assertion path of a test.
- Add `[Retry]`/rerun attributes to hide flakiness.
- Catch exceptions in a test to make it pass (`try { ... } catch { }`).
- Share mutable static state between tests.

## Unit vs integration boundary

| Tier | Covers | Doubles allowed | Runs in |
|---|---|---|---|
| Unit | domain invariants, handlers, validators, mappers, pure services | NSubstitute for repositories/ports; `FakeTimeProvider` | every build, < 1 s per class |
| Integration | endpoint → handler → EF → real DB; auth policies; problem+json mapping; migrations apply | fake external HTTP clients only | every PR (Docker required) |
| E2E | deployed app via browser/API | none | pipeline e2e step |

- Domain tests use no doubles at all.
- Every new endpoint gets ≥ 3 integration tests: happy path, one failure (400/404/409), one authz (other user/tenant → 404/403, anonymous → 401).

## Fakes vs mocks

- Prefer hand-written fakes for the clock (`FakeTimeProvider`), current user (`FakeCurrentUser`), and outbound clients (`FakePaymentsClient` recording calls).
- NSubstitute only at ports (repositories, `IUnitOfWork`, external client interfaces). Never substitute the class under test or EF `DbSet`.
- Assert state over interactions; interaction assertions only when the interaction is the behaviour.

## Integration host skeleton

```csharp
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _db = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Testing").ConfigureTestServices(s =>
        {
            s.RemoveAll<DbContextOptions<AppDbContext>>();
            s.AddDbContext<AppDbContext>(o => o.UseSqlServer(_db.GetConnectionString()));
            s.AddSingleton<TimeProvider>(Clock);
            s.AddAuthentication(TestAuthHandler.Scheme)
             .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        });

    public new async ValueTask DisposeAsync() { await _db.DisposeAsync(); await base.DisposeAsync(); }
}
```
- Testcontainers 4: image in the builder constructor (the parameterless constructor is obsolete). xUnit v3 `IAsyncLifetime` uses `ValueTask`.
- Share the factory with `IClassFixture<ApiFactory>` or an assembly fixture; reset data between tests with Respawn (`Respawner.CreateAsync(connectionString, new RespawnerOptions { DbAdapter = DbAdapter.SqlServer, TablesToIgnore = ["__EFMigrationsHistory"] })` then `ResetAsync`).
- Auth: `TestAuthHandler` builds a `ClaimsPrincipal` from a request header (`X-Test-User`, `X-Test-Tenant`); one test per auth scheme uses a real JWT minted with a test signing key.

## Test data builders

```csharp
public sealed class OrderBuilder
{
    private Guid _customerId = Guid.CreateVersion7();
    private int _lines = 1;
    public OrderBuilder ForCustomer(Guid id) { _customerId = id; return this; }
    public OrderBuilder WithLines(int n) { _lines = n; return this; }
    public Order Build(TimeProvider clock) => Order.Create(_customerId, Enumerable.Range(0, _lines).Select(i => OrderLine.Create($"SKU-{i}", 1, 10m)), clock);
}
```
- Builders go through domain factory methods (never set private state by reflection).
- Bogus: `Randomizer.Seed = new Random(42)` in the fixture; per-test unique values via `Guid.CreateVersion7()`.

## Example unit test

```csharp
public sealed class CancelOrderCommandHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Handle_OrderAlreadyShipped_ThrowsBusinessRuleException()
    {
        var order = new OrderBuilder().Build(_clock);
        order.Ship(_clock);
        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var sut = new CancelOrderCommandHandler(_orders, _uow, _clock);

        var act = () => sut.Handle(new CancelOrderCommand(order.Id, "late"), TestContext.Current.CancellationToken);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be(ErrorCodes.ORDER_ALREADY_SHIPPED);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

## Example integration test

```csharp
public sealed class OrdersEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Get_OtherTenantOrder_Returns404Problem()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-b");
        var orderId = await factory.SeedOrderAsync(tenant: "tenant-a");

        var response = await client.GetAsync($"/api/v1/orders/{orderId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("code").GetString().Should().Be("ORDER_NOT_FOUND");
    }
}
```

## Coverage and commands

```bash
dotnet test -c Release                                                     # all tiers
dotnet test -c Release -- --filter-trait "Category=Unit"                   # MTP filter syntax: verify with `dotnet test -- --help`
dotnet test -c Release -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```
- Gate: changed lines ≥ 80% (pipeline coverage step reads Cobertura); no global-percentage gate.
- Excluded from coverage: `Migrations/`, generated code, `Program.cs`.

## Flakiness

- A flaky test is a bug. Quarantine within 24 h with `[Trait("Category", "Quarantine")]` + issue link in the test's first line; CI runs quarantine separately and non-blocking; fix or delete (with plan approval) within one sprint.
- Async side effects: poll with a repo-local timeout helper in `Tests/Integration/Infrastructure/Eventually.cs` (loop with `Task.Delay(50)` until the assertion passes or 5 s elapse), never a single fixed sleep.
- Tests requiring Docker fail with a clear message when Docker is unavailable; the implementer reports `BLOCKED: docker unavailable` instead of skipping them.
