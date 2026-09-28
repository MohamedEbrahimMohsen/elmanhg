VERDICT: APPROVED

# Review — [E2.S1] Subject and unit CRUD with ordering (#60)

## Blocking
None.

## Non-blocking
- `web/src/features/content/components/UnitPanel.test.tsx:29` — the flaky run the implementer reported (1 of 5 coverage runs). The first `findByRole("button", { name: "Show units" })` uses the RTL default 1 s timeout while the lazy `/admin/content` route loads cold under coverage instrumentation. I could not reproduce it: 3/3 plain runs and 5/5 runs of the exact CI command (`npm test -- --run --coverage`, `.github/workflows/web-ci.yml:42`) passed. The same pattern (`renderApp` followed by a first `findBy*` at the default timeout) already exists in `AuditLogPage.test.tsx:53`, `LoginPage.test.tsx` and others. So this is a harness-wide cold-start margin, not a defect this story added, and I cannot write a Failure line for it. If CI shows it again, raise `asyncUtilTimeout` once in `src/test/setup.ts` (harness fix, all suites), or quarantine per react-testing.md "Flakiness". Do not add a per-test timeout.
- `web/src/features/content/pages/ContentPage.test.tsx` — 209 lines. This breaks react-feature §1 ("No file over 200 lines") and the plan DoD line "No web file is over 200 lines". The implementer disclosed it under Notes, not under Deviations. Trim it in any later rework (for example, merge the repeated `findSubjects` + `getByLabelText` preambles into a helper).
- `web/src/shared/lib/http.ts:55-59` — the empty-body fix (declared deviation) is correct and needed: the `Ok()` actions return 200 with no body. It is only covered indirectly (the "Subject moved." / "Unit deleted." toasts would never appear if `parse` threw). A direct case in `http.test.ts` ("200 with empty body resolves undefined") is worth adding when the plan allows editing that file.
- `api/Elmanhg.Tests/Integration/Content/SubjectsEndpointTests.cs:253` — `position` is always the current order of A (in practice 1), so this proves "move to the front" rather than "move before an arbitrary sibling". The implementer note explains why (the shared DB has many Order-1 subjects). Acceptable, but it constrains less than the name suggests.
- `api/Elmanhg.Application/Subjects/ReorderSubject/ReorderSubjectHandler.cs:18` — loads and tracks every live subject on each move. Correct per D4 and fine at curriculum scale; noted only in case the subject count ever grows large.
- `docs/PRD.md` §10.1 says "ordering (drag to reorder)". The build ships up/down only, and the plan puts drag out of scope because the story allows "drag or up/down". Drag is not built yet, which is incompleteness, not divergence, so it is not a finding.

