# Constitution

The rules for ALL implementation work in this repo. The stack: **.NET 10 · ASP.NET Core API (controllers) · MediatR 14 + FluentValidation 12 via the vendored `api/core-libraries/` (`Core.*`, copied from Morabh) (`Core.DDD` (incl. `Core.DDD.Time` `TruncateToMicroseconds`, `TimeZoneInfo.LocalDate`/`StartOfDay`, and the `ICurrentUser` actor port), `Core.CQRS`, `Core.EntityFrameworkCore` (including the opt-in `ConflictMap` that turns concurrency and unique-violation failures into `409` codes through an app-supplied unique-violation detector, and the `IVersioned` + `ApplyRowVersionConvention()` `xmin` row-version convention), `Core.Errors`, `Core.Storage` (SDK-free: `IFileStorage`, local provider, media serving), `Core.Storage.S3` (S3/R2 provider, `AddCoreS3FileStorage`), `Core.Validation` (incl. `ValidatePaging`, `ValidateDateRange`, `ValidateDistinct`, `ValidateFileSignature` with built-in signatures), `Core.Utilities` (`IGenerator`, `AddValidatedOptions`), …; Elmanhg-promoted, app-agnostic: `Core.Hosting` (forwarded headers, Kestrel limits, migrate-and-exit, placeholder-secret guard, rate-limit partitions), `Core.Observability` (OpenTelemetry + request metrics), `Core.Cache` (`ICacheableQuery` caching behaviour; lifetime from the query, a named cache profile, or the default), `Core.Spreadsheets` (ClosedXML reader/writer), `Core.Settings` (runtime settings and feature flags: typed keys, definitions, a startup-validated registry with an app-supplied group order, cross-setting constraints, JSON values and a per-process cached reader over an app-supplied override store), `Core.Identity` refresh-token rotation (single-use rotation where a replay inside the grace window still gets a new token and a replay after it is detected as reuse and revokes the token family over an app-mapped `IssuedRefreshToken` store, SHA-256 `RefreshTokenHash`, and the `SecurityStampClaim` stamp fingerprint compared in constant time), `Core.Queues` (`SweepWorker<TOptions>` sweep base with `SweepOptions`, `BackgroundJobMetrics`), `Core.DDD` `RetrySchedule` (owned retry value object), plus the Elmanhg-built `Core.Http` (outbound HTTP: transient-failure check, JSON send, timeout resilience, per-caller named clients, base address, User-Agent, Fake/real provider switch) and `Core.Messaging` (Resend email, Meta WhatsApp and HTTP SMS transport clients); every core package version is pinned centrally in `api/Directory.Packages.props`, and ASP.NET types come from the `Microsoft.AspNetCore.App` framework reference, which `Core.DDD`, `Core.Errors`, `Core.Settings` and `Core.Utilities` never take) · EF Core 10 + PostgreSQL (Npgsql) · `Elmanhg.Jobs` background host (Hangfire)**. Backend patterns are detailed in `.claude/skills/dotnet-feature/SKILL.md`; the feature pipeline (`feature-planner` → `feature-implementer` → `feature-reviewer`, artifacts in `.process/`) is the standard way features are built. When a rule here conflicts with a generic "best practice", this file wins. New code must be indistinguishable from existing code.

---

## 0. Prime Directives

1. **Ask before implementing.** Plan/design approval is not a build trigger. Do not write code until the dev explicitly says implement (an explicit instruction like "fix X" in a message counts for that task). An explicit autopilot instruction from the dev (e.g. "implement the whole board without interruption") counts for every story it covers.
2. **Mirror, don't modernize.** The repo's way IS the standard. Open a neighboring file and match it.
3. **No magic values — configuration or named constants, nothing inline.**
   - Every **tunable or environment-dependent value** (limits, caps, URLs, container/queue names, feature switches, allowed extensions) lives in `appsettings.json`, bound through a `[Topic]Options` class with a `public const string SectionName`.
   - Every **true invariant** (values that must never change or the product breaks — e.g. a wire-format version string, a hash prefix another system parses) is a **named constant with a WHY comment**, never an inline literal.
   - If unsure which it is: options.
