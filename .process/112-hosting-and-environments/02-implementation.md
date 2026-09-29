# Implementation — [E13.S1] Hosting and environments (#112)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Infrastructure/Hosting/ReverseProxyOptions.cs` | 9 | `ReverseProxy:TrustedNetworks` CIDR list, default empty |
| `api/Elmanhg.Infrastructure/Hosting/ReverseProxyOptionsValidator.cs` | 21 | `IValidateOptions`: each entry must parse as `System.Net.IPNetwork` |
| `api/Elmanhg.Api/Hosting/ReverseProxyExtensions.cs` | 21 | `UseReverseProxyForwardedHeaders()`: For + Proto only, `KnownIPNetworks` from config, loopback default kept |
| `api/Elmanhg.Api/Hosting/MigrationCommand.cs` | 22 | `--MigrateAndExit=true` host mode (`IsRequested`, `RunAsync`) |
| `api/Elmanhg.Tests/Infrastructure/Hosting/ReverseProxyOptionsValidatorTests.cs` | 40 | T1–T3 |
| `api/Elmanhg.Tests/Integration/Hosting/ForwardedHeadersTests.cs` | 65 | T4–T6 |
| `api/Elmanhg.Tests/Integration/Hosting/MigrationCommandTests.cs` | 46 | T7–T9 |
| `.dockerignore` | 14 | allowlist build context for `api/Dockerfile` |
| `api/Dockerfile` | 22 | Appendix A, verbatim |
| `web/Dockerfile` | 11 | Appendix B, verbatim |
| `web/.dockerignore` | 7 | per plan |
| `deploy/docker-compose.prod.yml` | 120 | Appendix C **without the `minio` service** (Deviation 1) |
| `deploy/Caddyfile` | 24 | Appendix D, verbatim |
| `deploy/.env.example` | 25 | Appendix E part 1 without the `MINIO_*` keys (Deviation 1) |
| `deploy/api.env.example` | 49 | Appendix E part 2 |
| `deploy/ai.env.example` | 6 | Appendix E part 3 (token identical to api.env.example) |
| `deploy/lib.sh` | 34 | Appendix F |
| `deploy/deploy.sh` | 39 | Appendix F (EXIT trap, Deviation 3; no minio wait) |
| `deploy/backup.sh` | 24 | Appendix F |
| `deploy/restore.sh` | 45 | Appendix F (drill + gated live restore) |
| `deploy/smoke-test.sh` | 86 | Appendix F (HTTP checks reworked, Deviation 4; `SMOKE_PROJECT_NAME`, Deviation 5; profile `ai` only) |
| `.github/workflows/images.yml` | 75 | Appendix G (actions SHA-pinned, Deviation 2) |
| `.github/workflows/deploy.yml` | 64 | Appendix G (actions SHA-pinned) |
| `docs/deployment.md` | 279 | Appendix H sections 1–13, including the observed failed-migration behaviour |

The 5 scripts are staged (`git add`) with mode `100755` in the index (`git update-index --chmod=+x`), and all are LF. Nothing is committed. `git ls-files` for `deploy/` will list exactly the 10 committed files once the rest is added.

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Api/Program.cs` | `#region DEPLOY-TIME MIGRATION` before SEED; `UseReverseProxyForwardedHeaders()` as the first middleware; localization comment reworded; `using Elmanhg.Api.Hosting;` |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | Binds `ReverseProxyOptions` with `ValidateOnStart` and registers the validator |
| `api/Elmanhg.Infrastructure/Payments/PaymentsOptions.cs` | `AllowFakePayments` (default false) with its comment |
| `api/Elmanhg.Infrastructure/Payments/FakePaymentGateway.cs` | `IsAllowed` gate on SupportsSimulatedCompletion, checkout and refund |
| `api/Elmanhg.Api/appsettings.example.json` | `Payments.AllowFakePayments: false`; `"ReverseProxy": { "TrustedNetworks": [] }` |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `Payments:AllowFakePayments = true` |
| `api/Elmanhg.Tests/Infrastructure/Payments/PaymentsTestSettings.cs` | Maps `AllowFakePayments` |
| `api/Elmanhg.Tests/Infrastructure/Payments/FakePaymentGatewayTests.cs` | `Gateway(bool allowFakePayments = false)`; `_Testing_IsTrue` replaced by T12; T11, T13–T17 added |
| `.env.example` | `# Payments__AllowFakePayments=…` line and the production pointer line. The MinIO block is left out (Deviation 1). |
| `.gitignore` | `/deploy/api.env`, `/deploy/ai.env`, `/deploy/backups/`, `/deploy/.smoke/` |
| `.gitattributes` | `*.sh`, `deploy/**`, `**/Dockerfile`, `.dockerignore` → LF |
| `README.md` | docs row, `deploy/` folder row, `## Deploy` section |
| `docs/paymob.md` | §1 Fake row, §2 `AllowFakePayments` row, §6 "Environment lock", §7 step 6, §9 fake sentence |
| `docs/subscriptions.md` | lines 95 and 133 per plan |
| `docs/otp-delivery.md` | §9 runbook sentence per plan |
| `docs/ai-service.md` | `ELMANHG_AI_ENV` note; staging/production paragraph in "Run locally" |
| `docs/constitution.md` | §2 "Configuration is never committed" sentence appended |
| `docs/PRD.md` | §18 Hosting bullet (MinIO wording adjusted, Deviation 1) |

