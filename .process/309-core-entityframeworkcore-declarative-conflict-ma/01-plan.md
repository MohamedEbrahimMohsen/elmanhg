# Plan — Core.EntityFrameworkCore: declarative conflict map and row-version convention (E21.S6, story 309)

## Goal
Today `AppDbContext.SaveChangesAsync` has 21 hand-written `catch` blocks that turn `DbUpdateConcurrencyException`s and PostgreSQL unique violations into `ConflictCoreException(code)`. It also has 11 copy-pasted `.IsRowVersion()` lines. After this story, core (`Core.EntityFrameworkCore` + `Core.DDD`) provides an app-agnostic `ConflictMap` (with a translation helper) and an `IVersioned` marker plus a `ModelBuilder.ApplyRowVersionConvention()` extension. The app declares its conflicts once in a fluent map, supplies the PostgreSQL unique-violation detector, and opts entities into `xmin` row versioning by implementing `IVersioned`. No product behaviour changes: every 409 code is the same, the EF model and its snapshot are identical, and core never references Npgsql.

## Scope
**In:**
- Core: `ConflictMap`, `UniqueViolation`, `IVersioned`, `RowVersionConvention.ApplyRowVersionConvention()`.
- App: the `AppDbContext.Conflicts` map plus its Postgres detector, a single `catch` in `SaveChangesAsync`, 11 entities implementing `IVersioned`, 11 `.IsRowVersion()` lines replaced by one convention call.
- Core unit tests, an app table-driven test covering every mapped conflict, and a model test for the row-version set.
- Docs: `docs/constitution.md` and `.claude/skills/dotnet-feature/SKILL.md`.