4. **Secrets are NEVER committed.** Not in `appsettings.json`, not in code, not in deployment parameters. Local secrets go in `.env` (gitignored, `.env.example` committed); deployed secrets go in the host's environment variables (`Section__Key` double-underscore convention). External providers without credentials (Paymob, SMS, LLM API, transcription) run behind an interface with a `Fake*` implementation selected by config.
5. **The domain stays tested.** Every aggregate behaviour, every handler branch, and every validator rule lands with its test — see `conventions/dotnet-testing.md`. A guard clause without a test that trips it is untested.
6. **Zero-cost bias.** Any change that adds an always-on resource or paid tier needs explicit approval first.

---

## 1. C# Style — Non-Negotiable

### 1.1 Type/member definitions — ALWAYS one line, never wrap

```csharp
// ✅ DO — one line, however long
public sealed class CreateTenantHandler(ITenantRepository tenantRepository, ICurrentUserService currentUserService, IGenerator generator) : IRequestHandler<CreateTenantCommand, CreateTenantResult>

// ❌ DON'T — parameter wrapping
public sealed class CreateTenantHandler(
    ITenantRepository tenantRepository,
    ICurrentUserService currentUserService) : IRequestHandler<CreateTenantCommand, CreateTenantResult>
```

Exception: multi-property **records** with many fields may list one parameter per line — mirror the file you're extending.

### 1.2 Method bodies — block-bodied with braces, never expression-bodied

Expression bodies are for **properties/accessors only**.

```csharp
// ✅ DO
public string GetStoragePath(string prefix)
{
    return $"{prefix}/{Slug}/{Id}.json";
}

// ✅ DO — computed property
public bool IsSuspended => Status == TenantStatus.Suspended;

// ❌ DON'T
public string GetStoragePath(string prefix) => $"{prefix}/{Slug}/{Id}.json";
```

### 1.3 Braces on every `if` — even one-liners

```csharp
// ✅ DO
if (tenant.Owner is null)
{
    errors.Add("Missing owner.");
}

// ❌ DON'T
if (x is null) throw new InvalidOperationException();
var y = x ?? throw new InvalidOperationException();
```

### 1.4 Everything else

- File-scoped namespaces matching folders (`namespace Elmanhg.Infrastructure.Tenants;`).
- `var` everywhere.
- Records for data shapes (`sealed record`); services/handlers/validators `sealed class`; primary constructors preferred.
- Collection expressions: `[]` for empty, `?? []` for null-coalescing, `[.. spread]`.
- `DateTimeOffset` ONLY — `DateTime` is PROHIBITED. Handlers and core read time from the injected `TimeProvider`; `Repository.SaveChangesAsync` stamps `CreationDate`, `UpdationDate`, `CreatedBy` and `UpdatedBy` when the aggregate did not.
- `CancellationToken cancellationToken` — full name, never `ct`, threaded to every downstream async call.
- `.ConfigureAwait(false)` on every await outside test method bodies.
- No comments explaining WHAT. A comment is allowed only for a hidden WHY-invariant.
- These rules apply to test code too.

---

## 2. Configuration Pattern

```csharp
// Elmanhg.Application/Shared/Options/ProvisioningOptions.cs
public sealed class ProvisioningOptions
{
    public const string SectionName = "Provisioning";

    public int MaxTenantsPerOwner { get; set; }
    public long MaxUploadBytes { get; set; }
    // ...
}
```

