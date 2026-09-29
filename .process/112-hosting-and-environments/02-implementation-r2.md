# Implementation (rework r2): [E13.S1] Hosting and environments

## Blocking findings addressed

| # | What I changed | Where |
|---|---|---|
| 1 | `deploy.sh` runs `compose run --rm migrate` after the backup and before `compose up -d --remove-orphans`. Under `set -e` and the EXIT trap, a failed migration stops the script before `api` or `web` is replaced. `depends_on: migrate` stays as a second guard, and the `migrate` run inside `up` applies 0 migrations. Docs: §7 steps 4 and 5 (renumbered 4 to 8). §8 now says the old containers keep serving, `deploy.sh <previous tag>` only resets `IMAGE_TAG`, and the old API must be compatible with the new schema for the whole migration and restart. | `deploy/deploy.sh:30-31`, `docs/deployment.md:217-218`, `docs/deployment.md:229-232` |
| 2 | MinIO text replaced with the acceptance decision: object storage (from #96) is a managed S3-compatible service (Cloudflare R2 or AWS S3), set by config. Local dev and CI use the local-disk store, and there is no object-store container in compose. The §9 backups bullet now says the data lives with the managed provider, and durability and versioning are settled with #96. | `docs/deployment.md:17`, `docs/deployment.md:242`, `docs/deployment.md:280`, `docs/PRD.md:545` |
| 3 | Every production image is now pinned as `tag@sha256:<index digest>`. Each digest was resolved with `docker buildx imagetools inspect` on 2026-09-29 and is the top-level multi-platform index digest. §5 now has a bullet on how to bump a pinned digest. | `deploy/docker-compose.prod.yml:23`, `api/Dockerfile:4,13`, `web/Dockerfile:4,11`, `ai/Dockerfile:2,3,13`, `docs/deployment.md:174` |
| 4 | Concurrency group: `images-${{ github.event_name == 'pull_request' && github.ref \|\| github.sha }}`, with `cancel-in-progress: ${{ github.event_name == 'pull_request' }}`. I used a group per commit on main rather than one shared group per ref. With a shared group and no cancelling, GitHub still cancels a *pending* run when a third push arrives, so a middle commit would get no images. A 2-line comment in the workflow says why. §5 in the docs now describes this. | `.github/workflows/images.yml:20-24`, `docs/deployment.md:175` |

Pinned digests:

| Image | Digest |
|---|---|
| `pgvector/pgvector:pg17` | `sha256:cf134a767f474095eeba57e0117be8e568e011a63f33fbf252f14c9b760f8e6f` |
| `mcr.microsoft.com/dotnet/sdk:10.0` | `sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29` |
| `mcr.microsoft.com/dotnet/aspnet:10.0` | `sha256:2d584d8147faddb0d678c5748d47953e5b8e18621ed4fb7049a91381d9d7746f` |
| `node:24-slim` | `sha256:0e0ff40c39bc087845bfb27465a0df4ea419520094bc35842ff83dd8cbe6f9b6` |
| `caddy:2.10-alpine` (2.10.2) | `sha256:4c6e91c6ed0e2fa03efd5b44747b625fec79bc9cd06ac5235a779726618e530d` |
| `python:3.13.15-slim` | `sha256:7c61056e61ac89e852de05f3dc6fa51a6dd2181797bceed46aa725dd7cb2cd3b` |
| `ghcr.io/astral-sh/uv:0.12.17` | `sha256:10787c682e4184e4f290de1171fd4703dc63de99221f10fe1c99002ce7fa9acc` |

Non-blocking item done: web (Caddy) as non-root. `web/Dockerfile` creates `caddy` uid/gid 10001, chowns `/data` and `/config`, and switches to `USER caddy`. The official binary carries `cap_net_bind_service`, so it still binds 80 and 443. In the smoke run, `docker exec … id` gave `uid=10001(caddy)` and Caddy logged "server running". §1 of the docs now says so (`docs/deployment.md:9`).

## Files modified (this round)
| Path | Change |
|---|---|
| `deploy/deploy.sh` | migrate-before-up |
| `.github/workflows/images.yml` | concurrency group and cancel condition |
| `deploy/docker-compose.prod.yml` | postgres pinned by digest |
| `api/Dockerfile` | sdk and aspnet pinned by digest |
| `web/Dockerfile` | node and caddy pinned by digest; non-root caddy user |
| `ai/Dockerfile` | python (x2) and uv pinned by digest |
| `docs/deployment.md` | §1, §5, §7, §8, §9, §13 |
| `docs/PRD.md` | §18 Hosting bullet |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `ai/Dockerfile` is not in the plan's touched files | Finding 3 and the orchestrator ask for every Dockerfile `FROM` to be pinned, and the ai image is deployed by this stack | Pinned its three image references. There are no other changes to that file. |
| Finding 4 suggested a shared group with PR-only cancelling | A shared group still drops queued runs when a third push arrives | Used a group per commit on main (the orchestrator's second option). Two main runs can now finish out of order, so the floating `main` tag may briefly point at the older commit. Deploys use `sha-<7>` tags only (§5), so this does not affect them. |

## Build & test
- Smoke test: `SMOKE_PROJECT_NAME=elmanhg-smoke-112r2 SMOKE_HTTP_PORT=18112 SMOKE_HTTPS_PORT=18113 SMOKE_DOCKER_SUBNET=172.30.212.0/24 bash deploy/smoke-test.sh` → built all 3 images from the pinned bases, then printed "Applying 0 pending migrations" on the second migrate, "Restore drill passed: 24 migrations in elmanhg_restore_drill" and "Smoke test passed", exit=0. The stack, volumes and network were removed afterwards, and `docker ps -a --filter name=elmanhg-smoke-112r2` is empty.
- shellcheck (`koalaman/shellcheck:stable -x` on deploy.sh, lib.sh, backup.sh, restore.sh, smoke-test.sh): no output (clean).
- actionlint (`rhysd/actionlint:latest` on images.yml, deploy.yml): no output (clean).
- API tests: not re-run. No .NET source changed; only `api/Dockerfile` changed.

## Notes for review
- `compose run --rm migrate` also starts `postgres` if it is not running. It may recreate `postgres` when its config changed, for example on the first deploy after this digest pin. That is a short database restart that the old API's retry strategy absorbs. It does not replace `api` or `web`.
- `deploy.sh` itself was not run against a failing migration. The ordering guarantee comes from `set -e` plus `run` never touching `api` or `web`. The smoke test exercises the same `compose run --rm migrate` command.
- Caddy as non-root needs the default Docker capability set (NET_BIND_SERVICE). A host that runs with `--cap-drop ALL` or `no-new-privileges` would need that capability added back.

## Main merge

`git merge origin/main` brought in #90 (lesson content retrieval: pgvector, `LessonContentIndexWorker`, OpenAI embeddings through the ai service). Conflicts in `.env.example` and `README.md` were already resolved by the orchestrator.

### Files modified
| Path | Change |
|---|---|
| `docs/deployment.md` | §3: `api.env` holds content retrieval; the API never sees the Anthropic or OpenAI key. §4: new "Content retrieval (`api.env`)" table with every `ContentRetrieval__*` key, its default and range, plus a note that with `AiService__Provider=Http` the sweep needs the `ai` profile. The AI service table gains `ELMANHG_AI_EMBEDDING_PROVIDER`, `ELMANHG_AI_OPENAI_API_KEY`, `_EMBEDDING_MODEL`, `_EMBEDDING_DIMENSIONS`, `_EMBEDDING_MAX_TEXTS`, `_EMBEDDING_MAX_TEXT_CHARS` and `_EMBEDDING_USD_PER_MILLION_TOKENS`, followed by a note that switching the embedding provider or model needs `POST /api/content-index/rebuild`. |
| `deploy/ai.env.example` | embedding block: `ELMANHG_AI_EMBEDDING_PROVIDER=fake`, then the key, model, dimensions, limits and price commented out, with the rebuild note |
| `deploy/api.env.example` | commented `ContentRetrieval__*` block with the defaults |

Checked without changing anything: the prod compose `postgres` is `pgvector/pgvector:pg17` (pinned by digest), so `CREATE EXTENSION vector` from `AddLessonContentIndex` works. `POSTGRES_USER` is the image's superuser. `--MigrateAndExit` returns before `app.Run`, so the new hosted worker never starts in the `migrate` container.

### Deviations
None.

### Build & test
- `dotnet build api/`: 0 errors (9 existing warnings in core-libraries).
- `dotnet test api/ -c Release`, run with `api/Elmanhg.Api/appsettings.json` moved aside and restored afterwards: `total: 2728, failed: 0, succeeded: 2728, skipped: 0`.
- ai checks: uv is not installed on this machine, so I used the main checkout's `ai/.venv` Python with `PYTHONPATH=<worktree>/ai/src`. I confirmed that `elmanhg_ai` resolves to the worktree. `pytest`: `97 passed`. `ruff check`: `All checks passed!`. `mypy`: `Success: no issues found in 50 source files`.
- Smoke: `SMOKE_PROJECT_NAME=elmanhg-smoke-112m SMOKE_HTTP_PORT=8192 SMOKE_HTTPS_PORT=8543 SMOKE_DOCKER_SUBNET=172.30.212.0/24 bash deploy/smoke-test.sh` exited 0. It printed "Applying 0 pending migrations" on the second migrate, "Restore drill passed: 25 migrations in elmanhg_restore_drill" (24 + `AddLessonContentIndex`, so the pgvector extension restores too) and "Smoke test passed". Afterwards no containers or volumes named `elmanhg-smoke-112m` remained.

### Notes for review
- The unstaged `docs/deployment.md` §7 `DEPLOY_KNOWN_HOSTS` fingerprint wording was already in the working tree before this pass. I left it as it was, and it is committed with the merge.
- The untracked `.process/112-hosting-and-environments/05-coderabbit-comments.md` and `06-coderabbit-triage.md` are staged with the merge, as instructed ("stage everything").
