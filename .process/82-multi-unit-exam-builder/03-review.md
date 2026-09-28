VERDICT: CHANGES_REQUESTED

# Review — [E6.S3] Multi-unit exam builder (#82)

## Blocking

### 1. `docs/sessions.md` still says every exam's ScopeKey is `unit:<guid>`
**Where:** `docs/sessions.md:17` (§ Model, `ScopeKey` row) vs `api/Elmanhg.Domain/Sessions/MultiUnitExamScope.cs:8`
**Rule:** `.claude/rules/docs-sync.md` (divergence: naming/format contracts); review order #8
**Problem:** The change adds a second exam key format, `units:{size}:{sorted ids}`. The ScopeKey row still reads "exams use `unit:<guid>` through `UnitExamScope` (Domain)". The same doc now lists `MultiUnitExam` as a live session kind (lines 3 and 15), so for a multi-unit exam the doc and the code give two different answers about the stored key. `docs/exams.md` § Multi-unit exams has the right format, so the repo contradicts itself. The plan's Docs table only changed sessions.md lines 3 and 15 and missed this row.
**Failure:** A multi-unit session is stored with `ScopeKey = "units:20:<id1>,<id2>"`. Following sessions.md, a reader (or #83's best-score/attempts grouping) expects `unit:<guid>` for every exam and would never match it.
**Fix:** Change the row to: "a unit exam uses `unit:<guid>` through `UnitExamScope`; a multi-unit exam uses `units:{size}:{sorted lowercase unit ids joined by ","}` through `MultiUnitExamScope` (`docs/exams.md#multi-unit-exams`)". The `Scope` row (line 16) could also name the two exam jsonb shapes.

## Non-blocking
- `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamSizes.cs:5`: `MinUnits = 2` has no WHY comment, although `All` has one (skill §1 "No magic values": invariants take a WHY comment). Both are product rules fixed by PRD §7.5 and plan Decisions 1–2, so keeping them as constants and not in options (§8.1) is acceptable.
- `api/Elmanhg.Application/Exams/StartMultiUnitExam/MultiUnitExamDraw.cs:15-18`: one `GetServableExamCandidatesAsync` round trip per unit (N+1, skill §9 "no N+1"). The plan prescribes this (Files #28), and N is small. A single `GetServableExamCandidatesAsync(unitIds)` grouped by lesson unit would do the same in one query.
- `api/Elmanhg.Domain/Sessions/Exams/ExamBreakdown.cs:53`: `unitOrder.ToList().IndexOf` allocates a list for every row. `unitOrder` is already an `IReadOnlyList`, so use a loop or `FindIndex` on it.
- `web/src/features/exam/components/MultiExamSubjectSection.tsx:40-45`: the selection and size are derived in the component. This could move into `useMultiExamSearch` (react skill 6.6), but it is small and pure.
- `web/src/features/exam/components/MultiExamPreview.tsx:25`: `useStartMultiExam` lives inside the preview, so a start error (for example a 409) stays visible after the selection changes, until the next start.

## Verified
- Ran `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside, then restored it: total 1879, failed 0 (matches the report). Docker 29.6.2 was up.
- Ran the web checks: `typecheck` clean, `lint` clean (max-warnings 0), `test -- --run` gave 100 files and 632 tests passed. `npx prettier --check ... --end-of-line auto` passed. All match the report.
- `dotnet format --verify-no-changes`: the only findings are CRLF whitespace noise, mostly in the vendored core-libraries; none are in the new or changed files. Guard grep (DateTime.Now/UtcNow, .Result, .Wait(), async void, FromSqlRaw) on the new files finds nothing.
- Apportioning (`MultiUnitBlueprintMerge.cs:23-38`):
  - It uses floors of c*size/total, and the leftover (size minus the sum of floors) goes by largest remainder, ties broken by unit index, then QuestionType. The leftover is never more than the number of non-zero remainders, so Take always fills to size.
  - I recomputed by hand: the worked example gives A 6 r20 and B 13 r10, so A 7 and B 13. Time ceil(21 + 26) = 47, clamped to [1, max]; pass mark 56.5 rounds to 57 with AwayFromZero. Test 11 gives A 0, B 20 (r81 wins), time 12, pass 50.
  - Only units with a non-zero share count toward time and pass mark. Any untimed contributing unit makes the exam untimed. This matches Decisions 4, 6, 7 and 8 and docs/exams.md.
- Spill-over (`MultiUnitExamQuestionSelector.cs`):
  - Each unit draws with its own mix, capped at the unit pool. The type shortfall is then drawn from the union minus the picks, with a null mix and not-mastered first, and the result is stable-sorted by type, then difficulty.
  - Only a union shortfall blocks the exam (`MultiUnitExamDraw.cs:24` calls `MultiUnitExamPlan.EnsureServable`: EXAM_SHORTFALL with context types). The union check guarantees the spill-over can always fill.
- Scoping:
  - All 3 actions carry `DefaultCodes.AssessmentsTake` (`ExamsController.cs:40-65`). Teacher 403 and anonymous 401 are covered by integration tests.
  - The open-exam lookup is filtered by StudentId == userId. A unit outside the subject gives 404 UNIT_NOT_FOUND (`MultiUnitExamPlanner.cs:20-24`).
  - Admins get IsTestMode, and history excludes test mode.
- unitIds binding: `[FromQuery] List<Guid>? unitIds` works end to end through repeated params (integration Preview_TwoUnits_ReturnsMergedCounts). The generated getPreviewMultiUnitExamUrl explodes unitIds (`web/src/shared/api/generated/exams/exams.ts:509`).
- #81 reuse:
  - `Session.Exam.cs` refactor: same checks and codes in the same order, and StartUnitExam keeps its signature. The loader handles both kinds with the per-unit GetByIdAsync loop (Decision 20). The #81 tests are unchanged (the diff only adds lines) and green.
  - Every other UnitExamScope.FromJson use now goes through GetExamUnitIds. The best-score query stays Kind == UnitExam (`SessionRepository.cs:39`), so multi-unit exams never feed it.
- Start handler order is validate, load, key, open-exam check (409 or resume, auto-submit past the deadline), then plan and draw only when nothing is open (Decision 13). SaveChangesAsync is called once.
- Every file in Files to create exists except #27 (conditional; the loader is 70 lines). That deviation and the other four are all declared. No extra files were added. routeTree.gen.ts has no diff, which is plausible because the route path already existed.
- The 4 error codes are in ErrorCodes, both resx files and both web errors files.
- Postman: secondUnitId variable. After "Submit exam", 4 requests in a correct state order: overview GET, preview GET with repeated unitIds, start POST with body and a test that sets examSessionId/examQuestionId, then submit POST. Auth is inherited bearer, and the folder description is updated.
- Docs: PRD §7.5, exams.md (new section, API table, result shapes, error table), exam-blueprints.md step 4, progress.md scope name, claude-design-prompt.md §4/§7 and prototype.md item 6 all agree with the code. The only exception is Blocking #1.
- Design tokens: the new components use only token classes (shadow-1, bg-warning-soft, accent-text, py-2.25 on the spacing scale, text-caption). No arbitrary values or left/right utilities. Inputs are labelled, and radios and checkboxes sit in a fieldset/legend.

## Test quality
- MultiUnitBlueprintMergeTests: these constrain the code.
  - The tie-break test (A {1,1}, B {1}) would fail if the unit/type tie order changed.
  - Pass mark 56.5 to 57 fails under banker's rounding.
  - The fractional time case (43.5 to 44) kills Ceiling to Floor.
  - The zero-share case kills removing the contributing filter.
- MultiUnitExamQuestionSelectorTests: these constrain the code. Test 17 (4 + 16) fails without spill-over, and the mix test fails if a unit's mix is dropped. Select_PrefersNotMasteredAcrossUnits only exercises the per-unit stage, not not-mastered preference inside the spill-over.
- MultiUnitExamPlanTests, MultiUnitExamScopeTests, SessionMultiUnitExamStartTests: meaningful. They assert the key sort, round trip, codes and deadline.
- StartMultiUnitExamHandlerTests: good. Repository substitutes evaluate the real predicates. Every throwing path asserts the code plus AddAsync and SaveChangesAsync DidNotReceive. The success paths assert Received(1) and the real outcome (20 items, pass mark, units in order).
- PreviewMultiUnitExamHandlerTests, GetMultiUnitExamOverviewHandlerTests, the validator tests: each rule has a failing case, and delegation through SetValidator is covered.
- MultiUnitExamEndpointTests: real PostgreSQL. It checks that exactly 10 questions come from each unit, the DB ScopeKey prefix, the resume with reversed selection order giving one session, and a union shortfall saving no session.
- Web MultiExamBuilderPage.test.tsx:
  - It covers every state, including RTL and axe.
  - The size test reads size from the request URL, so it proves the query refetches.
  - The URL restore and subject-change tests constrain the search state.
- No vacuous tests found.
