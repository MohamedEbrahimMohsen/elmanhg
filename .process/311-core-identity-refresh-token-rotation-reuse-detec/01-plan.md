# Plan — Core.Identity: refresh-token rotation, reuse detection and security-stamp claim (E21.S8, story 311)

## Goal
Today the refresh-token security lives in the app: single-use rotation, reuse detection with a grace window, family revocation, hashed token storage (`RefreshTokenHash`), the `IssuedRefreshToken` entity and the hashed security-stamp claim (`SecurityStampClaim`). After this story all of it is in `api/core-libraries/Core.Identity`, behind `IRefreshTokenRotator<TUser>` and `IIssuedRefreshTokenRepository`. Any app on core gets the same protection by mapping one table and implementing one insert-if-absent. `RefreshAccessTokenHandler` becomes a thin call into core. The app keeps the EF mapping, the table, the raw-SQL insert-if-absent, the `Auth:RefreshTokenReuseGraceSeconds` key and the user-active rule. Tokens, cookies, error codes, the grace window, the database schema and every auth test stay as they are.

## Base
Plan against **main after PR 324** (E21.S2 core hygiene). The implementer runs `git fetch origin && git merge origin/main` first. If `origin/main` does not yet contain PR 324, stop and report `BLOCKED: PR 324 not merged`. After 324 these things are already true, so do not redo them:
- `RefreshTokenService` and `JwtTokenService` take a `TimeProvider`.
- `AddCoreIdentity` calls `TryAddSingleton(TimeProvider.System)`.
- `Repository<T>(context, ICurrentUser, TimeProvider)`.
- Core uses central package management, with no `Version` on any `PackageReference`.
- `Core.Identity.csproj` has `FrameworkReference Microsoft.AspNetCore.App` and references only `Core.DDD` and `Core.Errors`.
- `Elmanhg.Domain` references only `Core.DDD` and `Core.Settings`. `DomainAssemblyReferencesTests` enforces this.

## Scope
**In:**
- **Core (`Core.Identity`):** `IssuedRefreshToken` entity, `IIssuedRefreshTokenRepository`, `RefreshTokenHash`, `SecurityStampClaim`, `IRefreshTokenRotator<TUser>` + `RefreshTokenRotator<TUser>`, `RefreshTokenRotationOptions`, the opt-in `AddCoreRefreshTokenRotation<TUser>()`, and the new error code `RefreshTokenRevoked`.
- **App switch-over:**
  - delete the four moved app files;
  - the handler becomes thin;
  - consumers switch namespaces;
  - DI goes through core, and the app maps `Auth:RefreshTokenReuseGraceSeconds` into the core options.
- **Tests:** moved tests go to `Elmanhg.Tests/Core/Identity/`, with new core tests for every rotation branch. App tests get only the edits the new signatures and namespaces need, plus two new tests.
- **Docs:** `docs/security.md` §8, `docs/constitution.md` (stack line), `.claude/skills/dotnet-feature/SKILL.md` (deltas §5 and §10).

