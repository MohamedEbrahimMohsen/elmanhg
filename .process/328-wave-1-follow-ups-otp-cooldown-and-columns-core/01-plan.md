# Plan — [E21.S11] Wave-1 follow-ups: OTP cooldown and columns, Core.Storage.S3, cache TTL, messaging tests

## Goal
This story closes the follow-ups in issue 319, left over from the first E21 wave. After it ships:
- The long block after the last allowed OTP resend is timed from that resend.
- The `Otps` table loses its two dead columns.
- `Core.Storage` (and so `Elmanhg.Application`) no longer pulls in `AWSSDK.S3`, because the S3 provider moves to a new `Core.Storage.S3` project.
- The query cache has its own default-TTL setting (`Caching:DefaultSeconds`). Dashboard cards still use `Dashboard:CacheSeconds`.
- The SMS OTP client gets back its `HttpSmsOtpChannel` named HttpClient.
- Tests now cover the invitation and reminder Resend retries and the registration of `CachingBehaviour`.
- The merged code from PRs 315–318 gets an independent correctness and security review.
- The pipeline gets a note about large PRs.

Users and clients see no change.

## Scope
**In:** every sub-task of the story:
1. OTP: the clock check (verify only), the cooldown timing, dropping the two columns (migration), tests.
2. The `Core.Storage.S3` split, and a deterministic private-folder test.
3. The cache default-TTL setting, and the `CachingBehaviour` registration test.
4. The SMS client name, and the Resend retry tests.
5. The review of PRs 315–318 (a reviewer task), and the pipeline note.
6. Docs.

**Out (parallel lane, issue 329; do not touch):**
- `Core.EntityFrameworkCore/Repositories/Repository.cs` `SaveChangesAsync` stamping.
- Any EF `SaveChangesInterceptor` on `AppDbContext`.
- `Elmanhg.Tests/Core/EntityFrameworkCore/ConflictMapTests.cs`, `RefreshTokenRotatorTests`, `Elmanhg.Tests/Core/Cache/CachingBehaviourTests.cs`, `RuntimeSettingRegistryTests`, `RefreshAccessTokenHandlerTests`.
- `Core.Queues/SweepWorker.cs`.

Also out: `CoreDbContext` structure, and `Core.Notifications`. Dev decision of 2026-10-06: both are kept as they are.

