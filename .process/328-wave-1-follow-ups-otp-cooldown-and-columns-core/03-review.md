VERDICT: APPROVED

# Review: [E21.S11] Wave-1 follow-ups: OTP cooldown and columns, Core.Storage.S3, cache TTL, messaging tests

## Blocking
None.

## Non-blocking
- api/Elmanhg.Tests/Application/Features/Dashboard/Shared/DashboardCachingTests.cs:1-133: the file is 133 lines, above the 80 to 100 line guidance of the skill (SKILL.md "No Long Files"). The plan placed T19 to T25 in this class. T23 to T25 (Caching:DefaultSeconds) could move to their own QueryCachingOptions test class later.
- deploy/api.env.example:155-161: the commented Dashboard__* block has no Caching__DefaultSeconds line beside it. The rule at docs/deployment.md:82 does not require one, because the setting is neither a secret nor per-host, so this is optional.
- api/core-libraries/Core.Storage/DependencyInjection.cs:47: when FileStorage:Provider is null, GetKeyedService(null) would resolve the non-keyed IFileStorage factory again and recurse. That cannot happen today: the [Required] attribute plus ValidateOnStart refuse a null provider, and Options(...).Value throws before Resolve runs. If the [Required] attribute is ever removed, it becomes a stack overflow.

## Verified
- **Story intent:** every sub-task is covered (OTP clock check, cooldown, columns; S3 split and private-folder test; cache default TTL and pipeline test; SMS client name and Resend retry tests; pipeline note; docs). The merged-PR review is in 03-merged-pr-review.md.
- **D1 clock:** Otp.Verify and MarkUsed take "now". Core.OTP reads the clock only through TimeProvider (GenerateOTPHandler.cs:17, VerifyOTPHandler.cs:21).
- **D2 cooldown:** OTP.cs:92 now times the block from "now" when ReissueCount == MaxReissueCount. T1 and T2 fail on the old code: the old code gives Now+24h+60s, so the resend at Now+25h would succeed instead of throwing with hours=1. T3 pins the below-max branch.
- **D3 columns:** RequestIP and UserAgent are removed from Otp. CoreDbContext.cs is untouched. The migration 20261007043926_DropOtpRequestIpAndUserAgent.cs contains exactly two DropColumn calls in Up and two nullable text AddColumn calls in Down, and nothing else. BuildTargetModel in the Designer is byte-identical to BuildModel in the snapshot.
- **Snapshot drift:** the other changes are IssuedRefreshToken moving from the Elmanhg.Domain.Identity name to Core.Identity.Tokens.RefreshToken, and RetrySchedule becoming an owned "Retry" on EssayGrades, MathStepGrades, TeacherVoiceDrafts and TrainingExports, with the same column names and explicit index names (IX_[Table]_NextAttemptAt). These are model-only changes with no operations. The migration has none, and AppDbContextTests.Model_Current_MatchesLatestMigrationSnapshot plus OtpTableSchemaTests pass against a real Postgres.
- **AppDbContextTests deviation:** one element is appended to the pinned migration list. Nothing is weakened. The test is required to stay green, and the change is declared in 02-implementation.md.
- **Core.Storage split:** Core.Storage.csproj has no AWSSDK.S3. Core.Storage.S3 exists (csproj, S3FileStorage.cs moved with git mv at 100% similarity, and AddCoreS3FileStorage with CreateS3Client copied verbatim). It is in Elmanhg.slnx under /core-libraries/ and referenced and called by Infrastructure. Directory.Packages.props is unchanged.
- **Provider selection parity:** for Local and S3, IFileStorage resolves the same concrete types (T8, T9, T11, and the existing Local test). Startup validation is unchanged: FileStorageOptionsValidator and the DataAnnotations [Required] rule are still registered in AddCoreFileStorage, and IAmazonS3 is still created lazily. No consumer resolved LocalDiskFileStorage or S3FileStorage directly (grep). An unknown provider still throws InvalidOperationException, now with a message that names the provider (T10).
- **T12:** the test asserts the precondition (the inner PhysicalFileProvider sees /TEACHER-THREADS/upper.png) and then that the public provider returns not-found. With an Ordinal compare in IsPrivate, it would fail on both case-sensitive and case-insensitive file systems.
- **Cache profile:** ResolveTtl gives precedence to query.Ttl, then the profile, then DefaultTtl. All 9 dashboard queries return the "dashboard" profile (T22). Dashboard:CacheSeconds still drives the dashboard TTL: 45/60/0 (T19 to T21). DefaultTtl is independent (T21, T23, T24). Out-of-range values fail validation (T25). The ApiFactory still sets Dashboard:CacheSeconds=0, and that value still switches the dashboard cache off. No other ICacheableQuery exists in the app (grep), so the TTL of no other query changed. Core has no Elmanhg names: "dashboard" lives only in Application.
- **CachingBehaviour registration:** T26 resolves the pipeline of the real host, and it would fail if the behaviour were not registered.
- **SMS client name:** AddHttpClient[HttpSmsClient](nameof(HttpSmsOtpChannel)) is the only HttpSmsClient registration. T29 now attaches its stub to that name and still asserts exactly 1 attempt. CoreOtpDeliveryRegistrationTests is green.
- **Resend retry tests:** T27 and T28 go through the real AddInvitationEmail and AddMessaging registrations. They fail if retries are off (CallCount would be 1), if the client name is wrong (the stub would not be attached, so CallCount would be 0), or if the idempotency key changes between attempts.
- **Pipeline note:** .claude/commands/feature.md Stage 4 step 2 matches paragraph F12 of the plan word for word. The later steps are renumbered 3 to 6.
- **Docs-sync:** otp-delivery.md (resend limits, named clients), dashboard.md (Caching), deployment.md (new Query cache table), constitution.md line 3, implementation-report.md:224 and SKILL.md (lines 19, 22, 167) all agree with the code. docs/security.md, rich-text.md, ask-teacher.md and training-data.md still attribute the media serving and local provider to Core.Storage, which is still true. No divergence.
- **Postman:** there is no endpoint or contract change (no controller in the diff, and the OpenAPI output is unchanged), so no Postman change is needed.
- **No comments or issue numbers in code:** a grep of the code diff and the new files found none.
- **Build and tests (run by me in the worktree):** "dotnet build api/Elmanhg.slnx -c Release" succeeded with 0 warnings and 0 errors (incremental). "dotnet test api/Elmanhg.slnx -c Release --no-build" passed 5574 of 5574, with 0 failed and 0 skipped. This matches the claim in 02-implementation.md.
- **Lane 329 files:** none touched (Repository.cs, SweepWorker.cs, CachingBehaviourTests, ConflictMapTests, RefreshTokenRotatorTests, RuntimeSettingRegistryTests, RefreshAccessTokenHandlerTests).