**Out:**
- `CoreDbContext` (unchanged).
- `Core.Notifications` (kept).
- Migrations: none. `has-pending-model-changes` and `AppDbContextTests` must stay clean.
- No new NuGet package and no new core project. `Elmanhg.slnx` is unchanged, because `Core.Identity` is already listed.
- Web: no change.
- The cookie code (`RefreshTokenCookieExtensions`), `AuthController`, `LogoutHandler`, `SuspendUserHandler`, `AuthOptions` and `JwtOptions` are unchanged.
- Purging expired rows: still security.md D10, not in this story.

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Morabh reuse | Grepped `D:\Personal\Projects\Projects\Morabh\repos\apis` for `RefreshToken`, `SecurityStamp`, `FamilyId`, `TokenHash`, `FixedTimeEquals` and `IsReplayed`. Morabh has only the plain `Core/Core.Identity/Tokens/RefreshToken/RefreshTokenService.cs`, which is already vendored and stays as is, and `Morabh.Application/Tokens/UserRefreshAccessToken/UserRefreshAccessTokenHandler.cs`, which has no rotation. Every new core file is moved Elmanhg code: **new, no Morabh equivalent**. | Reuse-first rule. |
| 2 | Where the "issued-token contract" lives, given `Elmanhg.Domain` must not reference `Core.Identity` (it has an ASP.NET framework reference) | The entity class `IssuedRefreshToken` and `IIssuedRefreshTokenRepository` move into `Core.Identity` (namespace `Core.Identity.Tokens.RefreshToken`). They are deleted from `Elmanhg.Domain/Identity`. `AppDbContext.IssuedRefreshTokens.cs` maps the core type, as it mapped the app type. `Elmanhg.Infrastructure.Identity.IssuedRefreshTokenRepository` implements the core interface. | Unlike Core.Settings (where the entity stayed in Domain behind an interface), the rotation logic needs to create, rotate and revoke records. A core entity avoids a factory abstraction. Domain never used the type (no navigation from `User`), so its references are unchanged. |
| 3 | Does moving the CLR type change the EF model or need a migration? | No migration. Table, columns, index names, FK (`HasOne<User>()`), max length and soft-delete filter are unchanged, and the differ compares the relational model, so `HasPendingModelChanges()` stays false. Do **not** edit the snapshot. Fallback: if `AppDbContextTests` or `dotnet ef migrations has-pending-model-changes` reports changes, replace only the two `"Elmanhg.Domain.Identity.IssuedRefreshToken"` strings in `AppDbContextModelSnapshot.cs` with `"Core.Identity.Tokens.RefreshToken.IssuedRefreshToken"`, then rerun. Never add a migration. | The story says no data migration and `CoreDbContext` unchanged. |
| 4 | "Behind interfaces": which pieces get one | `IRefreshTokenRotator<TUser>` (the rotation use case) and `IIssuedRefreshTokenRepository` (the store port). `RefreshTokenHash` and `SecurityStampClaim` stay **static** pure functions, moved as they are. | No new abstraction unless needed. SHA-256 and `FixedTimeEquals` have no seam worth substituting, and they are public, so any app can call them. |
| 5 | Rotation API shape | `Task<string> RotateAsync(TUser user, string presentedRefreshToken, CancellationToken cancellationToken)` returns the new refresh token. Validating the presented token stays in `IRefreshTokenService.ValidateTokenAsync`, called by the app before the rotator. | The app's suspended-user check (403 `USER_SUSPENDED`) runs **between** validation and the token-store read. Keeping validation outside the rotator keeps that order exactly. |
| 6 | Rotator generic constraint | `where TUser : IdentityUser<Guid>, new()`. It depends on `IRefreshTokenService<TUser, Guid>`. | `IssuedRefreshToken.UserId` is `Guid`, as every Core.DDD id is. |
| 7 | Grace-window configuration | Core `RefreshTokenRotationOptions { TimeSpan ReuseGrace = 10 s }` with no section of its own. The app keeps `AuthOptions.RefreshTokenReuseGraceSeconds` (`[Range(0, int.MaxValue)]`, default 10, key `Auth:RefreshTokenReuseGraceSeconds`) and maps it the way `CachingOptions` is mapped today: `services.AddOptions<RefreshTokenRotationOptions>().Configure<IOptions<AuthOptions>>((rotation, auth) => rotation.ReuseGrace = TimeSpan.FromSeconds(auth.Value.RefreshTokenReuseGraceSeconds))`. Core validates `ReuseGrace >= 0` on start. | Core stays app-agnostic. The config key, its default, its validation and `docs/deployment.md` stay the same. The integration tests' `Auth:RefreshTokenReuseGraceSeconds = "0"` override still drives the core window. |
| 8 | Registration | New opt-in `AddCoreRefreshTokenRotation<TUser>()` in a second file of a now-`partial` `Core.Identity.DependencyInjection`. It is not folded into `AddCoreIdentity`. The app calls it from `Elmanhg.Application.DependencyInjection.AddApplication`. | `AddCoreIdentity` must not need a repository the app may not have. A separate file keeps `DependencyInjection.cs` under ~100 lines. |
| 9 | `REFRESH_TOKEN_REVOKED` ownership | Add `Core.Identity.Exceptions.ErrorCodes.RefreshTokenRevoked = "REFRESH_TOKEN_REVOKED"`, thrown by core. **Remove** `Elmanhg.Application.Exceptions.ErrorCodes.RefreshTokenRevoked`, which becomes dead. The `resx` keys (`REFRESH_TOKEN_REVOKED`, ar and en) are keyed by value and stay unchanged. | One owner per code. The wire value is the same. Integration tests assert the literal string. |
| 10 | `SecurityStampClaim` namespace | `Core.Identity.Tokens.AccessToken` (file `Tokens/AccessToken/SecurityStampClaim.cs`). `ClaimType` keeps the value `"security_stamp_hash"`. | The claim rides on the access token. Keeping the value and the hash format means access tokens issued before the deploy stay valid. |
| 11 | `RefreshTokenHash` format | Unchanged: `Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)))`. | Existing `IssuedRefreshTokens.TokenHash` rows must still match after the deploy, with no data migration. |
| 12 | Where the security stamp is checked (unchanged; only namespaces move) | (a) **Refresh token:** core `RefreshTokenService.ValidateTokenAsync` → `SignInManager.ValidateSecurityStampAsync(ticket.Principal)` compares the raw stamp inside the Data-Protection ticket → null → 401 `REFRESH_TOKEN_USER_NOT_FOUND`. (b) **Access token:** app `ActiveUserTokenValidation.OnTokenValidated` → `CheckUserActiveQuery` → `CheckUserActiveHandler` → `state.IsActive && SecurityStampClaim.Matches(fingerprint, stamp)`, which is now core's constant-time `Matches`. (c) **Minting:** app `UserClaimsExtensions.GetUserClaims` adds `new Claim(SecurityStampClaim.ClaimType, SecurityStampClaim.Fingerprint(user.SecurityStamp))`. | (b) and (c) carry app rules (user active, the cache, app claims), so they stay in the app and only switch `using`. |
| 13 | Order of side effects in the thin handler | The access token is generated **after** `RotateAsync` returns. Before, it was generated just before the new refresh token. | `ITokenService.GenerateTokenAsync(claims)` is pure. Every throwing branch was already before it, so the observable result, persisted rows and error codes are identical. |
| 14 | Test time source | `Substitute.For<TimeProvider>()` with `GetUtcNow().Returns(Now)`, as the existing `RefreshTokenServiceTests` and `RefreshAccessTokenHandlerTests` do. | Mirror the neighbouring tests. `FakeTimeProvider` is not referenced by the test project, and the story adds no new package. |
| 15 | App handler tests | Keep all 7 existing `RefreshAccessTokenHandlerTests` methods with their names and bodies. Build the handler with a **real** `RefreshTokenRotator<User>` over the same substituted repository, so the test proves app-level behaviour is unchanged. Allowed edits: constructor wiring, `using`s, and `ErrorCodes.RefreshTokenRevoked` → `IdentityErrorCodes.RefreshTokenRevoked`. | Existing tests keep passing. Core coverage is duplicated in `RefreshTokenRotatorTests`. |
| 16 | Code comments | Moved code keeps its one WHY comment (on `SecurityStampClaim.Fingerprint`). No new comments, and never a `#<number>` in code. | House style and caller instruction. |

## Current behaviour → core equivalent (security parity map)
Every row must still hold after the change. "Test" names the test(s) that pin it after the change. **bold** = new test.

