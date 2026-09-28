VERDICT: CHANGES_REQUESTED

# Review — [E2.S2] Lesson authoring with explanation, objectives and summary (#61)

## Blocking

### 1. The lesson editor (TipTap/ProseMirror + KaTeX + DOMPurify) ships in the main chunk every route loads
**Where:** `web/src/features/content/index.ts:5-6` (the barrel now re-exports `LessonEditorPage` and `RichTextViewer`), pulled in eagerly by `web/src/app/i18n.ts:5` (`import { contentLocales } from '@/features/content'`)
**Rule:** react-feature §18 ("Route-level code splitting (`autoCodeSplitting`); `lazy()` for heavy widgets (charts, editors)"); PRD §14 Non-functional requirements ("Lesson page < 2s on 3G-class connections")
**Problem:** Because `app/i18n.ts` imports the component barrel to get the locales, everything the barrel re-exports lands in the entry chunk. `autoCodeSplitting` has no effect on `lesson.$lessonId`. I rebuilt it myself: `dist/assets/lesson._lessonId-*.js` is **262 bytes**, and `dist/assets/index-*.js` is **1,217.18 kB (379.94 kB gzip)**. That chunk contains `ProseMirror` (51 hits), `tiptap` (34), `katex` (52), `DOMPurify` and `insertBlockMath`. The implementer found this and reported it, but left it in place.
**Failure:** On any first visit (`/login`, a student route, the future student lesson page) the browser downloads and parses about 380 kB gzip of JS before first render, and that includes an editor no student ever uses. At Fast-3G speed (about 1.6 Mbps, about 200 kB/s) the main chunk alone takes about 1.9 s to download, before parse, CSS, fonts and API calls. That leaves the PRD §14 target of < 2 s no headroom, and every later story that adds to the entry chunk makes it worse. Supporting evidence: on my first coverage run the first test in both `UnitPanel.test.tsx` and `UnitLessons.test.tsx` timed out on `findByRole('button', { name: 'Show units' })`, because cold module import through `@/app/i18n` got much heavier. Both passed on the next two runs.
**Fix:** Stop importing the component barrel from `app/i18n.ts`. Expose the locales from a side-effect-free module (for example `features/content/locales.ts`, also re-exported from the barrel) and import that path in `app/i18n.ts`. Then rebuild and confirm that TipTap/ProseMirror sit in the `lesson.$lessonId` chunk and the entry chunk is back near its pre-#61 size.

### 2. New UI branches have no test: block-math insert, toolbar toggles, move-down, and the rich-text/objectives error display
**Where:** `web/src/features/content/components/RichTextEditor.tsx:92-93` (block-math branch); `web/src/features/content/components/RichTextToolbar.tsx:42,50,53,59,66,73` (all five toggle handlers never run); `web/src/features/content/components/ObjectivesField.tsx:45` (move down) and `:74-76` (server error under objectives); `web/src/features/content/components/RichTextField.tsx:35-39` (error message + `aria-describedby`)
**Rule:** `.claude/conventions/react-testing.md` §Coverage: "New/changed lines in the diff: every branch has a test". Reviewer order #6.
**Problem:** I read `coverage-final.json` from my own run. These statements and branches have 0 hits. The folder thresholds pass (93.6 % / 79.9 %), but the per-branch rule for new lines does not. Block formulas (a sub-task of this story) and every formatting button have no test at all.
**Failure:** Each of these mutants passes the whole suite:
- swap `insertBlockMath` for `insertInlineMath` at `RichTextEditor.tsx:93`;
- change `toggleBold()` to `toggleItalic()` at `RichTextToolbar.tsx:50`;
- change `move(index, index + 1)` to `move(index, index)` at `ObjectivesField.tsx:45`;
- delete the error paragraph at `RichTextField.tsx:35-39`. A server `LESSON_EXPLANATION_TOO_LONG` is then silently swallowed, because it is mapped to `explanation` (`LessonEditorForm.tsx:26`) and goes nowhere else.
**Fix:** In `RichTextEditor.test.tsx`, add:
- "inserts a block formula": tick "Show on its own line", insert, and assert the preview contains `katex-display` and the PUT body contains `data-type="block-math"`;
- one toolbar test: click Bold, type, Save, and assert `<strong>` in the PUT body and `aria-pressed="true"`.

