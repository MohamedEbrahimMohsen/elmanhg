# Plan — [E21.S1] Core bug fixes: soft delete, OTP, localization, paging overflow, middleware order

## Goal
Five confirmed defects in the vendored core libraries, plus one in the API pipeline, are fixed:
- soft-deleted rows record when they were deleted;
- OTP hashes are compared in constant time, the resend limit is exactly `MaxReissueCount`, and the daily resend window no longer slides with every resend;
- `Core.Localization` reads one default-language key;
- paging cannot overflow `int`;
- the unmapped `Core.Notifications` endpoints carry authorization;
- an exception thrown during authorization gets the standard error body.

App behaviour changes only where the story asks for it: the resend limit (one fewer resend), the window anchor, `DeletedAt` being set, and errors raised during authorization or media serving now being formatted.

## Scope
**In:** every sub-task of the story:
1. SoftDelete records the time, with tests.
2. OTP: constant-time compare, reissue limit, daily window, RequestIP filter, with tests.
3. Localization key and per-call default, with tests.
4. Paging offset and count in core and in the two copies, with tests.
5. Notifications endpoints authorized, with metadata tests.
6. Middleware order, with an integration test.
7. Docs.

**Out:**
- Decoupling `CoreDbContext`, or adding hooks to it (dev decision 2026-10-06).
- Registering or mapping `Core.Notifications` in the app.
- Egypt-only defaults in `OtpOptions.PhoneCodes`.
- `Repository.CountAsync` returning `int`.
- The `NextAllowedReissueAt.AddHours(...)` anchor in `Otp.Reissue`.
- `Otp.Verify`/`MarkUsed` reading `DateTimeOffset.UtcNow`.
- Backfilling `DeletedAt` on old rows (the story forbids it).
- Core.Http/Messaging (308), Core.Storage (310), Core.Hosting/Observability/Cache/Spreadsheets (313). Those are other lanes.

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | `SoftDelete` signature | `public void SoftDelete(DateTimeOffset deletedAt)`. The parameterless overload is removed. | The compiler forces every call site to pass a time, so no site can silently keep `null`. Domain methods already stamp `UpdationDate` with a value, and reuse it. |
| D2 | Time used at the 8 app call sites | `var now = DateTimeOffset.UtcNow; SoftDelete(now); … UpdationDate = now;`. `AvatarConversation.Delete` passes its microsecond-truncated `at` and drops `DeletedAt = at;`. | No behaviour change: these methods already use `DateTimeOffset.UtcNow`, and `DeletedAt == UpdationDate` is a testable invariant. |
| D3 | Core call site `DeleteNotificationTemplateHandler` | `notificationTemplate.SoftDelete(DateTimeOffset.UtcNow);`. No TimeProvider, no test. | The module is unregistered. Injecting TimeProvider would need DI changes to an unused module. |
| D4 | OTP constant-time compare | Private static `HashesMatch(string expected, string actual)` in `Otp`, using `CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual))`. | Skill §8.10. A length mismatch returns false inside `FixedTimeEquals`. |
| D5 | Reissue off-by-one | `if (ReissueCount >= MaxReissueCount)`. | `MaxReissueCount` resends, not Max+1 (story). |
| D6 | Daily window anchor | New column `Otp.ReissueWindowStartedAt` (`DateTimeOffset`, non-null). `Create` sets it to `now`. `Reissue` resets the count and sets the window start to `now` when `ReissueWindowStartedAt + 1 day <= now`. `CreatedAt` keeps its meaning (time of the latest code) and is still overwritten. | The story says the reset "must not depend on `CreatedAt`". Leaving `CreatedAt` alone keeps every other reader unchanged. |
| D7 | Window length | `private static readonly TimeSpan ReissueWindow = TimeSpan.FromDays(1);`, with a WHY comment. | Keeps the existing "daily" rule. No new option: the story fixes the anchor, not the length. |
| D8 | Migration for D6 | Generated migration `AddOtpReissueWindow` in `Elmanhg.Infrastructure/Migrations`. Keep EF's generated `defaultValue` (0001-01-01) for existing rows. No backfill. | `AppDbContextTests` asserts there are no pending model changes. Legacy rows hold an expired window, so their next resend starts a fresh one: equivalent to the old behaviour after 24 h. Codes live 5 minutes. |
| D9 | Testable OTP time | `Otp.Create(..., DateTimeOffset now)` and `Otp.Reissue(string newCodeHash, int expiresInMinutes, DateTimeOffset now)`. `GenerateOTPHandler` gets `TimeProvider timeProvider` and calls `timeProvider.GetUtcNow()` once. `AddCoreOtp` adds `services.TryAddSingleton(TimeProvider.System);`. | The window reset cannot be tested without controlling time. `TryAdd` keeps core self-contained and lets the app or tests override it. The app already registers `TimeProvider.System`. |
| D10 | RequestIP/UserAgent filter | Drop the filter. `IOtpRepository.FindAsync(string recipient, string? requestIP, ct)` becomes `FindByRecipientAsync(string recipient, CancellationToken cancellationToken)`. The `RequestIP`/`UserAgent` properties and columns stay, unused (dropping them would be a destructive migration). | This is the safe option: an IP-scoped lookup lets a caller reset the cooldown and the resend count by changing network. The columns are never written. Documented in `docs/otp-delivery.md`. |
| D11 | Default-language key | One key, `CoreLocalization:DefaultLanguage`. `LocalizationManager` stops reading `Localization:DefaultLanguage`. Shared parsing goes in the new static `DefaultLanguageResolver`. | One source of truth for `UseCoreLocalization` and `LocalizationManager`. The app already sets `CoreLocalization:DefaultLanguage: "ar"`. |
| D12 | `[..2]` guard | `Resolve(string? configured)`: trim; null, empty, or fewer than 2 characters gives `"en"`; otherwise the first 2 characters, lower-invariant. | Prevents `ArgumentOutOfRangeException` on values like `"a"`. |
| D13 | `LocalizedTextExtensions` default | Read `CultureInfo.DefaultThreadCurrentCulture?.TwoLetterISOLanguageName` inside `Localized()` on every call. Use `text?.Arabic` / `text?.English` in the fallback arm too. | Per call (story). Also removes the null-reference on the fallback arm. |
| D14 | Paging arithmetic home | New static `Core.DDD.Models.PageCalculator`, with `Offset`, `TotalPages` and `IsPastEnd`. Used by `Repository.FindPaginatedAsync`, `EssayGradeRepository.GetInReviewPageAsync` and `MathStepGradeRepository.GetInReviewPageAsync`. | One implementation instead of three copies, and unit-testable without a database. It sits next to `PageData`. |
| D15 | Offset beyond `int` / past the end | `Queryable.Skip` takes only `int`. When `offset >= totalItems` or `offset > int.MaxValue`, return `Items = []` without the data query. Otherwise `Skip((int)offset)`. A negative offset (page 0) passes through unchanged. | No overflow and no wasted query. Page-0 behaviour stays as it is today (validators reject it). |
| D16 | `TotalPages` | `pageSize <= 0 ? 0 : totalItems / pageSize + (totalItems % pageSize == 0 ? 0 : 1)`, all in `long`. | No `double`. No division by zero. No overflow from `total + size - 1`. |
| D17 | Notifications authorization | `MapCoreFirebaseNotificationEndpoints(this IEndpointRouteBuilder endpoints, string adminPolicyName)` and `MapCoreNotificationTemplateEndpoints(this IEndpointRouteBuilder endpoints, string adminPolicyName)` call `group.RequireAuthorization(adminPolicyName)`. Devices and user-feed groups call `group.RequireAuthorization()` (default policy = authenticated user). A blank policy name is rejected with `ArgumentException.ThrowIfNullOrWhiteSpace`. | Core stays app-agnostic: the policy name is a parameter. Send, subscribe and template are admin operations (Morabh `Morabh.APIs/Program.cs` lines 114–117 apply Admin to Firebase and templates). |
| D18 | Exception middleware position | Move `app.UseMiddleware<CoreExceptionMiddleware>();` to directly after `app.UseMiddleware<CoreRequestLoggingMiddleware>();`, before `UseHttpsRedirection`. | The request log reads `Items["ErrorCode"]`, so logging must wrap the exception middleware. Localization runs earlier, so messages stay localized. Errors from media serving and authorization are now formatted. |
| D19 | How the integration test raises an exception during authorization | A test-only `IAuthorizationHandler` that throws, registered in a derived host (`factory.WithWebHostBuilder(... ConfigureTestServices ...)`). Calls are anonymous `POST /api/auth/logout` (policy `AuthenticatedUser`). | `DefaultAuthorizationService` runs every registered handler on each policy evaluation, including anonymous ones. Derived hosts follow the existing pattern in `ForwardedHeadersTests`. |
| D20 | Paging integration tests class | One class, `RepositoryPagingTests`, covers core `Repository<T>` and both grade repositories. | All tests check the same behaviour (overflow-safe paging) against the real database. |
| D21 | Error codes / resx | None added. | Only existing codes are thrown. |
| D22 | Morabh reuse | Morabh `Core/` has the same defects (`Entity.cs` line 24 `DeletedAt = null`, `Repository.cs` line 225 `int` skip). All fixes are new, with no Morabh equivalent. Only the policy split mirrors Morabh `Program.cs`. | Reuse-first check done. |