## Test quality
- **OtpTests (T1 to T3):** they constrain the change. T1 and T2 fail on the old formula.
- **OtpTableSchemaTests (T7):** it constrains the change. The positive PhoneNumber check proves the query reads the right table.
- **CoreS3FileStorageDependencyInjectionTests, CoreFileStorageDependencyInjectionTests T10, DependencyInjectionTests T11:** they constrain the change. They check concrete types and the message of the exception.
- **StorageProjectReferencesTests:** it constrains the change through the XML project graph. It would catch AWSSDK being added back to Core.Storage or any project Application reaches.
- **CachingOptionsTests:** these are pure unit tests, and each precedence branch has its own failing case.
- **DashboardCachingTests:** the rewritten tests keep the same strength (45/60/0), and the new tests check that the two settings are independent.
- **PipelineCompositionTests T26, ResendRetryTests, OtpDeliveryResilienceTests T29:** they constrain real registrations. None asserts a value that a substitute was configured to return.
- **Vacuous tests:** none found.

## Part B: merged PRs 315 to 318
See 03-merged-pr-review.md. Totals: 0 critical, 1 major, 6 minor. None is small enough to fit this story and still worth blocking it. M1 (the OTP lost-update race) needs a concurrency token on the Otp mapping in CoreDbContext, which the dev decision keeps unchanged, so it is a follow-up. m1 to m6 are minor and go to one review-debt issue.
