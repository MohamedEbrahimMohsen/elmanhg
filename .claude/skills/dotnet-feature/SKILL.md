---
name: dotnet-feature
description: "The engineering handbook for implementing ANY backend work in the Elmanhg solution (.NET 10 · ASP.NET Core API · MediatR 14 · FluentValidation 12 · EF Core 10 · vendored core-libraries Core.*) in Mohamed's DDD/CQRS style. Use whenever the user asks to implement, add, create, or scaffold anything in the Elmanhg API: a feature, command, query, handler, validator, entity, repository, controller, endpoint, migration, or error code. Trigger on: new command, new query, add handler, add entity, wire endpoint, add error code. The feature pipeline agents (feature-planner/implementer/reviewer) follow this skill. All cross-cutting types come from the vendored api/core-libraries (Core.DDD, Core.CQRS, Core.EntityFrameworkCore, Core.Errors, Core.Validation, etc.) — no external extensions packages."
---

# Elmanhg Feature Implementation Handbook

Target stack: **.NET 10 · EF Core 10 (PostgreSQL) · MediatR 14 · FluentValidation 12 · vendored `api/core-libraries` (`Core.*`)**

Companion documents: `docs/constitution.md` (wins on conflict) · `docs/PRD.md` (product rules) · `conventions/dotnet-testing.md` · `.claude/rules/docs-sync.md`.

## Elmanhg deltas — read before anything below

