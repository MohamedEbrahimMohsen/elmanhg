---
name: momenta-greenfield-bootstrap
description: Use for story 1 of a greenfield epic (label `bootstrap`, empty repo, or `bootstrap:` in 00-status.md) — the walking skeleton order, init.sh, scripts/verify.sh, docker-compose, .env.example, CI, branch protection, PR-Agent config, health/canary/OTel stubs, and the "bootstrap done" checklist.
---

# Momenta greenfield bootstrap (walking skeleton)

Goal: the thinnest end-to-end path through every layer the product needs, green in CI, before any feature. Integration problems surface in hour 1, not hour 20. Every later story assumes this skeleton; nothing here is optional unless the stack does not exist.

Splitter: this is sub-task 1 (split further only if > ~400 lines: e.g. 1a scaffold + CI, 1b compose + health + canary + E2E). Planner: list every file below in `## Files`. Implementer: run the generator commands, do not hand-write what a generator produces.

## 1. Order (each step ends green before the next)

| # | Step | Output | Check |
|---|---|---|---|
| 1 | Repo layout | One stack: its `root` from `pipeline.yml`. Several: `apps/<app>` per stack (`apps/api`, `apps/web`, `apps/mobile`) + `packages/` (contracts, generated clients, shared config). `pipeline.yml` roots match exactly. | `ls` of every `root` succeeds |
| 2 | Scaffold per stack | Generator + layout from `.claude/skills/<stack>-feature/SKILL.md` §7 "New project from scratch", versions pinned per `docs/adr/0001-*` | each stack's `build` and `test` from `pipeline.yml` exit 0 |
| 3 | Toolchain pins + lockfiles | `global.json` / `.nvmrc` / `.python-version` / `.tool-versions`, `.editorconfig`, committed lockfile per stack | `npm ci` / `dotnet restore --locked-mode` / `uv sync --locked` (verify per stack) exit 0 |
| 4 | `.gitignore` | Stack ignores + `.env`, `.env.local`, `.process/autopilot/`, `.claude/pipeline.local.yml`, `.claude/settings.local.json` | `git check-ignore .env` prints `.env` |
| 5 | `.env.example` | Every env var the app reads, with safe dev values or `changeme`; no real secret | `gitleaks detect --no-git -s .` clean |
| 6 | `docker-compose.yml` | DB/cache only (the app runs from its own tooling). Every service has a `healthcheck`; container port only (`ports: ["5432"]`) so Docker picks a free host port; named volume | `docker compose up -d --wait` exit 0 |
| 7 | Health endpoint | `GET /health` → 200 `{"status":"ok"}` after one DB round-trip; `GET /health/ready` for readiness | integration test + smoke curl |
| 8 | Canary page | One UI route that calls `/health` (via the generated client when a contract exists) and renders the status; empty/loading/error states; design-system tokens only | component test + E2E test |
| 9 | OpenTelemetry stub | SDK wired per stack, service name set, OTLP exporter from env (`OTEL_EXPORTER_OTLP_ENDPOINT`), console exporter when unset; trace id in every log line | a log line with a trace id in test output |
| 10 | Error model + contract | `openapi/v1.json` with `/health` and the problem+json schema (RFC 9457) per `momenta-api-contract` | spec lint command exit 0 |
| 11 | `init.sh` | See §2 | `./init.sh && ./init.sh` both exit 0 |
| 12 | `scripts/verify.sh` | See §3 | exit 0 on a clean clone after `init.sh` |
| 13 | CI workflow | See §4 | first run green on the PR |
| 14 | PR-Agent config | See §5 | bot comments on the bootstrap PR |
| 15 | Branch protection | See §6 (after CI ran once) | `gh api repos/{owner}/{repo}/rulesets` lists it, or a logged skip |
| 16 | README | Prerequisites, `./init.sh`, `scripts/verify.sh`, how to run each app, URLs, where ADRs live | a fresh clone following it reaches the canary page |
| 17 | Design system | UI stack → `.claude/design-system.md` exists and is non-empty (never generated here) | missing → `BLOCKED: no-design-system` |

## 2. `init.sh` (idempotent, ≤ 2 min, `set -euo pipefail`)

In order; each line safe to re-run:
1. `[ -f .env ] || cp .env.example .env` — then replace `changeme` values with random dev secrets (`openssl rand -hex 16`) only in the new `.env`.
2. Install deps from lockfiles: `npm ci` / `dotnet restore` / `uv sync` / `flutter pub get` / `./gradlew --version` (per existing stack root only).
3. `docker compose up -d --wait` (skip when no compose file).
4. Migrate: the stack's migration command (`dotnet ef database update`, `npx prisma migrate deploy`, `python manage.py migrate`, `alembic upgrade head`).
5. Seed deterministic fixture data (idempotent upsert; fixed ids).
6. Print the URLs and `init: ok`. Exit ≠ 0 on the first failure with `init: FAILED at <step>`.

Never start long-running app servers in `init.sh`; `scripts/verify.sh` and the E2E step start what they need.

## 3. `scripts/verify.sh` (the single "is it green" command)

