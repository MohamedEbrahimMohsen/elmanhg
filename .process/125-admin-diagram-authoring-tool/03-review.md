VERDICT: CHANGES_REQUESTED

# Review — [E16.S1] Admin diagram authoring tool (#125)

## Blocking

### 1. Admin-only drag-and-drop authoring copy and schemas ship on every student critical path, and the quiz page is left at exactly its cap (255/255 KB)
**Where:**
- `web/src/features/questions/i18n/en.json` / `ar.json`: the `editor.dragDrop.*` block, the 11 diagram `editor.errors.*` keys, `preview.showKey`, `preview.dragDropGradingHint` and `view.diagram*` / `view.listSeparator` / `view.orderSeparator`. These are registered eagerly through `web/src/app/i18n.ts` (`questionsLocales`), so they land in the entry chunk.
- `web/src/shared/i18n/en.json:121-141` / `ar.json`: 21 admin-only codes (`QUESTION_TYPE_NOT_GRADABLE` plus 20 `QUESTION_DIAGRAM_*`). These also go into the entry chunk.
- `web/src/features/questions/schemas/questionContentSchemas.ts:62-85`: `dragDropBodySchema` and `dragDropSpecSchema`.

**Rule:**
- Plan D2: "Admin-only components never enter the quiz chunk (D16), so the performance.md budgets are unchanged."
- `docs/performance.md` §3, "Admin-only strings on demand": the dashboard namespace is registered with `addResourceBundle` when its admin chunk loads, "so student pages do not download admin copy".
- React skill §18 (route-level splitting).