## Existing code touched
| File | Change |
|------|--------|
| `api/core-libraries/Core.DDD/Entities/Entity.cs` | `SoftDelete()` becomes `SoftDelete(DateTimeOffset deletedAt)`, with body `IsDeleted = true; DeletedAt = deletedAt;` |
| `api/core-libraries/Core.EntityFrameworkCore/Repositories/Repository.cs` | Rewrite `FindPaginatedAsync` per "Domain behaviour §P" |
| `api/core-libraries/Core.OTP/Entities/OTP.cs` | Per "Domain behaviour §O" |
| `api/core-libraries/Core.OTP/Repositories/IOtpRepository.cs` | Replace `FindAsync(string recipient, string? requestIP, CancellationToken)` with `Task<Otp?> FindByRecipientAsync(string recipient, CancellationToken cancellationToken);` |
| `api/core-libraries/Core.EntityFrameworkCore/Repositories/OtpRepository.cs` | Implement `FindByRecipientAsync`: `_dbSet.FirstOrDefaultAsync(otp => otp.Recipient == recipient, cancellationToken).ConfigureAwait(false)` |
| `api/core-libraries/Core.OTP/GenerateOTP/GenerateOTPHandler.cs` | Constructor gains `TimeProvider timeProvider` (last parameter). Steps in "Files" row H1. |
| `api/core-libraries/Core.OTP/DependencyInjection.cs` | Add `services.TryAddSingleton(TimeProvider.System);` and `using Microsoft.Extensions.DependencyInjection.Extensions;` |
| `api/core-libraries/Core.Localization/DependencyInjection.cs` | `var defaultLang = DefaultLanguageResolver.Resolve(configuration[DefaultLanguageResolver.ConfigurationKey]);` replaces the `[..2]` line |
| `api/core-libraries/Core.Localization/LocalizationManager.cs` | `_defaultLanguage = DefaultLanguageResolver.Resolve(configuration[DefaultLanguageResolver.ConfigurationKey]);` Field type becomes `string`. Comparisons unchanged. Drop the trailing comment that explains what the code does. |
| `api/core-libraries/Core.Localization/LocalizedTextExtensions.cs` | Remove the static `_defaultLang` field. Body per D13 (code in "Domain behaviour §L"). |
| `api/core-libraries/Core.Notifications/Endpoints/NotificationEndpoints.cs` | `MapCoreFirebaseNotificationEndpoints(this IEndpointRouteBuilder endpoints, string adminPolicyName)`: first line `ArgumentException.ThrowIfNullOrWhiteSpace(adminPolicyName);`, then `var group = endpoints.MapGroup("/c/notification").RequireAuthorization(adminPolicyName);`. `MapCoreDevicesNotificationEndpoints` and `MapCoreUserNotificationEndpoints`: `endpoints.MapGroup("/c/notification").RequireAuthorization();` |
| `api/core-libraries/Core.Notifications/Endpoints/NotificationTemplateEndpoints.cs` | `MapCoreNotificationTemplateEndpoints(this IEndpointRouteBuilder endpoints, string adminPolicyName)`, with the same guard and `.RequireAuthorization(adminPolicyName)` on the group |
| `api/core-libraries/Core.Notifications/Templates/DeleteNotificationTemplate/DeleteNotificationTemplateHandler.cs` | `notificationTemplate.SoftDelete(DateTimeOffset.UtcNow);` |
| `api/Elmanhg.Domain/Avatar/AvatarConversation.cs` | `Delete`: `SoftDelete(at);` and delete the line `DeletedAt = at;` |
| `api/Elmanhg.Domain/ExamBlueprints/ExamBlueprint.cs` | `Delete`: after the guard, `var now = DateTimeOffset.UtcNow; SoftDelete(now); UpdatedBy = deletedBy; UpdationDate = now;` |
| `api/Elmanhg.Domain/Lessons/Lesson.cs` | `Delete`: same pattern after the objectives loop |
| `api/Elmanhg.Domain/Lessons/LessonObjective.cs` | `Delete`: same pattern |
| `api/Elmanhg.Domain/SlaCalendars/ExamPeriod.cs` | `Delete`: same pattern |
| `api/Elmanhg.Domain/Subjects/Subject.cs` | `Delete`: same pattern after the guard |
| `api/Elmanhg.Domain/Teachers/TeacherSubject.cs` | `Unassign`: same pattern (`UpdatedBy = unassignedBy`) |
| `api/Elmanhg.Domain/Units/CurriculumUnit.cs` | `Delete`: same pattern after the guard |
| `api/Elmanhg.Infrastructure/EssayGrading/EssayGradeRepository.cs` | `GetInReviewPageAsync` per "Domain behaviour §P" (copy variant) |
| `api/Elmanhg.Infrastructure/MathStepGrading/MathStepGradeRepository.cs` | Same as the line above |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef` (adds `ReissueWindowStartedAt` to `Otps`) |
| `api/Elmanhg.Api/Program.cs` | Move the line `app.UseMiddleware<CoreExceptionMiddleware>();` to directly after `app.UseMiddleware<CoreRequestLoggingMiddleware>();`. Add one comment line above it: `// Inside request logging (it reads the error code) and before authorization, so failures in auth handlers and media get the standard error body.` No other change. |
| `api/Elmanhg.Tests/Builders/OtpBuilder.cs` | Add field `private DateTimeOffset _issuedAt = DateTimeOffset.UtcNow;` and `private int _reissueBlockCooldownInHours = 24;`. Add methods `public OtpBuilder IssuedAt(DateTimeOffset issuedAt)` and `public OtpBuilder WithReissueBlockCooldownInHours(int reissueBlockCooldownInHours)`. `Build()` calls `Otp.Create(_recipientType, _recipient, CodeHash, 5, _maxVerificationAttempts, _reissueCooldownSeconds, _maxReissueCount, _reissueBlockCooldownInHours, _issuedAt)`. |
| `api/Elmanhg.Tests/Core/Otp/OtpTests.cs` | Intentional updates and new tests (Test plan T5–T15). |
| `api/Elmanhg.Tests/Core/Otp/GenerateOTPHandlerTests.cs` | Constructor: add `private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();`, `private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);`, `_timeProvider.GetUtcNow().Returns(Now);` and pass `_timeProvider` to the handler. The cooldown test arranges `_otpRepository.FindByRecipientAsync(PhoneNumber, Arg.Any<CancellationToken>()).Returns(new OtpBuilder().ForPhone(PhoneNumber).IssuedAt(Now).Build());`. Plus new tests T16–T17. |
| `api/Elmanhg.Tests/Domain/ExamBlueprints/ExamBlueprintTests.cs`, `Domain/Lessons/LessonLifecycleTests.cs`, `Domain/Lessons/LessonObjectiveTests.cs`, `Domain/SlaCalendars/ExamPeriodTests.cs`, `Domain/Subjects/SubjectTests.cs`, `Domain/Teachers/TeacherSubjectTests.cs`, `Domain/Units/CurriculumUnitTests.cs` | Add one test each (T2a–T2g). Existing tests untouched. |
| `.claude/skills/dotnet-feature/SKILL.md` | Docs section |
| `docs/constitution.md` | Docs section |
| `docs/otp-delivery.md` | Docs section |

