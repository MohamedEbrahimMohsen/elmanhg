# Implementation — Core hygiene: dead code, packages, TimeProvider and audit actor stamping (E21.S2)

Precondition: PR 321 (E21.S4) was still open when I started. I polled it and it merged at 03:03. The branch was fast-forwarded to `origin/main` (`9e14add2`) with `git merge --ff-only`, so there is no merge commit. Nothing else was committed and nothing was pushed.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.DDD/Time/DateTimeOffsetExtensions.cs` | 6 | C1. Moved from `Core.Utilities/Time` (git mv). Namespace is now `Core.DDD.Time`. |
| `api/core-libraries/Core.DDD/Time/TimeZoneInfoExtensions.cs` | 14 | C2. Moved the same way. Keeps its WHY comment. |
| `api/core-libraries/Core.DDD/Identity/ICurrentUser.cs` | 8 | C3. Actor port with `UserId`, `UserName` and `Role`. |
| `api/Elmanhg.Tests/Core/DDD/DateTimeOffsetExtensionsTests.cs` | 37 | T1. Moved verbatim (namespace and using updated). |
| `api/Elmanhg.Tests/Core/DDD/TimeZoneInfoExtensionsTests.cs` | 41 | T2. Moved verbatim. |
| `api/Elmanhg.Tests/Core/Persistence/AuditStampingProbes.cs` | 18 | T3. `StampedProbe` and `AuditStampingProbeDbContext`. No database is contacted. |
| `api/Elmanhg.Tests/Core/Persistence/RepositoryAuditStampingTests.cs` | 140 | T4. Test-plan rows 3–11. |
| `api/Elmanhg.Tests/Core/CQRS/ValidationBehaviourTests.cs` | 76 | T5. Rows 12, 18, 19, plus the probe request and validators. |
| `api/Elmanhg.Tests/Core/Identity/CurrentUserServiceTests.cs` | 33 | T6. Rows 22, 23. |
| `api/Elmanhg.Tests/Core/Identity/SignInManagerSubstitute.cs` | 12 | T7. Helper. |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenServiceTests.cs` | 57 | T8. Rows 24, 25. |
| `api/Elmanhg.Tests/Core/Identity/JwtTokenServiceTests.cs` | 52 | T9. Rows 26, 27. |
| `api/Elmanhg.Tests/Core/Otp/OtpOptionsRegistrationTests.cs` | 32 | T10. Rows 28, 29. |
| `api/Elmanhg.Tests/Domain/DomainAssemblyReferencesTests.cs` | 14 | T11. Row 30. |

