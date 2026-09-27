# 05 — Backend Stack Feature Skills & Testing Conventions (Sept 2026)

Research input for rewriting the per-stack feature skills (dotnet, node, python, python-flask, python-django). Written as imperative team defaults for agents building production web backends from scratch. Each stack section maps onto an expanded skill layout:

§1 Layout & scaffolding · §2 Platform wiring (config, DI, logging/OTel, health) · §3 HTTP contract (errors, validation, auth, pagination, versioning, OpenAPI, rate limit, CORS, caching) · §4 Data (access, migrations, transactions, concurrency, idempotency) · §5 Cross-cutting (time, resilience, jobs, perf) · §6 Tooling (Docker, CI, lint/format, packages) · §7 Testing conventions · §8 DO/DON'T · §9 LLM mistake catalogue.

---

## 0. Version baseline (verified Sept 2026)

| Area | Default | Notes |
|---|---|---|
| .NET | **.NET 10 LTS** (Nov 2025, supported to Nov 2028), C# 14, ASP.NET Core 10, EF Core 10 (LTS to 10 Nov 2028) | EF 10 needs .NET 10 runtime |
| .NET test | **xUnit v3** on Microsoft.Testing.Platform (MTP); TUnit acceptable for greenfield | FluentAssertions ≥8 is commercial (Xceed) — use **AwesomeAssertions** (Apache-2 fork) or **Shouldly** |
| .NET libs | MediatR ≥13 / AutoMapper ≥15 dual-licensed (RPL-1.5 / commercial; free "Community" tier <$5M revenue, registration + license key) | Pin deliberately; see §dotnet-2 |
| Node | **Node 24** (Active LTS until Oct 2026, EOL Apr 2028); Node 22 = maintenance, EOL Apr 2027 | |
| TypeScript | **TS 6.0** as the project `typescript` dep; TS 7.0 (Go-native, 3 Aug 2026) has no stable programmatic API until 7.1 → typescript-eslint can't use it yet | TS 6 defaults: `strict`, `module: esnext`, `target: es2025`, `types: []` |
| Node frameworks | Fastify 5 (preferred for new services), Express 5, NestJS 11 → **NestJS 12 released 28 Aug 2026** (ESM-first, Standard Schema validation, Vitest/oxlint defaults for new ESM projects) | |
| Node tooling | ESLint **10** (Feb 2026, flat config only), typescript-eslint, Prettier or Biome 2; Vitest 4.x; pnpm 11 (Apr 2026; `minimumReleaseAge` default 1 day) | |
| Node libs | Zod 4 (≥4.2 for `@fastify/type-provider-zod`), Prisma 7 (Rust-free client, driver adapters mandatory) or Drizzle, pino | |
| Python | **3.13** baseline, 3.14 allowed (Oct 2025) | `datetime.utcnow()` deprecated since 3.12 |
| Python web | FastAPI ~0.14x (Pydantic v1 support removed), Pydantic 2.x, SQLAlchemy 2.0/2.1, Alembic; Flask 3.1.x; **Django 5.2 LTS** (Py 3.10–3.14, supported to Apr 2028) or **Django 6.0** (Dec 2025, Py ≥3.12); DRF 3.17 (Mar 2026, first with official Django 6 / Py 3.14 support) | |
| Python tooling | uv (+ `uv.lock`), ruff (lint + format), mypy or pyright in CI (ty = beta, pilot only), pytest ≥8.4, pytest-asyncio 1.x (the `event_loop` fixture is gone) | |

---

## 1. .NET (ASP.NET Core 10 / EF Core 10)

### 1.1 Layout & scaffolding

Default: **vertical slices inside a modular monolith**, with a thin Domain project only when invariants are rich. Full clean architecture (Domain/Application/Infrastructure/Api, four projects) only when the domain justifies it. Slices keep request, validator, handler, endpoint, and DTOs in one folder, so an agent edits one place per feature.

```
src/
  Acme.Api/                 # host: Program.cs, composition root, endpoints registration
    Features/
      Orders/
        CreateOrder.cs      # Request, Validator, Handler, Endpoint (one file or folder)
        GetOrder.cs
        OrderMappings.cs
    Infrastructure/         # DbContext, EF configs, outbox, http clients
    Common/                 # ProblemDetails, pagination, behaviors
  Acme.Domain/              # (optional) entities, value objects, domain events — no EF/ASP refs
tests/
  Acme.UnitTests/
  Acme.IntegrationTests/    # WebApplicationFactory + Testcontainers
Directory.Build.props       # TFM, Nullable, TreatWarningsAsErrors, analyzers
Directory.Packages.props    # Central Package Management
global.json                 # SDK pin + test runner = MTP
.editorconfig
Acme.slnx
```

Scaffold:
```bash
dotnet new globaljson --sdk-version 10.0.100 --roll-forward latestFeature
dotnet new sln -n Acme                    # .NET 10 SDK creates Acme.slnx by default
dotnet new webapi -n Acme.Api -o src/Acme.Api     # minimal APIs, Microsoft.AspNetCore.OpenApi, no Swashbuckle
dotnet new xunit3 -n Acme.IntegrationTests -o tests/Acme.IntegrationTests   # needs xunit.v3.templates
dotnet sln add src/**/*.csproj tests/**/*.csproj
dotnet new editorconfig
```
`Directory.Build.props`: `<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors><AnalysisLevel>latest-recommended</AnalysisLevel><EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`. `Directory.Packages.props`: `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`; CI restores with `--locked-mode`.

global.json test runner (MTP; all test projects in the solution must be MTP):
```json
{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" },
  "test": { "runner": "Microsoft.Testing.Platform" } }
```

### 1.2 Platform wiring

**Config/secrets**: strongly typed options, validated at startup.
```csharp
builder.Services.AddOptions<PaymentsOptions>()
    .BindConfiguration(PaymentsOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```
Local secrets: `dotnet user-secrets`; prod: Key Vault / env vars (`Payments__ApiKey`). Never commit secrets to `appsettings*.json`. Inject `IOptions<T>` for singletons, `IOptionsSnapshot<T>` for scoped reload, `IOptionsMonitor<T>` for change callbacks.

**DI**: built-in container; register per slice through `IServiceCollection` extension methods (`services.AddOrdersFeature()`). Lifetimes: DbContext scoped; HttpClient via `IHttpClientFactory` (typed clients); `TimeProvider.System` singleton. Enable `ValidateScopes` + `ValidateOnBuild` (on by default in Development — keep it on in tests too). Use keyed services (`[FromKeyedServices("x")]`) instead of factories-of-strings.

**Mediator**: MediatR ≥13 needs a license key (free Community tier under $5M revenue, still requires registration). Options: (a) stay on MediatR with a key set in config (`cfg.LicenseKey = ...`); (b) pin MediatR 12.x (Apache-2, no further updates); (c) no mediator — endpoints call handler classes directly (`CreateOrderHandler` injected into the endpoint), cross-cutting via endpoint filters. Team default for new services: **(c) or an existing licensed setup**; never add an unlicensed MediatR ≥13 silently. Same for AutoMapper ≥15: prefer hand-written `ToDto()` extension methods or Mapperly (source generator).

**Logging**: `ILogger<T>` + source-generated `[LoggerMessage]` for hot paths; structured templates, never interpolation.
```csharp
logger.LogInformation("Order {OrderId} placed for {CustomerId}", order.Id, customerId); // good
logger.LogInformation($"Order {order.Id} placed");                                      // bad: CA2254
```
**OpenTelemetry**:
```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("acme-api"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
                       .AddEntityFrameworkCoreInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
                       .AddRuntimeInstrumentation().AddOtlpExporter());
builder.Logging.AddOpenTelemetry(o => { o.IncludeScopes = true; o.IncludeFormattedMessage = true; });
```
Configure endpoint via `OTEL_EXPORTER_OTLP_ENDPOINT`. Azure: `Azure.Monitor.OpenTelemetry.AspNetCore` (`UseAzureMonitor()`). If using Aspire, put this in `ServiceDefaults`. .NET 10 adds built-in auth metrics (`aspnetcore.authorization.*`).

**Health**: `AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: ["ready"])`; map `/health/live` (no dependencies, `Predicate = _ => false`) and `/health/ready` (tag `ready`). Keep them anonymous but excluded from rate limiting and OpenAPI.

### 1.3 HTTP contract

**Errors (RFC 9457)**: one global path.
```csharp
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>(); // IExceptionHandler: map known exceptions
app.UseExceptionHandler();
app.UseStatusCodePages();
```
Return `TypedResults.Problem(...)`/`TypedResults.ValidationProblem(errors)` for expected failures (prefer a `Result<T>`/error-code model over exceptions for business rules); reserve exceptions for the unexpected. Stable `type` URIs per error code (`https://errors.acme.dev/orders/insufficient-stock`), `application/problem+json`, never leak stack traces or SQL (RFC 9457 §5).

**Validation**: .NET 10 minimal APIs have built-in validation: `builder.Services.AddValidation();` validates DataAnnotations/`IValidatableObject` on parameters and records and returns 400 ProblemDetails; `.DisableValidation()` per endpoint. Use FluentValidation 12 for complex/async rules via an endpoint filter; pick **one** per project. Validate at the edge; the domain enforces invariants in constructors/methods.

**Auth**: JWT bearer for APIs (`AddAuthentication().AddJwtBearer(o => { o.Authority = ...; o.Audience = ...; })`; `MapInboundClaims = false` to keep raw claim names). OIDC + cookie for BFF/web. Breaking in .NET 10: cookie auth returns 401/403 (not 302 redirect) for known API endpoints — don't write redirect-suppression hacks. **Authorization**: policies, not role strings sprinkled in code: `AddAuthorizationBuilder().AddPolicy("orders:write", p => p.RequireClaim("scope", "orders:write"))`, `.RequireAuthorization("orders:write")` on the group; fallback policy = authenticated user; resource checks via `IAuthorizationService.AuthorizeAsync(user, order, "SameTenant")`.

**Endpoints**: group per slice: `var g = app.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization();`. Return `Results<Ok<OrderDto>, NotFound, ProblemHttpResult>` for accurate OpenAPI.

**Pagination/filtering**: cursor (keyset) by default for large/append-only tables; offset (`page`,`pageSize` capped at 100) acceptable for admin grids. Response envelope `{ items, nextCursor }` or `{ items, page, pageSize, totalCount }`. Always a deterministic `ORDER BY` (include PK as tiebreaker). Whitelist sortable fields; never build dynamic LINQ from raw strings.

**Versioning**: `Asp.Versioning.Http` 10.x (+ `Asp.Versioning.Mvc.ApiExplorer`, `Asp.Versioning.OpenApi`) — `app.NewVersionedApi("Orders").MapGroup("api/orders").HasApiVersion(1.0)`, `app.MapOpenApi().WithDocumentPerVersion()`. URL segment (`/v1/`) or header — pick one team-wide; default URL segment.

