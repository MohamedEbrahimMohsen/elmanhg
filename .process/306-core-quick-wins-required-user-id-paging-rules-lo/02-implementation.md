# Implementation — [E21.S3] Core quick wins: required user id, paging rules, load-or-404, soft-delete filter, validators, time helpers, OTP consume

Worktree: `D:/Personal/elmanhg-wt/306` (branch state: uncommitted, nothing pushed). Paths below are relative to the worktree root.

## Files created
| Path | Lines | Purpose |
|------|------:|---------|
| `api/core-libraries/Core.Identity/Tokens/CurrentUser/CurrentUserServiceExtensions.cs` | 8 | C1 `GetRequiredUserId(errorCode)` |
| `api/core-libraries/Core.DDD/Repositories/RepositoryExtensions.cs` | 14 | C2 `GetRequiredAsync` (id and predicate overloads, forward include/orderBy/asNoTracking verbatim) |
| `api/core-libraries/Core.EntityFrameworkCore/Repositories/QueryablePagingExtensions.cs` | 29 | C3 `ToPageDataAsync` |
| `api/core-libraries/Core.EntityFrameworkCore/Context/SoftDeleteModelBuilderExtensions.cs` | 28 | C4 `ApplySoftDeleteQueryFilters` (root, non-owned, no existing filter) |
| `api/core-libraries/Core.Validation/Extensions/PagingValidationExtensions.cs` | 19 | C5 `ValidatePaging` |
| `api/core-libraries/Core.Validation/Extensions/DateRangeValidationExtensions.cs` | 28 | C6 `ValidateDateRange` (instant and `DateOnly` overloads) |
| `api/core-libraries/Core.Validation/Files/FileSignature.cs` | 83 | C7 `FileSignature` + built-ins + `Matches` |
| `api/core-libraries/Core.Utilities/Time/DateTimeOffsetExtensions.cs` | 6 | C8 `TruncateToMicroseconds` |
| `api/core-libraries/Core.Utilities/Time/TimeZoneInfoExtensions.cs` | 14 | C9 `LocalDate` / `StartOfDay` |
| `api/core-libraries/Core.Utilities/ValidatedOptionsServiceCollectionExtensions.cs` | 20 | C10 `AddValidatedOptions<T>` / `<T, V>` |
| `api/core-libraries/Core.OTP/Repositories/OtpRepositoryExtensions.cs` | 19 | C11 `ConsumeAsync` (no save) |
| `api/Elmanhg.Application/Lessons/UploadDiagramImage/DiagramImageFormats.cs` | 11 | C12 diagram formats (removes the cross-slice reach) |
| `api/Elmanhg.Tests/Core/Identity/CurrentUserServiceExtensionsTests.cs` | 44 | tests 1–3 |
| `api/Elmanhg.Tests/Core/DDD/RepositoryExtensionsTests.cs` | 83 | tests 4–9 |
| `api/Elmanhg.Tests/Core/DDD/PageDataTests.cs` | 25 | tests 10–11 |
| `api/Elmanhg.Tests/Integration/Persistence/QueryablePagingExtensionsTests.cs` | 67 | tests 12–14 |
| `api/Elmanhg.Tests/Core/Validation/PagingValidationExtensionsTests.cs` | 63 | tests 15–20 |
| `api/Elmanhg.Tests/Core/Validation/DateRangeValidationExtensionsTests.cs` | 89 | tests 21–29 |
| `api/Elmanhg.Tests/Core/Validation/CollectionValidationExtensionsTests.cs` | 46 | tests 30–32 |
| `api/Elmanhg.Tests/Core/Validation/RequiredValidationExtensionsTests.cs` | 44 | tests 33–35 |
| `api/Elmanhg.Tests/Core/Validation/FileSignatureTests.cs` | 89 | tests 36–42 |
| `api/Elmanhg.Tests/Core/Validation/FileSignatureValidationTests.cs` | 58 | tests 43–46 |
| `api/Elmanhg.Tests/Core/Persistence/SoftDeleteModelBuilderExtensionsTests.cs` | 69 | tests 47–50 |
| `api/Elmanhg.Tests/Integration/Persistence/SoftDeleteQueryFilterModelTests.cs` | 42 | tests 51–52 (the 47-type set) |
| `api/Elmanhg.Tests/Core/Utilities/DateTimeOffsetExtensionsTests.cs` | 37 | tests 53–55 |
| `api/Elmanhg.Tests/Core/Utilities/TimeZoneInfoExtensionsTests.cs` | 41 | tests 56–59 |
| `api/Elmanhg.Tests/Core/Utilities/ValidatedOptionsServiceCollectionExtensionsTests.cs` | 71 | tests 60–63 |
| `api/Elmanhg.Tests/Core/Otp/OtpRepositoryExtensionsTests.cs` | 60 | tests 64–67 |

