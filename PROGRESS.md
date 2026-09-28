# Elmanhg — autopilot progress

Last updated: 2026-09-28, cloud session, after story #65 merged (main at `d4429a1`).
The laptop run stopped here because the dev asked it to. A Claude Code cloud session resumes at **#65**
(see "Running in a cloud session" below).

Board: https://github.com/users/MohamedEbrahimMohsen/projects/1

## Finished stories (10 of 59)

| # | Story | PR | Review rounds | CodeRabbit | Follow-up issue |
|---|---|---|---|---|---|
| 1 | #54 [E1.S1] Bootstrap backend (.NET 10, DDD/CQRS, PostgreSQL) | #131 | 2 | skipped (too many files) | #132 |
| 2 | #55 [E1.S2] Bootstrap frontend (React, Vite, RTL) | #133 | 2 | skipped (too many files) | #134, #135 (dev decision) |
| 3 | #56 [E1.S3] Authentication with phone OTP and email | #136 | 2 | skipped (too many files) | #137 |
| 4 | #57 [E1.S4] Role-based authorisation + teacher subject scoping | #138 | 1 | 1 comment, rejected | #139 |
| 5 | #58 [E1.S5] Audit log | #140 | 2 + 2 CI-fix rounds | 2 comments, fixed | — |
| 6 | #60 [E2.S1] Subject and unit CRUD with ordering | #141 | 1 | skipped (too many files) | #142 |
| 7 | #61 [E2.S2] Lesson authoring | #143 | 2 | skipped (too many files) | #144 |
| 8 | #62 [E2.S3] Lesson lifecycle (+ lesson reorder and delete) | #145 | 1 | 1 comment, fixed | #146 |
| 9 | #64 [E3.S1] Question aggregate with typed body and grading spec | #147 | 2 | skipped (too many files) | #148 |
| 10 | #65 [E3.S2] Admin question editor, live preview, test grader | #150 | 2 | skipped (too many files) | #151 |

Other PRs: #129 (docs, prototype, tooling), #130 (pipeline setup for this repo).
Per-story plans, reviews and metrics live in `.process/<issue>-<slug>/`.

## Remaining stories (49), in run order

Stories run in dependency order, not issue order. E10 (payments) comes before E7 because free-tier gating needs entitlements.

| Order | Issue | Story |
|---|---|---|
| 2 | #66 | [E3.S3] Bulk question import from spreadsheet |
| 3 | #67 | [E3.S4] Servable rule |
| 4 | #68 | [E3.S5] Teacher validation queue |
| 5 | #70 | [E4.S1] Arabic answer normalisation |
| 6 | #71 | [E4.S2] Graders for mcq, multi-select, true/false |
| 7 | #72 | [E4.S3] Graders for fill-in-the-blank and short answer |
| 8 | #74 | [E5.S1] Attempt log and session model |
| 9 | #75 | [E5.S2] Adaptive question selection |
| 10 | #76 | [E5.S3] Quiz screen with immediate feedback |
| 11 | #77 | [E5.S4] Mastery calculation and headline counter |
| 12 | #78 | [E5.S5] Progress page |
| 13 | #80 | [E6.S1] Exam blueprint authoring |
| 14 | #81 | [E6.S2] Unit exam generation and sitting |
| 15 | #82 | [E6.S3] Multi-unit exam builder |
| 16 | #83 | [E6.S4] Retakes and best score |
| 17 | #99 | [E10.S1] Plan catalogue and subscription state |
| 18 | #100 | [E10.S2] Paymob checkout integration |
| 19 | #101 | [E10.S3] Webhook-driven entitlement |
| 20 | #102 | [E10.S4] Refunds and payment log |
| 21 | #85 | [E7.S1] Subject, unit and lesson browsing |
| 22 | #86 | [E7.S2] Landing page and onboarding |
| 23 | #87 | [E7.S3] Free tier limits |
| 24 | #89 | [E8.S1] AI service skeleton (Python FastAPI) |
| 25 | #90 | [E8.S2] Lesson content retrieval |
| 26 | #91 | [E8.S3] Avatar chat with context bundles |
| 27 | #92 | [E8.S4] Conversation logging |
| 28 | #94 | [E9.S1] Thread creation with attached context and quota |
| 29 | #95 | [E9.S2] Teacher inbox, claiming and text replies |
| 30 | #96 | [E9.S3] Voice replies with transcription |
| 31 | #97 | [E9.S4] SLA timers, reminders and follow-up rules |
| 32 | #104 | [E11.S1] Dashboard metrics queries |
| 33 | #105 | [E11.S2] Dashboard UI |
| 34 | #106 | [E11.S3] Student and teacher administration |
| 35 | #107 | [E11.S4] Teacher personal stats card |
| 36 | #109 | [E12.S1] Append-only training records |
| 37 | #110 | [E12.S2] JSONL export |
| 38 | #112 | [E13.S1] Hosting and environments |
| 39 | #113 | [E13.S2] Observability |
| 40 | #114 | [E13.S3] Performance targets |
| 41 | #115 | [E13.S4] Security hardening |
| 42 | #117 | [E14.S1] Essay question authoring with rubric (v2) |
| 43 | #118 | [E14.S2] LLM essay grader (v2) |
| 44 | #119 | [E14.S3] Student essay input (v2) |
| 45 | #121 | [E15.S1] Math step input component (v2) |
| 46 | #122 | [E15.S2] CAS final answer check (v2) |
| 47 | #123 | [E15.S3] LLM step grading (v2) |
| 48 | #125 | [E16.S1] Admin diagram authoring tool (v2) |
| 49 | #126 | [E16.S2] Student canvas and grading (v2) |
| 50 | #128 | [E17.S1] Review queue and override (v2) |