Updating existing tests is limited to the four intentional, story-mandated changes:
- `OtpTests.Reissue_PastMaxReissueCount_ThrowsRateLimitExceeded` (it encodes the off-by-one);
- the `Reissue` and `Create` calls that gain a `now` argument;
- `GenerateOTPHandlerTests`: constructor and the renamed repository method.

Nothing is skipped or weakened.

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| F1 | `api/core-libraries/Core.DDD/Models/PageCalculator.cs` | `namespace Core.DDD.Models; public static class PageCalculator` | `public static long Offset(int pageNumber, int pageSize) => ((long)pageNumber - 1) * pageSize;` · `public static long TotalPages(long totalItems, int pageSize)` (body D16) · `public static bool IsPastEnd(long offset, long totalItems) => offset >= totalItems \|\| offset > int.MaxValue;`. One WHY comment above `IsPastEnd`: `// Queryable.Skip takes an int; an offset past the last row or past int range can only produce an empty page.` |
| F2 | `api/core-libraries/Core.Localization/DefaultLanguageResolver.cs` | `namespace Core.Localization; public static class DefaultLanguageResolver` | `public const string ConfigurationKey = "CoreLocalization:DefaultLanguage";` · `public const string Fallback = "en";` · `public static string Resolve(string? configured)`: `var value = configured?.Trim(); return value is { Length: >= 2 } ? value[..2].ToLowerInvariant() : Fallback;` |
| F3 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddOtpReissueWindow.cs` + `.Designer.cs` | EF migration | Generated: `dotnet ef migrations add AddOtpReissueWindow -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Expected `Up`: a single `AddColumn<DateTimeOffset>(name: "ReissueWindowStartedAt", table: "Otps", type: "timestamp with time zone", nullable: false, defaultValue: <generated>)`. `Down`: `DropColumn`. No other operations. If anything else appears, stop and report `BLOCKED`. |
| F4 | `api/Elmanhg.Tests/Core/DDD/EntityTests.cs` | `namespace Elmanhg.Tests.Core.DDD; public sealed class EntityTests` | Nested `private sealed class ProbeEntity(Guid id) : Entity(id);` plus T1 |
| F5 | `api/Elmanhg.Tests/Core/DDD/PageCalculatorTests.cs` | `public sealed class PageCalculatorTests` | T18–T23 |
| F6 | `api/Elmanhg.Tests/Core/Localization/CultureScope.cs` | `namespace Elmanhg.Tests.Core.Localization; public sealed class CultureScope : IDisposable` | Fields `_previousCulture = CultureInfo.CurrentCulture`, `_previousDefaultThreadCulture = CultureInfo.DefaultThreadCurrentCulture`. Constructor `CultureScope(string culture)` sets `CultureInfo.CurrentCulture = new CultureInfo(culture)`. `Dispose()` restores both. |
| F7 | `api/Elmanhg.Tests/Core/Localization/DefaultThreadCultureCollection.cs` | `[CollectionDefinition(Name, DisableParallelization = true)] public sealed class DefaultThreadCultureCollection { public const string Name = "DefaultThreadCulture"; }` | Mirrors `Integration/Configuration/RuntimeSettingsCollection.cs`. WHY comment: `// DefaultThreadCurrentCulture is process-wide; tests that set it must not overlap.` |
| F8 | `api/Elmanhg.Tests/Core/Localization/DefaultLanguageResolverTests.cs` | `public sealed class DefaultLanguageResolverTests` | T24 |
| F9 | `api/Elmanhg.Tests/Core/Localization/LocalizationManagerTests.cs` | `public sealed class LocalizationManagerTests` | T25–T28. Config built with `new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { … }).Build()`. Culture set with `using var _ = new CultureScope("fr");` |
| F10 | `api/Elmanhg.Tests/Core/Localization/LocalizedTextExtensionsTests.cs` | `[Collection(DefaultThreadCultureCollection.Name)] public sealed class LocalizedTextExtensionsTests` | T29–T33. Each test opens `using var _ = new CultureScope(...)` and sets `CultureInfo.DefaultThreadCurrentCulture` inside the scope. |
| F11 | `api/Elmanhg.Tests/Core/Notifications/CoreNotificationEndpointsTests.cs` | `public sealed class CoreNotificationEndpointsTests` | Helper `private static List<RouteEndpoint> Map(Action<IEndpointRouteBuilder> map)`. It runs `var builder = WebApplication.CreateSlimBuilder(); builder.Services.AddSingleton(Substitute.For<IMediator>()); var app = builder.Build(); map(app); return ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();`. Const `AdminPolicy = "Probe.Admin"`. T34–T38. |
| F12 | `api/Elmanhg.Tests/Integration/Infrastructure/ThrowingAuthorizationHandler.cs` | `namespace Elmanhg.Tests.Integration.Infrastructure; public sealed class ThrowingAuthorizationHandler(Exception exception) : IAuthorizationHandler` | `public Task HandleAsync(AuthorizationHandlerContext context) => throw exception;` |
| F13 | `api/Elmanhg.Tests/Integration/Hosting/ExceptionMiddlewareOrderTests.cs` | `public sealed class ExceptionMiddlewareOrderTests(ApiFactory factory)` | Helper `private WebApplicationFactory<Program> CreateHost(Exception exception) => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IAuthorizationHandler>(new ThrowingAuthorizationHandler(exception))));`. Request: `client.PostAsync("/api/auth/logout", JsonContent.Create(new { }), TestContext.Current.CancellationToken)`. Hosts are created with `await using`. T39–T40. |
| F14 | `api/Elmanhg.Tests/Integration/Persistence/RepositoryPagingTests.cs` | `public sealed class RepositoryPagingTests(ApiFactory factory)` | Seeds `ExamPeriod.Create("Paging probe", new DateOnly(2001, 6, 1), new DateOnly(2001, 7, 15), AdminId)`. Uses the far-past dates and comment from `ExamPeriodPersistenceTests` so parallel SLA tests are unaffected. Builds `new Repository<ExamPeriod>(context)`, `new EssayGradeRepository(context)` and `new MathStepGradeRepository(context)` from a scoped `AppDbContext`. T41–T44. |
| F15 | `api/Elmanhg.Tests/Integration/Persistence/OtpRepositoryTests.cs` | `public sealed class OtpRepositoryTests(ApiFactory factory)` | Builds `new OtpRepository<User, Role, Guid, AppDbContext>(context)`. Seeds through `context.Otps.Add(new OtpBuilder().ForPhone(phone).IssuedAt(IssuedAt).Build())` with a unique phone per test (`AuthTestClient.NewPhoneNumber()`) and `IssuedAt = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero)`. T45–T46. |