| # | Current behaviour (file today) | After | Test |
|---|---|---|---|
| B1 | Empty or missing cookie → validator 422 `REFRESH_TOKEN_IS_REQUIRED` (`RefreshAccessTokenValidator`) | unchanged (app) | `RefreshAccessTokenValidatorTests.*`, `SessionEndpointTests.Refresh_NoCookie_Returns422` |
| B2 | Ticket unprotect fails or `ExpiresUtc < now` → 401 `REFRESH_TOKEN_IS_EXPIRED` (core `RefreshTokenService`, `TimeProvider`) | unchanged (core, post-324) | `RefreshTokenServiceTests.ValidateTokenAsync_TicketExpiredAtProviderTime_ThrowsRefreshTokenIsExpired`, `SessionEndpointTests.Refresh_TamperedCookie_Returns401` |
| B3 | Ticket stamp stale (sign-out or suspend rotated it) → `ValidateSecurityStampAsync` null → 401 `REFRESH_TOKEN_USER_NOT_FOUND` | unchanged (core) | **`RefreshTokenServiceTests.ValidateTokenAsync_StaleSecurityStamp_ThrowsRefreshTokenUserNotFound`**, `SessionEndpointTests.Logout_Authenticated_Returns200AndRevokesRefreshToken`, `UsersEndpointTests.Suspend_SignedInStudent_RevokesAccessAndRefreshTokens` |
| B4 | `!user.IsActive` → 403 `USER_SUSPENDED`, before any token-store read or write | app handler, before `RotateAsync` | `RefreshAccessTokenHandlerTests.Handle_SuspendedUser_ThrowsForbidden` |
| B5 | Presented token is hashed. Any record with that hash revoked → 401 `REFRESH_TOKEN_REVOKED`, no save, no new token | core rotator step 3 | `RefreshTokenRotatorTests.RotateAsync_RevokedToken_ThrowsRefreshTokenRevokedWithoutSaving`, `RefreshAccessTokenHandlerTests.Handle_RevokedFamilyToken_ThrowsRefreshTokenRevoked` |
| B6 | The revoked check runs before the replay check (a revoked and replayed token does not revoke again or save) | core rotator steps 3→4 | **`RefreshTokenRotatorTests.RotateAsync_RevokedTokenAlsoReplayed_ThrowsWithoutRevokingSiblingsOrSaving`** |
| B7 | Replay = `RotatedAt` set and `now − RotatedAt > grace` (strict). It revokes every **unrevoked** token of the presented token's family or families, saves once, throws 401 `REFRESH_TOKEN_REVOKED` and issues no token. Other families are untouched | core rotator step 4 | `RefreshTokenRotatorTests.RotateAsync_RotatedTokenReplayedAfterGrace_RevokesFamilyAndThrowsRefreshTokenRevoked`, handler test of the same name, `RefreshTokenReuseTests.Refresh_RotatedCookieReplayed_Returns401AndRevokesTheWholeFamily`, `RefreshTokenReuseTests.Refresh_ReplayInOneSignIn_LeavesOtherSignInValid` |
| B8 | `Revoke` keeps the first `RevokedAt` (`??=`). The family query skips rows already revoked | core entity + rotator | `IssuedRefreshTokenTests.Revoke_AlreadyRevoked_KeepsFirstRevocationTime`, **`RefreshTokenRotatorTests.RotateAsync_ReplayWithEarlierRevokedSibling_KeepsItsFirstRevocationTime`** |
| B9 | Replay at or under the grace → normal rotation in the same family, nothing revoked | core rotator | `RefreshTokenRotatorTests.RotateAsync_RotatedTokenReplayedAtGraceBoundary_IssuesTokenInSameFamily`, handler `Handle_RotatedTokenReplayedWithinGrace_IssuesTokenInSameFamily`, `RefreshTokenReuseTests.Refresh_RotatedCookieReplayedWithinGrace_Returns200` |
| B10 | Grace comes from `Auth:RefreshTokenReuseGraceSeconds` (default 10, ≥ 0) | app `AuthOptions` → core `RefreshTokenRotationOptions.ReuseGrace` | **`RefreshTokenRotatorTests.RotateAsync_ReplayWithinLongerConfiguredGrace_IssuesToken`**, **`RefreshTokenRotationRegistrationTests.*`**, `RefreshTokenReuseTests` strict host (`"0"`) |
| B11 | Unknown hash (a sign-in token on its first refresh) → `AddIfAbsentAsync(Issue(user.Id, new family, hash, now, now+days))`, then re-read by hash | core rotator step 6 | `RefreshTokenRotatorTests.RotateAsync_SignInToken_RecordsItRotatedAndTheNewTokenInOneFamily`, handler test of the same name |
| B12 | A concurrent first refresh loses the race → `ON CONFLICT ("TokenHash") DO NOTHING` → the re-read returns the winner's family, and the new token joins it | core rotator + app repo SQL | `RefreshTokenRotatorTests.RotateAsync_SignInTokenRecordedByConcurrentRefresh_IssuesTokenInTheWinningFamily`, `IssuedRefreshTokenRepositoryTests.AddIfAbsentAsync_SameTokenFromTwoConcurrentRequests_StoresOneFamily`, `RefreshTokenReuseTests.Refresh_SignInCookieRefreshedConcurrently_Returns200AndKeepsOneFamily` |
| B13 | A known, unrevoked, unrotated hash → no insert-if-absent | core rotator step 6 | **`RefreshTokenRotatorTests.RotateAsync_RecordedToken_DoesNotInsertItAgain`** |
| B14 | Every presented record gets `RotatedAt = now` the first time only (`??=`) | core entity + rotator step 7 | `IssuedRefreshTokenTests.Rotate_AlreadyRotated_KeepsFirstRotationTime`, rotator SignIn test |
| B15 | New refresh token = `RefreshTokenService.GenerateTokenAsync(user)` (Data-Protection ticket, `ExpiresUtc = now + RefreshTokenExpirationDays`) | core rotator step 8 | `RefreshTokenServiceTests.GenerateTokenAsync_User_ExpiresConfiguredDaysAfterProviderTime`, `SessionEndpointTests.Refresh_ValidCookie_Returns200AndRotatesCookie` |
| B16 | New record: `presented[0].FamilyId`, `user.Id`, hash of the new token, `IssuedAt = now`, `ExpiresAt = now + JwtOptions.RefreshTokenExpirationDays`, `RotatedAt = null`. One `SaveChangesAsync` | core rotator steps 9–10 | `RefreshTokenRotatorTests.RotateAsync_ValidToken_ReturnsNewTokenAndSavesOnce`, rotator SignIn test |
| B17 | Only SHA-256 hex hashes are stored, never the raw token | core `RefreshTokenHash` | **`RefreshTokenRotatorTests.RotateAsync_ValidToken_StoresOnlyHashes`**, **`RefreshTokenHashTests.*`** |
| B18 | Access token = `ITokenService.GenerateTokenAsync(user.GetUserClaims())`, which includes `security_stamp_hash` = fingerprint, never the stamp | app handler + `UserClaimsExtensions` | **`RefreshAccessTokenHandlerTests.Handle_ValidToken_SignsAccessTokenWithStampFingerprintClaim`**, app `SecurityStampClaimTests.GetUserClaims_Always_CarriesStampFingerprintNotTheStamp` |
| B19 | Fingerprint = lower-hex SHA-256 of the UTF-8 stamp. A null stamp hashes `""` | core `SecurityStampClaim.Fingerprint` | **`SecurityStampClaimTests.Fingerprint_Stamp_ReturnsLowerHexSha256`**, **`SecurityStampClaimTests.Fingerprint_NullStamp_HashesEmptyString`** |
| B20 | `Matches` = `CryptographicOperations.FixedTimeEquals` over the UTF-8 bytes (constant time, exact and case-sensitive, a length mismatch is false) | core `SecurityStampClaim.Matches` | `SecurityStampClaimTests.Matches_SameStamp_ReturnsTrue` / `Matches_RotatedStamp_ReturnsFalse` (moved), **`Matches_TruncatedFingerprint_ReturnsFalse`**, **`Matches_UpperCaseFingerprint_ReturnsFalse`** |
| B21 | Per request: a missing user-id or stamp claim fails with `USER_NOT_AUTHENTICATED`. Inactive user or stamp mismatch fails with `USER_SUSPENDED`. The result is cached `Users:ActiveStatusCacheSeconds` | app (namespace swap only) | `ActiveUserTokenValidationTests.*`, `CheckUserActiveHandlerTests.*` (incl. `Handle_TokenMintedBeforeStampChanged_ReturnsFalse`), **`SessionEndpointTests.Logout_ThenOldAccessToken_Returns401`** |
| B22 | Cookie: HttpOnly, `Secure` per option, `SameSite=Strict`, path `Auth:RefreshTokenCookiePath`, expires `now + RefreshTokenExpirationDays`. Deleted on logout | app API, unchanged | `SessionEndpointTests.Refresh_ValidCookie_Returns200AndRotatesCookie`, `SessionEndpointTests.Logout_Authenticated_Returns200AndRevokesRefreshToken` |
| B23 | Sign-out and suspend rotate the stamp and clear the active-status cache | app, unchanged | `LogoutHandlerTests.*`, `SuspendUserHandlerTests.*` |
| B24 | `ClaimType == "security_stamp_hash"` | core | **`SecurityStampClaimTests.ClaimType_Always_IsSecurityStampHash`** |