- Bound in the layer's `DependencyInjection.cs` (`Elmanhg.Application` / `Elmanhg.Infrastructure`) — the ONLY registration points.
- Injected as `IOptions<ProvisioningOptions>` via primary constructor; read `.Value` once into a field/local.
- **Configuration is never committed.** `appsettings.json` is git-ignored. Locally
  it is copied by hand from the committed `api/Elmanhg.Api/appsettings.example.json`,
  and secrets and per-machine values (connection string, JWT key, OTP secret) come
  from the repo-root `.env` (copied from the committed `.env.example`), which the API
  loads as environment variables in Development only (`Section__Key`
  double-underscore convention). There are no `dotnet user-secrets`. Deployed
  environments supply the same keys as host environment variables. Consequences
  to remember: CI and fresh clones have NO configuration, so integration-style runs
  must supply it explicitly; and a new options section must be announced to the dev
  so it can be mirrored into every environment rather than silently defaulting to
  0/empty. Deployed images bake the committed `appsettings.example.json` as
  `appsettings.json` (shapes only); each host supplies secrets and per-host values
  through uncommitted env files (`deploy/.env`, `api.env`, `ai.env`) as described in
  `docs/deployment.md`.
- **Never re-add `appsettings.json` to the index.** Removing or weakening its
  `.gitignore` entry, or committing it with `git add -f`, is a blocking review
  finding — it publishes the signing key and the connection string in one commit.
- Tests construct options directly: `Options.Create(new ProvisioningOptions { ... })` — test values mirror the committed defaults unless the test targets a limit.

---

## 3. Naming Taxonomy

| Thing | Convention | Example |
|---|---|---|
| Use-case slice | `Elmanhg.Application/{Area}/{UseCase}/{Command\|Query, Handler, Validator, Result}` | `Tenants/CreateTenant/` |
| Command / Query | `[Verb][Noun]Command` / `Get[Noun]Query`, `List[Nouns]Query` | `CreateTenantCommand`, `ListTenantsQuery` |
| Handler | `[Name minus Command]Handler` / `[QueryName]Handler` | `CreateTenantHandler` |
| Result | `[Noun]Result` (+ `[Noun]ResultGenerator` when non-trivial), area `Shared/` when reused | `TenantResult` |
| Options | `[Topic]Options` + `SectionName` const | `ProvisioningOptions` |
| Controller | `[Nouns]Controller` in `Elmanhg.Api/Controllers/{Area}/`, kebab-case plural routes | `TenantsController`, `api/tenants` |
| Entity + repo interface | aggregate folder `Elmanhg.Domain/{Area}/` | `Tenant`, `ITenantRepository` |
| Repo implementation | `Elmanhg.Infrastructure/{Area}/` | `TenantRepository` |

**PROHIBITED names:** `DTO`, `Response` (for internal results), `Model`, `Manager`, `Helper`, `Utils`.

**No abbreviated identifiers.** Full descriptive names always, even when longer — `SemanticVersion` not `SemVer`, `request` not `req`, `document` not `doc`, `cancellationToken` not `ct`, `configuration` not `config`/`cfg`. The only tolerated short forms are universal lambda placeholders (`x =>`, `f =>`) and loop indexers (`i`). Don't swing to the other extreme either — names should be normal words, not sentences.

---

## 4. Layer Rules