1. **Database is PostgreSQL**, via `Npgsql.EntityFrameworkCore.PostgreSQL`. `UseNpgsql(...)` with `EnableRetryOnFailure()`. Never `UseSqlServer`, never SQL Server-only features (`rowversion`, `NEWSEQUENTIALID`, `datetime2`). Concurrency tokens use PostgreSQL `xmin`: the entity implements `IVersioned` (`Core.DDD`, `public uint Version { get; private set; }`) and `AppDbContext.OnModelCreating` calls `modelBuilder.ApplyRowVersionConvention()` (`Core.EntityFrameworkCore`), which maps every `IVersioned` root entity's `Version` to `xmin`. Never write `.IsRowVersion()` per entity. Timestamps are `timestamptz` (`DateTimeOffset`, UTC). Domain timestamps are truncated with `Core.DDD.Time` `TruncateToMicroseconds()` (PostgreSQL precision); local-day math uses `zone.LocalDate(instant)` / `zone.StartOfDay(day)`.
2. **JSON columns** (question body, grading spec, blueprint counts, context bundles) are `jsonb`: `.HasColumnType("jsonb")` on a typed owned model or a `JsonDocument`/`string` with a documented shape. Never serialise to `nvarchar`/`text` by hand.
3. **Vector search** (AI Avatar retrieval) uses `pgvector` via `Pgvector.EntityFrameworkCore`, only in the stories that need it.
4. **Integration tests** hit a real PostgreSQL through `Testcontainers.PostgreSql`. No in-memory provider, no SQLite stand-in.
5. **Reuse-first from Morabh.** The canonical implementation of every cross-cutting capability lives at `D:\Personal\Projects\Projects\Morabh\repos\apis`:
   - `Core/` (Core.DDD, Core.CQRS, Core.EntityFrameworkCore, Core.Errors, Core.Exceptions, Core.Identity, Core.OTP, Core.Localization, Core.Logging, Core.Auditing, Core.Cache, Core.Notifications, Core.Queues, Core.Utilities, Core.Validation) is vendored into `api/core-libraries/` in this repo. Copy it, do not reference the Morabh path at build time. When vendoring, swap every SQL Server dependency for Npgsql and drop `Core.Azure` unless a story needs it. `Core.Storage` (file storage `IFileStorage` with the Local provider; the S3 provider lives in `Core.Storage.S3`, registered with `AddCoreS3FileStorage()` after `AddCoreFileStorage()`, so `Core.Storage` has no AWS SDK, `StorageContentTypes`, public media serving with a `PrivateFolders` list, `WriteStoredFileAsync`) is Elmanhg-native, with no Morabh equivalent; files and media always go through it.
   - Before designing **any** feature (login, register, OTP, refresh tokens, audit log, localisation, notifications, error codes, paging, file upload…), search the Morabh repo (`Core/`, `Morabh.Application/`, `Morabh.Domain/`, `Morabh.Infrastructure/`, `Morabh.APIs/`, `CRUD_FEATURE_CREATION_GUIDE.md`, `ErrorCodes.md`, `AuditLogs.md`, `Localization.md`). If it exists, copy it into the matching Elmanhg layer, rename namespaces `Morabh.*` → `Elmanhg.*`, and adapt it to the Elmanhg domain. The plan must name the Morabh source file for every reused piece.
   - Only when nothing in Morabh covers it, write it from scratch — in the same shape, layering, naming and error-code style as the Morabh code. Murabaha/BNPL business logic is never copied.
   - Promoted in Elmanhg (no Morabh source): `Core.Hosting`, `Core.Observability`, `Core.Spreadsheets`, `Core.Settings`, plus `Core.Cache` (`ICacheableQuery` + `CachingBehaviour`), `Core.Logging` `TextRedactor`, `Core.Queues` (`SweepWorker<TOptions>`, `SweepOptions`, `BackgroundJobMetrics`), `Core.DDD` `RetrySchedule` and `Core.Errors` `BaseException.ErrorCodeOf`. Cache a query by implementing `ICacheableQuery` (lifetime: its own `Ttl`, else a named `CacheProfile` the app maps in `CachingOptions.Profiles`, else `Caching:DefaultSeconds`); read/write spreadsheets through `ISpreadsheetReader`/`ISpreadsheetWriter`; build rate-limit policies from `Core.Hosting.RateLimiting.RateLimitPartitions`; redact free text with a `TextRedactor`; write a background sweep as a `SweepWorker<TOptions>` subclass; keep retry state in a `RetrySchedule` owned value. Read a runtime setting or feature flag with `Core.Settings` `IRuntimeSettings.GetAsync(XRuntimeSettings.Key, cancellationToken)`; the app declares keys, definitions and constraints in `Application/Shared/RuntimeSettings/Definitions` (docs/configuration.md §4) and never re-implements the store, cache or validation. Plus the E21.S3 helpers: `ICurrentUserService.GetRequiredUserId(code)`, `IRepository<T>.GetRequiredAsync(id or predicate, code, …)`, `PageData<T>.Map`, `IQueryable<T>.ToPageDataAsync`, `ModelBuilder.ApplySoftDeleteQueryFilters`, `IOtpRepository.ConsumeAsync`, `Core.DDD.Time` and `AddValidatedOptions`. Plus `Core.DDD.Identity.ICurrentUser` (id, name, role; `ICurrentUserService` extends it) and audit stamping through the opt-in `AuditStampingInterceptor` (`AddCoreAuditStamping<AppDbContext>()`, after domain-event dispatch). Plus `Core.Identity` refresh-token rotation: refresh a session with `IRefreshTokenService.ValidateTokenAsync` then `IRefreshTokenRotator<TUser>.RotateAsync(user, presentedToken, cancellationToken)` (registered by `AddCoreRefreshTokenRotation<TUser>()`, grace in `RefreshTokenRotationOptions`); the app maps `IssuedRefreshToken` in `AppDbContext` and implements `IIssuedRefreshTokenRepository.AddIfAbsentAsync`; stamp fingerprints go through `SecurityStampClaim`.
   - `Elmanhg.Domain` references only `Core.DDD` and `Core.Settings` (for the `IRuntimeSettingOverride` contract) and never the ASP.NET Core shared framework, directly or transitively: `Core.DDD`, `Core.Errors`, `Core.Settings` and `Core.Utilities` use specific `Microsoft.Extensions.*` packages, never `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (`DomainAssemblyReferencesTests` guards this).
6. **Solution layout**: `api/Elmanhg.slnx` with `Elmanhg.Api`, `Elmanhg.Application`, `Elmanhg.Domain`, `Elmanhg.Infrastructure`, `Elmanhg.Jobs` (background jobs, when needed), `Elmanhg.Tests`, plus `core-libraries/`. Mirror Morabh's project structure.
7. **Secrets**: environment variables and a gitignored `.env`; `appsettings.json` holds shape and safe defaults only. External providers without credentials in this repo (Paymob, SMS gateway, Claude API, transcription) sit behind an interface with a `Fake*` implementation selected by config, so the app and tests run offline.

**Core-first, always.** Before writing any helper, middleware, validator extension, client, or base type — check `api/core-libraries/`. Re-implementing an existing `Core.*` capability is a defect, not a style choice. **Mirror, don't modernize** — when in doubt, open a neighboring slice; consistency beats cleverness.

---

## 1. Non-Negotiable Style Rules

Apply everywhere, every file, no exceptions.

### Namespaces — always file-scoped
```csharp
// ✅
namespace Elmanhg.Domain.Tenants;
// ❌
namespace Elmanhg.Domain.Tenants { }
```

### Types
- Commands and queries: `sealed record` — classes PROHIBITED
- Validators: `sealed class` — separate file from command, same folder
- Handlers: `sealed class`
- Value objects: `sealed record` (simple) or class with behavior
- Result types: `sealed record` (simple) or `sealed class` (rich)

### Class Definition Style
One line, however long — never wrap parameters (constitution §1.1).
```csharp
public sealed class CreateTenantHandler(ITenantRepository tenantRepository, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<CreateTenantCommand, TenantResult>
```

### No Comments
Zero comments unless the WHY is a hidden invariant. Never explain what code does.

### No Long Files
~80–100 lines max. Extract to extensions, generators, or helpers. One responsibility per file.

### Properties / Computed Members — inline
```csharp
public bool IsExpired => ExpiresAt < DateTimeOffset.UtcNow;
public decimal AmountDue => Amount + ServiceFees;
```

### LINQ / Fluent Chains — new line per operator
```csharp
return subscriptions
    .Where(x => x.IsActive())
    .Select(TenantResultGenerator.Generate)
    .ToList();
```

### Switch Expressions — always expression form
```csharp
return status switch
{
    TenantStatus.Active => "Active",
    TenantStatus.Suspended => "Suspended",
    _ => "Unknown",
};
```

### Collections — use `[]`
```csharp
public List<TenantSubscription> Subscriptions { get; init; } = [];
```

### ConfigureAwait — always on every await outside controllers
```csharp
var tenant = await tenantRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
```

### DateTime — ALWAYS DateTimeOffset, NEVER DateTime
```csharp
// ✅
public DateTimeOffset CreatedAt { get; init; }
// ❌
public DateTime CreatedAt { get; init; }
```

### Names — no abbreviations (constitution §3)
`SemanticVersion` not `SemVer`, `request` not `req`, `cancellationToken` full name, threaded to every downstream async call.

### No magic values (constitution §0.3)
Tunables → `[Topic]Options` + `SectionName` const bound from configuration; invariants → named constants with a WHY comment.

### Async — never block, never fire-and-forget
```csharp
// ❌
var tenant = tenantRepository.GetByIdAsync(id, cancellationToken).Result;
public async void Publish(Guid tenantId) { }
await Task.Run(() => tenantRepository.GetByIdAsync(id, cancellationToken));
// ✅
var tenant = await tenantRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
```
PROHIBITED: `.Result`, `.Wait()`, `GetAwaiter().GetResult()`, `async void`, `Task.Run` wrapping async I/O, `Thread.Sleep`, `Task.WhenAll` over calls sharing one `DbContext` (not thread-safe).

### No escape hatches
`dynamic`, `object` parameters, `IServiceProvider` injection / service locators, `HttpContext` below `Elmanhg.Api`, `// TODO`, commented-out code, swallowed exceptions (`catch { }`, `catch { return null; }`) — all PROHIBITED.

---

## 2. Naming Reference

| Thing | Convention | Example |
|-------|-----------|---------|
| File | PascalCase | `CreateTenantHandler.cs` |
| Namespace | file-scoped, mirrors folder | `namespace Elmanhg.Application.Tenants.CreateTenant;` |
| Command | `[Verb][Noun]Command` sealed record | `CreateTenantCommand` |
| Query | `[Verb][Noun]Query` sealed record | `GetTenantsQuery` |
| Handler | `[Verb][Noun]Handler` sealed class | `CreateTenantHandler` |
| Validator | `[Command]Validator` sealed class, separate file | `CreateTenantValidator` |
| API input | `[Feature]Request` sealed record | `CreateTenantRequest` |
| CQRS output | `[Noun]Result` sealed record or class | `TenantResult`, `TenantAdminResult` |
| Result generator | `[Noun]ResultGenerator` static class | `TenantResultGenerator` |
| Entity | PascalCase class | `Tenant`, `TenantSubscription` |
| Value object | `sealed record` | `TenantAddress` |
| Enum | PascalCase | `TenantStatus` |
| Enum extensions | `[Enum]Extensions` static class, same file as enum | `TenantStatusExtensions` |
| Domain extensions | `[Entity]Extensions` static class | `TenantSubscriptionExtensions` |
| Repo interface | `I[Entity]Repository : IRepository<T>` | `ITenantRepository` |
| Repo impl | `[Entity]Repository : Repository<T>` | `TenantRepository` |
| Error code const | SCREAMING_SNAKE_CASE string value | `"TENANT_NOT_FOUND"` |
| Error codes class | `ErrorCodes` flat static, comment-grouped | `ErrorCodes.TenantNotFound` |
| Lifecycle status enum value | `Deleted` — never `Removed`; domain method `Delete()` pairs with `SoftDelete()` | `EngineerStatus.Deleted` |
| Area options | `[Area]Options` + `SectionName` const; ALL schema/business caps live here | `EngineersOptions` |
| Policy names | `DefaultCodes` static class, PascalCase | `DefaultCodes.AdminTenantsRead` |
| DbSet | plural PascalCase, `{ get; set; }` | `public DbSet<Tenant> Tenants { get; set; }` |
| Private ctor | `private Entity(Guid id, Guid? createdBy)` | `private Tenant(Guid id, Guid? createdBy)` |
| Factory method | `static [Entity] Create(...)` | `Tenant.Create(...)` |
| Domain method | verb, mutates state; stamps `UpdationDate` only with an instant it receives | `tenant.Suspend()`, `tenant.Update(name)` |

**PROHIBITED names**: `DTO`, `Response` (for CQRS outputs), `Model` (for results), `Service` (for use-case logic).

---

## 3. Project & Folder Structure

```
api/
├── core-libraries/             Core.DDD · Core.CQRS · Core.EntityFrameworkCore · Core.Errors
│                               Core.Exceptions · Core.Validation · Core.Identity · Core.Localization
│                               Core.Auditing · Core.Queues · Core.Logging · Core.Cache · Core.OTP · Core.Notifications
│                               Core.Hosting · Core.Observability · Core.Spreadsheets · Core.Utilities · Core.Storage(.S3) · Core.Settings
│                               Core.Http · Core.Messaging (Elmanhg-built: outbound HTTP, message transports)
├── Elmanhg.Domain/
│   ├── Identity/               User.cs, Role.cs, RoleNames.cs (template)
│   └── {Area}/                 Entity + ValueObjects + Enums (+ext same file) + Extensions
│                               + I{Entity}Repository — aggregate folder holds all of it
├── Elmanhg.Application/
│   ├── {Area}/{UseCase}/       Command|Query + Validator + Handler (folder-per-use-case)
│   ├── {Area}/Shared/          shared Results + ResultGenerators for the area
│   ├── Exceptions/             ErrorCodes.cs
│   └── DependencyInjection.cs
├── Elmanhg.Infrastructure/
│   ├── Data/Context/           AppDbContext.cs  ← ONE shared context; all areas add DbSets here
│   ├── {Area}/                 TenantRepository.cs …
│   └── DependencyInjection.cs
├── Elmanhg.Api/                    ← ALL controllers live here only
│   ├── Controllers/{Area}/     TenantsController.cs (+ Requests.cs when HTTP shape differs)
│   ├── Resources/              Messages.ar.resx, Messages.en.resx
│   └── Program.cs
└── Elmanhg.Tests/                  xUnit + NSubstitute + FluentAssertions
```

**Tests are REQUIRED** per `conventions/dotnet-testing.md` — entity branches, every handler branch, every validator rule (repository implementations and controllers are out of scope). This supersedes any older no-tests rule.

---

## 4. Domain Layer

### 4.1 Entity Base Classes (from Core.DDD)

| Class | Inherits | Adds |
|-------|----------|------|
| `Entity(Guid id)` | — | `Id`, `IsDeleted`, `DeletedAt`, `SoftDelete(DateTimeOffset deletedAt)`, domain events |
| `AuditEntity(Guid id, Guid? createdBy)` | `Entity` | `CreatedBy`, `CreationDate`, `UpdatedBy`, `UpdationDate` |
| `AggregateRoot(Guid id, Guid? createdBy)` | `AuditEntity` | aggregate boundary marker |

Use `AggregateRoot` for top-level aggregates. `AuditEntity` for child entities needing audit. `Entity` for simple children.
All entities implement `ISoftDeletable` — call `.SoftDelete(now)` with the same `now` you stamp on `UpdationDate`; never set `IsDeleted`/`DeletedAt` directly.

### 4.2 Aggregate Root

```csharp
namespace Elmanhg.Domain.Tenants;

public class Tenant : AggregateRoot
{
    public LocalizedText Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public TenantStatus Status { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public List<TenantSubscription> Subscriptions { get; private set; } = [];

    private Tenant(Guid id, Guid createdBy) : base(id, createdBy) { }

    public static Tenant Create(LocalizedText name, string slug, Guid ownerUserId, Guid createdBy)
    {
        return new Tenant(Guid.NewGuid(), createdBy)
        {
            Name = name,
            Slug = slug,
            OwnerUserId = ownerUserId,
            Status = TenantStatus.Active,
        };
    }

    public void Update(LocalizedText name, string slug)
    {
        Name = name;
        Slug = slug;
    }

    public void Suspend()
    {
        if (Status == TenantStatus.Suspended)
        {
            throw new BusinessRuleViolationException(ErrorCodes.TenantAlreadySuspended);
        }
        Status = TenantStatus.Suspended;
    }
}
```

Rules:
- Private constructor. `static Create(...)` is the only way to construct from application code.
- All state changes through named domain methods. Direct property mutation from handlers is PROHIBITED.
- The `AuditStampingInterceptor` stamps `UpdationDate` (from `TimeProvider`) when the method did not, and `UpdatedBy` as the acting user (`ICurrentUser`) when there is one. A method sets `UpdatedBy` only from an actor argument it receives, never from an owner property (`StudentId`, `TeacherId`). A method that receives the transition instant sets `UpdationDate = at` so every field of the transition shares it. A method that changes only owned values or child collections stamps `UpdationDate` itself.
- Guard THEN mutate THEN stamp. Braces on every `if`.

### 4.3 Child Entity

```csharp
namespace Elmanhg.Domain.Tenants;

public class TenantSubscription : AuditEntity
{
    public Guid TenantId { get; private set; }
    public Guid SubscriptionTierId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public SubscriptionStatus Status { get; private set; }

    private TenantSubscription(Guid id, Guid createdBy) : base(id, createdBy) { }

    public static TenantSubscription Create(Guid tenantId, Guid tierId, DateTimeOffset startsAt, DateTimeOffset expiresAt, Guid createdBy)
    {
        return new TenantSubscription(Guid.NewGuid(), createdBy)
        {
            TenantId = tenantId,
            SubscriptionTierId = tierId,
            StartsAt = startsAt,
            ExpiresAt = expiresAt,
            Status = SubscriptionStatus.Active,
        };
    }

    public bool IsExpired() => ExpiresAt < DateTimeOffset.UtcNow;
}
```

### 4.4 Value Objects

```csharp
namespace Elmanhg.Domain.Tenants;

public sealed record TenantAddress(string Country, string City);
public sealed record TenantContact(string Email, string? Phone)
{
    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
}
```

### 4.5 LocalizedText (from Core.DDD)

Every genuinely bilingual field MUST use `LocalizedText`. Plain `string` for bilingual fields is PROHIBITED. (elmanhg is currently EN-only — plain `string` is correct until a field is genuinely bilingual.)

```csharp
public LocalizedText Name { get; private set; } = default!;

var name = new LocalizedText(arabic: "مجلس الإدارة", english: "Board of Directors");

// EF mapping — ALWAYS ConfigureLocalized, never inline OwnsOne
builder.ConfigureLocalized(x => x.Name);

// Client-facing result — active language only
Name = entity.Name.Localized()

// Admin-facing result — expose both
NameArabic = entity.Name.Arabic,
NameEnglish = entity.Name.English,
```

Never call `.Localized()` in an admin result. Never return raw `LocalizedText` in a client result.

Default language for an unsupported request culture: the single key `CoreLocalization:DefaultLanguage` (first two letters; anything shorter falls back to `en`). `.Localized()` reads the thread default culture on every call.

### 4.6 Enums + Extensions (same file)

```csharp
namespace Elmanhg.Domain.Tenants;

public enum TenantStatus { Active, Suspended, Terminated }

public static class TenantStatusExtensions
{
    public static bool IsOperational(this TenantStatus status)
    {
        return status is TenantStatus.Active;
    }

    public static string ToDisplayString(this TenantStatus status)
    {
        return status switch
        {
            TenantStatus.Active => "Active",
            TenantStatus.Suspended => "Suspended",
            TenantStatus.Terminated => "Terminated",
            _ => "Unknown",
        };
    }
}
```

### 4.7 Domain Extensions (complex calculations only)

```csharp
namespace Elmanhg.Domain.Tenants;

public static class TenantSubscriptionExtensions
{
    public static int DaysRemaining(this TenantSubscription subscription)
    {
        return Math.Max(0, (int)(subscription.ExpiresAt - DateTimeOffset.UtcNow).TotalDays);
    }

    public static bool IsAboutToExpire(this TenantSubscription subscription, int thresholdDays)
    {
        return !subscription.IsExpired() && subscription.DaysRemaining() <= thresholdDays;
    }
}
```

### 4.8 Domain Exception — domain layer only

```csharp
// throw inside entity/domain methods only; never from handlers
throw new BusinessRuleViolationException(ErrorCodes.TenantAlreadySuspended);
```

### 4.9 Repository Interface

```csharp
namespace Elmanhg.Domain.Tenants;

// Simple — generic base covers everything
public interface ITenantRepository : IRepository<Tenant> { }

// With custom queries — only add what base cannot express
public interface ITenantSubscriptionRepository : IRepository<TenantSubscription>
{
    Task<List<TenantSubscription>> GetExpiredAsync(CancellationToken cancellationToken);
}
```

`IRepository<T>` (Core.DDD) provides: `GetAllAsync`, `GetByIdAsync`, `AddAsync`, `AddRangeAsync`, `Update`, `UpdateRange`, `Delete`, `DeleteRange`, `FindAsync`, `FirstOrDefaultAsync`, `FindPaginatedAsync`, `CountAsync`, `SaveChangesAsync`.
Only add custom methods when base methods genuinely cannot express the query. Load-or-404 is the extension `GetRequiredAsync(id or predicate, ErrorCodes.XNotFound, cancellationToken, include?, asNoTracking?)` — never `?? throw new NotFoundCoreException` after `GetByIdAsync`/`FirstOrDefaultAsync`.

---

## 5. Application Layer

### 5.1 ErrorCodes — Flat Static Class

Single flat class in `Elmanhg.Application/Exceptions/ErrorCodes.cs`. No nesting. Group with comment separators only.

```csharp
namespace Elmanhg.Application.Exceptions;

public static class ErrorCodes
{
    // TENANTS
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string TenantSlugTaken = "TENANT_SLUG_TAKEN";
    public const string TenantAlreadySuspended = "TENANT_ALREADY_SUSPENDED";

    // AUTH
    public const string UserNotAuthenticated = "USER_NOT_AUTHENTICATED";
    public const string InsufficientPermissions = "INSUFFICIENT_PERMISSIONS";
}
```

### 5.2 Command

```csharp
namespace Elmanhg.Application.Tenants.CreateTenant;

public sealed record CreateTenantCommand(string NameArabic, string NameEnglish, string Slug, Guid OwnerUserId) : IRequest<TenantResult>;
```

### 5.3 Query

```csharp
namespace Elmanhg.Application.Tenants.GetTenants;

public sealed record GetTenantsQuery(string? Search, TenantStatus? Status, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<TenantResult>>;
```

### 5.4 Validator (separate file, same folder as command)

```csharp
namespace Elmanhg.Application.Tenants.CreateTenant;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.NameEnglish)
            .ValidateRequired(ErrorCodes.TenantNameRequired)
            .ValidateMaxLength(200, ErrorCodes.TenantNameTooLong);

        RuleFor(x => x.Slug)
            .ValidateRequired(ErrorCodes.TenantSlugRequired)
            .ValidateMaxLength(100, ErrorCodes.TenantSlugTooLong);

        RuleFor(x => x.OwnerUserId).ValidateRequired(ErrorCodes.OwnerRequired);
    }
}
```

**Core.Validation extensions** — use over raw `.NotEmpty()` / `.Must()` wherever they fit:

| Extension | Use Case |
|-----------|----------|
| `ValidateRequired(errorCode?)` | string?, Guid, Guid? (null or empty fails), int, IFormFile? |
| `ValidateMaxLength(max, errorCode?)` / `ValidateMinLength(min, errorCode?)` | string |
| `ValidatePositive` / `ValidateGreaterThanZero` / `ValidateMax` / `ValidateMin` | numeric |
| `ValidateEmail` / `ValidateUrl` / `ValidateOnlyNumbers` | string |
| `ValidateOnlyEnglishText` / `ValidateOnlyArabicText` | string |
| `ValidateImageExtensions` / `ValidateMaxFileSize` | IFormFile |
| `ValidateDateRequired` | DateOnly/DateTimeOffset |
| `ValidateListContainOneItem` | collection |
| `ValidatePaging(pageNumber, pageSize, max, numberCode, sizeCode)` | on `RuleFor(x => x)` — page ≥ 1, offset within `int`, size 1…max |
| `ValidateDateRange(from, to, code, maxSpan/maxDays?, tooLongCode?)` | on `RuleFor(x => x)` — instants half-open, `DateOnly` inclusive days |
| `ValidateDistinct(code)` | collection |
| `ValidateFileSignature(signatures, code)` | IFormFile? — `FileSignature.Png/Jpeg/WebP/Gif/WebM/Ogg/Mp4/Zip` (`ForExtensions(...)` to rebind) |

(Verify exact names in `core-libraries/Core.Validation` before use — the vendored set is the truth.)

### 5.5 Command Handler

```csharp
namespace Elmanhg.Application.Tenants.CreateTenant;

public sealed class CreateTenantHandler(ITenantRepository tenantRepository, ICurrentUserService currentUserService) : IRequestHandler<CreateTenantCommand, TenantResult>
{
    public async Task<TenantResult> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var slugTaken = await tenantRepository.FirstOrDefaultAsync(x => x.Slug == request.Slug, cancellationToken).ConfigureAwait(false);
        if (slugTaken is not null)
        {
            throw new ConflictCoreException(ErrorCodes.TenantSlugTaken);
        }

        var name = new LocalizedText(arabic: request.NameArabic, english: request.NameEnglish);
        var tenant = Tenant.Create(name, request.Slug, request.OwnerUserId, userId);

        await tenantRepository.AddAsync(tenant, cancellationToken).ConfigureAwait(false);
        await tenantRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TenantResultGenerator.Generate(tenant);
    }
}
```

### 5.6 Query Handler

List handlers return `List<TResult>` with `?? []` — never null. Use `FindPaginatedAsync`/`PageData<T>` only when the endpoint is genuinely paginated (admin grids); there is NO blanket PageData mandate. Language-aware ordering via `ILocalizer.IsCurrentLanguageArabic` when bilingual fields exist. Map a page with `page.Map(XResultGenerator.Generate)` — never copy the four paging fields by hand.

Handler rules:
- `sealed class`. No `try`/`catch`. Throw core exceptions directly.
- `SaveChangesAsync` called **once in handler** after all mutations — never inside repo CRUD methods.
- `.ConfigureAwait(false)` on every `await`.
- Current user read FIRST via `currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated)` (401).
- Business rule checks live in the DOMAIN (entity methods throw); the handler orchestrates.
- Never validate manually in handlers — the `Core.CQRS` `ValidationBehaviour` pipeline enforces validators → 422.
- Auditing: business mutations opt in via `IAuditableCommand` (Core.Auditing) — pipeline-side; handlers are never modified for auditing.
- Read-only queries pass `asNoTracking: true` wherever the Core repository method exposes it — tracking only for entities the handler mutates (§6.6).
- A handler that keeps growing gets a domain method on the aggregate — never a one-handler "helper" service.

### 5.7 Results

```csharp
// Client-facing — .Localized() on all LocalizedText fields
public sealed record TenantResult(Guid Id, string Name, string Slug, string Status, DateTimeOffset CreatedAt);

// Admin-facing — both language values, no .Localized()
public sealed record TenantAdminResult(Guid Id, string NameArabic, string NameEnglish, string Slug, string Status, DateTimeOffset CreatedAt);
```

### 5.8 Result Generator (when mapping is non-trivial)

```csharp
namespace Elmanhg.Application.Tenants.Shared;

public static class TenantResultGenerator
{
    public static TenantResult Generate(Tenant tenant)
    {
        return new TenantResult(tenant.Id, tenant.Name.Localized(), tenant.Slug, tenant.Status.ToString(), tenant.CreationDate);
    }
}
```

### 5.9 Application Exceptions (from Core.Errors) — reuse only

No `try`/`catch`. Throw directly. `CoreExceptionMiddleware` (Core.Exceptions) catches and formats.

| Exception | HTTP | When |
|-----------|------|------|
| `UnauthorizedCoreException(code)` | 401 | no authenticated user |
| `ForbiddenCoreException(code)` | 403 | authenticated but lacks permission |
| `NotFoundCoreException(code)` | 404 | entity not found |
| `ConflictCoreException(code)` | 409 | duplicate / concurrency conflict |
| `BadRequestCoreException(code, context?)` | 400 | general bad input |
| `BusinessRuleViolationCoreException(code, context?)` | 400 | business rule violation |
| `ApplicationValidationCoreException(code)` | 422 | validation (auto from pipeline) |
| `RateLimitExceededCoreException(code)` | 429 | rate limit |
| `InternalServerErrorCoreException(code)` | 500 | unexpected |
| `BusinessRuleViolationException(code)` | — (domain only) | inside entity domain methods |
| `InfrastructureCoreException(errorCode)` | 500, masked | infrastructure unhandled failures only |

NEVER create a new exception class for a status code already in this list.

With context dict:
```csharp
throw new BadRequestCoreException(ErrorCodes.DownPaymentTooLow, context: new Dictionary<string, object> { ["minimum"] = plan.MinimumDownPayment });
```

### 5.10 Context access

Handlers depend on repositories (aggregate-folder interfaces), never on concrete `AppDbContext`. If a use case genuinely needs direct context access, introduce/extend an `IAppDbContext` abstraction in `Application/Shared/Context` — repository-first remains the default (mirrors the template).

### 5.11 DependencyInjection.cs (Application)

```csharp
namespace Elmanhg.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        return services;
    }
}
```
(Mirror the actual signature already in `Elmanhg.Application/DependencyInjection.cs` — the repo is the truth.)

---

## 6. Infrastructure Layer

### 6.1 Repository Implementation

```csharp
namespace Elmanhg.Infrastructure.Tenants;

// Simple — generic base covers everything
public class TenantRepository(AppDbContext context) : Repository<Tenant>(context), ITenantRepository { }

// With custom queries
public class TenantSubscriptionRepository(AppDbContext context) : Repository<TenantSubscription>(context), ITenantSubscriptionRepository
{
    public async Task<List<TenantSubscription>> GetExpiredAsync(CancellationToken cancellationToken)
    {
        return await FindAsync(x => x.ExpiresAt < DateTimeOffset.UtcNow && x.Status == SubscriptionStatus.Active, cancellationToken, asNoTracking: true).ConfigureAwait(false);
    }
}
```

`Repository<T>` (Core.EntityFrameworkCore) base provides all 13 `IRepository<T>` members plus `_dbSet` and `_context`; its `SaveChangesAsync` only saves (the interceptor stamps).

**`AddAsync`/`Update`/`Delete` do NOT call `SaveChangesAsync`. Call it once in the handler.**

`FindPaginatedAsync` counts with `LongCountAsync`; a page whose offset is past the last row (or past `int` range) returns empty `Items` with correct totals. A hand-written paged query ends in `.ToPageDataAsync(pageNumber, pageSize, cancellationToken)` (Core.EntityFrameworkCore), never its own `Skip`/`Take` arithmetic.

### 6.2 AppDbContext — Single Shared Context

ONE `AppDbContext` for the whole solution. Per-area DbContext subclasses are PROHIBITED. Actual signature (mirror it):

```csharp
namespace Elmanhg.Infrastructure.Data.Context;

public class AppDbContext(DbContextOptions options, IMediator mediator) : CoreDbContext<User, Role, Guid>(options, mediator)
{
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantSubscription> TenantSubscriptions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureTenants(modelBuilder);
        modelBuilder.ApplySoftDeleteQueryFilters();
    }
}
```

- ✅ Always `DbSet<T> Xxx { get; set; }` — expression-bodied `=> Set<T>()` is PROHIBITED.
- `CoreDbContext` (Core.EntityFrameworkCore) handles domain-event dispatch on `SaveChangesAsync`; the `AuditStampingInterceptor` (registered by `AddCoreAuditStamping<AppDbContext>()`) stamps audit fields inside the save, after dispatch — never dispatch events manually, and never stamp the fields the interceptor supplies; the one exception is the §4 audit-fields rule: a method that changes only an owned value or a child collection (so the parent stays `Unchanged`) sets `UpdationDate` itself.

### 6.3 Entity Configuration

`ApplyConfigurationsFromAssembly` is PROHIBITED. Apply configs explicitly in named private methods.

```csharp
private static void ConfigureTenants(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Tenant>(builder =>
    {
        builder.ConfigureLocalized(x => x.Name);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasMany(x => x.Subscriptions)
            .WithOne()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    });
}
```

**EF Core Mapping Rules**

| Rule | When |
|------|------|
| `builder.ConfigureLocalized(x => x.Prop)` | ALL `LocalizedText` properties — no exceptions |
| `OwnsOne(x => x.ValueObject)` | simple value objects with no separate table |
| `OwnsOne(x => x.Retry, …)` + `Navigation(x => x.Retry).IsRequired()` | a `RetrySchedule` (explicit `HasColumnName`, index names kept) |
| `HasConversion<string>().HasMaxLength(50–100)` | all enums |
| `OnDelete(DeleteBehavior.Restrict)` | default for all FKs |
| `HasQueryFilter(e => !e.IsDeleted)` | never by hand; `ApplySoftDeleteQueryFilters()` adds it to every root `ISoftDeletable` entity |
| `HasIndex(...).IsUnique()` | unique business keys |
| `ValueGeneratedNever()` | child entities with manually assigned IDs |
| `HasPrecision(18, 2)` (or the currency's scale) | every money `decimal` — `double`/`float` for money PROHIBITED |
| implement `IVersioned` (`uint Version`), no per-entity config | aggregates written concurrently (§6.8) |

### 6.4 Soft-Delete Global Filters — MANDATORY for every entity

Never append `.Where(!IsDeleted)` in queries. The convention call is the single enforcement point — a new `Entity` gets the filter automatically. Use `IgnoreQueryFilters()` only in explicit admin/audit queries with documented reason.

### 6.5 DependencyInjection.cs (Infrastructure)

```csharp
namespace Elmanhg.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITenantRepository, TenantRepository>();
        return services;
    }
}
```
(No configuration parameter — mirror the actual signature. DbContext/Identity wiring lives in `Program.cs` via the `Core.*` composition already there.)

### 6.6 Query Rules

```csharp
// ❌ DON'T — materialise then filter; count to test existence
var subscriptions = await FindAsync(x => x.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
var active = subscriptions.Where(x => x.Status == SubscriptionStatus.Active).ToList();
if (await CountAsync(x => x.Slug == slug, cancellationToken).ConfigureAwait(false) > 0) { }

// ✅ DO — the whole predicate reaches SQL; existence is AnyAsync inside the repository
return await FindAsync(x => x.TenantId == tenantId && x.Status == SubscriptionStatus.Active, cancellationToken, asNoTracking: true).ConfigureAwait(false);

public async Task<bool> IsSlugExistsAsync(string slug, CancellationToken cancellationToken)
{
    return await _dbSet.AnyAsync(x => x.Slug == slug, cancellationToken).ConfigureAwait(false);
}
```

- `asNoTracking: true` on every read that does not mutate.
- No awaited repository call inside a `foreach` over query results (N+1) — load the set once with one predicate. (The §8.3 suffix loop probes, it does not iterate results — allowed.)
- Multiple collection `Include` on a write path → `AsSplitQuery()`.
- Lazy-loading proxies (`UseLazyLoadingProxies`) PROHIBITED.
- Raw SQL only inside `Elmanhg.Infrastructure`, parameterised only: `FromSql($"...")` / `ExecuteSql($"...")`. `FromSqlRaw` / `ExecuteSqlRaw` with interpolation or concatenation = SQL injection, BLOCKING.
- `ExecuteUpdateAsync` / `ExecuteDeleteAsync` bypass `CoreDbContext` and the stamping interceptor — no audit stamping, no domain events, and `ExecuteDeleteAsync` hard-deletes past `ISoftDeletable`. Only for non-audited bulk maintenance the plan names; never a load-modify-save loop over many rows either.

### 6.7 Migrations

```bash
dotnet ef migrations add AddTenantRowVersion -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api
dotnet ef migrations script --idempotent -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api
```
(Mirror the repo's actual project paths.)

| Rule | Detail |
|------|--------|
| Name | `<Verb><What>` PascalCase — `AddTenantRowVersion`, `CreateInvoicesTable` |
| Same change | entity/mapping change → migration in the same change |
| Review | open the generated file — no `DropColumn` / `DropTable` / `RenameColumn` unless the plan names it as a contract step |
| Expand/contract | across releases: (1) add nullable column/new table, write both · (2) backfill in batches · (3) switch reads · (4) drop old column later. Never rename in one step |
| NOT NULL | never add a NOT NULL column without a default to a populated table |
| Transactions | EF Core 10 does not wrap all migrations in one transaction — review via `--idempotent` script |
| Startup | `Database.Migrate()` / `EnsureCreated()` at startup outside local development PROHIBITED — mirror the existing deployment path |

### 6.8 Transactions, Concurrency & Side Effects

- One `SaveChangesAsync` per command is atomic — that IS the transaction. Explicit `BeginTransactionAsync` only across multiple saves or raw SQL, wrapped in `CreateExecutionStrategy().ExecuteAsync(...)` when retry-on-failure is on (PostgreSQL).
- Concurrency token on concurrently written aggregates: `public sealed class Order : AuditEntity, IVersioned` with `public uint Version { get; private set; }`; `ApplyRowVersionConvention()` maps it to `xmin`.
- Conflicts → 409 are declared once in `AppDbContext.Conflicts` (`AppDbContext.Conflicts.cs`), a `Core.EntityFrameworkCore.Conflicts.ConflictMap`: `.MapConcurrency<Order>(ErrorCodes.OrderModifiedConcurrently)` for a stale row version, `.MapUniqueConstraint(OrderNumberIndex, ErrorCodes.OrderNumberTaken)` (or `.MapUniqueTable(...)`) for a unique index race. `SaveChangesAsync` has a single `catch (DbUpdateException) when (Conflicts.TryTranslate(...))`. Registration order is precedence. Never a handler `try`/`catch`, never a new catch block; verify the existing mapping before adding an error code.
- Side effects that must follow a commit (emails, notifications, webhooks) → outbox row written in the same `SaveChangesAsync`, dispatched by a worker. Check `Core.Notifications` first; dispatch with a `Core.Queues` `SweepWorker<TOptions>` subclass (core has no outbox type).

---

## 7. API Layer

All controllers live in `Elmanhg.Api` ONLY. Module/layer projects have NO controllers.

### 7.1 DefaultCodes — Policy Names

```csharp
namespace Elmanhg.Domain.SharedKernel;

public static class DefaultCodes
{
    // TENANTS
    public const string AdminTenantsRead = "Admin.Tenants.Read";
    public const string AdminTenantsCreate = "Admin.Tenants.Create";
}
```

NEVER use `[Authorize(Roles = "...")]` or raw string literals. Always `[Authorize(Policy = DefaultCodes.Xxx)]` — following whatever policy wiring `Program.cs` (Core.Identity) already establishes; mirror the neighboring controller.

### 7.2 Request Records (API inputs)

API inputs use `Request` suffix, in a `Requests.cs` inside the controller's folder — only when the HTTP shape differs from the command; otherwise bind the command directly with `[FromBody]`.

```csharp
public sealed record CreateTenantRequest(string NameArabic, string NameEnglish, string Slug, Guid OwnerUserId);
```

### 7.3 Controller

```csharp
namespace Elmanhg.Api.Controllers.Tenants;

[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = DefaultCodes.AdminTenantsRead)]
    public async Task<ActionResult> GetAll([FromQuery] string? search, [FromQuery] TenantStatus? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetTenantsQuery(search, status, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = DefaultCodes.AdminTenantsCreate)]
    public async Task<ActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateTenantCommand(request.NameArabic, request.NameEnglish, request.Slug, request.OwnerUserId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{tenantId:guid}")]
    [Authorize(Policy = DefaultCodes.AdminTenantsDelete)]
    public async Task<ActionResult> Delete([FromRoute] Guid tenantId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTenantCommand(tenantId), cancellationToken);
        return Ok();
    }
}
```

Controller rules:
- Route: lowercase kebab-case plural nouns (`api/tenants`, `api/subscription-tiers`). Nested: `api/tenants/{tenantId}/subscriptions`.
- `[Authorize(Policy = DefaultCodes.Xxx)]` on every action; `[AllowAnonymous]` for public catalog reads.
- Thin: map Request → Command/Query → send → `Ok(result)` (bare `Ok()` when the command returns nothing). No business logic.
- `[FromBody]` for POST/PUT/PATCH · `[FromRoute]` for path params · `[FromQuery]` for filters.
- `CancellationToken cancellationToken` on every action, passed to `Send`.

**REST Method Map**

| HTTP | Route | Operation |
|------|-------|-----------|
| GET | `/resources` | list / paginated search |
| GET | `/resources/{id}` | get single |
| POST | `/resources` | create |
| PUT | `/resources/{id}` | full replace |
| PATCH | `/resources/{id}` | partial update |
| DELETE | `/resources/{id}` | delete |

### 7.4 Program.cs

`Elmanhg.Api/Program.cs` already composes the full `Core.*` pipeline (environment variables + `.env` locally, Core.Identity, Core.CQRS, Core.Exceptions middleware, Core.Localization, Core.Logging, Core.Hosting (Kestrel limits, forwarded headers, migrate-and-exit, secret guard, rate limiting), Core.Observability, Scalar OpenAPI). Mirror the existing composition when adding registrations — **middleware order is fixed**: forwarded headers → `UseCoreLocalization` → `CoreRequestLoggingMiddleware` → `CoreExceptionMiddleware` → HTTPS redirection → media storage → `UseAuthorization` → rate limiter → endpoints. The exception middleware sits inside request logging (which reads its error code) and before authorization, so a failure in an authorization handler still returns the standard error body; do not reorder. New policies go into the existing `AddAuthorization` block.

Core endpoint modules take the policy name from the app (`MapCoreNotificationTemplateEndpoints(adminPolicyName)`, `MapCoreFirebaseNotificationEndpoints(adminPolicyName)`); the user and device groups require an authenticated user.

### 7.5 Localization Resources

```
Elmanhg.Api/Resources/
├── Messages.ar.resx    ← Arabic strings; key = ErrorCode constant value
└── Messages.en.resx    ← English strings; key = ErrorCode constant value
```

Every `ErrorCodes` constant MUST have a matching key in both files. Messages never hardcoded in C# — resolved via `ILocalizer.GetMessage(code)`. Keep runtime placeholders (`{minimum}`, `{days}`) intact in BOTH languages; Arabic without tashkeel.

---

## 8. DO / DON'T Catalog — dev-review learnings (every DON'T found in review is BLOCKING)

Practical patterns extracted from Mohamed's real code reviews. The implementer reads this before writing; the reviewer walks it explicitly.

### 8.1 Schema & business caps live in Options, never as entity constants

A cap change must be a config change, not a redeployment.

```csharp
// ❌ DON'T — constants baked into the entity
public class Engineer : AuditEntity
{
    public const int DisplayNameMaxLength = 100;
    public const int MaxTags = 10;
}

// ✅ DO — one [Area]Options bound from appsettings.json; validators AND AppDbContext consume it
public sealed class EngineersOptions
{
    public const string SectionName = "Engineers";
    public int MaxEngineersPerCreator { get; set; }
    public int DisplayNameMaxLength { get; set; }
}

public CreateEngineerValidator(IOptions<EngineersOptions> engineersOptions)
{
    var options = engineersOptions.Value;
    RuleFor(x => x.DisplayName).ValidateMaxLength(options.DisplayNameMaxLength, ErrorCodes.EngineerDisplayNameTooLong);
}
```

True invariants (e.g. enum-column width) stay as a named constant WITH a WHY comment — but anything a product owner could plausibly tune goes to appsettings.

### 8.2 Identifiers & random values come from Core.Utilities IGenerator — never hand-rolled

```csharp
// ❌ DON'T — invent random/suffix generation in a custom class
var suffix = new Random().Next(1000, 9999).ToString();

// ✅ DO — inject Core.Utilities.Generator.IGenerator (registered by `AddCoreUtilities()`)
candidateSlug = generator.Generate(prefix: baseSlug, size: options.SlugSuffixSize);
```

Custom helpers are allowed ONLY for logic Core genuinely lacks (e.g. kebab-case normalization) — and they should do only that missing part.

### 8.3 Unique-slug pattern: repository `Is…ExistsAsync` + suffix loop — never throw Conflict for an auto-resolvable collision

```csharp
// ❌ DON'T
var existing = await repository.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken).ConfigureAwait(false);
if (existing != null) { throw new ConflictCoreException(ErrorCodes.SlugTaken); }

// ✅ DO — repo exposes the question; the handler auto-uniquifies
public interface IEngineerRepository : IRepository<Engineer>
{
    Task<bool> IsSlugExistsAsync(string slug, CancellationToken cancellationToken);
}

var baseSlug = EngineerSlugGenerator.Normalize(displayName, options.SlugMaxLength);
if (!await engineerRepository.IsSlugExistsAsync(baseSlug, cancellationToken).ConfigureAwait(false)) { return baseSlug; }
string candidateSlug;
do
{
    candidateSlug = generator.Generate(prefix: prefix, size: options.SlugSuffixSize);
} while (await engineerRepository.IsSlugExistsAsync(candidateSlug, cancellationToken).ConfigureAwait(false));
```

### 8.4 Lifecycle status naming: `Deleted`, never `Removed`

```csharp
// ❌ DON'T
public enum EngineerStatus { Draft, Published, Removed }
public void Remove(DateTimeOffset now) { Status = EngineerStatus.Removed; SoftDelete(now); }

// ✅ DO — the enum value pairs with ISoftDeletable semantics
public enum EngineerStatus { Draft, Published, Deleted }
public void Delete() { var now = DateTimeOffset.UtcNow; Status = EngineerStatus.Deleted; SoftDelete(now); UpdationDate = now; }
```

### 8.5 Soft-delete filtering has exactly one home

```csharp
// ✅ DO — the query filter lives ONLY in the convention call at the end of OnModelCreating
modelBuilder.ApplySoftDeleteQueryFilters();

// ✅ Also DO — a partial-index SQL filter stays WITH the index (it is schema, not querying)
builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");

// ❌ DON'T — ad-hoc IsDeleted checks inside queries or handlers
var engineers = await repository.FindAsync(x => !x.IsDeleted && ..., cancellationToken);
```

### 8.6 One JSON contract — every serialization path uses the same options

This app has **more than one** place that turns objects into JSON, and they do not share defaults. Configuring one and forgetting the others has now shipped two real bugs:

- `ConfigureHttpJsonOptions` configures **minimal APIs**; MVC **controllers** read `Mvc.JsonOptions`. Only the former was configured, so `POST /publish` could not bind `{"increment":"Patch"}` — a string enum — while every test passed.
- `CoreExceptionMiddleware` serialized errors with a bare `JsonSerializer.Serialize(response)`, which is **PascalCase**, while MVC serializes successes as **camelCase**. Every client reading `code` off a failure body got `undefined`, so a normal 404 could not be told apart from any other error.

```csharp
// ❌ DON'T — a bare Serialize call anywhere a client will read the result
await context.Response.WriteAsync(JsonSerializer.Serialize(response));

// ❌ DON'T — configure one pipeline and assume the other inherits it
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ✅ DO — configure MVC explicitly when controllers are the surface
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ✅ DO — hand-rolled serialization states its options, matching the API's dialect
private static readonly JsonSerializerOptions ErrorSerializerOptions = new(JsonSerializerDefaults.Web);
await context.Response.WriteAsync(JsonSerializer.Serialize(response, ErrorSerializerOptions));
```

**Both bugs are invisible to unit tests** — the handler and the middleware are correct in isolation; only the wire format is wrong. When you change a serialization path, verify the emitted JSON, not the object.

### 8.7 An expected 4xx is a result, not an application fault

A status the caller asked for is normal flow. Logging it at `Error` with a stack trace makes ordinary behaviour indistinguishable from failure and buries the 5xx that need attention.

```csharp
// ❌ DON'T — every exception logged the same way
logger.LogError(exception, $"StatusCode: {details.StatusCode}, ErrorCode: {details.Code}");

// ✅ DO — 4xx is the caller's outcome; 5xx is ours
if (details.StatusCode is >= 400 and < 500)
{
    logger.LogWarning($"StatusCode: {details.StatusCode}, ErrorCode: {details.Code}, ErrorMessage: {details.Message}");
}
else
{
    logger.LogError(exception, $"StatusCode: {details.StatusCode}, ErrorCode: {details.Code}, ErrorMessage: {details.Message}");
}
```

Related design point: when a state is genuinely normal — "this engineer has no upload yet" — prefer letting the caller **avoid the request** (expose `HasDraft` on the result) over making them provoke and catch a 404. Never leak `Exception.StackTrace` into a response body outside Development.

### 8.8 Outbound HTTP goes through typed clients — never `new HttpClient()`

```csharp
// ❌ DON'T — socket exhaustion, no resilience
private readonly HttpClient http = new();

// ✅ DO — typed client + Core.Http timeout resilience (check Core.* for an existing client first)
services.AddHttpClient<GitHubClient>((serviceProvider, client) => client.BaseAddress = HttpBaseAddress.From(options.BaseUrl)).AddTimeoutResilience(attempt, total, retryUnsafeMethods: false);
```

POST retries only with an idempotency key. Send through `Core.Http`: `SendJsonAsync` (throws `ServiceUnavailableCoreException` with your error code), `TrySendJsonAsync` / `TrySendAsync` (return the failure as a value). Catch only `IsTransientFailure` exceptions. Set the User-Agent with `WithUserAgent(CoreHttpOptions.UserAgent)`. Pick Fake or real with `AddProviderSwitch`. Email, WhatsApp and SMS go through the `Core.Messaging` clients (`ResendEmailClient`, `MetaWhatsAppClient`, `HttpSmsClient`). When a transport client has more than one caller, register each caller with `AddScopedHttpConsumer<TCaller, TClient>` so every caller gets its own named `HttpClient` and circuit breaker; one caller's failures must never trip another's breaker.

### 8.9 Background work gets a scope per iteration

```csharp
// ❌ DON'T — scoped AppDbContext / repository captured by a singleton BackgroundService
public sealed class ExpireSubscriptionsWorker(ITenantSubscriptionRepository repository) : BackgroundService

// ✅ DO — new scope every iteration, honour stoppingToken, catch-and-log per iteration
using var scope = scopeFactory.CreateScope();
var repository = scope.ServiceProvider.GetRequiredService<ITenantSubscriptionRepository>();
```

Prefer deriving from `Core.Queues.SweepWorker<TOptions>`, which owns the scope per listing/item, the `PeriodicTimer` on `TimeProvider`, the kill switch, deferral and job metrics.

The worker is the only place `IServiceScopeFactory` is allowed; a failed iteration is logged, never kills the loop.

### 8.10 Secrets & tokens use cryptographic randomness

`IGenerator` (§8.2) is for identifiers like slugs. Anything an attacker must not guess (OTPs, API keys, reset tokens) → `IGenerator` (Nanoid on `RandomNumberGenerator`) or `RandomNumberGenerator.GetInt32` / `GetBytes`. Never `Random`. Secret comparison → `CryptographicOperations.FixedTimeEquals`, never `==`.

### 8.11 Never weaken a test to go green

```csharp
// ❌ DON'T
[Fact(Skip = "flaky after refactor")]
```

Updating a test because behaviour **intentionally** changed is fine (§9). Editing, skipping, or deleting a test to hide a failure is not → stop and write `BLOCKED: <test> — <why>` in the report.

### 8.12 Options are validated at startup

```csharp
services.AddValidatedOptions<EngineersOptions>(EngineersOptions.SectionName);
```

`AddValidatedOptions<TOptions, TValidator>` also registers an `IValidateOptions<TOptions>`.

A missing/invalid cap fails the boot, not the first request. `IOptions<T>` in singletons, `IOptionsSnapshot<T>` in scoped services. Mirror the existing registration style in `Program.cs`.

### 8.13 Regex on user input is bounded

```csharp
// ✅ DO
private static readonly Regex SlugPattern = new("^[a-z0-9-]+$", RegexOptions.NonBacktracking);
```

`RegexOptions.NonBacktracking` or an explicit `matchTimeout` — never an unbounded pattern over user input (ReDoS).

---

## 9. Checklist — Adding a New Feature

**Domain**
- [ ] Entity extends `AggregateRoot` / `AuditEntity` / `Entity`; private ctor; `static Create(...)`
- [ ] All state changes via named domain methods; methods stamp `UpdationDate` only with an instant they receive (the save stamps the rest); `UpdatedBy` only from an actor argument
- [ ] `LocalizedText` for every genuinely bilingual field — no plain `string` for bilingual data
- [ ] Value objects as `sealed record`
- [ ] Enums + extensions in same file inside the aggregate folder
- [ ] Complex domain calc in `[Entity]Extensions.cs`
- [ ] Repo interface extends `IRepository<T>`; custom methods only when base is insufficient
- [ ] All date/time: `DateTimeOffset` — no `DateTime`

**Application**
- [ ] `ErrorCodes.cs` — add constants (flat class, SCREAMING_SNAKE_CASE, comment-grouped)
- [ ] Command/Query: `sealed record : IRequest<T>`
- [ ] Validator: separate file, `sealed class : AbstractValidator<T>`, Core.Validation extensions
- [ ] Handler: `sealed class`, no `try`/`catch`, throws `*CoreException` directly
- [ ] `SaveChangesAsync` called once in handler — never inside repo CRUD methods
- [ ] Current user via `GetRequiredUserId(ErrorCodes.UserNotAuthenticated)`
- [ ] Client-facing results `.Localized()`; admin-facing `.Arabic`/`.English` separately
- [ ] `Result` suffix on all CQRS outputs — never `DTO`, `Response`, `Model`
- [ ] Business mutations opt into auditing via `IAuditableCommand`

**Infrastructure**
- [ ] Repo: extends `Repository<T>`, implements interface; no `SaveChangesAsync` in CRUD
- [ ] `AppDbContext`: `DbSet<T> Prop { get; set; }` — no `=> Set<T>()`
- [ ] `ConfigureLocalized(x => x.Prop)` for every `LocalizedText` — no inline `OwnsOne`
- [ ] Entity config in named private method — no `ApplyConfigurationsFromAssembly`
- [ ] New entity derives from `Entity` (soft-delete filter applied by convention)
- [ ] Repos registered in `AddInfrastructure`; open-generic `IRepository<>` registration present

**API**
- [ ] Controller in `Elmanhg.Api` only — kebab-case plural route
- [ ] `[Authorize(Policy = DefaultCodes.Xxx)]` on every action — never role strings; `[AllowAnonymous]` only for public reads
- [ ] Policy constant added to `DefaultCodes`
- [ ] API input: `Request` suffix record when HTTP shape differs; controller thin: map → send → `Ok(result)`
- [ ] New error codes added to both `Messages.ar.resx` and `Messages.en.resx`
- [ ] `postman/elmanhg.postman_collection.json` updated: request added/modified/deleted for every endpoint change

**Tests (per conventions/dotnet-testing.md)**
- [ ] Entity: factory + every domain-method branch
- [ ] Every handler branch; every validator rule
- [ ] Repositories and controllers NOT tested (out of scope by convention)
- [ ] Existing tests added/updated/removed as needed to keep the suite true
- [ ] Success path asserts the outcome + `SaveChangesAsync` `Received(1)`; every throwing path asserts exception type + error code + `SaveChangesAsync` `DidNotReceive()`
- [ ] No test only asserts a mock returned what it was told to return
- [ ] No existing test skipped/weakened to go green (§8.11)

**Cross-cutting**
- [ ] File-scoped namespaces everywhere; no file exceeds ~100 lines
- [ ] Every DO/DON'T catalog entry (§8) honoured: caps in `[Area]Options` not entity constants · `IGenerator` for identifiers · `Is…ExistsAsync` + suffix loop for unique slugs · `Deleted` not `Removed` · soft-delete filter only through `modelBuilder.ApplySoftDeleteQueryFilters()`, never a hand-written `HasQueryFilter` · one JSON contract across every serialization path · expected 4xx logged as `Warning`, not `Error`
- [ ] If a serialization path changed, the **emitted JSON** was inspected — not just the object it came from (§8.6)
- [ ] `dotnet build` zero new warnings · `dotnet test` green
- [ ] `dotnet format --verify-no-changes` exits 0 · `dotnet list package --vulnerable --include-transitive` clean
- [ ] Entity change → EF mapping + migration, reviewed for destructive ops (§6.7)
- [ ] Reads: `asNoTracking`, predicate before materialising, no N+1, parameterised SQL only (§6.6)
- [ ] §8.8–§8.13 honoured: typed HTTP clients · scope per worker iteration · crypto RNG for secrets · no weakened tests · options `ValidateOnStart` · bounded regex
- [ ] §10 security, §11 dependency, §12 LLM-mistake lists walked
- [ ] Guard grep prints nothing: `git diff origin/main -U0 -- '*.cs' | grep -E '^\+.*(DateTime\.(Now|UtcNow)|\.Result\b|\.Wait\(\)|new HttpClient\(|FromSqlRaw|async void)'`
- [ ] Anything not done → report ends with `BLOCKED: <reason>`
- [ ] `/docs` updated when behavior/scope changed (`.claude/rules/docs-sync.md`)

---

## 10. Security Gotchas (every row is BLOCKING)

| Gotcha | Rule |
|--------|------|
| SQL injection | `FromSqlRaw` / `ExecuteSqlRaw` + interpolation — never (§6.6) |
| Missing auth | every action has a policy (§7.3); `[AllowAnonymous]` only for public catalog reads |
| BOLA | lookups filtered by owner/tenant; another owner's id → `NotFoundCoreException` (404), never a leak of existence |
| Overposting | commands never carry `TenantId`, `Role`, `IsAdmin`, `Status`, `Price` unless the use case sets them by design and authorises it |
| Identity source | user/tenant ids come from `ICurrentUserService` claims — never the request body |
| JWT | owned by `Core.Identity` — never `ValidateIssuerSigningKey = false`, never `RequireSignedTokens = false` |
| Refresh tokens | rotate only through `Core.Identity` `IRefreshTokenRotator<TUser>`; store `RefreshTokenHash`, never the raw token; compare stamp fingerprints with `SecurityStampClaim.Matches`, never `==` |
| Deserialization | never `BinaryFormatter` / `SoapFormatter` / `NetDataContractSerializer` / `LosFormatter` / `ObjectStateFormatter`; `XmlReader` with `DtdProcessing.Prohibit` |
| CORS | never `AllowAnyOrigin()` with credentials, never `SetIsOriginAllowed(_ => true)` |
| Error leakage | developer exception page / `StackTrace` only in Development (§8.7) |
| File paths | user-supplied path → `Path.GetFullPath` + `StartsWith(root + Path.DirectorySeparatorChar)` |
| Uploads | size-limited, extension allow-listed (`ValidateImageExtensions` / `ValidateMaxFileSize`), random names, never under `wwwroot` |
| Media serving | private storage folders are declared once (`MediaStorageExtensions.PrivateFolders`) and passed to `UseCorePublicMedia`; never a second list; responses via `WriteStoredFileAsync` |
| SSRF | any URL from user input → scheme + host allow-list, block private/link-local/metadata IPs, no auto-redirect |
| Secrets | none in `appsettings*.json` or fixtures — environment variables / host secret store; a leaked key is rotated, not just removed |
| Logging | never log tokens, passwords, OTPs, full request bodies, or PII |
| DataProtection | keys persisted and protected in multi-instance deployments |

---

## 11. Dependencies & Licences

A package is added only if the plan names it with an exact version. Core-first (§ header) beats any package.

| Package | Status (2026-09) | Rule |
|---------|------------------|------|
| FluentAssertions ≥ 8 | commercial (Xceed) | do not bump past the repo's licensed/pinned major; alternative `AwesomeAssertions` |
| MediatR ≥ 13 | dual RPL-1.5 / commercial, licence key | repo is on 14 — key lives in configuration, never committed; major bumps only with a decision record |
| AutoMapper ≥ 15 | dual RPL-1.5 / commercial | do not add — `[Noun]ResultGenerator` (§5.8) is the mapper |
| MassTransit ≥ 9 | commercial | do not add — a `Core.Queues` `SweepWorker<TOptions>` sweep (+ Hangfire for scheduled jobs) |
| Moq | avoid (SponsorLink) | NSubstitute |
| Swashbuckle.AspNetCore | dropped from .NET 10 templates | `Microsoft.AspNetCore.OpenApi` + Scalar (already wired) |
| Newtonsoft.Json | legacy | System.Text.Json only (§8.6) |
| Microsoft.EntityFrameworkCore.InMemory | wrong relational behaviour | never in any test project |

- Verify before adding: `dotnet package search <Id> --exact-match`; licence + publish date on `https://www.nuget.org/packages/<Id>/<version>`.
- If the repo uses Central Package Management (`Directory.Packages.props`), a `.csproj` `PackageReference` has no `Version` (Elmanhg core included: no `Version` in any csproj; HTTP types via `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, never the deprecated 2.x `Microsoft.AspNetCore.*` packages); if lock files are on, commit the updated `packages.lock.json` (`dotnet restore --force-evaluate`).

---

## 12. LLM Mistakes to Avoid (.NET 10)

| ❌ Wrong | ✅ Right |
|----------|---------|
| `Startup.cs`, `WebHost.CreateDefaultBuilder`, `IWebHostBuilder` hosting | minimal hosting in `Program.cs` (§7.4) |
| `services.AddSwaggerGen()` / `app.UseSwagger()` | existing OpenAPI + Scalar wiring |
| `Microsoft.OpenApi` v1 types (`OpenApiString`) in transformers | v2 — `JsonNode` |
| `app.UseProblemDetails()` (Hellang) | `CoreExceptionMiddleware` already formats errors |
| `services.AddFluentValidation()` (deprecated auto-validation) | `AddValidatorsFromAssembly(...)` + `Core.CQRS` `ValidationBehaviour` |
| `services.AddMediatR(typeof(Program))` (v11 signature) | `AddMediatR(configuration => configuration.RegisterServicesFromAssembly(...))` (§5.11) |
| `DateTime.Now` / `DateTime.UtcNow` | `DateTimeOffset.UtcNow` (§1) |
| `ToListAsync()` then filter in memory | predicate before materialising (§6.6) |
| `catch (Exception) { }` / `catch { return null; }` | throw core exceptions; middleware maps them |
| invented package ids, invented `Core.*` APIs | verify in `api/core-libraries` / §11 before use |
| xUnit v2 patterns on v3 (`Task InitializeAsync`, `Xunit.Abstractions`) | match the repo's xUnit major |
| `Task.WhenAll` over one `DbContext` | sequential awaits (§1 Async) |
| sync-over-async in constructors, filters, DI factories | async all the way |
| editing/skipping a failing test | fix the code, or `BLOCKED` (§8.11) |
