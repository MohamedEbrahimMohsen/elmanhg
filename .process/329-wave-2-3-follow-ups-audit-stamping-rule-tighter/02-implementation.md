# Implementation — [E21.S12] Wave-2/3 follow-ups: audit stamping rule, tighter tests, docs

## Files created
| Path | Lines | Purpose |
|------|-------|---------|
| `api/core-libraries/Core.EntityFrameworkCore/Auditing/AuditStampingInterceptor.cs` | 54 | `SaveChangesInterceptor` that stamps `CreationDate`/`CreatedBy` on added rows, `UpdationDate` on changed/deleted rows when not set, and `UpdatedBy = actor` whenever there is an acting user. The plan's single ordering comment is the only comment. |
| `api/core-libraries/Core.Queues/SweepWorker.Steps.cs` | 64 | Partial: the four guarded `Try*Async` steps, moved verbatim (`scopeFactory`/`logger` changed to `ScopeFactory`/`Logger`). |
| `api/Elmanhg.Tests/Core/Persistence/AuditStampingTestBase.cs` | 40 | Shared harness (Now/Earlier, actor/creator, substitutes, probe context, `Repository()`, `AttachedProbe()`, `OnPublish`). |
| `api/Elmanhg.Tests/Core/Persistence/RepositoryAuditStampingModifiedTests.cs` | 102 | Test rows 8–15. |
| `api/Elmanhg.Tests/Core/Persistence/AuditStampingRegistrationTests.cs` | 40 | Test rows 16–17. |
| `api/Elmanhg.Tests/Core/Persistence/ConflictMapProbe.cs` | 10 | Shared constants and `ProbeViolation`. |
| `api/Elmanhg.Tests/Core/Persistence/ConflictMapConcurrencyTests.cs` | 96 | 5 moved tests + new `TryTranslate_ConcurrencyAndUniqueRulesBothMatch_ReturnsConcurrencyCode`. |
| `api/Elmanhg.Tests/Core/Persistence/ConflictMapUniqueTests.cs` | 87 | 6 moved tests. |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenRotatorTestBase.cs` | 53 | Moved fields, constants, constructor and helpers (now `protected`). |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenRotatorIssueTests.cs` | 64 | 5 moved tests. |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenRotatorReplayTests.cs` | 91 | 6 moved tests. |
| `api/Elmanhg.Tests/Core/Cache/CacheProbes.cs` | 13 | `CacheProbeQuery`, `UncachedProbeRequest` moved verbatim. |
| `api/Elmanhg.Tests/Core/Cache/CachingBehaviourTestBase.cs` | 22 | Shared cache, options, `Behaviour<T>()`, `Next`. |
| `api/Elmanhg.Tests/Core/Cache/CachingBehaviourTtlTests.cs` | 43 | 3 moved TTL tests. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextAuditStampingTests.cs` | 83 | Test rows 18–20 against PostgreSQL (Testcontainers). |

