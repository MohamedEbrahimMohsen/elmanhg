VERDICT: APPROVED

# Review — [E21.S1] Core bug fixes: soft delete, OTP, localization, paging overflow, middleware order (round 1)

## Blocking
None.

## Non-blocking
- `.claude/skills/dotnet-feature/SKILL.md:921`: the §8.4 "DO NOT" example still calls the parameterless `SoftDelete()`, which no longer compiles. The example is about naming (`Removed` vs `Deleted`), and §4.1 (lines 196 and 201) and the §8.4 DO example (line 925) give the correct signature, so the doc does not give a second answer about the signature. Changing it to `SoftDelete(now)` would stop anyone copying an example that does not compile. `SKILL.md:146` ("`Delete()` pairs with `SoftDelete()`") describes the naming pairing, not the signature. It is fine as it is.
- `api/Elmanhg.Tests/Domain/Lessons/LessonLifecycleTests.cs:222`: the new test comes after the private `NewLesson()` helper. Elsewhere, tests come before helpers.
- `api/Elmanhg.Tests/Integration/Persistence/RepositoryPagingTests.cs:47-69` (T43/T44): the database is empty (total 0), so `IsPastEnd` returns true on `offset >= total` alone. The `offset > int.MaxValue` arm is only covered by `PageCalculatorTests.IsPastEnd_OffsetBeyondIntRange_ReturnsTrue` and T41. The tests still fail on the old code: `(int.MaxValue - 1) * 50` wraps to -100, which gives a negative OFFSET. Seeding one in-review grade would make these tests stronger.
- `api/core-libraries/Core.OTP/Entities/OTP.cs:64-68` with `20261006210928_AddOtpReissueWindow.cs:19`: after the migration, every legacy row gets 0001-01-01, so its next resend resets `ReissueCount` even if the row is mid-quota. This happens once and the cooldown still bounds it (plan D8). It is acceptable and already disclosed.
- `api/Elmanhg.Tests/Core/Notifications/CoreNotificationEndpointsTests.cs:56-63`: the `WebApplication` that is built is never disposed. It is never started, and the plan gave this exact body.
- Migration ordering: lanes 308/310/313 may add migrations too. Whoever merges second regenerates their migration and extends `AppDbContextTests.cs:23`.

## Verified
- **Build:** `dotnet build Elmanhg.slnx --no-incremental` reports 0 errors and 9 warnings. All 9 are older CS8618/CS8602 warnings in Core.Notifications, Core.OTP (`Recipient`/`CodeHash`, ctor line) and Core.Validation. None is new.
- **Tests:** `dotnet test Elmanhg.Tests --no-build` passed 5173 of 5173, with 0 skipped. This matches the claim.
- **Format:** `dotnet format --verify-no-changes --include` (changed and new .cs files) flags only lines this change did not touch: `Entity.cs:20`, `Repository.cs:18-45/135-195`, `NotificationEndpoints.cs:110-111`, `OTP.cs:18/37/95`. No new file is flagged. The claim that the format issues predate this change holds.
- **Deviations:**
  - `AppDbContextTests.cs:23`: the only change is an appended `_AddOtpReissueWindow` element. Nothing was loosened, and the deviation is disclosed.
  - T40 uses the `ExceptionErrorCodes.UnhandledException` constant.
  - T42 orders by `ToString()`. That is correct, because Postgres `uuid` compares in canonical byte order.
- **SoftDelete:**
  - `Entity.cs:21-25` takes `DateTimeOffset deletedAt`, and the parameterless overload is gone. The compiler forces every caller to pass a time.
  - All 8 app call sites pass the same `now` they stamp on `UpdationDate`.
  - `AvatarConversation.cs:69` passes the truncated `at`, and the manual assignment is gone.
  - The core `DeleteNotificationTemplateHandler.cs:24` passes `DateTimeOffset.UtcNow`.
  - No app code reads `DeletedAt` for logic. `AuditChangeReader` ignores it, so audit diffs do not change.
- **OTP:**
  - **Constant-time compare:** `OTP.cs:147` uses `FixedTimeEquals` over UTF-8 bytes. A length mismatch returns false (T15).
  - **Resend limit:** `OTP.cs:82` checks `>=`. T6 (Max 1) fails on the old `>` check, and T7 (Max 0) fails too.
  - **Daily window:** it is anchored on the new `ReissueWindowStartedAt` (`OTP.cs:30,49,64-68`), never on `CreatedAt`, which every resend still overwrites. T9 would fail against a `CreatedAt` anchor, and T10 pins that the window start does not move.
  - **Mutation before a throw:** the window reset happens before the guards that can throw. A throw happens before `SaveChangesAsync`, so nothing is persisted.
  - **Recipient-only lookup:** `OtpRepository.cs:16` and `IOtpRepository.cs:8` look up by recipient only. The old `RequestIP == null` filter matched every row anyway, because the column was never written. Dropping it removes the "change network to reset limits" vector, and `docs/otp-delivery.md` §1 documents this.
  - **Time source:** `GenerateOTPHandler.cs:17` reads `TimeProvider` once. `Core.OTP/DependencyInjection.cs:19` calls `TryAddSingleton(TimeProvider.System)`.
  - **Migration:** it adds exactly one column and drops nothing. The snapshot is updated, and `AppDbContextTests` passes.
