VERDICT: CHANGES_REQUESTED

# Review: [E13.S1] Hosting and environments (#112)

## Blocking

### 1. A failed migration takes the site down, and a one-line ordering fix exists
**Where:** `deploy/deploy.sh:30`, `docs/deployment.md` §7 step 4 (line 215) and §8 bullet 3 (line 226)
**Rule:** plan Goal ("Each deploy runs database migrations before the API starts"), plan D7 ("an exception ... fails `migrate` and blocks `api`"), orchestrator instruction for this review
**Problem:** `compose up -d --remove-orphans` recreates `api` and `web`, stopping the old containers, before it starts `migrate`. `depends_on: migrate: service_completed_successfully` only stops the *new* api from starting. It does not keep the old one serving. The implementer saw this happen: `api` and `web` were left in `Created` and HTTP failed to connect. The implementer documented it instead of fixing it.
**Failure:** Deploy a tag whose migration throws, for example one that adds a unique index over duplicate rows. `up -d` exits 1 and the public site is down until someone runs a manual rollback.
**Fix:** In `deploy.sh`, run the migration on its own before `up`: `compose run --rm migrate` between the backup and `compose up -d --remove-orphans`. Under `set -e` and the EXIT trap, a failed migration now stops the script before `api` and `web` are touched. The `migrate` run inside `up -d` then applies 0 pending migrations, and `depends_on` stays as a second guard. Rewrite deployment.md §7 step 4 and the §8 failed-migration bullet: the old containers keep serving, and `deploy.sh <previous tag>` is only needed to reset `IMAGE_TAG`. In the §8 compatibility bullet, the old API now runs against the new schema for the whole migration, not only the restart.