## Files modified
| Path | Change |
|---|---|
| `api/Directory.Packages.props` | New `<ItemGroup Label="Core libraries">` with the 21 `PackageVersion`s from the plan. `Microsoft.Extensions.Options` is 10.0.5. |
| `api/core-libraries/Directory.Packages.props` | Deleted. |
| all 21 `api/core-libraries/*/*.csproj` | Every `Version=` removed. Auditing, CQRS, EntityFrameworkCore, Exceptions, Identity, Localization, Logging, Notifications, OTP, Utilities and Queues changed per the plan (FrameworkReference added, the 2.3.9 packages, `Azure.Core`, and the framework-shipped `Microsoft.Extensions.*` packages removed; MediatR and FluentValidation now direct where listed). `Core.Auditing` no longer references Core.Identity, `Core.CQRS` no longer references Core.Utilities, `Core.Identity` no longer references Core.Validation. |
| `api/Elmanhg.Domain/Elmanhg.Domain.csproj` | Dropped the `Core.Utilities` reference. |
| Dead code | Deleted `CoreAuthEndpoints.cs`, `Register/*`, `Requests/*`, `RequestLog.cs`, `Core.CQRS/Exceptions/ErrorCodes.cs` (the folder is now empty and removed), `LocalizationOptions.cs`, `ILocalizationManager.cs`, `LocalizationManager.cs`, `Templates/Shared/NotificationTemplateClaimsChecker.cs` (folder removed) and `LocalizationManagerTests.cs`. Removed `UserIsRequired`/`PasswordIsRequired`, the `ILocalizationManager` registration, `using System.Timers` (CQRS DI), the 3 unused usings in `JwtTokenService`, the `Templates.Shared` usings, the commented validator rule, the commented `ConfigureLocalized` block, and `Latitude`/`Longitude` from the Azure columns. `SeedLocalizedExtensions` namespace is now `Core.EntityFrameworkCore.Context`. |
| `Core.Identity/DependencyInjection.cs` | Commented lines removed. Added `TryAddSingleton(TimeProvider.System)` and `AddScoped<ICurrentUser>(… ICurrentUserService)`. |
| `Core.Identity/Tokens/CurrentUser/ICurrentUserService.cs`, `CurrentUserService.cs` | `ICurrentUserService : ICurrentUser`. Removed `NationalId`/`LoginType`. Added `Role`. |
| `Core.Identity/Tokens/RefreshToken/RefreshTokenService.cs`, `AccessToken/JwtTokenService.cs` | `TimeProvider` as the last constructor parameter, used for expiry and for the expiry check. `ConfigureAwait(false)` on `ValidateSecurityStampAsync`. |
| `Core.Auditing/AuditBehaviour.cs`, `DependencyInjection.cs` | Now depends on `ICurrentUser` + `TimeProvider`. `ActorRole` comes from `currentUser.Role`. DI adds `TryAddSingleton(TimeProvider.System)`. |
| `Core.EntityFrameworkCore/Repositories/Repository.cs` | Constructor and fields per D3. `SaveChangesAsync` is exactly the "Domain behaviour" body. Dead commented code and the `events` variable removed. |
| 5 core repositories (`AuditLog`, `Notification`, `NotificationTemplate`, `Otp`, `UserDevice`) | Optional `ICurrentUser?` and `TimeProvider?` passed to the base. |
| 33 app repositories in `Elmanhg.Infrastructure` | `(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider)` passed to the base. `RuntimeSettingOverrideRepository` is untouched. |
| `Core.OTP/Entities/OTP.cs`, `Repositories/OtpRepositoryExtensions.cs`, `VerifyOTP/VerifyOTPHandler.cs` | `Verify(codeHash, now)`, `MarkUsed(now)`, `ConsumeAsync(…, now, ct)`. The handler gets `TimeProvider`. |
| `Core.OTP/OtpOptions.cs`, `DependencyInjection.cs` | `PhoneCodes = []` with `[MinLength(1)]`, `PhoneLength` with `[Range(1, int.MaxValue)]` and no default. Registration adds `ValidateDataAnnotations().ValidateOnStart()`. |
| `Elmanhg.Application/Auth/{AcceptInvitation,LoginWithEmailCode,LoginWithPhone}/*Handler.cs` | `TimeProvider` as the last constructor parameter, passed to `ConsumeAsync`. |
| `Elmanhg.Application/Auth/RegisterWithPhone/RegisterWithPhoneHandler.cs` | Reads `now` once and passes it to `ConsumeAsync` and `AcceptTerms`. |
| `Core.Validation/Files/FileSignature.cs` | `Create` throws `ArgumentOutOfRangeException` (`patterns`) when there are no patterns. |
| `Core.CQRS/Behaviours/ValidationBehaviour.cs` | Runs validators one at a time with `foreach` and `ConfigureAwait(false)`. No `Task.WhenAll`. See the deviation on the context. |
| `ConfigureAwait(false)` sweep | `ExceptionMiddleware` (3), `RequestLoggingMiddleware` (1), `NotificationEndpoints` (13), `NotificationTemplateEndpoints` (5), `OTPEndpoints` (3), `ListNotificationsQueryHandler` (2), `MarkNotificationAsReadHandler`, `RegisterUserDeviceHandler`, `FirebaseNotificationService` (2). No core `await` is left without it, apart from `await using`/`await foreach`. |
| 14 Domain + 5 Application files | `using Core.Utilities.Time;` replaced by `using Core.DDD.Time;` (Core usings kept sorted). |
| `EssayGrade{,.Grading,.Review}.cs`, `MathStepGrade{,.Grading,.Review}.cs`, `TrainingExport{,.Lifecycle}.cs`, `TeacherVoiceDraft.cs`, `TeacherThread.cs` | Private and internal `ToMicroseconds` removed. Calls became `x.TruncateToMicroseconds()` and `using Core.DDD.Time;` was added. |
| Domain `UpdationDate` cleanup | Removed 17 of the 18 listed lines: `Lesson.Lifecycle` Publish/Unpublish/Archive, `LessonObjective.Update`, `QuestionMastery.Record`, `Subject.MoveTo`, `CurriculumUnit.MoveTo`, Payment MarkSucceeded/MarkFailed/LinkProviderOrder/MarkRefunded/ResolveReview, Subscription Renew/MarkPastDue/Cancel/Expire/RevokePaidPeriod. `Lesson.MoveTo` is kept (see Deviations). The 14 listed "keep" lines are untouched. |
| Tests | Edited as listed in the plan: `AuditBehaviourTests` (plus row 20), `OtpBuilder`, `OtpTests` (plus 13 and 14), `OtpRepositoryExtensionsTests` (plus 15), `VerifyOTPHandlerTests` (plus 16), the 4 auth handler suites, `QuestionMasteryTests` (row 31 rename), `RepositoryPagingTests`, `ApiFactory` (phone keys), `FileSignatureTests` (plus 17). |
| `docs/constitution.md` | Line 3 stack (`Core.DDD.Time`, `ICurrentUser`, `Core.Utilities` = `IGenerator` + `AddValidatedOptions`, central pinning and the FrameworkReference sentence), §1.4 time/stamp rule, §4 Domain bullet and §4 Infrastructure bullet, all worded as the plan gives them. |
| `.claude/skills/dotnet-feature/SKILL.md` | Deltas 1 and 5, §2 Domain-method row, the §4.2 sample and rule, the §6 repository sample and base-members sentence, the §6 outbox line, §8.2, §8.10, §9 checklist (both bullets), §11 CPM line and MassTransit row, all as the plan gives them. |