- **Localization:**
  - `DefaultLanguageResolver` is the only reader of `CoreLocalization:DefaultLanguage`, and its `[..2]` is guarded. It is used by `DependencyInjection.cs:13` and `LocalizationManager.cs:8`.
  - No `Localization:DefaultLanguage` key remains in core.
  - `LocalizedTextExtensions.cs:8-19` reads the default per call, and the fallback arm is null-safe.
  - The app sets `CoreLocalization:DefaultLanguage: "ar"`, and nothing in the app sets `DefaultThreadCurrentCulture`, so `CultureScope` restoring it cannot affect parallel integration tests.
- **Paging:**
  - `PageCalculator` computes the offset in `long`. The extremes, `(int.MaxValue-1)*int.MaxValue` and `(int.MinValue-1)*int.MaxValue`, fit in `long`.
  - `TotalPages` uses no `double` and returns 0 for `pageSize <= 0`.
  - `IsPastEnd` handles `offset == total` (empty) and `offset > int.MaxValue`. `offset == int.MaxValue` with a larger total casts safely.
  - All three paged queries use it, and core uses `LongCountAsync`. `PageData` fields are already `long`.
  - Behaviour for page 0 and negative pages is unchanged, as plan D15 specifies.
- **Notifications:**
  - The template and Firebase groups take `adminPolicyName`, guarded with `ThrowIfNullOrWhiteSpace`. The devices and user groups call `RequireAuthorization()`.
  - The module is still not registered or mapped in Elmanhg.Api/Infrastructure/Application.
  - `CoreDbContext` is unchanged.
  - Core holds no Elmanhg names. `Probe.Admin` exists only in tests.
- **Middleware:**
  - In `Program.cs:168-169`, `CoreExceptionMiddleware` comes directly after request logging and before HTTPS redirection, media and authorization. No other line moved.
  - T39/T40 call anonymous `POST /api/auth/logout` on a derived host with a throwing `IAuthorizationHandler`. Under the old order the exception would escape the exception middleware, so both tests would fail. They therefore constrain the order.
- **Plan coverage:**
  - Every file in *Files to create* exists, and nothing extra was created.
  - All tests T1–T46 exist with the planned names (T3 untouched).
  - The guard grep finds no match in the diff.
  - No `#<number>` appears in any changed or new `.cs` file.
- **Postman:** no Elmanhg endpoint was added, changed or removed. The core notification endpoints are unmapped, so the collection needs no change.
- **Docs sync:**
  - The SKILL.md edits to §4.1, §4.5, §6.1, §7.4 and the §8.4 DO example were applied as planned.
  - `docs/constitution.md` lines 143 and 146 were updated, and `docs/otp-delivery.md` §1 has the resend-limits paragraph. These match the code: 5 resends per 24 h window anchored at the first code, recipient-only lookup and constant-time compare.
  - `docs/security.md` V11 and `docs/audit-log.md` stay true.
  - No divergence found.

## Test quality
- `OtpTests`: T6, T7, T9 and T10 each fail against the old limit or anchor. T13–T15 pin match and mismatch. Constant-time behaviour itself cannot be observed in a test, which is acceptable.
- `GenerateOTPHandlerTests`: T16 asserts times derived from the substituted `TimeProvider`, which is not an echo of the substitute. T17 checks `AddAsync DidNotReceive`, `SaveChangesAsync Received(1)` and `SendAsync Received(1)`, so it constrains the implementation.
- `EntityTests` and the T2a–T2g domain tests fail against `DeletedAt = null`. T4 guards the throwing path.
- `PageCalculatorTests` covers the boundaries.
- `DefaultLanguageResolverTests` and `LocalizationManagerTests` constrain the implementation. T26 pins the legacy key as ignored, and T27 pins the short-value guard.
- `LocalizedTextExtensionsTests` T32 would fail with a static captured default.
- `CoreNotificationEndpointsTests` checks endpoint counts and the policy on each endpoint.
- `ExceptionMiddlewareOrderTests` constrains the order.
- `RepositoryPagingTests` T41/T42 constrain the implementation. T43/T44 are weaker; see the non-blocking note.
- `OtpRepositoryTests` checks that the new column round-trips.