### 2. The MinIO docs contradict the orchestrator's object-storage decision
**Where:** `docs/deployment.md:17` (§1), `docs/deployment.md:276` (§13 "Object storage (#96)"), `docs/PRD.md` §18 Hosting bullet (added line 545)
**Rule:** `.claude/rules/docs-sync.md` (architecture divergence). `00-acceptance.md` line 10: "object storage uses a managed S3-compatible service (Cloudflare R2 or AWS S3), set by config ... There is no self-hosted object store in compose."
**Problem:** deployment.md says "the image choice is open" in two places. PRD §18 says object storage "joins the stack with #96 (the official MinIO images are no longer published)". Both describe a pending self-hosted container. The decision is a managed service outside compose.
**Failure:** Someone reading the runbook or the PRD for #96 is told to pick a container image and add it to the stack, which the decision rules out.
**Fix:** In deployment.md §1 and §13, and in PRD §18, say: object storage (from #96) is a managed S3-compatible service (Cloudflare R2 or AWS S3), set by config. Local dev and CI use the local-disk store, and there is no object-store container. §9 "Object storage data joins the backups with #96" should say the provider's durability or versioning covers it, or leave it open for #96, but it must not imply a compose volume.

### 3. Production images are not pinned by digest
**Where:** `deploy/docker-compose.prod.yml:23` (`pgvector/pgvector:pg17`), `api/Dockerfile:3,12` (`sdk:10.0`, `aspnet:10.0`), `web/Dockerfile:3,10` (`node:24-slim`, `caddy:2.10-alpine`)
**Rule:** momenta-dependency-policy §5: "Docker images pinned to a version tag (and digest for production); never `:latest`. GitHub Actions pinned to a full commit SHA."
**Problem:** The implementer used §5 to pin actions to SHAs (Deviation 2) but left every image on a floating tag, and did not record that as a deviation. `pg17`, `10.0`, `24-slim` and `2.10-alpine` all move when upstream pushes.
**Failure:** Every `deploy.sh` runs `compose pull`, so staging and production pull whatever `pgvector/pgvector:pg17` points to that day: a different PostgreSQL minor and pgvector build than CI's smoke test ran. Rolling back the app tag does not roll the database image back. Two builds of the same commit can also get different aspnet or caddy runtime layers.
**Fix:** Pin by digest, keeping the tag for readability, for example `pgvector/pgvector:pg17@sha256:<digest>`, `mcr.microsoft.com/dotnet/aspnet:10.0@sha256:<digest>` and `caddy:2.10-alpine@sha256:<digest>`. Minimum: the prod compose image and the two runtime FROMs. The build stages (sdk, node) are preferred too. Get each digest with `docker buildx imagetools inspect <image>` and record it in 02.

### 4. cancel-in-progress on main breaks "every main commit gets images"
**Where:** `.github/workflows/images.yml:20-22`, against `docs/deployment.md:172` (§5) and plan D11
**Rule:** plan D11 ("Every main commit then has a full, consistent set of images"). Docs-sync divergence with deployment.md §5 ("On every push to main ... it pushes ... each tagged sha-<7>").
**Problem:** The concurrency group is `images-refs/heads/main` with `cancel-in-progress: true`. A second push to main cancels the first run, either during `deploy-smoke` (that sha never gets images) or partway through the `publish` matrix (api pushed, web or ai not).
**Failure:** Two lanes squash-merge within the ~15-minute smoke window, which is routine with 4 parallel lanes. `deploy.sh sha-<first>` then fails at `compose pull` with "manifest unknown" for at least one image.
**Fix:** Set `cancel-in-progress` to `${{ github.event_name == 'pull_request' }}`. PR runs are still cancelled, and main runs queue.

## Non-blocking
- `web/Dockerfile:10`: the edge image runs Caddy as root, the image default. api (uid 1654) and ai (uid 10001) are non-root. The official caddy binary already has cap_net_bind_service, so a USER line plus a chown of /data and /config would make it non-root. No house rule mandates it, and hardening is #115, but it is cheap to fold into the rework.
- `.dockerignore:4`: the api/ allowlist entry re-includes any gitignored api/**/.env or .env.local. It only reaches the build stage (publish does not copy it, and the stage is not shipped), so no image leak. An `api/**/.env*` exclusion would make `docs/deployment.md:51` literally true.
- `deploy/backup.sh:12,20`: the ERR trap deletes the verified DB dump when only the media tar fails. The `fail "empty dump"` path exits without ERR and leaves a 0-byte file. Neither loses data.
- `deploy/restore.sh:21-24`: if the live pg_restore fails, api stays stopped (the DB is unchanged thanks to --single-transaction). Starting api again on failure would be kinder. The live path is not exercised by CI; only the drill is.
- `.github/workflows/deploy.yml:28-35`: DEPLOY_SSH_KEY and the other secrets are job-level env, so every step sees them, including the checkout action. Step-level env on "Configure SSH", "Upload" and "Deploy" is tighter.
- `api/Elmanhg.Tests/Integration/Hosting/ForwardedHeadersTests.cs:50`: uses ConfigureAppConfiguration. PROGRESS Conventions say test factory config uses builder.UseSetting(...). It works here only because both values are read lazily through IOptions. The plan prescribed this shape.
- `deploy/api.env.example:6,11`: the placeholder CoreJwt__Key and AdminSeed__Password values would boot a production host if they were not replaced. Nothing refuses a change-me value. Worth a startup guard later (#115).
- `docs/deployment.md:171`: the PR path list omits .github/workflows/images.yml, which is a trigger path.
- `PROGRESS.md:138` still says "MinIO in compose". The orchestrator should record the R2/S3 decision there when it updates PROGRESS. PROGRESS is not an owned doc, so this is not a finding.

## Verified
- dotnet test (api/, Release, appsettings.json moved aside and restored): total 2638, failed 0, matching 02's claim (baseline 2621 + 17).
- shellcheck (koalaman/shellcheck:stable, all 5 scripts): clean. actionlint on images.yml and deploy.yml: clean.
- The scripts are 100755 in the index, and every new infra file is LF. git check-ignore covers deploy/.env, api.env, ai.env, backups/ and .smoke/.
- Secrets: none committed. Every example value is a change-me/placeholder, and the api and ai tokens match. CI uses only GITHUB_TOKEN. packages: write is on publish only.
- Forwarded headers: UseReverseProxyForwardedHeaders is the first middleware (Program.cs:121). It forwards For and Proto only, adds only the configured CIDRs to KnownIPNetworks, and keeps the loopback default and ForwardLimit=1. Compose passes the same DOCKER_SUBNET to the network and the API. Caddy has no trusted_proxies, so it overwrites any client X-Forwarded-For. The rate limiter partitions on RemoteIpAddress (AuthRateLimiting.cs:36). T4-T6 cover trusted-distinct, trusted-same and untrusted-spoof.
- AllowFakePayments: FakePaymentGateway.cs:13 is `!IsProduction() && (IsDevelopment() || AllowFakePayments)`. Production always refuses, and T14 covers switch on + Production. CompleteFakePaymentHandler goes through SupportsSimulatedCompletion, and no other IsProduction bypass exists.
- --MigrateAndExit: it is a config key, runs before the seed and the pipeline, has no try/catch (a failure gives a non-zero exit), and flushes Serilog in a finally. It cannot be reached over HTTP.
- Caddy: file_server has no browse (no directory listings). The site address gets automatic HTTPS with an HTTP-to-HTTPS redirect. :2080/healthz is not published, and /health and /scalar are not proxied. Security headers are out of scope per the plan (#115).
- CI: publish runs only when github.ref is refs/heads/main and the event is not a PR. deploy.yml is workflow_dispatch-only, validates the sha tag, reads inputs only through env:, fails at "Check deploy secrets" without secrets, and uses plain ssh/scp with known_hosts.
- All 24 planned files exist, and there are no extras. The Dockerfiles, Caddyfile and .dockerignore match the appendices. Deviations 1-7 are declared and accurate. api/openapi shows no drift, and there is no endpoint change, so Postman is untouched correctly.
- docs/deployment.md covers the Appendix H sections and lists every ELMANHG_AI_* from settings.py, the OtpDelivery__*, Payments__*, AiService__*, compose-set keys and every compose .env key. The paymob, subscriptions, otp-delivery, ai-service, constitution and README edits match the plan text, and no "Production lock" wording remains.

## Test quality
- FakePaymentGatewayTests: constraining. The staging on/off pairs (T12/T13, T15/T16) pin both halves of IsAllowed, and T14 pins the Production override.
- ForwardedHeadersTests: T4 fails without the middleware or without KnownIPNetworks.Add. T6 fails if the trust is widened to any peer.
- ReverseProxyOptionsValidatorTests: constraining. The theory asserts the failure names the index.
- MigrationCommandTests.RunAsync_MigratedDatabase_LeavesNoPendingMigrations: weak. ApiFactory has already migrated, so a no-op RunAsync would pass. The real constraint is the CI smoke test (migrate on an empty database, then a healthy api, then an idempotent re-run). The plan prescribed this shape, so it is not blocking.