In `LessonEditorPage.test.tsx`, add:
- "Move objective 1 down", asserting the PUT order;
- a PUT 422 `LESSON_EXPLANATION_TOO_LONG` case, asserting the message is described by the Explanation textbox;
- a PUT 422 `LESSON_OBJECTIVES_TOO_MANY` case, asserting the message in the Objectives fieldset.

## Non-blocking
- `api/Elmanhg.Api/Controllers/Lessons/LessonsController.cs:59-63`: no `[RequestSizeLimit]`/`[RequestFormLimits]` on the upload. Kestrel buffers up to its 30 MB default before the 5 MB validator rejects.
- `api/Elmanhg.Application/Lessons/UploadLessonImage/UploadLessonImageValidator.cs:16-25`: there is no magic-byte check. This is accepted per plan D8, because the content type served comes from the extension and `nosniff` is set (`api/Elmanhg.Api/FileStorage/LocalFileStorageExtensions.cs:25`), so a disguised payload is only ever served as an image. Revisit when the S3 adapter lands, since buckets often serve the client-supplied type.
- `api/Elmanhg.Infrastructure/Storage/LocalDiskFileStorage.cs:12-14`: a `LocalRootPath` with a trailing separator produces a doubled separator in `root + DirectorySeparatorChar`, and every save would throw. `Path.TrimEndingDirectorySeparator(root)` hardens it.
- `web/src/features/content/components/RichTextEditor.tsx:64`: the `[invalid, describedBy]` deps destroy and recreate the editor whenever validity flips, so undo history and selection are lost after a failed save.
- `web/src/shared/ui/dialog.tsx:18`: `focus-visible:outline-hidden` with no ring replacement (react-feature §4). Harmless today because both dialogs have focusable children, but the shared primitive should carry the ring.
- `web/src/test/nodeFormData.ts:1`: `/// <reference types="node" />` puts Node globals into the app type-check, because `tsconfig.app.json` includes `src`.
- `api/Elmanhg.Infrastructure/RichText/RichTextSanitizer.cs:44-49`: a stripped external image leaves an `<img alt>` with no `src`. Removing the element would be cleaner.
- `npm run gen:api` producing no diff was not independently re-run (it rewrites the tree). The OpenAPI and Orval output are consistent with the controllers I read.

