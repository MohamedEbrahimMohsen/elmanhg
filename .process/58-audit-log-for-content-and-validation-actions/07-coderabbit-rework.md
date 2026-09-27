# CodeRabbit rework — PR #140 (#58, E1.S5)

| # | Change | File:line |
|---|---|---|
| 1 | Replaced "One append-only row per audited command in the `AuditLogs` PostgreSQL table." with the best-effort wording given in the triage, verbatim. | `docs/audit-log.md:3` |
| 2 | Replaced "including validation (422) and authorisation failures." with the wording given in the triage, verbatim: only authorisation failures raised inside the pipeline (e.g. `SubjectScopeBehaviour`) are audited; ASP.NET `[Authorize]` denials happen before `mediator.Send`. | `docs/audit-log.md:10` |

## Deviations
None.

## Build & test
Not run. The change is docs-only and touches no code.
