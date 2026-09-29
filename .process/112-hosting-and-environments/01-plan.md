# Plan — [E13.S1] Hosting and environments (#112)

## Goal
After this ships, the team can run Elmanhg on any Docker-capable VPS as a staging or production environment from one Compose file. Caddy terminates TLS, serves the SPA and proxies `/api/*`. CI builds the api, web and ai images, smoke-tests the whole stack, and pushes the images to GHCR. Each deploy runs database migrations before the API starts and takes a backup first. Backups can be restored, and CI rehearses the restore as a drill. `docs/deployment.md` documents every environment variable and every procedure. Per-IP rate limits see the real client IP behind Caddy (#137, #197). The fake payment gateway no longer grants free plans on a non-Production host unless an explicit switch allows it (#189).

## Scope
**In:**
- Sub-task 1, "Choose hosting": one VPS per environment running Docker Compose. The services are Caddy+web, api, a one-shot migrate, postgres (pgvector:pg17), ai (profile `ai`) and MinIO (profile `storage`). Images live on GHCR.
- Sub-task 2, "IaC + secrets": `deploy/docker-compose.prod.yml`, `deploy/Caddyfile`, and three per-host env files (`.env`, `api.env`, `ai.env`). The committed `*.example` files hold the shapes. Secrets are never committed.
- Sub-task 3, "Deploy pipelines": `.github/workflows/images.yml` smoke-tests the stack on PRs, and on every push to main it builds and pushes the api/web/ai images. `.github/workflows/deploy.yml` is a manual (`workflow_dispatch`) SSH deploy per environment that calls `deploy/deploy.sh` on the host.
- Sub-task 4, "Backups + restore drill": `deploy/backup.sh` does a pg_dump plus a media tarball with retention. `deploy/restore.sh` supports a drill into a scratch database with migration-count verification, and a live restore gated by confirmation. CI runs the drill inside the smoke test.
- API wiring:
  - forwarded headers (`ReverseProxy:TrustedNetworks`)
  - a migrate-and-exit host mode (`--MigrateAndExit=true`)
  - the `Payments:AllowFakePayments` switch
- Dev compose gets MinIO under profile `storage`.
- Docs: new `docs/deployment.md`, plus sync edits to paymob, subscriptions, otp-delivery, ai-service, constitution, PRD §18 and README.

**Out:**
- MinIO buckets and application use (#96).
- Security headers (HSTS, CSP) and hardening of the request-log client IP (#115).
- Structured logs, metrics, alerting and uptime checks (#113).
- Performance tuning (#114).
- No feature code outside Payments.

**Deferred:**
1. **First live deploy.** This covers running `deploy.yml`, getting a real Let's Encrypt certificate, setting up DNS, and getting GHCR pull credentials on a host. There is no VPS, domain or SSH key yet. The workflow fails fast with a clear error until the secrets exist.
2. **Off-site backup copy** (backups currently stay on the VPS disk). It needs an off-host bucket plus credentials, and the provider is a dev choice. The MinIO in the same compose is on the same host, so it cannot serve as off-site storage. The runbook states the gap.
3. **Staging OTP.** Students cannot sign in to a staging host with the fake OTP, because `FakeOtpChannel` logs codes in Development only. The dev must set real Resend keys for staging (`docs/otp-delivery.md` §6). This needs credentials, so no code changes. The runbook states it.

## Decisions
| # | Question | Decision | Why |
|---|---|---|---|
| D1 | Where does prod infra live | New top-level `deploy/` folder: compose file, Caddyfile, env examples, scripts. Only this folder is copied to the host (`/opt/elmanhg`). | The host needs no source tree. The images carry the code. |
| D2 | Separate prod compose or override of `docker-compose.yml` | Separate, self-contained `deploy/docker-compose.prod.yml`. The root `docker-compose.yml` stays the dev file. It only gains MinIO. | Overrides would force the dev file onto the host and mix published dev ports into prod. |
| D3 | How web is served | The `elmanhg-web` image **is** the edge. It is `caddy:2.10-alpine` with `web/dist` in `/srv`. Caddy serves the SPA with an index.html fallback and reverse-proxies `/api/*` to `api:8080`. The Caddyfile is bind-mounted from `deploy/`. | The API has no CORS and the refresh cookie is `SameSite=Strict`, so web and api must share one origin. One container handles TLS, static files and the proxy. |
| D4 | Environments | dev = local (`docker compose up -d postgres` + `dotnet run` + `vite`, unchanged). staging and prod = the same prod compose on **separate hosts**, with different `.env` values. staging uses `ASPNETCORE_ENVIRONMENT=Staging`; prod uses `Production`. Both use `ELMANHG_AI_ENV=production`. | Caddy binds 80/443, so there is one stack per host. The ai settings only accept development/testing/production. |
| D5 | Config and secret flow | Three uncommitted host files next to the compose file. `.env` holds compose interpolation: image tag, site, postgres, minio, profiles and subnet. `api.env` holds every `Section__Key` of the API. `ai.env` holds every `ELMANHG_AI_*`. Precedence inside the api container is compose `environment:` (computed values), then `api.env`, then the baked `appsettings.json`. | Least privilege: api never sees the MinIO or ai secrets, and ai never sees the JWT key. Matches constitution §0.4 and §2 (env vars, `Section__Key`). |
| D6 | appsettings in the image | `api/Dockerfile` copies the committed `appsettings.example.json` to `/app/appsettings.json`. A root `.dockerignore` allowlist keeps the developer's gitignored `appsettings*.json`, `.env` and `bin/obj` out of the build context. | The example holds the non-secret shapes that every options section needs. Program already does this for the build-time OpenAPI run. |
| D7 | Migrations at deploy time | New host mode: `dotnet Elmanhg.Api.dll --MigrateAndExit=true` runs `Database.MigrateAsync()` and exits before the seed and the pipeline. It runs as the compose one-shot service `migrate`, using the api image. `api` has `depends_on: migrate: service_completed_successfully`, so every `up -d` migrates first. Migrations are forward-only; rollback means restoring the pre-deploy backup plus the previous tag. | One image and no extra tooling. It is the same EF runtime and Npgsql retry strategy the app uses. EF bundles or idempotent SQL would add a second artefact, and the SQL script has never been applied in CI. |
| D8 | Migrate flag shape | Configuration key `MigrateAndExit` (bool), read with `IConfiguration.GetValue<bool>`, passed as `--MigrateAndExit=true`. | The `key=value` form is unambiguous for the command-line configuration provider. It needs no options class, because it is a host mode, not a tunable. |
| D9 | Forwarded headers | New `ReverseProxyOptions` (`ReverseProxy:TrustedNetworks`, a CIDR list, default empty) in Infrastructure, with an `IValidateOptions` validator. `UseReverseProxyForwardedHeaders()` is the **first** middleware. It forwards `XForwardedFor | XForwardedProto` only, adds each network to `KnownIPNetworks`, and keeps the default loopback proxy and `ForwardLimit = 1`. Compose pins the network subnet (`DOCKER_SUBNET`, default `172.30.0.0/24`) and passes it as `ReverseProxy__TrustedNetworks__0`. | Closes #137 and #197: the rate limiters partition on `RemoteIpAddress`. Only the pinned docker subnet is trusted, so an internet client cannot spoof X-Forwarded-For. Caddy (with no `trusted_proxies`) overwrites the client's X-Forwarded-For. `X-Forwarded-Host` is not trusted. Follows the FileStorage precedent: options in Infrastructure, app extension in `Elmanhg.Api`. |
| D10 | #189 fake-payments env lock | `PaymentsOptions.AllowFakePayments` (bool, default false). The fake is allowed when `!IsProduction() && (IsDevelopment() || AllowFakePayments)`. Production always refuses, even with the switch on. `ApiFactory` sets it to `true` (the Testing environment). | Staging with `Provider=Fake` no longer hands out plans unless the switch is set explicitly. The Production lock is unchanged. Local dev (Development) works with no config. |
| D11 | Image tags | On every push to main (no path filter), all three images are pushed with the tags `sha-<7>` and `main`. `deploy.sh` accepts only `^(sha-[0-9a-f]{7,40}|main)$`. Runbook: deploy sha tags, never `main`, so rollback is exact. | Every main commit then has a full, consistent set of images. |
| D12 | Architecture | linux/amd64 only. | This matches the CI runners and the usual VPS. arm64 is not needed yet. |
| D13 | Health checks | postgres uses `pg_isready` (existing). api uses `curl -fsS http://127.0.0.1:8080/health`; the api image installs `curl`. ai uses the existing `/health/ready` probe. web (Caddy) has an internal `:2080/healthz` site, checked with busybox `wget`. MinIO uses `mc ready local`. The API `/health` is **not** proxied publicly. | Each container's health is visible to `docker compose ps` and to the deploy script. Only `/api/*` and the SPA are public. |
| D14 | Deploy workflow shape | `workflow_dispatch` with inputs `environment` (staging\|production) and `image_tag`. It uses GitHub Environments for secrets and approvals: `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS`, `DEPLOY_PATH`. It uses plain `ssh`/`scp`, no third-party action. Inputs reach `run:` only through `env:`. | Dev decision "no live deploy", same as a real adapter switched on by config: it is built, then activated when secrets exist. The `env:` rule prevents script injection. |
| D15 | Backup scope | Postgres custom-format dump plus a `media` tarball from the api volume, into `BACKUP_DIR` (default `deploy/backups`). Retention is `BACKUP_RETENTION_DAYS` (default 14). Each dump is verified with `pg_restore --list`. `deploy.sh` takes a backup before every deploy. Nightly cron is described in the runbook. | Media lives in local FileStorage until #96. |
| D16 | MinIO image | Pin `minio/minio:RELEASE.2025-09-07T16-13-09Z` in both compose files. If `docker pull` fails for that tag, use the newest `RELEASE.*` tag that pulls, and record it in `02-implementation.md`. | MinIO stopped publishing community images after late 2025, so a tag must be pinned. |
| D17 | Zero-downtime | Not attempted. `up -d` restarts the api for a few seconds. | 99.5% availability (PRD §14) allows it. Blue/green would need a second api and upstream switching. |
| D18 | Dev compose MinIO password | `${MINIO_ROOT_PASSWORD:-}`, with no required-marker. | A `:?` marker would break `docker compose up -d postgres` for existing `.env` files. MinIO itself refuses an empty password when the profile is used. |
| D19 | Git Bash on Windows | `deploy/lib.sh` exports `MSYS_NO_PATHCONV=1`. All paths in the scripts are relative to `deploy/`. | Otherwise Git Bash rewrites `/app/App_Data` in `docker exec` arguments, and the implementer runs the smoke test locally. |

Morabh reuse: Morabh has no Dockerfile, compose, forwarded-headers or migration runner. Its only workflow (`.github/workflows/main_morabh.yml`) is a commented-out Azure Web App deploy. Everything here is **new — no Morabh equivalent**.

## Existing code touched
| File | Change |
|---|---|
| `api/Elmanhg.Api/Program.cs` | (a) After `var app = builder.Build();` and **before** `#region SEED`, add `#region DEPLOY-TIME MIGRATION`: `if (MigrationCommand.IsRequested(app.Configuration)) { try { await MigrationCommand.RunAsync(app.Services, CancellationToken.None); } finally { await Serilog.Log.CloseAndFlushAsync(); } return; }`, with the one-line comment "The compose `migrate` service runs `--MigrateAndExit=true`: apply pending migrations, then exit before the seed and the HTTP pipeline." (b) Insert `app.UseReverseProxyForwardedHeaders();` immediately before `app.UseCoreLocalization(...)`, with the comment "First: every later middleware (logging, rate limits) must see the client IP and scheme Caddy forwarded." Change the localization comment to start "Right after forwarded headers, so …". (c) Add `using Elmanhg.Api.Hosting;`. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | Next to the FileStorage options line, add `services.AddOptions<ReverseProxyOptions>().BindConfiguration(ReverseProxyOptions.SectionName).ValidateOnStart();` and `services.AddSingleton<IValidateOptions<ReverseProxyOptions>, ReverseProxyOptionsValidator>();`, plus `using Elmanhg.Infrastructure.Hosting;`. |
| `api/Elmanhg.Infrastructure/Payments/PaymentsOptions.cs` | Add `public bool AllowFakePayments { get; set; }` after `FakeCheckoutPath`, with the comment "Lets the fake gateway run outside Development (a staging host without Paymob keys). Production always refuses." |
| `api/Elmanhg.Infrastructure/Payments/FakePaymentGateway.cs` | Add `private bool IsAllowed => !hostEnvironment.IsProduction() && (hostEnvironment.IsDevelopment() || paymentsOptions.Value.AllowFakePayments);`. Set `SupportsSimulatedCompletion => IsAllowed;`. Both `if (hostEnvironment.IsProduction())` guards become `if (!IsAllowed)`. The same exception applies (`ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable)`). |
| `api/Elmanhg.Api/appsettings.example.json` | Payments: add `"AllowFakePayments": false` after `FakeCheckoutPath`. Add a top-level `"ReverseProxy": { "TrustedNetworks": [] }` after `FileStorage`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["Payments:AllowFakePayments"] = "true",` after `Payments:FakeCheckoutPath`. |
| `api/Elmanhg.Tests/Infrastructure/Payments/PaymentsTestSettings.cs` | In `ToConfiguration`, add `["Payments:AllowFakePayments"] = options.AllowFakePayments.ToString(CultureInfo.InvariantCulture),`. |
| `api/Elmanhg.Tests/Infrastructure/Payments/FakePaymentGatewayTests.cs` | Change the helper to `private FakePaymentGateway Gateway(bool allowFakePayments = false)`, which builds `PaymentsTestSettings.Fake()` with `AllowFakePayments = allowFakePayments`. Rename and add tests per the Test plan. |
| `docker-compose.yml` | Add a `minio` service (profile `storage`, spec below) and the volume `elmanhg-minio-data`. |
| `.env.example` (root) | Append comment block `# MinIO (dev compose profile "storage"; used from #96)` with `MINIO_ROOT_USER=elmanhg`, `MINIO_ROOT_PASSWORD=change-me-local-minio`, `# MINIO_PORT=9000`, `# MINIO_CONSOLE_PORT=9001`. Under the Payments block add `# Payments__AllowFakePayments=false   # non-Development hosts only; Production always refuses the fake`. Add the line `# Production and staging: see docs/deployment.md (deploy/*.env.example).` |
| `.gitignore` | Add `/deploy/api.env`, `/deploy/ai.env`, `/deploy/backups/` and `/deploy/.smoke/` under "Secrets and personal overrides". `deploy/.env` is already covered by `.env`. |
| `.gitattributes` | Add `*.sh text eol=lf`, `deploy/** text eol=lf`, `**/Dockerfile text eol=lf` and `.dockerignore text eol=lf`. |
| `README.md` | Docs table: add a row for `docs/deployment.md` ("Hosting, environments, config and secrets, deploys, migrations, backups"). Folders table: add a row for `deploy/` ("Production/staging Docker Compose, Caddyfile, env examples, deploy/backup/restore/smoke scripts"). Add a section `## Deploy` with one paragraph: images come from CI (GHCR), the stack is `deploy/docker-compose.prod.yml`, see docs/deployment.md, and `bash deploy/smoke-test.sh` runs the whole stack locally. |
| `docs/paymob.md` | §1 table, `Fake` row "Use": "Development, tests, CI and the build-time OpenAPI run; a non-Production host (staging) only with `AllowFakePayments=true`. No keys needed." §2 table: new row after `FakeCheckoutPath`: `` `AllowFakePayments` `` \| `false` \| no \| "Lets the fake run outside Development (staging without Paymob keys). Ignored in Production." §6: replace the "**Production lock:**" paragraph with "**Environment lock:** the fake works in Development, and in any other non-Production environment only when `Payments:AllowFakePayments=true` (the test host sets it). Otherwise it refuses checkout (503 `PAYMENT_GATEWAY_UNAVAILABLE`) and fake completion (404 `FAKE_CHECKOUT_UNAVAILABLE`), and in `Production` it always refuses, so a misconfigured server can never grant free plans. With `Provider=Paymob`, fake completion is always 404." §9 "The fake." sentence: "…Where the fake is locked (section 6) it refuses with 503…". §7: append step 6, "Deployment: set these in the host's `api.env` (docs/deployment.md)." |
| `docs/subscriptions.md` | Line 95: replace "In Production the fake refuses checkout (503) and fake completion (404 `FAKE_CHECKOUT_UNAVAILABLE`)" with "Outside Development the fake refuses checkout (503) and fake completion (404 `FAKE_CHECKOUT_UNAVAILABLE`) unless `Payments:AllowFakePayments=true`; Production always refuses (docs/paymob.md §6)". Line 133: "is locked in Production (503)" becomes "is locked like fake checkout (503, docs/paymob.md §6)". |
| `docs/otp-delivery.md` | Replace the §9 sentence "The production runbook (#112) must link this page." with "On a deployed host these go in `api.env`; the runbook is [docs/deployment.md](deployment.md). A staging host needs at least one real channel (Email through Resend is enough), because the fake logs codes in Development only." |
| `docs/ai-service.md` | `ELMANHG_AI_ENV` row notes: append "Staging and production hosts both use `production`." At the end of "Run locally", add the paragraph "Staging and production: the `ai` compose profile in `deploy/docker-compose.prod.yml`, variables in the host's `ai.env`; see [docs/deployment.md](deployment.md)." |
| `docs/constitution.md` | §2 "Configuration is never committed" bullet: append the sentence "Deployed images bake the committed `appsettings.example.json` as `appsettings.json` (shapes only); each host supplies secrets and per-host values through uncommitted env files (`deploy/.env`, `api.env`, `ai.env`) as described in `docs/deployment.md`." |
| `docs/PRD.md` | §18: add the bullet `- **Hosting**: Docker Compose on one VPS per environment (staging, production); Caddy (TLS, SPA, /api proxy), images built by CI and pushed to GHCR; PostgreSQL + pgvector, MinIO for S3-compatible storage. Runbook: docs/deployment.md.` |

## Files to create
| # | Path | Type | Contract |
|---|---|---|---|
| 1 | `api/Elmanhg.Infrastructure/Hosting/ReverseProxyOptions.cs` | options | `namespace Elmanhg.Infrastructure.Hosting; public sealed class ReverseProxyOptions { public const string SectionName = "ReverseProxy"; public List<string> TrustedNetworks { get; set; } = []; }`. Add the XML-free comment above `TrustedNetworks`: "CIDR networks whose X-Forwarded-For / X-Forwarded-Proto are trusted (the compose network Caddy sits in). Loopback is always trusted." |
| 2 | `api/Elmanhg.Infrastructure/Hosting/ReverseProxyOptionsValidator.cs` | validator | `public sealed class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>`. `Validate(string? name, ReverseProxyOptions options)`: for each index `i`, if `!System.Net.IPNetwork.TryParse(options.TrustedNetworks[i], out _)` then add ``$"ReverseProxy:TrustedNetworks:{i} '{options.TrustedNetworks[i]}' is not a CIDR network such as 172.30.0.0/24."``. Return Success when there are no failures, otherwise `Fail(failures)`. Mirror the shape of `PaymentsOptionsValidator` (a `List<string> failures`). |
| 3 | `api/Elmanhg.Api/Hosting/ReverseProxyExtensions.cs` | app extension | `namespace Elmanhg.Api.Hosting; public static class ReverseProxyExtensions { public static WebApplication UseReverseProxyForwardedHeaders(this WebApplication app) }`. Body: read `IOptions<ReverseProxyOptions>.Value`. `var forwardedHeaders = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor \| ForwardedHeaders.XForwardedProto };`. `foreach (var network in options.TrustedNetworks) { forwardedHeaders.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network)); }`. `app.UseForwardedHeaders(forwardedHeaders); return app;`. Use `KnownIPNetworks`, never the obsolete `KnownNetworks`: warnings are errors. |
| 4 | `api/Elmanhg.Api/Hosting/MigrationCommand.cs` | host mode | `namespace Elmanhg.Api.Hosting; public static class MigrationCommand`. Members: `public const string ConfigurationKey = "MigrateAndExit";` and `public static bool IsRequested(IConfiguration configuration) => configuration.GetValue<bool>(ConfigurationKey);`. `public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)` steps: (1) `await using var scope = services.CreateAsyncScope();` (2) `var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;` (3) `var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigrationCommand));` (4) `var pending = (await database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();` (5) `logger.LogInformation("Applying {Count} pending migrations: {Migrations}", pending.Count, pending);` (6) `await database.MigrateAsync(cancellationToken).ConfigureAwait(false);` (7) `logger.LogInformation("Database is up to date.");`. There is no try/catch: an exception gives a non-zero exit code, which fails `migrate` and blocks `api`. |
| 5 | `api/Elmanhg.Tests/Infrastructure/Hosting/ReverseProxyOptionsValidatorTests.cs` | unit tests | See the Test plan. |
| 6 | `api/Elmanhg.Tests/Integration/Hosting/ForwardedHeadersTests.cs` | integration tests | `public sealed class ForwardedHeadersTests(ApiFactory factory)`. Private helper `CreateFactory()` = `factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:CredentialPermitLimit"] = "1", ["ReverseProxy:TrustedNetworks:0"] = "172.30.0.0/24" })))`. Private helper `Task<int> PostLoginAsync(WebApplicationFactory<Program> host, string peerAddress, string forwardedFor)` uses `host.Server.SendAsync(context => { context.Request.Method = HttpMethods.Post; context.Request.Path = "/api/auth/login/email"; context.Request.ContentType = "application/json"; context.Request.Body = new MemoryStream("{}"u8.ToArray()); context.Connection.RemoteIpAddress = IPAddress.Parse(peerAddress); context.Request.Headers["X-Forwarded-For"] = forwardedFor; }, TestContext.Current.CancellationToken)` and returns `Response.StatusCode`. Each test creates its own factory (a fresh limiter), with `await using`. |
| 7 | `api/Elmanhg.Tests/Integration/Hosting/MigrationCommandTests.cs` | tests | `public sealed class MigrationCommandTests(ApiFactory factory)`. See the Test plan. `IsRequested` tests build `new ConfigurationBuilder().AddInMemoryCollection(...)` or `.AddCommandLine(["--MigrateAndExit=true"])`. |
| 8 | `.dockerignore` (root; the build context of `api/Dockerfile`) | allowlist | Lines, in order: `*`, `!global.json`, `!api/`, `api/**/bin/`, `api/**/obj/`, `api/**/TestResults/`, `api/.vs/`, `api/Elmanhg.Tests/`, `api/Elmanhg.Api/appsettings.json`, `api/Elmanhg.Api/appsettings.Development.json`, `api/Elmanhg.Api/appsettings.Production.json`, `api/Elmanhg.Api/App_Data/`, `api/**/Logs/`. Header comment: "Build context of api/Dockerfile. Allowlist: never let a developer's appsettings.json or .env into an image." |
| 9 | `api/Dockerfile` | Dockerfile | Exactly as in **Appendix A**. |
| 10 | `web/Dockerfile` | Dockerfile | Exactly as in **Appendix B**. |
| 11 | `web/.dockerignore` | ignore | `node_modules`, `dist`, `coverage`, `.env`, `.env.*`, `!.env.example`, `*.log`. |
| 12 | `deploy/docker-compose.prod.yml` | compose | Exactly as in **Appendix C**. |
| 13 | `deploy/Caddyfile` | Caddy config | Exactly as in **Appendix D**. |
| 14 | `deploy/.env.example` | env shape | Appendix E, part 1. |
| 15 | `deploy/api.env.example` | env shape | Appendix E, part 2. |
| 16 | `deploy/ai.env.example` | env shape | Appendix E, part 3. |
| 17 | `deploy/lib.sh` | bash, sourced | Appendix F. |
| 18 | `deploy/deploy.sh` | bash | Appendix F. |
| 19 | `deploy/backup.sh` | bash | Appendix F. |
| 20 | `deploy/restore.sh` | bash | Appendix F. |
| 21 | `deploy/smoke-test.sh` | bash | Appendix F. |
| 22 | `.github/workflows/images.yml` | workflow | Appendix G. |
| 23 | `.github/workflows/deploy.yml` | workflow | Appendix G. |
| 24 | `docs/deployment.md` | runbook | Appendix H. |

All `deploy/*.sh` files start with `#!/usr/bin/env bash`. All except `lib.sh` also start with `set -euo pipefail`, `cd "$(dirname "$0")"` and `source ./lib.sh`. Mark each script executable with `git update-index --chmod=+x deploy/<name>.sh`. The docs always invoke them as `bash deploy/<name>.sh`.

## Error codes
None new. `FakePaymentGateway` reuses `ErrorCodes.PaymentGatewayUnavailable` (503), and fake completion reuses the existing `FAKE_CHECKOUT_UNAVAILABLE` (404) path through `SupportsSimulatedCompletion`. There are no resource-string changes.

## Domain behaviour
No domain changes.

## API surface
No endpoint changes. `api/openapi/v1.json`, the Orval client and Postman stay untouched; the implementer confirms `git status` shows no drift after `dotnet build`. Behaviour changes:
- Every request passes through `UseForwardedHeaders` first.
- `POST /api/subscriptions/payments/{id}/fake-completion` and fake checkout now also refuse in non-Development environments unless `Payments:AllowFakePayments=true`.
- `dotnet Elmanhg.Api.dll --MigrateAndExit=true` is a new host mode.

## Test plan

### .NET (xUnit, `Method_Scenario_Expected`, run with `appsettings.json` moved aside)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| T1 | `ReverseProxyOptionsValidatorTests` | `Validate_NoTrustedNetworks_Succeeds` | `new ReverseProxyOptions()` gives `Succeeded` true. |
| T2 | `ReverseProxyOptionsValidatorTests` | `Validate_Ipv4AndIpv6Networks_Succeeds` | `["172.30.0.0/24", "fd00::/8"]` gives `Succeeded` true. |
| T3 | `ReverseProxyOptionsValidatorTests` | `Validate_MalformedNetwork_FailsNamingEntry` (Theory: `"not-a-network"`, `"10.0.0.0/33"`, `""`) | A list `["172.30.0.0/24", value]` gives `Failures` containing an entry that starts with `ReverseProxy:TrustedNetworks:1 '`. |
| T4 | `ForwardedHeadersTests` | `Post_TrustedProxyForwardsDifferentClients_UsesSeparateRateLimitBuckets` | Peer `172.30.0.5`, XFF `203.0.113.10` then `203.0.113.20`: both status codes are `!= 429`. |
| T5 | `ForwardedHeadersTests` | `Post_TrustedProxyForwardsSameClientTwice_Returns429` | Peer `172.30.0.6`, XFF `203.0.113.30` twice: first `!= 429`, second `== 429`. |
| T6 | `ForwardedHeadersTests` | `Post_UntrustedPeerSendsForwardedFor_IgnoresHeaderAndReturns429` | Peer `198.51.100.7`, XFF `203.0.113.40` then `203.0.113.50`: second `== 429` (the header is ignored and both requests share the peer bucket). |
| T7 | `MigrationCommandTests` | `IsRequested_CommandLineFlagTrue_ReturnsTrue` | `AddCommandLine(["--MigrateAndExit=true"])` gives true. |
| T8 | `MigrationCommandTests` | `IsRequested_FlagMissing_ReturnsFalse` | An empty configuration gives false. |
| T9 | `MigrationCommandTests` | `RunAsync_MigratedDatabase_LeavesNoPendingMigrations` | `await MigrationCommand.RunAsync(factory.Services, ct)` does not throw. A fresh scope's `GetPendingMigrationsAsync()` is empty, and `GetAppliedMigrationsAsync()` count equals `Migrations` count. |
| T10 | `FakePaymentGatewayTests` | keep `StartCheckoutAsync_Development_ReturnsFakeCheckoutPathForPayment`, `StartCheckoutAsync_Production_ThrowsPaymentGatewayUnavailable`, `SupportsSimulatedCompletion_Production_IsFalse`, `RefundAsync_Development_ReturnsFakeRefundTransaction`, `RefundAsync_Production_ThrowsPaymentGatewayUnavailable` | Unchanged. |
| T11 | `FakePaymentGatewayTests` | `SupportsSimulatedCompletion_Development_IsTrue` | Development with the switch off gives true. |
| T12 | `FakePaymentGatewayTests` | `SupportsSimulatedCompletion_StagingWithAllowFakePayments_IsTrue` (replaces `_Testing_IsTrue`) | `"Staging"` with `Gateway(allowFakePayments: true)` gives true. |
| T13 | `FakePaymentGatewayTests` | `SupportsSimulatedCompletion_StagingWithoutAllowFakePayments_IsFalse` | `"Staging"` with the switch off gives false. |
| T14 | `FakePaymentGatewayTests` | `SupportsSimulatedCompletion_ProductionWithAllowFakePayments_IsFalse` | Production with the switch on gives false. |
| T15 | `FakePaymentGatewayTests` | `StartCheckoutAsync_StagingWithoutAllowFakePayments_ThrowsPaymentGatewayUnavailable` | `ServiceUnavailableCoreException` with `PaymentGatewayUnavailable`. |
| T16 | `FakePaymentGatewayTests` | `StartCheckoutAsync_StagingWithAllowFakePayments_ReturnsFakeCheckoutPathForPayment` | RedirectUrl is `/student/fake-checkout/{id}`. |
| T17 | `FakePaymentGatewayTests` | `RefundAsync_StagingWithoutAllowFakePayments_ThrowsPaymentGatewayUnavailable` | Same exception and code. |

Mutation checks (the implementer records them in 02):
- Remove `app.UseReverseProxyForwardedHeaders()`: T4 must fail.
- Drop the `KnownIPNetworks.Add`: T4 must fail.
- Change `IsAllowed` back to `!IsProduction()`: T13 and T15 must fail.
- Remove `AllowFakePayments` from `ApiFactory`: the existing fake-completion integration tests must fail. Then restore everything.

### Infra verification (the implementer runs these and pastes the outcomes into 02; CI repeats V3–V9)
| # | Step | Pass condition |
|---|---|---|
| V1 | From `deploy/`: `API_ENV_FILE=api.env.example AI_ENV_FILE=ai.env.example docker compose -f docker-compose.prod.yml --env-file .env.example config -q` (shell variables win over `--env-file` values) | Exit 0. |
| V2 | `docker compose config -q` (dev file) with the current root `.env` | Exit 0, and `docker compose up -d postgres` still works without MinIO vars. |
| V3 | `docker build -f api/Dockerfile -t local/elmanhg-api:smoke .`, then `docker run --rm --entrypoint cat local/elmanhg-api:smoke /app/appsettings.json` | Identical to `api/Elmanhg.Api/appsettings.example.json`: no secret leaked from a local `appsettings.json`. |
| V4 | `bash deploy/smoke-test.sh` (Docker Desktop, Git Bash) | Prints `Smoke test passed`. That covers: all five long-running services healthy; `migrate` exit 0 twice; SPA `/` and `/login` served; `/api/questions/servable-count` 2xx JSON; `Cache-Control` no-cache on the shell and immutable on `/assets/*`; backup produced; restore drill passed. |
| V5 | actionlint: `docker run --rm -v "$(pwd -W 2>/dev/null \|\| pwd):/repo" -w /repo rhysd/actionlint:latest -color` | No findings for `images.yml` or `deploy.yml`. |
| V6 | `bash -n deploy/*.sh`, and `docker run --rm -v …:/mnt -w /mnt koalaman/shellcheck:stable deploy/*.sh` | No errors. SC1091 (sourced file) may be disabled inline with a comment. |
| V7 | `dotnet test api/ -c Release` with `appsettings.json` moved aside | Green, count = baseline + new tests. |
| V8 | `dotnet build api/ -c Release`, then `git status --porcelain api/openapi` | Empty. |
| V9 | `git check-ignore deploy/api.env deploy/ai.env deploy/.env deploy/backups/x deploy/.smoke/x` | All ignored. `git ls-files deploy` lists only the 10 committed files. |

## Definition of done
- [ ] All 24 files in "Files to create" exist with the contracts above, and no other new files.
- [ ] Every edit in "Existing code touched" is done.
- [ ] `UseReverseProxyForwardedHeaders` is the first middleware. It trusts only the configured CIDRs plus loopback, and forwards For and Proto only.
- [ ] `ReverseProxyOptions` is bound in `AddInfrastructure` with its validator and `ValidateOnStart`. It is in `appsettings.example.json`. Leaving it empty is valid, so CI works without config.
- [ ] `--MigrateAndExit=true` migrates and exits without seeding or serving. Compose `api` waits for `migrate` to complete successfully.
- [ ] `FakePaymentGateway` is refused in Production always, and in non-Development environments unless `AllowFakePayments=true`. `ApiFactory` sets the switch.
- [ ] T1–T17 exist and pass. The mutation checks are recorded.
- [ ] V1–V9 pass locally. `images.yml` runs `deploy-smoke` on the PR and it is green.
- [ ] The api image's `appsettings.json` is byte-identical to the example (V3). No secret, `.env` or `appsettings.json` is in the diff or in any image.
- [ ] `images.yml` pushes `sha-<7>` and `main` tags for elmanhg-api, elmanhg-web and elmanhg-ai to GHCR only on push to main or dispatch on main. `packages: write` is granted only to that job.
- [ ] `deploy.yml` is `workflow_dispatch`-only. It validates the tag, reads inputs only through `env:`, fails clearly when the secrets are missing, and uses no third-party SSH action.
- [ ] `docs/deployment.md` covers the Appendix H sections. It links `docs/otp-delivery.md`, `docs/paymob.md` and `docs/ai-service.md`, and lists every `OtpDelivery__*`, `Payments__*`, `AiService__*`, `ELMANHG_AI_*`, `CoreJwt__*`, `CoreOtp__*`, `AdminSeed__*`, `ReverseProxy__*`, `FileStorage__*` and compose variable.
- [ ] The docs-sync edits are in paymob, subscriptions, otp-delivery, ai-service, constitution, PRD §18 and README. No doc says "Production-only lock" for the fake any more.
- [ ] `.gitignore` and `.gitattributes` are updated. The scripts are LF and have `+x` in the git index.

---

## Appendix A — `api/Dockerfile`
```dockerfile
# syntax=docker/dockerfile:1
# Build context: the repository root (see .dockerignore). docs/deployment.md.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# The committed OpenAPI document is generated by developer builds only, never inside the image.
ENV OpenApiGenerateDocuments=false DOTNET_NOLOGO=true DOTNET_CLI_TELEMETRY_OPTOUT=true
WORKDIR /src
COPY global.json ./
COPY api/ api/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages dotnet restore api/Elmanhg.Api/Elmanhg.Api.csproj
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages dotnet publish api/Elmanhg.Api/Elmanhg.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
# Committed non-secret shapes only; every secret and per-host value arrives as an environment variable.
COPY api/Elmanhg.Api/appsettings.example.json ./appsettings.json
RUN mkdir -p App_Data/media /home/app/.aspnet/DataProtection-Keys && chown -R "$APP_UID" App_Data /home/app/.aspnet
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Elmanhg.Api.dll"]
```
If `aspnet:10.0` turns out not to be Debian/Ubuntu-based (`apt-get` missing), keep the tag and install curl with the image's package manager; record this in 02.

## Appendix B — `web/Dockerfile`
```dockerfile
# syntax=docker/dockerfile:1
# Build context: web/. The image is the edge: Caddy serves the SPA and proxies /api (deploy/Caddyfile, bind-mounted).
FROM node:24-slim AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY . .
RUN npm run build

FROM caddy:2.10-alpine
COPY --from=build /app/dist /srv
```

## Appendix C — `deploy/docker-compose.prod.yml`
```yaml
# Staging / production stack, one environment per host (docs/deployment.md).
# Settings: .env (compose), api.env (API), ai.env (AI service) next to this file; none is committed.

x-logging: &logging
  driver: json-file
  options:
    max-size: "10m"
    max-file: "5"

x-api-image: &api-image ${IMAGE_REGISTRY:-ghcr.io/mohamedebrahimmohsen}/elmanhg-api:${IMAGE_TAG:?set IMAGE_TAG in .env}

x-api-environment: &api-environment
  ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:?set ASPNETCORE_ENVIRONMENT in .env}
  ConnectionStrings__DbConnectionString: Host=postgres;Port=5432;Database=${POSTGRES_DB:?set POSTGRES_DB in .env};Username=${POSTGRES_USER:?set POSTGRES_USER in .env};Password=${POSTGRES_PASSWORD:?set POSTGRES_PASSWORD in .env}
  ReverseProxy__TrustedNetworks__0: ${DOCKER_SUBNET:-172.30.0.0/24}
  AiService__BaseUrl: http://ai:8000
  FileStorage__Provider: Local
  FileStorage__LocalRootPath: /app/App_Data/media
  FileStorage__PublicBaseUrl: /api/media

services:
  postgres:
    image: pgvector/pgvector:pg17
    restart: unless-stopped
    environment:
      POSTGRES_DB: ${POSTGRES_DB:?set POSTGRES_DB in .env}
      POSTGRES_USER: ${POSTGRES_USER:?set POSTGRES_USER in .env}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?set POSTGRES_PASSWORD in .env}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
      interval: 5s
      timeout: 5s
      retries: 10
    logging: *logging

  migrate:
    image: *api-image
    command: ["--MigrateAndExit=true"]
    restart: "no"
    env_file:
      - ${API_ENV_FILE:-api.env}
    environment: *api-environment
    depends_on:
      postgres:
        condition: service_healthy
    logging: *logging

  api:
    image: *api-image
    restart: unless-stopped
    env_file:
      - ${API_ENV_FILE:-api.env}
    environment: *api-environment
    volumes:
      - api-media:/app/App_Data/media
      - api-data-protection:/home/app/.aspnet/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy
      migrate:
        condition: service_completed_successfully
    healthcheck:
      test: ["CMD", "curl", "-fsS", "--max-time", "3", "http://127.0.0.1:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 6
      start_period: 30s
    logging: *logging

  web:
    image: ${IMAGE_REGISTRY:-ghcr.io/mohamedebrahimmohsen}/elmanhg-web:${IMAGE_TAG:?set IMAGE_TAG in .env}
    restart: unless-stopped
    environment:
      SITE_ADDRESS: ${SITE_ADDRESS:?set SITE_ADDRESS in .env}
    ports:
      - "${HTTP_PORT:-80}:80"
      - "${HTTPS_PORT:-443}:443"
      - "${HTTPS_PORT:-443}:443/udp"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
      - caddy-config:/config
    depends_on:
      - api
    healthcheck:
      test: ["CMD", "wget", "-qO-", "http://127.0.0.1:2080/healthz"]
      interval: 10s
      timeout: 5s
      retries: 6
    logging: *logging

  ai:
    profiles: ["ai"]
    image: ${IMAGE_REGISTRY:-ghcr.io/mohamedebrahimmohsen}/elmanhg-ai:${IMAGE_TAG:?set IMAGE_TAG in .env}
    restart: unless-stopped
    env_file:
      - ${AI_ENV_FILE:-ai.env}
    environment:
      ELMANHG_AI_ENV: ${ELMANHG_AI_ENV:-production}
    healthcheck:
      test: ["CMD", "python", "-c", "import sys, urllib.request; sys.exit(0 if urllib.request.urlopen('http://127.0.0.1:8000/health/ready', timeout=3).status == 200 else 1)"]
      interval: 10s
      timeout: 5s
      retries: 6
    logging: *logging

  minio:
    profiles: ["storage"]
    image: minio/minio:RELEASE.2025-09-07T16-13-09Z
    command: ["server", "/data", "--console-address", ":9001"]
    restart: unless-stopped
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER:?set MINIO_ROOT_USER in .env}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD:?set MINIO_ROOT_PASSWORD in .env}
    volumes:
      - minio-data:/data
    healthcheck:
      test: ["CMD", "mc", "ready", "local"]
      interval: 10s
      timeout: 5s
      retries: 6
    logging: *logging

networks:
  default:
    ipam:
      config:
        - subnet: ${DOCKER_SUBNET:-172.30.0.0/24}

volumes:
  postgres-data:
  api-media:
  api-data-protection:
  caddy-data:
  caddy-config:
  minio-data:
```
If Compose rejects the `x-api-image` scalar anchor, inline the image string in `migrate` and `api` instead. Postgres, api, ai and MinIO publish **no** ports.

**Dev `docker-compose.yml` MinIO service** (append after `ai`):
```yaml
  minio:
    profiles: ["storage"]
    image: minio/minio:RELEASE.2025-09-07T16-13-09Z
    container_name: elmanhg-minio
    command: ["server", "/data", "--console-address", ":9001"]
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER:-elmanhg}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD:-}
    ports:
      - "127.0.0.1:${MINIO_PORT:-9000}:9000"
      - "127.0.0.1:${MINIO_CONSOLE_PORT:-9001}:9001"
    volumes:
      - elmanhg-minio-data:/data
    healthcheck:
      test: ["CMD", "mc", "ready", "local"]
      interval: 10s
      timeout: 5s
      retries: 6
```
Add `elmanhg-minio-data:` under `volumes:`.

## Appendix D — `deploy/Caddyfile`
```caddyfile
# Edge for one environment (docs/deployment.md). SITE_ADDRESS is the public host name
# (automatic HTTPS through Let's Encrypt), or ":80" for plain HTTP in the smoke test.
{$SITE_ADDRESS} {
	encode zstd gzip

	handle /api/* {
		reverse_proxy api:8080
	}

	handle {
		root * /srv
		@assets path /assets/*
		header @assets Cache-Control "public, max-age=31536000, immutable"
		@shell not path /assets/*
		header @shell Cache-Control "no-cache"
		try_files {path} /index.html
		file_server
	}
}

# Container-internal health endpoint for the compose health check; the port is not published.
:2080 {
	respond /healthz "ok" 200
}
```
There is no `trusted_proxies`, so Caddy replaces any client-sent X-Forwarded-For with the real peer address. Security headers are left to #115.

## Appendix E — env examples

**Part 1: `deploy/.env.example`.** Each comment sits on its own line above its key. Compose dotenv values have no trailing comments.
```
# Compose settings for ONE environment on this host. Copy to .env; never commit it (docs/deployment.md).
# elmanhg-staging on the staging host.
COMPOSE_PROJECT_NAME=elmanhg-prod
# Add ",storage" to start MinIO (used from #96).
COMPOSE_PROFILES=ai
IMAGE_REGISTRY=ghcr.io/mohamedebrahimmohsen
# deploy.sh rewrites this line to the sha-<7> tag it deploys.
IMAGE_TAG=main
# Public host name; Caddy obtains its TLS certificate automatically.
SITE_ADDRESS=elmanhg.example.com
HTTP_PORT=80
HTTPS_PORT=443
# Compose network subnet; also the API's only trusted proxy network.
DOCKER_SUBNET=172.30.0.0/24
# Staging on the staging host.
ASPNETCORE_ENVIRONMENT=Production
# production on staging too (the AI service knows development/testing/production only).
ELMANHG_AI_ENV=production
POSTGRES_DB=elmanhg
POSTGRES_USER=elmanhg
# openssl rand -hex 32 (hex only: it is embedded in the connection string).
POSTGRES_PASSWORD=change-me-openssl-rand-hex-32
MINIO_ROOT_USER=elmanhg
# openssl rand -hex 32
MINIO_ROOT_PASSWORD=change-me-openssl-rand-hex-32
# Paths are relative to this folder.
API_ENV_FILE=api.env
AI_ENV_FILE=ai.env
```

**Part 2: `deploy/api.env.example`.** Group the keys under comment headers, in this order. Each group's header comment names the doc that owns it.
- Identity: `CoreJwt__Issuer=Elmanhg`, `CoreJwt__Audience=Elmanhg.Audience`, `CoreJwt__Key=change-me-openssl-rand-base64-48`, `CoreOtp__Secret=change-me-openssl-rand-hex-32`
- Admin seed: `AdminSeed__Email=admin@example.com`, `AdminSeed__Password=change-me-Admin1`, `AdminSeed__DisplayName=Admin`
- Logging: `CoreLogging__Trace__Cluster=production`
- OTP (docs/otp-delivery.md): the same commented `OtpDelivery__*` list as the root `.env.example`, plus `# OtpDelivery__DefaultPhoneChannel=WhatsApp`
- Payments (docs/paymob.md): `Payments__Provider=Fake`, `Payments__AllowFakePayments=false`, then the commented `Payments__Paymob__*` list from the root `.env.example`, with `RedirectionUrl=https://<SITE_ADDRESS>/student/checkout-result` and `NotificationUrl=https://<SITE_ADDRESS>/api/payments/paymob/webhook` as example values
- AI (docs/ai-service.md): `AiService__Provider=Http`, `AiService__ServiceToken=change-me-local-ai-service-token-0123456789`. Add a comment: "same value as ELMANHG_AI_SERVICE_TOKEN in ai.env; AiService__BaseUrl is set by compose".
- A closing comment: "Set by compose, do not add here: ASPNETCORE_ENVIRONMENT, ConnectionStrings__DbConnectionString, ReverseProxy__TrustedNetworks__0, AiService__BaseUrl, FileStorage__*".

**Part 3: `deploy/ai.env.example`.**
```
# AI service settings (docs/ai-service.md → Configuration). ELMANHG_AI_ENV is set by compose from .env.
ELMANHG_AI_SERVICE_TOKEN=change-me-local-ai-service-token-0123456789
ELMANHG_AI_LLM_PROVIDER=fake
# ELMANHG_AI_ANTHROPIC_API_KEY=
# ELMANHG_AI_CHAT_MODEL=claude-sonnet-5
# ELMANHG_AI_LOG_LEVEL=INFO
```
The example token values must be identical in `api.env.example` and `ai.env.example`, so that the smoke test works unchanged.

## Appendix F — scripts

**`deploy/lib.sh`** (sourced; no `set -e` of its own)
- `export MSYS_NO_PATHCONV=1`
- `ENV_FILE=${ENV_FILE:-.env}`, `BACKUP_DIR=${BACKUP_DIR:-backups}`, `export ENV_FILE BACKUP_DIR`
- `compose() { docker compose -f docker-compose.prod.yml --env-file "$ENV_FILE" "$@"; }`
- `fail() { echo "error: $*" >&2; exit 1; }`
- `wait_healthy() { local service=$1 timeout=${2:-180} waited=0 id status; id=$(compose ps -q "$service"); [ -n "$id" ] || fail "$service is not running"; until status=$(docker inspect -f '{{.State.Health.Status}}' "$id") && [ "$status" = healthy ]; do [ "$waited" -ge "$timeout" ] && fail "$service not healthy after ${timeout}s (status: $status)"; sleep 3; waited=$((waited+3)); done; echo "$service healthy"; }`
- `migrate_exit_code() { docker inspect -f '{{.State.ExitCode}}' "$(compose ps -aq migrate)"; }`

**`deploy/backup.sh`** (no arguments; prints the dump path as the last line)
1. `umask 077`, then `mkdir -p "$BACKUP_DIR"`, then `stamp=$(date -u +%Y%m%dT%H%M%SZ)`, then `dump="$BACKUP_DIR/postgres-$stamp.dump"`.
2. `trap 'rm -f "$dump"' ERR`.
3. `compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom' > "$dump"`.
4. `[ -s "$dump" ] || fail "empty dump"`, then `compose exec -T postgres pg_restore --list < "$dump" > /dev/null` to check the dump is readable.
5. If `compose ps -q --status running api` is non-empty: `compose exec -T api tar -czf - -C /app/App_Data media > "$BACKUP_DIR/media-$stamp.tar.gz"`.
6. `find "$BACKUP_DIR" -maxdepth 1 -type f \( -name 'postgres-*.dump' -o -name 'media-*.tar.gz' \) -mtime +"${BACKUP_RETENTION_DAYS:-14}" -delete`.
7. `echo "$dump"`.

**`deploy/restore.sh <dump-file> <target-database>`**
1. Validate the arguments: the file exists, and the target matches `^[a-z_][a-z0-9_]{0,62}$`. Otherwise fail with usage.
2. `user=$(compose exec -T postgres printenv POSTGRES_USER)` and `live=$(compose exec -T postgres printenv POSTGRES_DB)`. Strip `\r` with `tr -d '\r'`.
3. **Live** (`target == live`): require `CONFIRM_LIVE_RESTORE=yes`, or fail with "live restore overwrites $live; rerun with CONFIRM_LIVE_RESTORE=yes". Then:
   - `compose stop api`
   - `compose exec -T postgres pg_restore -U "$user" -d "$target" --clean --if-exists --no-owner --single-transaction --exit-on-error < "$dump"`
   - `compose start api`
   - `wait_healthy api 300`
   - `echo "Restored $dump into $target"`
4. **Drill** (any other target):
   - `dropdb -U "$user" --if-exists "$target"`, then `createdb -U "$user" "$target"` (both through `compose exec -T postgres`).
   - `pg_restore -U "$user" -d "$target" --no-owner --exit-on-error < "$dump"`.
   - `count_migrations() { compose exec -T postgres psql -U "$user" -d "$1" -tAc 'SELECT count(*) FROM "__EFMigrationsHistory"' | tr -d '\r'; }`. Compare the live and target counts, and fail on a mismatch.
   - Unless `KEEP_RESTORE_DB=1`, drop the target.
   - `echo "Restore drill passed: $count migrations in $target"`.

**`deploy/deploy.sh <image-tag>`**
1. `tag=${1:?usage: deploy.sh <image-tag>}`. `[[ $tag =~ ^(sha-[0-9a-f]{7,40}|main)$ ]]` or fail.
2. `grep -q '^IMAGE_TAG=' "$ENV_FILE"` or fail. `previous=$(grep '^IMAGE_TAG=' "$ENV_FILE" | cut -d= -f2-)`.
3. `trap 'echo "Deploy of $tag failed. Roll back: bash deploy.sh $previous; if a migration ran, restore the pre-deploy dump first (docs/deployment.md)." >&2; compose ps -a; compose logs --no-color --tail 100 migrate api' ERR`.
4. `sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$tag/" "$ENV_FILE"`.
5. `compose pull`.
6. If `compose ps -q --status running postgres` is non-empty: `bash ./backup.sh`. This is the pre-deploy backup; it is skipped on the first deploy.
7. `compose up -d --remove-orphans`.
8. `wait_healthy api 300`. `[ "$(migrate_exit_code)" = 0 ]` or fail. `wait_healthy web`. If `compose ps -q ai` is non-empty, `wait_healthy ai`. If `compose ps -q minio` is non-empty, `wait_healthy minio`.
9. `docker image prune -f`, then `echo "Deployed $tag (previous: $previous)"`.

**`deploy/smoke-test.sh`** (the local and CI whole-stack test)
1. `export ENV_FILE=.smoke/.env BACKUP_DIR=.smoke/backups`, `port=${SMOKE_HTTP_PORT:-8088}`, `base="http://localhost:$port"`.
2. `rm -rf .smoke && mkdir -p .smoke`. Copy `.env.example`, `api.env.example` and `ai.env.example` to `.smoke/.env`, `.smoke/api.env` and `.smoke/ai.env`.
3. `set_env() { local file=$1 key=$2 value=$3; if grep -q "^$key=" "$file"; then sed -i "s|^$key=.*|$key=$value|" "$file"; else printf '%s=%s\n' "$key" "$value" >> "$file"; fi; }`. Apply it to `.smoke/.env`:
   - `COMPOSE_PROJECT_NAME=elmanhg-smoke`, `COMPOSE_PROFILES=ai,storage`
   - `IMAGE_REGISTRY=local`, `IMAGE_TAG=smoke`
   - `SITE_ADDRESS=:80`, `HTTP_PORT=$port`, `HTTPS_PORT=${SMOKE_HTTPS_PORT:-8443}`
   - `DOCKER_SUBNET=${SMOKE_DOCKER_SUBNET:-172.30.250.0/24}`
   - `API_ENV_FILE=.smoke/api.env`, `AI_ENV_FILE=.smoke/ai.env`
   - `POSTGRES_PASSWORD=smokepostgrespasswordnotasecret`, `MINIO_ROOT_PASSWORD=smokeminiopasswordnotasecret`
4. `trap cleanup EXIT`. `cleanup` captures `$?`. If it is non-zero, it runs `compose ps -a` and `compose logs --no-color --tail 200`. Then it runs `compose down -v --remove-orphans`, and `rm -rf .smoke` unless `SMOKE_KEEP=1`.
5. Unless `SMOKE_SKIP_BUILD=1`, build the images:
   - `docker build -f ../api/Dockerfile -t local/elmanhg-api:smoke ..`
   - `docker build -t local/elmanhg-web:smoke ../web`
   - `docker build -t local/elmanhg-ai:smoke ../ai`
6. `compose up -d`, then `wait_healthy postgres`, `wait_healthy api 300`, `wait_healthy web`, `wait_healthy ai`, `wait_healthy minio`. Then `[ "$(migrate_exit_code)" = 0 ]`.
7. HTTP checks (each fails with a message):
   - `curl -fsS "$base/" | grep -q 'id="root"'`
   - `curl -fsS "$base/login" | grep -q 'id="root"'`
   - `curl -fsS -o /dev/null -w '%{content_type}' "$base/api/questions/servable-count" | grep -q 'application/json'`
   - `curl -fsSI "$base/" | grep -qi '^cache-control: no-cache'`
   - `asset=$(curl -fsS "$base/" | grep -o '/assets/[^"]*\.js' | head -1)`, then `curl -fsSI "$base$asset" | grep -qi 'immutable'`
8. `compose run --rm migrate` must exit 0. This proves migrations are idempotent.
9. `dump=$(bash ./backup.sh | tail -1)`, then `[ -s "$dump" ]`, then `bash ./restore.sh "$dump" elmanhg_restore_drill`.
10. `echo "Smoke test passed"`.

## Appendix G — workflows

**`.github/workflows/images.yml`**
- `name: images`.
- Triggers:
  - `pull_request.paths`: `['api/**', 'web/**', 'ai/**', 'deploy/**', 'global.json', '.dockerignore', '.github/workflows/images.yml']`
  - `push.branches: [main]` with **no** paths filter
  - `workflow_dispatch`
- Top-level `permissions: contents: read`. `concurrency: { group: images-${{ github.ref }}, cancel-in-progress: true }`.
- Job `deploy-smoke` (`ubuntu-latest`, `timeout-minutes: 30`):
  - `actions/checkout@v4` with `persist-credentials: false`
  - step "Whole-stack smoke test (build, migrate, serve, backup, restore drill)": `bash deploy/smoke-test.sh`
- Job `publish`:
  - `needs: deploy-smoke`, `if: github.ref == 'refs/heads/main' && github.event_name != 'pull_request'`
  - `permissions: { contents: read, packages: write }`
  - `strategy.matrix.include`:
    - `{ image: elmanhg-api, context: ., file: api/Dockerfile }`
    - `{ image: elmanhg-web, context: web, file: web/Dockerfile }`
    - `{ image: elmanhg-ai, context: ai, file: ai/Dockerfile }`
  - Steps:
    1. checkout (as above)
    2. `docker/setup-buildx-action@v3`
    3. `docker/login-action@v3` (`registry: ghcr.io`, `username: ${{ github.actor }}`, `password: ${{ secrets.GITHUB_TOKEN }}`)
    4. `docker/metadata-action@v5`, id `meta`: `images: ghcr.io/${{ github.repository_owner }}/${{ matrix.image }}`, `tags: | type=sha,prefix=sha- \n type=raw,value=main`
    5. `docker/build-push-action@v6`: `context: ${{ matrix.context }}`, `file: ${{ matrix.file }}`, `platforms: linux/amd64`, `push: true`, `tags: ${{ steps.meta.outputs.tags }}`, `labels: ${{ steps.meta.outputs.labels }}`, `cache-from: type=gha,scope=${{ matrix.image }}`, `cache-to: type=gha,mode=max,scope=${{ matrix.image }}`

**`.github/workflows/deploy.yml`**
- `name: deploy`.
- `on.workflow_dispatch.inputs`:
  - `environment` (`type: choice`, `options: [staging, production]`, required)
  - `image_tag` (`type: string`, required, description "sha-<7> tag pushed by the images workflow")
- `permissions: contents: read`. `concurrency: { group: deploy-${{ inputs.environment }}, cancel-in-progress: false }`.
- Job `deploy`: `runs-on: ubuntu-latest`, `environment: ${{ inputs.environment }}`, `timeout-minutes: 20`. Job-level `env`:
  - `IMAGE_TAG: ${{ inputs.image_tag }}`
  - `DEPLOY_HOST: ${{ secrets.DEPLOY_HOST }}`, `DEPLOY_USER: ${{ secrets.DEPLOY_USER }}`, `DEPLOY_PATH: ${{ secrets.DEPLOY_PATH }}`
  - `DEPLOY_SSH_KEY: ${{ secrets.DEPLOY_SSH_KEY }}`, `DEPLOY_KNOWN_HOSTS: ${{ secrets.DEPLOY_KNOWN_HOSTS }}`
- Steps:
  1. Checkout with `persist-credentials: false`.
  2. "Check deploy secrets": for each of the five variables, if it is empty, print `::error::<NAME> is not set for the <environment> environment (docs/deployment.md → Deploy from GitHub)` and exit 1.
  3. "Validate image tag": `[[ "$IMAGE_TAG" =~ ^sha-[0-9a-f]{7,40}$ ]]`, or `::error::` and exit 1. Only sha tags are accepted here.
  4. "Configure SSH":
     - `install -m 700 -d ~/.ssh`
     - `printf '%s\n' "$DEPLOY_SSH_KEY" > ~/.ssh/deploy_key && chmod 600 ~/.ssh/deploy_key`
     - `printf '%s\n' "$DEPLOY_KNOWN_HOSTS" > ~/.ssh/known_hosts`
  5. "Upload deploy files": `scp -i ~/.ssh/deploy_key deploy/docker-compose.prod.yml deploy/Caddyfile deploy/lib.sh deploy/deploy.sh deploy/backup.sh deploy/restore.sh "$DEPLOY_USER@$DEPLOY_HOST:$DEPLOY_PATH/"`.
  6. "Deploy": `ssh -i ~/.ssh/deploy_key "$DEPLOY_USER@$DEPLOY_HOST" "cd '$DEPLOY_PATH' && bash deploy.sh '$IMAGE_TAG'"`.
- `${{ inputs.* }}` and `${{ secrets.* }}` never appear inside any `run:` script. They reach scripts only through `env:`.

## Appendix H — `docs/deployment.md` (sections, in order; tables over prose)
1. **Overview**: the topology table (service, image, role, public?, volume, health check) for web/Caddy, api, migrate, postgres, ai (profile `ai`), minio (profile `storage`). Traffic: browser → Caddy :443 → `/api/*` → api:8080, everything else → SPA. api → ai:8000 over the service token. Postgres and MinIO are never public.
2. **Environments**: table with dev / staging / production. Columns: where it runs, `ASPNETCORE_ENVIRONMENT`, `ELMANHG_AI_ENV`, how it deploys, providers (fakes vs real), `Payments__AllowFakePayments`. Notes:
   - Staging needs a real OTP channel (Resend email), because the fake logs codes in Development only.
   - Staging exposes nothing extra: `/scalar` is mapped but Caddy does not proxy it.
   - One environment per host.
3. **Configuration and secrets**:
   - The three host files, what goes in each, and file mode `600`.
   - Precedence: compose `environment:`, then the env file, then the baked `appsettings.json` from `appsettings.example.json`.
   - Never committed; `.gitignore` entries.
   - Generating secrets (`openssl rand -hex 32` / `-base64 48`); the Postgres password must be hex.
   - Rotation: JWT key (signs out everyone), AI token (change both files, `up -d`), DB password (`ALTER USER` first).
   - A new options section added by a story must be mirrored into `api.env` when it holds secrets. Otherwise the baked example default applies.
4. **Environment variable reference**: one table per group, with columns variable, file, required, default, doc link.
   - Compose `.env`: every key in Appendix E part 1.
   - API identity/seed: `CoreJwt__*`, `CoreOtp__Secret`, `AdminSeed__*`.
   - `OtpDelivery__*`: every key from otp-delivery §2 in env form, linking **docs/otp-delivery.md**.
   - `Payments__*`, including `AllowFakePayments`, linking **docs/paymob.md**.
   - `AiService__*`, linking **docs/ai-service.md**.
   - `ELMANHG_AI_*`: every row from ai-service Configuration, linking **docs/ai-service.md**.
   - Compose-set keys: `ConnectionStrings__DbConnectionString`, `ReverseProxy__TrustedNetworks__0`, `FileStorage__*`, `ASPNETCORE_ENVIRONMENT`.
   - Other tunables: "every other key in `appsettings.example.json` can be overridden as `Section__Key`".
5. **Images and CI**:
   - `images.yml`: the smoke job on PRs; publish on main; tags `sha-<7>` and `main`; GHCR names.
   - If a package is private, the host needs `docker login ghcr.io -u <user>` with a PAT that has `read:packages`.
6. **First-time host setup** (numbered):
   1. VPS: Ubuntu 24.04, 2 vCPU / 4 GB minimum; Docker Engine + Compose v2.24+.
   2. DNS A/AAAA record to the host; firewall 22/80/443 only.
   3. `sudo mkdir -p /opt/elmanhg && chown deploy:deploy`.
   4. Copy `deploy/` files; create `.env`, `api.env` and `ai.env` from the examples; `chmod 600`.
   5. `docker login ghcr.io`.
   6. `bash deploy.sh sha-<7>`.
   7. Sign in as the seeded admin.
   8. Install the nightly backup cron.
7. **Deploying**:
   - From GitHub: `deploy.yml`, the GitHub Environment secrets table (`DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS` from `ssh-keyscan`, `DEPLOY_PATH`), and a required reviewer on `production`.
   - Manually on the host: `bash deploy.sh <tag>`.
   - What `deploy.sh` does (Appendix F steps).
   - Not yet run live (Deferred 1).
8. **Database migrations**:
   - The `migrate` service and `--MigrateAndExit=true`; forward-only; idempotent re-runs.
   - A failed migration: `docker compose up -d` exits non-zero with "service migrate didn't complete successfully", and the new api container is not started. The implementer checks with a deliberately failing run whether the old api container keeps serving, and writes the observed behaviour here.
   - Rollback = `CONFIRM_LIVE_RESTORE=yes bash restore.sh <pre-deploy dump> <db>` + `bash deploy.sh <previous tag>`.
   - A migration must stay compatible with the previous api image only for the few seconds of restart.
9. **Backups and restore**:
   - `backup.sh` output and retention; the cron line `15 2 * * * cd /opt/elmanhg && bash backup.sh >> backups/backup.log 2>&1`.
   - Off-site copy not automated (Deferred 2): copy `backups/` off the host with the provider's tool.
   - Monthly restore drill: `bash restore.sh backups/postgres-<stamp>.dump elmanhg_restore_drill` (CI runs it on every PR too).
   - Live restore steps.
   - Media restore: `docker compose -f docker-compose.prod.yml exec -T api tar -xzf - -C /app/App_Data < backups/media-<stamp>.tar.gz`.
   - MinIO data joins backups with #96.
10. **Health checks**: a table per service (command, interval, what "healthy" means). The API `/health` is internal only. Commands: `docker compose ps`, `docker compose logs -f api`. Log rotation 10 MB × 5 per container. Monitoring and alerting come with #113.
11. **Reverse proxy and client IPs**: Caddy terminates TLS and replaces X-Forwarded-For. The API trusts `DOCKER_SUBNET` only (`ReverseProxy__TrustedNetworks__0`), so per-IP rate limits (auth, analytics) work (#137, #197). If `DOCKER_SUBNET` changes, both values move together, because compose passes the same variable.
12. **Run the production stack locally**: `bash deploy/smoke-test.sh`; env knobs `SMOKE_HTTP_PORT`, `SMOKE_KEEP`, `SMOKE_SKIP_BUILD`, `SMOKE_DOCKER_SUBNET`.
13. **Not done yet**: Deferred 1–3, plus #96 (MinIO use), #113 (observability), #114 (performance) and #115 (security headers; the request log trusts `CF-Connecting-IP`).