### Handler H1 — `GenerateOTPHandler.Handle` (ordered)
1. Resolve `(recipientType, recipient)`, unchanged.
2. `var now = timeProvider.GetUtcNow();`
3. `code = generator.Generate(...)`, unchanged.
4. `var otp = await otpRepository.FindByRecipientAsync(recipient, cancellationToken).ConfigureAwait(false);`
5. `codeHash = otpHasher.Hash(code)`.
6. If found: `otp.Reissue(codeHash, _otpOptions.ExpirationMinutes, now);`. Otherwise: `Otp.Create(…existing named args…, now: now)`, then `AddAsync`.
7. `SendAsync`, then `SaveChangesAsync`, then return the result, all unchanged.

Constructor: `GenerateOTPHandler(IOtpRepository otpRepository, IGenerator generator, IOtpHasher otpHasher, IOptions<OtpOptions> otpOptions, IOtpSender otpSender, TimeProvider timeProvider)`.

## Error codes
No new constants and no resx changes. Existing codes exercised:

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `Core.OTP.Exceptions.ErrorCodes.OTPReissueCooldown` | `OTP_REISSUE_COOLDOWN` | `Otp.Reissue` | `RateLimitExceededCoreException` | 429 |
| `Core.OTP.Exceptions.ErrorCodes.OTPReachedMaxReissueCount` | `OTP_REACHED_MAX_REISSUE_COUNT` | `Otp.Reissue` | `RateLimitExceededCoreException` | 429 |
| `Core.OTP.Exceptions.ErrorCodes.OTPNotMatched` | `OTP_NOT_MATCHED` | `Otp.Verify` (return value) | `BadRequestCoreException` via handler | 400 |
| `Core.Exceptions.ExceptionErrorCodes.UnhandledException` | `UNHANDLED_EXCEPTION` | `CoreExceptionMiddleware` | (any non-core) | 500 |

Test-only code `PROBE_AUTHORIZATION_FAILED` is a string literal in F13. It is not added to `ErrorCodes` or resx; the localizer falls back to the message.

