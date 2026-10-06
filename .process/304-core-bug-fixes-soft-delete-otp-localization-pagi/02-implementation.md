# Implementation — [E21.S1] Core bug fixes: soft delete, OTP, localization, paging overflow, middleware order

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.DDD/Models/PageCalculator.cs` | 11 | F1: `Offset`, `TotalPages`, `IsPastEnd` in `long`, with one WHY comment |
| `api/core-libraries/Core.Localization/DefaultLanguageResolver.cs` | 13 | F2: the one reader of `CoreLocalization:DefaultLanguage`, guarded `[..2]` |
| `api/Elmanhg.Infrastructure/Migrations/20261006210928_AddOtpReissueWindow.cs` (+ `.Designer.cs`) | 30 (+3919 generated) | F3: one `AddColumn<DateTimeOffset>("ReissueWindowStartedAt", "Otps", timestamptz, not null, default 0001-01-01)`; `Down` is one `DropColumn`. Nothing else in it. |
| `api/Elmanhg.Tests/Core/DDD/EntityTests.cs` | 21 | F4: T1 |
| `api/Elmanhg.Tests/Core/DDD/PageCalculatorTests.cs` | 64 | F5: T18–T23 |
| `api/Elmanhg.Tests/Core/Localization/CultureScope.cs` | 20 | F6 |
| `api/Elmanhg.Tests/Core/Localization/DefaultThreadCultureCollection.cs` | 8 | F7 |
| `api/Elmanhg.Tests/Core/Localization/DefaultLanguageResolverTests.cs` | 22 | F8: T24 |
| `api/Elmanhg.Tests/Core/Localization/LocalizationManagerTests.cs` | 60 | F9: T25–T28 |
| `api/Elmanhg.Tests/Core/Localization/LocalizedTextExtensionsTests.cs` | 69 | F10: T29–T33 |
| `api/Elmanhg.Tests/Core/Notifications/CoreNotificationEndpointsTests.cs` | 64 | F11: T34–T38 |
| `api/Elmanhg.Tests/Integration/Infrastructure/ThrowingAuthorizationHandler.cs` | 8 | F12 |
| `api/Elmanhg.Tests/Integration/Hosting/ExceptionMiddlewareOrderTests.cs` | 46 | F13: T39–T40 |
| `api/Elmanhg.Tests/Integration/Persistence/RepositoryPagingTests.cs` | 80 | F14: T41–T44 |
| `api/Elmanhg.Tests/Integration/Persistence/OtpRepositoryTests.cs` | 51 | F15: T45–T46 |

## Files modified
| Path | Change |
|---|---|
| `api/core-libraries/Core.DDD/Entities/Entity.cs` | `SoftDelete(DateTimeOffset deletedAt)` sets `IsDeleted` and `DeletedAt`. The parameterless overload is gone. |
| `api/core-libraries/Core.EntityFrameworkCore/Repositories/Repository.cs` | `FindPaginatedAsync` uses `LongCountAsync` and `PageCalculator`. A page past the end returns `[]` without running the data query. No `double` and no `Math.Ceiling`. |
| `api/core-libraries/Core.OTP/Entities/OTP.cs` | Added `ReissueWindowStartedAt` and `ReissueWindow` (with a WHY comment). `Create`/`Reissue` take `DateTimeOffset now`. The limit check is now `>=`. The window resets on `ReissueWindowStartedAt`, not on `CreatedAt`. `HashesMatch` uses `CryptographicOperations.FixedTimeEquals`. The `CreatedAt` initializer is removed. |
| `api/core-libraries/Core.OTP/Repositories/IOtpRepository.cs` | `FindByRecipientAsync(string, CancellationToken)` replaces `FindAsync(recipient, requestIP, ct)` |
| `api/core-libraries/Core.EntityFrameworkCore/Repositories/OtpRepository.cs` | Looks up by recipient only |
| `api/core-libraries/Core.OTP/GenerateOTP/GenerateOTPHandler.cs` | Takes `TimeProvider timeProvider` as the last constructor parameter. Reads `GetUtcNow()` once and passes `now` to `Reissue`/`Create`. |
| `api/core-libraries/Core.OTP/DependencyInjection.cs` | `services.TryAddSingleton(TimeProvider.System);` |
| `api/core-libraries/Core.Localization/DependencyInjection.cs` | Uses `DefaultLanguageResolver` |
| `api/core-libraries/Core.Localization/LocalizationManager.cs` | Uses `DefaultLanguageResolver` (field type is now `string`). The trailing explanatory comment is removed. |
| `api/core-libraries/Core.Localization/LocalizedTextExtensions.cs` | The static captured default is removed. The thread default culture is read on every call, and the fallback arm is null-safe. |
| `api/core-libraries/Core.Notifications/Endpoints/NotificationEndpoints.cs` | Firebase group: `(…, string adminPolicyName)` with a guard and `.RequireAuthorization(adminPolicyName)`. Devices and user groups: `.RequireAuthorization()`. |
| `api/core-libraries/Core.Notifications/Endpoints/NotificationTemplateEndpoints.cs` | `(…, string adminPolicyName)` with a guard and `.RequireAuthorization(adminPolicyName)` |
| `api/core-libraries/Core.Notifications/Templates/DeleteNotificationTemplate/DeleteNotificationTemplateHandler.cs` | `SoftDelete(DateTimeOffset.UtcNow)` |
| `api/Elmanhg.Domain/Avatar/AvatarConversation.cs` | `SoftDelete(at)`. The manual `DeletedAt = at;` line is removed. |
| `api/Elmanhg.Domain/{ExamBlueprints/ExamBlueprint, Lessons/Lesson, Lessons/LessonObjective, SlaCalendars/ExamPeriod, Subjects/Subject, Teachers/TeacherSubject, Units/CurriculumUnit}.cs` | `var now = DateTimeOffset.UtcNow; SoftDelete(now); UpdatedBy = …; UpdationDate = now;` |
| `api/Elmanhg.Infrastructure/EssayGrading/EssayGradeRepository.cs`, `MathStepGrading/MathStepGradeRepository.cs` | `GetInReviewPageAsync` uses `PageCalculator`: it skips the data query past the end, and `TotalPages` comes from `PageCalculator.TotalPages` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated: adds `Otp.ReissueWindowStartedAt` |
| `api/Elmanhg.Api/Program.cs` | `CoreExceptionMiddleware` moved to directly after `CoreRequestLoggingMiddleware`, with the one comment line from the plan. No other change. |
| `api/Elmanhg.Tests/Builders/OtpBuilder.cs` | Added `IssuedAt(...)` and `WithReissueBlockCooldownInHours(...)`. `Build()` passes both to `Otp.Create`. |
| `api/Elmanhg.Tests/Core/Otp/OtpTests.cs` | T5, T6 and T11 updated as the plan specifies. T7–T10 and T12–T15 added. |
| `api/Elmanhg.Tests/Core/Otp/GenerateOTPHandlerTests.cs` | Substituted `TimeProvider`, uses the renamed repository method, adds T16–T17 |
| `api/Elmanhg.Tests/Domain/…` (7 files) | T2a–T2g. T4 added to `SubjectTests`. Existing tests are untouched. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | **Not in the plan.** See Deviations. |
| `.claude/skills/dotnet-feature/SKILL.md` | §4.1 row and line, §4.5, §6.1, §7.4 and the §8.4 DO example, as the plan worded them |
| `docs/constitution.md` | Lines 143 and 146, as the plan worded them. Line 3 is untouched. |
| `docs/otp-delivery.md` | §1 "Resend limits" paragraph, as the plan worded it |

The Postman collection is unchanged: no Elmanhg endpoint was added, changed or removed.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `AppDbContextTests` (no pending migrations) covers the migration "already in the suite". It is not listed under *Existing code touched*. | `Migrate_FreshDatabase_LeavesNoPendingMigrations` asserts the **exact ordered list** of applied migrations with `SatisfyRespectively`. A new migration fails it (it failed on the first full run: 5172/5173). Every earlier migration story (e.g. E18.S9, E18.S7, E18.S2) appended its migration to this list. | Appended `fortySixth => fortySixth.Should().EndWith("_AddOtpReissueWindow")` to that one assertion. Nothing was removed or loosened. The test is now stricter. |
| T40: assert body `code == "UNHANDLED_EXCEPTION"` | Same value | Asserted against the constant `ExceptionErrorCodes.UnhandledException` instead of the literal (no magic string). Same behaviour. |
| T42: the single item on page 2 (implicit) | Ordering by `Guid` differs between .NET `Guid.CompareTo` and the Postgres `uuid` order only in edge cases | I assert the item is `ids.MaxBy(x => x.ToString())`. Canonical string order matches the Postgres `uuid` byte order exactly. |

## Build & test
- `dotnet build api/`: **Build succeeded. 0 Warning(s), 0 Error(s)** (the final incremental build). The first full build of the API showed 9 warnings, all pre-existing CS8618/CS8602 in Core.Notifications, Core.OTP (`Recipient`/`CodeHash`; only the line number moved) and Core.Validation. None are new.
- `dotnet test api/` (Docker 29.6.2, Testcontainers Postgres): **Passed! total 5173, failed 0, succeeded 5173, skipped 0** (1m 31s).
  - The first run, before the `AppDbContextTests` deviation, gave 5172/5173. The only failure was `Migrate_FreshDatabase_LeavesNoPendingMigrations`, as explained above.
  - The new and changed classes, run in isolation with `--filter-class` (11 classes): **63/63 passed**. That count matches T1–T46 with the theory rows expanded, plus the existing OTP tests.
- `dotnet ef migrations has-pending-model-changes -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`: **"No changes have been made to the model since the last migration."**
- `dotnet format api/Elmanhg.slnx --verify-no-changes`: **not clean, and it was not clean before this change either.** It reports 100 WHITESPACE findings on `main` with my changes stashed, and the same 100 with them applied. They are pre-existing style issues in the vendored `core-libraries` (multi-line parameter lists in `Repository.cs`, `Otp(Guid id): base`, `MaxReissueCount? `…) plus `Tests/Builders/SubscriptionBuilder.cs`. None is on a line I added. CI does not run `dotnet format`. The DoD item "format exits 0" cannot be met without reformatting vendored files outside this plan's scope, so I did not do that.
- Guard grep (`DateTime.Now/UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`) on the `.cs` diff, including new files: no matches.
- No `#<number>` in any code or test file I added. The only hits in the diff are in the `.process` plan.
- No `Elmanhg` name inside `api/core-libraries`.
- `npm`/`pytest`: not run. This change touches neither `web/` nor `ai/`.

