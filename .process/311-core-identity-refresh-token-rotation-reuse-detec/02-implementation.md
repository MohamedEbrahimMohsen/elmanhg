# Implementation — Core.Identity: refresh-token rotation, reuse detection and security-stamp claim (E21.S8, story 311)

Base: waited for PR 324 (polled every 3 min; MERGED at 04:04), then `git fetch origin && git merge --ff-only origin/main` → `a81bd16b [E21.S2] Core hygiene …`. No commits made, nothing pushed.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.Identity/Tokens/RefreshToken/IssuedRefreshToken.cs` | 35 | C1, `git mv` from `Elmanhg.Domain/Identity`; only the namespace changed |
| `api/core-libraries/Core.Identity/Tokens/RefreshToken/IIssuedRefreshTokenRepository.cs` | 8 | C2, `git mv` from `Elmanhg.Domain/Identity`; only the namespace changed |
| `api/core-libraries/Core.Identity/Tokens/RefreshToken/RefreshTokenHash.cs` | 9 | C3, `git mv` from `Elmanhg.Application/Auth/Shared`; only the namespace changed |
| `api/core-libraries/Core.Identity/Tokens/RefreshToken/IRefreshTokenRotator.cs` | 8 | C4 port |
| `api/core-libraries/Core.Identity/Tokens/RefreshToken/RefreshTokenRotator.cs` | 50 | C5, steps 1–10 in plan order; family revocation in `RevokeFamiliesAsync`, the `throw` stays in `RotateAsync` |
| `api/core-libraries/Core.Identity/Tokens/RefreshToken/RefreshTokenRotationOptions.cs` | 6 | C6 (`ReuseGrace` default 10 s) |
| `api/core-libraries/Core.Identity/Tokens/AccessToken/SecurityStampClaim.cs` | 14 | C7, `git mv` from `Elmanhg.Application/Auth/Shared`; only the namespace changed (WHY comment kept) |
| `api/core-libraries/Core.Identity/DependencyInjection.RefreshTokenRotation.cs` | 19 | C8 `AddCoreRefreshTokenRotation<TUser>()` |
| `api/Elmanhg.Tests/Core/Identity/IssuedRefreshTokenTests.cs` | 72 | T1, `git mv` from `Tests/Domain/Identity`; namespace and `using` only (6 tests) |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenHashTests.cs` | 19 | T2 (2 tests) |
| `api/Elmanhg.Tests/Core/Identity/SecurityStampClaimTests.cs` | 57 | T3 (7 tests; the 2 `Matches_*` tests moved with their bodies unchanged) |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenRotatorTests.cs` | 176 | T4 (11 tests, rows 16–26) |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenRotationRegistrationTests.cs` | 45 | T5 (3 tests) |

