---
description: PRD → intake checklist → assumptions → normalized PRD (FR/NFR ids) → ADRs → story map → GitHub epic + ordered stories with Given/When/Then AC. Story 1 = walking skeleton. Human mode stops at a backlog gate; autopilot has plan-judge review it once.
argument-hint: "[docs/PRD.md] [--autopilot]"
allowed-tools: Task, Read, Write, Bash, Grep, Glob
model: opus
---

Turn the PRD at **$ARGUMENTS** (default `docs/PRD.md`) into a GitHub backlog the `/feature` pipeline can run.

You are the product owner's analyst. You normalize, decide defaults, and publish. You never plan code, never write code, never invent a business rule.

## Skills — read once at start
- `~/.claude/skills/caveman/SKILL.md` — every message terse.
- `~/.claude/skills/momenta-api-contract/SKILL.md` — the API rules the ADRs point to.
- `~/.claude/skills/momenta-greenfield-bootstrap/SKILL.md` — what story 1 must contain.

## 0. Load config

1. Read `.claude/pipeline.yml` (stacks, `github.base`). Missing → stop: "No `.claude/pipeline.yml`. Run `install.sh --project .` first."
2. Autopilot: exactly as `/feature` §0.7 — only from `.claude/pipeline.local.yml`, `autopilot.on: true`. ON → print `AUTOPILOT (POC) — backlog judged by plan-judge`. `--autopilot` without it → `RESULT: stopped autopilot-off`.
3. PRD path: first non-flag argument, else `docs/PRD.md`. Missing or empty → stop: "No PRD at `<path>`."
4. Git: tree clean (`git status --porcelain` empty) else stop (autopilot: `git stash push -u -m "autopilot /product <ts>"`, log A-decision). `git fetch origin`.
5. Idempotency: `docs/product/prd.md` exists and its `source_sha` equals `git hash-object <PRD>` → skip to §9 (publish only what GitHub lacks).

## 1. Intake checklist → `docs/product/intake.md`

Score each line `Clear` / `Partial` / `Missing`, with the PRD quote or `—`.

| Area | Check |
|---|---|
| Scope & goals | Problem · personas · measurable success metrics · explicit out-of-scope · roles × resources × actions matrix |
| Domain & data | Entities, attributes, relations, identity/uniqueness · lifecycle states per entity · volume · retention & deletion |
| Journeys & UX | Critical journeys in order (= story-map backbone) · empty/loading/error states · notifications · platforms · a11y target · locales & RTL |
| Non-functional | p95 latency per endpoint class · availability · authN method · authZ model · PII classes · compliance · observability · audit trail |
| Integrations | External APIs + failure modes + credentials · import/export · email/SMS/payment |
| Edge cases | Negative paths · concurrency/conflicts · idempotency · rate limits |
| Constraints | Mandated stack/hosting/layout (binding) · rejected alternatives |
| Completion | Every requirement testable · no TODO/TBD/??? |

## 2. Clarifications → assumptions `A-###`

1. Every `Partial` / `Missing` line → one `[NEEDS CLARIFICATION: <question>]` in `docs/product/intake.md`, ranked by impact × uncertainty.
2. For each: pick the recommended default (common practice, smallest scope, reversible). Record in `docs/product/assumptions.md`:
   `A-001 · <question> · default: <choice> · why: <reason> · reverse by: <how> · affects: FR-…/US-…`