`set -euo pipefail`; one `==> <step>` line before each step; quiet reporters; exit ≠ 0 on the first failure. Flag `--fast` skips step 6.
1. Format check (`dotnet format --verify-no-changes`, `prettier --check`, `ruff format --check`, `dart format --set-exit-if-changed`, `ktlintCheck`).
2. Lint / typecheck (`eslint`, `tsc --noEmit`, `ruff check`, `mypy`, `flutter analyze`, analyzers as errors).
3. Build every stack (`pipeline.yml` `build`).
4. Test every stack (`pipeline.yml` `test`).
5. Contract: spec lint + generated-client drift check.
6. Smoke: start the API on a free port in the background (PID file, killed by `trap … EXIT`), poll `curl -fsS http://localhost:$PORT/health` up to 60 s, assert `"status":"ok"`.
Last line `verify: ok`.

## 4. CI — `.github/workflows/ci.yml`

- Triggers: `pull_request`, `push: branches: [<base>]`, `merge_group`.
- One job per stack plus shared jobs. Job names are the required-check names and never change once protection exists: `<stack>-build-test` (per stack), `contract`, `e2e-smoke`, `test-integrity`, `secrets-scan`.
- **No `paths:` / `paths-ignore:` filters on the workflow** (a skipped required workflow stays "Pending" and blocks merge). To skip work, use job-level `if:` (a skipped job counts as success).
- Each job runs the same commands as `scripts/verify.sh` steps; `e2e-smoke` runs `./init.sh` then the one E2E test.
- `test-integrity`: fails when a pre-existing test file is deleted, or added lines contain skip/only markers (same patterns as `/feature` §7).
- `secrets-scan`: gitleaks action or `gitleaks detect --redact`.
- Pin actions to a major version (`actions/checkout@v5` — verify current majors); cache per ecosystem; `permissions: contents: read` at the top.

## 5. PR-Agent — `.pr_agent.toml`

- Set `[github_action_config] auto_review = true`, `auto_describe = true`, `auto_improve = true` and `pr_actions = ["opened", "reopened", "ready_for_review", "synchronize"]` (webhook action types). Workflow `.github/workflows/pr-agent.yml` per the PR-Agent docs; model and fallback from the same provider.
- The workflow's bot guard (`if: github.event.sender.type != 'Bot'`) must not skip autopilot PRs: when PRs come from a GitHub App/bot, guard on the PR-Agent bot's own login instead.
- Keys via repo secrets only; never in the file.

## 6. Branch protection / ruleset (needs admin)

After CI ran once on the bootstrap PR (so the check names exist):
```
gh api -X PATCH repos/{owner}/{repo} -F allow_auto_merge=true -F delete_branch_on_merge=true -F allow_squash_merge=true -F allow_merge_commit=false -F allow_rebase_merge=false
gh api -X POST repos/{owner}/{repo}/rulesets --input .github/ruleset-main.json
```
`.github/ruleset-main.json`: target branch `<base>`, rules `pull_request` (required approvals 0 for autopilot POCs, 1 for team repos), `required_status_checks` (the job names in §4, strict), `required_linear_history`, `non_fast_forward`, `deletion`; no bypass actors.
403 / not permitted → do not retry, do not ask for a token: write `SKIPPED: branch protection — needs admin` in the report and under `## Autopilot decisions`, continue. Auto-merge then falls back to a plain squash merge after checks pass (`/feature` §6.4).

## 7. "Bootstrap done" checklist

The orchestrator writes it to `<run>/09-bootstrap-check.md` from command results. All `[x]` before merge.

- [ ] Every `pipeline.yml` stack `root` exists; `build` and `test` exit 0 per stack.
- [ ] `./init.sh` exits 0 twice in a row on a fresh clone.
- [ ] `scripts/verify.sh` exits 0 and prints `verify: ok`.
- [ ] `curl -fsS <api>/health` → 200 with `"status":"ok"` (DB touched).
- [ ] Canary page renders the health status; its E2E test passes.
- [ ] A log line with a trace id appears in test or smoke output.
- [ ] `openapi/v1.json` lints clean and contains `/health` + problem+json schema.
- [ ] `.env.example` exists; `.env` is git-ignored; gitleaks clean.
- [ ] `docker compose up -d --wait` exits 0 (or no compose needed: stated).
- [ ] `.github/workflows/ci.yml` has no `paths` filters; all jobs green on the PR; job names listed in the report.
- [ ] `.pr_agent.toml` committed; PR-Agent commented on the bootstrap PR (or `PR-Agent silent` logged).
- [ ] Ruleset applied, or `SKIPPED: branch protection — needs admin` logged.
- [ ] README run instructions tested by following them.
- [ ] UI stack: `.claude/design-system.md` present and non-empty.
- [ ] Lockfile committed per stack; toolchain pins present.

## Never

- Never put a real secret, token, or connection string with a password in any committed file.
- Never add `paths:` filters to a workflow that holds required checks.
- Never rename a CI job after protection lists it (rename = add the new name to the ruleset first).
- Never generate `design-system.md`; it comes from the UI/UX team.
- Never pick a stack or version not in `pipeline.yml` / `docs/adr/0001-*`.