## Files modified
| Path | Change |
|------|--------|
| `api/core-libraries/Core.EntityFrameworkCore/Repositories/Repository.cs` | Constructor is now `(DbContext context)`. Removed the `_currentUser`/`_timeProvider` fields and the stamping loop. `SaveChangesAsync` only saves. |
| `api/core-libraries/Core.EntityFrameworkCore/Repositories/{AuditLog,Notification,NotificationTemplate,Otp,UserDevice}Repository.cs` | Constructor is now `(TContext context) : Repository<X>(context)`. Removed `using Core.DDD.Identity;`. For `OtpRepository.cs`, only the constructor line and that using changed. |
| `api/core-libraries/Core.EntityFrameworkCore/DependencyInjection.cs` | Added `AddCoreAuditStamping<TContext>()` as the plan specifies. |
| `api/core-libraries/Core.Queues/SweepWorker.cs` | Now `partial`. The `Try*` steps moved out and `using Core.Errors;` was removed. 124 → 68 lines. `Microsoft.Extensions.Logging` stays because the primary constructor takes `ILogger`. |
| `api/Elmanhg.Api/Program.cs` | Added `builder.Services.AddCoreAuditStamping<AppDbContext>();` after `AddCoreEntityFrameworkCore`. |
| 33 app repositories in `api/Elmanhg.Infrastructure/**` (the plan's exact list) | Constructor is now `(AppDbContext context) : Repository<X>(context)`. Removed `using Core.DDD.Identity;`. |
| `api/Elmanhg.Domain/{Mastery/QuestionMastery, Avatar/AvatarConversation (x2), Sessions/Session, TeacherThreads/TeacherThread.FollowUps (x2), ReviewSessions/ReviewSession, TeacherThreads/TeacherVoiceDraft}.cs` | Deleted the 8 owner-derived `UpdatedBy = StudentId/TeacherId;` lines. The grep now returns nothing. |
| `api/Elmanhg.Tests/Core/Persistence/AuditStampingProbes.cs` | Reworked to the plan's contract: the probe context dispatches and then calls `base` with the real interceptor, `SuppressedSaveInterceptor` was added, and `AuditStampingRegistrationProbeDbContext` was added. |
| `api/Elmanhg.Tests/Core/Persistence/RepositoryAuditStampingTests.cs` | Rewritten on the base class with test rows 1–7. Row 3 is renamed as the plan says. |
| `api/Elmanhg.Tests/Core/Persistence/ConflictMapTests.cs` | Deleted from the working tree only, not staged. |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenRotatorTests.cs` | Deleted from the working tree only, not staged. |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenRotationRegistrationTests.cs` | Now derives from `RefreshTokenRotatorTestBase`. The default-grace test body is rewritten and `..._RevokesReplayAfterTenSeconds` is added. |
| `api/Elmanhg.Tests/Core/Cache/CachingBehaviourTests.cs` | 4 tests kept on the base class. The probes and TTL tests moved out. |
| `api/Elmanhg.Tests/Application/Features/Auth/RefreshAccessToken/RefreshAccessTokenHandlerTests.cs` | One assertion added to `Handle_SuspendedUser_ThrowsForbidden`. |
| `api/Elmanhg.Tests/Application/Shared/RuntimeSettings/RuntimeSettingRegistryTests.cs` | Added `Definitions_DefaultOptions_EachGroupIsContiguous`. |
| `api/Elmanhg.Tests/Integration/Persistence/RepositoryPagingTests.cs` | Repository construction now passes the context only. Removed the unused `Core.DDD.Identity` using. |
| `api/Elmanhg.Tests/Domain/...` (7 files listed in the plan) | `UpdatedBy` expectations changed as in test rows 21–28. Added `Rate_AnsweredThread_LeavesUpdatedByToTheSave` and `RecordOpening_ActiveSession_LeavesUpdatedByToTheSave`. |
| `docs/audit-log.md` | Added the owned-values bullet and the new "## Audit fields" section before "Not audited". |
| `docs/constitution.md` | Lines 3, 75, 143 and 144 updated as the plan specifies. |
| `.claude/skills/dotnet-feature/SKILL.md` | Lines 22, 252, 597, 600, 609, 637, 719 and 1049 updated. Nothing in the docs still says the repository stamps. |

## Deviations
| Plan said | Reality | What I did |
|-----------|---------|------------|
| Test rows 17 and 18 read `FindExtension<CoreOptionsExtension>()!.Interceptors`. | `CoreOptionsExtension.Interceptors` is nullable (`IEnumerable<IInterceptor>?`), so `.OfType<>()` raised CS8604, and warnings are errors in this build. | Used `FindExtension<CoreOptionsExtension>()?.Interceptors ?? []`. The assertions are unchanged. |
| Row 30: rewrite the body of `AddCoreRefreshTokenRotation_NoConfiguration_DefaultsGraceToTenSeconds`. | The test now calls the async `RotateAsync`. | The method is now `async Task` with the same name. A private `RegisteredProvider()` helper builds the container the plan describes. Row 31 uses the same helper. |
| Row 19: `ActAs(scope, OtherAdminId)` with the helper signature `ActAs(IServiceProvider, Guid)`. | The signature takes an `IServiceProvider`. | Called `ActAs(scope.ServiceProvider, OtherAdminId)`. Added private `SeedAsync` and `ReadAsync` helpers (fresh scope, `AsNoTracking()`) and the test's own admin GUID constants. |
| SKILL line 609: replace "and stamps audit fields in `SaveChangesAsync`" with "; its `SaveChangesAsync` only saves (the interceptor stamps)". | A literal swap leaves "`_context`, ; its …". | Wrote "plus `_dbSet` and `_context`; its `SaveChangesAsync` only saves (the interceptor stamps)." |
| `CachingBehaviourTests` kept "verbatim". | `_memoryCache.Get<string>` is an extension method in `Microsoft.Extensions.Caching.Memory`. | Kept that using in `CachingBehaviourTests.cs`. The test bodies are unchanged. |

## Build & test
- `dotnet build api/ -c Release`: **Build succeeded. 0 Warning(s), 0 Error(s).**
- `git status --porcelain api/openapi` after the build: empty, so the OpenAPI document is unchanged.
- `dotnet test api/ -c Release --no-build` (Docker running, so Testcontainers integration tests ran): **Passed! total: 5560, failed: 0, succeeded: 5560, skipped: 0.**
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build`: **"No changes have been made to the model since the last migration."**
- `git diff --name-only` contains no `CoreDbContext.cs` and nothing under `Migrations/`. No new NuGet package was added.
- No `#<number>` appears in any added line.

## Deferred items
None.

## Notes for review
- **Old tests now running on the interceptor.** `Repository<StampedProbe>.SaveChangesAsync` no longer stamps. The repository tests that remain now exercise the real interceptor through the probe context's save. Rows 6 and 7 show the stamping happens after dispatch: `OnPublish` mutates the tracker during `DomainEventDispatcher.PublishAsync`, and the interceptor then stamps.
- **Interceptor condition.** It is written `(entry.State is Modified or Deleted) && !…IsModified`, with parentheses for clarity. The `UpdatedBy` condition matches the plan exactly.
- **Files over the size guide.**
  - `RepositoryAuditStampingModifiedTests.cs` is 102 lines.
  - `ReviewSessionTests.cs` was already 110 lines and grows to 122 with the new test the plan requires. I did not split it because the plan does not list it.
- **Line endings.** Several touched files (the 33 app repositories, the core repositories, the domain files, `Program.cs`, `RepositoryPagingTests.cs`) were LF in the working copy before this change, and the edits kept LF. The diffs contain only the intended lines.
- **Lane boundary.** The only file in #328's areas that changed is `OtpRepository.cs` (its constructor line and its now-unused `using`). `PipelineCompositionTests`, the OTP entity and migrations, Storage/S3 and the messaging tests are untouched. `CachingBehaviourTests.cs` was split, as Decision 16 assigns to this story.
- **Domain rule check.** The remaining domain `UpdatedBy =` assignments all come from actor arguments (`teacherId`, `assignment.TeacherId`, `updatedBy`, …).
- Nothing was committed or staged.
