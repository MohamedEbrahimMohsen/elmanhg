VERDICT: APPROVED

# Review — Core.Identity: refresh-token rotation, reuse detection and security-stamp claim (E21.S8, story 311)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Application/Features/Auth/RefreshAccessToken/RefreshAccessTokenHandlerTests.cs:127` — `Handle_SuspendedUser_ThrowsForbidden` pins "no save, no new token" but not "no token-store read". The code order is correct (`RefreshAccessTokenHandler.cs` throws 403 before `RotateAsync`), but adding `await _issuedRefreshTokenRepository.DidNotReceiveWithAnyArgs().FindAsync(default!, default)` would pin it. The test body is meant to stay unchanged (Decision 15), so this belongs in a later story.
- `api/Elmanhg.Tests/Core/Identity/RefreshTokenRotationRegistrationTests.cs:24` — `..._NoConfiguration_DefaultsGraceToTenSeconds` only re-reads the property initializer in `RefreshTokenRotationOptions.cs:5`. It is cheap, but it constrains little.
- `api/Elmanhg.Application/Auth/RefreshAccessToken/RefreshAccessTokenHandler.cs:21-22` — the access token is now generated after `SaveChangesAsync`, not before. The plan accepted this (Decision 13). `ITokenService.GenerateTokenAsync` is pure, so nothing observable changes.
- `api/Elmanhg.Tests/Core/Identity/RefreshTokenRotatorTests.cs` is 192 lines. The report says 176. It is over the ~100-line guide, and the implementer disclosed that.

## Verified
- **Rotator parity.** I compared `RefreshTokenRotator.cs:11-49` line by line with `git show HEAD:…/RefreshAccessTokenHandler.cs:24-55`. These are the same:
  - `now` is read once from `TimeProvider`.
  - The presented token is hashed, then looked up by hash with tracking.
  - The revoked check runs before the replay check, and the revoked path does not save.
  - Replay uses `IsReplayed(now, grace)`, which is strict `>`. That keeps the exact-grace edge inside the window, as before.
  - Family revocation runs over the distinct family ids with `RevokedAt == null`, so siblings revoked earlier keep their first time (`??=`). It saves once, then throws.
  - `expiresAt` is `now + RefreshTokenExpirationDays`.
  - Unknown hash: `AddIfAbsentAsync` with a new family, then a re-read by hash, so a concurrent first refresh joins the winning family.
  - `Rotate(now)` uses first-write-wins.
  - The new token's record goes into `presented[0].FamilyId`, with exactly one `SaveChangesAsync`.
  - Only hashes are stored. There is no logging anywhere.
  - Every await has `ConfigureAwait(false)`.
  - The only change is that the grace now comes from `RefreshTokenRotationOptions.ReuseGrace` instead of `TimeSpan.FromSeconds(AuthOptions…)`.
- **Order in the thin handler.** It validates, then returns 403 `USER_SUSPENDED` for a suspended user before any store access, then calls `RotateAsync`. It has 3 dependencies, as in A1.
- **Error codes.** `Core.Identity.Exceptions.ErrorCodes.RefreshTokenRevoked = "REFRESH_TOKEN_REVOKED"` (`ErrorCodes.cs:9`) is thrown as `UnauthorizedCoreException` (401). The app constant is removed, with no dangling references. The ar/en resx keys `REFRESH_TOKEN_REVOKED` are still present and unchanged (`Messages.*.resx:92`). The integration `RefreshTokenReuseTests` still asserts the literal wire value.
- **Moved files.** `IssuedRefreshToken`, `IIssuedRefreshTokenRepository`, `RefreshTokenHash` and `SecurityStampClaim` are `git mv` renames with only the namespace changed. The diff shows only that hunk. The `FixedTimeEquals` constant-time compare, the hash format and `ClaimType = "security_stamp_hash"` are all unchanged.
- **Where the stamp is checked.** This is unchanged. The refresh-token stamp is checked in core `RefreshTokenService.ValidateTokenAsync`. The access-token stamp is checked in `ActiveUserTokenValidation` → `CheckUserActiveHandler`, where only the `using` changed. Minting is in `UserClaimsExtensions`, where only a `using` was added.
- **Grace config.** `Auth:RefreshTokenReuseGraceSeconds` is mapped in `Elmanhg.Application/DependencyInjection.cs:215` through `Configure<IOptions<AuthOptions>>`. `AuthOptions` and its `[Range]` validation are unchanged. Core adds a `ReuseGrace >= 0` check with `ValidateOnStart` (`DependencyInjection.RefreshTokenRotation.cs:30-32`).
- **Things that stayed the same:**
  - No migration was added. The snapshot is untouched, and `AppDbContextTests` (including `HasPendingModelChanges`) passes.
  - `api/openapi` has no changes.
  - `CoreDbContext`, `Core.Notifications`, `Core.Identity.csproj` and `Elmanhg.Domain.csproj` are untouched, and `DomainAssemblyReferencesTests` passes.
  - `Core.Identity` contains no `Elmanhg` name, no `Auth:` key and no `#<number>`.
  - No new comments were added.