`docker-compose.yml` is **not** modified: its only planned change was the MinIO service (Deviation 1).

## Deviations
| # | Plan said | Reality | What I did |
|---|---|---|---|
| 1 | D16: pin `minio/minio:RELEASE.2025-09-07T16-13-09Z`; if it does not pull, use the newest `RELEASE.*` tag that pulls. MinIO goes in the dev and prod compose under profile `storage`. | Every official MinIO image repository is gone. `docker manifest inspect minio/minio:<any tag>` gives "denied / authentication required", and the Docker Hub API returns `object not found` for `minio/minio` (while it returns 200 for `library/caddy`). `quay.io/minio/minio` has no such manifest, even for `latest`, and `ghcr.io/minio/minio` is denied. The only maintained images are third-party forks (for example `pgsty/minio:RELEASE.2026-08-04T00-00-00Z`) or Bitnami legacy. The dependency policy (§1, §2 "publisher is the expected project") forbids adding a publisher the plan does not name. | **BLOCKED: needs a dependency decision for S3-compatible storage.** I left out the `minio` service and volume from both compose files, the `MINIO_*` keys from `deploy/.env.example` and root `.env.example`, the minio wait in `deploy.sh` and `smoke-test.sh`, and the `storage` profile. Smoke uses `COMPOSE_PROFILES=ai`. The docs (deployment §1, §13; PRD §18) say that object storage joins with #96 and that the image choice is open. Options for the dev: the `pgsty/minio` fork (AGPL-3.0, community-maintained), Bitnami legacy, or another S3 server (Garage, SeaweedFS). |
| 2 | Appendix G: `actions/checkout@v4`, `docker/*@v3/v5/v6` | The dependency policy (§5) requires GitHub Actions pinned to a full commit SHA. | Every tag was verified with `git ls-remote`, and each commit is more than 7 days old. The pins, each with a version comment: checkout `11d5960…` v4.4.0 (2026-07-16); setup-buildx `8d2750c…` v3.12.0 (2025-12-16); login `c94ce9f…` v3.7.0 (2026-01-27); metadata `c299e40…` v5.10.0 (2025-11-27); build-push `10e90e3…` v6.19.2 (2026-02-12). The existing workflows still use tags. |
| 3 | `deploy.sh` step 3: `trap '…' ERR` | `fail()` and `wait_healthy()` leave through `exit 1`, which never fires ERR, so a failed health wait would print no rollback hint. | `trap on_exit EXIT`, where `on_exit` prints the same message, `compose ps -a` and the logs when the status is not 0. The reason is in a one-line comment. |
| 4 | smoke step 7: `curl -o /dev/null …` and `curl … \| grep -q …` | The first smoke run failed. `lib.sh` exports `MSYS_NO_PATHCONV=1`, so Git Bash passed `/dev/null` unconverted to the native `curl.exe`, which failed with `curl: (23) client returned ERROR on write of 11 bytes`. Also, `grep -q` can SIGPIPE curl under `pipefail`. | The bodies and headers are read into variables and checked with `[[ … ]]` or with `grep <<< … > /dev/null`. The checks are the same (root element on `/` and `/login`, `/api` JSON content type, `no-cache` shell, `immutable` asset), and each has its own failure message. |
| 5 | smoke: `COMPOSE_PROJECT_NAME=elmanhg-smoke` | Parallel lanes share one Docker and need distinct project names. | `SMOKE_PROJECT_NAME` knob (default `elmanhg-smoke`), documented in deployment §12. |
| 6 | `deploy.yml` job env lists 6 variables | The "Check deploy secrets" error has to name the environment, and inputs may reach `run:` only through `env:`. | Added `TARGET_ENVIRONMENT: ${{ inputs.environment }}` to the job env. |
| 7 | V2: "with the current root `.env`" | This worktree has no root `.env`, and `docker-compose.yml` is unchanged (Deviation 1). | Ran `docker compose --env-file .env.example config -q` instead. |