## Files modified
| Path | Change |
|------|--------|
| Core: `Core.DDD/Models/PageData.cs` | C13 `Map<TOut>` |
| Core: `Core.EntityFrameworkCore/Repositories/Repository.cs` | `FindPaginatedAsync` applies `orderBy`, then `ToPageDataAsync` |
| Core: `Core.Validation/ValidationErrors.cs` | 6 default constants (`VALIDATION_PAGE_NUMBER`, `_PAGE_SIZE`, `_DATE_RANGE`, `_DATE_RANGE_TOO_LONG`, `_DISTINCT`, `_FILE_SIGNATURE`) |
| Core: `Core.Validation/Extensions/{Required,Collection,File}ValidationExtensions.cs` | C14 `Guid?` overload, C15 `ValidateDistinct`, C16 `ValidateFileSignature` |
| Core: `Core.Utilities/Core.Utilities.csproj` | D16: 4 `Microsoft.Extensions.*` package refs → `FrameworkReference Microsoft.AspNetCore.App` |
| Core: `Core.OTP/Delivery/CoreOtpDeliveryServiceCollectionExtensions.cs` | `AddValidatedOptions<OtpDeliveryOptions, OtpDeliveryOptionsValidator>` |
| Core: `Core.Notifications/Templates/{GetNotificationTemplate,UpdateNotificationTemplate}/…Handler.cs` | R3 shape B′ |
| Domain: `Elmanhg.Domain.csproj` | D15 reference to `Core.Utilities` |
| Domain: `Identity/User.cs` | D8 `: ISoftDeletable` (no member change) |
| Domain: `Avatar/AvatarConversation.cs`, `Sessions/Session{,.Answering,.Essays,.Exam,.ExamSubmission,.Submission}.cs`, `TeacherThreads/TeacherThread{,.FollowUps,.Replies,.Sla}.cs`, `TeacherThreadOutOfAppReminder.cs`, `TeacherThreadSlaEvent.cs` | R6a `.TruncateToMicroseconds()`; private `ToMicroseconds`/`UtcNowToMicroseconds` deleted (with their now-orphaned comments); `TeacherThread.ToMicroseconds` kept as a one-line delegation (D15, F4) |
| Domain: `SlaCalendars/SlaCalendar.cs` | `LocalDate`/`StartOfDay` delegate to the zone helpers |
| Application: 108 handlers + `GetMyTeacherStatsHandler` | R1 `GetRequiredUserId` (66 R1a, 39 R1b, 3 R1c, 1 variant) |
| Application: 14 paged validators | R2a `ValidatePaging` |
| Application: 12 paged handlers/loaders | R2b `page.Map(...)` |
| Application: 81 files (93 sites) + 2 Core.Notifications handlers | R3 `GetRequiredAsync` (95 sites total); `using Core.DDD.Repositories;` added in 86 files |
| Application: 4 formats classes, 5 file validators | R5a (byte logic deleted, `Signatures` lists, one-line delegations) |
| Application: 6 date validators, 3 distinct validators, `RefundPaymentValidator` | R5b, R5c, R5d |
| Application: `AvatarGate`, `FreeTierGate`, `GetMasteryOverviewHandler`, `DashboardWindow`, `AskTeacherGate` | R6b |
| Application: `DependencyInjection.cs` (24), Infrastructure `AiService/Invitations/Messaging/Payments` ServiceCollectionExtensions (4), Api `RateLimiting/AppRateLimiting.cs` (1) | R7 `AddValidatedOptions` (30 with OtpDelivery) |
| Application: 4 auth handlers | R8 `ConsumeAsync` |
| Application/Core.Notifications: 83 files | removed `using Core.Errors;` where nothing from it remains |
| Infrastructure: `Data/Context/AppDbContext.cs` | 42 hand-written filters + method deleted; `modelBuilder.ApplySoftDeleteQueryFilters();` (SaveChangesAsync / `.IsRowVersion()` untouched) |
| Infrastructure: `EssayGrading/EssayGradeRepository.cs`, `MathStepGrading/MathStepGradeRepository.cs` | `GetInReviewPageAsync` only → `ToPageDataAsync` (`GetDueIdsAsync` untouched) |
| Tests (additions only, 0 deleted lines): 13 paged-validator test classes | `Validate_PageOffsetPastIntRange_FailsPageNumberInvalid` (tests 68–80) |
| Tests: `UploadDiagramImageValidatorTests`, `RefundPaymentValidatorTests` | tests 81, 82 |
| Docs: `docs/progress.md` (line 60), `docs/constitution.md` (lines 3, 144) | as planned |
| `.claude/skills/dotnet-feature/SKILL.md` | deltas 1 and 5, §4.9, §5.4 table, §5.5 example and rule, §5.6, §6.1, §6.2, §6.3, §6.4, §8.5, §8.12, §9 checklist |