## Files modified
| Path | Change |
|---|---|
| `api/core-libraries/Core.Identity/DependencyInjection.cs` | `static class` → `static partial class` |
| `api/core-libraries/Core.Identity/Exceptions/ErrorCodes.cs` | + `RefreshTokenRevoked = "REFRESH_TOKEN_REVOKED"` |
| `api/Elmanhg.Application/Auth/RefreshAccessToken/RefreshAccessTokenHandler.cs` | Thin A1 form: 3 dependencies; validate → active check → `RotateAsync` → access token → result |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Removed `RefreshTokenRevoked` |
| `api/Elmanhg.Application/DependencyInjection.cs` | `AddCoreRefreshTokenRotation<User>()` + maps `AuthOptions.RefreshTokenReuseGraceSeconds` into `RefreshTokenRotationOptions.ReuseGrace`; 3 usings |
| `api/Elmanhg.Application/Auth/Shared/UserClaimsExtensions.cs` | + `using Core.Identity.Tokens.AccessToken;` |
| `api/Elmanhg.Application/Users/CheckUserActive/CheckUserActiveHandler.cs` | Namespace swap of the `using` |
| `api/Elmanhg.Api/Authorization/ActiveUserTokenValidation.cs` | Namespace swap of the `using` |
| `api/Elmanhg.Infrastructure/Identity/IssuedRefreshTokenRepository.cs` | `using Elmanhg.Domain.Identity` → `using Core.Identity.Tokens.RefreshToken` (SQL and constructor unchanged) |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.IssuedRefreshTokens.cs` | + `using Core.Identity.Tokens.RefreshToken;` (mapping unchanged) |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | + `using Core.Identity.Tokens.RefreshToken;` |
| `api/Elmanhg.Tests/Application/Features/Auth/RefreshAccessToken/RefreshAccessTokenHandlerTests.cs` | Decision 15 wiring (real `RefreshTokenRotator<User>`), using changes, `IdentityErrorCodes` alias in 2 asserts, + `Handle_ValidToken_SignsAccessTokenWithStampFingerprintClaim`. The 7 existing test names and bodies are unchanged apart from the alias |
| `api/Elmanhg.Tests/Application/Features/Auth/Shared/SecurityStampClaimTests.cs` | Keeps only `GetUserClaims_Always_CarriesStampFingerprintNotTheStamp`; + core `using` |
| `api/Elmanhg.Tests/Api/Authorization/ActiveUserTokenValidationTests.cs` | `using` swap (old one was no longer used) |
| `api/Elmanhg.Tests/Application/Features/Users/CheckUserActive/CheckUserActiveHandlerTests.cs` | `using` swap |
| `api/Elmanhg.Tests/Integration/Auth/IssuedRefreshTokenRepositoryTests.cs` | `using` swap; removed `Elmanhg.Domain.Identity` because it was no longer used |
| `api/Elmanhg.Tests/Core/Identity/RefreshTokenServiceTests.cs` | + `ValidateTokenAsync_StaleSecurityStamp_ThrowsRefreshTokenUserNotFound` |
| `api/Elmanhg.Tests/Integration/Auth/SessionEndpointTests.cs` | + `Logout_ThenOldAccessToken_Returns401` and a `SendLogoutAsync` helper |
| `docs/security.md` | §8 Access-token bullet and Reuse-detection bullet, using the plan's wording |
| `docs/constitution.md` | Stack line: `Core.Identity` refresh-token rotation entry added after `Core.Settings (…)` |
| `.claude/skills/dotnet-feature/SKILL.md` | Deltas §5 "Promoted" bullet extended; §10 new `Refresh tokens` row after `JWT` |

Deleted by `git mv`: the 4 app files and `Tests/Domain/Identity/IssuedRefreshTokenTests.cs`, as the plan lists.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| SKILL.md deltas §5: "Append" to the "Promoted in Elmanhg" bullet | After PR 324 that bullet ends with "…`ICurrentUser` (…) and repository audit stamping." | Appended the plan's sentence verbatim at the end of the bullet. No change in substance. |

There were no other deviations. Decision 3's snapshot fallback was **not** needed. The snapshot and migrations are untouched.

## Build & test
- `dotnet build api/Elmanhg.slnx -c Release` → **Build succeeded.** The build still prints the existing nullable warnings in `Core.Notifications`, `Core.OTP` and `Core.Validation`. I did not touch those files, and the build does not treat these warnings as errors. My files added no new warnings.
- OpenAPI check, as CI runs it: `git status --porcelain api/openapi` → empty (unchanged).
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build` (with CI's connection-string env) → `No changes have been made to the model since the last migration.`
- `dotnet test api/ -c Release --no-build` (Docker up, integration included) → `Test run summary: Passed! total: 5544 failed: 0 succeeded: 5544 skipped: 0`.
- Focused rerun of the rotation, registration, SecurityStampClaim, RefreshTokenHash, IssuedRefreshToken, handler, RefreshTokenService, SessionEndpoint, RefreshTokenReuse, DomainAssemblyReferences and AppDbContext test classes → 84/84 passed.

## Notes for review
- Gates: `CoreDbContext`, `Core.Notifications`, `Core.Identity.csproj`, `Elmanhg.Domain.csproj`, the migrations, the snapshot and the resx files are untouched. Core.Identity has no `Elmanhg` name and no `Auth:` key.
- In row 20 (`RotateAsync_RotatedTokenReplayedAfterGrace_…`) I folded `replayed.RevokedAt == Now` into the tuple assertion. Row 26 checks "none equals the raw token" with `OnlyContain(x => x.Length == 64 && x != Old && x != New)`.
- `RefreshTokenRotatorTests.cs` has 176 lines, over the ~100-line guide. It mirrors the existing 160-line handler test file, and the plan puts all 11 tests in this one class.
- Index state: the `git mv` renames are staged, as asked. I also ran `git add -N` (intent-to-add) on the new files so they show up in `git status` and `git diff`. Nothing is committed.
- Postman: unchanged (no endpoint or contract changed).