After the last story, write `docs/implementation-report.md` (see `.claude/commands/feature.md`, "Board report").

## How each story is run

The flow is `.claude/commands/feature.md`. Every stage is a fresh subagent: `feature-planner`, `feature-implementer` and `feature-reviewer`, all Opus 5.5.

1. **Pre-flight and start:** `python scripts/pipeline_orch.py start <issue>`. It checks for a clean tree, switches to main and pulls, cuts the `feature/<n>-<slug>` branch, and writes the `.process/` folder with `00-acceptance.md`, `00-story.md` and `04-metrics.md`.
2. **Plan:** the planner writes `01-plan.md`. Under autopilot the plan gate auto-approves, and the approval is appended to `00-acceptance.md`.
3. **Implement:** the implementer writes `02-implementation.md`.
4. **Review:** a fresh reviewer writes `03-review.md`. If it requests changes, a fresh implementer does the rework (`02-implementation-r2.md`), then a fresh reviewer re-reviews (`03-review-r2.md`). There is no round 3.
5. **PR:** `python scripts/pipeline_orch.py pr <issue>` commits and opens the PR.
6. **CodeRabbit:** `python scripts/pipeline_orch.py poll <issue> <pr>`. If there are actionable comments: a fresh reviewer triages (`06`), a fresh implementer fixes (`07`), a fresh reviewer verifies (`08`), then `pipeline_orch.py push <issue> "<msg>"`.
7. **Merge:** `python scripts/pipeline_orch.py merge <issue> <pr>`. It waits for CI, squash-merges, syncs main and closes the story.
8. **Follow-ups:** `python scripts/pipeline_orch.py issue deferred "<title>" <body-file> <issue>` records deferred items and non-blocking notes.
9. **Metrics:** `python scripts/pipeline_orch.py metric <issue> "#|Stage|Agent|Model|duration_ms|tokens|tool_uses|outcome"` after each stage, using the numbers the subagent reports.

## Conventions and decisions the next session must know

