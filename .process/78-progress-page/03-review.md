VERDICT: CHANGES_REQUESTED

# Review — [E5.S5] Progress page (#78)

## Blocking

### 1. A10 does not check the `WeakObjectiveCount` wiring. A wrong option or no limit still passes.
**Where:** `api/Elmanhg.Tests/Application/Features/Progress/GetWeakSpots/GetWeakSpotsHandlerTests.cs:194-207` (the test) and `api/Elmanhg.Application/Progress/GetWeakSpots/GetWeakSpotsHandler.cs:29` (the code it should check)
**Rule:** reviewer order #5 (a test must fail if the code is wrong), dotnet-testing "Every test must", plan A10 ("3 candidates each → 1 and 1")
**Problem:** Two of the three objective candidates use `Guid.NewGuid()` ids. `_builder.Lesson` has only one objective (`QuestionBuilder.cs:23`), so `WeakSpotsResultGenerator` skips those two for "objective missing" whether or not `Take(count)` trimmed them. The `ContainSingle` on objectives therefore holds for any count, and `WeakLessonCount == WeakObjectiveCount == 1` hides a swapped option. No integration test covers the limits either. The report says "12 mutations, 11 killed", but this mutation was never tried.
**Failure:** Change line 29 to `WeakSpots.PickObjectives(objectiveCounts, options.WeakLessonCount)` or to `int.MaxValue`. Every test stays green. With production defaults (4 / 3), the page then shows up to 4 objectives (or all of them) where the spec says 3.
**Fix:** In A10, make all three candidate objectives resolvable. For example, give the lesson three objectives through `Lesson.Update(...)` and use their ids. Also set different limits (for example `WeakLessonCount = 2`, `WeakObjectiveCount = 1`) and assert 2 lessons and 1 objective.

