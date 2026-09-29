VERDICT: CHANGES_REQUESTED

# Review — [E7.S1] Subject, unit and lesson browsing (#85)

## Blocking

### 1. The per-unit exam action on the student subject page is a 36 px touch target
**Where:** `web/src/features/browse/components/UnitListItem.tsx:33`
**Rule:** `.claude/design-system.md:81` (Button: "min height 44 (36 for `sm` in dense admin tables)") and `.claude/design-system.md:134` ("Touch targets ≥44px"). Plan "Mobile 375 px rules" #4.
**Problem:** `<Button asChild variant="secondary" size="sm">` renders `min-h-9` (36 px, `web/src/shared/ui/button.tsx:19`). Plan rule 4 allows this because it says the design system allows it. It does not: the design system limits `sm` to dense admin tables. Every other `size="sm"` in the repo is in admin or teacher content screens (`features/content`, `features/blueprints`). This is a student card on a mobile-first page.
**Failure:** At 375 px, a student on `/student/subject/:id` gets a 36 px-high «امتحان الوحدة» target in every unit card. That is below the 44 px minimum the design system sets for every interactive element.
**Fix:** Drop `size="sm"` so the button uses the default size (`min-h-11`). No test change is needed.

### 2. One `throw` in the new lesson read has no test
**Where:** `api/Elmanhg.Application/Browse/GetStudentLesson/GetStudentLessonHandler.cs:35-39`
**Rule:** Reviewer coverage rule ("every throw in the new code needs a test"). The testing convention requires every handler branch to be tested. Skill §9: "Every handler branch".
**Problem:** The subject-null branch (`NotFoundCoreException(LessonNotFound)` when the unit of the lesson points to a missing subject) is never exercised. `GetStudentLessonHandlerTests` covers the missing unit (`Handle_LessonOfMissingUnit_ThrowsLessonNotFound`) but not the missing subject. The sibling handler `GetStudentUnitHandler` has this test (`Handle_UnitOfMissingSubject_ThrowsUnitNotFound`), so this looks like an oversight.
**Failure:** Delete lines 36-39 and the whole suite stays green. A published lesson whose subject was soft-deleted then reaches `StudentLessonResultGenerator` with a null `subject` and returns a 500 instead of 404 `LESSON_NOT_FOUND`.
**Fix:** Add `Handle_LessonOfMissingSubject_ThrowsLessonNotFound` to `GetStudentLessonHandlerTests`. Seed a published lesson under a unit whose `SubjectId` has no `GetByIdAsync` stub, then assert `NotFoundCoreException` with `LESSON_NOT_FOUND`.

## Non-blocking
- `web/src/features/browse/components/LessonNavigation.tsx:11`: `inline-flex … break-words` on the link. The text becomes an anonymous flex item with `min-width: auto`, and `overflow-wrap: break-word` does not lower its min-content size. A single unbroken word longer than the row can still overflow at 375 px. Wrap the label in a span with `min-w-0 break-words`, or use `wrap-anywhere`. These standalone prev/next links are also below 44 px high. The existing text-link pattern in the repo does the same, so this is not gated.
- `web/src/features/browse/pages/LessonPage.test.tsx:144-153`: the `waitFor` equality on `recorded` passes at the first matching moment. A second POST that lands later (for example, if the `useRef` guard were removed) may not fail it. Consider asserting again after the mutation settles.
- `api/Elmanhg.Domain/Lessons/LessonSequence.cs:7`: this comment was specified by the plan and explains a WHY (a stable tie-break), so it is acceptable under "no comments unless the WHY is a hidden invariant". Noted only.
- `PROGRESS.md:121` still says "The practice route has no UI link until #85 adds the lesson tabs." That is stale once this merges. The orchestrator should update it in the post-merge bookkeeping. It is not in `/docs`, so it is not a docs-sync finding.
- I did not re-run `npm run gen:api` because this review is read-only. Drift is not independently proven, but `typecheck` is green against the committed generated client, and `openapi/v1.json` contains all 4 browse paths and `unopenedLessonCount`.

## Verified
- **Builds and tests (run by me):**
  - `dotnet test api/ -c Release` with `appsettings.json` moved aside and then restored: 2489 passed, 0 failed. The test DLL was newer than every `.cs` source.
  - `dotnet format --verify-no-changes --exclude api/core-libraries`: the only finding is `Elmanhg.Tests/Builders/SubscriptionBuilder.cs:57`, which is pre-existing and not touched by this change, as claimed.
  - Web `typecheck`: clean. `lint`: clean. `test -- --run`: 123 files, 778 tests passed. `prettier --check`: clean.
