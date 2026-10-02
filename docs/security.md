# Security

The security posture of Elmanhg: what is protected, how, and what is still open. Story #115 (E13.S4) wrote this page; later stories keep it current. Configuration keys are listed in [deployment.md](deployment.md) §4.

## 1. Scope

What the platform protects:

- **Student personal data:** phone numbers, email addresses, display names, answers, Avatar conversations, Ask a Teacher questions, photos and voice replies.
- **Payments:** Paymob checkouts and webhooks, and the entitlements they grant ([paymob.md](paymob.md)).
- **Training data:** pseudonymised records and JSONL exports ([training-data.md](training-data.md)). The student id is an HMAC, and the exports are admin-only private files.
- **Accounts:** student, teacher and admin sign-in, and the admin's power over users and content.

## 2. Authorisation review

Method (#115): every controller action was read against its policy, and three tests pin the result.

- `EndpointAuthorizationTests`: every controller action declares a named policy (`DefaultCodes` / `PermissionMatrixPolicies`) or `[AllowAnonymous]`.
- `EndpointRateLimitTests`: the anonymous actions are exactly the reviewed list below, and each one carries `[EnableRateLimiting]`. A new anonymous endpoint fails the test until this page and the list change together.
- `SecurityPostureTests`: no CORS policy is configured; a cross-origin request gets no `Access-Control-Allow-Origin`. Caddy serves the SPA and `/api` from one origin.

The 14 anonymous endpoints:

| Endpoint | Rate-limit policy | Why anonymous |
|---|---|---|
| `POST /api/auth/otp/send` | `auth-otp-requests` | sign-in and sign-up start here |
| `POST /api/auth/otp/verify` | `auth-credentials` | |
| `POST /api/auth/register/phone` | `auth-credentials` | |
| `POST /api/auth/register/email` | `auth-credentials` | |
| `POST /api/auth/login/phone` | `auth-credentials` | |
| `POST /api/auth/login/email` | `auth-credentials` | |
| `POST /api/auth/login/email-code` | `auth-credentials` | |
| `POST /api/auth/invitations/accept` | `auth-credentials` | invitees have no password yet |
| `POST /api/auth/refresh` | `auth-refresh` | authenticated by the HttpOnly refresh cookie |
| `POST /api/analytics/funnel-events` | `analytics-funnel-events` | landing-page funnel before sign-up |
| `POST /api/client-errors` | `client-errors` | the SPA reports crashes, also on the landing page |
| `POST /api/payments/paymob/webhook` | `payment-webhooks` | authenticated by the HMAC signature |
| `GET /api/questions/servable-count` | `public-reads` | landing-page counter |
| `GET /api/plans` | `public-reads` | landing-page and pricing plans |

Also reviewed:

- `GET /health` and the Caddy `/api/health` alias are anonymous by design and return only `Healthy` or `Unhealthy`.
- The SignalR hub `/api/hubs/notifications` requires `AuthenticatedUser`; the token in `?access_token=` is accepted on the hub path only and never logged ([ask-teacher.md](ask-teacher.md)).
- Teacher reads of subject-owned data go through `ISubjectScopedRequest` / `SubjectScopeBehaviour`, which fails closed (403 `SUBJECT_OUT_OF_SCOPE`).
- Owner lookups (sessions, threads, conversations, exports) filter by the caller and return 404 for another owner's id, never 403.
- Private media: `teacher-threads/` files are served only through `TeacherThreadMediaMiddleware` (`CanViewTeacherThreadMedia`: the owning student, a teacher of the subject, an admin). `training-exports/` files are never served by `/api/media`; only an admin downloads them through `GET /api/training-exports/{id}/file`.

Result: no gaps found.

## 3. Rate limits

ASP.NET Core rate limiting (`AppRateLimiting`), in memory per API instance. Every rejection is 429 `TOO_MANY_REQUESTS` (problem+json, localised). Per-IP limits key on the client IP that the forwarded-headers middleware resolves from Caddy ([deployment.md](deployment.md) §11); per-student limits key on the JWT user id (the IP when there is none).

| Policy | Endpoints | Partition | Default | Config keys |
|---|---|---|---|---|
| `auth-otp-requests` | OTP send | IP | 5 per 600 s | `Auth:OtpRequestPermitLimit`, `Auth:OtpRequestWindowSeconds` |
| `auth-credentials` | OTP verify, register, login, accept invitation | IP | 10 per 60 s | `Auth:CredentialPermitLimit`, `Auth:CredentialWindowSeconds` |
| `auth-refresh` | refresh | IP | 60 per 60 s | `RateLimiting:AuthRefreshPermitLimit`, `RateLimiting:AuthRefreshWindowSeconds` |
| `analytics-funnel-events` | funnel events | IP | 60 per 60 s | `Analytics:FunnelEventPermitLimit`, `Analytics:FunnelEventWindowSeconds` |
| `client-errors` | client errors | IP | 30 per 60 s | `ClientErrors:PermitLimit`, `ClientErrors:WindowSeconds` |
| `public-reads` | servable count and plans, one shared bucket | IP | 300 per 60 s | `RateLimiting:PublicReadPermitLimit`, `RateLimiting:PublicReadWindowSeconds` |
| `payment-webhooks` | Paymob webhook | IP | 300 per 60 s | `RateLimiting:PaymentWebhookPermitLimit`, `RateLimiting:PaymentWebhookWindowSeconds` |
| `avatar-messages` | `POST /api/avatar/messages` | student | 20 per 60 s | `RateLimiting:AvatarMessagePermitLimit`, `RateLimiting:AvatarMessageWindowSeconds` |
| `ask-teacher-submissions` | `POST /api/teacher-threads`, `POST /api/teacher-threads/{id}/follow-ups` | student | 10 per 600 s | `RateLimiting:AskTeacherSubmissionPermitLimit`, `RateLimiting:AskTeacherSubmissionWindowSeconds` |

The public-read and webhook limits are generous on purpose: Egyptian mobile carriers put many students behind one IP (CGNAT), and Paymob posts from a few IPs. They stop floods, not users.

**Concurrency cap (#211).** A global limiter allows `RateLimiting:StudentConcurrentRequestLimit` (1) request in flight per student on each of the two student policies: a second Avatar message sent while the first waits for its reply, or a second Ask a Teacher submission, gets 429. This makes the daily Avatar quota and the monthly Ask a Teacher quota exact on one API instance, because the quota check and the usage write can no longer interleave for one student. The cap is a partition of the global limiter rather than a chained limiter, so idle partitions are cleaned up. It is in memory: with several API instances each would allow one (known gap D9).

The business quotas (daily Avatar messages, monthly Ask a Teacher questions, daily quiz answers) stay in [subscriptions.md](subscriptions.md); the rate limits sit in front of them.

## 4. Edge headers

Caddy (`deploy/Caddyfile`) serves the SPA and proxies `/api`, so the headers are set there, once. The API only drops its own `Server` header (Kestrel `AddServerHeader = false`).

On every response (`security_headers` snippet, deferred so proxied API responses get them too):

| Header | Value | Why |
|---|---|---|
| `Strict-Transport-Security` | `max-age=31536000` | HTTPS only for a year. No `includeSubDomains` (other subdomains are unknown) and no `preload` (irreversible). Browsers ignore it over plain HTTP, so it takes effect once the live domain serves HTTPS |
| `X-Content-Type-Options` | `nosniff` | no MIME sniffing of uploads or API responses |
| `X-Frame-Options` | `DENY` | no framing (older browsers; CSP `frame-ancestors` for newer ones) |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | no paths or ids leak to other sites |
| `Permissions-Policy` | `camera=(), geolocation=(), payment=(), usb=(), microphone=(self)` | teachers record voice replies with `getUserMedia`; photos use a file input, which `camera` does not affect |
| `Cross-Origin-Opener-Policy` | `same-origin` | no cross-window access |
| `Server` | removed | |

**SPA Content-Security-Policy** (the `handle` that serves `/srv`):

```
default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';
img-src 'self' data: blob: {$MEDIA_ORIGIN}; media-src 'self' data: blob: {$MEDIA_ORIGIN};
font-src 'self' data:; connect-src 'self'; worker-src 'self' blob:; object-src 'none';
base-uri 'self'; form-action 'self'; frame-ancestors 'none'; manifest-src 'self'
```

Every allowed origin, and why. It was checked against the built app (`npm --prefix web run build`, `web/dist`, #115):

| Directive | Allows | Needed by |
|---|---|---|
| `script-src 'self'` | the hashed `/assets/*.js` bundles | `dist/index.html` has one `<script type="module" src>` and no inline script. No `unsafe-eval`: the only `Function(...)` in the bundles is zod's feature probe inside `try/catch`, which falls back (one harmless CSP report) |
| `style-src 'self' 'unsafe-inline'` | the hashed CSS, plus inline styles | KaTeX renders formulas as HTML strings with `style="…"` attributes; Radix and sonner set inline styles |
| `img-src 'self' data: blob:` | `/api/media` images, `data:` previews, `blob:` previews | lesson and diagram images, the photo preview before upload (`blobToDataUrl`) |
| `media-src 'self' data: blob:` | `/api/media` audio (private `teacher-threads/` files included), `blob:` | teacher voice replies and the local recording preview (`URL.createObjectURL`) |
| `{$MEDIA_ORIGIN}` in `img-src` and `media-src` | one extra origin, empty by default | only when `FileStorage:PublicBaseUrl` points at another origin (an R2 public bucket or a CDN). With `Local` or `S3` storage media stays under `/api/media` on the site origin, so it stays empty. Set `MEDIA_ORIGIN` in the compose `.env` ([deployment.md](deployment.md) §4) |
| `font-src 'self' data:` | self-hosted fonts | fontsource Noto Sans Arabic and Readex Pro, and the KaTeX fonts, all under `/assets`; one KaTeX face is inlined as `data:font/woff2`. No Google Fonts or other font host |
| `connect-src 'self'` | `/api` fetches and SignalR | the production image builds with an empty `VITE_API_BASE_URL`, so the API is same-origin. SignalR connects to `/api/hubs/notifications` by WebSocket (CSP3 `'self'` covers same-host `ws:`/`wss:`), falling back to Server-Sent Events or long polling on the same origin |
| `worker-src 'self' blob:` | nothing today | the build has no workers; kept narrow for future libraries |
| `object-src 'none'`, `base-uri 'self'`, `form-action 'self'`, `frame-ancestors 'none'` | | no plugins, no base-URL or form hijack, no framing |

Not needed, therefore not allowed: no iframes (lesson videos are plain links), no third-party scripts, analytics or fonts. Paymob checkout is a top-level redirect, not a frame or a fetch.

**API responses** (`handle /api/*`): `Content-Security-Policy: default-src 'none'; img-src 'self'; media-src 'self'; frame-ancestors 'none'` (a JSON response never runs anything; images and audio opened directly still display), and `Cache-Control: no-store` unless the API set its own (`?Cache-Control`), so tokens and personal data never land in shared caches. The servable-count 60 s cache and the media cache headers set by the API are kept.

**Client-sent `CF-*` headers.** Every proxy to the API (`api_proxy` snippet) removes `CF-Connecting-IP`, `CF-IPCountry`, `CF-IPCity`, `CF-Region`, `CF-Region-Code` and `CF-Timezone`. The vendored request log reads them as the client location, and no Cloudflare sits in front, so a client could otherwise forge its logged IP. Revisit when a CDN is added (#220).

`deploy/smoke-test.sh` asserts these headers and the `CF-*` stripping in the `images` CI job.

## 5. Input handling

- **Rich text:** sanitised on the server by `RichTextSanitizer` (allow-list, [rich-text.md](rich-text.md)) and again in the browser by DOMPurify in `SafeHtml`.
- **Request bodies:** Kestrel caps every body at 10 MB (`KestrelHardening.MaxRequestBodyBytes`), above the largest business cap; each validator still enforces its own configurable cap.
- **Uploads:** every upload is checked by extension, content type and file signature (magic bytes), never by name alone, and stored under a random name.

| Endpoint | Extensions | Signature | Cap |
|---|---|---|---|
| `POST /api/lessons/{id}/images` | `.png` `.jpg` `.jpeg` `.webp` `.gif` | PNG, JPEG, `RIFF….WEBP`, `GIF87a`/`GIF89a` | `Content:LessonImageMaxSizeInMb` (5) |
| `POST /api/lessons/{id}/diagram-images` | `.png` `.jpg` `.jpeg` `.webp` | PNG, JPEG, WEBP | `Content:LessonImageMaxSizeInMb` (5) |
| `POST /api/teacher-threads` (photo) | `.png` `.jpg` `.jpeg` `.webp` | PNG, JPEG, WEBP | `uploads.askTeacherImageMaxSizeInMb` runtime setting (default `AskTeacher:ImageMaxSizeInMb` 5, at most 9 MB) |
| `POST /api/teacher-inbox/{id}/voice-drafts` | `.webm` `.ogg` `.m4a` `.mp4` | EBML, `OggS`, `ftyp` | `uploads.voiceReplyMaxSizeInMb` runtime setting (default `AskTeacher:VoiceMaxSizeInMb` 5, at most 9 MB) |

The admin Configuration API (`GET /api/configuration/infrastructure`, [configuration.md](configuration.md)) returns only whether each secret is set (`{ key, isSet }`), never its value.
| `POST /api/question-imports/preview` and `POST /api/question-imports` | `.xlsx` | zip `50 4B 03 04` | `Content:QuestionImportMaxFileSizeInMb` (5) |
| `POST /api/payments/paymob/webhook` | JSON | HMAC | 64 KiB |

SVG is never accepted, because it can carry script and media is served from the site origin. Media responses carry `X-Content-Type-Options: nosniff`.

- **Paths:** `LocalDiskFileStorage.ResolvePath` and `PublicMediaMiddleware.IsPublicKey` refuse traversal.
- **Regular expressions** over user input are bounded (`RegexOptions.NonBacktracking` or a timeout).
- **SQL:** EF Core with parameters only; no `FromSqlRaw` with interpolation.

## 6. Secrets

- **Placeholder guard.** Outside Development the API refuses to start, and to migrate, while a known secret still starts with `change-me`, the connection string's parsed `Password` starts with `change-me`, or `CoreOtp:Secret` is empty (`PlaceholderSecretGuard`; the key list is in [deployment.md](deployment.md) §3). The error names the keys, never the values. The ai service refuses a `change-me` service token when `ELMANHG_AI_ENV=production`. Every example value in `deploy/*.env.example` starts with `change-me`; the smoke and load tests replace them with random ones.
- **Secret scanning.** The `security` workflow runs gitleaks 8.30.1 (checksum-verified binary) over the full git history of every pull request and push to main, with `.gitleaks.toml` (the default rules plus an allow-list of the exact reviewed placeholder values, anchored and matched against the extracted secret only, so a marker elsewhere on a line never hides a real credential). Triaged false positives are listed by fingerprint in `.gitleaksignore`, each with its reason. To triage a finding: if it is a real secret, rotate it first (deleting the commit is not enough, the value is already public), then remove it; only a value that is provably not a secret goes into `.gitleaksignore`. A new committed example or test value that the default rules flag is added to that allow-list as an exact, anchored value in the same change.
- **CI secrets.** `deploy.yml` hands each deploy secret only to the steps that use it (step-level `env`), never to the whole job.
- **Images.** `.dockerignore` keeps `appsettings.json`, `appsettings.*.json`, `api/**/.env` and `api/**/.env.*` out of the API build context.
- **Logging:** tokens, passwords, OTP codes, secrets and payment payloads are never logged ([observability.md](observability.md)).

## 7. Supply chain

- Per stack, on every change to that stack: `dotnet list package --vulnerable --include-transitive` (api-ci), `npm audit --audit-level=high` (web-ci), `pip-audit` over the locked requirements (ai-ci).
- Weekly (Monday 04:17 UTC) and on demand: the `security` workflow's `dependency-audit` job runs the same three audits, because new CVEs appear without code changes.
- Container images: after the smoke test, the `images` workflow scans the three built images with Trivy 0.74.0 (checksum-verified). A CRITICAL vulnerability with a fix available fails the job; HIGH ones are reported only; unfixed ones are ignored. Fix a failure by bumping the base image digest ([deployment.md](deployment.md) §5).
- Every base image is pinned by digest, every third-party action by commit SHA or version, and every downloaded CI binary by SHA-256.

## 8. Sessions

- **Access token:** a JWT signed with `CoreJwt:Key`, valid for `CoreJwt:ExpirationHours` (1 h on the hosts). It carries a SHA-256 fingerprint of the user's security stamp (`security_stamp_hash`), never the stamp. Every request checks that the user is active and the fingerprint still matches (`ActiveUserTokenValidation`, cached `Users:ActiveStatusCacheSeconds`). Suspension and sign-out rotate the stamp, so a token issued before them is refused on its next request, also after a quick reactivation (#240) ([user-administration.md](user-administration.md), Revocation).
- **Refresh token:** an HttpOnly, `Secure` (`Auth:RefreshTokenCookieSecure`, on by default), `SameSite=Strict` cookie on path `/api/auth`, valid `CoreJwt:RefreshTokenExpirationDays` (7). Every refresh rotates it.
- **Reuse detection (#137).** The API stores a SHA-256 hash of every refresh token it rotates or issues (`IssuedRefreshTokens`), grouped by sign-in (the family). When a rotated token is sent again more than `Auth:RefreshTokenReuseGraceSeconds` (10 s) after its rotation, the API treats it as stolen: it revokes every token of that sign-in and answers 401 `REFRESH_TOKEN_REVOKED`, so both the thief and the user must sign in again. Other sign-ins of the same user are untouched. Within the grace window a replay gets a new token in the same family, so two tabs refreshing together do not sign the user out. A refresh token from a sign-in is recorded on its first refresh; the hash is unique and that first record is an insert-if-absent, so two concurrent first refreshes of one sign-in join one family.
- **Sign-out** rotates the security stamp, so it ends every refresh and access token of the user on every device.
- **CSRF:** no CORS and a `SameSite=Strict` cookie that only `/api/auth` receives; every other endpoint needs the bearer token, which a cross-site page cannot read.
- **Lockout:** 5 failed passwords lock an account for 15 minutes.

## 9. Outbound calls and SSRF

No outbound URL comes from user input. Every HTTP client is a typed client whose base address is operator configuration:

| Client | Host (default) | Config |
|---|---|---|
| AI service (chat, embeddings, transcription, essay grading, math check) | `http://ai:8000` (compose) | `AiService:BaseUrl` |
| Paymob | `https://accept.paymob.com` | `Payments:Paymob:BaseUrl` |
| WhatsApp OTP (Meta) | `https://graph.facebook.com` | `OtpDelivery:WhatsApp:BaseUrl` |
| Email OTP and invitations (Resend) | `https://api.resend.com` | `OtpDelivery:Email:BaseUrl` |
| SMS OTP (disabled) | none | `OtpDelivery:Sms:Url` |
| Object storage (R2 or S3) | none | `FileStorage:S3ServiceUrl` |

The ai service calls the configured OpenAI-compatible LLM endpoint (`ELMANHG_AI_LLM_BASE_URL`, https only) and OpenAI (embeddings, Whisper) over `httpx2`, with hosts fixed by config ([ai-service.md](ai-service.md)).

## 10. Logging and audit

- Logs, traces and metrics, with their PII rules: [observability.md](observability.md).
- The append-only audit log of admin and teacher actions: [audit-log.md](audit-log.md).

## 11. OWASP ASVS 5.0 Level 1 status

| Chapter | Status | Note |
|---|---|---|
| V1 Encoding and Sanitization | Met | server and client rich-text sanitisers; parameterised SQL; bounded regex |
| V2 Validation and Business Logic | Met | FluentValidation on every command; quotas and rate limits; concurrency cap |
| V3 Web Frontend Security | Met | CSP, HSTS, anti-framing, `nosniff`, `SameSite=Strict` cookie, no CORS |
| V4 API and Web Service | Met | policy on every endpoint; reviewed anonymous list; problem+json errors without stack traces |
| V5 File Handling | Met | extension, content type and signature checks; size caps; random names; private media |
| V6 Authentication | Partial | OTP and password sign-in with lockout; the password rule is 8 characters with a digit, without a common-password check (D6) |
| V7 Session Management | Met | rotating refresh tokens with reuse detection; revocation on suspension and sign-out |
| V8 Authorization | Met | named policies, subject scoping (fails closed), owner filters returning 404 |
| V9 Self-contained Tokens | Met | signed JWT validated for issuer, audience, lifetime and key; stamp fingerprint check |
| V11 Cryptography | Met | HMAC-SHA256 for OTP and pseudonyms, HMAC-SHA512 for Paymob, constant-time comparisons, OS randomness |
| V12 Secure Communication | Partial | TLS by Caddy with automatic certificates; not yet checked on the live domain (D5) |
| V13 Configuration | Met | placeholder guard, secret scanning, step-scoped CI secrets, pinned dependencies and images |
| V14 Data Protection | Met | `no-store` on API responses, pseudonymised training data, masked IPs in logs |
| V16 Security Logging and Error Handling | Met | structured logs without secrets or PII; audit log; 4xx logged as warnings |

## 12. Known gaps

| # | Gap | Tracked in |
|---|---|---|
| D3 | Per-student rate limit on CAS math checks | [#237](https://github.com/MohamedEbrahimMohsen/elmanhg/issues/237), with #123 |
| D4 | Dependabot updates and CodeQL code scanning | repository-settings decision for the admin |
| D5 | Live check of HSTS and CSP on the real domain over TLS | [#205](https://github.com/MohamedEbrahimMohsen/elmanhg/issues/205) |
| D6 | A common-password check beyond 8 characters with a digit | product decision (it changes sign-up) |
| D7 | Paymob's unsigned `is_refund` / `is_void` flags | [#191](https://github.com/MohamedEbrahimMohsen/elmanhg/issues/191), refunds story |
| D8 | `mem_limit` on the `ai` compose service | [#237](https://github.com/MohamedEbrahimMohsen/elmanhg/issues/237), host sizing |
| D9 | Rate limits and the concurrency cap are per API instance | revisit at scale-out (#112 topology is one VPS) |
| D10 | Rotated and revoked refresh-token rows are kept after they expire; no purge job yet | follow-up issue |
| D11 | A request that races a suspension can cache "active" for up to `Users:ActiveStatusCacheSeconds` | [#240](https://github.com/MohamedEbrahimMohsen/elmanhg/issues/240) |

Refresh-token reuse detection (#137) and the pre-suspension access-token check (#240) were in the plan's deferred list and are done in #115.