**Deferred:** nothing.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | `Otp.Verify`/`MarkUsed` clock | No code change. Verified: `Verify(string, DateTimeOffset now)` and `MarkUsed(DateTimeOffset now)` take the time as a parameter. Their callers pass `timeProvider.GetUtcNow()`: `VerifyOTPHandler`, the `OtpRepositoryExtensions.ConsumeAsync` callers in `LoginWithPhoneHandler`, `RegisterWithPhoneHandler`, `LoginWithEmailCodeHandler` and `AcceptInvitationHandler`. `Core.OTP` has no `UtcNow` call. `OtpTests.Verify_ExpiredAtGivenTime_ReturnsExpired` and `MarkUsed_ExpiredAtGivenTime_ThrowsExpired` already cover it. The implementer ticks the sub-task and records this evidence in `02-implementation.md`. | Done by E21.S2. |
| D2 | Cooldown after the resend that uses up the quota | `NextAllowedReissueAt = ReissueCount == MaxReissueCount ? now.AddHours(ReissueBlockCooldownInHours) : now.AddSeconds(ReissueCooldownSeconds);` | The story says so. The old code (copied from Morabh `Core/Core.OTP/Entities/OTP.cs:87`) counted from the previous cooldown. |
| D3 | Dropping `RequestIP`/`UserAgent` | Delete both properties from `Otp`. `CoreDbContext` maps them by convention only: `CoreDbContext.cs` lines 34–47 have no `Property(x => x.RequestIP/UserAgent)`, so `CoreDbContext.cs` is **not edited**. One generated migration, `DropOtpRequestIpAndUserAgent`, does exactly two `DropColumn`s. | Verified that nothing writes them. Grep finds them only in `OTP.cs`, the `InitialCreate` migration, the snapshot and one test assertion. No raw SQL, no seed, no script, nothing in web/ or ai/. `Otp` has `private set` and no method assigns them, so every row holds NULL. |
| D4 | The assertion `otp.RequestIP.Should().BeNull()` in `OtpRepositoryTests` | Delete that single line. It cannot compile once the property is gone. The schema test T7 replaces it with a stronger check. | The story removes the property. This is not a weakened test. |
| D5 | How `Core.Storage` picks a provider without knowing S3 | Keyed services. `AddCoreFileStorage` registers `AddKeyedScoped<IFileStorage, LocalDiskFileStorage>(FileStorageProvider.Local)`. `IFileStorage` resolves `GetKeyedService<IFileStorage>(provider)`, and throws `InvalidOperationException` if nothing is registered for that provider. `Core.Storage.S3` adds `AddCoreS3FileStorage()`, which registers `IAmazonS3` and `AddKeyedScoped<IFileStorage, S3FileStorage>(FileStorageProvider.S3)`. | This is built into M.E.DI, so there is no new abstraction. `FileStorageOptions`, the validator and the enum stay in `Core.Storage`, so config is unchanged. |
| D6 | Namespace of the moved `S3FileStorage` | It stays `Core.Storage.S3`, now in assembly `Core.Storage.S3`. Move it with `git mv`. | `S3FileStorageTests` keeps compiling unchanged, and history is kept. |
| D7 | Where `AddCoreFileStorage_S3_ResolvesS3FileStorage` goes | It moves (the same body plus `.AddCoreS3FileStorage()`) to the new `CoreS3FileStorageDependencyInjectionTests`, and is deleted from `CoreFileStorageDependencyInjectionTests`. | `Core.Storage` alone can no longer resolve S3. That is the point of the story. "Moved code keeps its tests." |
| D8 | Deterministic private-folder test | Add a new `[Fact]` that writes the file under an upper-case `TEACHER-THREADS` folder, asserts that the inner `PhysicalFileProvider` sees it (`Exists == true`), then asserts that `PublicMediaFileProvider` returns not-found. The existing `[Theory]` rows stay as they are. | The not-found is then proven to come from `IsPrivate`. On a case-insensitive file system the two folder names are the same folder; on a case-sensitive one they are two folders. The test passes on both. Existing tests are not edited. |
| D9 | Cache default TTL that does not move dashboard behaviour | The core adds a named **cache profile**: `ICacheableQuery.CacheProfile` (default `null`), `CachingOptions.Profiles` (name → TTL) and `CachingOptions.ResolveTtl(query)`, which returns, in order of precedence, `query.Ttl`, then the profile TTL, then `DefaultTtl`. The dashboard queries return profile `"dashboard"`, and the app sets `Profiles["dashboard"]` from `Dashboard:CacheSeconds`. `DefaultTtl` comes from the new `Caching:DefaultSeconds`. | A query record cannot read options, so it cannot set its own `Ttl` from config. Passing the TTL through 9 constructors and the controller would be invasive. A profile name is app-agnostic and is the smallest addition. |
| D10 | Options class for `Caching:DefaultSeconds` | New app class `Elmanhg.Application.Shared.Options.QueryCachingOptions` (`SectionName = "Caching"`, `[Range(0, 3600)] int DefaultSeconds = 60`). The app maps it onto core `CachingOptions.DefaultTtl`, the same way `AuthOptions` is mapped onto `RefreshTokenRotationOptions` in `Application/DependencyInjection.cs`. | `CachingOptions` is already the core type's name. The default of 60 keeps today's effective default for any future profile-less cacheable query. No query uses it today. |
| D11 | Existing `DashboardCachingTests` that assert `DefaultTtl` follows `Dashboard:CacheSeconds` | Rewrite 3 tests (T19–T21) to assert `ResolveTtl(dashboard query)` instead of `DefaultTtl`. The names change as listed. The assertions keep the same strength: 45 s, 60 s and 0. | The story changes the contract they pin: `DefaultTtl` is no longer the dashboard TTL. The *behaviour* they protect (45/60/0 for dashboard cards) is still asserted. This is story-mandated, not a weakened test. |
| D12 | SMS client name | Change `services.AddHttpClient<HttpSmsClient>()` to `services.AddHttpClient<HttpSmsClient>(nameof(HttpSmsOtpChannel))`. The resilience call and `AddScoped<HttpSmsOtpChannel>()` stay. | This restores the name used before E21.S5. `HttpSmsClient` stays resolvable as a typed client, so `CoreOtpDeliveryRegistrationTests.AddCoreOtpDelivery_EnabledProviders_ResolveTransportTypedClients` still passes. |
| D13 | `OtpDeliveryResilienceTests.HttpSms_ServerError_MakesExactlyOneAttempt` passes `nameof(HttpSmsClient)` as the client name | Change that argument to `nameof(HttpSmsOtpChannel)`. That is the only edit. | Story-mandated rename. With the old name the stub is never attached. |
| D14 | Proving that the Application layer has no AWS SDK | An XML project-graph test (T13–T14), the same approach as `DomainAssemblyReferencesTests`. | `GetReferencedAssemblies` only lists assemblies used in IL, so it cannot see a transitive package. |
| D15 | Review of PRs 315–318 | A **reviewer** task in Stage 3. It is not an implementer file. See "Review step". | The story puts it in this story's review. |
| D16 | Pipeline note location | One paragraph in `.claude/commands/feature.md`, Stage 4, inserted after step 1 ("Commit & PR"). | Stage 4 is where CodeRabbit runs. `.claude/commands/` is the pipeline's own config, not product docs. |
| D17 | Docs for `Caching:DefaultSeconds` | Add a new `### Query cache` table in `docs/deployment.md` §4. `docs/configuration.md` is not changed. | `configuration.md` is about runtime settings. Its §7 row "cache seconds" already covers this key as not runtime-editable. |