- **Published-only reads:**
  - The lesson read and the opening both 404 on a null or non-Published lesson (`GetStudentLessonHandler.cs:24`, `RecordLessonOpeningHandler.cs:20`).
  - The unit read filters `State == Published` (`GetStudentUnitHandler.cs:36`). The neighbour pool is Published only (`GetStudentLessonHandler.cs:43`). The subject lesson counts use `CountByUnitAsync(..., publishedOnly: true)`.
  - Mastery comes from `GetLessonCountsAsync`, which uses `WhereServable` (the servable rule).
  - Integration tests prove that drafts are excluded (#54, #57, #59, #62).
- **Student-only policy:** all 4 actions carry `[Authorize(Policy = DefaultCodes.ProgressViewOwn)]`, and that policy is `RequireRole(Student)` (`PermissionMatrixPolicies.cs:26`). Teacher 403 and anonymous 401 are covered by the integration tests.
- **Lesson-open gate:**
  - `ExamsOptions.RequireAllLessonsOpened` defaults to false. It is set to false in `appsettings.example.json` and in `ApiFactory` (`UseSetting`).
  - For a unit exam, the gate runs only inside `StartAsync`, which is reached only when `open is null` (`StartUnitExamHandler.cs:47,67-73`). For a multi-unit exam, it runs only inside `if (session is null)` (`StartMultiUnitExamHandler.cs:44-50`). A resume is therefore never gated; `Handle_GateOnOpenExamSameUnit_ResumesWithoutGate` proves it.
  - Admins are exempt on start and on the overview, proven by #44 and #50.
  - Integration tests #64 to #68 run with a `PostConfigure` gated factory.
- **Idempotency:**
  - The handler returns early on `IsOpenedAsync` and saves nothing (#36). Two sequential POSTs store one row (#63).
  - The race is mapped `IX_LessonOpenings_StudentId_LessonId` to 409 `LESSON_ALREADY_OPENED` in `AppDbContext.cs:112-115`, proven by `LessonOpeningPersistenceTests`.
  - The web hook guards with a ref and posts only after `unitId` is known.
- **Link wiring:**
  - Home card (`SubjectMasteryCard.tsx:22`) and progress subject name (`SubjectProgressCard.tsx:23`) link to the subject page.
  - Progress unit name (`UnitProgressTable.tsx:37`) links to the unit page.
  - The exam-start breadcrumb (`ExamStartPage.tsx:34`) and «Back to the lesson» on the quiz result (`QuizResultPage.tsx:50`) are in place.
  - Each link has a test (#106 to #111).
- **Quiz and practice regressions:**
  - The practice route file is unchanged and is now a child of the lesson layout (`routeTree.gen.ts:270-273`).
  - `PracticePage.test.tsx` is unedited and green. `PracticePage` has no `h1`, and #96 asserts exactly one `h1`. `practice.title` was removed from both locales.
  - MSW defaults for the browse lesson and opening were added to `test/msw/server.ts`.
- **Plan contract:**
  - Every file in *Files to create* (#1 to #77) exists, and nothing extra was added beyond generated artifacts.
  - The signatures match the plan. Decisions D1 to D18 are honoured.
  - The migration only creates the table and indexes (no Drop or Rename in `Up`), and `AppDbContextTests` has `_AddLessonOpenings` appended.
  - The error codes are in `ErrorCodes`, both resx files and both web `errors` locales.
- **Deviations in `02-implementation.md`:** all 4 match the code (the named default tuple in `LessonSequence.cs:29`, the chevron direction in `LessonNavigation.tsx`, the scoping to `main`, and the extra `aria-current` assertion).
- **Postman:** the `Browse` folder comes after `Mastery`, with 4 requests in plan order, the correct methods and URLs, and collection-level Bearer auth inherited like `Mastery`. The overview contract change needs no request edit.
- **Docs sync:**
  - `docs/browsing.md` is new and agrees with the code.
  - `docs/exams.md` (step list, multi-unit start, overview shape, error row, option row, exam-start screen), `docs/mastery.md`, `docs/progress.md`, `docs/claude-design-prompt.md` §4 and `docs/prototype.md` were all updated.
  - PRD §7.4 ("configurable; default: no gate") agrees.
  - No "until #85" text remains in `/docs`. I found no divergence.
- **375 px rules walked:**
  - Rule 1 (single column below `md`), rule 2 (`flex-wrap`, no fixed width), rule 3 (`break-words` on names, and `.rich-text` has `overflow-wrap: anywhere`), rule 5 (no tables) and rule 6 (logical `ps-6` and `before:me-2`, chevrons with `rtl:rotate-180`) all hold.
  - Rule 4 fails only at Blocking #1.
  - A grep for physical-direction utilities, hex colours and pixel arbitrary values in `features/browse` and the new routes returns nothing.

## Test quality
- `LessonOpeningTests`, `LessonSequenceTests`: constrain the code. The input order is shuffled, the unknown unit is excluded, and the cross-unit next link is checked.
- `GetStudentSubjectHandlerTests`: constrains the code. The `CountByUnitAsync` stub matches only `publishedOnly: true`, the subject totals are weighted (8, 2, 3 gives 25 %), a unit from another subject is filtered by the compiled predicate, and a multi-unit best score is ignored.
- `GetStudentUnitHandlerTests`: constrains the code. Draft and archived lessons are filtered through the compiled predicate, and a count for another lesson is excluded from the per-lesson row but included in the unit totals.
- `GetStudentLessonHandlerTests`: constrains the code, apart from the gap in Blocking #2. The draft between units in #29 checks that it is skipped.
- `RecordLessonOpeningHandlerTests`: constrains the code (`Received(1)` with the exact StudentId, LessonId and OpenedAt; `DidNotReceive` on every throwing path and on the already-opened path).
- `ExamLessonGateTests`: constrains the code (a draft and the opening of another student are both excluded).
- Gate additions in the Start, StartMulti and Overview handler tests: constrain the code. `AssertThrowsAsync` checks that neither `AddAsync` nor `SaveChangesAsync` was called, and the gate-off overview test has unopened lessons present.
- Integration tests (Browse, gate, persistence): hit the real policy, the SQL filters and the unique index.
- Validator tests: one passing and one failing case per rule.
- Web page tests: constrain the rendered output. Hrefs, ordering, `aria-current`, the empty, error-with-retry and RTL states, and axe are all checked. The only weak one is "records the lesson opening once" (see Non-blocking).
- None of the tests only asserts that a substitute returned what it was told to return.