## Existing code touched
| File | Change |
|------|--------|
| `api/core-libraries/Core.Identity/DependencyInjection.cs` | `public static class DependencyInjection` → `public static partial class DependencyInjection`. No other change. |
| `api/core-libraries/Core.Identity/Exceptions/ErrorCodes.cs` | Add `public const string RefreshTokenRevoked = "REFRESH_TOKEN_REVOKED";` after `RefreshTokenUserNotFound`. |
| `api/Elmanhg.Application/Auth/RefreshAccessToken/RefreshAccessTokenHandler.cs` | Rewrite to the thin form below. |
| `api/Elmanhg.Application/Auth/Shared/UserClaimsExtensions.cs` | Add `using Core.Identity.Tokens.AccessToken;`. The `SecurityStampClaim` reference is unchanged. |
| `api/Elmanhg.Application/Users/CheckUserActive/CheckUserActiveHandler.cs` | Replace `using Elmanhg.Application.Auth.Shared;` with `using Core.Identity.Tokens.AccessToken;`. |
| `api/Elmanhg.Api/Authorization/ActiveUserTokenValidation.cs` | Replace `using Elmanhg.Application.Auth.Shared;` with `using Core.Identity.Tokens.AccessToken;`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Delete the line `public const string RefreshTokenRevoked = "REFRESH_TOKEN_REVOKED";`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | After `services.AddValidatedOptions<AuthOptions>(AuthOptions.SectionName);` add `services.AddCoreRefreshTokenRotation<User>();` and `services.AddOptions<RefreshTokenRotationOptions>().Configure<IOptions<AuthOptions>>((rotation, auth) => rotation.ReuseGrace = TimeSpan.FromSeconds(auth.Value.RefreshTokenReuseGraceSeconds));`. Add `using Core.Identity;`, `using Core.Identity.Tokens.RefreshToken;` and `using Elmanhg.Domain.Identity;`. |
| `api/Elmanhg.Infrastructure/Identity/IssuedRefreshTokenRepository.cs` | Add `using Core.Identity.Tokens.RefreshToken;`. Remove `using Elmanhg.Domain.Identity;` if unused (warnings are errors). The SQL body and the post-324 constructor are unchanged. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.IssuedRefreshTokens.cs` | Add `using Core.Identity.Tokens.RefreshToken;`. Keep `using Elmanhg.Domain.Identity;` (for `User`). The mapping body is unchanged. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | Add `using Core.Identity.Tokens.RefreshToken;`. Line `services.AddScoped<IIssuedRefreshTokenRepository, IssuedRefreshTokenRepository>();` is unchanged. |
| `api/Elmanhg.Tests/Application/Features/Auth/RefreshAccessToken/RefreshAccessTokenHandlerTests.cs` | Decision 15: constructor wiring (below), `using`s, the code alias, and one new test. |
| `api/Elmanhg.Tests/Application/Features/Auth/Shared/SecurityStampClaimTests.cs` | Keep only `GetUserClaims_Always_CarriesStampFingerprintNotTheStamp`. `Matches_SameStamp_ReturnsTrue` and `Matches_RotatedStamp_ReturnsFalse` **move** to the core file (same names, same bodies). Add `using Core.Identity.Tokens.AccessToken;` and keep `using Elmanhg.Application.Auth.Shared;` (for `GetUserClaims`). |
| `api/Elmanhg.Tests/Api/Authorization/ActiveUserTokenValidationTests.cs` | Add `using Core.Identity.Tokens.AccessToken;`. Remove `using Elmanhg.Application.Auth.Shared;` if unused. |
| `api/Elmanhg.Tests/Application/Features/Users/CheckUserActive/CheckUserActiveHandlerTests.cs` | Replace `using Elmanhg.Application.Auth.Shared;` with `using Core.Identity.Tokens.AccessToken;`. |
| `api/Elmanhg.Tests/Integration/Auth/IssuedRefreshTokenRepositoryTests.cs` | Replace `using Elmanhg.Application.Auth.Shared;` with `using Core.Identity.Tokens.RefreshToken;`. Remove `using Elmanhg.Domain.Identity;` if unused. Bodies unchanged. |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenServiceTests.cs` | Add one test (B3). |
| `api/Elmanhg.Tests/Integration/Auth/SessionEndpointTests.cs` | Add one test (B21). |
| `docs/security.md` | §8, see Docs. |
| `docs/constitution.md` | Stack line, see Docs. |
| `.claude/skills/dotnet-feature/SKILL.md` | Deltas §5 and §10, see Docs. |

### Files deleted
| Path | Replaced by |
|---|---|
| `api/Elmanhg.Domain/Identity/IssuedRefreshToken.cs` | core C1 |
| `api/Elmanhg.Domain/Identity/IIssuedRefreshTokenRepository.cs` | core C2 |
| `api/Elmanhg.Application/Auth/Shared/RefreshTokenHash.cs` | core C3 |
| `api/Elmanhg.Application/Auth/Shared/SecurityStampClaim.cs` | core C7 |
| `api/Elmanhg.Tests/Domain/Identity/IssuedRefreshTokenTests.cs` | T1 (moved, same 6 tests) |

Afterwards, `grep -rn "Elmanhg.Application.Auth.Shared.RefreshTokenHash\|Elmanhg.Domain.Identity.IssuedRefreshToken\|ErrorCodes.RefreshTokenRevoked" api --include=*.cs` may hit only the `IdentityErrorCodes.RefreshTokenRevoked` / core usages. It must not hit any Migrations file other than the untouched historical Designer files and the snapshot (Decision 3).

## Files to create
All core files: `.cs`, file-scoped namespace, no comments except the one moved WHY comment, `ConfigureAwait(false)` on every await.

