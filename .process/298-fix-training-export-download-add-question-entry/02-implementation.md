# Implementation — [E19.S7] Fix training export download; add question entry on Questions page

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `web/src/features/questions/hooks/useQuestionLessonPicker.ts` | 48 | Subject/unit/lesson UI state (choosing one clears the ones after it) over `useGetSubjects`, `useGetSubject`, `useGetLessons` |
| `web/src/features/questions/components/QuestionLessonPicker.tsx` | 83 | Three labelled selects; skeleton, error + retry, loading status, "no lessons" hint |
| `web/src/features/questions/components/AddQuestionDialog.tsx` | 59 | Dialog «إضافة سؤال»: picker + «إلغاء» (ghost), «استيراد من ملف» (ghost), «سؤال جديد» (secondary). Links once a lesson is chosen, disabled buttons before that |
| `web/src/features/questions/components/AddQuestionButton.tsx` | 41 | Header primary: a direct link to `/admin/question/new/$lessonId` when filtered by a lesson, otherwise opens the dialog |
| `web/src/features/questions/components/AddQuestionButton.test.tsx` | 153 | 7 tests from the plan |
| `web/src/features/trainingExport/hooks/useDownloadTrainingExport.test.tsx` | 115 | 2 hook tests from the plan (renderHook + MSW, real `http`) |

## Files modified
| Path | Change |
|---|---|
| `web/src/shared/lib/http.ts` | New `isJsonContentType`: the media type before `;`, compared case-insensitively. It returns true only for an empty type, `application/json` and `*/*+json`. Everything else, including `application/x-ndjson`, goes to a Blob |
| `web/src/shared/lib/http.test.ts` | Added the 4 cases from the plan; no existing case was touched |
| `web/src/features/questions/pages/QuestionListPage.tsx` | Header row now holds the title block and `AddQuestionButton` |
| `web/src/features/questions/components/QuestionListEmptyState.tsx` | «مسح الفلاتر» changed from `primary` to `secondary`, so the page keeps one primary |
| `web/src/features/questions/i18n/admin.en.json`, `admin.ar.json` | Added `list.add.*`; `list.hint` now names the new button |
| `docs/claude-design-prompt.md` | `#/admin/questions`: the «إضافة سؤال» entry point and dialog. `#/admin/export`: a 0-row export downloads an empty `.jsonl` |
| `docs/prototype.md` | Admin: a note that «إضافة سؤال» exists in the product only |

## Deviations
None.

## Build & test
All commands were run in `web/`:
- `npm run typecheck`: clean (no output from `tsc -b`).
- `npm run lint`: clean (`eslint . --max-warnings=0`).
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: "All matched files use Prettier code style!" One run of `--write` on `QuestionLessonPicker.tsx` came first.
- `npm test -- --run --coverage`: `Test Files 309 passed (309)`, `Tests 1834 passed (1834)`, exit 0. No timeouts.
- `npm run build`: exit 0, "Precompressed 298 files".
- `npm run perf:budget`: all `ok` (entry 189/210, landing 199/220, lesson 221/240, quiz 236/255, admin-dashboard 213/233, admin-users 245/270, teacher-home 245/265, assistant 244/260). No budget was changed.
- The two CI greps for physical directions and literal tokens had no matches (exit 1 each). `git status --porcelain src/routeTree.gen.ts` was empty.
- **Live check against the demo API (http://localhost:8080):** I added a temporary vitest file (MSW passthrough, deleted afterwards and never committed). It signed in with the seeded admin, whose credentials were read from the demo container's environment at run time. Through the real `http()` it requested two Attempts exports, waited for them, and downloaded each file:
  - `2026-01-01..2026-10-05 status=Completed rows=35 blob=true size=19163 type=application/x-ndjson lines=35`
  - `2020-01-01..2020-01-02 status=Completed rows=0 blob=true size=0 type=application/x-ndjson lines=0`

## Notes for review
- No reusable picker existed. The student pickers (`askTeacher/LessonPicker`, `avatar/AssistantContextPicker`) read `/api/mastery`, and the validation-queue filter is teacher-scoped. The new picker uses the admin content endpoints that the content tree already uses, and it stays inside the `questions` feature.
- The picker uses local `useState`, not RHF. The dialog has no submit; its actions are navigation links, so I followed the existing pickers rather than a form.
- When the list is filtered by a lesson, the header link «إضافة سؤال» and the existing lesson bar link «سؤال جديد في هذا الدرس» go to the same place. I kept the bar so its existing tests stay untouched.
- The existing page test 'downloads a completed export…' passed before the fix. Its body `'{}\n'` is valid JSON, so `JSON.parse` succeeded and the mocked `createObjectURL` accepted a non-Blob. The new hook tests use multi-line and empty bodies, and both fail on the old `parse`.
- The live check left 2 extra Attempts exports in the demo database (4 in total, because I ran it twice). They expire through normal retention.
- The PRD does not describe question entry points (§ "Full content tree…"), so it is unchanged.
