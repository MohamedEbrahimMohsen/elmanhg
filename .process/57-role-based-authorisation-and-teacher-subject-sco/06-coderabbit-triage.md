TRIAGE: 0 to implement, 1 rejected, 0 dev-decisions

# CodeRabbit triage: PR #138 (E1.S4, story #57)

## Implement

None.

## Rejected

### RC1: map the concurrent unique-index violation to 409 in `AssignTeacherSubjectHandler`
**Where:** `api/Elmanhg.Application/Teachers/AssignTeacherSubject/AssignTeacherSubjectHandler.cs:34-42`
**Claim verified:** Yes, partly. Two concurrent assigns of the same (TeacherId, SubjectId) can both pass `IsAssignedAsync` (line 34). The filtered unique index then rejects the second insert at `SaveChangesAsync` (line 42). Nothing in `api/` or `api/core-libraries` maps `DbUpdateException` or `PostgresException` (grep found no matches). `Core.Exceptions/ExceptionMiddleware.cs:25` catches it generically and returns 500. The prior review (03-review.md, Non-blocking) already recorded this.
**My severity:** Minor. The data stays correct because the index stops the duplicate. Only the status code is wrong (500 instead of 409), and only when an admin double-submits within milliseconds. This is not a security or data-loss issue.
**Why rejected:** The suggested fix breaks house rules that outrank CodeRabbit's generic advice:
- `docs/constitution.md:143` says "no `try`/`catch` in handlers — throw `Core.Errors` exceptions; `Core.Exceptions` middleware formats responses."
- SKILL.md §5 (line 503, 539) and the §9 checklist (line 1057) say handlers must have no `try`/`catch`.
- SKILL.md §6.8 (line 741) says persistence-conflict mapping to 409 belongs in `CoreExceptionMiddleware`, "never a handler `try`/`catch`".
- The fix would also make `Elmanhg.Application` depend on `Npgsql` and `Microsoft.EntityFrameworkCore`, and would hard-code an index name inside a handler.

The approved way to fix this is a cross-cutting change: map PostgreSQL `23505` to `ConflictCoreException` in the `Core.Exceptions` middleware or in the infrastructure layer. That would change every endpoint, which is outside this story's scope. It should be raised as a follow-up issue, not bolted onto this handler.

## Dev-decisions

None.

## Other

- PC1 (review body): only a summary that points to RC1. No separate action.