**OpenAPI**: `builder.Services.AddOpenApi()` (OpenAPI 3.1 default in .NET 10; `Microsoft.OpenApi` 2.x — `OpenApiAny` → `JsonNode` in transformers); `app.MapOpenApi()`; UI via `Scalar.AspNetCore` (`app.MapScalarApiReference()`) in Development. Swashbuckle is no longer in templates — don't add it. `<GenerateDocumentationFile>true</GenerateDocumentationFile>` feeds XML comments into the doc. Build-time doc: `Microsoft.Extensions.ApiDescription.Server` to emit the JSON in CI and diff it.

**Rate limiting**: built-in `AddRateLimiter` — partition by user/tenant or IP, `RejectionStatusCode = 429`, `OnRejected` sets `Retry-After`; `.RequireRateLimiting("per-user")` on groups. Behind a proxy, `UseForwardedHeaders` with `KnownProxies`/`KnownNetworks` first.

**CORS**: named policy with explicit origins from config; never `AllowAnyOrigin()` with `AllowCredentials()` (runtime error anyway). Order: `UseForwardedHeaders → UseExceptionHandler → UseHttpsRedirection? → UseCors → UseAuthentication → UseAuthorization → UseRateLimiter → endpoints`.

**Caching**: `HybridCache` (`Microsoft.Extensions.Caching.Hybrid`) — L1 in-memory + optional L2 (Redis) with stampede protection and tag invalidation:
```csharp
var dto = await cache.GetOrCreateAsync($"order:{id}",
    async ct => await db.Orders.Where(o => o.Id == id).Select(ToDto).FirstOrDefaultAsync(ct),
    tags: ["orders"], cancellationToken: ct);
await cache.RemoveByTagAsync("orders", ct);
```
HTTP output caching (`AddOutputCache`) only for anonymous/public GETs. Never cache per-user data under a shared key.

### 1.4 Data

**EF Core 10**: `AddDbContextPool<AppDbContext>` (or `AddDbContext` if you need scoped state in the context); `IEntityTypeConfiguration<T>` per entity; `ApplyConfigurationsFromAssembly`. Reads: `AsNoTracking()` + projection `Select(...)` into DTOs. Bulk: `ExecuteUpdateAsync`/`ExecuteDeleteAsync` (EF 10 accepts a regular lambda with conditional `SetProperty` calls). New in 10: named query filters (`HasQueryFilter("SoftDelete", ...)` + `IgnoreQueryFilters(["SoftDelete"])`), `LeftJoin`/`RightJoin` operators, complex types mapped to JSON (`ComplexProperty(c => c.Address, c => c.ToJson())`), better `Contains` parameterization. Raw SQL: `FromSql($"... {x}")` (parameterized interpolation) — EF 10 analyzer flags concatenation in `FromSqlRaw`.

**Migrations**: `dotnet ef migrations add <Name> -p src/Acme.Api`; deploy with an **idempotent script** or **migration bundle** (`dotnet ef migrations bundle --self-contained`), never `Database.Migrate()` at app startup in multi-instance prod. Note: EF 10 no longer wraps all migrations in a single transaction. **Expand/contract** for zero downtime: (1) add nullable column / new table, deploy code that writes both; (2) backfill in batches; (3) switch reads; (4) drop old column in a later release. Never rename a column in one step; never add a NOT NULL column without default on a big table.

**Transactions**: one `SaveChangesAsync` per request is already atomic. Explicit `BeginTransactionAsync` only when spanning multiple saves/raw SQL; with retrying execution strategies wrap in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`. Cross-boundary side effects (messages, emails) → **transactional outbox** table written in the same transaction, dispatched by a background worker.

**Optimistic concurrency**: SQL Server `[Timestamp] byte[] RowVersion`; PostgreSQL (Npgsql) `uint Version` mapped to `xmin` via `.IsRowVersion()`. Catch `DbUpdateConcurrencyException` → 409 ProblemDetails. Expose version as `ETag`; require `If-Match` on PUT/PATCH (412 on mismatch).

**Idempotency**: `Idempotency-Key` header on non-idempotent POSTs (payments, orders). Store `(key, user, request hash, status, response)` with a unique index; replay stored response on repeat; 409 if same key with a different body; 422/409 if in-flight. TTL 24h+.

### 1.5 Cross-cutting

**Time**: inject `TimeProvider`; store `DateTimeOffset` (or UTC `DateTime` with `Kind=Utc`); `DateOnly` for dates. Never `DateTime.Now` in domain/handlers. Tests use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`, `.Advance(TimeSpan)`). Pass `TimeProvider` to `Task.Delay`, `PeriodicTimer`, `CancellationTokenSource` where supported.

**Resilience**: `Microsoft.Extensions.Http.Resilience` (Polly v8): `.AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods())` — defaults: total timeout 30s, 3 retries exponential+jitter, circuit breaker, 10s attempt timeout. Add **one** resilience handler per client — never stack. Non-HTTP: `AddResiliencePipeline("db", b => b.AddRetry(...).AddTimeout(...))`. Don't retry non-idempotent calls unless an idempotency key is sent.

**Background jobs**: `BackgroundService`/`IHostedService` for in-process loops (create a scope per iteration: `using var scope = sp.CreateScope()`); durable jobs → Hangfire/Quartz/Azure Functions/queue consumers (MassTransit ≥9 is commercial; Wolverine or raw Azure Service Bus SDK are alternatives). Always honour `stoppingToken`; catch-and-log per iteration so one failure doesn't kill the host.