No Postman change: no endpoint, route or contract changed. No migration: `has-pending-model-changes` is clean.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Remove `UpdationDate = DateTimeOffset.UtcNow;` from `Lesson.MoveTo` (one of the 18). D9's criterion is "no test pins the in-memory value". | `Integration/ContentRetrieval/LessonContentReindexTests.StaleIds_LessonTouchedAfterIndexing_IsListed` touches the lesson through `ContentRetrievalTestData.TouchLessonAsync`, which calls `lesson.MoveTo(...)` and saves through the raw `AppDbContext`. That bypasses the repository, so with the line removed `UpdationDate` never changes and the test fails. | Kept the line in `Lesson.MoveTo`, applying the plan's own D9 rule. 17 lines are removed and 15 kept. I did not edit the test or its helper. |
| D13 / `ValidationBehaviour`: keep the single `var context = new ValidationContext<TRequest>(request);` and `foreach` with `ValidateAsync(context, …)`, `failures.AddRange(result.Errors)`. Test row 18 expects `ErrorCode == "A_CODE,B_CODE"`. | FluentValidation accumulates failures on the shared context, so the second validator's `result.Errors` also carries the first one's. The plan's body produced `"A_CODE,A_CODE,B_CODE"`, and row 18 failed. The old `Task.WhenAll` code had the same duplication, so this is a pre-existing bug. | Each validator gets its own context: `validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)`. Everything else follows D13. Codes now appear once each, in validator order. |
| DoD: `dotnet format --verify-no-changes` exits 0. | It fails on main as well, with 100 pre-existing whitespace findings in 24 files (vendored core, plus `Elmanhg.Tests/Builders/SubscriptionBuilder.cs`). | Not fixed, because those files are out of scope. The branch has 74 findings in 23 files, all of them already on main. Removing the commented block fixed `ModelBuilderExtensions.cs`. Details are in Build & test. |
| (Implicit) delete the private or internal `ToMicroseconds` methods. | Each method had a one-line WHY comment directly above it, about timestamptz precision. With the method gone, the comment was left pointing at nothing. | Deleted the 4 orphaned comments along with their methods (`EssayGrade`, `MathStepGrade`, `TrainingExport`, `TeacherThread`). |

