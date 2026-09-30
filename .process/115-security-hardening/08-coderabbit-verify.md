VERDICT: APPROVED

# CodeRabbit fix verification: Security hardening (#115, PR #243)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Api/Hosting/PlaceholderSecretGuardTests.cs:39-53`: the quoted and marker-outside-password cases are tested. The spaced (`; Password = change-me-x ;`) and case (`PASSWORD=CHANGE-ME-x`, `pwd=`) variants have no test. I checked all of them against Npgsql 10.0.1's `NpgsqlConnectionStringBuilder` in a scratch app and each one parses correctly. With the case-insensitive `IsPlaceholder` check, the guard catches them. A theory row for each would pin this behaviour.
- `api/Elmanhg.Api/Hosting/PlaceholderSecretGuard.cs:41`: outside Development, a malformed connection string or one with an unknown keyword now throws `ArgumentException` from the guard. Before this change it failed later, inside Npgsql. The message names the bad keyword, not the value. This is acceptable, but the startup error has changed.
- `api/Elmanhg.Infrastructure/Migrations/20260930192717_AddIssuedRefreshTokens.cs:50-54`: this migration was edited in place. A local database that already ran the old version keeps a non-unique index, and there `ON CONFLICT ("TokenHash")` fails with 42P10 until the database is recreated. `main` does not contain the migration (`git log main --` on the file is empty), so no shared environment is affected. 07 states this deviation.

## Verified
1. **Gitleaks (`.gitleaks.toml:7-13`).** The file sets `regexTarget = "secret"` and has three `^...$` anchored lists of exact values. I used the official `zricethezav/gitleaks:v8.30.1` image on a scratch commit that holds three high-entropy keys. Each key shares its line with a `// change-me ...`, `// notasecret` or `// not-a-secret` marker.
   - The old config (HEAD) reports **no leaks**, so the markers hid the keys.
   - The new config reports **3 leaks** (`generic-api-key`, lines 1-3).
   - Exact placeholder values such as `change-me-openssl-rand-hex-32` and `ci-only-service-token-not-a-secret-0123` are not flagged.
   - A full-history scan of a mirror clone runs the exact CI command (`gitleaks git --config .gitleaks.toml --redact --no-banner --exit-code 1 .`). It scans 193 commits and finds **no leaks**. With `--log-opts=--all` it also finds no leaks.
   - `.gitleaksignore` still covers the 2 pre-existing, fingerprinted false positives. This change does not affect them.
2. **Placeholder guard (`PlaceholderSecretGuard.cs:25,41`).** The guard parses the connection string with `NpgsqlConnectionStringBuilder` and runs `IsPlaceholder` on `.Password`. The removed constant is not referenced anywhere. `Application Name="Password=change-me";Password=real` yields `real`, so there is no false match. A duplicate `Password` key yields the last value, which is the value Npgsql uses. `docs/deployment.md` §3 and `docs/security.md` §6 match the code.
3. **Atomic first record.**
   - `IssuedRefreshTokenRepository.cs:10-19` is a single `INSERT ... ON CONFLICT ("TokenHash") DO NOTHING`, not a check followed by an insert. It lists every non-nullable column (Id, UserId, FamilyId, TokenHash, IssuedAt, ExpiresAt, IsDeleted), and the SQL is parameterised through `ExecuteSqlAsync`. It follows the same pattern as `UserActivityDayRepository`.
   - `RefreshAccessTokenHandler.cs:45-49` re-reads the row by hash after the insert, so both concurrent requests use the winner's `FamilyId`. `SaveChangesAsync` is still called once. No code soft-deletes an issued token, so the re-read always returns the row.
   - The unique index is set in `AppDbContext.IssuedRefreshTokens.cs:18`, the migration (`unique: true`, same index name), the Designer and `AppDbContextModelSnapshot.cs:904` alike. `Model_Current_MatchesLatestMigrationSnapshot` (`HasPendingModelChanges() == false`) passes.
4. **Tests.** `dotnet test -c Release` (api/, CI parity) passes: 4429 total, 0 failed, 0 skipped, exit 0. That matches the count claimed in 07.
5. **Docs.** The reuse-detection paragraph in `docs/security.md` matches the unique hash and insert-if-absent behaviour. I found no divergence.

## Test quality
- `IssuedRefreshTokenRepositoryTests.AddIfAbsentAsync_SameTokenFromTwoConcurrentRequests_StoresOneFamily`: this test constrains the code. A check-then-insert version would hit a unique violation, and a version without the unique index would fail `ON CONFLICT` (42P10). Either way the test fails.
- `RefreshAccessTokenHandlerTests.Handle_SignInTokenRecordedByConcurrentRefresh_IssuesTokenInTheWinningFamily`: this test constrains the code. If the handler used the token it built locally instead of re-reading the row, the issued family would not be `winningFamilyId`.
- `RefreshTokenReuseTests.Refresh_SignInCookieRefreshedConcurrently_Returns200AndKeepsOneFamily`: it checks the end-to-end outcome (3 rows, 1 family), but it cannot guarantee that the race actually happens on every run. The repository test is the deterministic proof. 07 says the same.
- `PlaceholderSecretGuardTests`: the quoted test fails against the old substring check, and the marker-outside test fails against the old code. Both constrain the code.
