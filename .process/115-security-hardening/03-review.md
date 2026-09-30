VERDICT: APPROVED

# Review — [E13.S4] Security hardening (#115)

Worktree `D:/Personal/elmanhg-wt/115`, branch `feature/115-security-hardening`, uncommitted against `origin/main` (`a59d34e`).
Judged against `01-plan.md`, the plan-gate conditions in `00-acceptance.md`, `02-implementation.md`, `.claude/skills/dotnet-feature/SKILL.md`, `.claude/skills/python-feature/SKILL.md`, the testing conventions, `.claude/rules/docs-sync.md` and PROGRESS.md "Conventions and decisions".

## Blocking
None.

## Rulings on the questions the orchestrator asked

**(a) Refresh-token reuse detection (#137): correct. The residual risks are the documented trade-offs, not bugs.**
- Family revocation (`RefreshAccessTokenHandler.cs:32-41`): the replayed row's `FamilyId`s are collected, then every unrevoked row in them is revoked and saved before the throw. No MediatR transaction behaviour rolls this back: I checked every `IPipelineBehavior` and found none that opens a transaction. `FindAsync` tracks by default (`Repository.cs:135-139`), so `Revoke` persists. `RefreshTokenReuseTests.Refresh_RotatedCookieReplayed_Returns401AndRevokesTheWholeFamily` proves this end to end, because the successor is refused too. Other sign-ins are untouched (`Refresh_ReplayInOneSignIn_LeavesOtherSignInValid`).
- The grace window only applies to the presented token. `IssuedRefreshToken.IsReplayed` (`IssuedRefreshToken.cs:30`) reads that row's own `RotatedAt`. `Rotate` uses `??=` (`:32`), so a second presentation cannot slide the window forward.
- Can a thief exploit the grace window? Only by presenting the stolen token within 10 s of the legitimate rotation (either order). The thief then gets an independent token in the same family (`:45-55`). Neither branch ever replays, so the fork is never detected. It lives until sign-out or suspension rotates the stamp. That is the same position as before #137 for every stolen token, so nothing regresses. The chance is low: the cookie is HttpOnly and sent over TLS only, and the thief would have to hit a 10 s window they cannot observe. See non-blocking 1.
- Concurrent refreshes:
  - Two first refreshes of one sign-in token can create two families. The token-hash index is non-unique on purpose, and a later replay revokes both, because `presented` holds both rows.
  - Two concurrent refreshes of a recorded token both rotate it and fork. That is the same outcome as the grace window.
  - One narrow race remains: a revocation can run while a sibling refresh is inserting its successor. The family query at `:38` cannot see the uncommitted insert, so that successor survives. The attacker cannot choose this timing. Non-blocking 2.
- Table growth: one row per refresh, never purged (D10). Lookups stay indexed (`TokenHash`, `FamilyId`, `ExpiresAt`). An expired row can safely be deleted, because `RefreshTokenService.ValidateTokenAsync` refuses an expired ticket before the lookup. A sweep is cheap: it would mirror `TrainingExportRetentionWorker` with a delete where `ExpiresAt < now`. This does not block the merge: there is no live deploy, and growth is linear and small (about 0.3 KB per row plus indexes). The follow-up issue D10 must actually be filed. Non-blocking 3.

**(b) Security-stamp fingerprint in the JWT (#240): correct, and not a UX regression against the docs.**
- Refresh tokens already die on every device at sign-out. `RefreshTokenService.ValidateTokenAsync` calls `SignInManager.ValidateSecurityStampAsync` (vendored `Core.Identity/Tokens/RefreshToken/RefreshTokenService.cs:43`), and `LogoutHandler` already rotated the stamp before this change. So "sign-out on one device signs out every device" was already the product behaviour; other devices just held on for up to 1 h. This change only removes that lag.
- The PRD says nothing about per-device sign-out. `docs/security.md` §8 and `docs/user-administration.md` (Revocation) now state the all-device behaviour. Code and docs agree, so there is no divergence.
- Revoking only the signing-out device's refresh family is now possible thanks to `IssuedRefreshTokens`. It is a product choice, though, and it would also need `LogoutHandler` to stop rotating the stamp. Not required here. Non-blocking 4.
- Per-request cost: a SHA-256 of about 40 bytes plus a constant-time compare on every request. The DB read happens at most once per user per `Users:ActiveStatusCacheSeconds` (30 s), from the `IMemoryCache` entry `user-active:{id}`, which now holds (active, stamp).
- Fail-closed:
  - A missing claim fails authentication (`ActiveUserTokenValidation.cs:28-32`, tested).
  - An unknown user returns false.
  - A DB exception propagates and nothing is authenticated.
- Sign-out, suspend and reactivate each evict the cache (`LogoutHandler.cs:28`, `SuspendUserHandler.cs:34`, `ReactivateUserHandler.cs:29`).
- Every one of the 7 access-token mint sites uses `GetUserClaims()`, so every token carries the claim.

**(c) Edge: approved.**
- The CSP matches the built app. I re-checked `web/dist`:
  - `index.html` has a single module script with `src`, and no inline scripts.
  - No third-party origins appear in the assets.
  - `importScripts` appears only in the SignalR ESM worker probe, which `worker-src 'self' blob:` covers.
  - SignalR goes to `/api/hubs/notifications` on the same origin.
  - Paymob is reached by a top-level navigation (`useCheckout.ts:23`), which `form-action` does not govern.
- `MEDIA_ORIGIN` is config-driven: `Caddyfile:85`, the compose `web` service environment, `deploy/.env.example`.
- `/api/*` gets its own CSP, `?Cache-Control no-store` and the `Server` header removed (`Caddyfile:76-80`).
- The six `CF-*` headers are stripped on every API proxy, including both health routes (`Caddyfile:36-52`). Smoke check S5 pins this, and it is not vacuous: the core logger masks `CF-Connecting-IP` to /24, which is exactly the string S5 searches for.

**(d) Rate limits: approved.**
- IP partitions use `Connection.RemoteIpAddress` (`RateLimitPartitions.cs:44`). Forwarded headers set it, and only from `ReverseProxy__TrustedNetworks` (the compose subnet). Forwarded-header handling runs first in the pipeline (`Program.cs:160`).
- Caddy without `trusted_proxies` replaces a client-sent `X-Forwarded-For`. The existing test `Post_UntrustedPeerSendsForwardedFor_IgnoresHeaderAndReturns429` pins the API side.
- Per-student partitions key on the user-id claim. Authentication runs before `UseRateLimiter` (the implicit `UseAuthentication`). `SendAvatarMessage_TwoStudentsAtLimitOne_NeitherIsLimited` would fail if the key fell back to the shared `unknown` IP.
- The one-in-flight cap:
  - It is a `GlobalLimiter` partition (no `CreateChained`).
  - It applies only to the two student policies.
  - The lease is held until the response completes.
  - `CoreExceptionMiddleware` sits before `UseRateLimiter`, so a rejection returns 429 `TOO_MANY_REQUESTS`.

**(e) Uploads: approved.**
- `LessonImageFormats.HasMatchingSignature` and `QuestionImportFile.HasZipSignature` run after the size check (`UploadLessonImageValidator.cs:23-24`, `QuestionImportFileValidation.cs:20-21`). The diagram-image endpoint from #241 already checks signatures.
- The Kestrel 10 MB cap sits above every single-file cap. Each multipart endpoint carries one file of at most 5 MB, so a valid request cannot hit the transport cap.

**(f) Boot refusal: approved.**
- The guard is skipped in Development and for build-time OpenAPI. It runs after `Build()`, so the `dotnet ef` design-time host is unaffected.
- `ApiFactory` carries real test values; I re-ran the suite green.
- `smoke-test.sh` and `load-test.sh` call `replace_example_secrets`. Both already overrode `POSTGRES_PASSWORD` with a value that is not `change-me`.
- No other workflow boots the API outside Development (`api-ci` and `web-ci` do not).
- The ai guard applies only when env is production.

**(g) CI: approved.**
- gitleaks 8.30.1 and Trivy 0.74.0 are installed from release tarballs and checked with `sha256sum -c`. Actions are pinned exactly as the sibling workflows pin them (Decision 25).
- I verified all six `.gitleaksignore` entries by reading the flagged lines:
  - The hex test constant `ValidKey` in `TrainingDataOptionsValidatorTests.cs:11`.
  - Prometheus series labels `job="elmanhg-api"` in `elmanhg.rules.test.yml` lines 307 and 322.
  - Each finding appears in both the squash commit and its feature-branch commit.
  - All are false positives.
- Flake risk is low. Trivy downloads its DB once per job, and `--ignore-unfixed` with CRITICAL-only failure is the deliberate Decision 23.

## Non-blocking
1. `api/Elmanhg.Application/Auth/RefreshAccessToken/RefreshAccessTokenHandler.cs:45-55`: a replay within the grace window forks the family, and the fork is never detected. `docs/security.md` §8 says a replay within the grace "gets a new token in the same family" but does not name the residual risk. Add one line to §12. Optionally, cap the grace path to a single successor per rotated row: a second in-grace presentation of the same token would then be treated as reuse.
2. `RefreshAccessTokenHandler.cs:38`: a family revocation cannot see a successor that a concurrent refresh has not committed yet. Closing this would need a family-level lock or a check after insert. It is rare, and the attacker cannot trigger it.
3. `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.IssuedRefreshTokens.cs:126` and `docs/security.md` §12 D10: file the purge issue. A retention worker along the lines of `TrainingExportRetentionWorker` (`ExecuteDeleteAsync` where `ExpiresAt` is before now minus a margin) is about 40 lines.
4. `api/Elmanhg.Application/Auth/Logout/LogoutHandler.cs:27`: per-device sign-out (revoke only the presented cookie's family, and keep the stamp for "sign out everywhere") is now feasible. It is a product decision, so raise it as an issue rather than change it here.
5. `api/Elmanhg.Application/Users/CheckUserActive/CheckUserActiveHandler.cs:29-31` with `LogoutHandler.cs:28`: a request that reads the user before the stamp rotation can re-cache the old stamp after the eviction. For up to 30 s this refuses a fresh sign-in's token and still accepts old ones. It is the same race class as D11 and is covered by that entry.
6. `api/Elmanhg.Api/Authorization/ActiveUserTokenValidation.cs:37`: a stamp mismatch is recorded as the `USER_SUSPENDED` failure. The client only sees a plain 401, so this affects diagnostics only. A separate code such as `ACCESS_TOKEN_REVOKED` would make logs clearer.
7. `deploy/.env.example` `MEDIA_ORIGIN` and `docs/deployment.md` §4: the comment says to set it when `FileStorage__PublicBaseUrl` points at another origin. Today `api/Elmanhg.Api/FileStorage/MediaStorageExtensions.cs:14` builds a `PathString` from `PublicBaseUrl`, which must be a path, so that setup cannot boot yet. The knob looks ahead, which is fine. A note like "requires an absolute-URL media base, not supported yet" would stop an operator trying it.
8. Refresh reuse on flaky mobile networks: if the server commits a rotation but the `Set-Cookie` never arrives, the next refresh more than 10 s later revokes the sign-in and the user must sign in again. This is inherent to rotation. Watch the rate of `REFRESH_TOKEN_REVOKED` after the first live deploy.

## Verified
- `dotnet test api/ -c Release`, with no `appsettings.json` in the worktree (CI parity): **4424 total, 0 failed**, 2 m 10 s. The implementer's figure matches.
- After the build, `git status --porcelain api/openapi web/src/shared/api` is empty, so there is no OpenAPI or Orval drift. No endpoint shape changed, so the Postman collection needs no edit (new codes on existing routes only).
- ai (from `ai/.venv`):
  - `pytest -m "not eval"`: 392 passed, 3 deselected.
  - `ruff check`: clean.
  - `ruff format --check`: 121 files formatted.
  - `mypy src`: 68 files, no issues.
- `npm --prefix web run build`: passes, 228 files precompressed.
- `dotnet format --verify-no-changes`, limited to the changed C# files: no diagnostics.
- Guard grep over tracked and untracked `.cs`: one hit, `context.Result!.Failure!.Message` in `ActiveUserTokenValidationTests`. That is `TokenValidatedContext.Result`, not `Task.Result`, and the line already existed on main.
- Deviations table: every row is accurate. The gate items #137 and #240, `MEDIA_ORIGIN`, the `.Must(file => ...)` lambdas (CS8622), the `PngBytes` input change, and the extra doc edits (`user-administration.md`, `subscriptions.md`, `avatar.md`) are all present.
- No assertion was weakened in the edited existing tests. Only inputs and constructors changed, and `SaveChangesAsync` checks were added.
- Migration `20260930192717_AddIssuedRefreshTokens`: creates the table only, with no destructive operations. The `AppDbContextTests` migration list is updated (the accepted pattern). The soft-delete filter is added in the global method, and the repository is registered.
- `REFRESH_TOKEN_REVOKED` is in `ErrorCodes.cs` and in both resx files.
- Every plan test T1–T46 exists with the exact planned name. The 5 new gate test classes exist as the report lists.
- Docs-sync:
  - `docs/security.md` §1–12 exists.
  - These agree with the code: PRD §14; deployment §3/§4/§5/§11/§12/§13 plus `Auth__RefreshTokenReuseGraceSeconds`; avatar (the "#115" pointer is gone); ask-teacher; paymob §8; question-import; rich-text; ai-service; user-administration (Revocation); subscriptions.
  - No divergence found.
- Not re-run by me: the local smoke test, `caddy validate`, shellcheck and the gitleaks full-history run. I read S1–S5 and the Caddyfile and found them consistent with the claims. The `images` CI job re-runs the smoke test and Trivy.

## Test quality
- `RefreshAccessTokenHandlerTests`: strong. It uses a real in-memory record list with the `FindAsync` predicate compiled, so the family, rotation and revocation state is actually exercised. `SaveChangesAsync` gets `Received(1)` on the rotate and revoke paths and `DidNotReceive` on the revoked and suspended paths.
- `RefreshTokenReuseTests` and `AccessTokenRevocationTests` (HTTP + Postgres): constraining.
  - The sign-out test warms the cache first, so it fails without the eviction at `LogoutHandler.cs:28`.
  - The suspend-then-reactivate test fails without the fingerprint.
- `RateLimitPartitionsTests` (T5–T10): these drive a real `PartitionedRateLimiter` and hold leases, so they are constraining. `StudentRateLimitTests.SendAvatarMessage_TwoStudentsAtLimitOne_NeitherIsLimited` guards the per-user key.
- `EndpointRateLimitTests`: exact-set equality on the 14 anonymous actions. It will fail on any new anonymous endpoint.
- `PlaceholderSecretGuardTests`: the theory runs over `SecretKeys`, plus a test that the value is never in the message. Constraining.
- No vacuous tests found (none that only assert a substitute's configured return).

## Process note
While running the guard grep I ran `git add -N .` and then `git reset -q` in the worktree. That reset **unstaged the implementer's `git mv AuthRateLimiting.cs -> AppRateLimiting.cs`**. Git now shows ` D api/Elmanhg.Api/RateLimiting/AuthRateLimiting.cs` and `?? api/Elmanhg.Api/RateLimiting/AppRateLimiting.cs`. Working-tree contents are unchanged. My attempt to re-stage the rename was denied by the permission system. `git add -A` at commit time stages it again, and git's rename detection keeps it a rename.
