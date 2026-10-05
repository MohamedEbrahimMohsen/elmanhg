VERDICT: APPROVED

# Review — [E19.S7] Fix training export download; add question entry on Questions page

## Blocking
None.

## Non-blocking
- `web/src/features/questions/components/QuestionLessonPicker.tsx:80` — the "no lessons" hint and the units/lessons loading status (`:75-79`) have no test. Skill §5 asks for loading and empty tests on data views. The plan's test plan did not list them, so this does not gate.
- `web/src/features/questions/components/QuestionLessonPicker.tsx:41` — a subject with zero units shows an enabled «الوحدة» select that holds only the placeholder, with no hint. A «لا توجد وحدات…» line would match the no-lessons case.
- `web/src/features/questions/components/AddQuestionButton.tsx:38` — the dialog is always mounted, so the picker state survives closing and reopening. The subjects query is shared with `QuestionListFilters` (same key), so this makes no extra request.
- `web/src/features/questions/pages/QuestionListPage.tsx:60,65-69` — with `?lessonId=` set, the header «إضافة سؤال» and the lesson bar's «سؤال جديد في هذا الدرس» link to the same route. This duplication was disclosed in the report.
- `web/src/features/questions/i18n/admin.ar.json` `list.add.noLessons` — plan §3 says «لا توجد دروس في هذه الوحدة.» but the code and the doc say «… بعد.». The code and the doc agree, so this is wording drift from the plan only, and "Deviations: None" is slightly inaccurate.
- `docs/claude-design-prompt.md` `#/admin/questions` — the sentence after «…secondary.» starts with a lower-case "each".

## Verified
- `http.ts:51-54,60` — `isJsonContentType` takes the media type before `;`, trims and lower-cases it, and returns true for `''`, `application/json` and `*+json`. Behaviour by case:
  - `application/json; charset=utf-8`, `application/problem+json` and no-header 204/empty 200 bodies behave exactly as before (empty → `undefined`).
  - `!ok` still goes through `toApiError` before any content-type check.
  - `application/x-ndjson` (empty or not) now returns a Blob. Binary and text types still return a Blob.
  - The API emits no other `*json*` media type (grep over `api/`: only `application/x-ndjson`, `TrainingExportsController.cs:38`, `MediaContentTypes.cs:18`).
  - Blob callers (`ThreadAudio.tsx:17`, `ThreadImage.tsx:14`, `useDownloadTrainingExport.ts:18`) serve image, audio and ndjson, so they are unaffected or fixed.
- The claim that the new tests fail on the old code is confirmed. I put back `origin/main`'s `http.ts`. Then all 4 tests failed: the 2 ndjson cases in `http.test.ts` and both `useDownloadTrainingExport` tests. The 2 charset and problem+json cases still passed, as expected. I then restored the changed file; `git diff --stat` shows the 6/2-line diff again.
- Every file in the plan exists, and nothing extra was added. `«مسح الفلاتر»` is now `secondary` (`QuestionListEmptyState.tsx:18`). The existing `QuestionListPage.test.tsx:121` (`mintButtons()` length 1 in no-results) now constrains it, together with the new header primary.
- Dialog: Radix `DialogContent` (`shared/ui/dialog.tsx`) gives the focus trap, Escape and focus return. Selects are labelled through `useId` + `Label htmlFor`. Later selects are disabled until the one before is chosen, and choosing clears downstream (`useQuestionLessonPicker.ts:35-43`). Links go to `/admin/question/new/$lessonId` and `/admin/question/import/$lessonId` (typed routes; typecheck is clean). The direct link when filtered is checked by test.
- Picker states: skeleton while subjects load; `ContentErrorState` + retry for any failing query; a `role=status` line while units/lessons load; the no-lessons hint.
- Strings are in both locales and follow the formal style of the file (`تعذر` matches the existing `تعذر تحميل الأسئلة`). There are no new numbers.
- Postman: no API change, so not applicable.
- Docs-sync: `docs/claude-design-prompt.md` (`#/admin/questions` entry point and dialog; `#/admin/export` 0-row empty `.jsonl`) and `docs/prototype.md` (product-only note) agree with the code. The PRD and design-system docs are not affected. No divergence.
- Commands, run by me:
  - `typecheck` and `lint`: clean.
  - `vitest run`: 309 files / 1834 tests passed, no timeouts.
  - `build`: OK, 298 files precompressed.
  - `perf:budget`: all `ok`, same numbers as reported. `web/scripts`, `package.json` and `.github` are unchanged, so no budget was raised.
  - Both CI greps: no match (exit 1).
  - Prettier on the changed files (`--end-of-line auto`): clean. The plain `format:check` flags 1262 files, all because of the Windows CRLF checkout; this is not from this change.
  - `routeTree.gen.ts` is unchanged.

## Test quality
- `http.test.ts` (new cases): the ndjson cases constrain the fix (they fail on the old code). The charset and problem+json cases guard against regression.
- `useDownloadTrainingExport.test.tsx`: it uses the real `http` and MSW, and checks the saved Blob's content, size and file name, plus that no error toast appears. Both tests fail on the old code. The toast string exists in `trainingExport/i18n/en.json:53`, so the negative assertion is not vacuous.
- `AddQuestionButton.test.tsx`:
  - The single-primary, direct-link, pick-and-link, clear-downstream, error+retry, Arabic and axe tests each check the DOM and links.
  - The retry test swaps the handler, so it proves a real refetch.
  - Gap: no test for the empty-unit hint or the cascade loading line (see non-blocking).