## Domain behaviour
### §E `Core.DDD.Entities.Entity`
```csharp
public void SoftDelete(DateTimeOffset deletedAt)
{
    IsDeleted = true;
    DeletedAt = deletedAt;
}
```
`Entity` has no `UpdationDate`. Each caller in the app stamps it with the same `now` (D2).

### §O `Core.OTP.Entities.Otp`
- New property: `public DateTimeOffset ReissueWindowStartedAt { get; private set; }`.
- New field: `private static readonly TimeSpan ReissueWindow = TimeSpan.FromDays(1);`, with the comment `// The resend quota is per day, counted from the window's first code; resends never move the window.`
- `CreatedAt`: remove the `= DateTimeOffset.UtcNow` initializer.
- `Create(OtpRecipientType recipientType, string recipient, string codeHash, int expiresInMinutes, int maxVerificationAttempts, int reissueCooldownSeconds, int maxReissueCount, int reissueBlockCooldownInHours, DateTimeOffset now)` sets:
  - `CreatedAt = now`
  - `ReissueWindowStartedAt = now`
  - `ExpiresAt = now.AddMinutes(expiresInMinutes)`
  - `NextAllowedReissueAt = now.AddSeconds(reissueCooldownSeconds)`
  - every other field as today.
- `Reissue(string newCodeHash, int expiresInMinutes, DateTimeOffset now)`:
  1. `if (ReissueWindowStartedAt + ReissueWindow <= now) { ReissueCount = 0; ReissueWindowStartedAt = now; }`
  2. Cooldown guard, unchanged (`now < NextAllowedReissueAt` → `OTPReissueCooldown` with days/hours/minutes/seconds context).
  3. `if (ReissueCount >= MaxReissueCount) { throw new RateLimitExceededCoreException(ErrorCodes.OTPReachedMaxReissueCount); }`
  4. Mutations unchanged: `VerificationId`, `CodeHash`, `IsVerified=false`, `IsUsed=false`, `VerificationAttempts=0`, `ReissueCount++`, `CreatedAt = now`, `ExpiresAt = now.AddMinutes(...)`, and the `NextAllowedReissueAt` ternary exactly as today.
- `Verify(string codeHash)`: the only change is `if (!HashesMatch(CodeHash, codeHash))` replacing `if (CodeHash != codeHash)`.
- `private static bool HashesMatch(string expected, string actual) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));`
- Add `using System.Security.Cryptography; using System.Text;`.
- `Otp` is a core `Entity` (no `UpdationDate`). Nothing to stamp.

### §L `LocalizedTextExtensions.Localized`
```csharp
public static string Localized(this LocalizedText text)
{
    var language = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
    var defaultLanguage = CultureInfo.DefaultThreadCurrentCulture?.TwoLetterISOLanguageName;

    return language switch
    {
        "en" => text?.English,
        "ar" => text?.Arabic,
        _ => defaultLanguage == "ar" ? text?.Arabic : text?.English,
    } ?? string.Empty;
}
```

### §P Paging
`Repository<T>.FindPaginatedAsync` (signature unchanged):
1. Build `query` with `asNoTracking`, `filter` and `include`, as today.
2. `var totalItems = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);`
3. `var offset = PageCalculator.Offset(pageNumber, pageSize);`
4. If `orderBy != null`, then `query = orderBy(query)`.
5. `List<T> items = PageCalculator.IsPastEnd(offset, totalItems) ? [] : await query.Skip((int)offset).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);`
6. Return `new PageData<T> { Items = items, TotalItems = totalItems, TotalPages = PageCalculator.TotalPages(totalItems, pageSize), PageNumber = pageNumber, PageSize = pageSize }`.

`EssayGradeRepository` / `MathStepGradeRepository.GetInReviewPageAsync`:
- Keep the `query` and `LongCountAsync` total.
- Then `var offset = PageCalculator.Offset(pageNumber, pageSize);` and `List<X> items = PageCalculator.IsPastEnd(offset, total) ? [] : await query.OrderBy(x => x.RequestedAt).ThenBy(x => x.Id).Skip((int)offset).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);`
- `TotalPages = PageCalculator.TotalPages(total, pageSize)`.
- Add `using Core.DDD.Models;` (it is already present in both).

## API surface
No new or changed Elmanhg endpoints.

| Method | Route | Authorization after change |
|---|---|---|
| (core, unmapped) POST | `/c/notification/send/*`, `/subscribe/topic`, `/unsubscribe/topic` | `RequireAuthorization(adminPolicyName)` |
| (core, unmapped) GET/POST/PUT/DELETE | `/c/notification-templates/*` | `RequireAuthorization(adminPolicyName)` |
| (core, unmapped) POST | `/c/notification/devices/register` | `RequireAuthorization()` |
| (core, unmapped) GET/POST | `/c/notification/me`, `/c/notification/me/{id}` | `RequireAuthorization()` |

The pipeline order in `Program.cs` becomes:
1. forwarded headers
2. `UseCoreLocalization`
3. `CoreRequestLoggingMiddleware`
4. **`CoreExceptionMiddleware`**
5. `UseHttpsRedirection`
6. `UseMediaStorage`
7. `UseAuthorization`
8. `UseRateLimiter`
9. endpoints

