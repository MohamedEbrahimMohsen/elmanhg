VERDICT: APPROVED

# CodeRabbit verify — PR #140 (#58, E1.S5)

## Blocking
None.

## Verified
- **IMPLEMENT #1 (RC1):** `docs/audit-log.md:3` now carries the triage's replacement sentence word for word. It matches the code: `api/core-libraries/Core.Auditing/AuditBehaviour.cs:60-69` catches any `AppendAsync` exception, logs it and does not rethrow, so the command's result is still returned and the command has no row. The "(see How it works)" pointer resolves to `docs/audit-log.md:7`.
- **IMPLEMENT #2 (RC2):** `docs/audit-log.md:10` now carries the triage's replacement wording word for word. It matches the code: `TeachersController.cs:17,26` put `[Authorize(Policy = ...)]` on both actions, and those checks run before `mediator.Send` (`:21,30`). `SubjectScopeBehaviour` is registered from `Elmanhg.Application/DependencyInjection.cs:16`. `AddCoreAuditing` is registered before `AddCoreCQRS` at `Program.cs:59-60`, so failures raised inside the pipeline are audited.
- **Scope:** `git status` shows only `docs/audit-log.md` modified, plus untracked `.process` files. The diff touches exactly lines 3 and 10. `01-plan.md` was not edited, as the triage required.
- **Rework claims:** "Deviations: None." is confirmed. "Build & test: Not run" is acceptable because the change touches docs only and no code.
- **Docs sync:** after the edit, the doc and the code give the same answer on best-effort persistence and on 403 auditing. No divergence remains.

## Non-blocking
None.