- **The 7 existing handler tests.** Names and bodies are unchanged, except the `IdentityErrorCodes` alias in 2 asserts and the constructor wiring that Decision 15 allows.
- **Test plan.** All 40 test rows exist under their exact names.
- **Build and tests.**
  - `dotnet build api/Elmanhg.slnx -c Release` gives 0 warnings and succeeds.
  - The auth, identity and persistence test classes (rotator, registration, both SecurityStampClaim classes, RefreshTokenHash, IssuedRefreshToken, handler, RefreshTokenService, SessionEndpoint, RefreshTokenReuse, IssuedRefreshTokenRepository, ActiveUserTokenValidation, CheckUserActive, LogoutHandler, DomainAssemblyReferences, AppDbContext, UsersEndpoint, PhoneAuthEndpoint, AuthOptions, the validator and SoftDeleteQueryFilterModel) pass, 129/129, with Docker running.
  - The tests that build `AddApplication()` containers (Progress, Dashboard caching, Exams, Subscriptions, Mastery and Sessions options) pass, 40/40.
- **Postman.** No endpoint or contract changed, so no sync is needed.
- **Docs sync.**
  - `docs/security.md` §8 (Access token and Reuse detection) and the `docs/constitution.md` stack line now describe the core ownership.
  - `.claude/skills/dotnet-feature/SKILL.md` deltas §5 and the §10 row were added.
  - No stale doc references the old app paths. There is no divergence.
- **Deviation report.** The only deviation (the SKILL.md bullet wording) matches the diff. "Snapshot fallback not needed" is also confirmed.

## Test quality
- `RefreshTokenRotatorTests`: these constrain the code. Each branch is driven through a real list-backed store:
  - exact-grace boundary vs grace+1s;
  - a longer configured grace;
  - revoked before replayed;
  - a sibling revoked earlier keeping its time;
  - another family left untouched;
  - the winning family on a concurrent insert;
  - no reinsert for a recorded token;
  - hash-only storage.

  Each would fail if a comparison direction, the check order or `??=` were wrong.
- `RefreshAccessTokenHandlerTests`: these constrain the code, because they run over a real `RefreshTokenRotator<User>`, so they are end-to-end parity tests at the app level. The new claim test pins the fingerprint claim.
- `SecurityStampClaimTests` (core): these constrain the code. The known-answer digests pin the hash, and the truncated and upper-case cases pin exact matching.
- `RefreshTokenHashTests`: these use known-answer SHA-256 vectors, so they constrain the hash.
- `RefreshTokenRotationRegistrationTests`: the descriptor test and the negative-grace test constrain the code. The default test is weak (see Non-blocking).
- `RefreshTokenServiceTests.ValidateTokenAsync_StaleSecurityStamp_*`: constrains the code (a null stamp result must give `REFRESH_TOKEN_USER_NOT_FOUND`).
- `SessionEndpointTests.Logout_ThenOldAccessToken_Returns401`: constrains the code end to end. The second logout with the same bearer token must fail on the stamp-fingerprint mismatch.