## Verified
- **Builds and tests, re-run by me:**
  - `dotnet build api/`: 0 warnings, 0 errors.
  - `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside: 465/465 passed. The file was restored afterwards.
  - `npm --prefix web run build`: exit 0, with the 1,217 kB entry-chunk warning (#1).
  - `npm --prefix web test -- --run --coverage`: run 1 had 2 failures (first-test timeouts in `UnitPanel.test.tsx`/`UnitLessons.test.tsx`, see #1). Runs 2 and 3 were 42 files / 211 tests passed, 93.58 % stmts / 79.88 % branches, thresholds met.
- **Deviations:**
  - #1 is correct: core `ValidateUrl` ends in `.When(x => ...x?.ToString())` evaluated on the command (`api/core-libraries/Core.Validation/Extensions/StringValidationExtensions.cs:41`).
  - #2 is correct: core `ValidateRequired(IFormFile?)` is `NotNull().Must(file!.Length > 0)` with the default code (`RequiredValidationExtensions.cs:39-44`), and the `Cascade(Stop).NotNull().WithErrorCode(...)` prefix fixes it.
  - #3 is an intentional, legitimate update (skill §8.11): one appended `_AddLessons` assertion in `AppDbContextTests.cs:23`, nothing else changed.
  - #4 (`nodeFormData.ts`), #5 (one `eslint-disable` in the sanctioned `SafeHtml` sink), #6, #7 and #8 are justified by tooling constraints. #9 is a plain `as` cast, not `as unknown as`. #10 and #11 are improvements. None of them is hidden.
- **Contract:**
  - Every file in *Files to create* exists. The only unplanned file is `web/src/test/nodeFormData.ts` (Deviation #4).
  - `Lesson`/`LessonObjective` follow guard, then mutate, then stamp. The unknown-objective guard runs before any mutation (`api/Elmanhg.Domain/Lessons/Lesson.cs:39-46`).
  - EF mapping: `ValueGeneratedNever` on `LessonObjective.Id`, Restrict FKs, the `(UnitId, Order)`/`(LessonId, Order)` indexes, `State` as varchar(50), and two query-filter lines.
  - The migration has only CreateTable/CreateIndex.
  - `Program.cs` changes only by `app.UseLocalFileStorage()` after `UseHttpsRedirection`.
  - HtmlSanitizer 9.2.1039 comes in through CPM. npm packages are pinned exactly.
- **Security:**
  - Server sanitiser: the allow-list matches D4 exactly, CSS properties are cleared, and `img[src]` must start with `PublicBaseUrl + "/"`, so protocol-relative and absolute hosts are dropped.
  - The sanitiser runs in `UpdateLessonHandler.cs:25-27` before `Lesson.Update`, and is registered as a singleton.
  - Render path: `renderMath` parses with `DOMParser` (inert, so no script or `onerror` execution). KaTeX runs with the default `trust: false`, then `DOMPurify.sanitize` runs in `SafeHtml.tsx:10`, the only `dangerouslySetInnerHTML` in `web/src`.
  - `toSafeVideoUrl` enforces an http/https allow-list.
  - Upload: extension allow-list plus content-type allow-list, no SVG, a size cap from `ContentOptions`. The storage key is built only from `lesson.Id`, a new GUID and `Path.GetExtension(...).ToLowerInvariant()`, so no user path segments.
  - `LocalDiskFileStorage` has the `GetFullPath` + `StartsWith(root + separator)` guard, and it is tested.
  - `/api/media` is anonymous static files by unguessable key (D8), with unknown file types not served and `nosniff` set.
- **Skill compliance (§1, §8, §9):**
  - file-scoped namespaces; `sealed` commands, handlers, validators and results;
  - `.ConfigureAwait(false)` on every handler and repository await; `DateTimeOffset` only;
  - no try/catch; exactly one `SaveChangesAsync` per mutation (none in the upload); current-user guard first;
  - `asNoTracking` on reads; `CountByUnitAsync` is one grouped query (no N+1);
  - caps live in `ContentOptions` and are validated on start;
  - the soft-delete filter lives only in the global method; no DON'T pattern is present.
- **Tests and i18n:**
  - Every test-plan row (1–65, 63b–63k, W1–W44) exists with the exact name.
  - All 17 codes are in both resx files. Web `errors.*` has 16 codes, plus `validation.url` and `validation.fileRequired`. The en/ar key sets are identical in both locale folders.
- **Postman:** the "Lessons" folder has 5 requests with the correct methods and URLs, collection bearer auth (like Units), plausible JSON bodies and formdata `file`. The `lessonId` variable was added.
- **Docs:** `docs/audit-log.md` (3 rows, entities, uploads bullet), `docs/PRD.md` §15 `video_url?`, `docs/claude-design-prompt.md` §4, and the new `docs/rich-text.md` agree with the code. PRD §18 "S3-compatible object storage" is incompleteness (the adapter is deferred), not divergence.

## Test quality
- **Domain** (`LessonTests`, `LessonObjectiveTests`, `CurriculumUnitTests`): constrain the behaviour. Row 8 asserts that nothing mutated on the throw.
- **Handlers:**
  - `UpdateLessonHandlerTests` proves the sanitiser output, not the raw input, reaches the entity (stubbed `raw-e` to `clean-e`), so passing `request.Explanation` straight through would fail.
  - `UploadLessonImageHandlerTests` asserts the key prefix and lower-cased extension from `Diagram.PNG`.
  - `DeleteUnitHandlerTests` row 34 asserts the type, the code, not deleted, and no save.
  - No handler test only asserts a stubbed return.
- **Validators:** every rule has a failing case, including the `javascript:` URL, the SVG, the content-type mismatch and the size cap.
- **Infrastructure:** `RichTextSanitizerTests` and `LocalDiskFileStorageTests` exercise the real adapters, and they constrain.
- **Integration:** these constrain the wire and DB behaviour: `Put_ObjectiveList_KeepsIdsAddsNewAndSoftDeletesRemoved`, `Put_Admin_SanitisesContentAndAudits`, `Post_Admin_StoresServesAndAuditsImage` (bytes round-trip, `nosniff`, audit) and `Delete_UnitWithLessons_Returns400UnitHasLessonsAndAuditsFailure`.
- **Web:** `Form.test` W1 (portal submit isolation), `RichTextEditor.test` W33 (no PUT before Save), `LessonEditorPage.test` W26/W27 (exact PUT bodies) and `UnitLessons.test` constrain real behaviour. The untested branches are listed in #2. No web test is vacuous.