## Deferred items
None from the plan. The plan's out-of-scope list is unchanged: Core.Notifications is not registered or mapped in the app; `CountAsync` still returns `int`; `Verify`/`MarkUsed` still read `DateTimeOffset.UtcNow`; there is no `DeletedAt` backfill.

## Notes for review
- **Test file line endings.** New files were written with LF. The repo uses `core.autocrlf=true`, so git normalises them on commit. Edited existing files keep their CRLF and BOM.
- **`LocalizationManagerTests` is not in `DefaultThreadCultureCollection` (as planned).** Its `CultureScope` restores `DefaultThreadCurrentCulture` on dispose. This cannot race with `LocalizedTextExtensionsTests`, because that collection has `DisableParallelization = true` and runs alone.
- **`CoreNotificationEndpointsTests.Map` does not dispose the `WebApplication` it builds.** The plan gives this exact helper body. The app is never started.
- **Two SKILL.md lines still show the parameterless `SoftDelete()`.** One is in the §2 naming table, line 146 ("`Delete()` pairs with `SoftDelete()`"). The other is the §8.4 **DON'T** example. The plan only rewrote the §8.4 DO example and §4.1, so I left both as they were. Line 146 reads as prose about the pairing, not as a signature.
- **Migration file name.** The timestamp is `20261006210928`, generated by `dotnet ef` in UTC. Other lanes (308/310/313) may add migrations too. Whoever merges second will need to regenerate theirs and update the `AppDbContextTests` ordered list.
- **Legacy `Otps` rows.** They get `ReissueWindowStartedAt = 0001-01-01`, so their next resend starts a new window (D8). Their `ReissueCount` resets to 0 on that resend. That matches the old behaviour after 24 h, as the plan intends.
- **Behaviour change visible to clients.** With the default options, a recipient now gets 5 resends per 24 h instead of 6. The 5th resend also pushes the next allowed resend out by 24 h, as before. The existing `OtpEndpointTests` cooldown test still passes.