## Build & test
- Image and action checks:
  - `docker manifest inspect` passes for `caddy:2.10-alpine`, `mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/aspnet:10.0`, `node:24-slim`, `pgvector/pgvector:pg17` and `python:3.13.15-slim`. MinIO fails (Deviation 1).
  - All action tags are resolved and dated (Deviation 2).
- `dotnet build api/ -c Release`: 0 warnings, 0 errors. The first attempt hit CS0104, an ambiguous `IPNetwork`; I fixed it with `System.Net.IPNetwork`, as the plan wrote it.
- **V7** `dotnet test api/ -c Release` with `appsettings.json` moved aside (restored afterwards): **total 2638, failed 0, succeeded 2638**. The baseline is 2621. The +17 new tests are validator 5 (T3 is a 3-row theory), forwarded headers 3, migration 3 and fake gateway +6 (12, up from 6: T12 replaces `_Testing_IsTrue`, plus T11, T13–T17).
- **V8** `git status --porcelain api/openapi` after the build: empty.
- Mutation checks (each one restored):
  1. Remove `app.UseReverseProxyForwardedHeaders()`: T4 fails.
  2. Replace the `KnownIPNetworks.Add` with a no-op: T4 fails.
  3. Set `IsAllowed => !IsProduction()`: T13, T15 and T17 fail (9/12 pass).
  4. Remove `Payments:AllowFakePayments` from `ApiFactory`: 5 of 6 `FakePaymentCompletionEndpointTests` fail.
- **V1** prod compose `config -q` with the example env files: exit 0. The `x-api-image` scalar anchor resolves.
- **V2** dev compose `config -q` (`--env-file .env.example`): exit 0.
- **V3**: `local/elmanhg-api:smoke` built. `/app/appsettings.json` is byte-identical to `appsettings.example.json` (`cmp`). The image runs as uid 1654 (app) and has curl 8.5.0. The aspnet image is Debian-based, so `apt-get` works.
- **V4** `bash deploy/smoke-test.sh` (with `SMOKE_PROJECT_NAME=elmanhg-smoke-112`, `SMOKE_HTTP_PORT=18088`, `SMOKE_HTTPS_PORT=18443`, `SMOKE_DOCKER_SUBNET=172.30.212.0/24`):
  - Run 1 built all three images (api, web with `npm ci` and `tsc -b && vite build`, ai), and postgres, api, web and ai were healthy. It then failed at the `/api` check (Deviation 4).
  - Run 2 (`SMOKE_SKIP_BUILD=1`, same images) printed `Smoke test passed`: migrate exit 0; the second `compose run --rm migrate` gave "Applying 0 pending migrations"; backup produced; `Restore drill passed: 24 migrations in elmanhg_restore_drill`.
  - Teardown left no containers, volumes or networks.
- Failed-migration experiment (for docs §8): I used a throwaway `local/elmanhg-api:broken` image (entrypoint `exit 3`) and a copy of the web image. `compose up -d` exited 1 with `service "migrate" didn't complete successfully: exit 3`. Compose had already recreated `api` and `web`, which stayed in `Created`, so HTTP failed to connect: **the site goes down until rollback**. This is documented. The images and stack were removed afterwards.
- **V5** actionlint: no findings for `images.yml` or `deploy.yml`. It reports one existing SC2034 in `ai-ci.yml:42`, which this story does not touch.
- **V6** `bash -n` passes on all 5 scripts, and `koalaman/shellcheck:stable` exits 0. SC1091 is disabled inline next to `source ./lib.sh`, and SC2016 on the single-quoted `pg_dump` command.
- **V9**: `git check-ignore` ignores all five paths.
- web: no source file changed (only `web/Dockerfile` and `web/.dockerignore`). The production build (`tsc -b` + `vite build`) ran green inside the image build. I did not run lint or vitest, because nothing they cover changed. ai: nothing changed, and its image built.

## Notes for review
- **The failed migration takes the site down** (observed above). The plan assumed the old api might keep serving. A later improvement could run `compose run --rm migrate` before `up -d` in `deploy.sh`, so a failure leaves the old containers untouched. I did not do this, because it changes the planned deploy shape.
- **MinIO (Deviation 1)** needs a dev decision before #96. It is also the reason `docker-compose.yml` is unchanged.
- `images.yml` uses `github.repository_owner` (mixed case). `docker/metadata-action` lowercases image names; the compose default registry is already lowercase.
- The image logs the existing MediatR "Lucky Penny license" warning at startup. It predates this story.
- `deploy/.env.example` `COMPOSE_PROFILES` comment reads "Starts the AI service; leave empty to run without it", replacing the plan's ",storage" hint (Deviation 1).
- The `api.env.example` group headers name their owning docs. Identity, seed and logging point to `docs/deployment.md` sections, since no other doc owns them.