## Existing code touched
| File | Change |
|------|--------|
| `api/core-libraries/Core.OTP/Entities/OTP.cs` | Delete `RequestIP` and `UserAgent` (lines 19–20) and the blank line between them and `CodeHash`. Change the last line of `Reissue` per D2. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef`. The two properties go from `Core.OTP.Entities.Otp`. |
| `api/core-libraries/Core.Storage/Core.Storage.csproj` | Remove `<PackageReference Include="AWSSDK.S3" />`. Keep the `FrameworkReference`. |
| `api/core-libraries/Core.Storage/DependencyInjection.cs` | Remove the `Amazon*` and `Core.Storage.S3` usings, the `IAmazonS3` registration, `AddScoped<S3FileStorage>`, `AddScoped<LocalDiskFileStorage>` and `CreateS3Client`. See F2 for the new body of `AddCoreFileStorage`. `UseCorePublicMedia` is unchanged. |
| `api/core-libraries/Core.Storage/S3/S3FileStorage.cs` | `git mv` to `api/core-libraries/Core.Storage.S3/S3FileStorage.cs`. Content unchanged. The `S3/` folder disappears. |
| `api/Elmanhg.slnx` | Add `<Project Path="core-libraries/Core.Storage.S3/Core.Storage.S3.csproj" />` right after the `Core.Storage` line, inside `/core-libraries/`. |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | Add `<ProjectReference Include="..\core-libraries\Core.Storage.S3\Core.Storage.S3.csproj" />` after the `Core.Storage` reference, and keep that reference. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddCoreFileStorage();` becomes `services.AddCoreFileStorage().AddCoreS3FileStorage();`. Add `using Core.Storage.S3;`. |
| `api/core-libraries/Core.Cache/ICacheableQuery.cs` | Add the member `string? CacheProfile => null;` after `Ttl`. |
| `api/core-libraries/Core.Cache/CachingOptions.cs` | See F6. |
| `api/core-libraries/Core.Cache/CachingBehaviour.cs` | `var ttl = query.Ttl ?? cachingOptions.Value.DefaultTtl;` becomes `var ttl = cachingOptions.Value.ResolveTtl(query);`. Nothing else changes. |
| `api/Elmanhg.Application/Dashboard/Shared/DashboardCacheKey.cs` | Add `public const string Profile = "dashboard";` above `For`. |
| `api/Elmanhg.Application/Dashboard/Shared/IDashboardRangeQuery.cs` | Body: `string? ICacheableQuery.CacheProfile => DashboardCacheKey.Profile;` |
| `api/Elmanhg.Application/Dashboard/GetContentMetrics/GetContentMetricsQuery.cs` | Add `public string? CacheProfile => DashboardCacheKey.Profile;` after `CacheKey`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | Replace line 55 (`services.AddOptions<CachingOptions>().Configure<IOptions<DashboardOptions>>(...)`) with the F8 block. Add `using Elmanhg.Application.Dashboard.Shared;` if it is missing. |
| `api/Elmanhg.Api/appsettings.example.json` | Add the line `"Caching": { "DefaultSeconds": 60 },` right above the `"Dashboard"` line. |
| `api/core-libraries/Core.OTP/Delivery/CoreOtpDeliveryServiceCollectionExtensions.cs` | D12. A one-line change. |
| `api/Elmanhg.Tests/Integration/Persistence/OtpRepositoryTests.cs` | D4. Delete line 30 (`otp.RequestIP.Should().BeNull();`). |
| `api/Elmanhg.Tests/Core/Storage/CoreFileStorageDependencyInjectionTests.cs` | D7. Delete the `AddCoreFileStorage_S3_ResolvesS3FileStorage` method and the now-unused `using Core.Storage.S3;`. Add T10. |
| `api/Elmanhg.Tests/Core/Storage/PublicMediaFileProviderTests.cs` | Add T12. Existing members are unchanged. |
| `api/Elmanhg.Tests/Core/Otp/OtpTests.cs` | Add T1–T3. |
| `api/Elmanhg.Tests/Application/Features/Dashboard/Shared/DashboardCachingTests.cs` | D11. Rewrite T19–T21 and add T22–T25. |
| `api/Elmanhg.Tests/Integration/Composition/PipelineCompositionTests.cs` | Add T26. |
| `api/Elmanhg.Tests/Core/Otp/Delivery/OtpDeliveryResilienceTests.cs` | D13. Line 37: `nameof(HttpSmsClient)` becomes `nameof(HttpSmsOtpChannel)`. Remove `using Core.Messaging.Sms;` only if it becomes unused. |
| `api/Elmanhg.Tests/Infrastructure/DependencyInjectionTests.cs` | Add T11. |
| `.claude/commands/feature.md` | The pipeline note (F12). |
| Docs | See "Docs to update". |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| F1 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_DropOtpRequestIpAndUserAgent.cs` + `.Designer.cs` | EF migration | Generated by `dotnet ef migrations add DropOtpRequestIpAndUserAgent -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. `Up` must be exactly `DropColumn(name: "RequestIP", table: "Otps")` and `DropColumn(name: "UserAgent", table: "Otps")`. `Down` must be exactly two `AddColumn<string>(name, table: "Otps", type: "text", nullable: true)`. If any other operation appears, stop and report `BLOCKED`. If main gains a migration before merge, rebase, delete this migration and regenerate it. |
| F2 | `api/core-libraries/Core.Storage/DependencyInjection.cs` (rewritten body) | static class `Core.Storage.DependencyInjection` | `AddCoreFileStorage(this IServiceCollection services)` does these steps in order. (1) The existing options line, unchanged. (2) The `IValidateOptions<FileStorageOptions>` registration, unchanged. (3) `services.AddKeyedScoped<IFileStorage, LocalDiskFileStorage>(FileStorageProvider.Local);` (4) `services.AddScoped<IFileStorage>(serviceProvider => Resolve(serviceProvider));` (5) `return services;`. Add a private method `private static IFileStorage Resolve(IServiceProvider serviceProvider)`. It reads `var provider = Options(serviceProvider).Provider;`, then `return serviceProvider.GetKeyedService<IFileStorage>(provider) ?? throw new InvalidOperationException($"No file storage is registered for FileStorage:Provider '{provider}'.");`. Keep the private `Options` helper. |
| F3 | `api/core-libraries/Core.Storage.S3/Core.Storage.S3.csproj` | csproj | `Sdk="Microsoft.NET.Sdk"`. `TargetFramework net10.0`, `ImplicitUsings enable`, `Nullable enable`, the same PropertyGroup as `Core.Storage.csproj`. `<PackageReference Include="AWSSDK.S3" />` (the version comes from `Directory.Packages.props`, which already pins it). `<ProjectReference Include="..\Core.Storage\Core.Storage.csproj" />`. |
| F4 | `api/core-libraries/Core.Storage.S3/S3FileStorage.cs` | moved (D6) | `public sealed class S3FileStorage(IAmazonS3 s3, IOptions<FileStorageOptions> fileStorageOptions) : IFileStorage`, unchanged. |
| F5 | `api/core-libraries/Core.Storage.S3/DependencyInjection.cs` | `namespace Core.Storage.S3; public static class DependencyInjection` | `public static IServiceCollection AddCoreS3FileStorage(this IServiceCollection services)` runs `services.AddSingleton<IAmazonS3>(serviceProvider => CreateS3Client(serviceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value));`, then `services.AddKeyedScoped<IFileStorage, S3FileStorage>(FileStorageProvider.S3);`, then `return services;`. `private static AmazonS3Client CreateS3Client(FileStorageOptions options)` is moved verbatim from `Core.Storage/DependencyInjection.cs`. Usings: `Amazon`, `Amazon.Runtime`, `Amazon.S3`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`. |
| F6 | `api/core-libraries/Core.Cache/CachingOptions.cs` (rewritten) | `public sealed class CachingOptions` | Keep `public TimeSpan DefaultTtl { get; set; } = TimeSpan.Zero;`. Add `public Dictionary<string, TimeSpan> Profiles { get; } = new(StringComparer.Ordinal);`. Add `public TimeSpan ResolveTtl(ICacheableQuery query) => query.Ttl ?? (query.CacheProfile is { } profile && Profiles.TryGetValue(profile, out var ttl) ? ttl : DefaultTtl);`. |
| F7 | `api/Elmanhg.Application/Shared/Options/QueryCachingOptions.cs` | `namespace Elmanhg.Application.Shared.Options; public sealed class QueryCachingOptions` | `public const string SectionName = "Caching";` `[Range(0, 3600)] public int DefaultSeconds { get; set; } = 60;` `using System.ComponentModel.DataAnnotations;`. Same shape as `DashboardOptions`. |
| F8 | Block in `Application/DependencyInjection.cs` | DI | `services.AddValidatedOptions<QueryCachingOptions>(QueryCachingOptions.SectionName);` followed by `services.AddOptions<CachingOptions>().Configure<IOptions<QueryCachingOptions>, IOptions<DashboardOptions>>((caching, queryCaching, dashboard) => { caching.DefaultTtl = TimeSpan.FromSeconds(queryCaching.Value.DefaultSeconds); caching.Profiles[DashboardCacheKey.Profile] = TimeSpan.FromSeconds(dashboard.Value.CacheSeconds); });`. Lambda body on separate lines, braces style. |
| F9 | `api/Elmanhg.Tests/Core/Storage/S3/CoreS3FileStorageDependencyInjectionTests.cs` | test class `Elmanhg.Tests.Core.Storage.S3.CoreS3FileStorageDependencyInjectionTests` | T8–T9. A private `BuildProvider(Dictionary<string,string?> settings, bool withS3)` that copies the existing `BuildProvider` and appends `.AddCoreS3FileStorage()` when `withS3`. The S3 settings dictionary is copied from the moved test. |
| F10 | `api/Elmanhg.Tests/Core/Storage/StorageProjectReferencesTests.cs` | test class `Elmanhg.Tests.Core.Storage.StorageProjectReferencesTests` | T13–T14. Private helpers `FindProject(string relativePath)`, which walks up from `AppContext.BaseDirectory` to `api/<relativePath>` the way `DomainAssemblyReferencesTests.FindDomainProject` does, and `CollectProjects(string root)`, copied from `DomainAssemblyReferencesTests`. Do not edit `DomainAssemblyReferencesTests`. |
| F11 | `api/Elmanhg.Tests/Core/Cache/CachingOptionsTests.cs` | test class `Elmanhg.Tests.Core.Cache.CachingOptionsTests` | T15–T18 and T18a. A file-local probe `public sealed record ProfileProbeQuery(string? Profile, TimeSpan? QueryTtl = null) : IRequest<string>, ICacheableQuery { public string CacheKey => "probe"; public TimeSpan? Ttl => QueryTtl; public string? CacheProfile => Profile; }`. Do **not** reuse or edit `CachingBehaviourTests.cs` or its `CacheProbeQuery`. |
| F12 | `.claude/commands/feature.md` | process doc | Insert this paragraph as a new step after Stage 4 step 1 and renumber the later steps 3→6, verbatim: "**Large PRs.** CodeRabbit skips a PR that changes more than 100 files, and the pipeline reviewer is then the only review. When a story moves or renames code across more than 100 files, split it into PRs of at most 100 files each, merged in order and each green on its own: for example, the project move first, then the namespace and caller updates. When a PR still exceeds 100 files, or CodeRabbit is rate-limited and never reviews it, the Stage 3 reviewer runs an explicit correctness and security pass over the whole diff and records it in `05-coderabbit-comments.md`." |
| F13 | `api/Elmanhg.Tests/Infrastructure/Messaging/ResendRetryTests.cs` | test class `Elmanhg.Tests.Infrastructure.Messaging.ResendRetryTests` | T27–T28. Fields: `private readonly StubHttpMessageHandler _handler = new() { StatusCode = HttpStatusCode.InternalServerError };`. Helper `BuildProvider(string clientName)`: `ServiceCollection` with `AddLogging()`, `Substitute.For<IHostEnvironment>()`, and an `IConfiguration` from `OtpDeliveryTestSettings.ToConfiguration(OtpDeliveryTestSettings.WithResend())` plus `["InvitationEmail:AcceptInviteUrl"] = "https://elmanhg.test/accept-invite"` and `["OutOfAppReminders:ThreadLinkBaseUrl"] = "https://site.test/teacher/thread/"`. Then `services.AddOtpDelivery(); services.AddInvitationEmail(); services.AddMessaging();`, then `services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => _handler);` and `services.PostConfigure<HttpStandardResilienceOptions>($"{clientName}-standard", resilience => resilience.Retry.Delay = TimeSpan.Zero);`. This mirrors `MetaWhatsAppMessageChannelTests.BuildResilientProvider`. |
| F14 | `api/Elmanhg.Tests/Integration/Persistence/OtpTableSchemaTests.cs` | test class `Elmanhg.Tests.Integration.Persistence.OtpTableSchemaTests(ApiFactory factory)` | T7. Shares the factory the way `OtpRepositoryTests` does (primary-constructor `ApiFactory`, no `IClassFixture`). |

## Error codes
None added or changed.

## Domain behaviour
`Otp.Reissue(string newCodeHash, int expiresInMinutes, DateTimeOffset now)`. Every line stays the same except the last:
```csharp
NextAllowedReissueAt = ReissueCount == MaxReissueCount ? now.AddHours(ReissueBlockCooldownInHours) : now.AddSeconds(ReissueCooldownSeconds);
```
The guards are unchanged: `RateLimitExceededCoreException(ErrorCodes.OTPReissueCooldown, context)` and `RateLimitExceededCoreException(ErrorCodes.OTPReachedMaxReissueCount)`. `Otp` is a `Core.DDD` `Entity` with no `UpdationDate` member of its own, so nothing new is stamped. No comment is added.

`CachingBehaviour` behaviour is unchanged for queries without a profile. With a profile, the TTL is the profile's value, and `<= 0` bypasses the cache, the same as today.

## API surface
No endpoint changes. Config gains `Caching:DefaultSeconds` (int, 0–3600, default 60, validated at startup).

## Review step (Stage 3, reviewer; no implementer files)
The Stage 3 reviewer runs an independent correctness and security review of the code merged by:

| PR | Story | Merge commit |
|---|---|---|
| 315 | E21.S7 Core.Storage | `7cf514af` |
| 316 | E21.S10 Core.Hosting/Observability/Cache/Spreadsheets | `13cef235` |
| 317 | E21.S1 Core bug fixes | `d8e8f743` |
| 318 | E21.S5 Core.Http/Core.Messaging | `315a6a9a` |

**Method:**
1. Run `git show --stat <sha>` for each commit.
2. For every changed production file, read the diff and the file's **current** state on this branch.

**Checklist:**
- Media path handling: traversal, private folders, `~` aliases, S3 keys.
- Forwarded-header and trusted-proxy configuration.
- Rate-limit partition keys: no spoofable partitions.
- Kestrel limits.
- Placeholder-secret guard.
- Secrets or PII in logs and metrics: redaction and phone masking.
- Outbound HTTP: base-address and URL validation (https only), retry only with idempotency keys, timeouts, breakers per caller.
- OTP: constant-time compare, resend and verify limits.
- Soft-delete filters.
- Localization fallbacks.
- Paging overflow.
- Middleware order.
- Moved code: behaviour parity with the code it replaced.

**Output:** `.process/328-wave-1-follow-ups-otp-cooldown-and-columns-core/03-merged-pr-review.md`.
- First line: `MERGED-PR REVIEW: <c> critical, <m> major, <n> minor`.
- One row per finding: file:line, the PR it came from, severity, evidence.

**Findings:**
- Critical: blocks this story. It goes to the normal rework loop if the fix is in a file this story touches. Otherwise it becomes its own issue, labelled `review-debt`, opened by the orchestrator.
- Major and minor: one `review-debt` issue that lists them verbatim.

## Docs to update (docs-sync)
| Doc | Section | Change |
|---|---|---|
| `docs/otp-delivery.md` | line 18, "Resend limits" | Replace "the `RequestIP` and `UserAgent` columns are not filled, so a new network does not reset the limits" with "the row stores no IP address or user agent, so a new network does not reset the limits". Replace "The resend that uses up the quota also pushes the next one out by `ReissueBlockCooldownInHours` (24)" with "The resend that uses up the quota also blocks the next one for `ReissueBlockCooldownInHours` (24), counted from that resend". |
| `docs/otp-delivery.md` | line 111 | In the list of named clients, add `HttpSmsOtpChannel` to the examples, giving "e.g. `MetaWhatsAppOtpChannel`, `HttpSmsOtpChannel`, `MetaWhatsAppMessageChannel`, `ResendInvitationEmailSender`". Then add this sentence: "Retries stay on for every Resend caller (OTP, invitations, reminders) and off for every Meta and SMS caller; tests pin each one." |
| `docs/dashboard.md` | `## Caching`, first paragraph | Append: "The cards use the `dashboard` cache profile, whose lifetime is `Dashboard:CacheSeconds`. The query cache's own default, `Caching:DefaultSeconds`, applies only to cacheable queries without a profile or their own lifetime, and none exists today." |
| `docs/deployment.md` | §4, new `### Query cache (api.env)` table right before `### Dashboard` | Row: `` `Caching__DefaultSeconds` `` \| `60` \| "0 to 3600; in-memory lifetime for a cacheable query that sets neither its own lifetime nor a cache profile; `0` disables it. Dashboard cards use `Dashboard__CacheSeconds` instead". Add one intro sentence: "Not a secret; validated at startup." |
| `docs/constitution.md` | line 3, stack list | Replace "`Core.Storage`," with "`Core.Storage` (SDK-free: `IFileStorage`, local provider, media serving), `Core.Storage.S3` (S3/R2 provider, `AddCoreS3FileStorage`),". Replace "`Core.Cache` (`ICacheableQuery` caching behaviour)" with "`Core.Cache` (`ICacheableQuery` caching behaviour; lifetime from the query, a named cache profile, or the default)". |
| `docs/implementation-report.md` | line 224 | Change `Core.Storage/S3/S3FileStorage.cs` to `Core.Storage.S3/S3FileStorage.cs`. |
| `.claude/skills/dotnet-feature/SKILL.md` | line 19 | "`Core.Storage` (file storage `IFileStorage` with Local/S3 providers, …)" becomes "`Core.Storage` (file storage `IFileStorage` with the Local provider; the S3 provider lives in `Core.Storage.S3`, registered with `AddCoreS3FileStorage()` after `AddCoreFileStorage()`, so `Core.Storage` has no AWS SDK, …)". The rest of the sentence is unchanged. |
| `.claude/skills/dotnet-feature/SKILL.md` | line 22 | "Cache a query by implementing `ICacheableQuery`;" becomes "Cache a query by implementing `ICacheableQuery` (lifetime: its own `Ttl`, else a named `CacheProfile` the app maps in `CachingOptions.Profiles`, else `Caching:DefaultSeconds`);". |
| `.claude/skills/dotnet-feature/SKILL.md` | line 167 (layer diagram) | `Core.Storage` becomes `Core.Storage(.S3)`. |
| `docs/PRD.md`, `docs/backlog.json`, design docs | — | No change. There is no scope, UI or rule change. |

