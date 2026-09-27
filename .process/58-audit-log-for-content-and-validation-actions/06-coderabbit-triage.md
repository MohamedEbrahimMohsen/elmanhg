TRIAGE: 2 to implement, 0 rejected, 0 dev-decisions

# CodeRabbit triage — PR #140 (#58, E1.S5)

Each claim below was checked against the working tree. The plan (`01-plan.md`) and both reviews (`03-review.md`, `03-review-r2.md`) were read for context.

## IMPLEMENT

### 1. RC1 (Major): the doc summary promises one row per command, but the append is best-effort
**Where:** `docs/audit-log.md:3` (the doc's own `docs/audit-log.md:14` and `api/core-libraries/Core.Auditing/AuditBehaviour.cs:60-69` say otherwise)
**Verified:** Confirmed. `AuditBehaviour.cs:61-69` catches any `AppendAsync` exception, logs it and returns the command result, so a failed insert leaves no row. Best-effort is a deliberate, approved decision: plan step 5 (`01-plan.md:195`) keeps Morabh's comment "Audit persistence must never break the request it describes", and `docs/audit-log.md:14` already says so. The only problem is the absolute wording at line 3, which contradicts line 14 in the same doc and the code.
**Smallest fix:** In `docs/audit-log.md:3`, replace "One append-only row per audited command in the `AuditLogs` PostgreSQL table." with: "Each audited command appends one row to the append-only `AuditLogs` PostgreSQL table. The append is best-effort: if it fails, the error is logged and the command's result is still returned, so that command has no row (see How it works)." Change nothing else.
**Rejected part:** Do not edit `.process/.../01-plan.md:4`. The plan is a historical pipeline record, not an owned doc (`.claude/rules/docs-sync.md`: docs live in `/docs` only), and its step 5 already records best-effort. CodeRabbit's other option, making the audit write durable before reporting success, is a heavy redesign that goes against the approved plan. It is out of scope for this PR. If the dev reads PRD §17 rule 13 (`docs/PRD.md:497`) as requiring fail-closed auditing, that belongs in a follow-up story.

### 2. RC2 (Minor): the doc says ASP.NET authorisation denials are audited, but they never reach the pipeline
**Where:** `docs/audit-log.md:10`
**Verified:** Confirmed. `TeachersController.cs:17,26` put `[Authorize(Policy = DefaultCodes.UsersManage)]` on both actions, so a policy denial returns 403 before `mediator.Send` (`:21,30`), and `AuditBehaviour` never runs. The only in-pipeline authorisation check is `SubjectScopeBehaviour` (`api/Elmanhg.Application/Shared/Authorization/SubjectScopeBehaviour.cs`, registered after auditing). Its failures are audited, which is why the doc sentence is only partly true. As written, the doc and the code give two different answers to "is a 403 audited?", which is a docs-sync divergence.
**Smallest fix:** In `docs/audit-log.md:10`, replace "including validation (422) and authorisation failures." with "including validation (422) failures and authorisation failures raised inside the pipeline (for example `SubjectScopeBehaviour`). ASP.NET `[Authorize]` policy denials happen before `mediator.Send`, so they are not audited." Leave the rest of the bullet as it is.

## REJECTED
None.

## DEV-DECISION
None.

## Not actionable
- PC1 (review body): summary only. It repeats RC1 and RC2 and asks for no further change.