## Test plan
Assertions use FluentAssertions (the repo's pinned library). Time comes from fixed `DateTimeOffset` values or `Substitute.For<TimeProvider>()`. Async calls pass `TestContext.Current.CancellationToken`.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | `Core/DDD/EntityTests` | `SoftDelete_GivenTime_MarksDeletedAndStampsDeletedAt` | `(IsDeleted, DeletedAt) == (true, (DateTimeOffset?)T)` for fixed `T = 2026-01-01` |
| T2a | `Domain/ExamBlueprints/ExamBlueprintTests` | `Delete_UnitBlueprint_StampsDeletedAtWithUpdationDate` | Arranged as `Delete_UnitBlueprint_SoftDeletes`. `DeletedAt` not null and `== (DateTimeOffset?)UpdationDate` |
| T2b | `Domain/Lessons/LessonLifecycleTests` | `Delete_Draft_StampsDeletedAtWithUpdationDate` | Same assertion on the lesson; each objective's `DeletedAt` is not null |
| T2c | `Domain/Lessons/LessonObjectiveTests` | `Delete_Always_StampsDeletedAtWithUpdationDate` | Same assertion |
| T2d | `Domain/SlaCalendars/ExamPeriodTests` | `Delete_StampsDeletedAtWithUpdationDate` | Same assertion |
| T2e | `Domain/Subjects/SubjectTests` | `Delete_NoUnits_StampsDeletedAtWithUpdationDate` | Same assertion |
| T2f | `Domain/Teachers/TeacherSubjectTests` | `Unassign_Always_StampsDeletedAtWithUpdationDate` | Same assertion |
| T2g | `Domain/Units/CurriculumUnitTests` | `Delete_NoLessons_StampsDeletedAtWithUpdationDate` | Same assertion |
| T3 | `Domain/Avatar/AvatarConversationTests` (existing `Delete_Conversation_SoftDeletesAndZeroesMessageCount`) | unchanged | Still passes with the manual line removed (`DeletedAt == truncated deletedAt`) |
| T4 | `Domain/Subjects/SubjectTests` | `Delete_HasUnits_LeavesDeletedAtNull` | After the throwing call, `DeletedAt` is null |
| T5 | `Core/Otp/OtpTests` (update) | `Reissue_BeforeCooldown_ThrowsRateLimitExceeded` | Builder `.IssuedAt(Now)`, `Reissue("new-hash", 5, Now)` throws `OTPReissueCooldown` with key `minutes` |
| T6 | `Core/Otp/OtpTests` (update) | `Reissue_PastMaxReissueCount_ThrowsRateLimitExceeded` | Builder: cooldown 0, Max 1, block 0, `IssuedAt(Now)`. First `Reissue(…, Now)` succeeds; second `Reissue(…, Now.AddMinutes(1))` throws `OTPReachedMaxReissueCount` |
| T7 | `Core/Otp/OtpTests` | `Reissue_MaxReissueCountZero_ThrowsOnFirstReissue` | Max 0, cooldown 0: the first reissue throws `OTPReachedMaxReissueCount` (the old code allowed it) |
| T8 | `Core/Otp/OtpTests` | `Reissue_BelowMax_IssuesNewCode` | Cooldown 0, Max 2: after `Reissue("next-hash", 5, Now.AddMinutes(1))`: `CodeHash == "next-hash"`, `ReissueCount == 1`, `ExpiresAt == Now.AddMinutes(6)`, `VerificationId` changed, `IsVerified` false |
| T9 | `Core/Otp/OtpTests` | `Reissue_DayAfterWindowStartDespiteRecentReissue_ResetsCount` | Cooldown 0, Max 1, block 0, `IssuedAt(Now)`. `Reissue(…, Now.AddHours(23))` succeeds. `Reissue(…, Now.AddDays(1))` succeeds, with `ReissueCount == 1` and `ReissueWindowStartedAt == Now.AddDays(1)` |
| T10 | `Core/Otp/OtpTests` | `Reissue_WithinWindow_KeepsWindowStart` | Cooldown 0, Max 3: after `Reissue(…, Now.AddHours(5))`, `ReissueWindowStartedAt == Now` and `CreatedAt == Now.AddHours(5)` |
| T11 | `Core/Otp/OtpTests` (update) | `Create_EmailRecipient_KeepsRecipientAndType` | Same asserts; the call gains `Now` as its last argument |
| T12 | `Core/Otp/OtpTests` | `Create_GivenNow_StampsTimesFromNow` | `Otp.Create(..., 5, 3, 60, 5, 24, Now)` gives `CreatedAt == Now`, `ReissueWindowStartedAt == Now`, `ExpiresAt == Now.AddMinutes(5)`, `NextAllowedReissueAt == Now.AddSeconds(60)` |
| T13 | `Core/Otp/OtpTests` | `Verify_MatchingHash_MarksVerified` | Default builder (real-time issue). `Verify(OtpBuilder.CodeHash)` returns null, `IsVerified` true, `VerificationAttempts == 0` |
| T14 | `Core/Otp/OtpTests` | `Verify_DifferentHashSameLength_ReturnsNotMatched` | `Verify("code-hasx")` returns `OTPNotMatched`, `IsVerified` false |
| T15 | `Core/Otp/OtpTests` | `Verify_DifferentLengthHash_ReturnsNotMatched` | `Verify("short")` returns `OTPNotMatched` |
| T16 | `Core/Otp/GenerateOTPHandlerTests` | `Handle_NewPhone_StampsTimesFromTimeProvider` | Result `ExpiresAt == Now.AddMinutes(5)` and `NextAllowedReissueAt == Now.AddSeconds(60)` (default `OtpOptions`); `SaveChangesAsync` `Received(1)` |
| T17 | `Core/Otp/GenerateOTPHandlerTests` | `Handle_ExistingOtpPastCooldown_ReissuesWithoutAdding` | `FindByRecipientAsync` returns a builder OTP `IssuedAt(Now.AddMinutes(-2))`. Result `ReissueCount == 1`; `AddAsync` `DidNotReceive()`; `SaveChangesAsync` `Received(1)`; `SendAsync` `Received(1)` |
| T18 | `Core/DDD/PageCalculatorTests` | `Offset_MaxPageNumber_DoesNotOverflow` | `Offset(int.MaxValue, 100) == ((long)int.MaxValue - 1) * 100` and is positive |
| T19 | `Core/DDD/PageCalculatorTests` | `Offset_FirstPage_IsZero` | `Offset(1, 20) == 0` |
| T20 | `Core/DDD/PageCalculatorTests` | `TotalPages_Counts_RoundsUp` | `[Theory]` `(0,20,0)`, `(1,20,1)`, `(20,20,1)`, `(21,20,2)`, `(5_000_000_000L,1,5_000_000_000L)` |
| T21 | `Core/DDD/PageCalculatorTests` | `TotalPages_NonPositivePageSize_ReturnsZero` | `TotalPages(10, 0) == 0` |
| T22 | `Core/DDD/PageCalculatorTests` | `IsPastEnd_OffsetBeyondIntRange_ReturnsTrue` | `IsPastEnd((long)int.MaxValue + 1, long.MaxValue)` is true |
| T23 | `Core/DDD/PageCalculatorTests` | `IsPastEnd_Offsets_ComparedToTotal` | `[Theory]` `(0,0,true)`, `(0,1,false)`, `(20,20,true)`, `(19,20,false)` |
| T24 | `Core/Localization/DefaultLanguageResolverTests` | `Resolve_ConfiguredValue_ReturnsTwoLetterOrFallback` | `[Theory]` `("ar","ar")`, `("ar-EG","ar")`, `("AR","ar")`, `(" en ","en")`, `(null,"en")`, `("","en")`, `("a","en")` |
| T25 | `Core/Localization/LocalizationManagerTests` | `GetLocalizedValue_UnsupportedCultureWithArabicDefault_ReturnsArabic` | Key `CoreLocalization:DefaultLanguage = "ar"`, culture `fr`: returns the Arabic value |
| T26 | `Core/Localization/LocalizationManagerTests` | `GetLocalizedValue_LegacyKeyOnly_FallsBackToEnglish` | Only `Localization:DefaultLanguage = "ar"`, culture `fr`: returns English |
| T27 | `Core/Localization/LocalizationManagerTests` | `GetLocalizedValue_SingleCharacterDefault_FallsBackToEnglish` | Key value `"a"`: constructing does not throw and returns English |
| T28 | `Core/Localization/LocalizationManagerTests` | `GetLocalizedValue_ArabicCulture_ReturnsArabic` | Culture `ar-EG`, no key: Arabic |
| T29 | `Core/Localization/LocalizedTextExtensionsTests` | `Localized_EnglishCulture_ReturnsEnglish` | culture `en-US` |
| T30 | `Core/Localization/LocalizedTextExtensionsTests` | `Localized_ArabicCulture_ReturnsArabic` | culture `ar` |
| T31 | `Core/Localization/LocalizedTextExtensionsTests` | `Localized_UnsupportedCultureWithArabicThreadDefault_ReturnsArabic` | culture `fr`, `DefaultThreadCurrentCulture = ar` |
| T32 | `Core/Localization/LocalizedTextExtensionsTests` | `Localized_ThreadDefaultChangedBetweenCalls_UsesCurrentDefault` | culture `fr`; default `ar` gives Arabic; then default `en` gives English (same test) |
| T33 | `Core/Localization/LocalizedTextExtensionsTests` | `Localized_NullTextUnsupportedCulture_ReturnsEmpty` | `((LocalizedText)null!).Localized()` under `fr` returns `""` |
| T34 | `Core/Notifications/CoreNotificationEndpointsTests` | `MapCoreNotificationTemplateEndpoints_EveryEndpoint_RequiresAdminPolicy` | 5 endpoints, each with `IAuthorizeData` whose `Policy == AdminPolicy` |
| T35 | `Core/Notifications/CoreNotificationEndpointsTests` | `MapCoreFirebaseNotificationEndpoints_EveryEndpoint_RequiresAdminPolicy` | 10 endpoints, each with `Policy == AdminPolicy` |
| T36 | `Core/Notifications/CoreNotificationEndpointsTests` | `MapCoreDevicesNotificationEndpoints_EveryEndpoint_RequiresAuthenticatedUser` | 1 endpoint with non-empty `IAuthorizeData` and no `IAllowAnonymous` metadata |
| T37 | `Core/Notifications/CoreNotificationEndpointsTests` | `MapCoreUserNotificationEndpoints_EveryEndpoint_RequiresAuthenticatedUser` | 2 endpoints, same assertion |
| T38 | `Core/Notifications/CoreNotificationEndpointsTests` | `MapCoreNotificationTemplateEndpoints_BlankPolicy_Throws` | `" "` throws `ArgumentException` |
| T39 | `Integration/Hosting/ExceptionMiddlewareOrderTests` | `Post_AuthorizationHandlerThrowsCoreException_ReturnsStandardErrorBody` | `ForbiddenCoreException("PROBE_AUTHORIZATION_FAILED")`: status 403, content type `application/json`, body `code == "PROBE_AUTHORIZATION_FAILED"`, response header `X-Trace-Id` present |
| T40 | `Integration/Hosting/ExceptionMiddlewareOrderTests` | `Post_AuthorizationHandlerThrowsUnexpectedException_Returns500Unhandled` | `InvalidOperationException("probe")`: status 500, body `code == "UNHANDLED_EXCEPTION"` |
| T41 | `Integration/Persistence/RepositoryPagingTests` | `FindPaginatedAsync_PageBeyondIntOffset_ReturnsEmptyPageWithTotals` | Seeds 1 period. `FindPaginatedAsync(int.MaxValue, 100, ct, filter: x => x.Id == id)` gives `Items` empty, `TotalItems == 1`, `TotalPages == 1`, no exception |
| T42 | `Integration/Persistence/RepositoryPagingTests` | `FindPaginatedAsync_SecondPage_ReturnsRemainderAndTotalPages` | Seeds 3 periods. Filter on the 3 ids, ordered by `Id`, `(2, 2)`: 1 item, `TotalItems == 3`, `TotalPages == 2` |
| T43 | `Integration/Persistence/RepositoryPagingTests` | `EssayGradeGetInReviewPageAsync_PageBeyondIntOffset_ReturnsEmptyPage` | `GetInReviewPageAsync(Guid.NewGuid(), int.MaxValue, 50, ct)` gives empty items, `TotalItems == 0`, `TotalPages == 0` |
| T44 | `Integration/Persistence/RepositoryPagingTests` | `MathStepGradeGetInReviewPageAsync_PageBeyondIntOffset_ReturnsEmptyPage` | Same as T43 on `MathStepGradeRepository` |
| T45 | `Integration/Persistence/OtpRepositoryTests` | `FindByRecipientAsync_SavedOtp_ReturnsItWithWindowStart` | Found by phone. `ReissueWindowStartedAt == IssuedAt`, `RequestIP` null |
| T46 | `Integration/Persistence/OtpRepositoryTests` | `FindByRecipientAsync_UnknownRecipient_ReturnsNull` | A fresh phone returns null |

Regression guards already in the suite:
- `AppDbContextTests` (no pending model changes) covers the migration.
- `OtpEndpointTests` cooldown covers the end-to-end resend path.
- All existing `PageData` handler tests.

## Docs (docs-sync)
| File | Edit (exact) |
|---|---|
| `.claude/skills/dotnet-feature/SKILL.md` §4.1 table | The `Entity(Guid id)` row's "Adds" column reads `` `Id`, `IsDeleted`, `DeletedAt`, `SoftDelete(DateTimeOffset deletedAt)`, domain events ``. The line below it becomes: ``All entities implement `ISoftDeletable` — call `.SoftDelete(now)` with the same `now` you stamp on `UpdationDate`; never set `IsDeleted`/`DeletedAt` directly.`` |
| SKILL §8.4 ✅ example | `public void Delete() { var now = DateTimeOffset.UtcNow; Status = EngineerStatus.Deleted; SoftDelete(now); UpdationDate = now; }` |
| SKILL §4.5 | Append: ``Default language for an unsupported request culture: the single key `CoreLocalization:DefaultLanguage` (first two letters; anything shorter falls back to `en`). `.Localized()` reads the thread default culture on every call.`` |
| SKILL §6.1 | Append after the "AddAsync/Update/Delete" note: ```FindPaginatedAsync` counts with `LongCountAsync`; a page whose offset is past the last row (or past `int` range) returns empty `Items` with correct totals. A hand-written paged query uses `Core.DDD.Models.PageCalculator` (`Offset`, `TotalPages`, `IsPastEnd`), never `(pageNumber - 1) * pageSize` in `int`.`` |
| SKILL §7.4 | Replace ``**middleware order is fixed; do not change it**`` with: ``**middleware order is fixed**: forwarded headers → `UseCoreLocalization` → `CoreRequestLoggingMiddleware` → `CoreExceptionMiddleware` → HTTPS redirection → media storage → `UseAuthorization` → rate limiter → endpoints. The exception middleware sits inside request logging (which reads its error code) and before authorization, so a failure in an authorization handler still returns the standard error body; do not reorder.`` Append: ``Core endpoint modules take the policy name from the app (`MapCoreNotificationTemplateEndpoints(adminPolicyName)`, `MapCoreFirebaseNotificationEndpoints(adminPolicyName)`); the user and device groups require an authenticated user.`` |
| `docs/constitution.md` line 143 | `guarded domain methods setting \`UpdationDate\`` becomes ``guarded domain methods setting `UpdationDate`, soft deletes through `SoftDelete(deletedAt)` with the same time``. Do not touch line 3 (other lanes edit it). |
| `docs/constitution.md` line 146 | `` `Core.Exceptions` middleware formats responses`` becomes `` `Core.Exceptions` middleware (inside request logging, before authorization) formats responses``. |
| `docs/otp-delivery.md` §1 | After the paragraph that ends "…or the provider's response body.", add: ``**Resend limits** (`CoreOtp`). One OTP row per recipient is reused for every resend and is found by recipient only; the `RequestIP` and `UserAgent` columns are not filled, so a new network does not reset the limits. A resend waits `ReissueCooldownSeconds` (60) after the previous code (429 `OTP_REISSUE_COOLDOWN`). A recipient gets at most `MaxReissueCount` (5) resends per 24-hour window; the window starts with the first code and later resends do not move it. The resend that uses up the quota also pushes the next one out by `ReissueBlockCooldownInHours` (24), and a resend over the quota returns 429 `OTP_REACHED_MAX_REISSUE_COUNT`. Codes are compared in constant time.`` |

`docs/PRD.md` and `docs/design-system.md` describe nothing in this story and need no change. `docs/security.md` (V11, "constant-time comparisons") already states the target and is unchanged. `docs/audit-log.md` (`DeletedAt` excluded from diffs) stays true.

## Definition of done
- [ ] `Entity.SoftDelete(DateTimeOffset deletedAt)` sets `IsDeleted` and `DeletedAt`, and no parameterless overload remains.
- [ ] All 8 app call sites and `DeleteNotificationTemplateHandler` pass a time. `DeletedAt == UpdationDate` in the 7 audit-entity methods. `AvatarConversation.Delete` no longer assigns `DeletedAt` directly.
- [ ] `Otp.Verify` uses `CryptographicOperations.FixedTimeEquals`, and no `CodeHash !=` / `==` comparison remains.
- [ ] `Otp.Reissue` throws at `ReissueCount >= MaxReissueCount`, and the window reset reads `ReissueWindowStartedAt`, never `CreatedAt`.
- [ ] `Otp.Create`/`Reissue` take `DateTimeOffset now`. `GenerateOTPHandler` reads `TimeProvider` once. `AddCoreOtp` calls `TryAddSingleton(TimeProvider.System)`.
- [ ] `IOtpRepository.FindByRecipientAsync(string, CancellationToken)` exists, with no `RequestIP` filter anywhere.
- [ ] Migration `AddOtpReissueWindow` adds exactly one column and drops nothing. The snapshot is updated, and `AppDbContextTests` passes.
- [ ] `DefaultLanguageResolver` is the only reader of `CoreLocalization:DefaultLanguage`, and there is no `Localization:DefaultLanguage` and no unguarded `[..2]` in `Core.Localization`.
- [ ] `LocalizedTextExtensions` has no static captured default.
- [ ] `PageCalculator` exists. The three paged queries use it: no `(pageNumber - 1) * pageSize`, no `Math.Ceiling`, no `double` in paging. Core uses `LongCountAsync`.
- [ ] The Core.Notifications template and Firebase groups require the passed admin policy; device and user groups require authorization. The module is still not registered or mapped in `Elmanhg.Api`.
- [ ] In `Program.cs`, `CoreExceptionMiddleware` comes directly after `CoreRequestLoggingMiddleware` and before `UseAuthorization`. No other line changes.
- [ ] Every test T1–T46 exists with the listed name and passes. No existing test is skipped or weakened; only the updates listed under "Existing code touched" are made.
- [ ] Core stays app-agnostic: no `Elmanhg` name, app error code or policy constant inside `api/core-libraries`.
- [ ] No new NuGet package and no new project.
- [ ] No code comment contains a `#<number>` reference.
- [ ] The docs edits in the Docs table are applied verbatim in `SKILL.md`, `docs/constitution.md` and `docs/otp-delivery.md`.
- [ ] `dotnet build` passes with zero new warnings (`TreatWarningsAsErrors`), `dotnet test` is green, and `dotnet format --verify-no-changes` exits 0.
- [ ] This guard grep prints nothing new: `git diff origin/main -U0 -- '*.cs' | grep -E '^\+.*(DateTime\.(Now|UtcNow)|\.Result\b|\.Wait\(\)|new HttpClient\(|FromSqlRaw|async void)'`.
