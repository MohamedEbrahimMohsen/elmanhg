# CodeRabbit fixes — Security hardening (#115, PR #243)

| # | Comment | Verdict | Fix |
|---|---|---|---|
| 1 | `.gitleaks.toml`: `regexTarget = "line"` + unanchored `change-me` / `not-a-secret` / `notasecret` | **Valid.** Reproduced: a synthetic commit with a real-looking `api_key = "..."; // change-me before release` (and one with `// notasecret`) is **suppressed** by the old config and **flagged** by the new one. | `regexTarget = "secret"`; three anchored (`^...$`) regexes listing the exact reviewed placeholder values found in full history (`git log -p --all`): the `change-me[-...]` example values, the six `...not-a-secret...` test/CI values, and the four `...passwordnotasecret` smoke/load values. `docs/security.md` §6 "Secret scanning" updated (allow-list description + triage advice: new values are added as exact anchored entries). |
| 2 | `PlaceholderSecretGuard.cs:24` substring check on the raw connection string | **Valid.** `Password="change-me-..."` slipped past `Contains("Password=change-me")`, and `Application Name="Password=change-me";Password=<real>` false-matched. | Parses with `NpgsqlConnectionStringBuilder` and applies the same `IsPlaceholder` (trimmed, case-insensitive `change-me` prefix) to `.Password`; removed the `DatabasePasswordPlaceholder` constant. New tests: `EnsureReplaced_ProductionWithQuotedPlaceholderDatabasePassword_ThrowsNamingConnectionString`, `EnsureReplaced_ProductionWithPlaceholderMarkerOutsideDatabasePassword_DoesNotThrow`. `docs/deployment.md` §3 and `docs/security.md` §6 wording now say "the connection string's parsed `Password` starts with `change-me`". |
| 3 | `RefreshAccessTokenHandler.cs:45-50` non-atomic first-seen record | **Valid.** `TokenHash` index was not unique; two concurrent first refreshes of one sign-in token each inserted a row with its own `FamilyId`, so a later replay in one chain would not revoke the other. | Unique index on `TokenHash` (edited in place in the unmerged `20260930192717_AddIssuedRefreshTokens` migration + Designer + snapshot; the index name is unchanged). New `IIssuedRefreshTokenRepository.AddIfAbsentAsync` = `INSERT ... ON CONFLICT ("TokenHash") DO NOTHING` (same pattern as `UserActivityDayRepository.AddIfAbsentAsync`); the handler then re-reads the row by hash, so both requests rotate the one stored row and issue successors into its family. `SaveChangesAsync` still once. `docs/security.md` reuse-detection paragraph updated. Tests: unit `Handle_SignInTokenRecordedByConcurrentRefresh_IssuesTokenInTheWinningFamily`; integration `IssuedRefreshTokenRepositoryTests.AddIfAbsentAsync_SameTokenFromTwoConcurrentRequests_StoresOneFamily` (real Postgres, two scopes via `Task.WhenAll`) and `RefreshTokenReuseTests.Refresh_SignInCookieRefreshedConcurrently_Returns200AndKeepsOneFamily` (two concurrent HTTP refreshes: both 200, 3 rows, 1 family). |

## Files modified / created
- `.gitleaks.toml`
- `api/Elmanhg.Api/Hosting/PlaceholderSecretGuard.cs`
- `api/Elmanhg.Application/Auth/RefreshAccessToken/RefreshAccessTokenHandler.cs`
- `api/Elmanhg.Domain/Identity/IIssuedRefreshTokenRepository.cs`
- `api/Elmanhg.Infrastructure/Identity/IssuedRefreshTokenRepository.cs`
- `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.IssuedRefreshTokens.cs`
- `api/Elmanhg.Infrastructure/Migrations/20260930192717_AddIssuedRefreshTokens.cs`, `.Designer.cs`, `AppDbContextModelSnapshot.cs`
- `api/Elmanhg.Tests/Api/Hosting/PlaceholderSecretGuardTests.cs` (+2 tests)
- `api/Elmanhg.Tests/Application/Features/Auth/RefreshAccessToken/RefreshAccessTokenHandlerTests.cs` (constructor wires `AddIfAbsentAsync`; +1 test; no assertion changed)
- `api/Elmanhg.Tests/Integration/Auth/RefreshTokenReuseTests.cs` (+1 test)
- `api/Elmanhg.Tests/Integration/Auth/IssuedRefreshTokenRepositoryTests.cs` (new)
- `docs/security.md`, `docs/deployment.md`

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| (CodeRabbit) "unique index + on conflict reload" | Unique index needs a schema change | Edited the PR's own unmerged migration in place instead of adding a second migration, so `AppDbContextTests` migration list stays as is. Any local dev DB that already applied the old version keeps a non-unique index until it is recreated. |

## Build & test
- gitleaks 8.30.1 linux_x64, SHA-256 `551f6fc8...70eb` verified (`sha256sum -c`: OK), run in `alpine:3.20` on a fresh clone of this branch with all refs, exact CI command `gitleaks git --config .gitleaks.toml --redact --no-banner --exit-code 1 .` with the new config: **172 commits scanned, no leaks found**. Also: default rules alone find nothing in history, i.e. the allow-list is currently defensive. The uncommitted added lines scanned with `gitleaks dir`: no leaks found.
- `dotnet build -c Release` (api/): **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet test -c Release --no-build` (api/, Docker/Testcontainers): **total 4429, failed 0, succeeded 4429, skipped 0** (was 4424; +5 new tests). Includes `Model_Current_MatchesLatestMigrationSnapshot` (no pending model changes).
- Not committed, per instructions.

## Notes for review
- `AddIfAbsentAsync` executes immediately (not in the unit of work), like the existing `UserActivityDayRepository.AddIfAbsentAsync`; if the handler later throws, the unrotated first-seen row stays, which is the same row the next refresh would create.
- Concurrent requests both set `RotatedAt ??= now` on the shared row; last writer wins, both inside the grace window. Intended.
- The HTTP concurrency test proves the outcome, not that the race window was hit on every run; the repository test is the deterministic proof (the unique constraint decides).