**Performance pitfalls**: N+1 via lazy loading (keep lazy loading off; `Include` or projection; `AsSplitQuery()` for multiple collection includes); `.Result`/`.Wait()`/`GetAwaiter().GetResult()` (thread-pool starvation); `ToList()` before `Where`; missing `CancellationToken` propagation; `new HttpClient()` per call (socket exhaustion); `async void`; unbounded `Task.WhenAll` fan-out (use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism`); large payloads buffered into strings instead of streaming; `Count() > 0` instead of `AnyAsync()`.

### 1.6 Tooling

Dockerfile (chiseled, non-root):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.*.props *.slnx ./
COPY src/Acme.Api/Acme.Api.csproj src/Acme.Api/
RUN dotnet restore src/Acme.Api/Acme.Api.csproj --locked-mode
COPY . .
RUN dotnet publish src/Acme.Api -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Acme.Api.dll"]
```
(Alternative: `dotnet publish /t:PublishContainer` — no Dockerfile.) Local dev: `docker-compose.yml` with `postgres:17`/`mcr.microsoft.com/mssql/server:2022-latest`, `redis:7`, and an OTel collector or Aspire dashboard (`mcr.microsoft.com/dotnet/aspire-dashboard`) on 18888/4317; healthcheck on DB before API (`depends_on: condition: service_healthy`). Or use .NET Aspire AppHost for local orchestration.

CI:
```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes
dotnet build -c Release --no-restore        # warnings as errors
dotnet test -c Release --no-build            # MTP; add coverage extension
dotnet test --no-build -- --coverage --coverage-output-format cobertura   # with Microsoft.Testing.Extensions.CodeCoverage
dotnet list package --vulnerable --include-transitive
```
Analyzers: built-in .NET analyzers (`AnalysisLevel latest-recommended`), plus optionally `Meziantou.Analyzer`, `Roslynator.Analyzers`; `.editorconfig` severity for IDE rules so `dotnet format` enforces style.

### 1.7 Testing conventions (.NET)

- **Framework**: xUnit v3 (`xunit.v3`, runs as an executable on MTP). TUnit is faster and source-generated; acceptable for new repos but keep one framework per repo. Assertions: **AwesomeAssertions** (drop-in for FluentAssertions 7 API, namespace `AwesomeAssertions`) or **Shouldly**; do not add FluentAssertions ≥8 without a license. Mocks: **NSubstitute** (avoid Moq after the 2023 SponsorLink incident unless already standard). Snapshot: **Verify** (`Verify.XunitV3`) for API responses/OpenAPI doc.
- **Naming**: `Method_Scenario_ExpectedResult` or `Should_<result>_When_<condition>`; class per SUT (`CreateOrderHandlerTests`). AAA with blank lines; one behaviour per test.
- **Unit** = domain logic, handlers with fakes, validators, mappers. **Integration** = endpoint → DB via `WebApplicationFactory<Program>` + **Testcontainers** (real Postgres/SQL Server, never EF InMemory for relational behaviour) + **Respawn** to reset between tests.
```csharp
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public async ValueTask InitializeAsync() => await _db.StartAsync();
    protected override void ConfigureWebHost(IWebHostBuilder b) => b.ConfigureTestServices(s =>
    {
        s.RemoveAll<DbContextOptions<AppDbContext>>();
        s.AddDbContext<AppDbContext>(o => o.UseNpgsql(_db.GetConnectionString()));
        s.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    });
    public new async ValueTask DisposeAsync() => await _db.DisposeAsync();
}
```
(xUnit v3 `IAsyncLifetime` uses `ValueTask`. Testcontainers 4.x: pass the image to the builder constructor — the parameterless ctor is obsolete.) Add `public partial class Program;` for `WebApplicationFactory<Program>`. Auth in tests: a test auth handler scheme or mint JWTs with a test signing key.
- **Test data**: builders (`new OrderBuilder().WithLines(2).Build()`), Bogus with fixed seed (`Randomizer.Seed = new Random(42)`), unique values per test for parallel safety.
- **Determinism**: `FakeTimeProvider`, injected `Random`/`Guid` factories, no `Thread.Sleep`; poll with timeout for async side effects.
- **Fakes vs mocks**: prefer hand-rolled fakes for repositories/clock/clients; mock only at process boundaries; assert on state, not call counts, unless the call is the behaviour (e.g., "email sent").
- **Coverage**: `Microsoft.Testing.Extensions.CodeCoverage` → Cobertura; gate on changed lines (e.g. 80%), not global %.
- **Flakiness**: a flaky test is a bug — quarantine with `[Trait("Category","Quarantine")]` + issue link within 24h; never `Retry` attributes as a fix.

### 1.8 DO / DON'T (.NET)

1. DO pin the SDK in `global.json` and use Central Package Management + lock files.
2. DO enable `Nullable`, `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`.
3. DO pass `CancellationToken` from endpoint to every async I/O call.
4. DO use `TypedResults` + `Results<...>` unions for accurate OpenAPI.
5. DO return ProblemDetails for every non-2xx; include `traceId`.
6. DO validate options with `ValidateOnStart()`.
7. DO use `IHttpClientFactory` typed clients + `AddStandardResilienceHandler`.
8. DO use `AsNoTracking()` + projections for reads.
9. DO use keyset pagination on large tables with a deterministic order.
10. DO add concurrency tokens to aggregates edited by humans; map to ETag/If-Match.
11. DO use the outbox for side effects that must follow a commit.
12. DO inject `TimeProvider`; store UTC/`DateTimeOffset`.
13. DO ship migrations as bundles/idempotent scripts; follow expand/contract.
14. DO use source-generated `[LoggerMessage]` in hot paths.
15. DO test endpoints through `WebApplicationFactory` against Testcontainers.
16. DON'T call `.Result`, `.Wait()`, `GetAwaiter().GetResult()` or write `async void` (except event handlers).
17. DON'T call `Database.Migrate()`/`EnsureCreated()` at startup in production.
18. DON'T use EF InMemory provider for integration tests.
19. DON'T use string-interpolated log messages or log PII/secrets/tokens.
20. DON'T add Swashbuckle, Newtonsoft.Json (use System.Text.Json), or AutoMapper/MediatR ≥ licensed versions without a key.
21. DON'T `new HttpClient()` per request or stack resilience handlers.
22. DON'T throw exceptions for expected business outcomes in hot paths — return results/ProblemDetails.
23. DON'T expose EF entities directly as API contracts.
24. DON'T `AllowAnyOrigin` in prod or combine it with credentials.
25. DON'T capture scoped services (DbContext) in singletons/background services — create a scope.

### 1.9 LLM mistakes to catch (.NET)

- Uses `Startup.cs`/`WebHostBuilder`/`IWebHostBuilder` style or `services.AddSwaggerGen()` in a .NET 10 project.
- Generates `DateTime.Now`/`DateTime.UtcNow` directly instead of `TimeProvider`.
- Adds `FluentAssertions` latest (commercial) or `Moq` by default; `Microsoft.EntityFrameworkCore.InMemory` for "integration" tests.
- Hallucinated APIs: `services.AddProblemDetails().AddFluentValidation()`, `app.UseProblemDetails()` (Hellang package, not built-in), `AddMediatR(typeof(Program))` (old v11 signature — now `cfg => cfg.RegisterServicesFromAssemblyContaining<Program>()`).
- `IActionContextAccessor`/`WebHost.CreateDefaultBuilder` (obsolete); `Microsoft.OpenApi` v1 types (`OpenApiString`) in transformers — v2 uses `JsonNode`.
- `new PostgreSqlBuilder().WithImage(...)` (obsolete ctor in Testcontainers 4).
- xUnit v2 patterns in v3 projects (`Task InitializeAsync` vs `ValueTask`, `Xunit.Abstractions` namespace for `ITestOutputHelper` — now `Xunit`).
- `catch (Exception) { }` swallow; `ConfigureAwait(false)` sprinkled in ASP.NET app code (unnecessary — no sync context).
- `ToListAsync()` then filter in memory; `Include` chains for read models instead of `Select`.
- `[Authorize(Roles="Admin")]` string roles everywhere instead of policies.

---

## 2. Node.js (TypeScript; Fastify 5 default, Express 5, NestJS 11/12)

### 2.1 Layout & scaffolding

Default: **feature modules (vertical slices)** + a small `platform/` for wiring. Each module owns routes, schemas, service, repository. Framework-agnostic domain logic lives in plain TS functions/classes so it's unit-testable without HTTP.

```
src/
  app.ts                 # buildApp(opts): FastifyInstance — no listen() (testable)
  server.ts              # listen, signals, graceful shutdown
  instrumentation.ts     # OTel SDK, loaded via --import before app
  config.ts              # zod-validated env
  platform/              # db.ts, logger.ts, errors.ts (problem+json), auth.ts, health.ts
  modules/
    orders/
      orders.routes.ts   # route + schema registration
      orders.schemas.ts  # zod schemas → types + OpenAPI
      orders.service.ts  # business logic, no req/res
      orders.repo.ts     # data access
      orders.test.ts     # co-located unit tests
test/integration/        # app.inject / supertest + Testcontainers
db/migrations/
```
Scaffold (Fastify):
```bash
mkdir svc && cd svc && pnpm init
pnpm add fastify @fastify/type-provider-zod zod @fastify/swagger @fastify/swagger-ui @fastify/helmet @fastify/cors @fastify/rate-limit @fastify/jwt pino
pnpm add -D typescript@6 @types/node@24 tsx vitest @vitest/coverage-v8 eslint@10 typescript-eslint @eslint/js prettier testcontainers @testcontainers/postgresql
npx tsc --init
```
NestJS: `npx @nestjs/cli new svc --strict --package-manager pnpm` (NestJS 12 new ESM projects default to Vitest + oxlint). Express 5: only when the team already has Express middleware investment.

`package.json`: `"type": "module"`, `"engines": {"node": ">=24"}`, `"packageManager": "pnpm@11.x"` (Corepack). `tsconfig.json` (TS 6):
```json
{ "compilerOptions": { "target": "es2024", "module": "nodenext", "moduleResolution": "nodenext",
  "strict": true, "noUncheckedIndexedAccess": true, "exactOptionalPropertyTypes": true,
  "verbatimModuleSyntax": true, "isolatedModules": true, "types": ["node"],
  "outDir": "dist", "rootDir": "src", "sourceMap": true, "skipLibCheck": true } }
```
TS 6 defaults `types` to `[]` — list `"node"` explicitly. Don't use `baseUrl`, `moduleResolution: node`, `esModuleInterop: false` (deprecated in 6, errors in 7). Keep `typescript@6` until typescript-eslint supports TS 7's API (7.1); TS 7 native `tsc` can be trialled as a CI type-check step for speed.

### 2.2 Platform wiring

**Config**: parse `process.env` once with zod; fail fast; export a frozen typed object. `.env` only for local (`node --env-file=.env` — built-in, no dotenv needed on Node ≥20.6). Secrets from the platform (Key Vault / Secrets Manager → env). Never read `process.env` outside `config.ts`.
```ts
const Env = z.object({ NODE_ENV: z.enum(['development','test','production']), PORT: z.coerce.number().default(3000),
  DATABASE_URL: z.url(), JWT_ISSUER: z.url(), LOG_LEVEL: z.enum(['debug','info','warn','error']).default('info') });
export const config = Object.freeze(Env.parse(process.env));
```
(Zod 4: top-level `z.url()`, `z.email()`, `z.uuid()`; `error` param replaces `message`/`errorMap`.)

**DI**: Fastify — plugins + `decorate` (`app.decorate('db', db)`) with `fastify-plugin` for shared deps, typed via declaration merging; or explicit factory composition (`buildApp({ db, clock })`) — preferred: constructor/param injection, no global singletons, so tests pass fakes. NestJS — built-in module DI.

**Logging**: pino (Fastify's built-in logger). JSON in prod, `pino-pretty` only in dev (transport). Log with objects: `req.log.info({ orderId }, 'order placed')`. Redact: `redact: ['req.headers.authorization','*.password']`. Request id: `genReqId` honouring `x-request-id`; correlate with trace id.

**OpenTelemetry**: `@opentelemetry/sdk-node` + `@opentelemetry/auto-instrumentations-node` + OTLP exporter in `instrumentation.ts`; start **before** app import: `node --import ./dist/instrumentation.js dist/server.js`. For ESM auto-instrumentation register the loader hook (`register('@opentelemetry/instrumentation/hook.mjs', import.meta.url)` inside the instrumentation file or `--experimental-loader`). Configure via `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_ENDPOINT`. Add `@opentelemetry/instrumentation-pino` for log/trace correlation.

**Health**: `/health/live` (process up) and `/health/ready` (DB ping with short timeout). **Graceful shutdown**: on SIGTERM → stop accepting, `await app.close()` (drains), close DB pool, `sdk.shutdown()`; set `terminationGracePeriodSeconds` > drain time.

### 2.3 HTTP contract

**Errors**: single `setErrorHandler` producing `application/problem+json`; domain errors are typed classes (`class NotFoundError extends AppError { status = 404; type = '.../not-found' }`). Map zod validation errors to 400 with `errors: [{ path, message }]`. Unknown errors → 500 with generic title; log the error with stack, never return it.
```ts
app.setErrorHandler((err, req, reply) => {
  const p = toProblem(err, req.id);          // AppError | validation | unknown
  if (p.status >= 500) req.log.error({ err }, 'unhandled');
  reply.status(p.status).type('application/problem+json').send(p);
});
```
Express 5: rejected promises in async handlers are forwarded to error middleware automatically — remove `express-async-errors`/try-catch-next boilerplate; error middleware has 4 args and is registered last. Express 5 path syntax changed (path-to-regexp 8: `/*splat` not `*`, `{/:optional}` not `:optional?`).

**Validation**: zod schemas per route via `@fastify/type-provider-zod` (package moved to the `@fastify` scope; needs Zod ≥4.2). `app.setValidatorCompiler(validatorCompiler); app.setSerializerCompiler(serializerCompiler);` — response schemas also strip unknown fields (prevents leaking columns). NestJS 11: global `ValidationPipe({ whitelist: true, forbidNonWhitelisted: true, transform: true })` with class-validator; NestJS 12: Standard Schema (zod/valibot) route schemas.

**Auth**: JWT verification via JWKS (`jose` `createRemoteJWKSet` + `jwtVerify` with `issuer`, `audience`, `algorithms`) or `@fastify/jwt`; never `jwt.decode` without verify; never accept `alg: none`. Cookie sessions for browser apps: `httpOnly; Secure; SameSite=Lax`, CSRF token for unsafe methods. **Authorization**: explicit per-route `preHandler: [requireScope('orders:write')]`; resource-level checks in the service (tenant id from the token, never from body).

**Pagination/versioning/OpenAPI**: cursor pagination (`?limit=50&cursor=<opaque base64>`), max limit enforced in schema. URL-prefix versioning via `app.register(v1Routes, { prefix: '/v1' })`. OpenAPI generated from the zod schemas (`@fastify/swagger` + `transform: jsonSchemaTransform`); UI only outside prod; commit/diff the generated spec in CI. NestJS: `@nestjs/swagger`.

**Rate limit/CORS/headers**: `@fastify/rate-limit` (Redis store when >1 instance; key by user id then IP), `@fastify/cors` with explicit origin list from config, `@fastify/helmet`. Express: `express-rate-limit`, `cors`, `helmet`. Set `trustProxy` correctly behind load balancers or rate limits key on the proxy IP.

**Caching**: Redis (`ioredis` or `redis` v5) cache-aside with TTL + jitter and key versioning (`orders:v2:{id}`); `ETag`/`Cache-Control` for public GETs. Protect against stampede with single-flight (in-process promise map) for hot keys.

### 2.4 Data

**Access**: Prisma 7 or Drizzle (SQL-first, lighter) or Kysely; team default **Drizzle or Prisma 7 — one per repo**. Prisma 7 changes: generator `provider = "prisma-client"` with mandatory `output`, driver adapters mandatory (`@prisma/adapter-pg`), `prisma.config.ts`, env vars not auto-loaded, middleware removed (use Client Extensions), `migrate dev` no longer auto-generates/seeds.
```ts
import { PrismaClient } from './generated/prisma/client.js';
import { PrismaPg } from '@prisma/adapter-pg';
export const prisma = new PrismaClient({ adapter: new PrismaPg({ connectionString: config.DATABASE_URL }) });
```
**Migrations**: `prisma migrate deploy` / `drizzle-kit migrate` as a separate release step (job/init container), never on app boot across replicas. Expand/contract as in §1.4; review generated SQL for destructive ops.
**Transactions**: `prisma.$transaction(async (tx) => …)` / `db.transaction(async (tx) => …)`; pass `tx` down; no external HTTP calls inside a transaction. **Optimistic concurrency**: `version` int column; `UPDATE … WHERE id = $1 AND version = $2` → 0 rows = 409 (Prisma: `updateMany` with version in `where`, check `count`). **Idempotency**: `Idempotency-Key` table with unique constraint; insert-first then execute; replay stored response.

### 2.5 Cross-cutting

**Time**: store `timestamptz`; serialize ISO-8601 UTC (`toISOString()`); inject a `clock: () => Date` (or `Temporal` when available) into services; tests use `vi.useFakeTimers({ now: new Date('2026-01-01T00:00:00Z') })`. **Resilience**: `AbortSignal.timeout(ms)` on every `fetch`/undici call; retries with exponential backoff + jitter only for idempotent requests (`p-retry`, or `cockatiel` for retry + circuit breaker + bulkhead). **Background jobs**: BullMQ (Redis) or pg-boss (Postgres) — separate worker process entrypoint; jobs idempotent; `setInterval` in the API process is not a job system. **Perf pitfalls**: N+1 (`findMany` in a loop → `include`/`IN` batch/DataLoader); sync APIs (`fs.readFileSync`, `crypto.pbkdf2Sync`, big `JSON.parse`) on the request path block the event loop; unbounded `Promise.all` over user input (use `p-limit`); floating promises (lint `no-floating-promises`); unhandled rejections (crash + restart is correct — don't swallow `unhandledRejection`); leaking DB pools in tests.

### 2.6 Tooling

Dockerfile:
```dockerfile
FROM node:24-slim AS deps
WORKDIR /app
RUN corepack enable
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN --mount=type=cache,target=/root/.local/share/pnpm/store pnpm install --frozen-lockfile
FROM deps AS build
COPY . .
RUN pnpm build && pnpm prune --prod
FROM node:24-slim
ENV NODE_ENV=production
WORKDIR /app
COPY --from=build --chown=node:node /app/node_modules ./node_modules
COPY --from=build --chown=node:node /app/dist ./dist
USER node
CMD ["node", "--enable-source-maps", "--import", "./dist/instrumentation.js", "dist/server.js"]
```
Run `node` directly as PID 1-aware (use `--init` in compose / `tini`), not `npm start` (npm doesn't forward SIGTERM reliably). docker-compose: `postgres:17`, `redis:7`, `otel/opentelemetry-collector` or Jaeger/Aspire dashboard; `pnpm dev` = `tsx watch --env-file=.env src/server.ts` (Node 24 can also run `.ts` directly via type stripping for scripts, but build with `tsc` for prod).

CI:
```bash
corepack enable && pnpm install --frozen-lockfile
pnpm lint            # eslint .
pnpm format:check    # prettier --check . (or biome ci .)
pnpm typecheck       # tsc --noEmit
pnpm test -- --coverage   # vitest run --coverage
pnpm audit --prod    # + Socket/OSV scanner
```
ESLint 10 flat config (`eslint.config.ts`):
```ts
import js from '@eslint/js'; import tseslint from 'typescript-eslint'; import { defineConfig } from 'eslint/config';
export default defineConfig(
  { ignores: ['dist', 'src/generated'] },
  js.configs.recommended,
  tseslint.configs.strictTypeChecked,
  { languageOptions: { parserOptions: { projectService: true } },
    rules: { '@typescript-eslint/no-floating-promises': 'error', '@typescript-eslint/no-misused-promises': 'error' } },
);
```
ESLint 10: `.eslintrc*`/`.eslintignore` are ignored, `/* eslint-env */` errors, config lookup starts from each file's directory. Prettier for format (or Biome 2 for lint+format in one fast tool; keep typescript-eslint for type-aware rules if Biome's type-aware coverage is insufficient). **Packages**: pnpm 11 — commit `pnpm-lock.yaml`, `--frozen-lockfile` in CI, keep `minimumReleaseAge` (default 1 day; consider 3–7 days), `allowBuilds` allowlist for postinstall scripts (replaces `onlyBuiltDependencies`), exact versions for direct deps via Renovate. npm users: `npm ci`, `ignore-scripts=true`.

### 2.7 Testing conventions (Node)

- **Framework**: Vitest 4 (ESM/TS-native, Jest-compatible API). Jest only for legacy/CJS NestJS projects. `node:test` acceptable for tiny libs.
- **HTTP**: Fastify `app.inject({ method: 'POST', url: '/v1/orders', payload })` (no socket); Express/Nest: `supertest(app)`. Build app via `buildApp(deps)` so tests inject fakes.
- **Integration**: `@testcontainers/postgresql` started in `globalSetup`, migrations applied once, each test in a transaction rolled back or `TRUNCATE … RESTART IDENTITY CASCADE` between files; use unique ids per test for parallel runs (`pool: 'forks'`).
- **Naming**: `describe('createOrder')` → `it('returns 409 when stock is insufficient')`; AAA; `*.test.ts` co-located for unit, `test/integration/*.int.test.ts` for integration (separate Vitest project).
- **Mocks**: prefer fakes passed through factories; `vi.mock` module mocking only at boundaries (email SDK, payment SDK); HTTP outbound mocked with `msw` or `undici` `MockAgent` — never real network. `vi.restoreAllMocks()`/`restoreMocks: true` in config.
- **Determinism**: `vi.useFakeTimers()`/`vi.setSystemTime`; seeded faker (`faker.seed(42)`); no `setTimeout` sleeps — `vi.waitFor`.
- **Coverage**: `vitest run --coverage` (v8 provider), thresholds in config (lines 80, branches 70) on `src/**` excluding generated code.
- **Contract**: snapshot the generated OpenAPI JSON; property tests with `fast-check` for parsers/money math.
- **Flakiness**: no `retry` in config as a fix; `--sequence.shuffle` in nightly to surface order dependence.

### 2.8 DO / DON'T (Node)

1. DO use Node 24 LTS, ESM (`"type": "module"`), TS `strict` + `noUncheckedIndexedAccess`.
2. DO validate env at startup with zod and export a typed config.
3. DO validate every request (body, params, query, headers) and response with schemas.
4. DO return `application/problem+json` from one error handler.
5. DO use `pino` structured logs with redaction and request ids.
6. DO start OTel via `--import` before any other module.
7. DO put `AbortSignal.timeout()` on every outbound call.
8. DO separate `buildApp()` from `listen()`.
9. DO run migrations as a separate deploy step.
10. DO use transactions for multi-write operations; pass `tx` explicitly.
11. DO enforce `no-floating-promises` and `no-misused-promises`.
12. DO commit lockfiles and install with `--frozen-lockfile`/`npm ci`.
13. DO verify JWTs against JWKS with issuer, audience and algorithm allow-list.
14. DO handle SIGTERM with graceful drain.
15. DO use cursor pagination with a max `limit`.
16. DON'T use `any`; use `unknown` + narrowing; no `// @ts-ignore` (use `@ts-expect-error` with reason).
17. DON'T block the event loop with `*Sync` APIs or heavy CPU in handlers (use worker threads/queues).
18. DON'T `new PrismaClient()` per request or forget to close pools in tests.
19. DON'T build SQL with string concatenation; use parameterized queries/tagged templates.
20. DON'T use `express-async-errors` or wrap every handler in try/catch in Express 5.
21. DON'T use `.eslintrc`, `ts-node`, `moduleResolution: node`, or `baseUrl` path hacks in new projects.
22. DON'T `console.log` in app code.
23. DON'T trust `req.body.tenantId`/`userId` — derive from the verified token.
24. DON'T install packages an agent "remembers" without checking npm (existence, weekly downloads, repo, age).
25. DON'T run the container as root or via `npm start`.

### 2.9 LLM mistakes to catch (Node)

- CommonJS `require` / `module.exports` mixed into ESM projects; missing `.js` extensions in relative imports under `nodenext`.
- Express 4 idioms in Express 5: `app.get('*')`, `:param?`, `req.param()`, `res.send(status, body)`, `express-async-errors`, `body-parser` package (use `express.json()`).
- `fastify-type-provider-zod` (old unscoped name) with Zod 3; Zod 3 APIs in Zod 4 code (`z.string().email()` still works but deprecated; `errorMap`, `.strict()` semantics changed; `ZodError.errors` → `.issues`).
- Prisma 6 patterns in Prisma 7: `provider = "prisma-client-js"`, importing from `@prisma/client` without generated output, `$use` middleware, relying on auto `.env` load.
- `jsonwebtoken.decode()` used as verification; HS256 secrets hardcoded.
- `dotenv` added although `--env-file` exists; `ts-node` instead of `tsx`/native type stripping.
- `await` inside `forEach` (doesn't await); `Promise.all` on thousands of items; missing `return await` inside try blocks.
- Jest config generated for a Vitest project (`jest.fn` vs `vi.fn`); `supertest` against `app.listen()` leaving open handles.
- Hallucinated packages (slopsquatting: frontier LLMs invent ~5% of suggested package names; some names are shared across models) — every new dependency must be verified on the registry before `pnpm add`.

---

## 3. Python — generic services / FastAPI / AI services

### 3.1 Layout & scaffolding

`src/` layout, feature packages, framework at the edge:
```
pyproject.toml  uv.lock  .python-version
src/acme/
  main.py            # create_app(): FastAPI; lifespan wires resources
  settings.py        # pydantic-settings
  api/deps.py        # Annotated dependencies (DbSession, CurrentUser)
  core/              # errors.py (problem+json), logging.py, telemetry.py, clock.py
  db/                # engine/session factory, base metadata
  features/
    orders/
      router.py      # APIRouter, thin
      schemas.py     # Pydantic request/response models
      service.py     # business logic, no FastAPI imports
      repository.py  # SQLAlchemy queries
      models.py      # ORM models
migrations/          # alembic
tests/unit  tests/integration  tests/conftest.py
```
Scaffold:
```bash
uv init --package acme --python 3.13 && cd acme
uv add "fastapi[standard]" pydantic-settings "sqlalchemy[asyncio]>=2.0" asyncpg alembic structlog httpx tenacity \
  opentelemetry-distro opentelemetry-exporter-otlp
uv add --dev pytest pytest-asyncio pytest-cov "testcontainers[postgres]" factory-boy hypothesis ruff mypy asgi-lifespan
uv run alembic init -t async migrations
uv run fastapi dev src/acme/main.py
```
AI services add: `openai`/`anthropic` SDK, streaming via SSE (`StreamingResponse`/`EventSourceResponse`), timeouts and token budgets on every model call, prompt/response logging with PII redaction, eval tests separated from unit tests (marker `@pytest.mark.eval`, not in the default CI run).

### 3.2 Platform wiring

**Config**: `pydantic-settings` `BaseSettings` with `SettingsConfigDict(env_file=".env", env_prefix="ACME_", extra="ignore")`; `SecretStr` for secrets; one cached `get_settings()` (`@lru_cache`) injected as a dependency so tests override it.

**DI**: FastAPI `Depends` with `Annotated` aliases:
```python
SessionDep = Annotated[AsyncSession, Depends(get_session)]
CurrentUser = Annotated[User, Depends(get_current_user)]

@router.post("/orders", status_code=201, response_model=OrderOut)
async def create_order(body: OrderIn, db: SessionDep, user: CurrentUser) -> OrderOut: ...
```
Long-lived resources (engine, HTTP client, Redis) created in `lifespan` and stored on `app.state` — not module globals, not `@app.on_event` (deprecated).
```python
@asynccontextmanager
async def lifespan(app: FastAPI):
    app.state.engine = create_async_engine(settings.database_url, pool_pre_ping=True)
    app.state.http = httpx.AsyncClient(timeout=httpx.Timeout(10.0, connect=3.0))
    yield
    await app.state.http.aclose(); await app.state.engine.dispose()
```
**Logging**: `structlog` → JSON in prod, contextvars for `request_id`/`trace_id` (middleware binds them); stdlib logging routed through structlog so uvicorn/SQLAlchemy logs are structured too. Never `print`.
**OTel**: zero-code: `uv run opentelemetry-bootstrap -a requirements` → add listed instrumentations, run `opentelemetry-instrument uvicorn acme.main:app` with `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_PYTHON_LOGGING_AUTO_INSTRUMENTATION_ENABLED=true`. Auto-instrumentation doesn't work with `--reload`; with multiple workers prefer programmatic setup per worker (`FastAPIInstrumentor.instrument_app(app)`, `SQLAlchemyInstrumentor`, `HTTPXClientInstrumentor`).
**Health**: `/health/live`, `/health/ready` (`SELECT 1` with timeout), excluded from auth and OTel noise.

### 3.3 HTTP contract

**Errors**: domain exceptions (`class DomainError(Exception): status=400; type=...`) + `app.add_exception_handler(DomainError, problem_handler)`; override `RequestValidationError` handler to emit `application/problem+json` with `errors=[{loc,msg,type}]`; catch-all handler for `Exception` → 500 generic. Services raise domain errors, never `HTTPException` (keeps services framework-free).
**Validation**: Pydantic v2 models with `model_config = ConfigDict(extra="forbid", frozen=True)` for inputs; `Field(min_length=..., ge=...)`, `Annotated` constraints, `@field_validator`/`@model_validator`; separate `In`/`Out` models; `response_model` filters output. v2 API only: `model_validate`, `model_dump`, `model_config` — not `parse_obj`, `.dict()`, `class Config`, `@validator`.
**Auth**: `OAuth2PasswordBearer`/`HTTPBearer` extracting token; verify with `PyJWT` (`jwt.decode(token, key, algorithms=["RS256"], audience=..., issuer=...)`) and `PyJWKClient` for JWKS; `python-jose` is effectively unmaintained — don't use. Passwords: `argon2-cffi` or `pwdlib` (passlib is unmaintained). Authorization: dependencies like `require_scope("orders:write")` via `Security(...)`, resource checks in service.
**Pagination**: `limit` (le=100) + opaque `cursor`; keyset `WHERE (created_at, id) < (:c_at, :c_id) ORDER BY created_at DESC, id DESC`.
**Versioning/OpenAPI**: `APIRouter(prefix="/v1")`; OpenAPI auto-generated — set `operation_id` via `generate_unique_id_function`, tag routers, export `app.openapi()` to a file in CI and diff it. Disable docs in prod (`docs_url=None`) if public.
**Rate limit/CORS**: rate limiting at the gateway preferred; in-app `slowapi` (Redis storage) if needed. `CORSMiddleware(allow_origins=settings.cors_origins, allow_credentials=True)` — never `["*"]` with credentials. Add `TrustedHostMiddleware` and correct `--forwarded-allow-ips` for uvicorn behind a proxy.
**Caching**: Redis (`redis.asyncio`) cache-aside, TTL + key versioning; `functools.lru_cache` only for pure config.

### 3.4 Data

SQLAlchemy 2.x typed ORM (`DeclarativeBase`, `Mapped[int]`, `mapped_column`), `select()` 2.0 style — never `session.query()`. Async: `create_async_engine` + `async_sessionmaker(engine, expire_on_commit=False)`; one `AsyncSession` per request via dependency (`async with sessionmaker() as s: yield s`); never share a session across tasks/`asyncio.gather`. Relationships: `lazy="raise"` default on models to make N+1 an error; load explicitly with `selectinload()`/`joinedload()`.
**Transactions**: `async with session.begin():` in the service/unit-of-work boundary; commit once per use case.
**Migrations**: Alembic autogenerate (`alembic revision --autogenerate -m "..."`) then **review** the script; `alembic upgrade head` as a release step; naming convention on `MetaData` so constraint names are deterministic; expand/contract; data backfills in separate batched migrations or scripts.
**Concurrency**: `version_id_col` in `__mapper_args__` → `StaleDataError` → 409. Or `SELECT … FOR UPDATE` (`with_for_update()`) for short critical sections.
**Idempotency**: idempotency table with unique `(user_id, key)`, `INSERT … ON CONFLICT DO NOTHING RETURNING` to claim.

### 3.5 Cross-cutting

**Time**: `datetime.now(UTC)` (`from datetime import UTC`), never naive datetimes, never `utcnow()` (deprecated); DB `TIMESTAMP WITH TIME ZONE` (`DateTime(timezone=True)`); inject a `Clock` protocol; tests use `time-machine` (faster/stricter than freezegun). **Resilience**: `httpx.AsyncClient` shared, explicit `Timeout`; `tenacity` retries (`stop_after_attempt(3)`, `wait_exponential_jitter()`, `retry_if_exception_type(httpx.TransportError)`) only for idempotent calls. **Background jobs**: FastAPI `BackgroundTasks` only for fire-and-forget trivial work (lost on crash); durable → Celery, Dramatiq, arq (asyncio/Redis) or Taskiq, run as separate process. **Async pitfalls**: calling blocking libraries (`requests`, sync DB drivers, `time.sleep`, heavy CPU/pandas) inside `async def` blocks the loop — use `def` endpoints (run in threadpool), `anyio.to_thread.run_sync`, or async libs; don't create an `AsyncClient` per request; N+1 via lazy loads (async lazy loads raise `MissingGreenlet`).

### 3.6 Tooling

Dockerfile (uv, multi-stage):
```dockerfile
FROM python:3.13-slim AS builder
COPY --from=ghcr.io/astral-sh/uv:0.8 /uv /uvx /bin/
ENV UV_COMPILE_BYTECODE=1 UV_LINK_MODE=copy UV_PYTHON_DOWNLOADS=0
WORKDIR /app
RUN --mount=type=cache,target=/root/.cache/uv \
    --mount=type=bind,source=uv.lock,target=uv.lock \
    --mount=type=bind,source=pyproject.toml,target=pyproject.toml \
    uv sync --locked --no-install-project --no-dev
COPY . .
RUN --mount=type=cache,target=/root/.cache/uv uv sync --locked --no-dev
FROM python:3.13-slim
RUN useradd -r -u 10001 app
COPY --from=builder --chown=app:app /app /app
ENV PATH="/app/.venv/bin:$PATH"
USER app
CMD ["uvicorn", "acme.main:app", "--host", "0.0.0.0", "--port", "8000", "--proxy-headers"]
```
(Pin the uv image tag; don't use `:latest` in prod.) Processes: `uvicorn --workers N` or `gunicorn -k uvicorn.workers.UvicornWorker`; one worker per container on k8s and scale pods.

CI:
```bash
uv sync --locked
uv run ruff format --check .
uv run ruff check .
uv run mypy src          # or: uv run pyright
uv run pytest -q --cov=acme --cov-report=xml --cov-fail-under=80
uv run pip-audit          # or uvx pip-audit / osv-scanner
```
`pyproject.toml`:
```toml
[tool.ruff]
target-version = "py313"
line-length = 100
[tool.ruff.lint]
select = ["E","F","W","I","B","UP","SIM","ASYNC","S","DTZ","RUF","PT","N","C4","PERF","TRY","FAST"]
[tool.mypy]
strict = true
plugins = ["pydantic.mypy"]
[tool.pytest.ini_options]
addopts = "-ra --strict-markers --strict-config"
asyncio_mode = "auto"
asyncio_default_fixture_loop_scope = "session"
testpaths = ["tests"]
```
(`DTZ` catches naive datetimes; `ASYNC` catches blocking calls in async; `S` = bandit; `FAST` = FastAPI rules.) `ty` (Astral) is beta — pilot it as a non-blocking job alongside mypy/pyright. Packages: `uv.lock` committed, `uv sync --locked` in CI, `uv lock --upgrade-package x` for bumps, Renovate for updates; no `requirements.txt` hand-edits (export with `uv export` if a platform needs it).

### 3.7 Testing conventions (Python generic/FastAPI)

- pytest + pytest-asyncio 1.x (`asyncio_mode=auto`; the `event_loop` fixture was removed in 1.0 — use `loop_scope`) or AnyIO's pytest plugin (`@pytest.mark.anyio`) — pick one.
- HTTP: `httpx.AsyncClient(transport=ASGITransport(app=app), base_url="http://test")`; wrap with `asgi_lifespan.LifespanManager(app)` because AsyncClient doesn't run lifespan. Sync tests: `with TestClient(app) as c:` (context manager triggers lifespan). Override deps: `app.dependency_overrides[get_settings] = lambda: test_settings`; clear in teardown.
- Integration DB: `testcontainers.postgres.PostgresContainer("postgres:17-alpine")` session-scoped fixture, `alembic upgrade head` once, then per-test connection with outer transaction + `join_transaction_mode="create_savepoint"` rollback.
- Data: `factory_boy` factories (`SQLAlchemyModelFactory` with session set by fixture) or plain builder functions; `hypothesis` for validators/parsers/money logic (`@given(st.decimals(...))`).
- Naming: `test_<unit>_<scenario>_<expected>`; files `test_*.py`; AAA; `@pytest.mark.parametrize` with `ids=`.
- Fakes over `unittest.mock`; when mocking use `autospec=True`/`create_autospec`; outbound HTTP via `respx` (httpx) — never real network (`pytest-socket` `--disable-socket` in unit tier).
- Determinism: `time-machine`, seeded `random`/Faker, no sleeps.
- Coverage: `pytest --cov=acme --cov-branch --cov-report=term-missing`; `pytest-xdist -n auto` for speed once tests are isolated; `pytest-randomly` to shuffle.

### 3.8 DO / DON'T (Python generic/FastAPI)

1. DO use uv + committed `uv.lock`; `--locked` in CI and Docker.
2. DO use Python ≥3.13 typing (`list[int]`, `X | None`, `type` aliases) and run mypy/pyright strict.
3. DO use Pydantic v2 APIs only.
4. DO use `Annotated[..., Depends()]` dependency aliases.
5. DO manage resources with `lifespan`.
6. DO keep services free of FastAPI imports.
7. DO return problem+json for all errors.
8. DO use SQLAlchemy 2.0 `select()` style with typed `Mapped[]`.
9. DO set `expire_on_commit=False` for async sessions and `lazy="raise"` on relationships.
10. DO use timezone-aware UTC datetimes.
11. DO set explicit timeouts on every httpx/DB/LLM call.
12. DO run Alembic as a release step; review autogenerated scripts.
13. DO log with structlog + request/trace ids; redact secrets.
14. DO use `SecretStr` for secrets in settings.
15. DO use durable queues for background work that must not be lost.
16. DON'T call blocking I/O (`requests`, `time.sleep`, sync drivers) in `async def`.
17. DON'T use `session.query()`, `declarative_base()` from `sqlalchemy.ext.declarative`, or `engine.execute()` (removed in 2.0).
18. DON'T use `@app.on_event("startup")`, `python-jose`, `passlib` in new code.
19. DON'T use `datetime.utcnow()` or naive datetimes.
20. DON'T share an `AsyncSession` across concurrent tasks.
21. DON'T raise `HTTPException` from the domain/service layer.
22. DON'T use mutable default arguments or module-level DB connections.
23. DON'T `pip install` in Dockerfiles without the lock; don't use `:latest` base images.
24. DON'T `except Exception: pass`; use `logger.exception` and re-raise or map.
25. DON'T use `pickle`/`eval` on untrusted input; don't f-string SQL (use bound params/`text()` with params).

### 3.9 LLM mistakes to catch (Python)

- Pydantic v1 syntax (`class Config`, `.dict()`, `parse_obj`, `@validator`, `orm_mode=True` → now `from_attributes=True`); `from pydantic import BaseSettings` (moved to `pydantic-settings`).
- `from sqlalchemy.ext.declarative import declarative_base`, `session.query(User).get(id)`, `Column(Integer)` without `Mapped`.
- `@pytest.fixture def event_loop()` overrides (removed in pytest-asyncio 1.0); `@pytest.mark.asyncio` missing while `asyncio_mode=strict`.
- `TestClient` without context manager, so lifespan doesn't run; `AsyncClient(app=app)` (the `app=` shortcut was removed from httpx — use `ASGITransport`).
- `requests` inside async endpoints; `asyncio.run()` inside a running loop.
- Hallucinated/misnamed packages (`fastapi-jwt-auth` unmaintained, `flask-restful-swagger-3`, invented `pydantic-sqlalchemy` helpers). Python has the highest hallucination rate in the 2026 Socket study — verify on PyPI before adding.
- `datetime.utcnow()`, `pytz` (use `zoneinfo`), `os.getenv` scattered instead of settings.
- `Optional[str] = None` Pydantic field assumed required/optional wrongly; `Field(regex=)` (v2: `pattern=`).

---

## 4. Python — Flask 3.1

### 4.1 Layout & scaffolding

Application factory + blueprints per feature; extensions initialized in `extensions.py`, bound in `create_app`.
```
src/acme/
  __init__.py        # create_app(config: Settings | None = None) -> Flask
  extensions.py      # db = SQLAlchemy(model_class=Base), migrate = Migrate(), ...
  settings.py        # pydantic-settings → app.config.from_mapping(settings.model_dump())
  errors.py          # problem+json handlers
  features/orders/{routes.py, schemas.py, service.py, repository.py, models.py}
migrations/          # Flask-Migrate (Alembic)
tests/
wsgi.py              # app = create_app()
```
```bash
uv init --package acme --python 3.13
uv add flask flask-sqlalchemy flask-migrate "psycopg[binary,pool]" pydantic-settings apiflask structlog gunicorn \
  opentelemetry-instrumentation-flask
uv add --dev pytest pytest-cov factory-boy "testcontainers[postgres]" ruff mypy time-machine
uv run flask --app acme db init
```
API layer: **APIFlask** (Flask subclass with request/response schemas, OpenAPI, pydantic or marshmallow support) or **flask-smorest** (marshmallow) — team default APIFlask for new APIs; plain Flask + Pydantic for tiny services.

### 4.2 Platform & HTTP

- **Config**: pydantic-settings object → `app.config.from_mapping(...)`; `SECRET_KEY` from env; use `SECRET_KEY_FALLBACKS` (Flask 3.1) for key rotation; `MAX_CONTENT_LENGTH`, `MAX_FORM_MEMORY_SIZE` set; `TRUSTED_HOSTS` (3.1) in prod. Never `app.run(debug=True)` in prod.
- **Request context**: `g` for per-request state (current user); `current_app` instead of importing the app. Flask-SQLAlchemy 3.1 `db.session` is request-scoped and removed at teardown.
- **Logging**: structlog/`dictConfig` JSON before app creation; `before_request` binds request id; gunicorn `--access-logfile -`.
- **OTel**: `opentelemetry-instrument gunicorn wsgi:app` or `FlaskInstrumentor().instrument_app(app)`. With gunicorn preload/fork, init the SDK in `post_fork` hook (BatchSpanProcessor threads don't survive fork).
- **Errors**: `@app.errorhandler(HTTPException)` + `@app.errorhandler(DomainError)` + catch-all `Exception` → problem+json (`Response(json.dumps(p), status, mimetype="application/problem+json")`). APIFlask: set `app.config["VALIDATION_ERROR_STATUS_CODE"]=422/400` and customise `@app.error_processor` to emit RFC 9457.
- **Validation**: APIFlask `@app.input(OrderIn)`/`@app.output(OrderOut, status_code=201)`, or Pydantic `OrderIn.model_validate(request.get_json())` with `ValidationError` → 400.
- **Auth**: JWT via `flask-jwt-extended` or PyJWT in a `before_request`/decorator; APIFlask `HTTPTokenAuth`. Browser sessions: `SESSION_COOKIE_SECURE/HTTPONLY/SAMESITE`, CSRF via Flask-WTF for forms. **Authorization**: decorators `@require_scope("orders:write")` + resource checks in service.
- **Pagination**: `db.paginate(select(...), page=, per_page=, max_per_page=100)` for offset; keyset for large tables.
- **Versioning**: blueprint `url_prefix="/v1"`. **OpenAPI**: APIFlask `/openapi.json` + docs UI; export spec with `flask spec --output openapi.json` in CI.
- **Rate limit**: `Flask-Limiter` with Redis `storage_uri`; **CORS**: `flask-cors` with explicit origins; **Caching**: `Flask-Caching` (Redis) or direct redis-py.
- **Security headers**: `flask-talisman` or reverse proxy; `ProxyFix(app.wsgi_app, x_for=1, x_proto=1)` behind a proxy.

### 4.3 Data & cross-cutting

Flask-SQLAlchemy 3.1 with SQLAlchemy 2.0 style: `class Base(DeclarativeBase)`, `db = SQLAlchemy(model_class=Base)`, `db.session.execute(select(Order).where(...)).scalars()`, `db.get_or_404(Order, id)`. Legacy `Order.query.filter_by` is legacy API — don't use in new code. Migrations: `flask db migrate -m` → review → `flask db upgrade` as release step. Concurrency: `version_id_col`. Transactions: one `db.session.commit()` per request in the service; `db.session.begin_nested()` for savepoints. Flask is sync (WSGI): async views run in a thread per request and give no concurrency benefit — keep views sync, use `httpx.Client` with timeouts. Background jobs: Celery/RQ/Dramatiq with an app-context-aware task base (`with app.app_context():`). Time/resilience rules same as §3.5. Server: `gunicorn -w $((2*CPU+1)) -k gthread --threads 4 wsgi:app`.

### 4.4 Tooling

Dockerfile as §3.6 with `CMD ["gunicorn", "--bind", "0.0.0.0:8000", "--workers", "3", "wsgi:app"]`. CI identical to §3.6 (ruff, mypy, pytest). Compose: postgres + redis + app with `flask --app acme run --debug` for local.

### 4.5 Testing conventions (Flask)

- Fixtures: `app` (created with test settings: `TESTING=True`, testcontainer DB URL), `client = app.test_client()`, `runner = app.test_cli_runner()`.
- DB isolation: session-scoped container + `db.create_all()`/`flask db upgrade` once; per-test outer transaction + savepoint rollback (bind `db.session` to the connection), or truncate.
- Use `with app.app_context():` for service/repository tests; `client.post("/v1/orders", json={...})`; assert `resp.status_code`, `resp.json`, and `resp.mimetype == "application/problem+json"` for errors.
- `factory_boy.alchemy.SQLAlchemyModelFactory` with `sqlalchemy_session = db.session`, `sqlalchemy_session_persistence = "flush"`.
- Everything else (naming, fakes, time-machine, respx/responses for outbound, coverage) as §3.7.

### 4.6 DO / DON'T (Flask)

1. DO use the application factory; no module-level `app` in library code.
2. DO use blueprints per feature with `url_prefix`.
3. DO init extensions unbound (`db = SQLAlchemy()`), bind with `init_app`.
4. DO use SQLAlchemy 2.0 `select()` via `db.session.execute`.
5. DO use `db.get_or_404`/`db.paginate` helpers rather than hand-rolled.
6. DO register JSON problem+json error handlers, including for `404/405` HTML defaults.
7. DO validate every JSON body (APIFlask/Pydantic); reject unknown fields.
8. DO set `MAX_CONTENT_LENGTH`, secure cookie flags, `TRUSTED_HOSTS`.
9. DO rotate `SECRET_KEY` with `SECRET_KEY_FALLBACKS`.
10. DO use `ProxyFix` behind a proxy.
11. DO run under gunicorn; configure workers/threads explicitly.
12. DO init OTel per worker (post_fork) when preloading.
13. DO keep business logic out of view functions.
14. DO use Flask-Migrate/Alembic as a release step.
15. DO use `current_app`/`g`, not global imports of `app`.
16. DON'T use `app.run()` or the dev server in containers/prod.
17. DON'T use `Model.query` legacy API or `flask.ext.*` imports.
18. DON'T use `jsonify` with raw ORM objects; serialize through schemas.
19. DON'T write `async def` views expecting concurrency gains.
20. DON'T store secrets in `config.py` committed to git.
21. DON'T use `before_first_request` (removed in Flask 2.3).
22. DON'T use `flask-restful` / `flask-restplus` (unmaintained) for new APIs.
23. DON'T share `db.session` across threads/Celery tasks without an app context.
24. DON'T disable CSRF for cookie-authenticated form endpoints.

### 4.7 LLM mistakes (Flask)

`@app.before_first_request`; `from flask.ext.sqlalchemy import`; `flask_script`/`Manager` (dead); `app.run(debug=True)` in Dockerfile; `Model.query.get(id)` (legacy, warns); `json.dumps(model.__dict__)`; `flask-restplus`; `Flask-SQLAlchemy` 2.x `SQLALCHEMY_TRACK_MODIFICATIONS` boilerplate everywhere; `request.json` without handling `415`/`None`; `flask_cors.CORS(app)` with `*`; `werkzeug.contrib` imports (removed); `JSONEncoder` subclass (`app.json_encoder` removed in 2.3 — use `app.json` provider).

---

## 5. Python — Django 5.2 LTS / 6.0 + DRF 3.17

### 5.1 Layout & scaffolding

Choose **Django 5.2 LTS** for long-lived enterprise services (supported to April 2028; Python 3.10–3.14). Choose **6.0** for greenfield wanting the built-in tasks framework, CSP, template partials, AsyncPaginator (Python ≥3.12; `DEFAULT_AUTO_FIELD` now defaults to `BigAutoField`). DRF 3.17 is required for Django 6.0 support.

```
config/                  # project package
  settings/{base.py, local.py, test.py, production.py}   # or single settings.py driven by env
  urls.py  asgi.py  wsgi.py
apps/
  orders/
    models.py  admin.py  apps.py
    services.py          # write-side business logic (functions), transactions
    selectors.py         # read-side query functions
    api/{serializers.py, views.py, urls.py, filters.py}
    tests/{test_services.py, test_api.py, factories.py}
    migrations/
manage.py  pyproject.toml  uv.lock
```
"Fat services, thin views/serializers" (HackSoft style): serializers validate/shape, services mutate, selectors read; models hold invariants and constraints.
```bash
uv init acme --python 3.13 && cd acme
uv add "django>=5.2,<5.3" djangorestframework drf-spectacular django-filter django-environ \
  "psycopg[binary,pool]" django-cors-headers structlog django-structlog gunicorn uvicorn
uv add --dev pytest pytest-django pytest-cov factory-boy ruff mypy django-stubs djangorestframework-stubs time-machine
uv run django-admin startproject config .
uv run python manage.py startapp orders apps/orders
```
Define a custom user model (`AUTH_USER_MODEL = "accounts.User"`) **before the first migration**.

### 5.2 Platform

- **Settings**: `django-environ` (`env = environ.Env(); DEBUG = env.bool("DEBUG", False); DATABASES = {"default": env.db()}`); `SECRET_KEY`, `ALLOWED_HOSTS`, `CSRF_TRUSTED_ORIGINS` from env; `manage.py check --deploy` in CI for production settings. Secure cookies/HSTS settings on in prod; Django 6.0 `SECURE_CSP` + `ContentSecurityPolicyMiddleware` for HTML surfaces.
- **DB**: PostgreSQL with psycopg 3; native connection pool (Django ≥5.1): `"OPTIONS": {"pool": {"min_size": 2, "max_size": 10}}` (don't combine with persistent `CONN_MAX_AGE`); `ATOMIC_REQUESTS` off — use explicit `transaction.atomic()` in services.
- **Logging**: `LOGGING` dictConfig with JSON formatter (structlog + `django-structlog` for request ids). **OTel**: `opentelemetry-instrumentation-django` (+ psycopg, redis, celery instrumentations) via `opentelemetry-instrument gunicorn config.wsgi` or programmatic in `wsgi.py/asgi.py`; `DJANGO_SETTINGS_MODULE` must be set before instrumenting.
- **Health**: tiny view `/health/live`, `/health/ready` (DB `connection.ensure_connection()`), or `django-health-check`.
- **DI**: Django has none — pass collaborators as function arguments to services with production defaults (`def place_order(*, user, items, clock: Clock = system_clock, payments: PaymentsClient | None = None)`), keyword-only args.

### 5.3 HTTP contract (DRF)

- **Views**: `APIView`/`GenericAPIView` + mixins or `ViewSet` with routers; views call services/selectors; permissions per view (`permission_classes = [IsAuthenticated, HasScope]`) and object-level `has_object_permission`; `get_queryset()` scoped to the user/tenant (never `Order.objects.all()` for multi-tenant).
- **Serializers**: separate input and output serializers per endpoint; `serializer.is_valid(raise_exception=True)`; don't put side effects in `serializer.save()` for complex flows — call a service with `serializer.validated_data`.
- **Errors**: custom `EXCEPTION_HANDLER` converting DRF `ValidationError`/`APIException`/domain errors into RFC 9457 `application/problem+json` (`drf-standardized-errors` can do this). Map `IntegrityError` for known constraints to 409.
- **Auth**: JWT via `djangorestframework-simplejwt` (or OIDC via `mozilla-django-oidc`/an API gateway); session auth + CSRF for same-site SPA. `DEFAULT_PERMISSION_CLASSES = ["rest_framework.permissions.IsAuthenticated"]` (deny by default).
- **Pagination/filtering**: `DEFAULT_PAGINATION_CLASS` = `CursorPagination` (ordering `-created_at`) for feeds, `PageNumberPagination` with `max_page_size` for admin; `django-filter` `FilterSet` with explicit fields; `OrderingFilter` with `ordering_fields` whitelist.
- **Versioning**: `DEFAULT_VERSIONING_CLASS = "rest_framework.versioning.URLPathVersioning"`, `ALLOWED_VERSIONS = ["v1"]`.
- **OpenAPI**: `drf-spectacular` (`DEFAULT_SCHEMA_CLASS = "drf_spectacular.openapi.AutoSchema"`), `@extend_schema` on non-trivial views; `manage.py spectacular --file openapi.yaml --validate --fail-on-warn` in CI.
- **Rate limit**: DRF throttles (`UserRateThrottle`, `ScopedRateThrottle`) with a shared Redis cache backend (LocMem is per-process — wrong in prod). **CORS**: `django-cors-headers`, `CORS_ALLOWED_ORIGINS` explicit, middleware placed high. **Caching**: `django.core.cache` with `RedisCache` backend (built in since 4.0); `cache_page` only for anonymous endpoints; low-level cache with versioned keys.
- **Alternative**: django-ninja (Pydantic-based, FastAPI-like) is fine for API-only services; don't mix with DRF in one project.

### 5.4 Data

- Models: `constraints = [UniqueConstraint(...), CheckConstraint(...)]` (Django 5.1+: `condition=` kwarg for CheckConstraint), indexes declared in `Meta.indexes`; `TextChoices` for enums; `DecimalField` for money; `db_default` (5.0+) for DB-side defaults.
- Queries: `select_related` (FK/1-1) / `prefetch_related` (M2M/reverse) in selectors; `only()`/`values()` for heavy tables; `exists()` over `count()`; `bulk_create`/`bulk_update` in batches; `iterator(chunk_size=)` for large scans; `F()` expressions for atomic increments (`update(stock=F("stock") - 1)`).
- **Transactions**: `with transaction.atomic():` in services; side effects after commit via `transaction.on_commit(lambda: send_email.enqueue(...))`. `select_for_update()` inside atomic for pessimistic locks.
- **Optimistic concurrency**: `version` IntegerField + `Order.objects.filter(pk=pk, version=v).update(..., version=F("version") + 1)` → 0 rows = 409; ETag/If-Match via DRF conditional headers.
- **Migrations**: `makemigrations` committed and reviewed (`makemigrations --check --dry-run` in CI to catch missing ones); `migrate` as release step; expand/contract; `RunPython` data migrations with `reverse_code` and `apps.get_model` (never import models directly); `AddIndexConcurrently` (postgres, `atomic = False`) for big tables; `SeparateDatabaseAndState` for zero-downtime renames; `django-migration-linter`/`squawk` optional.
- **Idempotency**: idempotency-key model with `UniqueConstraint(fields=["user","key"])`, `get_or_create` inside atomic.

### 5.5 Cross-cutting

- **Time**: `USE_TZ = True` (default), `TIME_ZONE = "UTC"`; `django.utils.timezone.now()`; inject clock into services for tests (or time-machine).
- **Background jobs**: Django 6.0 `django.tasks` (`@task` + `.enqueue()`) is an API; built-in backends are for dev/test (Immediate/Dummy) — production needs a worker backend (e.g. the `django-tasks` package's database backend) — or Celery (+ `django-celery-beat`) as the proven default. Tasks take IDs, not model instances; tasks are idempotent; enqueue with `transaction.on_commit`.
- **Async**: Django supports async views and async ORM (`aget`, `acreate`, `async for`), but ORM calls still run through thread sync adapters; use async views only for I/O fan-out (httpx calls) under ASGI (uvicorn/gunicorn+uvicorn worker). Never call sync ORM from async code without `sync_to_async`.
- **Resilience**: `httpx` with timeouts + `tenacity` (see §3.5). **Perf**: N+1 is the #1 Django issue — use `nplusone`/`django-debug-toolbar` locally and `assertNumQueries`/`django_assert_num_queries` in tests; serializers with nested relations require `prefetch_related` in the queryset.

### 5.6 Tooling

Dockerfile as §3.6; `RUN python manage.py collectstatic --noinput` (with WhiteNoise if serving static); `CMD ["gunicorn", "config.wsgi", "-w", "3", "-b", "0.0.0.0:8000"]` (ASGI: `-k uvicorn_worker.UvicornWorker config.asgi`). Migrations run in a separate job/entrypoint step. CI:
```bash
uv sync --locked
uv run ruff format --check . && uv run ruff check .
uv run mypy .                             # django-stubs + drf-stubs plugin
uv run python manage.py check --deploy --settings=config.settings.production
uv run python manage.py makemigrations --check --dry-run
uv run python manage.py spectacular --file openapi.yaml --validate --fail-on-warn
uv run pytest --cov --cov-report=xml
```
Ruff: add `"DJ"` (flake8-django) to `select`. mypy: `plugins = ["mypy_django_plugin.main", "mypy_drf_plugin.main"]` with `[tool.django-stubs] django_settings_module = "config.settings.test"`.

### 5.7 Testing conventions (Django)

- pytest + **pytest-django** (`DJANGO_SETTINGS_MODULE = "config.settings.test"` in `[tool.pytest.ini_options]`); `@pytest.mark.django_db` (or `db` fixture) — tests run in a rolled-back transaction; `transaction=True` only when testing `on_commit`/`select_for_update` (or use `django_capture_on_commit_callbacks`).
- API: DRF `APIClient` fixture; `client.force_authenticate(user)` for most tests, real token flow in one auth test.
- Data: `factory_boy.django.DjangoModelFactory` per model in `tests/factories.py`; `SubFactory`, `Sequence`, `LazyAttribute`; `build()` for unit tests without DB, `create()` for DB tests.
- DB: test against Postgres (compose service or Testcontainers), never SQLite when production is Postgres (constraints, JSON, locking differ); `--reuse-db` locally, `--create-db` in CI; `--nomigrations` avoided in CI (tests must exercise migrations).
- Query budgets: `django_assert_num_queries(3)` on list endpoints to lock out N+1.
- Settings: `settings` fixture to override; test settings use fast password hasher (`MD5PasswordHasher`), locmem email backend, `ImmediateBackend` tasks/`CELERY_TASK_ALWAYS_EAGER` only in unit tier.
- Naming/AAA/fakes/time/coverage as §3.7; `pytest -n auto` with pytest-xdist (pytest-django creates per-worker DBs).

### 5.8 DO / DON'T (Django)

1. DO create a custom user model before the first migration.
2. DO put business logic in services/selectors, not views, serializers, signals or `save()` overrides.
3. DO use `transaction.atomic()` + `on_commit` for side effects.
4. DO use `select_related`/`prefetch_related` and test query counts.
5. DO scope querysets to the requesting user/tenant in `get_queryset`.
6. DO deny by default (`IsAuthenticated` global) and add object permissions.
7. DO use separate input/output serializers.
8. DO return problem+json via a custom `EXCEPTION_HANDLER`.
9. DO use `CursorPagination` for large lists and cap page sizes.
10. DO generate and validate the OpenAPI schema with drf-spectacular in CI.
11. DO run `check --deploy` and `makemigrations --check` in CI.
12. DO use DB constraints (`UniqueConstraint`, `CheckConstraint`) as the source of truth.
13. DO use `F()` expressions/`update()` for counters and optimistic versioning.
14. DO use Redis cache backend for throttling/caching in multi-process deployments.
15. DO use psycopg 3 and the native pool (or PgBouncer).
16. DON'T use signals for core business flow (hidden coupling); OK for decoupled audit hooks.
17. DON'T use `ModelSerializer` with `fields = "__all__"`.
18. DON'T run with `DEBUG=True` or `ALLOWED_HOSTS=["*"]` in prod.
19. DON'T import models directly inside `RunPython` migrations.
20. DON'T call sync ORM in async views without `sync_to_async`; don't sprinkle async views for no reason.
21. DON'T use SQLite for tests of a Postgres app.
22. DON'T pass model instances to Celery/django.tasks — pass IDs.
23. DON'T use `.extra()` or f-string raw SQL; use `RawSQL`/params.
24. DON'T use `datetime.now()` — use `timezone.now()`.
25. DON'T edit applied migrations; create new ones.

### 5.9 LLM mistakes (Django)

`from django.conf.urls import url` (removed in 4.0; use `path`/`re_path`); `ugettext` (removed); `USE_L10N` setting (removed 5.0); `index_together` (removed 5.1); `CheckConstraint(check=...)` (deprecated in 5.1 → `condition=`); `DEFAULT_AUTO_FIELD` boilerplate misunderstanding on 6.0; `django.utils.timezone.utc` (removed 5.0 → `datetime.UTC`); `JSONField` from `django.contrib.postgres.fields` (use `models.JSONField`); `rest_framework_jwt` (dead — use simplejwt); `drf-yasg` for OpenAPI 3.x (Swagger 2 only — use drf-spectacular); `ATOMIC_REQUESTS=True` as default fix; `get_object_or_404` in services; N+1 from `SerializerMethodField` calling queries per row; `CELERY_ALWAYS_EAGER` in production settings; using `django.tasks` on 5.2 (only in 6.0+) or assuming 6.0's built-in task backends run jobs in production.

---

## 6. Cross-stack conventions (put in every skill)

- **Error contract**: RFC 9457 — `type` (stable URI per error code; `about:blank` when only status matters), `title`, `status`, `detail`, `instance`, plus extensions `traceId`, `errors` (validation list), `code`. Content-Type `application/problem+json`. Never stack traces or internal ids. Return the single most relevant problem.
- **Status codes**: 400 malformed/validation (or 422 — pick one team-wide; default 400 for .NET/Django parity, FastAPI default 422 overridden to 400), 401 unauthenticated, 403 forbidden, 404 (also for resources of other tenants), 409 conflict/concurrency/idempotency mismatch, 412 If-Match failed, 429 with `Retry-After`, 503 dependency down.
- **Pagination**: `limit` ≤100, opaque `cursor`, deterministic order with PK tiebreak.
- **IDs**: UUIDv7 (time-ordered) or bigint + public opaque id; never expose sequential ids for guessable resources.
- **Time**: UTC everywhere, ISO-8601 with `Z` offset on the wire, injected clock.
- **Idempotency-Key** on unsafe POSTs that create money/orders; retries only for idempotent operations.
- **Migrations**: expand → migrate/backfill → contract, in separate deploys; migrations are a release step, not app startup.
- **Observability**: OTel traces + metrics + logs with trace correlation; service name/env/version resource attributes; no PII in spans or logs.
- **Supply chain**: lockfiles committed, frozen installs in CI, dependency age gates (pnpm `minimumReleaseAge`), vulnerability scan, and **verify every agent-proposed package exists and is the canonical one** (slopsquatting).
- **Testing pyramid for agents**: every feature ships with (a) unit tests for domain/service rules, (b) at least one integration test through HTTP against a real DB container covering happy path + one failure (validation or 404/409), (c) an authz test (other user/tenant gets 404/403). Tests deterministic: fake clock, seeded random, no sleeps, no real network.

---

## Sources

- What's new in ASP.NET Core 10 — https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0
- What's new in .NET 10 — https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview
- What's new in EF Core 10 — https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew
- `dotnet new sln` defaults to SLNX — https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default
- dotnet test with MTP (.NET Blog) — https://devblogs.microsoft.com/dotnet/dotnet-test-with-mtp/
- Microsoft.Testing.Platform adoption — https://devblogs.microsoft.com/dotnet/mtp-adoption-frameworks/
- xUnit v3 + MTP — https://xunit.net/docs/getting-started/v3/microsoft-testing-platform
- Benchmarking xUnit v3/NUnit/MSTest/TUnit (Meziantou) — https://www.meziantou.net/benchmarking-dotnet-test-frameworks-xunit-v3-nunit-mstest-and-tunit.htm
- Converting xUnit to TUnit (Andrew Lock) — https://andrewlock.net/converting-an-xunit-project-to-tunit/
- Fluent Assertions v8 license change (InfoQ) — https://www.infoq.com/news/2025/01/fluent-assertions-v8-license/
- AwesomeAssertions fork — https://github.com/AwesomeAssertions/AwesomeAssertions
- AutoMapper & MediatR commercial editions (Jimmy Bogard) — https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/
- MediatR/MassTransit going commercial (Milan Jovanović) — https://milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you
- API versioning + OpenAPI in .NET 10 (.NET Blog) — https://devblogs.microsoft.com/dotnet/api-versioning-in-dotnet-10-applications/
- HTTP resilience (Microsoft Learn) — https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience
- HybridCache (Microsoft Learn) — https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid
- FakeTimeProvider testing — https://learn.microsoft.com/en-us/dotnet/core/extensions/timeprovider-testing
- Testcontainers .NET PostgreSQL module — https://dotnet.testcontainers.org/modules/postgres/
- Scalar for ASP.NET Core — https://scalar.com/docs-for/dotnet
- RFC 9457 Problem Details — https://www.rfc-editor.org/rfc/rfc9457.html
- Announcing TypeScript 6.0 — https://devblogs.microsoft.com/typescript/announcing-typescript-6-0/
- TypeScript 7.0 released (InfoQ) — https://www.infoq.com/news/2026/08/typescript-7-released/
- Node 22 vs 24 after March 2026 schedule change — https://pocketlantern.dev/briefs/node-22-vs-node-24-after-release-schedule-change-2026
- Node.js 24 becomes LTS (NodeSource) — https://nodesource.com/blog/nodejs-24-becomes-lts
- ESLint v10.0.0 released — https://eslint.org/blog/2026/02/eslint-v10.0.0-released/
- ESLint defineConfig/extends — https://eslint.org/blog/2025/03/flat-config-extends-define-config-global-ignores/
- Express 5 migration (LogRocket) — https://blog.logrocket.com/express-js-5-migration-guide/
- NestJS migration guide — https://docs.nestjs.com/migration-guide
- NestJS 12 is now available (Trilon) — https://trilon.io/blog/nestjs-12-is-now-available
- @fastify/type-provider-zod — https://github.com/fastify/fastify-type-provider-zod
- Zod 4 release notes — https://zod.dev/v4
- Prisma 7 upgrade guide — https://www.prisma.io/docs/guides/upgrade-prisma-orm/v7
- pnpm 11.0 release — https://pnpm.io/blog/releases/11.0
- Vitest 4.0 — https://vitest.dev/blog/vitest-4
- OpenTelemetry Node.js ESM auto-instrumentation — https://oneuptime.com/blog/post/2026-02-06-fix-otel-auto-instrumentation-nodejs-esm/view
- OpenTelemetry JS getting started — https://opentelemetry.io/docs/languages/js/getting-started/nodejs/
- Biome vs ESLint/Oxlint 2026 — https://www.pkgpulse.com/guides/biome-vs-eslint-vs-oxlint-2026
- Slopsquatting study (Socket) — https://socket.dev/blog/slopsquatting-targets-across-frontier-llms
- Django 6.0 release notes — https://docs.djangoproject.com/en/6.0/releases/6.0/
- DRF 3.17 released — https://forum.djangoproject.com/t/django-rest-framework-3-17-released/44581
- drf-spectacular — https://github.com/tfranzel/drf-spectacular
- Django native Postgres pool ticket — https://code.djangoproject.com/ticket/35685
- FastAPI release notes — https://fastapi.tiangolo.com/release-notes/
- FastAPI async tests — https://fastapi.tiangolo.com/advanced/async-tests/
- Flask changes (3.1.x) — https://flask.palletsprojects.com/en/stable/changes/
- APIFlask comparison — https://apiflask.com/comparison/
- flask-smorest — https://flask-smorest.readthedocs.io/
- SQLAlchemy 2.1 asyncio — https://docs.sqlalchemy.org/en/21/orm/extensions/asyncio.html
- pytest-asyncio changelog — https://pytest-asyncio.readthedocs.io/en/stable/reference/changelog.html
- uv in Docker (Astral) — https://docs.astral.sh/uv/guides/integration/docker/
- Production-ready Python Docker with uv (Hynek) — https://hynek.me/articles/docker-uv/
- ty type checker (pydevtools) — https://pydevtools.com/handbook/topics/ty/
- What's new in Python 3.14 — https://docs.python.org/3/whatsnew/3.14.html
- OpenTelemetry Python zero-code — https://opentelemetry.io/docs/zero-code/python/
