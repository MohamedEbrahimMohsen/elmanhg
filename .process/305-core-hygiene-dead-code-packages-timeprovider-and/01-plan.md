# Plan — Core hygiene: dead code, packages, TimeProvider and audit actor stamping (E21.S2)

## Goal
The vendored core builds from its own direct references, under the repo's central package management, with no deprecated ASP.NET Core 2.x packages. Core reads time from the injected `TimeProvider` (OTP verify/consume, audit rows, repository stamps, JWT and refresh-token expiry). `Repository.SaveChangesAsync` stamps `CreatedBy`/`UpdatedBy` from a tiny `Core.DDD` `ICurrentUser`, and stamps `UpdationDate` only when the aggregate did not. So a transition that stamps its own instant keeps it, and 18 redundant `UpdationDate = DateTimeOffset.UtcNow` lines leave the Domain. `Elmanhg.Domain` no longer pulls the ASP.NET Core shared framework, because the pure time helpers move to `Core.DDD.Time`. Dead and Morabh-only code is deleted, validators run one at a time, the OTP phone rules come only from configuration, and every core `await` has `ConfigureAwait(false)`.

## Scope
**In:** all 7 sub-tasks of the story, plus the folded leftovers: the `ToMicroseconds` copies in `EssayGrade`, `MathStepGrade`, `TrainingExport` and the `TeacherThread.ToMicroseconds` pass-through; the Domain layering (time helpers move to `Core.DDD`); `Otp.Verify`/`MarkUsed` on injected time; the `FileSignature.Create` zero-pattern guard; the SKILL.md soft-delete checklist wording.
**Out:** `CoreDbContext` is unchanged (dev decision 2026-10-06). `Core.Notifications` stays (only its dead `NotificationTemplateClaimsChecker`, commented-out code, package references and `ConfigureAwait` change; its own `DateTimeOffset.UtcNow` uses are not in the story's clock list). `Application/Shared/RuntimeSettings`, the Configuration slices, `Elmanhg.Domain/RuntimeSettings/*` and `Elmanhg.Infrastructure/RuntimeSettings/*` are left alone (parallel lane #312). Refresh-token rotation is out (story #311). `AuditBehaviour`'s `errorCode` expression is unchanged. `RequestLoggingMiddleware`'s duration clock is unchanged.
**Deferred:** none.

**Precondition:** the branch contains E21.S4 (PR 321): `api/core-libraries/Core.Queues/SweepWorker.cs` and `api/core-libraries/Core.DDD/Models/RetrySchedule.cs` exist. If they do not, run `git merge origin/main` before starting. Every line reference below is to main-after-321.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Where do the pure time helpers go so Domain drops ASP.NET? | Move `Core.Utilities/Time/*` to `Core.DDD/Time/*`, namespace `Core.DDD.Time`. `Elmanhg.Domain.csproj` drops its `Core.Utilities` reference. `Core.Utilities` keeps `IGenerator` and `AddValidatedOptions`. | `Core.DDD` has no framework reference (MediatR + Core.Errors only) and Domain already references it. No new project. Splitting Core.Utilities would add a project for 2 files. |
| D2 | Shape of `ICurrentUser` ("id only") | `Core.DDD.Identity.ICurrentUser { Guid? UserId; string? UserName; string? Role; }`. `ICurrentUserService : ICurrentUser`. | `AuditBehaviour` writes `ActorUserName` and `ActorRole` today. An id-only port would change audit rows, and the rules forbid behaviour change. Three read-only members keep it tiny. `Role` replaces `GetClaim(ClaimTypes.Role)`, so `Core.Auditing` needs no claim knowledge. |
| D3 | How does the repository get the actor and the clock? | `Repository<T>(DbContext context, ICurrentUser? currentUser = null, TimeProvider? timeProvider = null)`. App repositories take `(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider)` and pass both. Core repositories take optional `(TContext context, ICurrentUser? currentUser = null, TimeProvider? timeProvider = null)`. | Explicit constructor injection, the same pattern as today's optional `ICurrentUserService?`. MS DI fills optional parameters when they are registered. `CoreDbContext` stays untouched. |
| D4 | `RuntimeSettingOverrideRepository` | Not changed. It keeps `Repository<RuntimeSettingOverride>(context)`, so it falls back to `TimeProvider.System` and no actor. | Lane #312 owns that folder. `RuntimeSettingOverride` stamps `UpdatedBy`/`UpdationDate` itself, so nothing is lost. |
| D5 | What does "not already set" mean for each stamp? | Added: `CreationDate = now` (as today, now from `TimeProvider`), `CreatedBy ??= actor`, `UpdatedBy ??= actor`. Modified/Deleted: `UpdationDate = now` only when `!entry.Property(x => x.UpdationDate).IsModified`. `UpdatedBy = actor` only when `actor is not null && !entry.Property(x => x.UpdatedBy).IsModified`. | `ChangeTracker.Entries()` runs `DetectChanges`, so `IsModified` shows whether the aggregate assigned the value in this unit of work. A null actor (workers, anonymous webhooks) never erases an existing `UpdatedBy`. |
| D6 | Stamp `IAuditEntity` (`User`) too? | No. The loop stays on `Entries<AuditEntity>()`. | `User` is saved by `UserManager` (store `SaveChanges`), not by the repository, and stamps itself. No behaviour change. |
| D7 | Truncate the repository stamp to microseconds? | No. `now = timeProvider.GetUtcNow()` is used as is. | Same precision as today's `DateTimeOffset.UtcNow`. No change in behaviour. |
| D8 | `AuditEntity` property defaults (`= DateTimeOffset.UtcNow`) | Kept. | Not feasible: a `Core.DDD` entity has no DI. The repository overwrites `CreationDate` on insert, and the default only seeds `UpdationDate` for a new row (unchanged). |
| D9 | Which Domain `UpdationDate = DateTimeOffset.UtcNow` lines are removed? | 18 lines where the method always changes a mapped scalar on the same entity, the entity is an `AuditEntity` saved through a repository, no test pins the in-memory value, and the method is never called from a domain-event handler (verified: handlers only add training records or clear a cache). The 14 kept lines are listed in "Existing code touched" with the reason. | "Only where the repository stamp is equivalent." No-op paths (`Rename` with the same name, `FlagForReview` with the same reason, `Lesson.Update`/`Question.Update` changing only owned/child data, `ReviewSession.RecordOpening` adding a child) would lose their stamp, so they keep it. `User` is not an `AuditEntity`. `ExamBlueprint`/`ExamPeriod`/`Question` tests pin the in-memory stamp. `RuntimeSettingOverride` belongs to lane #312. |
| D10 | OTP phone defaults | `PhoneCodes = []`, `PhoneLength` has no initialiser, with `[MinLength(1)]` and `[Range(1, int.MaxValue)]`. `AddCoreOtp` adds `services.AddOptions<OtpOptions>().ValidateDataAnnotations().ValidateOnStart();`. `ApiFactory` gets the example's keys. | Config-only, and a missing key fails at start instead of rejecting every phone number. An empty default also fixes the binder appending configured codes to the Egyptian defaults (today the list holds 7 entries). `appsettings.example.json` (baked into the image by the Dockerfile) already sets both keys. |
| D11 | Signatures for OTP time | `Otp.Verify(string codeHash, DateTimeOffset now)`, `Otp.MarkUsed(DateTimeOffset now)`, `OtpRepositoryExtensions.ConsumeAsync(..., string invalidErrorCode, DateTimeOffset now, CancellationToken cancellationToken)`. `VerifyOTPHandler` and the 3 app handlers that lack it get `TimeProvider`. | Same shape as `Otp.Reissue(..., DateTimeOffset now)` and `Otp.Create(..., now)`. |
| D12 | `FileSignature.Create` with zero patterns | `ArgumentOutOfRangeException.ThrowIfZero(patterns.Length, nameof(patterns));` in `Create`. | Fails at definition time instead of `InvalidOperationException` from `Max()` at upload time. No new exception type. |
| D13 | Sequential validators | `foreach` over `validators`, `await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false)`, collecting `result.Errors` in order. | Story text, plus SKILL §1 prohibits `Task.WhenAll` over a shared `DbContext`. |
| D14 | Central package management for core | Yes. Delete `api/core-libraries/Directory.Packages.props`. Strip every `Version=` from core csproj files. Add the missing `PackageVersion`s to `api/Directory.Packages.props`, with versions exactly as core pins them today, except `Microsoft.Extensions.Options` (10.0.7 → 10.0.5). `core-libraries/Directory.Build.props` stays, so core is still not under `TreatWarningsAsErrors`. | Feasible: every core version already matches the central one where both exist (MediatR 14.1.0, Identity.EFCore 10.0.5, Caching.Memory 10.0.5, ClosedXML 0.105.1, Http.Resilience 10.7.0, OpenTelemetry 1.19.x). Aligning `Microsoft.Extensions.*` means one 10.0.5 line for every package outside the shared framework. |
| D15 | Packages inside `Microsoft.AspNetCore.App` | Every project that needs HTTP/ASP.NET types gets `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. That project drops its `Microsoft.AspNetCore.Http.Abstractions 2.3.9`, `Microsoft.AspNetCore.Localization 2.3.9` and every `Microsoft.Extensions.*` package that ships in the shared framework. | The 2.3.9 packages are deprecated. Framework-shipped packages are pruned by the .NET 10 SDK (NU1510) and only add version drift. |
| D16 | `ConfigureAwait(false)` coverage | Every `await` in `api/core-libraries` except `await using`/`await foreach`, including middleware and minimal-API endpoint lambdas. | Constitution §1.4: "on every await outside test method bodies". |
| D17 | `ILocalizationManager` | Delete `ILocalizationManager`, `LocalizationManager`, their registration and `LocalizationManagerTests`. | Nothing consumes it (grep). `LocalizedTextExtensions.Localized()` is the live path. |
| D18 | Core.Identity `ErrorCodes.UserIsRequired`/`PasswordIsRequired` | Delete both constants. Keep `UserCreationFailed` (used by `JwtTokenService`). | Only the deleted `Register/*` used them. The app has its own constants. |
| D19 | Test clock | `Substitute.For<TimeProvider>()` with `GetUtcNow().Returns(Now)`. | The repo's existing pattern. `FakeTimeProvider`'s package is not referenced, and the story forbids new packages. |
| D20 | `JwtTokenService` | Also gets `TimeProvider` (token `expires` and the refresh-ticket expiry check). | Same `UtcNow` expiry logic as `RefreshTokenService` in the same project. Story #311 builds on this. |

## Existing code touched
All paths are under `api/` unless they start with `docs/` or `.claude/`.

### Core projects (csproj + CPM)
| File | Change |
|------|--------|
| `core-libraries/Directory.Packages.props` | **Delete.** |
| `Directory.Packages.props` | New `<ItemGroup Label="Core libraries">` with `PackageVersion`: `FluentValidation` 12.1.1, `FluentValidation.DependencyInjectionExtensions` 12.1.1, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.5, `Microsoft.EntityFrameworkCore` 10.0.5, `Microsoft.EntityFrameworkCore.Relational` 10.0.5, `Microsoft.Extensions.Hosting` 10.0.5, `Microsoft.Extensions.Options` 10.0.5, `Nanoid` 3.1.0, `AWSSDK.S3` 4.0.103.3, `FirebaseAdmin` 3.5.0, `Google.Apis.Auth` 1.74.0, `Serilog` 4.3.1, `Serilog.AspNetCore` 10.0.0, `Serilog.Enrichers.Environment` 3.0.1, `Serilog.Extensions.Logging` 10.0.0, `Serilog.Sinks.Async` 2.1.0, `Serilog.Sinks.ApplicationInsights` 4.0.0, `Serilog.Sinks.AzureTableStorage` 10.2.0, `Serilog.Sinks.Console` 6.1.1, `Serilog.Sinks.File` 7.0.0, `Serilog.Sinks.Seq` 9.0.0. No other edits. |
| every `core-libraries/*/*.csproj` | Remove every `Version="…"` attribute from `PackageReference`. This includes `Core.Queues` (MediatR), `Core.Http`, `Core.Storage`, `Core.Observability`, `Core.Validation` and `Core.Hosting`, whose other content is unchanged. |
| `core-libraries/Core.Auditing/Core.Auditing.csproj` | Packages: `MediatR`, `Microsoft.Extensions.Hosting`. Projects: `Core.DDD`, `Core.Errors` (**remove** `Core.Identity`). |
| `core-libraries/Core.CQRS/Core.CQRS.csproj` | Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. Packages: `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`, `MediatR`. Projects: `Core.Localization` only (**remove** `Core.Utilities`). |
| `core-libraries/Core.Cache/Core.Cache.csproj` | Packages: `MediatR`, `Microsoft.Extensions.Caching.Memory` (versionless). |
| `core-libraries/Core.EntityFrameworkCore/Core.EntityFrameworkCore.csproj` | Add FrameworkReference. Packages: `MediatR`, `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Relational` (**remove** `Microsoft.AspNetCore.Http.Abstractions`). Project refs unchanged. |
| `core-libraries/Core.Exceptions/Core.Exceptions.csproj` | Add FrameworkReference. **Remove** all 3 `PackageReference`s. Project refs unchanged. |
| `core-libraries/Core.Identity/Core.Identity.csproj` | Add FrameworkReference. Packages: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore` (**remove** `FluentValidation`, `MediatR`, `Microsoft.Extensions.DependencyInjection`). Projects: `Core.DDD`, `Core.Errors` (**remove** `Core.Validation`). |
| `core-libraries/Core.Localization/Core.Localization.csproj` | Add FrameworkReference. **Remove** all 4 `PackageReference`s. Project ref `Core.DDD` unchanged. |
| `core-libraries/Core.Logging/Core.Logging.csproj` | Add FrameworkReference. **Remove** `Microsoft.AspNetCore.Http.Abstractions` and `Microsoft.Extensions.Hosting`. Keep the 10 Serilog packages, versionless. |
| `core-libraries/Core.Notifications/Core.Notifications.csproj` | Add FrameworkReference. Packages: `FirebaseAdmin`, `Google.Apis.Auth`, `FluentValidation`, `MediatR`. Project refs unchanged. |
| `core-libraries/Core.OTP/Core.OTP.csproj` | Add FrameworkReference. Packages: `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`, `MediatR` (**remove** `Microsoft.Extensions.Configuration.Abstractions` and `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.9). Project refs unchanged. |
| `core-libraries/Core.Spreadsheets/Core.Spreadsheets.csproj` | Packages: `ClosedXML`, `Microsoft.Extensions.Options` (versionless, central 10.0.5). |
| `core-libraries/Core.Utilities/Core.Utilities.csproj` | **Remove** `Azure.Core`. Keep FrameworkReference and `Nanoid`. |
| `Elmanhg.Domain/Elmanhg.Domain.csproj` | **Remove** the `Core.Utilities` `ProjectReference`. |

### Dead code
| File | Change |
|------|--------|
| `core-libraries/Core.Identity/CoreAuthEndpoints.cs` | **Delete.** |
| `core-libraries/Core.Identity/Register/RegisterCommand.cs`, `RegisterHandler.cs`, `RegisterResult.cs`, `RegisterValidator.cs` | **Delete** (whole folder). |
| `core-libraries/Core.Identity/Requests/RegisterRequest.cs` | **Delete** (whole folder). |
| `core-libraries/Core.Identity/Exceptions/ErrorCodes.cs` | Delete `UserIsRequired` and `PasswordIsRequired`. |
| `core-libraries/Core.Identity/DependencyInjection.cs` | Delete the 3 commented `//services.Add…` lines and the trailing `//,Action<JwtBearerOptions>? jwtOptions = null` comment. Add `services.TryAddSingleton(TimeProvider.System);` and `services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<ICurrentUserService>());` right after the `ICurrentUserService` registration (`using Core.DDD.Identity; using Microsoft.Extensions.DependencyInjection.Extensions;`). |
| `core-libraries/Core.Logging/RequestLog.cs` | **Delete** (stray `Extensions.Core.Logging` namespace, unused). |
| `core-libraries/Core.Logging/DependencyInjection.cs` | Remove `"Latitude", "Longitude", ` from `azureRequestLogPropertyColumns`. |
| `core-libraries/Core.CQRS/Exceptions/ErrorCodes.cs` | **Delete.** |
| `core-libraries/Core.CQRS/DependencyInjection.cs` | Remove `using System.Timers;`. |
| `core-libraries/Core.Localization/LocalizationOptions.cs` | **Delete.** |
| `core-libraries/Core.Localization/ILocalizationManager.cs`, `LocalizationManager.cs` | **Delete.** |
| `core-libraries/Core.Localization/DependencyInjection.cs` | Remove `services.AddScoped<ILocalizationManager, LocalizationManager>();`. |
| `core-libraries/Core.Notifications/Templates/Shared/NotificationTemplateClaimsChecker.cs` | **Delete** (folder becomes empty, remove it). |
| `core-libraries/Core.Notifications/Templates/AddNotificationTemplate/AddNotificationTemplateHandler.cs`, `GetNotificationTemplate/GetNotificationTemplateQueryHandler.cs`, `ListNotificationTemplates/ListNotificationTemplatesQueryHandler.cs` | Remove `using Core.Notifications.Templates.Shared;`. |
| `core-libraries/Core.Notifications/RegisterUserDevice/RegisterUserDeviceValidator.cs` | Delete the 6 commented `//RuleFor(x => x.Platform)…` lines. |
| `core-libraries/Core.EntityFrameworkCore/Context/ModelBuilderExtensions.cs` | Delete the commented-out `//public static void ConfigureLocalized…` block (the 30 lines from `//public static void` to the closing `//}`). |
| `core-libraries/Core.EntityFrameworkCore/Context/SeedLocalizedExtensions.cs` | `namespace Core.EntityFramework.Context;` → `namespace Core.EntityFrameworkCore.Context;`. |
| `core-libraries/Core.Identity/Tokens/AccessToken/JwtTokenService.cs` | Remove `using MediatR;`, `using System.Timers;`, `using System.Xml.Linq;`. |

### Time helpers move (D1) and `ToMicroseconds` leftovers
| File | Change |
|------|--------|
| `core-libraries/Core.Utilities/Time/DateTimeOffsetExtensions.cs`, `TimeZoneInfoExtensions.cs` | **Delete** (moved, see C1/C2). |
| `Elmanhg.Domain/Avatar/AvatarConversation.cs`, `Sessions/Session.cs`, `Session.Answering.cs`, `Session.Essays.cs`, `Session.Exam.cs`, `Session.ExamSubmission.cs`, `Session.Submission.cs`, `SlaCalendars/SlaCalendar.cs`, `TeacherThreads/TeacherThread.cs`, `TeacherThread.FollowUps.cs`, `TeacherThread.Replies.cs`, `TeacherThread.Sla.cs`, `TeacherThreadOutOfAppReminder.cs`, `TeacherThreadSlaEvent.cs` | `using Core.Utilities.Time;` → `using Core.DDD.Time;`. |
| `Elmanhg.Application/Avatar/Shared/AvatarGate.cs`, `Dashboard/Shared/DashboardWindow.cs`, `Mastery/GetMasteryOverview/GetMasteryOverviewHandler.cs`, `Subscriptions/Shared/FreeTierGate.cs`, `TeacherThreads/Shared/AskTeacherGate.cs` | `using Core.Utilities.Time;` → `using Core.DDD.Time;`. |
| `Elmanhg.Domain/TeacherThreads/TeacherThread.cs` | Delete `internal static DateTimeOffset ToMicroseconds(DateTimeOffset value) => value.TruncateToMicroseconds();`. |
| `Elmanhg.Domain/TeacherThreads/TeacherVoiceDraft.cs` | Add `using Core.DDD.Time;`. Each `TeacherThread.ToMicroseconds(x)` becomes `x.TruncateToMicroseconds()` (4 sites: `recordedAt`, `transcribedAt`, `failedAt`, `sentAt`). |
| `Elmanhg.Domain/EssayGrading/EssayGrade.cs` | Add `using Core.DDD.Time;`. Delete `private static DateTimeOffset ToMicroseconds(...)`. `ToMicroseconds(requestedAt)` becomes `requestedAt.TruncateToMicroseconds()`. |
| `Elmanhg.Domain/EssayGrading/EssayGrade.Grading.cs`, `EssayGrade.Review.cs` | Add `using Core.DDD.Time;`. Each `ToMicroseconds(x)` becomes `x.TruncateToMicroseconds()`. |
| `Elmanhg.Domain/MathStepGrading/MathStepGrade.cs` | Same as `EssayGrade.cs` (delete the private method, convert `requestedAt`). |
| `Elmanhg.Domain/MathStepGrading/MathStepGrade.Grading.cs`, `MathStepGrade.Review.cs` | Add using, convert every `ToMicroseconds(x)` (includes `UpdationDate = checkedAt.TruncateToMicroseconds();`). |
| `Elmanhg.Domain/TrainingExports/TrainingExport.cs` | Add using, delete the private method, convert `requestedAt`, `from`, `to`. |
| `Elmanhg.Domain/TrainingExports/TrainingExport.Lifecycle.cs` | Add using, convert every `ToMicroseconds(x)` (`startedAt`, `completedAt`, `failedAt`, `expiredAt`, `discardedAt`). |

### ICurrentUser, clock, actor stamping
| File | Change |
|------|--------|
| `core-libraries/Core.Identity/Tokens/CurrentUser/ICurrentUserService.cs` | `public interface ICurrentUserService : ICurrentUser { string? PhoneNumber { get; } long? CreatedAtUnixTimeSeconds { get; } string? GetClaim(string claimName); }` (`using Core.DDD.Identity;`). `UserId`/`UserName` are inherited. `NationalId` and `LoginType` are removed. |
| `core-libraries/Core.Identity/Tokens/CurrentUser/CurrentUserService.cs` | Remove `NationalIdClaimType`, `LoginTypeClaimType`, `NationalId` and `LoginType`. Add `public string? Role => User?.FindFirst(ClaimTypes.Role)?.Value;`. |
| `core-libraries/Core.Identity/Tokens/RefreshToken/RefreshTokenService.cs` | Ctor `(SignInManager<TUser> signInManager, IOptionsMonitor<BearerTokenOptions> bearerOptions, IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider)`. `ExpiresUtc = timeProvider.GetUtcNow().AddDays(_jwt.RefreshTokenExpirationDays)`. Expiry check `< timeProvider.GetUtcNow()`. |
| `core-libraries/Core.Identity/Tokens/AccessToken/JwtTokenService.cs` | Ctor `(SignInManager<TUser> signInManager, IOptions<JwtOptions> jwtOptions, IOptionsMonitor<BearerTokenOptions> bearerOptions, TimeProvider timeProvider)`. `expires: timeProvider.GetUtcNow().AddHours(_jwt.ExpirationHours).UtcDateTime`. Expiry check `< timeProvider.GetUtcNow()`. `await signInManager.ValidateSecurityStampAsync(ticket.Principal).ConfigureAwait(false)`. |
| `core-libraries/Core.Auditing/AuditBehaviour.cs` | Ctor `(IAuditLogRepository auditLogRepository, IAuditChangeCollector auditChangeCollector, ICurrentUser currentUser, TimeProvider timeProvider, ILogger<AuditBehaviour<TRequest, TResponse>> logger)`. `timestamp: timeProvider.GetUtcNow()`, `actorUserId: currentUser.UserId`, `actorUserName: currentUser.UserName`, `actorRole: currentUser.Role`. Usings: remove `Core.Identity.Tokens.CurrentUser` and `System.Security.Claims`, add `Core.DDD.Identity`. |
| `core-libraries/Core.Auditing/DependencyInjection.cs` | Add `services.TryAddSingleton(TimeProvider.System);` before the `if`. |
| `core-libraries/Core.EntityFrameworkCore/Repositories/Repository.cs` | See "Domain behaviour → Repository". Ctor `Repository<T>(DbContext context, ICurrentUser? currentUser = null, TimeProvider? timeProvider = null)`. Fields `protected readonly ICurrentUser? _currentUser = currentUser;` and `protected readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;` (remove `_currentUserService`). Delete the commented-out old `SaveChangesAsync` and the unused `var events = …`. Usings: `Core.Identity.Tokens.CurrentUser` → `Core.DDD.Identity`. |
| `core-libraries/Core.EntityFrameworkCore/Repositories/AuditLogRepository.cs`, `NotificationRepository.cs`, `NotificationTemplateRepository.cs`, `OtpRepository.cs`, `UserDeviceRepository.cs` | Primary ctor `(TContext context, ICurrentUser? currentUser = null, TimeProvider? timeProvider = null)` and base `Repository<X>(context, currentUser, timeProvider)`. Add `using Core.DDD.Identity;`. |
| the 33 app repositories (every file in the `: Repository<` list except `Elmanhg.Infrastructure/RuntimeSettings/RuntimeSettingOverrideRepository.cs`): `Analytics/FunnelEventRepository.cs`, `Analytics/UserActivityDayRepository.cs`, `Avatar/AvatarConversationRepository.cs`, `Avatar/AvatarMessageUsageRepository.cs`, `ContentRetrieval/LessonContentChunkRepository.cs`, `ContentRetrieval/LessonContentIndexRepository.cs`, `EssayGrading/EssayGradeRepository.cs`, `ExamBlueprints/ExamBlueprintRepository.cs`, `Identity/IssuedRefreshTokenRepository.cs`, `Identity/UserRepository.cs`, `Lessons/LessonOpeningRepository.cs`, `Lessons/LessonRepository.cs`, `Mastery/QuestionMasteryRepository.cs`, `MathStepGrading/MathStepGradeRepository.cs`, `Questions/QuestionImportBatchRepository.cs`, `Questions/QuestionRepository.cs`, `ReviewSessions/ReviewSessionRepository.cs`, `Sessions/SessionRepository.cs`, `SlaCalendars/ExamPeriodRepository.cs`, `Subjects/SubjectRepository.cs`, `Subscriptions/PaymentRepository.cs`, `Subscriptions/SubscriptionRepository.cs`, `Teachers/TeacherSubjectRepository.cs`, `TeacherThreads/TeacherThreadOutOfAppReminderRepository.cs`, `TeacherThreads/TeacherThreadRepository.cs`, `TeacherThreads/TeacherThreadSlaEventRepository.cs`, `TeacherThreads/TeacherVoiceDraftRepository.cs`, `TrainingData/AttemptTrainingRecordRepository.cs`, `TrainingData/AvatarTrainingRecordRepository.cs`, `TrainingData/EssayGradeTrainingRecordRepository.cs`, `TrainingData/TeacherThreadTrainingRecordRepository.cs`, `TrainingExports/TrainingExportRepository.cs`, `Units/CurriculumUnitRepository.cs` (all under `Elmanhg.Infrastructure/`) | `(AppDbContext context)` → `(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider)`. `: Repository<X>(context)` → `: Repository<X>(context, currentUser, timeProvider)`. Add `using Core.DDD.Identity;`. Body unchanged. |

### OTP
| File | Change |
|------|--------|
| `core-libraries/Core.OTP/Entities/OTP.cs` | `Verify(string codeHash, DateTimeOffset now)` and `MarkUsed(DateTimeOffset now)`: each `ExpiresAt <= DateTimeOffset.UtcNow` becomes `ExpiresAt <= now`. Nothing else changes. |
| `core-libraries/Core.OTP/Repositories/OtpRepositoryExtensions.cs` | `ConsumeAsync(this IOtpRepository otpRepository, Guid verificationId, OtpRecipientType recipientType, string invalidErrorCode, DateTimeOffset now, CancellationToken cancellationToken)`. Calls `otp.MarkUsed(now)`. |
| `core-libraries/Core.OTP/VerifyOTP/VerifyOTPHandler.cs` | Ctor `(IOtpRepository otpRepository, IOtpHasher otpHasher, TimeProvider timeProvider)`. `otp.Verify(codeHash, timeProvider.GetUtcNow())`. |
| `core-libraries/Core.OTP/OtpOptions.cs` | `[MinLength(1)] public List<string> PhoneCodes { get; set; } = [];` and `[Range(1, int.MaxValue)] public int PhoneLength { get; set; }` (`using System.ComponentModel.DataAnnotations;`). |
| `core-libraries/Core.OTP/DependencyInjection.cs` | After `services.Configure<OtpOptions>(…)` add `services.AddOptions<OtpOptions>().ValidateDataAnnotations().ValidateOnStart();`. |
| `Elmanhg.Application/Auth/AcceptInvitation/AcceptInvitationHandler.cs`, `LoginWithEmailCode/LoginWithEmailCodeHandler.cs`, `LoginWithPhone/LoginWithPhoneHandler.cs` | Append `TimeProvider timeProvider` as the last ctor parameter. `ConsumeAsync(request.VerificationId, OtpRecipientType.X, ErrorCodes.OtpInvalid, timeProvider.GetUtcNow(), cancellationToken)`. |
| `Elmanhg.Application/Auth/RegisterWithPhone/RegisterWithPhoneHandler.cs` | Read the clock once at the start: `var now = timeProvider.GetUtcNow();`. Pass it to `ConsumeAsync(…, now, cancellationToken)` and `user.AcceptTerms(request.TermsVersion, now)`. |

### Validation, CQRS, ConfigureAwait
| File | Change |
|------|--------|
| `core-libraries/Core.Validation/Files/FileSignature.cs` | `Create` becomes a block body: `ArgumentOutOfRangeException.ThrowIfZero(patterns.Length, nameof(patterns)); return new([.. extensions.Select(extension => extension.ToLowerInvariant())], patterns);`. |
| `core-libraries/Core.CQRS/Behaviours/ValidationBehaviour.cs` | Body per D13: `var failures = new List<ValidationFailure>(); foreach (var validator in validators) { var result = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false); failures.AddRange(result.Errors); }`, then the existing message/code loop and throw. `return await next(cancellationToken).ConfigureAwait(false);`. Add `using FluentValidation.Results;`. |
| `core-libraries/Core.Exceptions/ExceptionMiddleware.cs` | `.ConfigureAwait(false)` on its 3 awaits. |
| `core-libraries/Core.Logging/RequestLoggingMiddleware.cs` | `.ConfigureAwait(false)` on `await _next(context)`. |
| `core-libraries/Core.Notifications/Endpoints/NotificationEndpoints.cs`, `NotificationTemplateEndpoints.cs`, `core-libraries/Core.OTP/Endpoints/OTPEndpoints.cs` | `.ConfigureAwait(false)` on every `await mediator.Send(...)`. |
| `core-libraries/Core.Notifications/ListNotifications/ListNotificationsQueryHandler.cs` | `.ConfigureAwait(false)` on both `FindPaginatedAsync` awaits. |
| `core-libraries/Core.Notifications/MarkNotificationAsRead/MarkNotificationAsReadHandler.cs` | `.ConfigureAwait(false)` on `GetByIdAsync`. |
| `core-libraries/Core.Notifications/RegisterUserDevice/RegisterUserDeviceHandler.cs` | `(await userDevicesRepository.FindAsync(…, cancellationToken).ConfigureAwait(false)).FirstOrDefault()`. |
| `core-libraries/Core.Notifications/Services/FirebaseNotificationService.cs` | `.ConfigureAwait(false)` on `SubscribeToTopicAsync` and `UnsubscribeFromTopicAsync`. |

### Domain `UpdationDate` cleanup (D9)
**Remove** the line `UpdationDate = DateTimeOffset.UtcNow;` from these 18 methods (nothing else in them changes):
| File | Methods |
|------|---------|
| `Elmanhg.Domain/Lessons/Lesson.cs` | `MoveTo` |
| `Elmanhg.Domain/Lessons/Lesson.Lifecycle.cs` | `Publish`, `Unpublish`, `Archive` |
| `Elmanhg.Domain/Lessons/LessonObjective.cs` | `Update` |
| `Elmanhg.Domain/Mastery/QuestionMastery.cs` | `Record` |
| `Elmanhg.Domain/Subjects/Subject.cs` | `MoveTo` |
| `Elmanhg.Domain/Units/CurriculumUnit.cs` | `MoveTo` |
| `Elmanhg.Domain/Subscriptions/Payment.cs` | `MarkSucceeded`, `MarkFailed`, `LinkProviderOrder` |
| `Elmanhg.Domain/Subscriptions/Payment.Refund.cs` | `MarkRefunded`, `ResolveReview` |
| `Elmanhg.Domain/Subscriptions/Subscription.Lifecycle.cs` | `Renew`, `MarkPastDue`, `Cancel`, `Expire` |
| `Elmanhg.Domain/Subscriptions/Subscription.Refund.cs` | `RevokePaidPeriod` |

**Keep** (14), not equivalent: `ExamBlueprint.Update` (test pins the value; `Apply(shape)` may change only owned data). `User.Suspend`, `User.Reactivate`, `User.SetContactPhoneNumber` (`User` is not an `AuditEntity`; saved by `UserManager`). `Lesson.Update` (may change only objectives/owned text). `Question.Update`, `Question.Resubmit` (tests pin; a no-op edit still touches). `ReviewSession.RecordOpening` (adds a child only). `RuntimeSettingOverride.Override`/`Reset` (lane #312). `ExamPeriod.Update` (tests pin). `Subject.Rename`, `CurriculumUnit.Rename` (same-name no-op). `Payment.FlagForReview` (same-reason no-op).

### Tests and docs touched
| File | Change |
|------|--------|
| `Elmanhg.Tests/Core/Utilities/DateTimeOffsetExtensionsTests.cs`, `TimeZoneInfoExtensionsTests.cs` | **Delete** (moved to T1/T2 with identical test bodies). |
| `Elmanhg.Tests/Core/Localization/LocalizationManagerTests.cs` | **Delete** (D17). |
| `Elmanhg.Tests/Core/Auditing/AuditBehaviourTests.cs` | `ICurrentUserService _currentUserService` → `ICurrentUser _currentUser` (`Substitute.For<ICurrentUser>()`). `.Role.Returns("Admin")` replaces the `GetClaim` setup. Add `TimeProvider _timeProvider` returning `Now = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)`. `Behaviour()` passes `(_auditLogRepository, _auditChangeCollector, _currentUser, _timeProvider, NullLogger…)`. Remove `using Core.Identity.Tokens.CurrentUser; using System.Security.Claims;`. Add Test-plan row 20. |
| `Elmanhg.Tests/Builders/OtpBuilder.cs` | `otp.Verify(CodeHash, _issuedAt);`. |
| `Elmanhg.Tests/Core/Otp/OtpTests.cs` | Every builder used with `Verify`/`MarkUsed` gets `.IssuedAt(Now)`. Every `Verify(x)` becomes `Verify(x, Now)`, and every `MarkUsed()` / `otp.MarkUsed` method group becomes `otp.MarkUsed(Now)` / `() => otp.MarkUsed(Now)`. Add Test-plan rows 13, 14. |
| `Elmanhg.Tests/Core/Otp/OtpRepositoryExtensionsTests.cs` | Add `private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);`. Builders get `.IssuedAt(Now)`. Every `ConsumeAsync(…, InvalidCode, TestContext.Current.CancellationToken)` gets `Now` before the token. Add Test-plan row 15. |
| `Elmanhg.Tests/Core/Otp/VerifyOTPHandlerTests.cs` | Add `Now` (as above) and `TimeProvider _timeProvider = Substitute.For<TimeProvider>()` returning `Now`. Handler `new VerifyOTPHandler(_otpRepository, _otpHasher, _timeProvider)`. Every builder `.IssuedAt(Now)`. Add Test-plan row 16. |
| `Elmanhg.Tests/Application/Features/Auth/AcceptInvitation/AcceptInvitationHandlerTests.cs`, `LoginWithEmailCode/LoginWithEmailCodeHandlerTests.cs`, `LoginWithPhone/LoginWithPhoneHandlerTests.cs` | Add `Now` (2026-09-01 10:00 UTC) and a `TimeProvider` substitute returning it, passed as the new last ctor arg. `ArrangeOtp` builds `builder.IssuedAt(Now).Build()`. Assertions unchanged. |
| `Elmanhg.Tests/Application/Features/Auth/RegisterWithPhone/RegisterWithPhoneHandlerTests.cs` | `ArrangeOtp` builds `builder.IssuedAt(Now).Build()`. |
| `Elmanhg.Tests/Domain/Mastery/QuestionMasteryTests.cs` | Rename `Record_NewAttempt_StampsUpdatedByAndUpdationDate` → `Record_NewAttempt_StampsUpdatedBy`. Delete its `var before` line and the `UpdationDate` assertion. |
| `Elmanhg.Tests/Integration/Persistence/RepositoryPagingTests.cs` | Both `new EssayGradeRepository(...)` / `new MathStepGradeRepository(...)` get `scope.ServiceProvider.GetRequiredService<ICurrentUser>(), TimeProvider.System` as extra args (`using Core.DDD.Identity;`). |
| `Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | After `["CoreOtp:Secret"] = TestOtpSecret,` add `["CoreOtp:PhoneCodes:0"] = "010"`, `[":1"] = "011"`, `[":2"] = "012"`, `[":3"] = "015"` (full keys `CoreOtp:PhoneCodes:N`), and `["CoreOtp:PhoneLength"] = "11"`. |
| `Elmanhg.Tests/Core/Validation/FileSignatureTests.cs` | Add Test-plan row 17. |
| `docs/constitution.md` | See Definition of done → Docs. |
| `.claude/skills/dotnet-feature/SKILL.md` | See Definition of done → Docs. |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| C1 | `api/core-libraries/Core.DDD/Time/DateTimeOffsetExtensions.cs` | static class | `namespace Core.DDD.Time;` Body identical to the deleted `Core.Utilities` file. Moved, Elmanhg-native (E21.S3). |
| C2 | `api/core-libraries/Core.DDD/Time/TimeZoneInfoExtensions.cs` | static class | `namespace Core.DDD.Time;` Body identical to the deleted file, including its WHY comment. |
| C3 | `api/core-libraries/Core.DDD/Identity/ICurrentUser.cs` | interface | `namespace Core.DDD.Identity; public interface ICurrentUser { Guid? UserId { get; } string? UserName { get; } string? Role { get; } }`. New, no Morabh equivalent. |
| T1 | `api/Elmanhg.Tests/Core/DDD/DateTimeOffsetExtensionsTests.cs` | test | `namespace Elmanhg.Tests.Core.DDD;` `using Core.DDD.Time;` Same methods as the deleted file. |
| T2 | `api/Elmanhg.Tests/Core/DDD/TimeZoneInfoExtensionsTests.cs` | test | Same, moved. |
| T3 | `api/Elmanhg.Tests/Core/Persistence/AuditStampingProbes.cs` | probes | `public sealed class StampedProbe : AuditEntity { public StampedProbe(Guid? createdBy) : base(Guid.NewGuid(), createdBy) { } public string Name { get; set; } = "first"; }` and `public sealed class AuditStampingProbeDbContext() : DbContext(new DbContextOptionsBuilder<AuditStampingProbeDbContext>().UseNpgsql("Host=localhost;Database=audit-stamping-probe").Options) { public DbSet<StampedProbe> Probes { get; set; } public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0); }`. No database is contacted. |
| T4 | `api/Elmanhg.Tests/Core/Persistence/RepositoryAuditStampingTests.cs` | test | Test-plan rows 3–11. Fields: `Now = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)`, `Earlier = Now.AddHours(-3)`, `_actorId`, `ICurrentUser _currentUser` (substitute, `UserId` returns `_actorId`), `TimeProvider _timeProvider` (substitute returning `Now`), `AuditStampingProbeDbContext _context = new()`, `Repository<StampedProbe> Repository() => new(_context, _currentUser, _timeProvider)`. "Modified" arrange: `var probe = new StampedProbe(creator) { UpdationDate = Earlier, UpdatedBy = creator }; _context.Attach(probe);` then mutate. |
| T5 | `api/Elmanhg.Tests/Core/CQRS/ValidationBehaviourTests.cs` | test | Probe types in the same file: `public sealed record ValidationProbeRequest : IRequest<string>;`, `GatedValidator : AbstractValidator<ValidationProbeRequest>` (ctor `(Task gate)`, `RuleFor(x => x).MustAsync(async (_, _) => { await gate; return true; })`), `RecordingValidator` (`public bool Ran { get; private set; }`, `RuleFor(x => x).Must(_ => { Ran = true; return true; })`), `FailingValidator` (ctor `(string code)`, `RuleFor(x => x).Must(_ => false).WithErrorCode(code)`). `ILocalizer` substitute returns its `fallbackMessage` argument. |
| T6 | `api/Elmanhg.Tests/Core/Identity/CurrentUserServiceTests.cs` | test | `new CurrentUserService(accessor)` where `accessor.HttpContext` is a `DefaultHttpContext` with `User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))`. |
| T7 | `api/Elmanhg.Tests/Core/Identity/SignInManagerSubstitute.cs` | helper | `internal static class SignInManagerSubstitute { public static SignInManager<User> Create() => Substitute.For<SignInManager<User>>(UserManagerSubstitute.Create(), Substitute.For<IHttpContextAccessor>(), Substitute.For<IUserClaimsPrincipalFactory<User>>(), null, null, null, null); }` (`using Elmanhg.Tests.Application.Features.Auth;`). |
| T8 | `api/Elmanhg.Tests/Core/Identity/RefreshTokenServiceTests.cs` | test | `BearerTokenOptions { RefreshTokenProtector = _protector }` (`ISecureDataFormat<AuthenticationTicket>` substitute). `IOptionsMonitor<BearerTokenOptions>` substitute `.Get(IdentityConstants.BearerScheme)` returns it. `Options.Create(new JwtOptions { RefreshTokenExpirationDays = 7 })`. Clock returns `Now`. |
| T9 | `api/Elmanhg.Tests/Core/Identity/JwtTokenServiceTests.cs` | test | `JwtOptions { Issuer = "elmanhg-test", Audience = "elmanhg-test", Key = new string('k', 64), ExpirationHours = 2 }`. Same protector and monitor setup as T8. `Now = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)`. |
| T10 | `api/Elmanhg.Tests/Core/Otp/OtpOptionsRegistrationTests.cs` | test | `services.AddCoreOtp(new ConfigurationBuilder().AddInMemoryCollection(values).Build())`, `BuildServiceProvider()`, then read `GetRequiredService<IOptions<OtpOptions>>().Value`. |
| T11 | `api/Elmanhg.Tests/Domain/DomainAssemblyReferencesTests.cs` | test | `typeof(Elmanhg.Domain.Identity.User).Assembly.GetReferencedAssemblies().Select(x => x.Name)`. |

## Domain behaviour

**Repository** (`Core.EntityFrameworkCore/Repositories/Repository.cs`), exact body:
```csharp
public virtual async Task SaveChangesAsync(CancellationToken cancellationToken)
{
    var now = _timeProvider.GetUtcNow();
    var actorId = _currentUser?.UserId;

    foreach (var entry in _context.ChangeTracker.Entries<AuditEntity>())
    {
        if (entry.State == EntityState.Added)
        {
            entry.Entity.CreationDate = now;
            entry.Entity.CreatedBy ??= actorId;
            entry.Entity.UpdatedBy ??= actorId;
        }

        if (entry.State is EntityState.Modified or EntityState.Deleted)
        {
            if (!entry.Property(x => x.UpdationDate).IsModified)
            {
                entry.Entity.UpdationDate = now;
            }

            if (actorId is not null && !entry.Property(x => x.UpdatedBy).IsModified)
            {
                entry.Entity.UpdatedBy = actorId;
            }
        }
    }

    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
```
**Otp:** `Verify(codeHash, now)` keeps its order (attempts++, already verified, expired at `now`, max attempts, hash match). `MarkUsed(now)` keeps its order (not verified, already used, expired at `now`). The only change is `now` replacing `DateTimeOffset.UtcNow`.
**Domain aggregates:** none beyond the 18 deleted lines and the `TruncateToMicroseconds` substitutions. No guard, transition or `BusinessRuleViolationCoreException` changes.
**Migration:** none. `CreatedBy`/`UpdatedBy`/`CreationDate`/`UpdationDate` already exist on every `AuditEntity` table (`AppDbContextModelSnapshot.cs`). `dotnet ef migrations add Probe` must produce an empty diff and must not be committed.

## Error codes
None added. Deleted: `Core.Identity.Exceptions.ErrorCodes.UserIsRequired` and `.PasswordIsRequired` (both unused; the app's own `ErrorCodes.PasswordIsRequired` and its resx entries are untouched). The `Core.CQRS.Exceptions.ErrorCodes` file was fully commented out.

## API surface
No endpoint, route, policy or contract change. Observable changes: `CreatedBy`/`UpdatedBy` are now filled from the caller on rows the aggregate did not stamp. The API refuses to start without `CoreOtp:PhoneCodes`/`CoreOtp:PhoneLength`.

## Test plan
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `DateTimeOffsetExtensionsTests` (T1) | existing methods, moved verbatim | as before, against `Core.DDD.Time` |
| 2 | `TimeZoneInfoExtensionsTests` (T2) | existing methods, moved verbatim | as before |
| 3 | `RepositoryAuditStampingTests` | `SaveChangesAsync_AddedEntity_StampsCreationDateFromTimeProvider` | `probe.CreationDate == Now` after `AddAsync` + save |
| 4 | `RepositoryAuditStampingTests` | `SaveChangesAsync_AddedWithoutCreator_StampsCreatedByAndUpdatedByFromCurrentUser` | `new StampedProbe(null)` → `(CreatedBy, UpdatedBy) == (_actorId, _actorId)` |
| 5 | `RepositoryAuditStampingTests` | `SaveChangesAsync_AddedWithCreator_KeepsCreatedBy` | `new StampedProbe(creator)` → `(CreatedBy, UpdatedBy) == (creator, creator)` |
| 6 | `RepositoryAuditStampingTests` | `SaveChangesAsync_ModifiedWithoutUpdationDate_StampsUpdationDateFromTimeProvider` | attached, `Name = "second"` → `UpdationDate == Now` |
| 7 | `RepositoryAuditStampingTests` | `SaveChangesAsync_ModifiedWithUpdationDateSet_KeepsAggregateInstant` | `Name = "second"; UpdationDate = Earlier.AddMinutes(1)` → still `Earlier.AddMinutes(1)` |
| 8 | `RepositoryAuditStampingTests` | `SaveChangesAsync_ModifiedWithoutUpdatedBy_StampsUpdatedByFromCurrentUser` | `Name = "second"` → `UpdatedBy == _actorId` |
| 9 | `RepositoryAuditStampingTests` | `SaveChangesAsync_ModifiedWithUpdatedBySet_KeepsAggregateActor` | `Name = "second"; UpdatedBy = other` → `UpdatedBy == other` |
| 10 | `RepositoryAuditStampingTests` | `SaveChangesAsync_ModifiedWithoutCurrentUser_LeavesUpdatedByUnchanged` | `_currentUser.UserId` returns `null`, `Name = "second"` → `UpdatedBy == creator`, `UpdationDate == Now` |
| 11 | `RepositoryAuditStampingTests` | `SaveChangesAsync_UnchangedEntity_LeavesStampsUnchanged` | attached, untouched → `(UpdationDate, UpdatedBy) == (Earlier, creator)` |
| 12 | `ValidationBehaviourTests` | `Handle_TwoValidators_RunsSecondOnlyAfterFirstCompletes` | `[Gated(tcs.Task), recording]`. Start `Handle` without awaiting → `recording.Ran` false. `tcs.SetResult()`, await → `Ran` true, and `next` was called |
| 13 | `OtpTests` | `Verify_ExpiredAtGivenTime_ReturnsExpired` | built `.IssuedAt(Now)` (5-min expiry), `Verify(CodeHash, Now.AddMinutes(5))` → `ErrorCodes.OTPExpired`, `IsVerified` false |
| 14 | `OtpTests` | `MarkUsed_ExpiredAtGivenTime_ThrowsExpired` | verified at `Now`, `MarkUsed(Now.AddMinutes(5))` → `BadRequestCoreException` with `ErrorCodes.OTPExpired`, `IsUsed` false |
| 15 | `OtpRepositoryExtensionsTests` | `ConsumeAsync_ExpiredAtGivenTime_ThrowsOtpExpired` | verified OTP issued at `Now`, `ConsumeAsync(…, Now.AddMinutes(5), …)` → `BadRequestCoreException` `OTPExpired` |
| 16 | `VerifyOTPHandlerTests` | `Handle_OtpExpiredAtProviderTime_SavesAttemptAndThrowsExpired` | OTP issued at `Now.AddMinutes(-5)`, clock `Now` → `BadRequestCoreException` `OTPExpired`, `SaveChangesAsync` `Received(1)` |
| 17 | `FileSignatureTests` | `Create_NoPatterns_ThrowsArgumentOutOfRange` | `() => FileSignature.Create([".bin"])` throws `ArgumentOutOfRangeException` with `ParamName == "patterns"` |
| 18 | `ValidationBehaviourTests` | `Handle_FailuresFromEveryValidator_ThrowsWithCodesInValidatorOrder` | `[Failing("A_CODE"), Failing("B_CODE")]` → `ValidationBehaviourException`, `ErrorCode == "A_CODE,B_CODE"`, `StatusCode == 422`, `next` not called |
| 19 | `ValidationBehaviourTests` | `Handle_NoValidators_CallsNext` | empty validators → returns `next`'s value |
| 20 | `AuditBehaviourTests` | `Handle_AuditableCommand_StampsTimestampFromTimeProvider` | `_entry!.Timestamp == Now` |
| 21 | `AuditBehaviourTests` | existing `Handle_AuditableSuccess_AppendsSuccessRowWithActorActionAndResource` | still passes with `ICurrentUser.Role` → `ActorRole == "Admin"` |
| 22 | `CurrentUserServiceTests` | `Role_RoleClaim_ReturnsRole` | claim `ClaimTypes.Role = "Teacher"` → `Role == "Teacher"` |
| 23 | `CurrentUserServiceTests` | `UserId_NoHttpContext_ReturnsNull` | `accessor.HttpContext` null → `(UserId, UserName, Role)` all null |
| 24 | `RefreshTokenServiceTests` | `GenerateTokenAsync_User_ExpiresConfiguredDaysAfterProviderTime` | `signInManager.CreateUserPrincipalAsync(user)` returns a principal. Capture the ticket given to `_protector.Protect` → `Properties.ExpiresUtc == Now.AddDays(7)` |
| 25 | `RefreshTokenServiceTests` | `ValidateTokenAsync_TicketExpiredAtProviderTime_ThrowsRefreshTokenIsExpired` | `_protector.Unprotect("t")` returns a ticket with `ExpiresUtc = Now.AddSeconds(-1)` → `UnauthorizedCoreException` `ErrorCodes.RefreshTokenIsExpired`. `ValidateSecurityStampAsync` `DidNotReceive()` |
| 26 | `JwtTokenServiceTests` | `GenerateTokenAsync_Claims_ExpiresConfiguredHoursAfterProviderTime` | `new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo == Now.AddHours(2).UtcDateTime` |
| 27 | `JwtTokenServiceTests` | `GenerateTokenAsync_RefreshTicketExpiredAtProviderTime_ThrowsRefreshTokenIsExpired` | ticket expires `Now.AddSeconds(-1)` → `UnauthorizedCoreException` `RefreshTokenIsExpired` |
| 28 | `OtpOptionsRegistrationTests` | `AddCoreOtp_NoPhoneSettings_FailsValidation` | empty config → reading `.Value` throws `OptionsValidationException` naming `PhoneCodes` and `PhoneLength` |
| 29 | `OtpOptionsRegistrationTests` | `AddCoreOtp_ConfiguredPhoneSettings_BindsExactlyConfiguredValues` | `{CoreOtp:PhoneCodes:0 = "015", CoreOtp:PhoneLength = "11"}` → `PhoneCodes` equals `["015"]` (no default prefixes), `PhoneLength == 11` |
| 30 | `DomainAssemblyReferencesTests` | `DomainAssembly_ReferencedAssemblies_ExcludeCoreUtilitiesAndAspNetCore` | no name equals `Core.Utilities`, none starts with `Microsoft.AspNetCore.` |
| 31 | `QuestionMasteryTests` | `Record_NewAttempt_StampsUpdatedBy` (renamed) | `UpdatedBy == _studentId` |
| 32 | all existing suites | unchanged, still green | the 4 auth handler suites, `OtpTests`, `VerifyOTPHandlerTests`, `OtpRepositoryExtensionsTests`, `RepositoryPagingTests`, `PipelineCompositionTests`, every Domain and Integration suite |

## Definition of done
**Build and packages**
- [ ] `dotnet build api/Elmanhg.slnx` succeeds with no new warnings in `Elmanhg.*` projects, and `dotnet test` is green (unit + integration).
- [ ] `api/core-libraries/Directory.Packages.props` is gone. No core csproj has a `Version=` attribute. Every core package version lives in `api/Directory.Packages.props`.
- [ ] `grep -r "2.3.9\|Azure.Core" api/core-libraries --include=*.csproj` returns nothing.
- [ ] `Core.CQRS`, `Core.OTP`, `Core.Notifications`, `Core.Identity`, `Core.Localization`, `Core.Exceptions`, `Core.Logging` and `Core.EntityFrameworkCore` carry `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. OTP and Notifications reference `MediatR` directly, Notifications references `FluentValidation`, and EF Core references `MediatR`.
- [ ] `Core.CQRS` no longer references `Core.Utilities`. `Core.Auditing` no longer references `Core.Identity`. `Elmanhg.Domain` references only `Core.DDD` among core projects.
- [ ] `dotnet list api/Elmanhg.slnx package --vulnerable --include-transitive` is clean, and `dotnet format --verify-no-changes` exits 0.

**Dead code**
- [ ] Every file marked **Delete** is gone. `grep -rn "^\s*//.*[;{}]" api/core-libraries --include=*.cs` finds no commented-out code.
- [ ] `SeedLocalizedExtensions.cs` namespace is `Core.EntityFrameworkCore.Context`. The Azure Table column list has no `Latitude`/`Longitude`.

**Clock and actor**
- [ ] `grep -rn "DateTimeOffset.UtcNow" api/core-libraries --include=*.cs` matches only `Core.DDD/Entities/AuditEntity.cs` (D8), `Core.Logging/RequestLoggingMiddleware.cs` and `Core.Notifications/**`.
- [ ] `ICurrentUser` lives in `Core.DDD.Identity`, and `ICurrentUserService : ICurrentUser` has no `NationalId`/`LoginType`. `AuditBehaviour` depends on `ICurrentUser` + `TimeProvider`.
- [ ] `Repository.SaveChangesAsync` matches "Domain behaviour" byte for byte. The 5 core repositories and the 33 app repositories pass `currentUser`/`timeProvider`. `RuntimeSettingOverrideRepository` is untouched.
- [ ] Exactly the 18 listed `UpdationDate = DateTimeOffset.UtcNow;` lines are removed, and the 14 listed ones remain.
- [ ] No `ToMicroseconds(` method or call remains in `Elmanhg.Domain` (only `TruncateToMicroseconds`).
- [ ] `Otp.Verify`/`MarkUsed`/`ConsumeAsync` take `now`. `VerifyOTPHandler` and the 4 consuming app handlers read it from `TimeProvider`.

**Behaviour switches**
- [ ] `ValidationBehaviour` has no `Task.WhenAll`.
- [ ] `OtpOptions` has no Egyptian defaults, and it is validated on start.
- [ ] `FileSignature.Create` rejects zero patterns.
- [ ] Every core `await` (except `await using`/`await foreach`) ends in `.ConfigureAwait(false)`.
- [ ] No migration file is added. No comment contains `#<number>`.
- [ ] Every test in the Test plan exists with the exact name.

**Docs** (docs-sync: these are divergences, so they ship in this change)
- [ ] `docs/constitution.md` line 3 stack:
  - `(`Core.DDD`, ` becomes `(`Core.DDD` (incl. `Core.DDD.Time` `TruncateToMicroseconds`, `TimeZoneInfo.LocalDate`/`StartOfDay`, and the `ICurrentUser` actor port), `.
  - `` `Core.Utilities` (`TruncateToMicroseconds`, `TimeZoneInfo.LocalDate`/`StartOfDay`, `AddValidatedOptions`) `` becomes `` `Core.Utilities` (`IGenerator`, `AddValidatedOptions`) ``.
  - Add after the core list: "every core package version is pinned centrally in `api/Directory.Packages.props`, and ASP.NET types come from the `Microsoft.AspNetCore.App` framework reference".
- [ ] `docs/constitution.md` §1.4: "`DateTimeOffset.UtcNow` for stamps." becomes "Handlers and core read time from the injected `TimeProvider`; `Repository.SaveChangesAsync` stamps `CreationDate`, `UpdationDate`, `CreatedBy` and `UpdatedBy` when the aggregate did not."
- [ ] `docs/constitution.md` §4 Domain bullet: "guarded domain methods setting `UpdationDate`" becomes "guarded domain methods (a method that receives the transition instant stamps `UpdationDate` with it, otherwise the repository stamps it)".
- [ ] `docs/constitution.md` §4 Infrastructure bullet: "repositories (`Repository<T>` base, never saving)" becomes "repositories (`Repository<T>` base taking `ICurrentUser` + `TimeProvider`, never saving; its `SaveChangesAsync` stamps audit fields)".
- [ ] SKILL.md delta 1: `` `Core.Utilities.Time` `TruncateToMicroseconds()` `` → `` `Core.DDD.Time` `TruncateToMicroseconds()` ``.
- [ ] SKILL.md delta 5 "Promoted" bullet: `` `Core.Utilities.Time` `` → `` `Core.DDD.Time` ``. Append "`Core.DDD.Identity.ICurrentUser` (id, name, role; `ICurrentUserService` extends it) and repository audit stamping".
- [ ] SKILL.md §2 naming row "Domain method": "verb, mutates state, sets UpdationDate" becomes "verb, mutates state; stamps `UpdationDate` only with an instant it receives".
- [ ] SKILL.md §4.2: remove `UpdationDate = DateTimeOffset.UtcNow;` from the `Tenant.Update`/`Suspend` sample. The rule "Methods that mutate state MUST set `UpdationDate = DateTimeOffset.UtcNow`." becomes "`Repository.SaveChangesAsync` stamps `UpdationDate` (from `TimeProvider`) and `UpdatedBy` (from `ICurrentUser`) when the method did not. A method that receives the transition instant sets `UpdationDate = at` so every field of the transition shares it. A method that changes only owned values or child collections stamps `UpdationDate` itself."
- [ ] SKILL.md §6 repository sample: `TenantRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<Tenant>(context, currentUser, timeProvider)`, and the same for `TenantSubscriptionRepository`. The base-members sentence adds "and stamps audit fields in `SaveChangesAsync`".
- [ ] SKILL.md §6 outbox line: "Check `Core.Queues` / `Core.Notifications` first." becomes "Check `Core.Notifications` first; dispatch with a `Core.Queues` `SweepWorker<TOptions>` subclass (core has no outbox type)."
- [ ] SKILL.md §8.2: the heading keeps `IGenerator`. "(already DI-registered)" becomes "(registered by `AddCoreUtilities()`)".
- [ ] SKILL.md §8.10: "`Core.OTP` / `IGenerator` only if backed by `RandomNumberGenerator` — verify in `core-libraries`" becomes "`IGenerator` (Nanoid on `RandomNumberGenerator`) or `RandomNumberGenerator.GetInt32` / `GetBytes`".
- [ ] SKILL.md §9 checklist:
  - "methods set `UpdationDate = DateTimeOffset.UtcNow`" becomes "methods stamp `UpdationDate` only with an instant they receive (the repository stamps the rest)".
  - "soft-delete filter only in the global method" becomes "soft-delete filter only through `modelBuilder.ApplySoftDeleteQueryFilters()`, never a hand-written `HasQueryFilter`".
- [ ] SKILL.md §11: the CPM line gains "Elmanhg core included: no `Version` in any csproj; HTTP types via `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, never the deprecated 2.x `Microsoft.AspNetCore.*` packages". The MassTransit row "`Core.Queues` (+ Hangfire…)" becomes "a `Core.Queues` `SweepWorker<TOptions>` sweep (+ Hangfire for scheduled jobs)".
- [ ] No other doc diverges: `docs/user-administration.md` already says the phone rules come from `CoreOtp:PhoneCodes`/`PhoneLength`, `docs/audit-log.md` already excludes the stamp fields from diffs, and `docs/deployment.md` already bakes `appsettings.example.json`.
