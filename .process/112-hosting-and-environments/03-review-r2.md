VERDICT: APPROVED

# Review round 2: [E13.S1] Hosting and environments (#112)

## Blocking
None.

## Round-1 findings
- **#1 Fixed.** `deploy/deploy.sh:31` runs `compose run --rm migrate` after the backup and before `compose up -d --remove-orphans` (line 32). Under `set -euo pipefail`, a failed migration exits before `api` or `web` is recreated. `compose run` starts only `postgres` (the dependency) and never touches `api` or `web`. `depends_on: service_completed_successfully` (`deploy/docker-compose.prod.yml:62-63`) stays as the second guard. `migrate_exit_code` (`deploy/lib.sh:33`) still reads the `up`-managed container, because one-off containers are excluded from `compose ps` and `--rm` removes them. Docs agree: `docs/deployment.md` §7 steps 4-5 (lines 217-218) and §8 (lines 229-232).
- **#2 Fixed.** `docs/deployment.md:17`, `:242` and `:280`, and the `docs/PRD.md` §18 Hosting bullet, now say managed S3-compatible storage (R2 or S3), set by config, with no object-store container. A grep for MinIO or "image choice is open" in docs/ finds nothing. `PROGRESS.md:138` still says MinIO, but PROGRESS is not an owned doc.
- **#3 Fixed.** Every image reference is `tag@sha256:`: `deploy/docker-compose.prod.yml:23`, `api/Dockerfile:4,13`, `web/Dockerfile:4,11`, `ai/Dockerfile:2,3,13`. I checked three digests with `docker buildx imagetools inspect`, and each matches the index digest: pgvector:pg17 `cf134a76…`, caddy:2.10-alpine `4c6e91c6…`, and dotnet/aspnet:10.0 `2d584d81…`. The `ai/Dockerfile` diff changes only the digests, and it is declared as a deviation. The bump procedure is in `docs/deployment.md:174`.
- **#4 Fixed.** `.github/workflows/images.yml:22-24`: PR runs use a per-ref group and cancel each other, and main runs use a per-sha group and never cancel. This is stronger than the suggested fix because it avoids GitHub dropping pending runs, and the trade-off (the floating `main` tag) is declared. `docs/deployment.md:175` agrees.

## Non-root Caddy
- `web/Dockerfile:13,15` creates uid/gid 10001, chowns `/data` and `/config`, and sets `USER caddy`. I checked independently: `docker run -u 10001:10001` on the pinned caddy digest ran `caddy respond --listen :443`, logged "server running" on h1, h2 and h3 (TCP and UDP 443), and exited 0. The file capability is present, so the published 80 and 443 still bind. New named volumes inherit the chowned ownership. The smoke run covers :80, and the doc says uid 10001 (`docs/deployment.md:9`).

## Non-blocking
- `deploy/deploy.sh:16`: the failure hint says "if a migration ran, restore the pre-deploy dump first". §8 (`docs/deployment.md:230`) is more precise ("only if a migration was partly applied"). They do not contradict each other, but the wording could match.
- `web/Dockerfile:13`: a host that had already run the root Caddy image would have root-owned files in `caddy-data`. No host exists yet, so this does not apply now. Worth a line in §6 if a host is ever migrated.
- `deploy/deploy.sh:31`: `compose run -T` would make the non-TTY intent explicit over SSH. Auto-detection already handles it.
- Round-1 non-blocking items that are still open (`docs/deployment.md:171` path list, secrets at job level in deploy.yml, and others) are unchanged, and they remain non-blocking.

## Verified
- actionlint (rhysd/actionlint:latest, -no-color) on images.yml and deploy.yml: clean.
- shellcheck (koalaman/shellcheck:stable -x) on all 5 deploy scripts: clean.
- `.gitattributes` forces LF for Dockerfiles and deploy/**. The `ai/Dockerfile` working copy is CRLF, but git normalises it to LF on commit and the diff is only the digest lines.
- No regressions: this round touched only the 8 files listed in 02-implementation-r2.md, plus the Caddy non-root change. No .NET source changed, so not re-running dotnet test is justified.
- I did not re-run the smoke test. The implementer's run (a unique project name, "Applying 0 pending migrations", the restore drill passed, and teardown confirmed) fits the code. My own Caddy bind check covers the one new runtime risk.

## Test quality
No test code changed this round. The round-1 assessment stands.