## Verified
- Build: `dotnet build api/` succeeded with 0 warnings and 0 errors.
- API tests: `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside gave total 372, failed 0 (file restored afterwards). This matches the 372/372 claim in the report.
- Web: `npm --prefix web run build` exits 0. `npm --prefix web test -- --run` passed 3/3 (33 files, 167 tests). CI command with `--coverage` passed 5/5 (167 tests).
- Deviation 1, `AppDbContextTests.cs:23`: only a `fifth => …_AddSubjectOrderAndUnits` entry was appended. Nothing was removed or loosened, and the test would fail without it once the plan migration exists. This is a legitimate intentional-behaviour update (SKILL §8.11) and it was declared.
- Deviation 2, `http.ts`: declared, minimal, and the existing `http.test.ts` still passes.
- Deviations 3–4, NameForm cancel label and the 1-based `position` in SubjectCard: both declared. `SubjectCard.tsx:36` calls `targetPosition(position - 1, …)` and `UnitPanel.tsx:51` calls `targetPosition(index, …)`. Both give the correct 1-based target (up = index, down = index + 2), matching the handler `Math.Min(Position, count) - 1` insert.
- Intent: all 8 audited commands, 2 queries, teacher list filtering (`GetSubjectsHandler.cs:24-31`), 403 via `ISubjectScopedRequest` on detail (`GetSubjectQuery.cs:7`), and the admin UI at `/admin/content` (`routes/admin/content.tsx:5`). Lesson and question counts are properly deferred, since those entities do not exist yet.
- Correctness checked by hand:
  - The reorder clamp (`Math.Min(Position, siblings.Count + 1) - 1` after `Remove`) puts position >= n at the end and 1 at the front.
  - `MoveTo` no-op skips stamping on unchanged siblings (D16).
  - The D12 ownership check is in both `UpdateUnitHandler.cs:19` and `DeleteUnitHandler.cs:19`. Reorder filters by `SubjectId`, so a foreign unit returns 404.
  - `AnyInSubjectAsync` respects the soft-delete filter, so deleted units do not block a subject delete.
  - `CreateUnit` on a deleted subject returns 404 through the global filter.
- Contract fidelity: every file #1–#89 exists with the planned signatures. Nothing extra beyond the two declared deviations. Migration `20260928000438_AddSubjectOrderAndUnits.cs` has AddColumn (default 0), then the ROW_NUMBER backfill, then CreateTable `Units` (FK Restrict) and index `(SubjectId, Order)`. There is no Drop or Rename in `Up`.
- Skill §9 / §8:
  - All commands and queries are `sealed record`; validators and handlers are `sealed class`.
  - File-scoped namespaces, no comments, no try/catch, and `.ConfigureAwait(false)` on every await.
  - One `SaveChangesAsync` per mutation. `asNoTracking: true` on every non-mutating read.
  - `DateTimeOffset.UtcNow` only. Guard grep over the new API code is clean.
  - Caps live in `ContentOptions` (`ValidateDataAnnotations().ValidateOnStart()`, `DependencyInjection.cs:20`), not in entity constants (§8.1, §8.12).
  - The soft-delete filter is added only in the global method (`AppDbContext.cs:71`).
  - `DbSet { get; set; }` and a named `ConfigureUnits` method.
  - Policies use `DefaultCodes.ContentBrowse` / `ContentManage` on every action.
  - All 10 codes are in both resx files. The 9 UI-reachable codes are in web `errors.*` in en and ar.
- Web (react-feature + design system):
  - Tokens only. The §14/§16 greps over `src/features/content` are clean. `text-h1`, `text-h2`, `text-ui`, `text-caption`, `shadow-1` and `bg-bg` exist in `app.css`.
  - No hard-coded strings; en/ar key parity is 33/33. ICU Arabic plural for `subjects.unitCount`.
  - Loading, empty and error+retry states exist for both the subject list and the unit panel.
  - Up/down buttons have `aria-label`s and are disabled at the edges. Inline delete confirmation. The disclosure uses `aria-expanded`/`aria-controls`.
  - Every mutation invalidates and toasts. Create and rename errors map to the field through `Form`.
  - `ItemActions.tsx` is 120 lines (at the limit, not over).
- Postman: a "Subjects" folder (6 requests) and a "Units" folder (4 requests). Correct methods and URLs, bearer auth inherited from the collection, plausible JSON bodies, and Create requests store `subjectId`/`unitId`. No stale requests. The only removed line is the `subjectId` variable being re-declared.
- Docs sync: `docs/audit-log.md` lists all 8 commands with the correct ResourceType and Id source, and the audited entities `TeacherSubject`, `Subject`, `CurriculumUnit`. The "join later" sentence was updated so it no longer contradicts the table. There is no `docs/architecture.md` in this repo. `docs/claude-design-prompt.md` §4 (`#/admin/content` tree with reorder) agrees with the UI. No divergence.

## Test quality
- `SubjectTests` / `CurriculumUnitTests`: constrain every branch, including trim, the order guard, the same-order no-stamp case, and the has-units guard leaving `IsDeleted` false.
- `Create*HandlerTests`: capture the added entity via `Arg.Do` and assert Order = last + 1, SubjectId, creator and result Id. Removing the `+ 1` or the `?? 0` would fail them.
- `Reorder*HandlerTests`: assert the full renumbering, including the clamp. They would catch an off-by-one in the insert index.
- `Update/DeleteUnitHandlerTests`: the other-subject cases assert 404 and unchanged state, so they would catch a missing D12 check.
- `DeleteSubjectHandlerTests`: the has-units case asserts the domain code and that `IsDeleted` stays false.
- `GetSubjectsHandlerTests.Handle_Teacher_QueriesOnlyAssignedSubjects`: compiles the predicate against assigned and other subjects and asserts `GetAllAsync` was not received. Not vacuous.
- `GetSubjectHandlerTests`: maps real entities through the generator. Not vacuous.
- Throw paths: every one asserts type, code and `SaveChangesAsync` `DidNotReceive()`. Every success path asserts `Received(1)`.
- Validator tests: one passing case and one failing case per rule, asserting the specific code.
- Integration tests: go over HTTP and assert status plus `code` plus DB state through a fresh scope. Audit rows are checked for Success on create and delete, and for Failure with `SUBJECT_HAS_UNITS`. Reads use seeded ids only, never whole-list equality.
- Web: tests capture request paths and bodies, assert the toasts and the disabled edge buttons, cover the cancel path sends no DELETE, and include axe and an RTL render. No mocked hooks; MSW only.
- Vacuous tests: none found.