- **Agent definitions are cached at session start.** Edits to `.claude/agents/*.md` are not seen by already-registered agent types. Every agent prompt therefore starts with: "First read your current role definition at `.claude/agents/<name>.md` and follow it; where it differs, the file wins." Keep doing this.
- **Keep subagent final messages short.** Prompts cap them at 5–15 lines, and the full content goes to the `.process` file. Without the cap, the orchestrator's context fills with plans.
- **CI parity:** `api/Elmanhg.Api/appsettings.json` is gitignored, and CI has none. Implementers and reviewers run `dotnet test api/ -c Release` with that file moved aside, and restore it afterwards. New config keys go into `appsettings.example.json`, the test `ApiFactory`, and safe code defaults. The #58 lesson: auditing was silently off in production because its only "on" switch was in the gitignored file. Product-critical switches default ON in code.
- **Test factory config** must use `builder.UseSetting(...)`. Settings added through `ConfigureAppConfiguration` apply too late for values read while `Program` registers services.
- **Migrations:** every migration story adds one entry to the hard-coded list in `AppDbContextTests.Migrate_FreshDatabase_LeavesNoPendingMigrations`. That is the accepted pattern, not a test-integrity violation.
- **Generated artifacts are committed and CI checks them for drift:** `api/openapi/v1.json`, which `dotnet build` generates, and the Orval client in `web/src/shared/api/generated`, from `npm --prefix web run gen:api`. Regenerate both whenever an endpoint changes.
- **Postman:** `postman/elmanhg.postman_collection.json` must mirror the API in the same change. Requests in a folder run top to bottom, so order them to respect state transitions (the #62 CodeRabbit catch).
- **Morabh reuse-first:** look in `D:\Personal\Projects\Projects\Morabh\repos\apis` before designing any cross-cutting feature. The Core libraries are vendored in `api/core-libraries/`, switched to PostgreSQL. Plans name the Morabh source file for each reused piece.
- **External providers** (SMS, object storage, Paymob, Claude API, transcription) sit behind interfaces with `Fake*` or local implementations selected by config. The real adapters are tracked as `deferred` issues.
- **Access rules:**
  - Every endpoint needs a named policy from `DefaultCodes` / `PermissionMatrixPolicies`. A test fails on any unpoliced endpoint.
  - Teacher reads of subject-owned data go through `ISubjectScopedRequest` / `SubjectScopeBehaviour`, which fails closed and returns 403.
  - Admins can never approve questions; approval only happens through `Question.Approve(TeacherSubject)`.
- **Auditing:** mark commands `IAuditableCommand` and entities `IAuditedEntity`. The audit table is append-only, enforced by a database trigger. Details are in `docs/audit-log.md`.
- **Student-facing lesson reads return Published lessons only** (PRD §5.2). The servable rule for questions is story #67.
- **Graders already exist (from #65):** the Arabic answer normaliser and deterministic graders for all five v1 types live in `api/Elmanhg.Domain/Questions/Grading/`, with `POST /api/questions/grade-draft`. #70–#72 extend them rather than create them: #70 adds per-rule toggles and the Egyptian spelling corpus, and #71/#72 add feedback text. Edge cases are in #151. `Question.Reject` and `Resubmit` exist; #68 adds the teacher approve/reject commands and UI.
- **Question content** is jsonb, one schema per type, documented in `docs/question-schemas.md`. A content edit on an Approved question sends it back to Pending, bumps the version, and saves a `QuestionRevision`.
- **Web:**
  - Every visual value comes from `.claude/design-system.md` tokens (Glass, light only).
  - Heavy editors (TipTap, KaTeX) are lazy-loaded in their route chunk; `web/package.json` has `sideEffects: ["**/*.css"]`.
  - The Testing Library async timeout is 3000 ms in `web/src/test/setup.ts`.
  - Mobile tab bar: 3 items + "المزيد". Awaiting dev confirmation in #135.
- **Docs-sync rule** (`.claude/rules/docs-sync.md`): a change that alters behaviour must update the owning doc in `/docs` in the same PR. Reviewers block on divergence. This file sits at the repo root because the dev asked for it by name; strictly, the rule says docs live in `/docs`.

## Running in a cloud session

The run continues in a Claude Code cloud container from 2026-09-28. The dev approved full autopilot there:
per-story `feature/<n>-<slug>` branches, PRs, and squash-merge on green CI.

- **Toolchain:** run `bash scripts/cloud-setup.sh` after any container restart. It starts Docker, which
  Testcontainers needs, and copies .NET SDK 10.0.401 out of `mcr.microsoft.com/dotnet/sdk:10.0` because the
  dotnet download host is blocked. Baseline on `ed2d60f`: api 665/665 and web 240/240, with typecheck, lint
  and format clean.
- **No `gh` CLI.** `pipeline_orch.py` detects this. `start` reads the issue from the public REST API, `pr`
  pushes and writes the PR body to a temp file, and `merge` only pushes the remaining artifacts. The
  orchestrator opens PRs, polls CodeRabbit and CI, merges, closes stories and opens issues through the GitHub
  MCP tools, then runs `pipeline_orch.py sync`.
- **Morabh** is cloned read-only at `/home/user/apis`, not `D:\...\Morabh\repos\apis`. Agent prompts pass
  this path.
- **Node:** the container has Node 22, while CI uses 24 (`web/.nvmrc`). The baseline passed on 22. Treat
  web-ci as authoritative.
- **#135** (mobile tab bar: 3 items + "المزيد") was confirmed by the dev on 2026-09-28 and closed.

## Gotchas

- **CodeRabbit (free plan):**
  - It skips any PR over 100 files, which is most stories here.
  - It is rate-limited. After a "Review limit reached — next review in N minutes" notice, wait N+1 minutes, comment `@coderabbitai review`, and poll again. `pipeline_orch.py poll` does this once.
- **Git credentials:** this repo pins github.com to `gh auth token --user MohamedEbrahimMohsen` through a repo-local credential helper, so git never shows the account picker. `gh` must stay signed in to that account.
- **Local-only noise:** `prettier --check` fails on CRLF endings in the Windows checkout, and `dotnet format` flags whitespace only inside the vendored `core-libraries`. CI (Linux) is clean. Neither counts as a finding.
- **Visual Studio's `api/.vs/` cache:** now in `.gitignore`. Earlier it blocked the pre-flight stash.
- **Flaky web test:** `RichTextEditor.test.tsx` "inserts an inline formula" sometimes times out under `--coverage` (tracked in #148). Re-run CI once before treating a web-ci failure as real.
- **Postgres port:** another project's container uses 5432 on this machine. For a local `docker compose up`, map the database to another port such as 55432 in `.env`.
- **Metrics:** start and finish times in `04-metrics.md` for #54–#58 were approximate. From #60 on they are computed from the agents' reported durations.
- **Local dev config:** after each story that adds config keys, copy the new sections from `appsettings.example.json` into your local `appsettings.json`, or `dotnet run` fails its startup validation.

## Open issues created by the run

`deferred`: #132, #134, #137, #139, #142, #144, #146, #148, #151 · `dev-decision`: none open (#135 confirmed and closed).