| # | Path | Type | Contract |
|---|------|------|----------|
| C1 | `api/core-libraries/Core.Identity/Tokens/RefreshToken/IssuedRefreshToken.cs` | entity | `namespace Core.Identity.Tokens.RefreshToken;` `public class IssuedRefreshToken : Entity` (`using Core.DDD.Entities;`). The body is a **verbatim** copy of the deleted `Elmanhg.Domain.Identity.IssuedRefreshToken`: `Guid UserId`, `Guid FamilyId`, `string TokenHash = string.Empty`, `DateTimeOffset IssuedAt`, `DateTimeOffset ExpiresAt`, `DateTimeOffset? RotatedAt`, `DateTimeOffset? RevokedAt` (all `{ get; private set; }`); `public bool IsRevoked => RevokedAt is not null;`; `private IssuedRefreshToken(Guid id) : base(id) { }`; `public static IssuedRefreshToken Issue(Guid userId, Guid familyId, string tokenHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt)` (new `Guid.NewGuid()` id); `public bool IsReplayed(DateTimeOffset now, TimeSpan reuseGrace) => RotatedAt is { } rotatedAt && now - rotatedAt > reuseGrace;`; `public void Rotate(DateTimeOffset rotatedAt) => RotatedAt ??= rotatedAt;`; `public void Revoke(DateTimeOffset revokedAt) => RevokedAt ??= revokedAt;` |
| C2 | `api/core-libraries/Core.Identity/Tokens/RefreshToken/IIssuedRefreshTokenRepository.cs` | port | `public interface IIssuedRefreshTokenRepository : IRepository<IssuedRefreshToken>` (`using Core.DDD.Repositories;`) with `Task AddIfAbsentAsync(IssuedRefreshToken token, CancellationToken cancellationToken);` (must insert unless a row with the same `TokenHash` exists, and must not throw on that conflict) |
| C3 | `api/core-libraries/Core.Identity/Tokens/RefreshToken/RefreshTokenHash.cs` | static | `public static class RefreshTokenHash { public static string Compute(string refreshToken) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))); }` (verbatim) |
| C4 | `api/core-libraries/Core.Identity/Tokens/RefreshToken/IRefreshTokenRotator.cs` | port | `public interface IRefreshTokenRotator<TUser> where TUser : IdentityUser<Guid>, new() { Task<string> RotateAsync(TUser user, string presentedRefreshToken, CancellationToken cancellationToken); }` |
| C5 | `api/core-libraries/Core.Identity/Tokens/RefreshToken/RefreshTokenRotator.cs` | service | See the body below. |
| C6 | `api/core-libraries/Core.Identity/Tokens/RefreshToken/RefreshTokenRotationOptions.cs` | options | `public sealed class RefreshTokenRotationOptions { public TimeSpan ReuseGrace { get; set; } = TimeSpan.FromSeconds(10); }` |
| C7 | `api/core-libraries/Core.Identity/Tokens/AccessToken/SecurityStampClaim.cs` | static | `namespace Core.Identity.Tokens.AccessToken;` `public static class SecurityStampClaim` is a **verbatim** copy: `public const string ClaimType = "security_stamp_hash";`, the WHY comment line, `public static string Fingerprint(string? securityStamp) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp ?? string.Empty)));`, `public static bool Matches(string presentedFingerprint, string? securityStamp) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presentedFingerprint), Encoding.UTF8.GetBytes(Fingerprint(securityStamp)));` |
| C8 | `api/core-libraries/Core.Identity/DependencyInjection.RefreshTokenRotation.cs` | DI | `namespace Core.Identity;` `public static partial class DependencyInjection` with `public static IServiceCollection AddCoreRefreshTokenRotation<TUser>(this IServiceCollection services) where TUser : IdentityUser<Guid>, new()`. Body in order: `services.TryAddSingleton(TimeProvider.System);` then `services.AddOptions<RefreshTokenRotationOptions>().Validate(x => x.ReuseGrace >= TimeSpan.Zero, "Refresh-token reuse grace must not be negative.").ValidateOnStart();` then `services.AddScoped<IRefreshTokenRotator<TUser>, RefreshTokenRotator<TUser>>();` then `return services;` |
| A1 | `api/Elmanhg.Application/Auth/RefreshAccessToken/RefreshAccessTokenHandler.cs` (rewrite) | handler | See below. |

### C5 — `RefreshTokenRotator<TUser>` (exact port of the current handler body; the order is load-bearing)
```csharp
namespace Core.Identity.Tokens.RefreshToken;

public sealed class RefreshTokenRotator<TUser>(IRefreshTokenService<TUser, Guid> refreshTokenService, IIssuedRefreshTokenRepository issuedRefreshTokenRepository, IOptions<RefreshTokenRotationOptions> rotationOptions, IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider) : IRefreshTokenRotator<TUser> where TUser : IdentityUser<Guid>, new()
```
`RotateAsync(user, presentedRefreshToken, cancellationToken)` steps:
1. `var now = timeProvider.GetUtcNow();`
2. `var presentedHash = RefreshTokenHash.Compute(presentedRefreshToken);` then `var presented = await issuedRefreshTokenRepository.FindAsync(x => x.TokenHash == presentedHash, cancellationToken)`. Use the default tracking: these rows are mutated.
3. `if (presented.Any(x => x.IsRevoked))` → `throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenRevoked);` (no save).
4. `if (presented.Any(x => x.IsReplayed(now, rotationOptions.Value.ReuseGrace)))`:
   - `familyIds = presented.Select(x => x.FamilyId).Distinct().ToList()` (one operator per line);
   - `family = await FindAsync(x => familyIds.Contains(x.FamilyId) && x.RevokedAt == null, …)`;
   - `family.ForEach(x => x.Revoke(now))`;
   - `await SaveChangesAsync(cancellationToken)`;
   - `throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenRevoked);`

   It may be a `private async Task RevokeFamiliesAsync(List<IssuedRefreshToken> presented, DateTimeOffset now, CancellationToken cancellationToken)` helper. The `throw` stays in `RotateAsync`.
5. `var expiresAt = now.AddDays(jwtOptions.Value.RefreshTokenExpirationDays);`
6. `if (presented.Count == 0)`:
   - `await AddIfAbsentAsync(IssuedRefreshToken.Issue(user.Id, Guid.NewGuid(), presentedHash, now, expiresAt), …)`;
   - `presented = await FindAsync(x => x.TokenHash == presentedHash, …)`.
7. `presented.ForEach(x => x.Rotate(now));`
8. `var refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken);`
9. `await issuedRefreshTokenRepository.AddAsync(IssuedRefreshToken.Issue(user.Id, presented[0].FamilyId, RefreshTokenHash.Compute(refreshToken), now, expiresAt), cancellationToken);`
10. `await issuedRefreshTokenRepository.SaveChangesAsync(cancellationToken);` then `return refreshToken;`

`using`s: `Core.Errors`, `Core.Identity.Exceptions`, `Microsoft.AspNetCore.Identity`, `Microsoft.Extensions.Options`.

### A1 — thin handler
```csharp
public sealed class RefreshAccessTokenHandler(ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService, IRefreshTokenRotator<User> refreshTokenRotator) : IRequestHandler<RefreshAccessTokenCommand, AuthResult>
```
`Handle` steps:
1. `var user = await refreshTokenService.ValidateTokenAsync(request.RefreshToken, cancellationToken)` (B2, B3).
2. `if (!user.IsActive) throw new ForbiddenCoreException(ErrorCodes.UserSuspended);` (app `ErrorCodes`, B4).
3. `var refreshToken = await refreshTokenRotator.RotateAsync(user, request.RefreshToken, cancellationToken)`.
4. `var accessToken = tokenService.GenerateTokenAsync(user.GetUserClaims());`
5. `return AuthResultGenerator.Generate(user, accessToken, refreshToken);`

`using`s: `Core.Errors`, `Core.Identity.Tokens.AccessToken`, `Core.Identity.Tokens.RefreshToken`, `Elmanhg.Application.Auth.Shared`, `Elmanhg.Application.Exceptions`, `Elmanhg.Domain.Identity`, `MediatR`. Drop the `Options`, `AuthOptions`, `JwtOptions` and `TimeProvider` dependencies.