## Test plan
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | `OtpTests` | `Reissue_LastAllowedResend_BlocksFromThatResend` | `OtpBuilder().WithReissueCooldownSeconds(60).WithMaxReissueCount(1).WithReissueBlockCooldownInHours(24).IssuedAt(Now)`, then `Reissue("next-hash", 5, Now.AddHours(2))`. `NextAllowedReissueAt == Now.AddHours(26)`. The old code gives `Now + 60 s + 24 h`. |
| T2 | `OtpTests` | `Reissue_BlockedAfterLastAllowedResend_ThrowsCooldownUntilBlockEnds` | Same setup, resend at `Now.AddHours(2)`, then `Reissue` at `Now.AddHours(25)`. Throws `RateLimitExceededCoreException` with `ErrorCode == ErrorCodes.OTPReissueCooldown` and `Context["hours"] == 1`. |
| T3 | `OtpTests` | `Reissue_BelowMax_NextAllowedIsCooldownFromResend` | Cooldown 60, max 3, resend at `Now.AddMinutes(10)`. `NextAllowedReissueAt == Now.AddMinutes(10).AddSeconds(60)`. |
| T4 | `OtpRepositoryTests` | `FindByRecipientAsync_SavedOtp_ReturnsItWithWindowStart` (existing) | D4: only the `RequestIP` line is removed. |
| T5 | `AppDbContextTests` | existing no-pending-model-changes test | Unchanged. It must stay green after F1, which proves the snapshot and migration match the model. |
| T6 | `OtpTests` | existing `Verify_ExpiredAtGivenTime_ReturnsExpired`, `MarkUsed_ExpiredAtGivenTime_ThrowsExpired` | Unchanged. This is the D1 evidence. |
| T7 | `OtpTableSchemaTests` | `Migrate_FreshDatabase_OtpsHasNoRequestIpOrUserAgentColumn` | Fresh scope `AppDbContext`, `Database.SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'Otps'")`. The list contains `"PhoneNumber"`, which proves the query reads the right table. It contains neither `"RequestIP"` nor `"UserAgent"`. |
| T8 | `CoreS3FileStorageDependencyInjectionTests` | `AddCoreS3FileStorage_S3Provider_ResolvesS3FileStorage` | Moved from `CoreFileStorageDependencyInjectionTests` with the same settings. `IFileStorage` is `S3FileStorage`. |
| T9 | `CoreS3FileStorageDependencyInjectionTests` | `AddCoreS3FileStorage_LocalProvider_ResolvesLocalDiskFileStorage` | With S3 registered and `Provider = Local`, `IFileStorage` is `LocalDiskFileStorage`. |
| T10 | `CoreFileStorageDependencyInjectionTests` | `AddCoreFileStorage_S3ProviderWithoutS3Registration_ThrowsInvalidOperation` | The S3 settings with only `AddCoreFileStorage()`. Resolving `IFileStorage` throws `InvalidOperationException` with a message containing `"FileStorage:Provider 'S3'"`. |
| T11 | `DependencyInjectionTests` (Infrastructure) | `AddInfrastructure_S3Provider_ResolvesS3FileStorage` | `AddInfrastructure()` with the T8 S3 settings plus `FileStorage:LocalRootPath`/`PublicBaseUrl`. In a scope, `IFileStorage` is `S3FileStorage`. Proves that the app wires `AddCoreS3FileStorage`. |
| T12 | `PublicMediaFileProviderTests` | `GetFileInfo_UpperCasePrivateFolderWithExistingFile_ReturnsNotFound` | Create `Path.Combine(_root, "TEACHER-THREADS")` and write `upper.png` in it. `_files.GetFileInfo("/TEACHER-THREADS/upper.png").Exists` is `true` (the precondition), and `_provider.GetFileInfo("/TEACHER-THREADS/upper.png").Exists` is `false`. |
| T13 | `StorageProjectReferencesTests` | `CoreStorageProject_PackageReferences_ExcludeAwsSdk` | `core-libraries/Core.Storage/Core.Storage.csproj` has no `PackageReference` whose `Include` starts with `"AWSSDK"`. |
| T14 | `StorageProjectReferencesTests` | `ApplicationProject_TransitiveProjects_ExcludeAwsSdk` | `CollectProjects(Elmanhg.Application.csproj)` contains `Core.Storage.csproj` and does not contain `Core.Storage.S3.csproj`. No project in the set has an `AWSSDK*` `PackageReference`. |
| T15 | `CachingOptionsTests` | `ResolveTtl_QueryTtl_WinsOverProfileAndDefault` | Profile `"p"` = 30 s, default 60 s, query Ttl 5 s, profile `"p"`. Result is 5 s. |
| T16 | `CachingOptionsTests` | `ResolveTtl_ConfiguredProfile_UsesProfileTtl` | Profile `"p"` = 30 s, default 60 s. Result is 30 s. |
| T17 | `CachingOptionsTests` | `ResolveTtl_UnknownProfile_UsesDefaultTtl` | Profile `"other"`. Result is 60 s. |
| T18 | `CachingOptionsTests` | `ResolveTtl_NoProfileNoTtl_UsesDefaultTtl` | Profile `null`. Result is 60 s. |
| T18a | `CachingOptionsTests` | `ResolveTtl_ProfileZero_ReturnsZeroEvenWithPositiveDefault` | Profile `"p"` = 0, default 60 s. Result is `TimeSpan.Zero`. This proves a profile can switch caching off. |
| T19 | `DashboardCachingTests` | `AddApplication_DashboardCacheSeconds_SetsDashboardQueryTtl` (was `..._SetsCachingDefaultTtl`) | `Dashboard:CacheSeconds = 45`. `options.ResolveTtl(new GetFunnelMetricsQuery(null, null))` is 45 s. |
| T20 | `DashboardCachingTests` | `AddApplication_NoDashboardConfiguration_UsesSixtySecondDefault` (name kept) | No configuration. `ResolveTtl(GetFunnelMetricsQuery)` is 60 s. |
| T21 | `DashboardCachingTests` | `AddApplication_DashboardCacheSecondsZero_DisablesDashboardCaching` (was `..._DisablesCaching`) | `Dashboard:CacheSeconds = 0`. `ResolveTtl(GetFunnelMetricsQuery)` is `TimeSpan.Zero`, and `DefaultTtl` is still 60 s, which proves the two settings are independent. |
| T22 | `DashboardCachingTests` | `DashboardCardQuery_UsesDashboardCacheProfile` | `[Theory][MemberData(nameof(DashboardCardQueries))]`: `((ICacheableQuery)query).CacheProfile == DashboardCacheKey.Profile` for all 9 queries. |
| T23 | `DashboardCachingTests` | `AddApplication_CachingDefaultSeconds_SetsDefaultTtlOnly` | `Caching:DefaultSeconds = 15`. `DefaultTtl` is 15 s, and `ResolveTtl(GetFunnelMetricsQuery)` is still 60 s. |
| T24 | `DashboardCachingTests` | `AddApplication_NoCachingConfiguration_DefaultTtlIsSixtySeconds` | No configuration. `DefaultTtl` is 60 s. |
| T25 | `DashboardCachingTests` | `AddApplication_CachingDefaultSecondsOutOfRange_FailsValidation` | `Caching:DefaultSeconds = 3601`. Reading `IOptions<QueryCachingOptions>.Value` throws `OptionsValidationException`. |
| T26 | `PipelineCompositionTests` | `Resolve_CacheableQueryPipeline_IncludesCachingBehaviour` | `GetServices<IPipelineBehavior<GetFunnelMetricsQuery, FunnelMetricsResult>>()` contains items assignable to `CachingBehaviour<GetFunnelMetricsQuery, FunnelMetricsResult>`. |
| T27 | `ResendRetryTests` | `InvitationSender_ServerError_RetriesWithTheSameIdempotencyKey` | Provider from `BuildProvider(nameof(ResendInvitationEmailSender))`. Resolve `ResendInvitationEmailSender` in a scope and call `SendAsync(new InvitationEmail("teacher@elmanhg.test", "Mona", UserRole.Teacher), ct)`. It returns `false`. `_handler.CallCount > 1`, and `_handler.RequestHeaders.Select(x => x["Idempotency-Key"]).Distinct()` is a single non-blank value. |
| T28 | `ResendRetryTests` | `ReminderEmailChannel_ServerError_RetriesWithTheSameIdempotencyKey` | Provider from `BuildProvider(nameof(ResendEmailMessageChannel))`. Resolve `ResendEmailMessageChannel` and call `SendAsync(new TeacherThreadReminderMessage(...), ct)`, using the same values as `ResendEmailMessageChannelTests.Reminder`. It returns `false`. Same retry and key assertions as T27. |
| T29 | `OtpDeliveryResilienceTests` | `HttpSms_ServerError_MakesExactlyOneAttempt` (existing) | D13: the client name argument becomes `nameof(HttpSmsOtpChannel)`. It still asserts `CallCount == 1`, which now proves that the `HttpSmsOtpChannel` named client carries the no-retry pipeline. |
| T30 | `CoreOtpDeliveryRegistrationTests` | existing `AddCoreOtpDelivery_EnabledProviders_ResolveTransportTypedClients` | Unchanged. It must stay green, because `HttpSmsClient` is still resolvable. |
| T31 | all existing | — | `dotnet build api/` with no new warnings in `Elmanhg.*`, and `dotnet test api/` green. `S3FileStorageTests`, `PublicMediaPipelineTests` and `CachingBehaviourTests` are unchanged and pass. |