**Problem:** I measured these on this branch with `npm run build` and then `npm run perf:budget`:
- The report is entry 205/210, landing 214/220, lesson 235/240, **quiz 255/255** and admin-dashboard 224/230. The implementer's baseline was 203/212/232/252/221, so every student route grew by 2–3 KB brotli, and no student can use this feature yet.
- The entry chunk `dist/assets/index-DItCUCi1.js` contains `zoneOverCapacity`, `diagramKeyOrdered` and `QUESTION_DIAGRAM_ZONES_OVERLAP`. The added locale lines alone come to about 2.55 KB brotli, of which 1.76 KB is the questions namespace.
- The quiz chunk `dist/assets/QuestionView-*.js` contains the compiled DragDrop zod objects (`image:{key,url,width,height,alt}` and `zones:[{zoneId,itemIds,ordered}]`). They come in through `questionContentSchemas.ts`. Zod object construction is not tree-shaken, so the whole module ships.
- `docs/performance.md` §8 already records the lesson 2 s target as missed (#220).

**Failure:** the quiz page has 0 KB of headroom. The next change that adds 1 KB brotli to the quiz route fails `perf:budget` in web-ci. That includes lane #122 (MathSteps), which is going first and needs that headroom. The only other way out is raising a budget with no user-facing reason: this story's growth is copy and schemas for a question type that is not servable.

**Fix:**
1. Move the DragDrop-only questions keys into a feature-local namespace, for example `questionsDiagram`. Register it with `i18n.addResourceBundle` from a `locales.ts` that only `DragDropFields`, `DragDropPreview` and `DiagramKeyLegend` import, following the `features/dashboard/locales.ts` pattern. Do not register it from `app/i18n.ts`.
2. Register the 21 new admin-only `common:errors.*` strings in the same lazy module, deep-merged into `common` (addResourceBundle with deep=true, overwrite=false). The `common:errors.<code>` lookups keep working once an editor chunk has loaded.
3. Move `dragDropBodySchema` and `dragDropSpecSchema` into their own module, for example `schemas/dragDropContentSchemas.ts`, imported only by `api/dragDropValues.ts`.
4. Re-run `perf:budget`. Quiz and lesson should be back within 1 KB of 252/232, and the entry within 1 KB of 203.
5. Add one line to `docs/performance.md` §3 naming the new lazy namespace.

## Non-blocking
- `api/Elmanhg.Application/Questions/Shared/DiagramImageKey.cs:10`: the key is pinned to the diagram prefix but not to the question's lesson. Cross-lesson reuse is harmless today because no lesson media is ever deleted (only TrainingExports call `IFileStorage.DeleteAsync`). Add the binding before orphan cleanup ships: create and update know `LessonId`, so the key prefix `question-diagrams/{lessonId}/` could be checked there.
- `api/Elmanhg.Tests/Application/Features/Questions/Shared/DragDropQuestionRulesTests.cs:13` (MessyBody): no test sends `image.url` next to a valid key and asserts it is dropped. Today it is dropped by construction, since `DiagramImage` has no `Url`. `docs/question-schemas.md` promises the drop, so a Normalize or I1 test should pin it before someone adds `Url` to the record.
- `api/Elmanhg.Application/Lessons/UploadDiagramImage/UploadDiagramImageHandler.cs:15`: the `UserId == default` branch has no test. This matches the UploadLessonImage sibling.
- `api/Elmanhg.Application/Questions/Shared/DiagramZoneRules.cs:73`: touching zones share an edge. #126 should use half-open intervals, `[x, x+w)`, when it maps a drop point to a zone, and should say so in question-schemas.md.
- `web/src/features/questions/components/DiagramImageField.tsx:29-50`: the optimise, size, upload and setValue steps are orchestration logic inside a component (skill §6.6). This matches the askTeacher ImageField precedent. It could move into `useDiagramImageUpload`.
- `web/src/features/questions/pages/NewDragDropQuestion.test.tsx`: there is no Arabic (RTL) render of the editor itself. Only the validation page has one (W34).

## Verified
- **API tests (CI parity):** `appsettings.json` was moved aside, `dotnet test api/ -c Release` was run, and the file was restored. Result: **4069 passed, 0 failed**, matching the report.
- **Web:** `typecheck` clean, `lint` 0 warnings, `prettier --check` clean. `vitest run`: **225 files, 1300 tests passed**. `build` passes. `perf:budget` numbers are exactly as reported.
- **Orval:** I snapshotted `web/src/shared/api/generated`, re-ran `npm run gen:api` and diffed. No drift. The OpenAPI adds `UploadDiagramImage` (multipart `file`), `UploadDiagramImageResult` and the `DragDrop` enum value.
- **Postman:** "Upload diagram image" (POST `/api/lessons/{{lessonId}}/diagram-images`, form-data `file`, inherited collection bearer auth, stores `json.key`) comes before "Create drag-and-drop question" (key-based body, stores `dragDropQuestionId`). Both come after "Grade essay draft". No orphaned requests.
- **Storage key and gate condition:**
  - `DiagramImageKey` accepts only the lowercase `question-diagrams/{guid}/{32hex}.(png|jpg|jpeg|webp)` pattern, with NonBacktracking.
  - Hosts, `/api/media/...`, `..`, query strings, `javascript:`, `data:`, `.svg` and `.PNG` are all rejected and tested.
  - The body record stores `Key` only, so a sent `url` is dropped by Normalize.
  - `QuestionBodyMedia.Resolve` adds `url` only for a valid key, and only on the admin and teacher detail reads.
  - `GetPublicUrl` is PublicBaseUrl (trailing slash trimmed) + "/" + key in both `LocalDiskFileStorage` and `S3FileStorage`, and `SaveAsync` reuses it.
- **Upload:** the extension allow-list, content-type set and magic-byte check (`TeacherThreadImageFormats.HasMatchingSignature`: PNG, JPEG and RIFF/WEBP), plus the size cap. SVG, GIF, text/html and SVG-bytes-named-.png are all 422 (V4–V6, I8, I9). The endpoint is ContentManage only (I11, I12) and audited (I7). `optimizeImage` renames to `.webp` when it re-encodes, so the extension and signature stay consistent.
- **Safe rendering:** the stored model is structured JSON. `DiagramSvg` renders image, rect, circle and text elements from numbers, and item and alt text as React text nodes. There is no `dangerouslySetInnerHTML`, and no SVG or HTML is stored.
- **Zone and key rules are sound for #126:**
  - Geometry is checked in integer hundredths with strict-inequality overlap. All 4 touching layouts are allowed and overlap is rejected.
  - The key lists every body zone once, and item ids are unique across zones.
  - Capacity is enforced per zone, and `ordered` needs at least 2 items.
  - Unordered ids are canonicalised to body item order, and ordered ids are kept as authored (A29, I1, I5).
- **Keyboard and accessibility:**
  - "Add zone" adds a 40/40/20/20 zone.
  - Each zone has labelled X, Y, width, height and capacity fields (`dir="ltr"`) and an "order matters" checkbox.
  - Each item has a native labelled select for its correct zone, including "none" (distractor).
  - Ordered zones have move up and move down buttons with i18n aria-labels, disabled at the ends. Icon buttons are labelled.
  - axe passes on the editor and the validation page (W32, W35).
- **RTL:** zone coordinates are physical and the canvas is never mirrored (docs and tokens agree). The Arabic order separator is an arrow pointing left. The validation page renders `dir="rtl"` (W34).
- **Servability:** `ServableQuestionSpecification` excludes DragDrop and `ServedTypes` is unchanged (D1, D2, I14 on the SQL and in-memory paths). grade-draft returns 422 `QUESTION_TYPE_NOT_GRADABLE` (G1, I6). The import ignores a DragDrop sheet (M1).
- **Docs sync:**
  - question-schemas.md describes the key and read-time URL model.
  - PRD §5.3, §6, §10.1 and §17 are updated, as are exam-blueprints, mastery, question-import, audit-log, claude-design-prompt §4 and prototype.
  - design-system §5.13 agrees with the `.claude/design-system.md` DiagramCanvas row.
  - No divergence found.
- **Deviations:** every deviation listed follows from the plan-gate condition, and each one is present in code as described. `QuestionView` and the quiz feature import no diagram component. Only the shared schemas leak (finding 1).

## Test quality
- DragDropQuestionRulesTests: constrains the implementation. Each rule has a failing case, the overlap theory kills every clause, and A29 checks the canonical form with DeepEquals.
- DiagramImageKeyTests: constrains the implementation. The negative cases cover hosts, traversal, case, query strings and schemes.
- UploadDiagramImageValidatorTests: constrains the implementation. It uses real signature bytes, and the signature-mismatch case uses SVG bytes.
- UploadDiagramImageHandlerTests: constrains the implementation. It asserts the key folder and extension lowercasing, that SaveAsync is called with the returned key, and DidNotReceive on the throwing paths.
- GetQuestionHandlerTests.Handle_DragDropQuestion_ResolvesDiagramImageUrlFromKey: partly substitute-driven. The URL comes from the IFileStorage stub, but the test does pin that the resolver is wired in and writes `image.url`. The integration test Get_AdminDragDrop_ResolvesImageUrlFromStoredKey covers it for real and also asserts that the stored body has no `url`.
- Integration tests (I1–I14): constrain the implementation. They check DB state, the servable SQL and in-memory paths, policies and audit.
- Web tests (W1–W35): constrain the implementation. W25 drives real pointer events through getBoundingClientRect and asserts the exact request body. W27 asserts the moved order, and W30 maps a server error into the "Drop zones" group.
- Not guarded: no test asserts bundle placement, so finding 1 was caught only by perf:budget at its cap.