Command, validator, result and controller are unchanged.

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `Core.Identity.Exceptions.ErrorCodes.RefreshTokenRevoked` (new in core; app constant removed) | `REFRESH_TOKEN_REVOKED` | `RefreshTokenRotator.RotateAsync` steps 3 and 4 | `UnauthorizedCoreException` | 401 |
| `Core.Identity.Exceptions.ErrorCodes.RefreshTokenIsExpired` (unchanged) | `REFRESH_TOKEN_IS_EXPIRED` | `RefreshTokenService.ValidateTokenAsync` | `UnauthorizedCoreException` | 401 |
| `Core.Identity.Exceptions.ErrorCodes.RefreshTokenUserNotFound` (unchanged) | `REFRESH_TOKEN_USER_NOT_FOUND` | `RefreshTokenService.ValidateTokenAsync` (stale stamp) | `UnauthorizedCoreException` | 401 |
| `Elmanhg.Application.Exceptions.ErrorCodes.UserSuspended` (unchanged) | `USER_SUSPENDED` | `RefreshAccessTokenHandler` | `ForbiddenCoreException` | 403 |
| `Elmanhg.Application.Exceptions.ErrorCodes.RefreshTokenIsRequired` (unchanged) | `REFRESH_TOKEN_IS_REQUIRED` | `RefreshAccessTokenValidator` | validation | 422 |

Resource strings: unchanged, already present in `api/Elmanhg.Api/Resources/Messages.ar.resx` / `Messages.en.resx`:
- `REFRESH_TOKEN_REVOKED`: ar «انتهت جلستك لأسباب أمنية. سجل الدخول مرة اخرى.», en "Your session was ended for your security. Sign in again."
- The other refresh codes: ar «انتهت جلستك. سجل الدخول مرة اخرى.», en "Your session has ended. Sign in again."

Do not edit the resx files.

## Domain behaviour
`IssuedRefreshToken` (C1) has no `UpdationDate` (it is an `Entity`, not an `AuditEntity`), no `BusinessRuleViolationException` and no new invariants.
- `Rotate` and `Revoke` are idempotent first-write-wins (`??=`).
- `IsReplayed` is strictly greater-than the grace.
- `IsRevoked` is computed and ignored in the mapping (`builder.Ignore(x => x.IsRevoked)` stays).

## API surface
No change: `POST /api/auth/refresh` · `[AllowAnonymous]` · rate limit `AuthRateLimitPolicies.Refresh` · body none (cookie `Auth:RefreshTokenCookieName`) · `200 AuthResult` + rotated cookie, `401 REFRESH_TOKEN_IS_EXPIRED | REFRESH_TOKEN_USER_NOT_FOUND | REFRESH_TOKEN_REVOKED`, `403 USER_SUSPENDED`, `422 REFRESH_TOKEN_IS_REQUIRED`. No OpenAPI or web client change.

## Test plan
FluentAssertions (pinned) + NSubstitute + xUnit v3, with `TestContext.Current.CancellationToken` on every async call. Every new core test file uses `namespace Elmanhg.Tests.Core.Identity;`. Inside that namespace, refer to core types only through top-level `using` directives or aliases (`Core.` resolves to `Elmanhg.Tests.Core` inside it).

**Shared fixture for T4.** Mirror `RefreshAccessTokenHandlerTests`:
- `Now = 2026-09-30T12:00Z`, `GraceSeconds = 10`, `OldRefreshToken = "old-refresh-token"`, `NewRefreshToken = "new-refresh-token"`;
- `IRefreshTokenService<User, Guid>` substitute returning `NewRefreshToken`;
- `IIssuedRefreshTokenRepository` substitute backed by `List<IssuedRefreshToken> _records`: `FindAsync` compiles the predicate, `AddAsync` appends, and `AddIfAbsentAsync` calls a swappable `Action<IssuedRefreshToken> _addIfAbsent` that defaults to `_records.Add`;
- `TimeProvider` substitute returning `Now`;
- `JwtOptions { RefreshTokenExpirationDays = 7 }`, `RefreshTokenRotationOptions { ReuseGrace = 10 s }`;
- `User.CreateStudentWithPhone("Ahmed", "01012345678")`;
- a `Record(token, familyId, rotatedAt)` helper as in the existing file.

