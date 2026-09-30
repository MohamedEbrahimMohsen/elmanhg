# Implementation — [E13.S4] Security hardening (#115)

Worktree `D:/Personal/elmanhg-wt/115`, branch `feature/115-security-hardening`, merged with `origin/main` at `a59d34e` (#241) before starting. Nothing committed.
Scope = approved plan + the plan-gate conditions in `00-acceptance.md` (#137 refresh-token reuse detection, #240 pre-suspension access-token check, CSP checked against the built web app, config-driven media origin). The gate items are listed under Deviations because the plan deferred them.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Api/RateLimiting/RateLimitingOptions.cs` | 41 | `RateLimiting` section, 11 keys with code defaults, `[Range]` |
| `api/Elmanhg.Api/RateLimiting/PublicRateLimitPolicies.cs` | 7 | `public-reads`, `payment-webhooks` |
| `api/Elmanhg.Api/RateLimiting/StudentRateLimitPolicies.cs` | 7 | `avatar-messages`, `ask-teacher-submissions` |
| `api/Elmanhg.Api/RateLimiting/RateLimitPartitions.cs` | 45 | `PerClientIp`, `PerUser`, `ConcurrentStudentRequests` (global-limiter partition, no chained limiter) |
| `api/Elmanhg.Api/RateLimiting/AppRateLimiting.cs` | 44 | `git mv` of `AuthRateLimiting.cs`; `AddAppRateLimiting` (options `ValidateOnStart`, all policies, `GlobalLimiter`) |
| `api/Elmanhg.Api/Hosting/KestrelHardening.cs` | 13 | 10 MB body cap, no `Server` header |
| `api/Elmanhg.Api/Hosting/PlaceholderSecretGuard.cs` | 41 | refuses `change-me` / `Password=change-me` / empty `CoreOtp:Secret` outside Development; names keys only |
| `docs/security.md` | 218 | sections 1–12 incl. every CSP origin and the CSP-vs-build check |
| `.github/workflows/security.yml` | 63 | gitleaks 8.30.1 full history (SHA-256 verified) + weekly/dispatch 3-stack audit |
| `.gitleaks.toml` | 9 | default rules + `change-me`/`not-a-secret`/`notasecret` line allow-list |
| `.gitleaksignore` | 13 | 6 triaged false-positive fingerprints, each with a reason |
| `api/Elmanhg.Domain/Identity/IssuedRefreshToken.cs` | 35 | GATE #137: hashed refresh token row (family, rotated/revoked); `Issue`, `IsReplayed`, `Rotate`, `Revoke` |
| `api/Elmanhg.Domain/Identity/IIssuedRefreshTokenRepository.cs` | 5 | GATE #137 |
| `api/Elmanhg.Infrastructure/Identity/IssuedRefreshTokenRepository.cs` | 7 | GATE #137 |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.IssuedRefreshTokens.cs` | 23 | GATE #137: mapping, indexes on hash/family/expiry |
| `api/Elmanhg.Infrastructure/Migrations/20260930192717_AddIssuedRefreshTokens(.Designer).cs` | 68 / generated | GATE #137 migration (create table only; no destructive ops) |
| `api/Elmanhg.Application/Auth/Shared/RefreshTokenHash.cs` | 9 | GATE #137: SHA-256 hex of the refresh token |
| `api/Elmanhg.Application/Auth/Shared/SecurityStampClaim.cs` | 14 | GATE #240: `security_stamp_hash` claim, fingerprint, constant-time match |
| `api/Elmanhg.Tests/Api/RateLimiting/RateLimitPartitionsTests.cs` | 147 | T1–T10 |
| `api/Elmanhg.Tests/Api/RateLimiting/RateLimitingOptionsTests.cs` | 40 | T11–T12 |
| `api/Elmanhg.Tests/Api/Hosting/PlaceholderSecretGuardTests.cs` | 101 | T13–T20 |
| `api/Elmanhg.Tests/Application/Features/Auth/AuthOptionsTests.cs` | 15 | T21 |
| `api/Elmanhg.Tests/Application/Features/Lessons/UploadLessonImage/LessonImageFormatsTests.cs` | 47 | T22–T23 |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/Import/QuestionImportFileTests.cs` | 32 | T26–T27 |
| `api/Elmanhg.Tests/Integration/Composition/EndpointRateLimitTests.cs` | 77 | T29–T31 |
| `api/Elmanhg.Tests/Integration/Composition/SecurityPostureTests.cs` | 21 | T32 |
| `api/Elmanhg.Tests/Integration/Hosting/KestrelHardeningTests.cs` | 18 | T33 |
| `api/Elmanhg.Tests/Integration/RateLimiting/AnonymousRateLimitTests.cs` | 85 | T34–T37 |
| `api/Elmanhg.Tests/Integration/RateLimiting/StudentRateLimitTests.cs` | 107 | T38–T41 |
| `api/Elmanhg.Tests/Domain/Identity/IssuedRefreshTokenTests.cs` | 72 | GATE #137 entity tests (6) |
| `api/Elmanhg.Tests/Application/Features/Auth/Shared/SecurityStampClaimTests.cs` | 35 | GATE #240 (3) |
| `api/Elmanhg.Tests/Integration/Auth/RefreshTokenReuseTests.cs` | 82 | GATE #137 over HTTP: replay revokes family (401 `REFRESH_TOKEN_REVOKED`, successor dead too); replay within grace → 200; other sign-in unaffected |
| `api/Elmanhg.Tests/Integration/Auth/AccessTokenRevocationTests.cs` | 57 | GATE #240 over HTTP: token from before suspend→reactivate is 401, fresh sign-in 200; access token after sign-out 401 |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Api/Program.cs` | `UseKestrelHardening()`, `AddAppRateLimiting()`, `PlaceholderSecretGuard.EnsureReplaced` after `Build()` (skipped for build-time OpenAPI), exactly as planned |
| `api/Elmanhg.Api/RateLimiting/AuthRateLimitPolicies.cs` | `Refresh = "auth-refresh"` |
| `Auth/Avatar/TeacherThreads/PaymobWebhooks/Questions/Plans` controllers | `[EnableRateLimiting(...)]` per plan + usings |
| `api/Elmanhg.Api/appsettings.example.json` | `RateLimiting` section; `Auth.RefreshTokenReuseGraceSeconds: 10` (gate) |
| `api/Elmanhg.Application/Shared/Options/AuthOptions.cs` | `RefreshTokenCookieSecure = true`; gate: `RefreshTokenReuseGraceSeconds` `[Range(0,…)] = 10` |
| `LessonImageFormats.cs` / `UploadLessonImageValidator.cs` | signature check (PNG/JPEG/WEBP/GIF87a/89a), appended to the cascade |
| `QuestionImportFile.cs` / `QuestionImportFileValidation.cs` | `HasZipSignature`, appended to the cascade |
| `api/Elmanhg.Application/Auth/RefreshAccessToken/RefreshAccessTokenHandler.cs` | GATE #137: family rotation + replay detection (see Notes) |
| `api/Elmanhg.Application/Auth/Shared/UserClaimsExtensions.cs` | GATE #240: adds the stamp-fingerprint claim |
| `api/Elmanhg.Application/Users/CheckUserActive/CheckUserActiveQuery.cs` / `CheckUserActiveHandler.cs` | GATE #240: query carries the fingerprint; cache holds active flag + stamp |
| `api/Elmanhg.Api/Authorization/ActiveUserTokenValidation.cs` | GATE #240: missing claim → fail; passes the fingerprint |
| `api/Elmanhg.Application/Auth/Logout/LogoutHandler.cs` | GATE #240: evicts the user's cached state after the stamp rotation |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs`, `Messages.ar.resx`, `Messages.en.resx` | GATE: `REFRESH_TOKEN_REVOKED` (401) in both languages |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs`, `DependencyInjection.cs`, `Migrations/AppDbContextModelSnapshot.cs` | GATE: configure call, soft-delete filter line, repo registration, snapshot |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | 5 `RateLimiting:*PermitLimit` `UseSetting` lines + comment (plan); `Auth:RefreshTokenReuseGraceSeconds = 10` (gate) |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | migration list + `_AddIssuedRefreshTokens` (accepted pattern) |
| `UploadLessonImageValidatorTests.cs` | plan: signature-writing `File` helper, length 12, T24, T25 |
| `PreviewQuestionImportValidatorTests.cs` | plan: zip header in `Upload`, T28 |
| `LessonImagesEndpointTests.cs` | plan: `ImageForm(…, byte[]? bytes = null)`, T42; see Deviations for `PngBytes` |
| `QuestionImportEndpointTests.cs` | plan: corrupt-file input gets `PK\x03\x04`, T43 |
| `RefreshAccessTokenHandlerTests.cs` | GATE: new constructor deps; existing 2 tests kept (assertions extended with SaveChanges checks), 4 reuse cases added |
| `CheckUserActiveHandlerTests.cs`, `ActiveUserTokenValidationTests.cs`, `LogoutHandlerTests.cs` | GATE: inputs updated for the new signatures; assertions untouched; 2 new cases (stale stamp, missing claim) |
| `ai/src/elmanhg_ai/settings.py`, `ai/tests/unit/test_settings.py` | `PLACEHOLDER_PREFIX`, `_production_token_is_not_placeholder`; T44–T46 |
| `deploy/Caddyfile` | `security_headers` + `api_proxy` snippets, API CSP + `?Cache-Control no-store`, SPA CSP with `{$MEDIA_ORIGIN}` |
| `deploy/lib.sh`, `deploy/smoke-test.sh`, `deploy/load-test.sh` | `replace_example_secrets`; S1–S5; load-test limits |
| `deploy/api.env.example`, `deploy/ai.env.example` | placeholder notes, 11 `RateLimiting__*` lines (+ `Auth__RefreshTokenReuseGraceSeconds`) |
| `deploy/docker-compose.prod.yml`, `deploy/.env.example` | GATE: `MEDIA_ORIGIN` passed to the `web` (Caddy) service, documented |
| `.dockerignore` | `api/**/.env`, `api/**/.env.*` |
| `.github/workflows/deploy.yml` | secrets moved to step-level `env` |
| `.github/workflows/images.yml` | Trivy 0.74.0 install + scan steps |
| `docs/PRD.md` §14, `docs/deployment.md` §3/§4/§5/§11/§12/§13, `docs/avatar.md`, `docs/ask-teacher.md`, `docs/paymob.md`, `docs/question-import.md`, `docs/rich-text.md`, `docs/ai-service.md` | per plan |
| `docs/user-administration.md` (Revocation), `docs/subscriptions.md` (Ask a Teacher quota note) | GATE / docs-sync |

Postman and OpenAPI: no endpoint shape changed; `git status --porcelain api/openapi` is empty after the build.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| D1 deferred: refresh-token reuse detection (#137) | Gate condition 1 overrides: implement it, migration allowed | New `IssuedRefreshTokens` table (SHA-256 of each token, `FamilyId`, `RotatedAt`, `RevokedAt`). A rotated token replayed after `Auth:RefreshTokenReuseGraceSeconds` (10 s) revokes every row of its family → 401 `REFRESH_TOKEN_REVOKED`; within the grace a replay gets a new token in the same family (multi-tab boot). Sign-in tokens are recorded lazily on their first refresh, so the six login/register handlers and Core.Identity stay untouched. Files listed above as GATE. |
| D2 deferred: pre-suspension access token (#240), "e.g. SuspendedAt vs iat" | Our JWTs carry no `iat`/`nbf` (Core `JwtTokenService`), and second-precision `iat` vs a suspension timestamp is ambiguous inside one second (flaky tests either way). Suspension already rotates the security stamp. | Access tokens carry a SHA-256 fingerprint of the security stamp (never the raw stamp: Identity's token providers derive codes from it). `CheckUserActive` compares it; missing claim fails closed. No migration needed. Side effect (documented): sign-out now also ends access tokens on all devices, not only refresh tokens. |
| `.Must(LessonImageFormats.HasMatchingSignature)` / `.Must(QuestionImportFile.HasZipSignature)` (method groups) | CS8622: `IFormFile` vs `IFormFile?` nullability does not match the rule's delegate; build fails | `.Must(file => file is not null && X(file))` — same pattern as the sibling validators |
| Media origin not in plan (CSP fully `'self'`) | Gate condition 2: media origin must be config-driven | `{$MEDIA_ORIGIN}` in SPA `img-src`/`media-src`; `MEDIA_ORIGIN: ${MEDIA_ORIGIN:-}` on the compose `web` service; documented in `deploy/.env.example`, `deployment.md` §4 and `security.md` §4. Empty by default (media is `/api/media` also with S3). Files not in the plan's list: `docker-compose.prod.yml`, `deploy/.env.example`. |
| `LessonImagesEndpointTests`: only `ImageForm` input changes | The existing `Post_Admin_StoresServesAndAuditsImage` asserts served bytes `Equal(PngBytes)` where the class field was the 4-byte stub; sending 12 bytes would break that assertion | Class field `PngBytes = TeacherThreadTestData.PngBytes` (input), `ImageForm` defaults to it; the assertion line is unchanged |
| "No other existing test may be edited" | The gate changes `CheckUserActiveQuery`, `LogoutHandler` and `RefreshAccessTokenHandler` signatures | Edited inputs/constructors only in `CheckUserActiveHandlerTests`, `ActiveUserTokenValidationTests`, `LogoutHandlerTests`, `RefreshAccessTokenHandlerTests`; no assertion weakened, new cases added |
| Docs list | Divergence also in `docs/user-administration.md` (said "access tokens carry no stamp") and `docs/subscriptions.md` (Ask a Teacher parallel note), and `docs/avatar.md` "Not in this story" still pointed at #115 | Updated all three (docs-sync rule) |
| `security.md` §12 known gaps D1–D9 | D1/D2 are now done | §12 lists D3–D9 plus D10 (rotated/revoked token rows are not purged yet — follow-up issue) and D11 (the existing ≤30 s cache race from #240) |
| Test-only fake secrets in `PlaceholderSecretGuardTests` | gitleaks `generic-api-key` flagged my realistic-looking fake values | Values carry `not-a-secret`, covered by `.gitleaks.toml`; no ignore entries added for them |

## Build & test
- `dotnet build api/ -c Release` (appsettings.json absent in this worktree = CI parity; nothing to move aside): **Build succeeded**, no new warnings outside vendored `core-libraries`. `git status --porcelain api/openapi`: empty.
- `dotnet test api/ -c Release --no-build` (Docker/Testcontainers): **total 4424, failed 0, succeeded 4424** (first run had 1 failure in my own new test — a second phone OTP hit the OTP cooldown — rewritten to email sign-ins; then green).
- `dotnet ef migrations has-pending-model-changes … --configuration Release --no-build`: "No changes have been made to the model since the last migration."
- `dotnet list api/ package --vulnerable --include-transitive`: 0 vulnerable.
- `dotnet format api/Elmanhg.slnx --verify-no-changes`: only `Elmanhg.Tests/Builders/SubscriptionBuilder.cs` (pre-existing, untouched) plus the known `core-libraries` noise.
- Guard grep: one hit, `context.Result!.Failure!.Message` in `ActiveUserTokenValidationTests` — `TokenValidatedContext.Result`, not `Task.Result` (same line already existed in that file).
- ai: `ruff format --check` 121 files formatted; `ruff check` all passed; `mypy src` no issues (68 files); `pytest -m "not eval"` **392 passed**, 3 deselected.
- web: `npm --prefix web run build` passed (228 files precompressed). Web source untouched, web tests not re-run.
- CSP check of `web/dist`: `index.html` has one `<script type="module" src>` and no inline script; no external origins in JS/CSS (only doc strings like w3.org namespaces); fonts self-hosted (Noto, Readex, KaTeX; one KaTeX face inlined as `data:font/woff2`); no iframes, no workers; `URL.createObjectURL` (voice preview) → `blob:`; KaTeX HTML strings carry `style="…"` → `'unsafe-inline'` styles; the only `Function(…)` is zod's `try/catch` probe → no `unsafe-eval`; SignalR uses WebSocket/SSE/long polling to the same origin → `connect-src 'self'`.
- `caddy validate` (pinned `caddy:2.10-alpine@sha256:4c6e…`) with `MEDIA_ORIGIN` empty and `https://media.example.com`: **Valid configuration** both times.
- shellcheck (`koalaman/shellcheck:stable -x`) on `smoke-test.sh`, `load-test.sh`, `lib.sh`: clean.
- gitleaks 8.30.1 (checksum verified locally: gitleaks `551f6f…70eb` and trivy `2ae6fe…371a` match the plan) over the full local history, all refs (190 commits) with `.gitleaks.toml` + `.gitleaksignore`: **no leaks found**, exit 0. The 6 ignored fingerprints are two test constants (`0123456789abcdef…` in `TrainingDataOptionsValidatorTests`) and Prometheus rule-test series labels, each in a squash-merged feature commit and its main commit. No real secret found (nothing BLOCKED). `gitleaks dir` over the changed folders: clean.
- Local smoke test: `SMOKE_OBSERVABILITY=0 bash deploy/smoke-test.sh` (own project name, ports, subnet and image tag `smoke115` to stay clear of other lanes) → postgres/api/web/ai healthy, **"Smoke test passed"**, exit 0. This exercised the placeholder guard (the API booted in Production with `replace_example_secrets` values), `caddy validate`, S1–S5 (security headers, no `Server`, API CSP, kept `public, max-age=60`, `no-store` on refresh, `CF-Connecting-IP` stripped), migrate twice, backup and restore drill. The observability part and the Trivy step were not run locally (Trivy runs in the `images` CI).

## Notes for review
- Refresh rotation: the handler saves once per path (rotation path, or revoke-then-throw path). Two concurrent first refreshes of one sign-in token create two families (non-unique hash index on purpose); a later replay revokes every family that hash belongs to.
- Grace window (10 s) is a deliberate trade-off for multi-tab refresh (`web/src/shared/lib/http.ts` single-flights per tab only). A thief who replays within 10 s of the legitimate rotation is not detected; after that, detection revokes the family. `0` disables the grace.
- Access tokens issued before this deploy lack the fingerprint claim and are refused (fail closed); the SPA refreshes and continues. At most 1 h of tokens (no live deploy yet).
- `IssuedRefreshTokens` rows are never purged yet (tiny rows, indexed by `ExpiresAt` for a future purge) — needs a follow-up issue (D10).
- `RateLimitPartitionsTests.cs` is 147 lines (test file; production files are all under 50).
- The rate-limit integration tests authenticate on the shared factory and replay the bearer on a derived limited host (same JWT key and DB), as the plan prescribed.
- `Core.Identity.JwtOptions.ExpirationHours` defaults to 72 in code; hosts set 1 through compose. Not changed here.