## Build & test
- `dotnet clean` then `dotnet build api/Elmanhg.slnx -c Release`: **Build succeeded, 0 errors, 9 warnings.** All 9 are pre-existing nullable warnings in vendored core (CS8618 in `Notification.cs`, `NotificationTemplate.cs` and the `OTP.cs` private constructor; CS8602 in `RequiredValidationExtensions.cs`). There are no warnings in `Elmanhg.*` projects and no NU1510.
- `dotnet test --solution Elmanhg.slnx -c Release --no-build` (Docker 29.6.2, Testcontainers): **Passed. total 5489, failed 0, succeeded 5489, skipped 0** (1m 14s). A filtered run of the 14 new and changed classes gave 84 tests, all passing.
- First run (before the two deviations above): 2 failures. One was `ValidationBehaviourTests.Handle_FailuresFromEveryValidator_ThrowsWithCodesInValidatorOrder` (duplicate codes); the other was `LessonContentReindexTests.StaleIds_LessonTouchedAfterIndexing_IsListed`. Both were fixed as described in Deviations.
- `dotnet list api/Elmanhg.slnx package --vulnerable --include-transitive`: every project reports "has no vulnerable packages". There are 0 "has the following vulnerable" lines.
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build` (the CI command): **"No changes have been made to the model since the last migration."**
- `dotnet format api/Elmanhg.slnx --verify-no-changes`: exit 2. Baseline (HEAD exported to the scratchpad): 100 findings in 24 files. Branch: 74 findings in 23 files. The branch adds no new findings.
- Grep checks: no `2.3.9` or `Azure.Core` in core csproj files. No `Version=` in any core csproj. No `ToMicroseconds(` left in Domain. No `Task.WhenAll` in Core.CQRS. In core, `DateTimeOffset.UtcNow` remains only in `AuditEntity` (D8), `RequestLoggingMiddleware` and `Core.Notifications/**`. All 8 listed projects carry the FrameworkReference. No `#<number>` in added lines. The commented-code grep matches only prose WHY comments that contain a semicolon; no commented-out code is left.

## Notes for review
- After the move, `docs/content-retrieval.md` §52 ("Publish, content edits, reorder … stamp `UpdationDate`") is still true, because the repository now does the stamping. No change was needed.
- SKILL.md §4.2 still says "Guard THEN mutate THEN stamp." The plan did not list it. It still reads correctly (stamp the received instant or `UpdatedBy`), so I left it.
- `ICurrentUserService` substitutes in the existing tests keep working through the inherited `UserId`/`UserName` members. No existing test used `NationalId` or `LoginType`.
- `Lesson.MoveTo` is the only listed method still stamping in memory. If the reviewer would rather have the line removed, the test helper `ContentRetrievalTestData.TouchLessonAsync` must save through `ILessonRepository`. That is a test-infrastructure change I did not make on my own.
- Line endings: some `sed` edits turned CRLF working files into LF. I normalised every touched text file back to CRLF in the working copy (`core.autocrlf=true`), so commits stay LF and the diffs contain content changes only.
- Lane boundary: `Application/Shared/RuntimeSettings`, `Application/Configuration`, `Domain/RuntimeSettings` and `Infrastructure/RuntimeSettings` show no changes.
