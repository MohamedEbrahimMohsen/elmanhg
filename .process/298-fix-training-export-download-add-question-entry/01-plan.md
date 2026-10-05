# Plan — [E19.S7] Fix training export download; add question entry on Questions page

Story: `00-story.md` (#298). Web only. No API, AI, OpenAPI or Postman change.

## 1. `http.ts` content-type handling

`parse()` today treats any `Content-Type` that contains `json` as JSON, so `application/x-ndjson` goes through `JSON.parse` (throws on many lines) or returns `undefined` on an empty body.

Change: a private `isJsonContentType(contentType)` takes the media type before `;`, trims and lower-cases it, and returns true only for `''` (no header), `application/json` and `*/*+json`. Everything else is returned as a `Blob`.

| Case | Before | After |
|---|---|---|
| `application/json; charset=utf-8` (every JSON endpoint) | JSON | JSON (unchanged) |
| `application/problem+json` | JSON | JSON (unchanged) |
| no `Content-Type` (204, empty 200) | `undefined` / JSON | unchanged |
| `application/x-ndjson`, non-empty | `JSON.parse` throws | Blob |
| `application/x-ndjson`, empty (0-row export) | `undefined` | empty Blob, so an empty `.jsonl` still downloads |
| binary (`xlsx`, `text/plain`, ...) | Blob | Blob (unchanged) |

Only `application/x-ndjson` (training export file) changes behaviour; the API emits no other `*json*` type that is not `application/json` (checked: `grep` over `api/`). Error bodies still go through `toApiError` (unchanged).

## 2. Picker reuse

| Candidate | Why not reused |
|---|---|
| `askTeacher/LessonPicker`, `avatar/AssistantContextPicker` | Student pickers backed by `/api/mastery` (student-only; also bound to RHF/assistant context types). |
| `questions/ValidationQueueFilters` | Teacher-scoped filter options (`/api/validation/filters`), filter form, not a lesson chooser. |
| `trainingExport` subject select | Subject only. |
| Content tree (`ContentPage`/`UnitPanel`/`UnitLessons`) | Editing tree, not a picker. Its admin queries are what the new picker uses. |

So a new admin picker inside the `questions` feature, on the same generated admin hooks the content tree uses: `useGetSubjects()` → `useGetSubject(subjectId)` (units) → `useGetLessons({ unitId })`. It stays in the feature (6.7: no shared component for one use).

## 3. Design

Questions page header gets «إضافة سؤال» / "Add question", the page's only primary (mint) button.
- Filtered by a lesson (`?lessonId=`): a primary link straight to `/admin/question/new/$lessonId`.
- Otherwise: a primary button that opens a dialog «إضافة سؤال» with three labelled selects «المادة» → «الوحدة» → «الدرس» (each later select disabled until the earlier one is chosen; changing one clears the ones after it), and actions «سؤال جديد» (secondary) and «استيراد من ملف» (ghost), which are links to `/admin/question/new/$lessonId` and `/admin/question/import/$lessonId` once a lesson is chosen and disabled buttons before, plus «إلغاء» (ghost). States: subjects loading → skeleton; subjects/units/lessons error → `ContentErrorState` with retry; a unit with no lessons → «لا توجد دروس في هذه الوحدة.» hint.
- To keep one primary on the page, the no-results empty state's «مسح الفلاتر» changes from `primary` to `secondary`.
- The header hint changes from "open the lesson from the content tree" to point at the new button as well.
- Arabic follows the current (formal) style; Latin digits unchanged (no new numbers).

## Files to create
| Path | Purpose |
|---|---|
| `web/src/features/questions/hooks/useQuestionLessonPicker.ts` | Picker UI state (subject/unit/lesson) + the three generated queries |
| `web/src/features/questions/components/QuestionLessonPicker.tsx` | The three selects with loading/error/empty |
| `web/src/features/questions/components/AddQuestionDialog.tsx` | Dialog: picker + actions |
| `web/src/features/questions/components/AddQuestionButton.tsx` | Header primary: direct link when filtered, else dialog trigger |
| `web/src/features/questions/components/AddQuestionButton.test.tsx` | Component tests (through `renderApp('/admin/questions')`) |
| `web/src/features/trainingExport/hooks/useDownloadTrainingExport.test.tsx` | Hook tests: Blob for non-empty and empty NDJSON |

## Existing code touched
| Path | Change |
|---|---|
| `web/src/shared/lib/http.ts` | `isJsonContentType`, used by `parse` |
| `web/src/shared/lib/http.test.ts` | Add cases (no existing case edited) |
| `web/src/features/questions/pages/QuestionListPage.tsx` | Header row with `AddQuestionButton` |
| `web/src/features/questions/components/QuestionListEmptyState.tsx` | Clear filters → `secondary` |
| `web/src/features/questions/i18n/admin.en.json`, `admin.ar.json` | `list.add.*`, new `list.hint` |
| `docs/claude-design-prompt.md` | `#/admin/questions` entry points; `#/admin/export` 0-row download note |
| `docs/prototype.md` | Admin: product-only «إضافة سؤال» note |

PRD: §65 only says "Full content tree with publish and validation workflows"; it does not describe entry points, so no PRD change.

## Test plan
`web/src/shared/lib/http.test.ts` (added `it`s in `describe('http')`):
- `parses application/json with a charset`
- `parses an application/problem+json success body`
- `returns a Blob with the lines for a non-empty application/x-ndjson body`
- `returns an empty Blob for an empty application/x-ndjson body`
(binary is already covered by `returns a Blob for a non-JSON response`.)

`web/src/features/trainingExport/hooks/useDownloadTrainingExport.test.tsx` — `describe('useDownloadTrainingExport')`:
- `saves a non-empty JSONL export as a Blob under its file name`
- `saves an empty JSONL export as an empty Blob without an error toast`

`web/src/features/questions/components/AddQuestionButton.test.tsx` — `describe('AddQuestionButton')`:
- `is the only primary action on the questions page`
- `links straight to a new question when the list is filtered by a lesson`
- `picks subject, unit and lesson then links to new question and import`
- `clears the unit and lesson when the subject changes`
- `shows an error and recovers on retry when the subjects fail`
- `renders the dialog in Arabic`
- `has no axe violations with the dialog open`