**Out:**
- Any change to `CoreDbContext` (dev decision 2026-10-06: no new hook inside it).
- `Core.Notifications` stays.
- Soft-delete `HasQueryFilter` lines (owned by the parallel story 306).
- `RetrySchedule` owned type (owned by the parallel story 307).
- New error codes, any change to existing 409 codes, migrations.

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | How does core detect a unique violation without Npgsql? | `ConflictMap` takes a constructor delegate `Func<DbUpdateException, UniqueViolation?> detectUniqueViolation`. The app passes a static method that matches `PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }` and returns `new UniqueViolation(ConstraintName, TableName)`. | The story says "behind a provider hook; the app supplies the SqlState check". A delegate is the smallest hook. No interface or DI registration is needed because the map is static. |
| 2 | One catch block uses `TableName: nameof(QuestionImportBatches)`, not a constraint name. | Add a third method, `MapUniqueTable(string tableName, string errorCode)`. Constraint and table rules share one ordered list. | Today's behaviour matches any unique violation on that table, including the primary key `PK_QuestionImportBatches`. Mapping by constraint names alone could miss one and change behaviour. |
| 3 | "or" patterns (`Session or QuestionMastery`, `PaymobTransactionIndex or PaymentRefundTransactionIndex`, `SubjectDefaultBlueprintIndex or UnitBlueprintIndex`, `EssayGrade or MathStepGrade`). | Call `Map…` once per alternative, with the same code, in the same position. | No new API. Same semantics. |
| 4 | Precedence | `TryTranslate` checks the concurrency rules first, in registration order, and only when the exception is a `DbUpdateConcurrencyException`. The first rule where any `exception.Entries[i].Entity` is an instance of `TEntity` (`Type.IsInstanceOfType`, matching C# `is`, derived types included) wins. Then it calls the detector once. If it returns non-null, the unique rules are checked in registration order and the first match wins. Name comparison is `string.Equals(..., StringComparison.Ordinal)`. | Matches the existing catch order exactly. A `DbUpdateConcurrencyException` is a `DbUpdateException`, so today an unmatched one already falls through to the unique catches. |
| 5 | Translation helper shape | `public bool TryTranslate(DbUpdateException exception, [NotNullWhen(true)] out ConflictCoreException? conflict)`. It builds `new ConflictCoreException(errorCode, innerException: exception)`, the same constructor call as today: no message, no context. | Fits a `catch (DbUpdateException exception) when (Conflicts.TryTranslate(exception, out var conflict)) { throw conflict; }` filter. An exception that is not mapped propagates unchanged, as it does today. |
| 6 | Where the map lives | New partial `AppDbContext.Conflicts.cs`: `public static ConflictMap Conflicts { get; }`, built once. | Keeps the `AppDbContext.cs` edit to the catch block only, so there are fewer merge conflicts with stories 306 and 307. It is public so the table-driven test can exercise it; `AppDbContext` already exposes public index constants for the same reason. No `InternalsVisibleTo` exists in the repo, and this story will not add one. |
| 7 | Where `IVersioned` lives | `Core.DDD/Entities/IVersioned.cs`, namespace `Core.DDD.Entities`, next to `ISoftDeletable`/`IAuditedEntity`. | `Elmanhg.Domain` references only `Core.DDD`, not `Core.EntityFrameworkCore`. |
| 8 | `IVersioned` member | `uint Version { get; }`, get-only. | Entities declare `public uint Version { get; private set; }`. A get-only interface member keeps the private setter. |
| 9 | Convention filter | Root entity types only (`BaseType is null`), not owned (`!IsOwned()`), not shared-type (`!HasSharedClrType`), `typeof(IVersioned).IsAssignableFrom(ClrType)`. Collect them into a list first, then call `modelBuilder.Entity(clrType).Property(nameof(IVersioned.Version)).IsRowVersion()` for each. | Calling `modelBuilder.Entity(ownedType)` would turn an owned type into an entity, which changes the model; owned `LocalizedText`, and `RetrySchedule` from story 307, exist. Derived types inherit the root's property. Materialising the list avoids mutating the model while enumerating it. |
| 10 | Where `ApplyRowVersionConvention()` is called | In `AppDbContext.OnModelCreating`, immediately after `ConfigureSlaCalendars(modelBuilder);` and before `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);`. | All entity types are in the model by then. The line sits outside the soft-delete filter method that story 306 owns. |
| 11 | Does the model change? | No. `IsRowVersion()` on the same `uint Version` property yields the same `xmin`/`xid`, `IsConcurrencyToken`, `ValueGenerated.OnAddOrUpdate` annotations. This is verified by the existing test `AppDbContextTests.Model_Current_MatchesLatestMigrationSnapshot` and by `dotnet ef migrations has-pending-model-changes`. | Story requirement. No migration is added. |
| 12 | How to build a `DbUpdateConcurrencyException` with entries in tests | EF 10 only exposes `DbUpdateConcurrencyException(string, IReadOnlyList<IUpdateEntry>)`. The test helper substitutes `IUpdateEntry`, a public provider-facing interface, with NSubstitute and returns a real `context.Entry(entity)` from `ToEntityEntry()`. | The only public route that avoids EF internal APIs (EF1001 is an error under `TreatWarningsAsErrors`). `IUpdateEntry` is a framework port, so a substitute is allowed. |
| 13 | DbContext for core tests | A probe `DbContext` configured with `UseNpgsql("Host=localhost;Database=core-persistence-probe")`. It never opens a connection: only `Model` and `Entry()` are used. | The convention forbids InMemory/SQLite. Npgsql is the real provider, so the `xmin` mapping can be asserted. The test project already gets Npgsql through Infrastructure. |
| 14 | App concurrency rows need instances of 11 domain types | Build them with the existing builders and factories (table under Test plan), with no reflection. | Testing convention: builders go through domain factories. |
| 15 | Guards on `Map…` arguments | None. Nullable annotations only. | A wrong name simply never matches. No new exception or error code (hard rule: no new abstractions). |
| 16 | Morabh reuse | None. Morabh has no `DbUpdateConcurrencyException`, `IsRowVersion` or unique-violation handling; this was checked by grepping `D:\Personal\Projects\Projects\Morabh\repos\apis`. | Every new file is "new, no Morabh equivalent". |
| 17 | Comments on the two special rules | Keep the two existing WHY comments, one above `.MapUniqueTable(nameof(QuestionImportBatches), …)` and one above `.MapUniqueConstraint(TeacherThreadTrainingTriggerIndex, …)`, worded as today. Do not write a story or issue number in any comment. | Hidden invariants. Skill §1 "No Comments" allows WHY comments. |
| 18 | Count | The story says 22 catch blocks; the file actually has 21 (9 concurrency + 12 unique). The map has 24 rule registrations: 11 concurrency + 13 unique, because of the "or" splits. | Plan against what is there. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | (a) Replace the whole body of `SaveChangesAsync`, lines 103–195, with the single try/catch under API surface. (b) Delete the 7 lines `builder.Property(x => x.Version).IsRowVersion();` in `ConfigureSessions`, `ConfigureQuestionMastery`, `ConfigureSubscriptions` (Subscription), `ConfigureSubscriptions` (Payment), `ConfigureTeacherThreads`, `ConfigureAvatar` (AvatarConversation) and `ConfigureEssayGrades`. (c) Insert `modelBuilder.ApplyRowVersionConvention();` between `ConfigureSlaCalendars(modelBuilder);` and `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);`. (d) Remove usings that become unused: `using Core.Errors;`, `using Elmanhg.Application.Exceptions;`, `using Npgsql;`, `using DomainErrorCodes = …;`. Keep a using only if the build still needs it. Touch nothing else, in particular not `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.MathStepGrading.cs` | Delete line 36 `builder.Property(x => x.Version).IsRowVersion();` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.RuntimeSettings.cs` | Delete line 21 `builder.Property(x => x.Version).IsRowVersion();` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.SlaCalendars.cs` | Delete line 15 `builder.Property(x => x.Version).IsRowVersion();` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.TrainingExports.cs` | Delete line 24 `builder.Property(x => x.Version).IsRowVersion();` |
| `api/Elmanhg.Domain/Sessions/Session.cs` | `public partial class Session : AuditEntity, IVersioned` |
| `api/Elmanhg.Domain/Mastery/QuestionMastery.cs` | `public class QuestionMastery : AuditEntity, IVersioned` |
| `api/Elmanhg.Domain/Subscriptions/Subscription.cs` | `public partial class Subscription : AuditEntity, IAuditedEntity, IVersioned` |
| `api/Elmanhg.Domain/Subscriptions/Payment.cs` | `public partial class Payment : AuditEntity, IAuditedEntity, IVersioned` |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.cs` | `public partial class TeacherThread : AuditEntity, IVersioned` |
| `api/Elmanhg.Domain/Avatar/AvatarConversation.cs` | `public class AvatarConversation : AuditEntity, IAuditedEntity, IVersioned` |
| `api/Elmanhg.Domain/TrainingExports/TrainingExport.cs` | `public partial class TrainingExport : AuditEntity, IAuditedEntity, IVersioned` |
| `api/Elmanhg.Domain/EssayGrading/EssayGrade.cs` | `public partial class EssayGrade : AuditEntity, IVersioned` |
| `api/Elmanhg.Domain/MathStepGrading/MathStepGrade.cs` | `public partial class MathStepGrade : AuditEntity, IVersioned` |
| `api/Elmanhg.Domain/RuntimeSettings/RuntimeSettingOverride.cs` | `public class RuntimeSettingOverride : AuditEntity, IAuditedEntity, IVersioned` |
| `api/Elmanhg.Domain/SlaCalendars/ExamPeriod.cs` | `public class ExamPeriod : AuditEntity, IAuditedEntity, IVersioned` |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Add one test method (Test plan #19). Existing methods unchanged. |
| `docs/constitution.md` | Line 3, stack list: replace `` `Core.EntityFrameworkCore`, `` with `` `Core.EntityFrameworkCore` (including the opt-in `ConflictMap` that turns concurrency and unique-violation failures into `409` codes through an app-supplied unique-violation detector, and the `IVersioned` + `ApplyRowVersionConvention()` `xmin` row-version convention), ``. |
| `.claude/skills/dotnet-feature/SKILL.md` | See "Docs edits" below. |

All 11 domain files already have `using Core.DDD.Entities;`, so no using changes are needed there. Leave the `uint Version` declarations exactly as they are.

### Docs edits (SKILL.md)
| Location | Old | New |
|---|---|---|
| Delta 1, line 14 | `Concurrency tokens use PostgreSQL \`xmin\` (\`.IsRowVersion()\` on a \`uint Version\` property mapped to \`xmin\`).` | `Concurrency tokens use PostgreSQL \`xmin\`: the entity implements \`IVersioned\` (\`Core.DDD\`, \`public uint Version { get; private set; }\`) and \`AppDbContext.OnModelCreating\` calls \`modelBuilder.ApplyRowVersionConvention()\` (\`Core.EntityFrameworkCore\`), which maps every \`IVersioned\` root entity's \`Version\` to \`xmin\`. Never write \`.IsRowVersion()\` per entity.` |
| §6.3 table, line 673 | `` \| `IsRowVersion()` on `byte[] RowVersion` \| aggregates edited concurrently by humans (§6.8) \| `` | `` \| implement `IVersioned` (`uint Version`), no per-entity config \| aggregates written concurrently (§6.8) \| `` |
| §6.8, lines 741–747 | The bullet "Concurrency token on human-edited aggregates", its `byte[] RowVersion` code block, and the bullet "`DbUpdateConcurrencyException` → 409 is mapped in `CoreExceptionMiddleware`…" | Bullet: "Concurrency token on concurrently written aggregates: `public sealed class Order : AuditEntity, IVersioned` with `public uint Version { get; private set; }`; `ApplyRowVersionConvention()` maps it to `xmin`." Bullet: "Conflicts → 409 are declared once in `AppDbContext.Conflicts` (`AppDbContext.Conflicts.cs`), a `Core.EntityFrameworkCore.Conflicts.ConflictMap`: `.MapConcurrency<Order>(ErrorCodes.OrderModifiedConcurrently)` for a stale row version, `.MapUniqueConstraint(OrderNumberIndex, ErrorCodes.OrderNumberTaken)` (or `.MapUniqueTable(...)`) for a unique index race. `SaveChangesAsync` has a single `catch (DbUpdateException) when (Conflicts.TryTranslate(...))`. Registration order is precedence. Never a handler `try`/`catch`, never a new catch block; verify the existing mapping before adding an error code." |

No other doc diverges. Feature docs that say a race is "mapped in `AppDbContext.SaveChangesAsync`" (ask-teacher.md, exam-blueprints.md, training-data.md) stay true: the mapping still runs there, through the map. `docs/PRD.md`, the design docs and `docs/backlog.json` are not affected.

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/core-libraries/Core.DDD/Entities/IVersioned.cs` | interface | `namespace Core.DDD.Entities; public interface IVersioned { uint Version { get; } }`. New, no Morabh equivalent. |
| 2 | `api/core-libraries/Core.EntityFrameworkCore/Conflicts/UniqueViolation.cs` | record | `namespace Core.EntityFrameworkCore.Conflicts; public sealed record UniqueViolation(string? ConstraintName, string? TableName);` New. |
| 3 | `api/core-libraries/Core.EntityFrameworkCore/Conflicts/ConflictMap.cs` | class | See ConflictMap contract below. New. |
| 4 | `api/core-libraries/Core.EntityFrameworkCore/Context/RowVersionConvention.cs` | static class | `namespace Core.EntityFrameworkCore.Context; public static class RowVersionConvention { public static void ApplyRowVersionConvention(this ModelBuilder modelBuilder) }`. Body as in Decision 9: a LINQ chain with one operator per line, `.ToList()`, then a `foreach` calling `modelBuilder.Entity(clrType).Property(nameof(IVersioned.Version)).IsRowVersion();`. New. |
| 5 | `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.Conflicts.cs` | partial class | See AppDbContext.Conflicts contract below. |
| 6 | `api/Elmanhg.Tests/Core/Persistence/ConcurrencyFailures.cs` | test helper | `namespace Elmanhg.Tests.Core.Persistence; public static class ConcurrencyFailures { public static DbUpdateConcurrencyException For(DbContext context, params object[] entities) }`. For each entity it creates `Substitute.For<IUpdateEntry>()` with `EntityState` returning `EntityState.Modified`, `SharedIdentityEntry` returning `(IUpdateEntry?)null`, and `ToEntityEntry()` returning `context.Entry(entity)`. It returns `new DbUpdateConcurrencyException("Stale row version.", entries)`. |
| 7 | `api/Elmanhg.Tests/Core/Persistence/CorePersistenceProbes.cs` | test probes | Namespace `Elmanhg.Tests.Core.Persistence`. Classes (plain, not `Entity`): `public class ProbeAlpha { public Guid Id { get; set; } = Guid.NewGuid(); }`, `public sealed class ProbeAlphaDerived : ProbeAlpha;`, `public sealed class ProbeBeta { public Guid Id { get; set; } = Guid.NewGuid(); }`, `public class VersionedProbe : IVersioned { public Guid Id { get; set; } = Guid.NewGuid(); public uint Version { get; private set; } public VersionedProbeDetail Detail { get; set; } = new(); }`, `public sealed class VersionedProbeDerived : VersionedProbe;`, `public sealed class VersionedProbeDetail : IVersioned { public uint Version { get; private set; } public string Note { get; set; } = string.Empty; }`, `public sealed class UnversionedProbe { public Guid Id { get; set; } = Guid.NewGuid(); public uint Version { get; private set; } }`. Context: `public sealed class CorePersistenceProbeDbContext() : DbContext(new DbContextOptionsBuilder<CorePersistenceProbeDbContext>().UseNpgsql("Host=localhost;Database=core-persistence-probe").Options)` with `DbSet<ProbeAlpha> Alphas`, `DbSet<ProbeBeta> Betas`, `DbSet<VersionedProbe> VersionedProbes`, `DbSet<UnversionedProbe> UnversionedProbes`. `OnModelCreating`: `modelBuilder.Entity<ProbeAlphaDerived>(); modelBuilder.Entity<VersionedProbeDerived>(); modelBuilder.Entity<VersionedProbe>().OwnsOne(x => x.Detail); modelBuilder.ApplyRowVersionConvention();` |
| 8 | `api/Elmanhg.Tests/Core/Persistence/ConflictMapTests.cs` | tests | Test plan #1–#11. |
| 9 | `api/Elmanhg.Tests/Core/Persistence/RowVersionConventionTests.cs` | tests | Test plan #12–#15. |
| 10 | `api/Elmanhg.Tests/Integration/Persistence/AppDbContextConflictMapTests.cs` | tests | Test plan #16–#18. |

No `.csproj` changes and no new packages. `Core.EntityFrameworkCore` already references `Core.DDD`, which references `Core.Errors` (`ConflictCoreException`) and `Microsoft.EntityFrameworkCore` (`DbUpdateException`). The test project already has NSubstitute and gets Npgsql transitively.

### ConflictMap contract (file 3)
```csharp
namespace Core.EntityFrameworkCore.Conflicts;

public sealed class ConflictMap(Func<DbUpdateException, UniqueViolation?> detectUniqueViolation)
{
    private readonly List<(Type EntityType, string ErrorCode)> _concurrencyRules = [];
    private readonly List<(Func<UniqueViolation, bool> Matches, string ErrorCode)> _uniqueRules = [];

    public ConflictMap MapConcurrency<TEntity>(string errorCode) where TEntity : class;      // adds (typeof(TEntity), errorCode); returns this
    public ConflictMap MapUniqueConstraint(string constraintName, string errorCode);         // adds (v => string.Equals(v.ConstraintName, constraintName, StringComparison.Ordinal), errorCode); returns this
    public ConflictMap MapUniqueTable(string tableName, string errorCode);                   // adds (v => string.Equals(v.TableName, tableName, StringComparison.Ordinal), errorCode); returns this
    public bool TryTranslate(DbUpdateException exception, [NotNullWhen(true)] out ConflictCoreException? conflict);
}
```
`TryTranslate`, ordered steps:
1. `var errorCode = FindConcurrencyCode(exception) ?? FindUniqueCode(exception);`, where:
   - `FindConcurrencyCode`: if `exception is not DbUpdateConcurrencyException`, return `null`. Otherwise `var entities = exception.Entries.Select(x => x.Entity).ToList();` and return the `ErrorCode` of the first `_concurrencyRules` rule where `entities.Any(rule.EntityType.IsInstanceOfType)`, or `null`.
   - `FindUniqueCode`: `var violation = detectUniqueViolation(exception);` If it is null, return `null`. Otherwise return the `ErrorCode` of the first `_uniqueRules` rule whose `Matches(violation)` is true, or `null`.
2. `conflict = errorCode is null ? null : new ConflictCoreException(errorCode, innerException: exception);`
3. `return conflict is not null;`

Both finders are `private` methods. The detector is called only when no concurrency rule matched. The file stays under 60 lines.

### AppDbContext.Conflicts contract (file 5)
```csharp
namespace Elmanhg.Infrastructure.Data.Context;

public partial class AppDbContext
{
    public static ConflictMap Conflicts { get; } = new ConflictMap(DetectUniqueViolation)
        .MapConcurrency<Session>(ErrorCodes.SessionModifiedConcurrently)
        .MapConcurrency<QuestionMastery>(ErrorCodes.SessionModifiedConcurrently)
        .MapConcurrency<Payment>(ErrorCodes.PaymentModifiedConcurrently)
        .MapConcurrency<Subscription>(ErrorCodes.SubscriptionModifiedConcurrently)
        .MapConcurrency<TeacherThread>(ErrorCodes.TeacherThreadModifiedConcurrently)
        .MapConcurrency<AvatarConversation>(ErrorCodes.AvatarConversationModifiedConcurrently)
        .MapConcurrency<TrainingExport>(ErrorCodes.TrainingExportModifiedConcurrently)
        .MapConcurrency<EssayGrade>(ErrorCodes.GradeModifiedConcurrently)
        .MapConcurrency<MathStepGrade>(ErrorCodes.GradeModifiedConcurrently)
        .MapConcurrency<RuntimeSettingOverride>(ErrorCodes.RuntimeSettingModifiedConcurrently)
        .MapConcurrency<ExamPeriod>(ErrorCodes.ExamPeriodModifiedConcurrently)
        .MapUniqueConstraint(RuntimeSettingKeyIndex, ErrorCodes.RuntimeSettingModifiedConcurrently)
        .MapUniqueConstraint(PaymobTransactionIndex, ErrorCodes.PaymentTransactionAlreadyRecorded)
        .MapUniqueConstraint(PaymentRefundTransactionIndex, ErrorCodes.PaymentTransactionAlreadyRecorded)
        // (keep the existing WHY comment: two confirms of one import batch id … resolves it)
        .MapUniqueTable(nameof(QuestionImportBatches), ErrorCodes.QuestionImportBatchConflict)
        .MapUniqueConstraint(InProgressSessionIndex, ErrorCodes.SessionAlreadyInProgress)
        .MapUniqueConstraint(AttemptPerQuestionIndex, DomainErrorCodes.SessionQuestionAlreadyAnswered)
        .MapUniqueConstraint(QuestionMasteryPerStudentIndex, ErrorCodes.SessionModifiedConcurrently)
        .MapUniqueConstraint(OneOpenExamIndex, ErrorCodes.ExamAlreadyInProgress)
        .MapUniqueConstraint(LessonOpeningPerStudentIndex, ErrorCodes.LessonAlreadyOpened)
        .MapUniqueConstraint(SubjectDefaultBlueprintIndex, ErrorCodes.ExamBlueprintModifiedConcurrently)
        .MapUniqueConstraint(UnitBlueprintIndex, ErrorCodes.ExamBlueprintModifiedConcurrently)
        .MapUniqueConstraint(AvatarMessagePositionIndex, ErrorCodes.AvatarConversationModifiedConcurrently)
        // (keep the existing WHY comment: EF inserts the training row before the stale thread UPDATE …)
        .MapUniqueConstraint(TeacherThreadTrainingTriggerIndex, ErrorCodes.TeacherThreadModifiedConcurrently)
        .MapUniqueConstraint(EssayGradeTrainingTriggerIndex, ErrorCodes.GradeModifiedConcurrently);

    private static UniqueViolation? DetectUniqueViolation(DbUpdateException exception) => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException ? new UniqueViolation(postgresException.ConstraintName, postgresException.TableName) : null;
}
```
Usings: `Core.EntityFrameworkCore.Conflicts`, `Elmanhg.Application.Exceptions`, `Elmanhg.Domain.Avatar`, `.EssayGrading`, `.Mastery`, `.MathStepGrading`, `.RuntimeSettings`, `.Sessions`, `.SlaCalendars`, `.Subscriptions`, `.TeacherThreads`, `.TrainingExports`, `Microsoft.EntityFrameworkCore`, `Npgsql`, and `DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`. The two `// (keep …)` placeholders mean: copy the two existing comments verbatim from the current catch blocks at lines 155 and 188.

## Error codes
None added or changed. The existing codes keep their values and resources: `SESSION_MODIFIED_CONCURRENTLY`, `PAYMENT_MODIFIED_CONCURRENTLY`, `SUBSCRIPTION_MODIFIED_CONCURRENTLY`, `TEACHER_THREAD_MODIFIED_CONCURRENTLY`, `AVATAR_CONVERSATION_MODIFIED_CONCURRENTLY`, `TRAINING_EXPORT_MODIFIED_CONCURRENTLY`, `GRADE_MODIFIED_CONCURRENTLY`, `RUNTIME_SETTING_MODIFIED_CONCURRENTLY`, `EXAM_PERIOD_MODIFIED_CONCURRENTLY`, `PAYMENT_TRANSACTION_ALREADY_RECORDED`, `QUESTION_IMPORT_BATCH_CONFLICT`, `SESSION_ALREADY_IN_PROGRESS`, `SESSION_QUESTION_ALREADY_ANSWERED`, `EXAM_ALREADY_IN_PROGRESS`, `LESSON_ALREADY_OPENED`, `EXAM_BLUEPRINT_MODIFIED_CONCURRENTLY`. All are thrown as `ConflictCoreException` and return HTTP 409.

## Domain behaviour
No new domain methods. Each of the 11 entities only gains `IVersioned` in its base list. The existing `public uint Version { get; private set; }` satisfies the interface. No state transitions or `UpdationDate` changes.

## API surface
No HTTP surface change. The internal contract changes as follows:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
{
    try
    {
        return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (Conflicts.TryTranslate(exception, out var conflict))
    {
        throw conflict;
    }
}
```
A `DbUpdateException` that is not mapped propagates unchanged and still surfaces as 500, the same as today.

## Test plan
All tests use FluentAssertions (the repo's pinned library), the `Method_Scenario_Expected` naming, and `TestContext.Current.CancellationToken` where async. Every translated-conflict assertion checks `conflict!.ErrorCode`, `conflict.InnerException` being the same instance as the input exception (`BeSameAs`), and `conflict.StatusCode == 409`, all from the returned `ConflictCoreException`.

**Core: `Elmanhg.Tests/Core/Persistence/ConflictMapTests.cs`** (`public sealed class ConflictMapTests`; uses `CorePersistenceProbeDbContext` and `ConcurrencyFailures.For`; error codes are local `const string`s such as `"ALPHA_STALE"`. The detector is a lambda; tests that need it to report a violation return a fixed `UniqueViolation`.)
| # | Test method | Asserts |
|---|-------------|---------|
| 1 | `TryTranslate_ConcurrencyOnMappedEntity_ReturnsConflictWithMappedCode` | `true`, code `ALPHA_STALE`, inner is the same exception, status 409 |
| 2 | `TryTranslate_ConcurrencyOnDerivedEntity_UsesBaseTypeMapping` | `ProbeAlphaDerived` entry with only `MapConcurrency<ProbeAlpha>`: `true`, `ALPHA_STALE` |
| 3 | `TryTranslate_ConcurrencyOnSeveralMappedEntities_UsesFirstRegisteredMapping` | Entries `[beta, alpha]`, map Alpha then Beta: code `ALPHA_STALE` |
| 4 | `TryTranslate_ConcurrencyOnUnmappedEntity_ReturnsFalse` | Only Beta mapped, entry Alpha, detector returns null: `false`, `conflict` null |
| 5 | `TryTranslate_UnmappedConcurrencyWithMappedUniqueViolation_ReturnsUniqueCode` | Concurrency exception on an unmapped entity, detector returns `("IX_Probe", "Probes")` mapped by constraint: unique code |
| 6 | `TryTranslate_UniqueViolationOnMappedConstraint_ReturnsConflictWithMappedCode` | `new DbUpdateException("duplicate")`, detector `("IX_Probe", "Probes")`: `true`, code, inner same, 409 |
| 7 | `TryTranslate_UniqueViolationOnMappedTable_ReturnsConflictWithMappedCode` | `MapUniqueTable("Probes", …)`, detector `("PK_Probes", "Probes")`: `true`, table code |
| 8 | `TryTranslate_UniqueViolationOnUnmappedConstraint_ReturnsFalse` | Detector `("IX_Other", "Others")`: `false`, null |
| 9 | `TryTranslate_NoUniqueViolationDetected_ReturnsFalse` | Detector returns null with constraint rules registered: `false`, null |
| 10 | `TryTranslate_ConstraintAndTableBothMatch_UsesFirstRegisteredMapping` | Register the table rule first, then the constraint rule for the same violation: the table code |
| 11 | `TryTranslate_ConstraintNameDiffersInCase_ReturnsFalse` | Mapped `"IX_Probe"`, detector `("ix_probe", null)`: `false` (ordinal) |

**Core: `Elmanhg.Tests/Core/Persistence/RowVersionConventionTests.cs`** (`public sealed class RowVersionConventionTests`; `using var context = new CorePersistenceProbeDbContext();` and read `context.Model`)
| # | Test method | Asserts |
|---|-------------|---------|
| 12 | `ApplyRowVersionConvention_VersionedEntity_MapsVersionToXminRowVersion` | The `VersionedProbe.Version` property: `IsConcurrencyToken` true, `ValueGenerated` `OnAddOrUpdate`, `GetColumnName()` `"xmin"`, `GetColumnType()` `"xid"` |
| 13 | `ApplyRowVersionConvention_UnversionedEntityWithVersionProperty_LeavesItPlain` | `UnversionedProbe.Version`: `IsConcurrencyToken` false, column name `"Version"` |
| 14 | `ApplyRowVersionConvention_OwnedVersionedType_LeavesItOwnedAndPlain` | `FindEntityType(typeof(VersionedProbeDetail))!.IsOwned()` true; its `Version` `IsConcurrencyToken` false |
| 15 | `ApplyRowVersionConvention_DerivedOfVersionedEntity_InheritsRowVersion` | `FindEntityType(typeof(VersionedProbeDerived))!.FindProperty("Version")!` has `IsConcurrencyToken` true and `DeclaringType.ClrType == typeof(VersionedProbe)` |

**App: `Elmanhg.Tests/Integration/Persistence/AppDbContextConflictMapTests.cs`** (`public sealed class AppDbContextConflictMapTests(ApiFactory factory)`, shaped like `GradeConcurrencyTests`. It resolves `AppDbContext` from `factory.Services.CreateScope()` and calls `AppDbContext.Conflicts.TryTranslate`. Unique rows use the helper `private static DbUpdateException UniqueViolationOn(string? constraintName, string? tableName, string sqlState = PostgresErrorCodes.UniqueViolation) => new("duplicate", new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", sqlState, tableName: tableName, constraintName: constraintName));`. If the named-argument form does not compile, pass the 18 positional arguments of the long `PostgresException` constructor.)
| # | Test method | Rows | Asserts |
|---|-------------|------|---------|
| 16 | `[Theory] TryTranslate_ConcurrencyOnVersionedEntity_ReturnsExistingCode(string entityName, string expectedCode)` | 11 `InlineData` rows: `nameof(Session)`→`ErrorCodes.SessionModifiedConcurrently`, `nameof(QuestionMastery)`→`SessionModifiedConcurrently`, `nameof(Payment)`→`PaymentModifiedConcurrently`, `nameof(Subscription)`→`SubscriptionModifiedConcurrently`, `nameof(TeacherThread)`→`TeacherThreadModifiedConcurrently`, `nameof(AvatarConversation)`→`AvatarConversationModifiedConcurrently`, `nameof(TrainingExport)`→`TrainingExportModifiedConcurrently`, `nameof(EssayGrade)`→`GradeModifiedConcurrently`, `nameof(MathStepGrade)`→`GradeModifiedConcurrently`, `nameof(RuntimeSettingOverride)`→`RuntimeSettingModifiedConcurrently`, `nameof(ExamPeriod)`→`ExamPeriodModifiedConcurrently`. A private switch-expression `CreateVersionedEntity(string entityName)` builds them as follows: `new SessionBuilder().Build()`; `QuestionMastery.Start(Guid.NewGuid(), Guid.NewGuid(), new MasteryAttempt(Guid.NewGuid(), 1m, Now))`; `Payment.Create(Guid.NewGuid(), SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP"))`; `new SubscriptionBuilder().Build()`; `new TeacherThreadBuilder().Build()`; `new AvatarConversationBuilder().Build()`; `new TrainingExportBuilder().Build()`; `new EssayGradeBuilder().Build()`; `new MathStepGradeBuilder().Build()`; `RuntimeSettingOverride.Create("askTeacher.replySlaHours", "30", Guid.NewGuid())`; `ExamPeriod.Create("Finals", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), Guid.NewGuid())`; `_ => throw new ArgumentOutOfRangeException(nameof(entityName))`. `Now` is a fixed `DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)`. The exception is `ConcurrencyFailures.For(context, entity)`. | `true`, `ErrorCode == expectedCode`, inner same, 409 |
| 17 | `[Theory] TryTranslate_UniqueViolation_ReturnsExistingCode(string? constraintName, string? tableName, string expectedCode)` | 14 rows: `RuntimeSettingKeyIndex`→`RuntimeSettingModifiedConcurrently`; `PaymobTransactionIndex`→`PaymentTransactionAlreadyRecorded`; `PaymentRefundTransactionIndex`→`PaymentTransactionAlreadyRecorded`; (`"PK_QuestionImportBatches"`, `"QuestionImportBatches"`)→`QuestionImportBatchConflict`; `InProgressSessionIndex`→`SessionAlreadyInProgress`; `AttemptPerQuestionIndex`→`DomainErrorCodes.SessionQuestionAlreadyAnswered`; `QuestionMasteryPerStudentIndex`→`SessionModifiedConcurrently`; `OneOpenExamIndex`→`ExamAlreadyInProgress`; `LessonOpeningPerStudentIndex`→`LessonAlreadyOpened`; `SubjectDefaultBlueprintIndex`→`ExamBlueprintModifiedConcurrently`; `UnitBlueprintIndex`→`ExamBlueprintModifiedConcurrently`; `AvatarMessagePositionIndex`→`AvatarConversationModifiedConcurrently`; `TeacherThreadTrainingTriggerIndex`→`TeacherThreadModifiedConcurrently`; `EssayGradeTrainingTriggerIndex`→`GradeModifiedConcurrently`. Constraint rows pass `tableName: null`; index names are `AppDbContext.*Index` constants. | `true`, `ErrorCode == expectedCode`, inner same, 409 |
| 18 | `[Theory] TryTranslate_UnmappedOrNonUniqueFailure_ReturnsFalse(string constraintName, string sqlState)` | (`AppDbContext.UserActivityDayIndex`, `PostgresErrorCodes.UniqueViolation`); (`AppDbContext.InProgressSessionIndex`, `PostgresErrorCodes.ForeignKeyViolation`) | `false`, `conflict` null |

**App: `Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs`** (existing class; add one method)
| # | Test method | Asserts |
|---|-------------|---------|
| 19 | `Model_VersionedEntities_MapVersionToXminRowVersion` | The set of entity CLR types whose `Version` property `IsConcurrencyToken` equals exactly the 11 types above (`BeEquivalentTo`), and also equals `context.Model.GetEntityTypes().Where(x => !x.IsOwned() && typeof(IVersioned).IsAssignableFrom(x.ClrType)).Select(x => x.ClrType)`. Every such `Version` has `GetColumnName() == "xmin"`. |

Existing regression guards stay unchanged and must pass: `AppDbContextTests.Model_Current_MatchesLatestMigrationSnapshot`, `GradeConcurrencyTests`, `SessionPersistenceTests`, `ExamSessionPersistenceTests`, `LessonOpeningPersistenceTests`, `ExamBlueprintPersistenceTests`, `ExamPeriodPersistenceTests`, `RuntimeSettingOverridePersistenceTests`, `PaymentPersistenceTests`, `TeacherThreadPersistenceTests`, `AvatarConversationPersistenceTests`, `QuestionMasteryPersistenceTests`, `ImportQuestionsReplayBehaviourTests`, `AttemptTrainingRecordTests`, and the worker tests. Do not edit them.

## Definition of done
- [ ] `IVersioned` exists in `Core.DDD.Entities` with exactly `uint Version { get; }`.
- [ ] `ConflictMap` and `UniqueViolation` exist in `Core.EntityFrameworkCore.Conflicts` with the signatures above. Core has no `Npgsql` using or package reference (`grep -ri npgsql api/core-libraries --include=*.cs --include=*.csproj` returns nothing new).
- [ ] Core has no Elmanhg names, error codes or index names.
- [ ] `RowVersionConvention.ApplyRowVersionConvention()` skips owned, derived and shared-type entity types.
- [ ] `CoreDbContext.cs` is byte-for-byte unchanged.
- [ ] `AppDbContext.SaveChangesAsync` has exactly one `catch`, `catch (DbUpdateException exception) when (Conflicts.TryTranslate(exception, out var conflict))`.
- [ ] `AppDbContext.Conflicts` registers all 24 rules in the order listed, with the 2 WHY comments kept.
- [ ] `grep -rn "IsRowVersion" api/Elmanhg.Infrastructure/Data/Context` returns nothing. `ApplyRowVersionConvention()` is called once, after `ConfigureSlaCalendars` and before the soft-delete filter method.
- [ ] The 11 entities implement `IVersioned`, and their `Version` declarations are unchanged.
- [ ] `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries` and the `ConfigureTeacherVoiceDrafts` body are untouched. Other `Configure*` bodies differ only by the deleted `.IsRowVersion()` line.
- [ ] No migration was added. `dotnet ef migrations has-pending-model-changes -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api` reports no changes, and `Model_Current_MatchesLatestMigrationSnapshot` passes.
- [ ] Tests #1–#19 exist with these exact names and pass. All existing tests pass, and no existing test file is modified except the one added method in `AppDbContextTests`.
- [ ] `dotnet build api/Elmanhg.slnx -c Release` passes with zero warnings in the app projects (TreatWarningsAsErrors), and no using is left unused in `AppDbContext.cs`.
- [ ] No code comment contains a `#<number>` reference.
- [ ] `docs/constitution.md` line 3 and `.claude/skills/dotnet-feature/SKILL.md` (delta 1, §6.3 row, §6.8) are updated as specified. No other doc is edited.
- [ ] No new NuGet package. No new project, so `Elmanhg.slnx` is unchanged.