Postman: no route, method, auth or body changed, so `postman/elmanhg.postman_collection.json` needs no update.

## Verification counts
| Check | Before | After | Plan after | Note |
|---|---:|---:|---:|---|
| user guard (`… == null \|\| … == default`) | 110 | 2 | 0 | See Deviations 3 |
| `GetRequiredUserId(` | 0 | 109 | 109 | |
| `currentUserService.UserId.Value` | 106 | 1 | 1 | `SubjectScopeBehaviour` |
| `RuleFor(x => x.PageNumber)` | 14 | 0 | 0 | |
| `ValidatePaging(` | 0 | 14 | 14 | |
| `new PageData<` (App + Infra) | 14 | 0 | 0 | |
| infra `PageCalculator.` | 6 | 0 | 0 | |
| inline `?? throw new NotFoundCoreException` | 62 | 21 | 19 | See Deviations 3 |
| all `throw new NotFoundCoreException` | 151 | 58 | 58 | |
| `GetRequiredAsync(` | 0 | 95 | 95 | |
| app `HasQueryFilter` | 42 | 0 | 0 | |
| core `HasQueryFilter` (`CoreDbContext`) | 5 | 5 | 5 | `CoreDbContext.cs` unchanged |
| `git diff --exit-code -- api/Elmanhg.Infrastructure/Migrations` | 0 | 0 | 0 | |
| app `stackalloc` | 4 | 0 | 0 | |
| date-range copies | 8 | 0 | 0 | |
| `ValidateDateRange(` | 0 | 6 | 6 | |
| distinct copies | 3 | 0 | 0 | |
| `TicksPerMicrosecond` in Domain | 6 | 3 | 3 | F1–F3 left for lane 307 |
| local-date copies | 5 | 0 | 0 | |
| start-of-day copies | 3 | 0 | 0 | |
| `AddValidatedOptions<` | 0 | 30 | 30 | |
| raw `ValidateDataAnnotations()` | 32 | 3 | 3 | Observability, Storage, Core.Utilities |
| `otp.MarkUsed();` in Application | 4 | 0 | 0 | |

