# 02 — Planning agents: best practices (product/PRD → story-splitter → planner)

Research date: 2026-09-27. Scope: rules and templates for `/product` (PRD → GitHub epic + stories + AC), `story-splitter` (story → ordered PR-sized sub-tasks), `planner` (02-plan.md). Stacks: .NET, React, Node, Python (Flask/Django), Flutter, KMP. Target: fully unattended greenfield web-app build.

---

## 0. Cross-cutting findings (read first)

1. **Every mature SDD tool uses the same 3-layer chain: WHAT (requirements + scenarios) → HOW (design/plan) → DO (ordered tasks), with IDs linking each layer.** spec-kit: spec.md (FR-###, SC-###, P1..Pn stories) → plan.md → tasks.md (T###, [P], [US1]). Kiro: requirements.md (numbered Requirement N, EARS criteria N.M) → design.md → tasks.md (each task cites `_Requirements: 1.2, 3.1_`). BMAD: PRD (FR/NFR, epics, stories+AC) → architecture → story file (Tasks reference `AC: #`). **Adopt: stable IDs at every layer + traceability matrix = the single most useful anti-drift mechanism.**
2. **Keep behavior out of implementation and vice versa.** OpenSpec: "A spec is a behavior contract, not an implementation plan… If implementation can change without changing externally visible behavior, it likely does not belong in the spec." Stories/AC = observable behavior; plan = files, signatures, decisions.
3. **Uncertainty must be explicit, never silently guessed.** spec-kit uses `[NEEDS CLARIFICATION: …]` markers and an `## Assumptions` section; `/clarify` scans an 11-category taxonomy, asks max 5 high-impact questions, each with a recommended answer. In an unattended pipeline: **auto-accept the recommended answer, record it as an Assumption with an ID, and surface it in the epic body** so humans can veto asynchronously.
4. **Unattended = verification-gated.** Anthropic: "Give Claude a check it can run… Without a check it can run, 'looks done' is the only signal available." Long-running-agent harness: one feature at a time, a feature list with `"passes": false`, never edit/remove tests to pass, failure modes are *one-shotting* and *declaring victory early*. → Every story and sub-task must carry a runnable verification command and expected result.
5. **Critique of SDD (Böckeler/Fowler):** verbose markdown causes review overload; agents still ignore parts of long specs; one-size workflow is overkill for small changes. → Use **progressive rigor** (OpenSpec "Lite by default, Full for API/contract, migration, security, cross-repo"). Cap document lengths. Plan must be shorter than the code it describes (superpowers: "A plan longer than the code it describes has written the code instead").
6. **Size for agents, not sprints.** METR: frontier agents ~100% on tasks <4 human-minutes, success falls steeply as human-time grows; 80%-reliability horizon is far shorter than 50%. BMAD: "Each story must be completable by a single AI agent in one focused session without context overflow… junior developer working for 2-4 hours." → Sub-task ≈ ≤ 2-4 human-hours, one PR, one test cycle.

---

## 1. `/product` agent — PRD → epic + stories + AC

### 1.1 Rules

- **Intake before generation.** Run the PRD intake checklist (1.3). For each gap, emit `[NEEDS CLARIFICATION]` → pick the recommended default → record as `A-###` assumption. Max ~5 blocking questions (Impact × Uncertainty ranking, spec-kit). Never invent business rules; defaults only for technical/common-practice choices.
- **Build a story map first, then cut the backlog from it** (Patton). Backbone = user activities left→right in narrative order; tasks under each; priority top→bottom. Do not flatten early — a flat backlog is "a bag of context-free mulch."
- **First release slice = walking skeleton.** Cockburn: "a tiny implementation of the system that performs a small end-to-end function. It need not use the final architecture, but it should link together the main architectural components." Patton: the top row across all activities "describe[s] the smallest possible system."
- **Epic 1 = foundation + a visible increment.** BMAD: "Epic 1 must establish foundational project infrastructure (app setup, Git, CI/CD, core services)… while also delivering an initial piece of functionality, even as simple as a health-check route or display of a simple canary page."
- **Cross-cutting concerns flow through, never last.** BMAD: "adding a logging framework as a last story… would be terrible." Logging, error model, auth, i18n scaffolding, a11y lint, telemetry, CI gates go in Epic 1 / foundation stories; each later story's AC *inherits* them via Definition of Done.
- **Err on fewer epics**; an epic must deliver a deployable, testable increment (an API alone can be value).
- **Story ordering:** "No story should depend on work from a later story or epic" (BMAD). Dependencies may only point to lower IDs (Taskmaster: "a task can only depend on tasks with lower IDs"). Build order = topological sort of a module dependency graph (Taskmaster RPG: foundation layer Phase 0 → layers above; no cycles; every non-foundation module depends on ≥1 module).
- **Order by priority within the dependency constraint:** P1 journey stories first (each P1 story alone should be a viable MVP — spec-kit), then P2/P3. Prefer "getting as quickly as possible to something usable/visible front end that works" (Taskmaster PRD template).
- **Each story = vertical slice** through all layers needed for observable behavior (Humanizing Work, Bogard: "minimize coupling between slices, maximize coupling in a slice"). Allowed exceptions: enabler stories in Epic 1 only.
- **INVEST gate on every story** (Wake): Independent, Negotiable, Valuable, Estimable, Small, Testable ("I understand what I want well enough that I could write a test for it").
- **AC rules:**
  - Write AC as Given/When/Then (behavior) — Given = "state of the world before", When = "the behavior", Then = "the changes you expect" (Fowler). Use declarative domain language, not UI click-steps.
  - Use EARS for system-level/NFR requirements: Ubiquitous `The <system> shall <response>`; Event `When <trigger>, the <system> shall…`; State `While <state>, …`; Unwanted `If <trigger>, then the <system> shall…`; Optional `Where <feature>, …`; Complex `While…, when…, …`.
  - Every story needs ≥1 happy path, ≥1 validation/unwanted-behavior (`If…then`), ≥1 authorization AC if data is user-scoped, and empty/loading/error states for UI.
  - Ban unmeasured adjectives ("fast", "secure", "intuitive", "robust") — convert to numbers (spec-kit analyze flags these as HIGH).
  - Use Example Mapping heuristics: too many rules (blue cards) on a story → split; too many questions (red) → not ready; many examples under one rule → split the rule.
  - Number AC `AC-<story>.<n>` so sub-tasks/tests reference them.
- **Success criteria tech-agnostic and measurable** (spec-kit SC-###), e.g. "user completes signup in < 2 min", "list page p95 < 300 ms at 10k rows".
- **Record out-of-scope explicitly** (OpenSpec proposal "Out of scope"; Anthropic: good specs "state what is out of scope").
- **GitHub mapping (gh ≥ 2.94, June 2026):** `gh issue create --type Epic`, stories with `--type Feature/Story --parent <epic#>`, ordering with `--blocked-by <#>`. Store the machine-readable ID (`US-003`) in the title and a hidden `<!-- meta: {...} -->` block for the pipeline. Query via `gh issue view --json` (parent, sub-issues, type, dependency fields).
- **Self-validate before publishing** (spec-kit `/analyze` passes): duplication, ambiguity, underspecification, coverage (every FR/NFR → ≥1 story; every story → ≥1 FR), inconsistency (terminology drift, conflicting stack choices), dependency order. Severity CRITICAL blocks publishing.

### 1.2 Output structure

```
product/
  prd.normalized.md        # PRD rewritten into template 1.4, with FR/NFR/A IDs
  story-map.md             # backbone, tasks, release slices
  glossary.md              # canonical terms (spec-kit: avoid synonyms)
  adr/0001-*.md ...        # stack & cross-cutting decisions (see §3.4)
  epics/E1.md, stories/US-001.md ...  # mirrored to GitHub issues
  traceability.csv         # FR/NFR -> US -> AC
```

### 1.3 Template — PRD intake checklist (score Clear / Partial / Missing; derived from spec-kit clarify taxonomy + BMAD PO checklist)

```markdown
## PRD Intake — <product>
### Scope & goals
- [ ] Problem, target users/personas, measurable success metrics
- [ ] Explicit out-of-scope list
- [ ] Roles & permissions matrix (role × resource × action)
### Domain & data
- [ ] Entities, attributes, relationships, uniqueness/identity rules
- [ ] Lifecycle/state transitions per entity (draft→active→archived…)
- [ ] Data volume/scale assumptions; retention & deletion rules
### Journeys & UX
- [ ] Critical user journeys (ordered) = story-map backbone
- [ ] Empty / loading / error states; notifications
- [ ] Target platforms (web responsive / mobile Flutter / KMP / desktop)
- [ ] Accessibility target (default WCAG 2.2 AA); locales & RTL
### Non-functional
- [ ] Performance (p95 latency, throughput), availability target, RPO/RTO
- [ ] Security & privacy: authN method, authZ model, PII classes, compliance
- [ ] Observability expectations; audit-trail requirements
### Integrations
- [ ] External services/APIs + failure modes, rate limits, credentials to obtain
- [ ] Import/export formats; email/SMS/payment providers
### Edge cases
- [ ] Negative scenarios, concurrency/conflict resolution, idempotency, rate limiting
### Constraints & decisions
- [ ] Mandated stack/hosting/repo layout (PRD choices are binding — Taskmaster: "STRICTLY ADHERE")
- [ ] Explicit tradeoffs / rejected alternatives
### Completion signals
- [ ] Every requirement testable; no TODO/TBD/??? left
- [ ] Unresolved → [NEEDS CLARIFICATION] → resolved as A-### with recommended default
```

### 1.4 Template — normalized PRD (merge of spec-kit spec, BMAD PRD, Taskmaster RPG)

```markdown
# <Product> PRD (normalized v<N>)
## Goals & Background   (1-line goals; 1-2 para context)
## Personas & Roles      (+ permission matrix)
## Functional Requirements   FR-001 System MUST … (EARS where possible)
## Non-Functional Requirements NFR-001 … (measurable; see §1.7)
## Success Criteria          SC-001 … (tech-agnostic, measurable)
## Key Entities              (no implementation detail)
## UI Design Goals           (core screens, a11y level, platforms, branding)
## Technical Assumptions     (repo structure, service architecture, testing levels — CRITICAL DECISIONS → ADRs)
## Story Map                 (activities → tasks; release slices; walking skeleton marked)
## Epic List                 (E1 foundation+canary, E2..; 1-sentence goal each)
## Dependency Chain          (Phase 0 foundation → Phase n; no cycles)
## Risks                     (impact, likelihood, mitigation, fallback)
## Assumptions               A-001 … (default chosen, why, how to reverse)
## Out of Scope
## Glossary
## Clarifications            (Q → A log)
```

### 1.5 Template — Epic issue

```markdown
# E<n>: <title>
**Goal:** <2-3 sentences: objective + value when deployed>
**Increment delivered:** <what a user/dev can do after this epic>
**Requirements covered:** FR-…, NFR-…
**Stories (ordered):** US-00x → US-00y (blocked-by chain)
**Exit criteria:** <observable, runnable check: e2e suite tag @E<n> green on main>
**Assumptions in play:** A-…
```

### 1.6 Template — Story issue

```markdown
# US-<nnn>: <short verb-object title>   (Epic E<n>, Priority P1|P2|P3)
**As a** <role>, **I want** <capability>, **so that** <benefit>.
**Why this priority:** <value / risk / dependency reason>
**Independent test:** <how this story alone is demoed/tested>
**Depends on:** US-… (lower IDs only)   **Traces:** FR-…, NFR-…, SC-…
## Acceptance Criteria
- AC-<nnn>.1 Given <state>, when <action>, then <observable outcome>
- AC-<nnn>.2 Given …, when <invalid input>, then <problem+json 422 with field errors>
- AC-<nnn>.3 Given a user without <permission>, when …, then 403 and no data leak
- AC-<nnn>.4 (UI) empty / loading / error state …
## NFR hooks (only those that apply; defaults come from DoD)
- perf: … | a11y: … | i18n: new strings in en+ar, RTL verified | audit: event <name> recorded
## Out of scope
## Open questions / assumptions: A-…
## Size: S|M|L (L ⇒ must be split before ready)   Stacks touched: [api, web, mobile]
```

### 1.7 Template — NFR checklist (default DoD inheritance; each line becomes an AC only when story-specific)

```markdown
- AuthN: OIDC/OAuth 2.x auth-code + PKCE (RFC 9700: PKCE for public clients; no implicit, no ROPC; exact redirect-URI match; refresh-token rotation or sender-constrained). Passwords only via a vetted IdP/library.
- AuthZ: deny-by-default; resource-ownership checks server-side on every endpoint; tests for cross-tenant/IDOR.
- Input validation at boundary; output encoding; parameterized queries (OWASP ASVS 5.0 as checklist, L1 min, L2 for PII/money).
- Secrets never in repo; config via env/secret store.
- Error model: RFC 9457 application/problem+json (type, title, status, detail, instance + extensions e.g. errors[]); no stack traces to clients.
- Pagination on every collection endpoint from day one (adding it later is breaking — Google AIP-158); cursor/opaque token preferred (Zalando 160).
- Idempotency for POST that creates money/side-effects (Idempotency-Key).
- i18n: all user-facing strings externalized; locale-aware dates/numbers/currency; RTL layout if Arabic; UTC storage.
- a11y: WCAG 2.2 AA — keyboard operable (2.1.1), focus visible (2.4.7), focus not obscured (2.4.11), contrast 4.5:1 (1.4.3), target size 24×24 (2.5.8), labels (3.3.2), accessible auth (3.3.8), redundant entry (3.3.7), status messages (4.1.3), reflow (1.4.10). Automated axe check in e2e.
- Performance: stated p95 per endpoint class; N+1 check; indexes for every filter/sort column.
- Observability: OpenTelemetry traces+metrics+logs, semantic conventions; correlation/trace id in logs and problem+json `instance`; health/readiness endpoints.
- Audit: append-only audit event for create/update/delete of business entities and all auth/permission changes (who, what, when, before/after, request id).
- Data: migrations forward-only & reviewed; expand→migrate→contract for breaking schema changes (Fowler ParallelChange); backups/retention.
- Privacy: PII inventory; delete/export path if required.
- Rate limiting on auth and public endpoints.
```

---

## 2. `story-splitter` agent — story → ordered sub-tasks (one PR each)

### 2.1 Rules

- **Input gate:** story must pass INVEST (except Small) and have numbered AC. If not → return `NOT_READY` with the failing checks (Humanizing Work step 1: "If you start with something that isn't an increment of value, there's no way to slice it smaller"). Treat readiness as guidelines, not a stage gate (Cohn) — allow proceeding with recorded assumptions.
- **Split story-sized work with the Humanizing Work flowchart, in order:** (1) workflow steps — build the simple end-to-end first, then middle steps ("one step at a time from beginning to end—is the wrong way"); (2) operations/CRUD (any "manage"); (3) business-rule variations; (4) data variations; (5) data-entry/interface variations; (6) major effort (first variant pays infrastructure cost); (7) simple/complex core; (8) defer performance/NFR ("make it work" then "make it fast"); (9) spike last. SPIDR mnemonic = Spikes, Paths, Interfaces, Data, Rules.
- **Meta-pattern for the first slice:** find the core complexity, list variations, reduce to one complete slice.
- **Choose between candidate splits:** prefer the split that lets you deprioritize/delete a piece, then the one giving more equally sized pieces.
- **Then split into PR-sized sub-tasks** (Google small CLs): "one self-contained change"; strategies: stacking, by files, horizontal (shared code/stubs/contracts first), vertical, grid. Separate refactors from features; keep tests in the same PR; never break the build between PRs.
- **Default sub-task order for a full-stack story (contract-first grid):**
  1. Contract: OpenAPI paths/schemas + problem types (+ generated client) — no behavior.
  2. Data: entity + migration + repository + unit tests.
  3. Backend behavior: handler/service/endpoint + integration tests hitting real DB (Testcontainers/pytest-django db) covering AC.
  4. Client(s): web (React) / mobile (Flutter, KMP) screen consuming generated client + component tests.
  5. E2E: Playwright/integration_test scenario per AC-happy path + a11y/axe.
  Merge 1+2 or 4+5 when tiny; split 3 by operation if > budget. Each sub-task ends green and deployable (feature flag if user-visible but incomplete).
- **Size budget per sub-task:** target 50–200 changed LOC excluding generated code/lockfiles/snapshots; hard cap ~400 (Graphite: ~50-line PRs merge ~40% faster than 250-line and are 15% less likely to be reverted, sweet spot 25–100; Cisco/SmartBear: review ≤ 200–400 LOC; Google: ~100 reasonable, 1000 too large). ≤ ~10 files touched. ≤ 2–4 human-hours (BMAD, METR). Exceed → split again.
- **Fold scaffolding into the task that needs it** (superpowers "Task Right-Sizing": "fold setup, configuration, scaffolding, and documentation steps into the task whose deliverable needs them; split only where a reviewer could meaningfully reject one task while approving its neighbor").
- **Complexity score 1–10 per sub-task** (Taskmaster `analyze-complexity`: score, recommendedSubtasks, reasoning) using factors: new vs modified code, number of layers, new external dependency, concurrency/state, security sensitivity, migration of existing data, unknown API. Rule: score ≥ 7 ⇒ split or add a spike; score ≥ 5 ⇒ planner must list interfaces + risks explicitly.
- **Dependencies:** explicit `depends_on` (lower IDs only), mark `[P]` parallel-safe when different files & no dependency (spec-kit). Validate acyclic (Taskmaster `validate-dependencies`).
- **Only coding tasks** (Kiro): no "deploy to staging", "UAT", "gather metrics", "write user docs" as separate tasks; E2E *automated* tests are allowed.
- **No orphan code** (Kiro): "each prompt builds on the previous… ends with wiring things together. There should be no hanging or orphaned code."
- **Coverage check before output:** every AC is covered by ≥1 sub-task's tests; every sub-task maps to ≥1 AC.

### 2.2 Template — sub-task (one PR)

```markdown
## ST-<story>-<n>: <imperative, component-specific title>   ("Implement X", not "Support X")
- Story: US-<nnn>   Covers: AC-<nnn>.1, AC-<nnn>.3   Depends on: ST-…   Parallel: [P]|no
- Stack(s): api(.NET) | web(React) | mobile(Flutter/KMP) | node | py
- Complexity: <1-10> — <one-line reason>   Size budget: ≤ <N> LOC, ≤ <M> files
- Outcome (observable): <what exists/behaves after merge>
- Scope IN: <bullets>   Scope OUT: <explicitly deferred items → ST-…>
- Verify: `<exact command>` → <expected: e.g. "12 passed, 0 failed">
- Feature flag: <name|none>
- Risks / gotchas: <1-3 lines>
```

### 2.3 Template — split report

```markdown
# Split: US-<nnn>
Readiness: READY | NOT_READY (<failed INVEST items>)
Pattern used: <workflow|CRUD|rules|data|interface|major-effort|simple-complex|defer-NFR|spike> — why
Rejected alternative split: <pattern> — why worse (can't deprioritize / unequal sizes)
Order: ST-1 → ST-2 → (ST-3 [P], ST-4 [P]) → ST-5
Coverage: AC-1:ST-2,ST-5 | AC-2:ST-2 | AC-3:ST-3 ...
Deferred/follow-up stories proposed: US-<nnn>b (e.g. "performance: cursor pagination >10k rows")
```

---

## 3. `planner` agent — 02-plan.md

### 3.1 Rules (implementation plans for LLM implementers)

- **Audience = a skilled engineer with zero context** (superpowers): "Assume they write idiomatic code… What they cannot know is what you decided: which files, which names and signatures, which values from the spec, which tests prove each task. Document those."
- **A plan is the set of decisions the implementer cannot make alone.** Each step must let the implementer "write exactly one reasonable thing." Ban lines that decide nothing: "TBD", "handle edge cases", "add appropriate validation", "write tests for the above", or a type/function no task defines.
- **Map files before tasks.** List every file Create/Modify (with line ranges when modifying)/Test, one responsibility per file, "files that change together should live together. Split by responsibility, not by technical layer." Follow existing repo patterns (from repo-explorer output) over preferences.
- **Interfaces first.** For each unit: exact signatures (name, params, return types), DTO/schema shapes, OpenAPI operationIds, DB columns/constraints, event names. Add `Consumes`/`Produces` blocks so parallel/sequential implementers agree on names. Body code only for algorithms the signature+tests don't determine, or exact copy the spec fixes.
- **Exact tests.** Name every test (`Should_Return422_When_EmailInvalid`, `test_create_order_rejects_negative_qty`), its file, its key assertions with the spec's exact values, and the command to run it with expected output. TDD order: failing test → run (expect FAIL message) → implement → run (PASS) → commit.
- **Map tests to AC.** Every AC → ≥1 test; every E2E scenario named and tagged with AC IDs.
- **Global Constraints section** copied verbatim from spec/ADRs/constitution (versions, naming, error model, pagination, i18n rules) — every task implicitly inherits it.
- **Review Focus section** (superpowers): the ~5 input classes/failure modes the spec implies but no test covers (e.g., duplicate submit, unicode/RTL names, timezone boundaries, concurrent edit, empty list) — then add a test for each to the owning task.
- **Stop conditions** (explicit, for unattended runs): halt and report instead of improvising when: a required interface/file from the plan doesn't exist; a test must be deleted/weakened to pass (Anthropic harness: "unacceptable to remove or edit tests"); diff exceeds budget by > 50%; a new dependency not listed is needed; schema change is destructive; the plan contradicts an ADR/constitution; ≥ 2 failed fix attempts on the same failure (Anthropic: after two failed corrections, restart with a better prompt).
- **Per-stack sections** name the idioms to use (from constitution/skills), not generic advice.
- **Security notes are threat-specific**: list the concrete abuse cases for this change (IDOR on `/orders/{id}`, mass assignment on DTO, XSS in rich text) and the test for each.
- **Constitution/ADR check as a gate** (spec-kit "Constitution Check… Must pass before Phase 0… Re-check after Phase 1"); violations go in a Complexity Tracking table with "simpler alternative rejected because".
- **Proportion check:** plan shorter than expected diff; if code blocks dominate, replace with signatures+assertions.
- **Architect/editor split:** planner (strongest reasoning model, read-only) decides; implementer (cheaper/faster model) edits. Aider: architect+editor pairs beat solo models (o1-preview+DeepSeek 85.0%; same-model pairs also improved). Run implementation in a fresh context with plan + story only (Anthropic: "start a fresh session to execute it").
- **Self-review passes (run before emitting; fix inline):**
  1. Spec coverage — every AC/FR/NFR hook → task + test.
  2. Step scan — no decide-nothing lines; no transcribed bodies.
  3. Type/name consistency across tasks (`clearLayers()` vs `clearFullLayers()` is a bug).
  4. Review Focus filled (empty only if checked).
  5. Proportion.
  6. **Pre-mortem** (Klein): "assume the project has just failed" — write 3–5 concrete reasons the PR is rejected/rolled back and the mitigation now in the plan.
  7. **"What will the implementer get wrong?"** list: ambiguous names, similar existing helpers they might duplicate (Böckeler observed agents duplicating existing code), wrong layer placement, forgotten registrations (DI, routes, migrations, i18n keys, nav entries), generated-client regeneration.
  8. Optional fresh-context adversarial review subagent — flag only gaps affecting correctness/requirements (Anthropic: reviewers asked for gaps always find some; chasing all → over-engineering).

### 3.2 Template — 02-plan.md

````markdown
# Plan: ST-<id> <title>
**Story:** US-<nnn> | **Covers:** AC-… | **Depends on:** ST-… | **Complexity:** n/10 | **Budget:** ≤ N LOC
**Goal:** <one sentence>
**Approach:** <2-3 sentences>
**Stack:** <exact frameworks/versions from repo>

## Global Constraints   (verbatim from constitution/ADRs)
- Errors: RFC 9457 problem+json, type URIs `https://<app>/problems/<slug>`
- Collections: cursor pagination `?page_size&page_token` → `next_page_token`
- …

## Decisions
| # | Decision | Alternatives rejected | Why | ADR |
|---|----------|----------------------|-----|-----|

## Files
| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `src/Orders/CreateOrder/CreateOrderHandler.cs` | … |
| Modify | `src/Api/Program.cs:40-52` | register endpoint |
| Test   | `tests/Orders.IntegrationTests/CreateOrderTests.cs` | … |

## Interfaces / Contracts
- OpenAPI: `POST /orders` operationId `createOrder` → 201 `Order`, 422 `validation-error`, 409 `duplicate-order`
- `CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines) : IRequest<Result<OrderDto>>`
- DB: `orders(id uuid pk, customer_id uuid fk not null, status text check(...), created_at timestamptz)` + index `(customer_id, created_at desc)`
- Produces (for later tasks): … | Consumes (from earlier): …

## Tasks  (TDD; each ends green + commit)
### Task 1: <component>
- [ ] Write failing test `CreateOrder_Returns422_When_NoLines` — asserts status 422, `errors[0].field == "lines"`
- [ ] Run `dotnet test --filter CreateOrder_Returns422_When_NoLines` → FAIL (handler missing)
- [ ] Implement `CreateOrderValidator` in `…/CreateOrderValidator.cs`
- [ ] Run → PASS
- [ ] Commit `feat(orders): validate order lines`

## Test Plan
| Test | Level | File | AC |
|------|-------|------|----|
E2E scenarios (Playwright/integration_test): `@AC-012.1 customer places order and sees confirmation` — steps, data, expected; axe check on page.

## Per-stack notes
- .NET: … | React: … | Node: … | Python: … | Flutter: … | KMP: …   (only stacks touched)

## Security notes  (abuse case → control → test)
## Observability & audit  (spans, metrics, log fields, audit events)
## Migration & rollback  (expand/contract step; down-migration or forward-fix)
## Review Focus  (≤5 untested-by-default input classes → owning test added)
## Pre-mortem & likely implementer mistakes
## Stop conditions  (halt & report if …)
## Definition of Done
- [ ] All listed tests + full suite green: `<cmd>`   - [ ] lint/format/typecheck clean
- [ ] Diff within budget; no unlisted deps; no TODOs   - [ ] OpenAPI & generated clients in sync
- [ ] i18n keys added (all locales), a11y check green   - [ ] AC-… demonstrably met (evidence in PR body)
````

### 3.3 Greenfield architecture decisions the planner (or `/product`) must settle once, as ADRs, in Epic 1

Decide-once list (each an ADR; later plans cite, never re-decide):
1. **Repo layout.** Monorepo `apps/` (api, web, mobile) + `packages/` (contracts, generated clients, ui, config) — Turborepo/Nx convention; no nested packages; namespaced package names; `exports` fields. For .NET use a solution under `apps/api` with `src/` + `tests/`; Flutter/KMP under `apps/mobile`.
2. **Architecture style.** Modular monolith + vertical slices (feature folders) by default; no microservices until a named driver exists.
3. **API contract-first.** OpenAPI 3.1 single spec file as source of truth (Zalando rules 100/101); generate clients (Orval/openapi-typescript for React, Kiota/NSwag for .NET, openapi-generator for Dart/Kotlin); CI fails if spec and code drift.
4. **Error model.** RFC 9457 problem details (obsoletes RFC 7807): `type` (URI, default `about:blank`), `title`, `status`, `detail`, `instance`, extension members (letters/digits/_). Define a problem-type catalog.
5. **Pagination.** Cursor/opaque token (AIP-158: tokens opaque, not user-parseable; empty `next_page_token` at end; missing page_size ⇒ default, oversized ⇒ coerce; other params must be unchanged across pages). Must exist from v1.
6. **Versioning & compatibility.** Prefer no versioning + compatible evolution (Zalando 113, tolerant reader); if needed, pick one scheme (URL `/v1` is common but Zalando forbids it and prefers media-type) and record it. Breaking changes via expand/migrate/contract.
7. **Data & migrations.** DB schema + migration tool chosen before any data story (BMAD: "Schema definitions are created before data operations"); UTC timestamps; soft-delete policy; seed data.
8. **Auth.** Managed IdP/OIDC vs in-app; follow RFC 9700; session vs token for SPA (prefer BFF/cookie for web; PKCE for mobile).
9. **Naming conventions.** JSON casing (Zalando: snake_case; many .NET/JS teams use camelCase — pick one), path kebab-case.
10. **Testing strategy.** Levels & tools per stack, coverage floor, E2E runner, test data strategy (BMAD: "CRITICAL DECISION").
11. **Observability.** OpenTelemetry SDKs + exporter; log format; correlation IDs.
12. **i18n/a11y baselines**, **CI/CD & environments**, **feature flags**, **secrets management**.

### 3.4 Template — ADR (MADR 4 condensed; Nygard rules: numbered, never reused, superseded not deleted, 1–2 pages, written as a conversation with a future developer)

```markdown
---
status: proposed | accepted | superseded by ADR-00xx
date: YYYY-MM-DD
decision-makers: <agent/human>
---
# ADR-00nn: <short title: problem + chosen solution>
## Context and Problem Statement
<2-3 sentences, value-neutral forces; phrase as a question; scope explicit>
## Decision Drivers
- <quality attribute / constraint / NFR-id>
## Considered Options
- <A> - <B> - <C>
## Decision Outcome
Chosen: "<A>", because <meets k.o. driver X / best on drivers>.
### Consequences
- Good, because … - Bad, because … (list negatives too — Nygard)
### Confirmation
<how compliance is checked: lint rule, ArchUnit/NetArchTest, CI contract diff, test>
## Pros and Cons of the Options  (brief per option)
## More Information  (revisit trigger, links)
```

### 3.5 Template — constitution (project-wide, read by all agents; spec-kit concept)

```markdown
# Constitution (v<semver>, ratified <date>)
## Principles (MUST / SHOULD, each testable)
1. Contract-first: OpenAPI is source of truth; generated clients only.
2. Test-first: failing test before implementation; never weaken tests to pass.
3. Vertical slices; no cross-slice imports except via packages/contracts.
4. Errors = RFC 9457; collections paginated; UTC; i18n keys only.
5. Security baseline = ASVS L1 (+L2 for PII/payments); deny-by-default authZ.
6. PR ≤ 400 LOC (excl. generated); one concern per PR.
7. Simplicity: no new dependency/project without ADR (Complexity Tracking table).
## Governance: amendments via ADR; constitution conflicts are CRITICAL in analysis.
```

---

## 4. Estimation & PR-size policy (summary)

| Signal | Green | Split trigger |
|---|---|---|
| Changed LOC (excl. generated/lock/snapshots) | 25–200 | > 400 |
| Files touched | ≤ 10 | > 15 |
| Complexity (1–10) | ≤ 4 | ≥ 7 (or add spike) |
| Human-hours equivalent | ≤ 2–4 | > 1 day |
| AC covered | 1–3 | > 5 rules on story (Example Mapping) |
| Layers touched per sub-task | 1–2 (grid) | whole stack + migration + UI at once |

Complexity factor checklist (+1 each, cap 10): new external integration; data migration on existing rows; concurrency/transactions/locking; authZ-sensitive; new cross-cutting infra; UI with >3 states; unfamiliar library; touches >2 modules; ambiguous AC; performance target stated.

---

## 5. Minimal agent prompts (drop-in rule blocks)

**/product:** "Normalize the PRD into the template. Run the intake checklist; for each Missing/Partial item emit [NEEDS CLARIFICATION], choose the recommended default, and record it as A-###. Build a story map; mark the walking skeleton. Epic 1 = foundation + canary + all cross-cutting concerns. Stories: INVEST, vertical slices, P1..P3, depend only on lower IDs, numbered Given/When/Then AC including an unwanted-behavior and an authorization case; no unmeasured adjectives. Write ADRs for every CRITICAL DECISION. Run analysis passes (duplication, ambiguity, underspecification, coverage, inconsistency, ordering); do not publish with CRITICAL findings. Publish with gh --type/--parent/--blocked-by."

**story-splitter:** "Reject stories failing INVEST (except Small). Split using the flowchart order; prefer splits that let a piece be deprioritized, then equal sizes. Produce PR-sized sub-tasks via contract→data→backend→client→e2e grid; fold scaffolding into the consuming task; ≤ 400 LOC; complexity 1–10 with reasons; lower-ID dependencies; [P] where parallel-safe; only coding tasks; no orphan code; every AC covered."

**planner:** "Write for an engineer with zero context. Files table first, then interfaces with exact signatures, then TDD tasks with exact test names, assertions, commands and expected output. Copy Global Constraints verbatim. Add Security (abuse→control→test), Observability/Audit, Migration/Rollback, Review Focus, Pre-mortem, Likely-mistakes, Stop conditions, DoD. Self-review: coverage, step scan, name consistency, review focus, proportion. Never write TBD/‘handle edge cases’."

---

## Sources

- GitHub spec-kit templates: https://github.com/github/spec-kit/blob/main/templates/spec-template.md , https://github.com/github/spec-kit/blob/main/templates/plan-template.md , https://github.com/github/spec-kit/blob/main/templates/tasks-template.md , commands clarify/analyze/checklist: https://github.com/github/spec-kit/tree/main/templates/commands
- Kiro docs: https://kiro.dev/docs/specs/ , https://kiro.dev/docs/specs/feature-specs/ , https://kiro.dev/docs/specs/best-practices/ , https://kiro.dev/blog/specs-bugfix-and-design-first/ , https://kiro.dev/blog/property-based-testing/
- Kiro spec-agent system prompt (community capture): https://gist.github.com/notdp/19822831b54190bd9c6b34f6b69fadeb
- EARS (Mavin): https://alistairmavin.com/ears/
- BMAD-METHOD v4 templates/checklists: https://github.com/bmad-code-org/BMAD-METHOD/tree/v4.44.3/bmad-core (prd-tmpl.yaml, story-tmpl.yaml, story-draft-checklist.md, po-master-checklist.md)
- Taskmaster: https://github.com/eyaltoledano/claude-task-master (assets/example_prd.txt, example_prd_rpg.txt, docs/task-structure.md, src/prompts/parse-prd.json, analyze-complexity.json)
- OpenSpec: https://github.com/Fission-AI/OpenSpec (README, docs/concepts.md)
- Agent OS: https://buildermethods.com/agent-os
- Tessl: https://tessl.io/blog/tessl-launches-spec-driven-framework-and-registry , https://docs.tessl.io/
- Böckeler, Understanding SDD tools: https://martinfowler.com/articles/exploring-gen-ai/sdd-3-tools.html
- superpowers writing-plans: https://github.com/obra/superpowers/blob/main/skills/writing-plans/SKILL.md
- Claude Code best practices: https://code.claude.com/docs/en/best-practices
- Anthropic, Effective harnesses for long-running agents: https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents
- Aider architect/editor: https://aider.chat/2024/09/26/architect.html
- METR time horizons: https://metr.org/blog/2025-03-19-measuring-ai-ability-to-complete-long-tasks/ , https://metr.org/time-horizons/
- Humanizing Work splitting guide + flowchart: https://www.humanizingwork.com/the-humanizing-work-guide-to-splitting-user-stories/ , https://www.humanizingwork.com/wp-content/uploads/2020/10/HW-Story-Splitting-Flowchart.pdf
- SPIDR (Cohn): https://www.mountaingoatsoftware.com/blog/five-simple-but-powerful-ways-to-split-user-stories
- Cohn, dangers of DoR: https://www.mountaingoatsoftware.com/agile/the-dangers-of-a-definition-of-ready
- Patton, The New Backlog (story map): https://jpattonassociates.com/the-new-backlog/
- Walking skeleton (Cockburn, summarized): https://www.henricodolfing.ch/en/start-your-project-with-a-walking-skeleton/
- INVEST/SMART (Wake): https://xp123.com/invest-in-good-stories-and-smart-tasks/
- Given-When-Then (Fowler): https://martinfowler.com/bliki/GivenWhenThen.html
- Example Mapping (Wynne): https://cucumber.io/blog/bdd/example-mapping-introduction/
- Google small CLs: https://google.github.io/eng-practices/review/developer/small-cls.html
- Graphite PR size: https://graphite.com/blog/the-ideal-pr-is-50-lines-long , https://graphite.com/research/median-pr_size
- SmartBear/Cisco review study: https://smartbear.com/learn/code-review/best-practices-for-peer-code-review/
- Klein premortem (HBR): https://hbr.org/2007/09/performing-a-project-premortem
- Nygard ADRs: https://www.cognitect.com/blog/2011/11/15/documenting-architecture-decisions
- MADR template: https://github.com/adr/madr/blob/develop/template/adr-template.md
- RFC 9457: https://www.rfc-editor.org/rfc/rfc9457.html
- RFC 9700 OAuth 2.0 Security BCP: https://www.rfc-editor.org/rfc/rfc9700.html
- Google AIP-158 pagination: https://google.aip.dev/158
- Zalando REST guidelines: https://opensource.zalando.com/restful-api-guidelines/
- Fowler ParallelChange: https://martinfowler.com/bliki/ParallelChange.html
- Bogard vertical slices: https://www.jimmybogard.com/vertical-slice-architecture/
- Turborepo repo structure: https://turborepo.dev/docs/crafting-your-repository/structuring-a-repository
- WCAG 2.2 quickref: https://www.w3.org/WAI/WCAG22/quickref/
- OWASP ASVS 5.0: https://owasp.org/www-project-application-security-verification-standard/
- OpenTelemetry semantic conventions: https://opentelemetry.io/docs/concepts/semantic-conventions/
- GitHub CLI sub-issues/types/dependencies (v2.94): https://github.blog/changelog/2026-06-10-manage-sub-issues-types-and-dependencies-from-github-cli/
