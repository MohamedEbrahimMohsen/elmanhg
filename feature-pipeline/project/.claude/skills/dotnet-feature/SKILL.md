---
name: dotnet-feature
description: Style guide for every backend change in this repo's .NET solution. The planner, style-checker, reviewer and OpenCode judge api/ code against this file and nothing else. Team default; tune the sections, keep the numbering.
---

# .NET feature handbook

Stack: **.NET 10 · ASP.NET Core Web API · MediatR (CQRS) · FluentValidation · EF Core (SQL Server / Azure SQL) · xUnit + NSubstitute + FluentAssertions**
  → 2026-09 update: versions = .NET 10 LTS (C# 14, EF Core 10, supported to Nov 2028) · FluentValidation 12 · xUnit v3 on Microsoft.Testing.Platform. FluentAssertions ≥8 is commercial: use AwesomeAssertions (Apache-2) or keep FluentAssertions pinned `[7.0.0,8.0.0)`. MediatR ≥13 needs a licence key: keep the repo's existing licensed setup or pinned 12.x; never cross 12→13 without an ADR (§12).
One vertical slice per feature. The repo is the style: before writing a file, open its sibling and match it.
Shared contract rules (errors, pagination, idempotency, versioning): global skill `momenta-api-contract`. New packages: global skill `momenta-dependency-policy`.

## 1. Layout

- Clean layers, one project each: `Domain/`, `Application/`, `Infrastructure/`, `Api/`, `Tests/`.
- Vertical slice per use case in `Application/Features/<Feature>/<UseCase>/`: `XCommand.cs` (or `XQuery.cs`), `XCommandHandler.cs`, `XCommandValidator.cs`, `XResult.cs`. Four files, same folder. Nothing else in it.
- Entities in `Domain/<Aggregate>/`. Repository interfaces in `Domain/`, implementations in `Infrastructure/Persistence/`.
- One controller per resource in `Api/Controllers/`. Controllers stay thin: bind, send, map status. No logic.
- No file over 150 lines. A handler over 60 lines gets a domain method, not a helper class.
- Dependency direction: `Api → Application → Domain`; `Infrastructure → Application, Domain`. `Domain` references no EF Core, ASP.NET Core or MediatR package.
- Solution-wide settings live in `Directory.Build.props` (TFM, `Nullable`, `TreatWarningsAsErrors`, analyzers), never repeated in a `.csproj`.
- Package versions live only in `Directory.Packages.props` (Central Package Management). A `.csproj` `PackageReference` has no `Version` attribute.
- SDK pinned in `global.json`. Solution file is `.slnx` (default from `dotnet new sln` on the .NET 10 SDK).
- DI registration per layer in one extension method each: `AddApplication()`, `AddInfrastructure(configuration)`. `Program.cs` calls them; it registers nothing feature-specific itself.
- `Api/Program.cs` ends with `public partial class Program;` so `WebApplicationFactory<Program>` works.

## 2. Naming

- Commands are verbs: `CreateOrderCommand`, `SuspendTenantCommand`. Queries: `GetOrderByIdQuery`, `ListOrdersQuery`.
- Handlers `<Command>Handler`. Validators `<Command>Validator`. Results `<UseCase>Result`. Never `Dto`, `Response`, `Model`, `ViewModel`.
- Error codes: constants in `Domain/Errors/ErrorCodes.cs`, `SCREAMING_SNAKE`, grouped by aggregate, value = the constant name. Every code has an entry in `Messages.ar.resx` and `Messages.en.resx`.
- Domain methods are business verbs (`order.Cancel(reason)`), never setters exposed for the handler's convenience.
- Async methods end in `Async`. Test methods `Method_Scenario_Expected`.
- EF configuration classes `<Entity>Configuration : IEntityTypeConfiguration<Entity>`. Migrations named `<Verb><What>` in PascalCase (`AddOrderRowVersion`, `CreateInvoicesTable`).
- Options classes `<Area>Options` with `public const string Section = "<Area>";`.
- Authorization policy names in `Api/Policies.cs` as constants; policy value `resource:action` (`orders:write`).
- Route segments plural kebab-case (`/api/v1/purchase-orders/{id}`); JSON properties camelCase (System.Text.Json default).

## 3. Forbidden

- `DateTime`. Use `DateTimeOffset`, and only through the injected clock.
  → 2026-09 update: the clock is `TimeProvider` (`_clock.GetUtcNow()`), registered as `TimeProvider.System`. A repo-local `IClock` wrapper stays allowed only if it already exists and delegates to `TimeProvider`. `DateOnly` is allowed for calendar dates.
- `try`/`catch` in handlers. Throw the typed exception; middleware maps it.
- `SaveChangesAsync` anywhere but once, in the handler, after all mutations. Never in a repository.
- Public setters on entities. State changes go through domain methods.
- Business logic in controllers, validators, or EF configurations.
- `Include(...)` chains in handlers for read models. Reads use projections (`Select`) to the result type.
- Raw SQL strings outside `Infrastructure/`. `dynamic`. `object` parameters.
- Comments in production code, except one line for a non-obvious invariant. `// TODO`. Commented-out code.
- Static service locators, `IServiceProvider` injection, `HttpContext` below the Api layer.
- Swallowing exceptions. `async void`. `.Result` / `.Wait()`.
- `GetAwaiter().GetResult()`, `Task.Run` wrapping async I/O, `Thread.Sleep`.
- `new HttpClient()`. Outbound HTTP goes through `IHttpClientFactory` typed clients.
- `FromSqlRaw`/`ExecuteSqlRaw` with string interpolation or concatenation. Use `FromSql($"...")` / `ExecuteSql($"...")` (parameterised).
- `Database.Migrate()` or `EnsureCreated()` at application startup outside local development.
- Newtonsoft.Json in new code (System.Text.Json only). Swashbuckle / `AddSwaggerGen()` (templates dropped it; use `Microsoft.AspNetCore.OpenApi`).
- `Microsoft.EntityFrameworkCore.InMemory` in any test project.
- Lazy-loading proxies (`UseLazyLoadingProxies`).
- String-interpolated log messages (`LogInformation($"...")`, analyzer CA2254). Logging tokens, passwords, full request bodies, or PII.
- `[AllowAnonymous]` without a one-line reason comment and a mention in the plan.
- Unnamed `IgnoreQueryFilters()`; it also drops tenant filters. Name the filter: `IgnoreQueryFilters(["SoftDelete"])`.

## 4. Required

- File-scoped namespaces. `sealed record` for commands/queries/results. `sealed class` for handlers and validators.
- `.ConfigureAwait(false)` on every `await` outside test bodies and controllers.
- Every command has a validator in its own file; every validator rule maps to one error code.
- Every mutation goes through a domain method that guards invariants with `BusinessRuleException(ErrorCodes.X)` and sets `UpdatedAt` from the clock.
- `ICurrentUser.Id` null-checked → `UnauthorizedException`. Missing aggregate → `NotFoundException(ErrorCodes.X_NOT_FOUND)`.
- Result types: client-facing results expose localised text; admin results expose both languages explicitly.
- `[Authorize(Policy = Policies.X)]` on every action. Route attributes on the controller, verbs on actions, `ProducesResponseType` for every status the action can return.
- New entity → EF configuration class in `Infrastructure/Persistence/Configurations/` + migration in the same change.
- Every new endpoint → Postman request(s) in the same change (see the pipeline's Postman step).
- Collection initialisers `[]`. Switch expressions over switch statements. Pattern matching over type checks.
- `CancellationToken` accepted by every controller action and passed to every async call down to EF/HTTP.
- `AsNoTracking()` on every read query that does not mutate.
- Options bound with `AddOptions<T>().BindConfiguration(T.Section).ValidateDataAnnotations().ValidateOnStart()`.
- Every non-2xx response is RFC 9457 `application/problem+json` produced by the global `IExceptionHandler` (§8).
- A fallback authorization policy requiring an authenticated user is set in `Program.cs`.
- Aggregates edited concurrently by humans carry a concurrency token (§9).

## 5. Tests

- xUnit, NSubstitute, FluentAssertions. One test class per handler and per validator, in `Tests/Application/Features/<Feature>/`.
  → 2026-09 update: xUnit v3 (`xunit.v3`) on Microsoft.Testing.Platform; assertions AwesomeAssertions or FluentAssertions pinned `[7.0.0,8.0.0)`; full rules in `conventions/dotnet-testing.md`.
- Handler tests: one per branch. Success asserts the domain method was called and `SaveChangesAsync` `Received(1)`. Every throwing path asserts the exception type and error code and `SaveChangesAsync` `DidNotReceive()`.
- Validator tests: one passing case, one failing case per rule, asserting the error code.
- Domain tests for every invariant on the entity, no mocks.
- No test asserts a mock returned what it was told to return. That is not a test.
- Every new endpoint: at least one integration test through `WebApplicationFactory<Program>` against a Testcontainers SQL Server: happy path + one failure (validation 400 or 404/409) + one authz case (other user/tenant → 403/404).
- Tests never read the real clock: `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`.
- Never edit, skip (`[Fact(Skip = ...)]`), or delete an existing test to make the build green. If a test is wrong, stop and write `BLOCKED: <test> — <why>` in the report.

## 6. DO / DON'T catalog (every DON'T is a blocking review finding)

| # | DON'T | DO |
|---|-------|----|
| 6.1 | `DateTime.UtcNow` | `_clock.UtcNow` (`DateTimeOffset`) |
| 6.2 | `catch (Exception)` in a handler | throw typed exception, let middleware map it |
| 6.3 | `order.Status = OrderStatus.Cancelled` | `order.Cancel(reason, _clock)` |
| 6.4 | `SaveChangesAsync` inside `Repository.AddAsync` | once, at the end of the handler |
| 6.5 | Return the entity from a query | project to `XResult` with `Select` |
| 6.6 | Error string literal `"Order not found"` | `ErrorCodes.ORDER_NOT_FOUND` + both `.resx` entries |
| 6.7 | Validation inside the handler | `XCommandValidator` in its own file |
| 6.8 | Logic in the controller | `await _mediator.Send(command)` and return |
| 6.9 | New endpoint, no Postman request | request(s) added in the same change |
| 6.10 | Test that only checks `Substitute.Received()` on a repository read | assert the outcome: result fields, exception, `SaveChangesAsync` count |
| 6.11 | One "helper" service for one handler | domain method on the aggregate |
| 6.12 | `List<T>` returned from the API unpaged | paged result with `Page`, `Size`, `Total` |
| 6.13 | `_http = new HttpClient()` | typed client via `AddHttpClient<TClient>()` + `AddStandardResilienceHandler()` |
| 6.14 | `db.Orders.FromSqlRaw($"... {id}")` | `db.Orders.FromSql($"... {id}")` |
| 6.15 | `await db.Orders.ToListAsync()` then `.Where(...)` | `.Where(...)` before materialising |
| 6.16 | `if (await q.CountAsync() > 0)` | `await q.AnyAsync(ct)` |
| 6.17 | `logger.LogInformation($"Order {id}")` | `logger.LogInformation("Order {OrderId}", id)` |
| 6.18 | Scoped `DbContext` captured in a singleton / `BackgroundService` | `using var scope = _scopeFactory.CreateScope();` per iteration |
| 6.19 | `[Authorize(Roles = "Admin")]` | `[Authorize(Policy = Policies.OrdersAdmin)]` |
| 6.20 | Request binds an EF entity | request binds the `sealed record` command |

  → 2026-09 update (6.1): `_clock.GetUtcNow()` on an injected `TimeProvider`.
  → 2026-09 update (6.12): large or append-only tables use cursor pagination `{ items, nextCursor }`; offset `{ items, page, pageSize, totalCount }` only for admin grids, `pageSize` ≤ 100 (see `momenta-api-contract`).

## 7. New project from scratch

Use only when `01-context.md` says the repo has no .NET solution yet. Walking-skeleton rules: global skill `momenta-greenfield-bootstrap`.

```bash
dotnet new globaljson --sdk-version 10.0.100 --roll-forward latestFeature
dotnet new sln -n Acme                                   # creates Acme.slnx
dotnet new classlib -n Acme.Domain -o src/Acme.Domain
dotnet new classlib -n Acme.Application -o src/Acme.Application
dotnet new classlib -n Acme.Infrastructure -o src/Acme.Infrastructure
dotnet new webapi --use-controllers -n Acme.Api -o src/Acme.Api
dotnet new install xunit.v3.templates
dotnet new xunit3 -n Acme.Tests -o tests/Acme.Tests
dotnet sln add src/*/*.csproj tests/*/*.csproj
dotnet new editorconfig
dotnet new gitignore
```
- Verify template flags before use: `dotnet new webapi --help`, `dotnet new xunit3 --help`.
- `global.json` also selects the test runner:
```json
{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" },
  "test": { "runner": "Microsoft.Testing.Platform" } }
```
- `Directory.Build.props` contains: `<TargetFramework>net10.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<AnalysisLevel>latest-recommended</AnalysisLevel>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`, `<GenerateDocumentationFile>true</GenerateDocumentationFile>` + `<NoWarn>$(NoWarn);CS1591</NoWarn>`.
- `Directory.Packages.props`: `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`, `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`. Commit every `packages.lock.json`.
- `nuget.config` with `<clear />`, nuget.org (plus the org feed), and `packageSourceMapping` when a private feed exists.
- Health: `AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: ["ready"])`; map `/health/live` (`Predicate = _ => false`) and `/health/ready` (tag `ready`); anonymous, excluded from rate limiting and OpenAPI.
- Dockerfile (chiseled, non-root):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.*.props nuget.config *.slnx ./
COPY src/ src/
RUN dotnet restore src/Acme.Api/Acme.Api.csproj --locked-mode
RUN dotnet publish src/Acme.Api -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Acme.Api.dll"]
```
- `docker-compose.yml`: `mcr.microsoft.com/mssql/server:2022-latest` (or `postgres:17`), `redis:7` if caching, `mcr.microsoft.com/dotnet/aspire-dashboard` for OTLP (4317) + UI (18888). Every service has a `healthcheck`; the API uses `depends_on: { db: { condition: service_healthy } }`. Backing services get no fixed host port (`ports: ["1433"]`).
- `.env.example` lists every setting as `Section__Key=` with a dummy value; `.env` is gitignored. Local secrets: `dotnet user-secrets`. Production: Key Vault / environment variables.
- Migrations run from a bundle in the pipeline (`dotnet ef migrations bundle --self-contained -o efbundle`), never from `Program.cs`.

## 8. HTTP contract

Full shapes in `momenta-api-contract`. .NET wiring:
```csharp
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<AppExceptionHandler>();   // NotFound/BusinessRule/Unauthorized/Validation → status + code
app.UseExceptionHandler();
app.UseStatusCodePages();
```
- `AppExceptionHandler : IExceptionHandler` sets `status`, `title`, `detail` (localised from `.resx` by `Accept-Language`), `type`, and extension `code` = the `ErrorCodes` constant. Unknown exceptions → 500, generic `title`, no `detail`, logged with stack.
- Validation failures → 400 `ValidationProblemDetails` with `errors: { field: [code] }` + `code: "VALIDATION_FAILED"`.
- `DbUpdateConcurrencyException` → 409 `CONCURRENCY_CONFLICT`. `If-Match` mismatch → 412.
- `UseDeveloperExceptionPage` only inside `if (app.Environment.IsDevelopment())`.
- OpenAPI: `builder.Services.AddOpenApi()` + `app.MapOpenApi()` (OpenAPI 3.1, `Microsoft.OpenApi` 2.x). UI via `Scalar.AspNetCore` in Development only. Spec emitted at build with `Microsoft.Extensions.ApiDescription.Server`, committed as `openapi/v1.json`, diffed in CI (`oasdiff breaking`).
- Controllers declare `[ProducesResponseType<XResult>(200)]` and `[ProducesResponseType<ProblemDetails>(4xx)]` per status so the spec is accurate.
- Versioning: URL segment `/api/v{version:apiVersion}/...` with `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer` (check the current major with `dotnet package search Asp.Versioning.Mvc --exact-match`).
- Pagination: cursor = opaque base64url of the last row's sort key + id; `ORDER BY <key>, Id`; `limit` default 20, max 100.
- Idempotency: `Idempotency-Key` header required on POSTs that create orders/payments; stored in an `IdempotencyRecords` table with a unique index on `(UserId, Key)`; replay the stored response.
- Rate limiting: `AddRateLimiter` with a partitioned limiter by user id (fallback IP), `RejectionStatusCode = 429`, `Retry-After` set in `OnRejected`; `[EnableRateLimiting("per-user")]` on controllers.
- CORS: one named policy, origins from `Cors:AllowedOrigins`. Never `AllowAnyOrigin()`, never `SetIsOriginAllowed(_ => true)`.
- Middleware order: `UseForwardedHeaders` (with `KnownProxies`/`KnownNetworks`) → `UseExceptionHandler` → `UseHttpsRedirection` → `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseRateLimiter` → `MapControllers`.

## 9. Data

- Contexts registered with `AddDbContextPool<AppDbContext>` unless the context holds scoped state; `ApplyConfigurationsFromAssembly` in `OnModelCreating`.
- Reads: `AsNoTracking()` + `Select` projection into the result record. Multiple collection `Include` on a write path → `AsSplitQuery()`.
- Bulk changes: `ExecuteUpdateAsync` / `ExecuteDeleteAsync`; no load-modify-save loop over many rows.
- Migrations: `dotnet ef migrations add <Name> -p src/Acme.Infrastructure -s src/Acme.Api`. Review the generated file: no `DropColumn`/`DropTable`/`RenameColumn` unless the plan names it as a contract step.
- Expand/contract across separate releases: (1) add nullable column/new table and write both; (2) backfill in batches; (3) switch reads; (4) drop the old column in a later release. Never rename a column in one step. Never add a NOT NULL column without a default to a populated table.
- EF Core 10 does not wrap all migrations in one transaction; review with `dotnet ef migrations script --idempotent`.
- Transactions: one `SaveChangesAsync` per command is atomic. Explicit `BeginTransactionAsync` only across multiple saves/raw SQL, wrapped in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` when retries are on.
- Side effects that must follow a commit (emails, messages, webhooks) → outbox table written in the same `SaveChangesAsync`, dispatched by a background worker.
- Optimistic concurrency: SQL Server `[Timestamp] public byte[] RowVersion { get; private set; }` (PostgreSQL: `uint Version` + `.IsRowVersion()` → `xmin`). Expose as `ETag`; require `If-Match` on PUT/PATCH.
- N+1: no query inside a loop over query results; lazy loading off; every `foreach` that awaits a repository call is a review finding.
- Money: `decimal` with explicit `HasPrecision(18, 2)` (or the currency's scale). Never `double`/`float`.
- Time columns: `datetimeoffset` (SQL Server) / `timestamptz` (PostgreSQL).
- Multi-tenant data: named global query filter `HasQueryFilter("Tenant", e => e.TenantId == _tenant.Id)`.

## 10. Cross-cutting

- AuthN: `AddAuthentication().AddJwtBearer(o => { o.Authority = ...; o.Audience = ...; o.MapInboundClaims = false; })`; `TokenValidationParameters` keeps `ValidateIssuer/Audience/Lifetime/IssuerSigningKey = true`, sets `ValidAlgorithms`, `ClockSkew` ≤ 1 min; `RequireHttpsMetadata = true` outside Development.
- AuthZ: policies via `AddAuthorizationBuilder().AddPolicy(...)`; `SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`; resource checks via `IAuthorizationService.AuthorizeAsync(user, resource, Policies.SameTenant)`.
- Tenant/user ids come from claims (`ICurrentUser`), never from the request body.
- Logging: `ILogger<T>` with message templates; `[LoggerMessage]` source-generated methods on hot paths; scopes for `OrderId`/`TenantId`.
- OpenTelemetry:
```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("acme-api"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation().AddOtlpExporter());
builder.Logging.AddOpenTelemetry(o => { o.IncludeScopes = true; o.IncludeFormattedMessage = true; });
```
  Endpoint from `OTEL_EXPORTER_OTLP_ENDPOINT`. Azure: `Azure.Monitor.OpenTelemetry.AspNetCore` + `UseAzureMonitor()` instead of OTLP exporters. The EF Core instrumentation package is prerelease: add only if the plan names its version.
- Config/secrets: no secret in any `appsettings*.json`; `IOptions<T>` in singletons, `IOptionsSnapshot<T>` in scoped services.
- Resilience: `AddHttpClient<TClient>(...).AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods())` (`Microsoft.Extensions.Http.Resilience`). One resilience handler per client. POST retries only with an idempotency key.
- Caching: `HybridCache` (`Microsoft.Extensions.Caching.Hybrid`) with tags; key includes tenant/user for per-user data; invalidate with `RemoveByTagAsync`. Output caching only for anonymous GETs.
- Background jobs: `BackgroundService` for in-process loops (scope per iteration, honour `stoppingToken`, catch-and-log per iteration). Durable/scheduled jobs: Hangfire/Quartz/Azure Functions; the plan names which.
- Tokens/codes: `RandomNumberGenerator.GetInt32` / `GetBytes`, never `Random`. Secret comparison: `CryptographicOperations.FixedTimeEquals`.
- `Regex` on user input: `RegexOptions.NonBacktracking` or an explicit `matchTimeout`.

## 11. Tooling & CI commands

Run in this order; each must exit 0. Paste commands + output tails + exit codes in the implementation report.
```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes
dotnet build -c Release --no-restore                 # TreatWarningsAsErrors
dotnet test -c Release --no-build                    # MTP runner from global.json
dotnet test -c Release --no-build -- --coverage --coverage-output-format cobertura   # needs Microsoft.Testing.Extensions.CodeCoverage
dotnet list package --vulnerable --include-transitive
dotnet list package --deprecated
```
- Changed a package version → `dotnet restore --force-evaluate` and commit the updated `packages.lock.json`.
- Analyzers: built-in (`AnalysisLevel latest-recommended`). `Meziantou.Analyzer` / `Roslynator.Analyzers` only if already in `Directory.Packages.props`.
- Style severity lives in `.editorconfig`; `dotnet format` is the only formatter.

## 12. Dependencies & licences

Policy: `momenta-dependency-policy`. A package is added only if the plan names it with an exact version.

| Package | Status (2026-09) | Rule |
|---|---|---|
| FluentAssertions ≥ 8 | Commercial (Xceed) | Do not add. `AwesomeAssertions` / `Shouldly`, or FluentAssertions pinned `[7.0.0,8.0.0)` |
| MediatR ≥ 13 | Dual RPL-1.5 / commercial, licence key required | Keep the repo's current major; bump only with ADR + key in config (`cfg.LicenseKey`) |
| AutoMapper ≥ 15 | Dual RPL-1.5 / commercial | Do not add. Hand-written `ToResult()` or Mapperly |
| MassTransit ≥ 9 | Commercial | Do not add. Azure Service Bus SDK / Wolverine with ADR |
| Moq | Avoid (SponsorLink 2023) | NSubstitute |
| Swashbuckle.AspNetCore | Not in .NET 10 templates | `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` |
| Newtonsoft.Json | Legacy | System.Text.Json |
| Microsoft.EntityFrameworkCore.InMemory | Wrong relational behaviour | Testcontainers |

- Verify before adding: `dotnet package search <Id> --exact-match` (lists versions); licence + publish date on `https://www.nuget.org/packages/<Id>/<version>`.
- Add with `dotnet add <project> package <Id> --version <x.y.z>`; with CPM the version lands in `Directory.Packages.props`.

## 13. Security gotchas (.NET)

- SQL: `FromSqlRaw`/`ExecuteSqlRaw` + interpolation = SQL injection (blocking).
- Missing `[Authorize]` / stray `[AllowAnonymous]`; object lookups not filtered by owner/tenant (BOLA). Other tenants' ids return 404.
- Overposting: commands never contain `TenantId`, `Role`, `IsAdmin`, `Status`, `Price` unless the use case sets them by design and authorises it.
- JWT: `ValidAlgorithms` set; never `ValidateIssuerSigningKey = false`; never `RequireSignedTokens = false`.
- Deserialisation: never `BinaryFormatter`, `SoapFormatter`, `NetDataContractSerializer`, `LosFormatter`, `ObjectStateFormatter`; Json.NET `TypeNameHandling` = `None`; `XmlReader` with `DtdProcessing.Prohibit`.
- CORS `AllowAnyOrigin()` with credentials, or reflected origins.
- Developer exception page or exception messages in production responses.
- Files: user-supplied path → `Path.GetFullPath` + `StartsWith(root + Path.DirectorySeparatorChar)`; uploads size-limited, extension allow-listed, stored outside `wwwroot` with random names.
- SSRF: any URL from user input → scheme+host allow-list, block private/link-local/metadata IPs, no auto-redirect.
- DataProtection keys persisted and protected in multi-instance deployments.
- Antiforgery on cookie-authenticated form endpoints.
- Secrets in `appsettings*.json` or real-looking keys in fixtures → gitleaks blocking + rotate.

## 14. LLM mistakes to avoid (.NET)

- `Startup.cs`, `WebHost.CreateDefaultBuilder`, `IWebHostBuilder`-style hosting in a .NET 10 app.
- `services.AddSwaggerGen()` / `app.UseSwagger()`; `Microsoft.OpenApi` v1 types (`OpenApiString`) in transformers; v2 uses `JsonNode`.
- `app.UseProblemDetails()` (Hellang package, not built-in); `services.AddFluentValidation()` (deprecated auto-validation package). Register validators with `AddValidatorsFromAssemblyContaining<T>()`.
- `services.AddMediatR(typeof(Program))` (v11 signature) → `AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<T>())`.
- `DateTime.Now` / `DateTime.UtcNow` in handlers or domain.
- `new PostgreSqlBuilder().WithImage(...)` / `new MsSqlBuilder().WithImage(...)`: Testcontainers 4 takes the image in the constructor.
- xUnit v2 patterns in v3: `Task InitializeAsync` (v3: `ValueTask`), `using Xunit.Abstractions;` (v3: `Xunit`).
- `ToListAsync()` then filter in memory; `Include` chains for read models.
- `catch (Exception) { }` or `catch { return null; }`.
- Invented package ids or APIs. Every new package → §12 verification before `dotnet add`.
- `Task.WhenAll` over calls on the same `DbContext` (not thread-safe).
- Sync-over-async: `.Result`, `.Wait()`, `GetAwaiter().GetResult()` in constructors, filters or DI factories.
- Editing or skipping a failing test instead of fixing the code.

## 15. Definition of done

- [ ] `dotnet format --verify-no-changes`, `dotnet build -c Release`, `dotnet test` all exit 0; commands + output tails + exit codes pasted in `03-implementation-dotnet.md`.
- [ ] Every new command/query has handler + validator + result + tests per §5.
- [ ] Every new endpoint: policy, `ProducesResponseType` per status, problem+json errors, Postman request, integration test (happy + failure + authz).
- [ ] Every new error code in `ErrorCodes.cs` + `Messages.ar.resx` + `Messages.en.resx`.
- [ ] Entity change → EF configuration + migration; migration reviewed for destructive ops.
- [ ] No new package unless named in the plan; `packages.lock.json` updated; nothing from the §12 "Do not add" rows.
- [ ] Generated OpenAPI committed; no breaking diff unless the plan declares a new version.
- [ ] No existing test modified/deleted/skipped unless the plan's `### Tests` table lists it.
- [ ] `git diff origin/main -U0 -- '*.cs' | grep -E '^\+.*(DateTime\.(Now|UtcNow)|\.Result\b|\.Wait\(\)|new HttpClient\(|FromSqlRaw)'` prints nothing.
- [ ] Anything not done → report ends with `BLOCKED: <reason>`.