| # | Test class (file) | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `IssuedRefreshTokenTests` (T1 `Tests/Core/Identity/IssuedRefreshTokenTests.cs`, moved) | `Issue_Always_CreatesUnrotatedUnrevokedToken` | unchanged body |
| 2 | 〃 | `IsReplayed_NotRotated_ReturnsFalse` | unchanged |
| 3 | 〃 | `IsReplayed_WithinGraceOfRotation_ReturnsFalse` | unchanged (exactly grace → false) |
| 4 | 〃 | `IsReplayed_AfterGraceOfRotation_ReturnsTrue` | unchanged (grace + 1 tick → true) |
| 5 | 〃 | `Rotate_AlreadyRotated_KeepsFirstRotationTime` | unchanged |
| 6 | 〃 | `Revoke_AlreadyRevoked_KeepsFirstRevocationTime` | unchanged |
| 7 | `RefreshTokenHashTests` (T2) | `Compute_Token_ReturnsLowerHexSha256OfUtf8` | `RefreshTokenHash.Compute("abc") == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"` |
| 8 | 〃 | `Compute_EmptyToken_ReturnsSha256OfEmptyInput` | `Compute("") == "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"` |
| 9 | `SecurityStampClaimTests` (T3 `Tests/Core/Identity/SecurityStampClaimTests.cs`) | `ClaimType_Always_IsSecurityStampHash` | `SecurityStampClaim.ClaimType == "security_stamp_hash"` |
| 10 | 〃 | `Fingerprint_Stamp_ReturnsLowerHexSha256` | `Fingerprint("stamp-one") == "798751b9a682bbdaa202876c45827cb550273da38a1f2f156ec8ae1044bf34c4"` |
| 11 | 〃 | `Fingerprint_NullStamp_HashesEmptyString` | `Fingerprint(null) == "e3b0c442…b855"` (full value as row 8) |
| 12 | 〃 | `Matches_SameStamp_ReturnsTrue` (moved from the app file) | unchanged body |
| 13 | 〃 | `Matches_RotatedStamp_ReturnsFalse` (moved from the app file) | unchanged body |
| 14 | 〃 | `Matches_TruncatedFingerprint_ReturnsFalse` | `Matches(Fingerprint("stamp-one")[..63], "stamp-one")` is false |
| 15 | 〃 | `Matches_UpperCaseFingerprint_ReturnsFalse` | `Matches(Fingerprint("stamp-one").ToUpperInvariant(), "stamp-one")` is false |
| 16 | `RefreshTokenRotatorTests` (T4) | `RotateAsync_ValidToken_ReturnsNewTokenAndSavesOnce` | returns `NewRefreshToken`; `GenerateTokenAsync(_user, …)` `Received(1)`; `SaveChangesAsync` `Received(1)` |
| 17 | 〃 | `RotateAsync_SignInToken_RecordsItRotatedAndTheNewTokenInOneFamily` | presented record (hash of old) has `RotatedAt == Now`. Issued record has `RotatedAt == null`, the same `FamilyId`, `UserId == _user.Id`, `IssuedAt == Now`, `ExpiresAt == Now.AddDays(7)`. `AddIfAbsentAsync` `Received(1)` |
| 18 | 〃 | `RotateAsync_SignInTokenRecordedByConcurrentRefresh_IssuesTokenInTheWinningFamily` | `_addIfAbsent` records the old token in `winningFamilyId` instead. Presented `FamilyId == winning`, `RotatedAt == Now`; issued `FamilyId == winning`; `SaveChangesAsync` `Received(1)` |
| 19 | 〃 | `RotateAsync_RecordedToken_DoesNotInsertItAgain` | `Record(Old, family, rotatedAt: null)` → `AddIfAbsentAsync` `DidNotReceive()`; issued in `family`; old `RotatedAt == Now` |
| 20 | 〃 | `RotateAsync_RotatedTokenReplayedAfterGrace_RevokesFamilyAndThrowsRefreshTokenRevoked` | replayed rotated at `Now − 11 s`, successor in the same family, other sign-in in another family. `UnauthorizedCoreException` with `ErrorCodes.RefreshTokenRevoked`; `(replayed, successor, other).IsRevoked == (true, true, false)`; replayed `RevokedAt == Now`; `SaveChangesAsync` `Received(1)`; `GenerateTokenAsync` `DidNotReceive()`; no record with hash of `NewRefreshToken` |
| 21 | 〃 | `RotateAsync_ReplayWithEarlierRevokedSibling_KeepsItsFirstRevocationTime` | sibling revoked at `Now − 1 h` in the same family, replayed token rotated at `Now − 11 s` → throws `RefreshTokenRevoked`; sibling `RevokedAt == Now − 1 h` |
| 22 | 〃 | `RotateAsync_RotatedTokenReplayedAtGraceBoundary_IssuesTokenInSameFamily` | rotated at `Now − 10 s` → returns `NewRefreshToken`; issued in the same family; no record revoked; old `RotatedAt` unchanged (`Now − 10 s`) |
| 23 | 〃 | `RotateAsync_ReplayWithinLongerConfiguredGrace_IssuesToken` | a rotator built with `ReuseGrace = 30 s`; token rotated at `Now − 20 s` → returns `NewRefreshToken`; no record revoked |
| 24 | 〃 | `RotateAsync_RevokedToken_ThrowsRefreshTokenRevokedWithoutSaving` | record (not rotated) revoked at `Now − 1 min` → `UnauthorizedCoreException` + `RefreshTokenRevoked`; `SaveChangesAsync` `DidNotReceive()`; `GenerateTokenAsync` `DidNotReceive()` |
| 25 | 〃 | `RotateAsync_RevokedTokenAlsoReplayed_ThrowsWithoutRevokingSiblingsOrSaving` | old token rotated at `Now − 1 h` and revoked at `Now − 30 min`, unrevoked sibling in the same family → throws `RefreshTokenRevoked`; sibling `IsRevoked == false`; `SaveChangesAsync` `DidNotReceive()` |
| 26 | 〃 | `RotateAsync_ValidToken_StoresOnlyHashes` | every `_records` `TokenHash` has length 64; none equals `OldRefreshToken` or `NewRefreshToken`; the set equals `{Compute(Old), Compute(New)}` |
| 27 | `RefreshTokenRotationRegistrationTests` (T5) | `AddCoreRefreshTokenRotation_Always_RegistersScopedRotator` | `ServiceCollection` after the call has a descriptor `ServiceType == typeof(IRefreshTokenRotator<User>)`, `ImplementationType == typeof(RefreshTokenRotator<User>)`, `Lifetime == Scoped` |
| 28 | 〃 | `AddCoreRefreshTokenRotation_NoConfiguration_DefaultsGraceToTenSeconds` | `BuildServiceProvider().GetRequiredService<IOptions<RefreshTokenRotationOptions>>().Value.ReuseGrace == TimeSpan.FromSeconds(10)` |
| 29 | 〃 | `AddCoreRefreshTokenRotation_NegativeGrace_FailsValidation` | after `services.Configure<RefreshTokenRotationOptions>(x => x.ReuseGrace = TimeSpan.FromSeconds(-1))`, reading `.Value` throws `OptionsValidationException` |
| 30 | `RefreshTokenServiceTests` (existing file, new test) | `ValidateTokenAsync_StaleSecurityStamp_ThrowsRefreshTokenUserNotFound` | the protector returns a ticket expiring `Now + 1 day`; `_signInManager.ValidateSecurityStampAsync(Arg.Any<ClaimsPrincipal>()).Returns((User?)null)` → `UnauthorizedCoreException` + `ErrorCodes.RefreshTokenUserNotFound` |
| 31 | `RefreshAccessTokenHandlerTests` (existing, Decision 15) | `Handle_ValidToken_IssuesAccessTokenAndRotatesRefreshToken` | body unchanged |
| 32 | 〃 | `Handle_SignInToken_RecordsItRotatedAndTheNewTokenInOneFamily` | body unchanged |
| 33 | 〃 | `Handle_SignInTokenRecordedByConcurrentRefresh_IssuesTokenInTheWinningFamily` | body unchanged |
| 34 | 〃 | `Handle_RotatedTokenReplayedAfterGrace_RevokesFamilyAndThrowsRefreshTokenRevoked` | body unchanged except `IdentityErrorCodes.RefreshTokenRevoked` |
| 35 | 〃 | `Handle_RotatedTokenReplayedWithinGrace_IssuesTokenInSameFamily` | body unchanged |
| 36 | 〃 | `Handle_RevokedFamilyToken_ThrowsRefreshTokenRevoked` | body unchanged except `IdentityErrorCodes.RefreshTokenRevoked` |
| 37 | 〃 | `Handle_SuspendedUser_ThrowsForbidden` | body unchanged (`ErrorCodes.UserSuspended` app) |
| 38 | 〃 (new) | `Handle_ValidToken_SignsAccessTokenWithStampFingerprintClaim` | `_user.SecurityStamp = "stamp-one"`; `_tokenService.Received(1).GenerateTokenAsync(Arg.Is<List<Claim>>(claims => claims.Any(x => x.Type == SecurityStampClaim.ClaimType && x.Value == SecurityStampClaim.Fingerprint("stamp-one"))))` and `result.AccessToken == "access-token"` |
| 39 | `SecurityStampClaimTests` (app file, kept) | `GetUserClaims_Always_CarriesStampFingerprintNotTheStamp` | body unchanged |
| 40 | `SessionEndpointTests` (existing file, new test) | `Logout_ThenOldAccessToken_Returns401` | register by phone; read `accessToken`; logout with it → 200; a second `POST /api/auth/logout` with the **same** bearer → `401` (the stamp fingerprint no longer matches; the user is still active) |