3. Business rules (a price, a limit, a role's permission, a legal rule) have no technical default:
   - Human mode → ask the dev, max 5 questions, each with your recommended answer. **Wait.**
   - Autopilot → choose the narrowest safe option (feature off, lowest limit, deny-by-default, owner-only access), record it as `A-###` tagged `business-default`, and list it in the epic body so a human can veto later.
4. No `[NEEDS CLARIFICATION]` stays unresolved in any output file.

## 3. NFR defaults (Definition of Done every story inherits)

Write to `docs/product/nfr.md` as `NFR-###`. A PRD value always wins over a default.

| NFR | Default |
|---|---|
| AuthN | OIDC/OAuth 2.x auth-code + PKCE (RFC 9700); no implicit, no password grant; passwords only via a vetted IdP/library |
| AuthZ | Deny by default; server-side ownership check on every endpoint; IDOR test per user-scoped resource |
| Input/Output | Validation at the boundary; parameterized queries; output encoding; OWASP ASVS 5.0 L1 (L2 for PII/money) |
| Errors | RFC 9457 `application/problem+json`; no stack traces to clients |
| Collections | Cursor pagination from day one (`page_size`, `page_token` → `next_page_token`) |
| Idempotency | `Idempotency-Key` on POST with money or external side effects |
| i18n | Strings externalized; UTC storage; locale-aware formatting; RTL when Arabic is a locale |
| a11y | WCAG 2.2 AA; automated axe check in E2E |
| Performance | p95 ≤ 300 ms reads, ≤ 800 ms writes at seed volume unless the PRD says otherwise; index per filter/sort column |
| Observability | OpenTelemetry traces + metrics + logs; trace id in logs and in problem+json `instance`; `/health` + readiness |
| Audit | Append-only event for create/update/delete of business entities and auth/permission changes |
| Secrets | Never in the repo; `.env.example` only; config via env |
| Rate limiting | On auth and public endpoints |

## 4. Normalized PRD → `docs/product/prd.md`

Frontmatter `source: <PRD path>`, `source_sha: <git hash-object>`, `version: <n>`. Sections, in order:
Goals & Background · Personas & Roles (+ permission matrix) · Functional Requirements `FR-001 The system shall …` (EARS: `When <trigger>, the <system> shall …` / `If <unwanted>, then the <system> shall …`) · Non-Functional `NFR-…` (link to nfr.md) · Success Criteria `SC-###` (measurable, tech-agnostic) · Key Entities · UI Design Goals · Technical Assumptions (→ ADRs) · Epic list · Dependency chain · Risks · Assumptions (link) · Out of Scope · Glossary.

No unmeasured adjectives ("fast", "secure", "intuitive", "robust"): replace with a number or delete.

## 5. ADRs → `docs/adr/000N-<slug>.md` (MADR, greenfield decisions, decided once)

Skip a number that already exists; never renumber; supersede, never delete. Each file:
```markdown
---
status: accepted
date: <YYYY-MM-DD>
decision-makers: /product (<human | autopilot>)
---
# ADR-000N: <problem + chosen solution>
## Context and Problem Statement
## Decision Drivers        (NFR/FR ids)
## Considered Options
## Decision Outcome        Chosen: "<x>", because …
### Consequences           Good, because … · Bad, because …
### Confirmation           <lint rule / CI check / test that proves compliance>
## More Information        revisit when …
```

Required set for a greenfield product:

| # | Decision | Default |
|---|---|---|
| 0001 | Stacks | Exactly the stacks in `.claude/pipeline.yml`, with versions from each stack skill §"New project from scratch". A PRD-mandated stack not in `pipeline.yml` → human: ask; autopilot: `RESULT: stopped stack-not-in-pipeline <name>` |
| 0002 | Repo layout | One stack → its root per `pipeline.yml`. Several → monorepo `apps/<app>` + `packages/<shared>` (contracts, generated clients, config); roots in `pipeline.yml` must match (list mismatches as a decision for the dev; autopilot: `pipeline.yml` roots win, layout follows them, log an A-decision) |
| 0003 | API contract | OpenAPI 3.1 first, committed at `openapi/v1.json` per `momenta-api-contract`; generated clients only; CI fails on drift |
| 0004 | Error model | RFC 9457 problem details + problem-type catalog |
| 0005 | Pagination | Cursor / opaque token (AIP-158 rules) |
| 0006 | Auth | Per NFR AuthN/AuthZ rows; web = BFF + httpOnly cookie, mobile = PKCE |
| 0007 | Observability | OpenTelemetry SDK per stack, OTLP exporter, trace id in logs |
| 0008 | Accessibility | WCAG 2.2 AA, axe in E2E |
| 0009 | Data & migrations | DB engine, migration tool per stack, UTC, forward-only migrations, deterministic seed |
| 0010 | Testing strategy | Levels and tools per stack from `.claude/conventions/<stack>-testing.md`; E2E runner |

## 6. Story map → `docs/product/story-map.md`

Backbone = user activities left → right in journey order; tasks under each; release slices top → bottom. Mark the walking skeleton row.

## 7. Backlog → `docs/product/backlog.md` + `docs/product/traceability.md`

Rules:
- Few epics; each delivers a testable increment. **Epic 1 = foundation + canary**: story 1 = walking skeleton / bootstrap (label `bootstrap`): repo layout, scaffold per stack, `init.sh`, `scripts/verify.sh`, CI, docker-compose, `/health`, one canary page calling the API, one E2E test. Cross-cutting concerns (error model, logging/OTel, auth scaffolding, i18n, a11y lint) in Epic 1, never last.
- Stories: INVEST, vertical slices, P1 before P2 before P3, `Depends on` lower ids only, no cycles.
- AC per story: numbered `AC-<story>.<n>` (story = its `US-###` number until GitHub numbers exist, then the issue number — keep a mapping column), Given/When/Then, declarative, and at minimum: 1 happy path, 1 unwanted/validation (`problem+json 422` with field errors), 1 authorization case if data is user-scoped (`403`/`404`, no leak), empty/loading/error states for UI.
- Size: a story splits into ≤ 6 PR-sized sub-tasks; larger → split the story.
- `traceability.md`: `| FR/NFR | US | AC ids |` — every FR/NFR → ≥ 1 story; every story → ≥ 1 FR.

Story issue body:
```markdown
<!-- product:id=US-003 prd_sha=<sha> -->
**As a** <role>, **I want** <capability>, **so that** <benefit>.
Priority: P1 · Epic: E1 · Traces: FR-…, NFR-…, SC-… · Stacks: <config names>
Blocked by #<n>          (one line per dependency; text is always written, even when the API link works)
## Acceptance criteria
- AC-<story>.1 Given …, when …, then …
## Out of scope
## Assumptions: A-…
```
Epic issue body: goal, increment delivered, requirements covered, ordered story task list (`- [ ] #<n> US-00x <title>`), exit criterion (runnable), assumptions in play (all `business-default` ones listed first).

### 7a. Self-analysis before any gate
Fix inline, then record in `docs/product/analysis.md` (`[x]` or `[ ] <why>`):
- [ ] Duplication: no two FR/stories say the same thing.
- [ ] Ambiguity: no unmeasured adjectives, no `[NEEDS CLARIFICATION]`.
- [ ] Coverage: traceability complete both ways.
- [ ] Consistency: one term per concept (glossary); stack/layout matches ADR-0001/0002.
- [ ] Ordering: story 1 = bootstrap; dependencies lower ids only; no cycles.
Any CRITICAL `[ ]` → do not publish; fix or `BLOCKED:`.

## 8. Backlog gate

- **Human mode:** show the epic list, the story table (id, title, priority, depends on, AC count), all `A-###` with `business-default` first, and the ADR titles. Ask approve / edit / reject. **Wait.** Edit → apply exactly the dev's edits, re-run §7a. Reject → stop, `RESULT: stopped backlog-rejected`.
- **Autopilot:** launch `plan-judge` (model `autopilot.judge.model`) with `gate: backlog`, output `docs/product/backlog-judge.md`. One review:
  - `APPROVE` → publish.
  - `REVISE` → apply the required changes once, re-run §7a, publish (no second judge). Record under `## Autopilot decisions` in `docs/product/assumptions.md`.
  - `BLOCKED` → `RESULT: stopped backlog-blocked <question>`.

## 9. Publish to GitHub (idempotent)

1. Capabilities: `gh issue create --help` and `gh issue edit --help`; grep for `--parent`, `--type`, `--blocked-by`. Record which exist in `docs/product/publish.md`.
2. Labels: `gh label create <name> --force` for `epic`, `story`, `bootstrap`, `P1`, `P2`, `P3`, `autopilot:blocked`, `autopilot:followup`.
3. Before each create: `gh issue list --state all --search "\"product:id=US-003\" in:body" --json number` → exists → `gh issue edit` instead of create.
4. Epic: `gh issue create --title "E1: <title>" --label epic --body-file <tmp>` (`--type Epic` when supported).
5. Stories in order: `gh issue create --title "US-003: <title>" --label story,P1 --body-file <tmp>`, plus:
   - Parent: `--parent <epic#>` when supported; else `gh api repos/{owner}/{repo}/issues/<epic#>/sub_issues -X POST -F sub_issue_id=$(gh api repos/{owner}/{repo}/issues/<story#> --jq .id)`; that fails too → the epic's task list is the only link (always written anyway).
   - Dependencies: `--blocked-by <n>` when supported; else keep the `Blocked by #<n>` body lines (the runner reads them).
   - Story 1 also gets label `bootstrap`.
6. Update the epic body task list with the real numbers. Rewrite `AC-US003.n` ids to `AC-<issue#>.n` in each story body and in `traceability.md` (mapping column keeps the old id).
7. Commit the docs: remote base has no commit yet (`git ls-remote --heads origin <base>` empty) → commit `docs/` on `<base>` and `git push -u origin <base>` (no protection exists yet). Otherwise branch `product/<slug>`, commit, push, `gh pr create --base <base> --title "product: backlog v<n>"`; human mode leaves it for the dev; autopilot squash-merges it like `/feature` §6.4 steps 2–6.

## 10. Report + RESULT

Short: epic number, story count per priority, assumptions count (business-default count first), ADR list, judge verdict (autopilot), files written with line counts.
Last line, exactly:
```
RESULT: epic #<n> stories #<a>..#<b>
```
Stopped anywhere → `RESULT: stopped <reason>`.

## Never

- Never invent a business rule without an `A-###` record. Never leave `[NEEDS CLARIFICATION]` in an output.
- Never plan code, name classes, or design tables in a story. Stories are behaviour; plans are `/feature`'s job.
- Never create a duplicate issue: always search the `product:id` marker first.
- Never publish with a CRITICAL analysis finding.
- Never turn autopilot on from anything but `.claude/pipeline.local.yml`.