### 2. Docs divergence: PRD §7.6 says the streak is per subject, but the change makes it student-wide
**Where:** `web/src/features/progress/components/ProgressSummary.tsx:24` (one student-wide streak in the summary), `docs/progress.md` "The page" item 1 ("The streak is student-wide … shown once here, not per subject") vs `docs/PRD.md` §7.6 "Progress page", line 248: "Per subject: mastery %, unit exam best scores, streak (consecutive days with ≥ 1 quiz)."
**Rule:** `.claude/rules/docs-sync.md`, divergence. The PRD owns business rules and mastery. Plan Decision 2 made this choice, but the plan never listed the PRD edit.
**Problem:** Code and docs give two answers to "is the progress streak per subject?". The PRD says yes. The code, `docs/progress.md` and `docs/mastery.md` say no. This is not incompleteness: the story deliberately settled the rule the other way, and `docs/progress.md` states the opposite of the PRD in so many words. The prototype (`vProgress`, `streak(sid)` in the summary card) and #77 agree with the code, so the PRD is the stale side.
**Failure:** A reader of PRD §7.6 (a planner for #106's admin progress view, or QA) expects a streak on each subject card and files the page as missing a feature. Or they build a per-subject streak that contradicts `docs/mastery.md`.
**Fix:** A one-line edit to `docs/PRD.md` §7.6. For example: "- Summary: headline counter and day streak (student-wide: consecutive days with ≥ 1 non-test quiz attempt, `docs/mastery.md`)." and "- Per subject: mastery %, unit exam best scores." No code change is needed.

## Non-blocking
- `api/Elmanhg.Domain/Sessions/QuizScope.cs:12`: the new `?? throw new InvalidOperationException` has no test. D10 covers only `UnitExamScope`. A `QuizScope_FromJsonNull_ThrowsInvalidOperation` twin would be cheap to add.
- `api/Elmanhg.Application/Progress/GetSessionHistory/GetSessionHistoryValidator.cs:17-19`: over HTTP, the model binder makes `SESSION_HISTORY_KIND_INVALID` unreachable (declared deviation, I14 renamed to 400). This is documented in `docs/progress.md` "Error codes". The rule and the web i18n key are dead weight for HTTP callers, which is acceptable as an in-process guard.
- `api/Elmanhg.Domain/Mastery/WeakSpots.cs:3`: the comment explains *what* the code does, not a hidden invariant. The plan mandated it, so it stays, but it is weak by skill §1 "No Comments".
- `api/Elmanhg.Application/Progress/GetWeakSpots/GetWeakSpotsHandler.cs:10`: the Application layer depends on `Microsoft.EntityFrameworkCore` for `Include`. This has precedent (`BulkApproveQuestionsHandler`), so it is not new debt.
- Unit-test gaps worth adding later: `WeakSpotsResultGenerator` skipping a missing subject, and `SessionHistoryResultGenerator` for `UnitExam` / `MultiUnitExam` (covered only by integration test I16, with no MultiUnitExam case).
- `web/src/features/progress/components/SessionHistoryEmptyState.tsx:21`: design-system EmptyState says no-results offers "مسح الفلاتر". The plan chose "عرض الكل", which is plan-sanctioned but differs in wording.
- `PROGRESS.md` (orchestrator bookkeeping, not story code). `scripts/progress_done.py` inserted the #77 row (`| 20 | #77 … |`) after the last row of the **Remaining** table, not in Finished. Its regex `^\| (\d+) \| #\d+ .*\|` also matches Remaining rows. Line 5 still says "Next story: #77". Fix this before the next story runs. `scripts/progress_done.py` itself is an untracked file outside the plan, which is fine as tooling but should land in its own commit.

## Verified
- **Build and tests, run by me:** `dotnet test api/ -c Release` with `appsettings.json` moved aside (restored afterwards) gave **1538 total, 0 failed**, Docker 29.6.2. Web `typecheck` exit 0, `lint` exit 0 (`--max-warnings=0`), `prettier --check … --end-of-line auto` gave "All matched files use Prettier code style!", `test -- --run` gave **84 files, 528 tests passed**. These match the report's claims.
- **Files:** every file in *Files to create* 1–61 exists at the planned path. The only extras are the declared fixture exports and the generated Orval output.
- **Signatures:** the query, handler and result records match the plan contracts. `sealed` is used throughout, namespaces are file-scoped, class definitions are one line, `.ConfigureAwait(false)` is on every await, and there are no `DateTime`, no `try`/`catch` and no `SaveChangesAsync`. Every new `.cs` file is ≤ 100 lines.
- **Decisions:**
  - 4–5: generator grouping, subject and unit order, and `unit:{id:D}` matching via `UnitExamScope`. `GetBestUnitExamScoresAsync` filters submitted, non-test, `UnitExam` and non-null score, and the global filter handles soft delete.
  - 7–8: ordering and filters in `WeakSpots`.
  - 11–13: history filter, ordering, `PageData` copy-through, name lookup with one `FindAsync` each and short-circuit on empty, links.
  - 15: `invalidateMastery` prefix, `staleTime: 0`.
  - 16: `ProgressViewOwn` on all three actions.
  - 17: options with `[Range]` in `appsettings.example.json` and `ApiFactory`.
  - 19: `overflow-x-auto` tables inside cards.
- **Error codes:** three constants, both resx files, and both web `shared/i18n` files. The Arabic resx has no tashkeel.
- **Postman:** a `Progress` folder after `Mastery` with 3 GET requests. URLs, method and query match the controller, and auth is inherited from the collection bearer, like `Mastery`. No stale or orphaned requests.
- **Docs:** `docs/progress.md` created; `docs/mastery.md`, `docs/sessions.md` and `docs/claude-design-prompt.md` §4 edited as planned and consistent with the code, apart from finding 2.
- **Web:** tokens only (`py-2.25`, `px-3.5`, `text-micro`, `shadow-1`, `rounded-*` are all theme or scale utilities, with precedent in `AuditLogTable`). No physical-direction utilities. The progress i18n files have identical 50-key sets. Filter and page live in the URL through `validateSearch`. Cross-feature imports go only through barrels. Every section has loading, empty and error-with-retry states. Arabic dates use Arabic-Indic digits.
- **Deviations:** all three declared deviations (I14 → 400, the strengthened I10, the extra fixture exports) are real and justified.

## Test quality
- `WeakSpotsTests`: constraining. D2 separates every tie-break level, and D5 uses fixed GUIDs so objective order, not id, decides.
- `SessionScopeTests`: constraining for `UnitExamScope`. `QuizScope`'s null path is untested (non-blocking).
- `GetSubjectProgressHandlerTests`: constraining. A4 includes a decoy `lesson:` key, and A2 checks floor percent and pooling.
- `GetWeakSpotsHandlerTests`: A5–A9 are constraining. **A10 is vacuous for objectives** (blocking #1).
- `GetSessionHistoryHandlerTests`: constraining for Quiz naming, paging copy-through and the empty short-circuit. The UnitExam path is left to integration.
- `GetSessionHistoryValidatorTests` and `GetSessionHistoryFilterTests`: one failing case per rule, with codes asserted.
- `ProgressOptionsTests` A13–A14: fine.
- Integration (`SubjectProgress`, `WeakSpots`, `SessionHistory`): strong. I5 seeds decoys (open, test-mode, other student). I9 excludes both mastered and unseen. I10 now proves a mastered objective is excluded. I16 checks both filter directions. I17 checks ownership.
- Web (`progressSearchSchema`, `sessionHistory`, `invalidateMastery` W12, `ProgressPage`, `ProgressPage.history`): behaviour-level through roles and names. MSW handlers branch on query params, so the filter and paging tests really go through the URL-to-request path. axe and RTL are both covered.