## Deviations
| Plan said | Reality | What I did |
|-----------|---------|------------|
| Test probe types are `private sealed` nested types | NSubstitute (Castle DynamicProxy) cannot proxy `IRepository<T>` when `T` is a private nested type | `RepositoryExtensionsTests.ProbeEntity` is `public sealed` nested. In `SoftDeleteModelBuilderExtensionsTests`, `ProbeEntity` is `private class` (not sealed) because `DerivedProbeEntity` has to derive from it for test 49. All other probes are `private sealed`. |
| R1 split: 63 R1a, 42 R1b, 3 R1c | The detection found 66 files with a `var <name> = currentUserService.UserId.Value;` declaration (R1a), 39 inline-only (R1b) and 3 guard-only (R1c) | The rules were applied as written. The final counts match the plan: 109 calls, 1 `.UserId.Value` left. No file kept a leftover `currentUserService.UserId` reference, and there were no `userId` name collisions. |
| Count "user guard" 108 → 0, and "inline 404" 62 → 19 | That grep has no `if (` prefix, so before = 110. The 2 extra hits are `var userId = currentUserService.UserId == null \|\| … ? null : …` ternaries in `RecordFunnelEventHandler` and `ReportClientErrorHandler`. These optional-user reads are not guards (D18 spirit), so I left them. The inline-404 grep covers `api/core-libraries`, so it also counts the 2 new `?? throw` lines in `RepositoryExtensions` (the plan's "all 404" row already counts them) | Both untouched; after = 2 and 21. |
| `AskTeacherGate.MonthStart`: "keeping its Npgsql UTC WHY comment" | The method is now one expression line, so the comment can't stay inside the body | The comment sits directly above the method. |

No other deviations. No existing test was edited, skipped or deleted (`git diff api/Elmanhg.Tests` has 0 removed lines). I found no exception sites to revert: the first full build after the sweep had 0 errors and 0 new warnings.

## Build & test
- `dotnet build api/Elmanhg.slnx -c Release`: **Build succeeded**, 0 errors. The only warnings are 9 existing nullable warnings in vendored `core-libraries` (`Notification.cs`, `NotificationTemplate.cs`, `OTP.cs`, and the LocalizedText block of `RequiredValidationExtensions.cs`). `core-libraries/Directory.Build.props` exempts these from TreatWarningsAsErrors on purpose, and none come from new code. App projects have 0 warnings.
- `dotnet build api/` (Debug): **Build succeeded**, 9 warnings (the same existing core ones), 0 errors.
- `dotnet test api/ -c Release --no-build` (Docker 29.6.2, Testcontainers): **Passed! total 5394, failed 0, succeeded 5394, skipped 0** (1m 15s).
- Filtered rerun of the new classes `SoftDeleteQueryFilterModelTests`, `FileSignatureTests`, `TimeZoneInfoExtensionsTests`, `QueryablePagingExtensionsTests`: 29/29 passed. This confirms the new tests were in the run.
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build`: "No changes have been made to the model since the last migration."
- `git diff --exit-code -- api/Elmanhg.Infrastructure/Migrations`: exit 0. `git status --porcelain api/openapi`: empty.

## Deferred (other lanes)
F1–F4 are untouched, as planned: the private `ToMicroseconds` in `EssayGrade*`, `MathStepGrade*` and `TrainingExport*`, plus the 4 `TeacherThread.ToMicroseconds` calls in `TeacherVoiceDraft.cs` (lane 307). After that lane lands, delete `TeacherThread.ToMicroseconds` (now a one-line delegation). Also untouched, as instructed: `AppDbContext.SaveChangesAsync`, every `.IsRowVersion()` line, workers, `GetDue*` and `Fail*` handlers, and `GetDueIdsAsync`.

## Notes for review
- **`ValidateRequired` `Guid?` overload binds everywhere.** Overload resolution prefers it over the generic `TValue?` overload for every `Guid?` property. I scanned all validators: only `RefundPaymentValidator.IdempotencyKey` is `Guid?` with `ValidateRequired`, so no other endpoint changes behaviour.
- **`QuestionImportFile.HasZipSignature` is now extension-bound (`.xlsx`).** Before, it checked the bytes whatever the file name. Its only production caller runs `ValidateAllowedExtensions(.xlsx)` first under `Cascade(Stop)`, and `QuestionImportFileTests` uses `q.xlsx`, so behaviour is unchanged in practice.
- **`ValidatePaging` and `ValidateDateRange` are one root-level rule with chained `Must`s.** They rely on FluentValidation's default rule-level `Continue` cascade; no global cascade override exists in the repo. The existing `Should().Equal(...)` validator tests all pass.
- **`using` directives.** New `using`s were placed in sorted position and existing using order was kept. `using Core.Errors;` was removed from 83 files where no Core.Errors type remained (build-verified).
- **`EssayGradeRepository` / `MathStepGradeRepository` share files with lane 307.** These two files are touched only inside `GetInReviewPageAsync`. The hunk is separated from `GetDueIdsAsync` by unchanged lines.
- **Docs-sync.** The only behaviour change is the paging-overflow 422 on 13 endpoints. `docs/progress.md` is the only doc that described the old "below 1" reason. The other docs list the codes without a reason, so they don't diverge.