Wiring for rows 31–38 (`RefreshAccessTokenHandlerTests` constructor):
```csharp
var rotationOptions = Options.Create(new RefreshTokenRotationOptions { ReuseGrace = TimeSpan.FromSeconds(GraceSeconds) });
var jwtOptions = Options.Create(new JwtOptions { RefreshTokenExpirationDays = 7 });
_handler = new RefreshAccessTokenHandler(_tokenService, _refreshTokenService, new RefreshTokenRotator<User>(_refreshTokenService, _issuedRefreshTokenRepository, rotationOptions, jwtOptions, _timeProvider));
```
Remove `using Elmanhg.Application.Shared.Options;`. Add `using Core.Identity.Tokens.AccessToken;`, `using IdentityErrorCodes = Core.Identity.Exceptions.ErrorCodes;`, and keep `using Elmanhg.Application.Exceptions;` (for `UserSuspended`). `RefreshTokenHash` now comes from `using Core.Identity.Tokens.RefreshToken;` (already imported).

Existing tests that must stay green **unchanged in body**:
- `RefreshTokenReuseTests` (4)
- `SessionEndpointTests` (5 existing)
- `IssuedRefreshTokenRepositoryTests` (1)
- `UsersEndpointTests.Suspend_SignedInStudent_RevokesAccessAndRefreshTokens` and `Reactivate_SuspendedStudent_AllowsSignInAgain`
- `ActiveUserTokenValidationTests` (4)
- `CheckUserActiveHandlerTests` (5)
- `LogoutHandlerTests` (3)
- `RefreshAccessTokenValidatorTests` (2)
- `AuthOptionsTests`
- `JwtTokenServiceTests`, `RefreshTokenServiceTests` (existing 2)
- `AppDbContextTests` (incl. `HasPendingModelChanges` false)
- `SoftDeleteQueryFilterModelTests` (the `"IssuedRefreshToken"` short name is unchanged)
- `DomainAssemblyReferencesTests` (3)
- `PhoneAuthEndpointTests`

Commands:
- `dotnet build api/Elmanhg.slnx -c Release` (0 warnings; warnings are errors)
- `dotnet test api/Elmanhg.slnx -c Release` (Docker required for integration)
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api`

## Docs (docs-sync)
| Doc | Edit |
|---|---|
| `docs/security.md` §8 "Reuse detection" bullet | Append: "Rotation, reuse detection, family revocation and the token hash live in `Core.Identity` (`IRefreshTokenRotator<TUser>`, `IssuedRefreshToken`, `IIssuedRefreshTokenRepository`, `RefreshTokenHash`). The app maps the `IssuedRefreshTokens` table in `AppDbContext`, implements the insert-if-absent, checks that the user is active before rotating, and supplies the grace window from `Auth:RefreshTokenReuseGraceSeconds`." |
| `docs/security.md` §8 "Access token" bullet | After "`security_stamp_hash`), never the stamp." add: "The fingerprint and its constant-time comparison are `Core.Identity` `SecurityStampClaim`." |
| `docs/constitution.md` stack line (line 3 after 324) | After the `Core.Settings (…)` entry insert: "`Core.Identity` refresh-token rotation (single-use rotation with reuse detection inside a grace window and family revocation over an app-mapped `IssuedRefreshToken` store, SHA-256 `RefreshTokenHash`, and the `SecurityStampClaim` stamp fingerprint compared in constant time)," |
| `.claude/skills/dotnet-feature/SKILL.md` Elmanhg deltas §5 "Promoted in Elmanhg" bullet | Append: "Plus `Core.Identity` refresh-token rotation: refresh a session with `IRefreshTokenService.ValidateTokenAsync` then `IRefreshTokenRotator<TUser>.RotateAsync(user, presentedToken, cancellationToken)` (registered by `AddCoreRefreshTokenRotation<TUser>()`, grace in `RefreshTokenRotationOptions`); the app maps `IssuedRefreshToken` in `AppDbContext` and implements `IIssuedRefreshTokenRepository.AddIfAbsentAsync`; stamp fingerprints go through `SecurityStampClaim`." |
| `.claude/skills/dotnet-feature/SKILL.md` §10 Security Gotchas table | Add a row after `JWT`: `\| Refresh tokens \| rotate only through `Core.Identity` `IRefreshTokenRotator<TUser>`; store `RefreshTokenHash`, never the raw token; compare stamp fingerprints with `SecurityStampClaim.Matches`, never `==` \|` |
| `docs/user-administration.md`, `docs/deployment.md`, `docs/PRD.md` | No edit. Behaviour, config keys and claim names are unchanged, so these stay accurate. |

## Definition of done
- [ ] Branch merged with `origin/main` containing PR 324 before any change.
- [ ] C1–C8 exist with exactly the listed namespaces, signatures and bodies. `RefreshTokenRotator.RotateAsync` follows steps 1–10 in order.
- [ ] `IssuedRefreshToken`, `RefreshTokenHash` and `SecurityStampClaim` bodies are byte-equivalent to the deleted app versions (same hash format, same claim type, `FixedTimeEquals`).
- [ ] The 5 listed files are deleted. No `Elmanhg.Domain.Identity.IssuedRefreshToken`, `Elmanhg.Application.Auth.Shared.RefreshTokenHash` or `…SecurityStampClaim` type remains.
- [ ] `Core.Identity` contains no Elmanhg name, no app error code, and no `Auth:` section name. `Core.Identity.csproj` is unchanged: no new package or project reference.
- [ ] `Elmanhg.Domain.csproj` still references only `Core.DDD` and `Core.Settings`. `DomainAssemblyReferencesTests` passes.
- [ ] `RefreshAccessTokenHandler` has exactly 3 constructor dependencies and the 5 steps of A1.
- [ ] `Auth:RefreshTokenReuseGraceSeconds` still drives the grace (Application DI mapping into `RefreshTokenRotationOptions`). `AuthOptions` is unchanged.
- [ ] `REFRESH_TOKEN_REVOKED` is thrown from core. The app `ErrorCodes.RefreshTokenRevoked` is removed. The resx files are unchanged.
- [ ] No migration added. `AppDbContextTests` `HasPendingModelChanges` is false, and the `has-pending-model-changes` CLI is clean. The snapshot is untouched unless Decision 3's fallback was needed (state it in `02-implementation.md` if so).
- [ ] `CoreDbContext` and `Core.Notifications` are untouched.
- [ ] All 40 test rows exist with these exact names. Every existing auth, user and persistence test listed above passes unchanged in body (only the Decision 15 and namespace edits).
- [ ] Build has 0 warnings. The full `dotnet test` is green, including integration (Docker).
- [ ] No new comments except the moved WHY line. No `#<number>` in any code comment.
- [ ] `docs/security.md`, `docs/constitution.md` and `.claude/skills/dotnet-feature/SKILL.md` are edited as in Docs. No other doc is created.