## Definition of done
- [ ] `Otp` has no `RequestIP`/`UserAgent`. `CoreDbContext.cs` is unchanged. One migration `DropOtpRequestIpAndUserAgent` has exactly two `DropColumn`s up and two nullable `text` `AddColumn`s down. The snapshot is regenerated, and T5 and T7 pass.
- [ ] `Reissue` sets the block from `now` (D2). T1–T3 pass.
- [ ] `02-implementation.md` records the clock check (D1) with its evidence.
- [ ] `Core.Storage.csproj` has no `AWSSDK.S3`. `Core.Storage.S3` exists, is in `Elmanhg.slnx` under `/core-libraries/`, and holds `S3FileStorage` (moved with `git mv`) and `AddCoreS3FileStorage`. Infrastructure references it and calls it. Config keys are unchanged. T8–T11 and T13–T14 pass.
- [ ] T12 proves the case-insensitive private-folder check. Existing `PublicMediaFileProviderTests` members are unchanged.
- [ ] `ICacheableQuery.CacheProfile`, `CachingOptions.Profiles` and `CachingOptions.ResolveTtl` exist. `CachingBehaviour` uses `ResolveTtl`. Core has no Elmanhg names.
- [ ] `Caching:DefaultSeconds` (`QueryCachingOptions`, 0–3600, default 60) drives `DefaultTtl`. Dashboard cards resolve their TTL from `Dashboard:CacheSeconds` through the `dashboard` profile. The 60 s default and "0 off" hold (T19–T25). `appsettings.example.json` has `"Caching"`.
- [ ] T26 proves that `CachingBehaviour<,>` is in the app's MediatR pipeline.
- [ ] The SMS named client is `HttpSmsOtpChannel`, and T29 and T30 pass.
- [ ] T27 and T28 prove the retries on the invitation and reminder Resend clients.
- [ ] No file from the lane of issue 329 (listed in Scope/Out) is modified.
- [ ] No new NuGet package. `Directory.Packages.props` is unchanged.
- [ ] No `#<number>` and no new explanatory comments in code.
- [ ] Every edit to an existing test is limited to D4, D7, D11 and D13, and none weakens an assertion.
- [ ] The F12 paragraph is in `.claude/commands/feature.md` Stage 4.
- [ ] Every row of "Docs to update" is applied. `docs/` and `.claude/skills/dotnet-feature/SKILL.md` do not contradict the code.
- [ ] The reviewer's `03-merged-pr-review.md` exists, with the summary first line and every finding routed as described in "Review step".