- **`Elmanhg.Api`** — thin controllers only: map Request → Command/Query → `mediator.Send` → `Ok(result)`. No business logic. Resources (`Messages.ar/en.resx`) live here. Background sweeps derive from `Core.Queues.SweepWorker<TOptions>`; retried aggregates keep their retry state in `RetrySchedule`.
- **`Elmanhg.Application`** — vertical slices `{Area}/{UseCase}/`, `Exceptions/ErrorCodes.cs`, options, MediatR pipeline behaviors from `Core.CQRS`.
- **`Elmanhg.Domain`** — aggregates (Core.DDD bases: private ctor + `Create` factory, guarded domain methods (a method that receives the transition instant stamps `UpdationDate` with it, otherwise the repository stamps it), soft deletes through `SoftDelete(deletedAt)` with the same time), enums + extensions, repository interfaces in aggregate folders; references only `Core.DDD` and `Core.Settings` (for the `IRuntimeSettingOverride` contract), so it never reaches the ASP.NET Core shared framework.
- **`Elmanhg.Infrastructure`** — ONE `AppDbContext` (named private config methods, soft-delete query filters applied by convention (`modelBuilder.ApplySoftDeleteQueryFilters()` for every `ISoftDeletable` entity), no `ApplyConfigurationsFromAssembly`), repositories (`Repository<T>` base taking `ICurrentUser` + `TimeProvider`, never saving; its `SaveChangesAsync` stamps audit fields), external provider adapters built on `Core.Http` / `Core.Messaging` (OTP channel adapters and their router live in `Core.OTP/Delivery`).
- **`Elmanhg.Tests`** — xUnit + NSubstitute + FluentAssertions per `conventions/dotnet-testing.md`: entity branches, every handler branch, every validator rule; repos/controllers out of scope.
- Errors: no `try`/`catch` in handlers — throw `Core.Errors` exceptions; `Core.Exceptions` middleware (inside request logging, before authorization) formats responses. Never invent an exception type for a covered status code.
- Observability: telemetry is wired only in `Elmanhg.Api/Hosting/ObservabilityExtensions.cs` (on `Core.Observability`) and `ai/src/elmanhg_ai/core/telemetry.py`; business metrics go through `ElmanhgMetrics`, `Core.Queues` `BackgroundJobMetrics` (configured with the `Elmanhg` meter and `elmanhg` prefix), `Core.Observability` `RequestMetrics`, or a pipeline behaviour, never ad-hoc `Meter`s; logs, span attributes and metric tags never carry phone numbers, emails, OTP codes, tokens or payment payloads, and metric tags carry no ids (docs/observability.md).
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults in `api/core-libraries`; the app passes them in as parameters or options.
- One `SaveChangesAsync` per handler, at the end. Full backend detail: `.claude/skills/dotnet-feature/SKILL.md`.

---

## 5. Frontend (web/) and AI service (ai/)

- `web/`: governed by `.claude/skills/react-feature/SKILL.md` (React 19 + TypeScript strict + Vite 8 + Tailwind v4 + shadcn + TanStack Query + i18next RTL). Every visual value is a token from `.claude/design-system.md` (Mist, light only). Screen content and flow come from `prototype/`.
- No magic values: API base URL and tunables come from Vite env (`import.meta.env.VITE_*`) with `.env.example` committed, real `.env.local` gitignored.
- `ai/`: Python 3.13 FastAPI service (uv, committed `uv.lock`, pydantic v2, structlog, ruff, mypy strict, pytest), governed by `.claude/skills/python-feature/SKILL.md`. It is called only by the .NET API over HTTP with a shared service token (`Authorization: Bearer`; contract in `docs/ai-service.md`). Every model/provider call sits behind `clients/model.py` with a fake for tests and offline runs; the OpenAI-compatible LLM adapter (`clients/openai_compatible_model.py`: OpenAI by default, Gemini or DeepSeek by base URL) is switched on by config. The Anthropic API is not used (dev decision 2026-10-01). Prompt text built from student input is treated as untrusted.

---

## 6. Definition of Done (per change)

- Style checklist (§1) passes on every touched file.
- No inline magic values (§0.3); options bound in `AddApplication` / `AddInfrastructure`.
- `dotnet build` clean — zero new warnings. `dotnet test` green. `web/` build + tests green, `ai/` pytest green, for every touched stack.
- Domain, handler, and validator changes carry tests (§0.5).
- No secrets in the diff (§0.4).
- **Docs sync** (full rule: `.claude/rules/docs-sync.md`): a change that alters business
  logic, scope, architecture, policies, or contracts must update the owning doc in `/docs`
  in the same change — implementation and docs may never give two different answers to the
  same question. Incompleteness is fine (plan says 10 features, 6 built — normal);
  divergence is a blocking finding. Reviewers must check this distinction explicitly.
