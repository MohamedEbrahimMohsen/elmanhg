VERDICT: APPROVED

# Review r2 — [E16.S1] Admin diagram authoring tool (#125)

## Blocking
None. Blocking #1 from 03-review.md is resolved.

## Non-blocking
- `web/src/features/questions/diagramLocales.test.ts:25-26` — the "without overwriting common strings" test checks `UNHANDLED_EXCEPTION`, which is not a key in `diagramErrors.*.json`. It would pass with `overwrite=true`, so it does not pin the `false` at `diagramLocales.ts:13-14`. To pin it, pre-seed one diagram code in `common` and assert that it survives.
- Carried from r1, still open and fine to leave: the `UserId == default` test for UploadDiagramImage, `[x, x+w)` hit-testing for #126, `DiagramImageField` orchestration, and an Arabic render of the editor.

## Verified
- **Finding 1a: strings are lazy.**
  - `shared/i18n/{ar,en}.json` have no diff against HEAD.
  - `features/questions/i18n/{ar,en}.json` add only `types.DragDrop`, which the eager type labels need.
  - `registerDiagramLocales` (`diagramLocales.ts:9-15`) adds `questionsDiagram` and deep-merges the 21 codes into `common` with `deep=true, overwrite=false`.
  - It is called only at module load in DragDropFields, DragDropPreview, DiagramKeyLegend and QuestionPreviewPanel. It is not called from `app/i18n.ts`.
  - `dragDropRules.ts:9` issues keys prefixed `questionsDiagram:`.
- **Finding 1b: schemas are out of the quiz chunk.** `schemas/dragDropContentSchemas.ts` is imported only by `api/dragDropValues.ts`, and `questionContentSchemas.ts` has no diff against HEAD.
- **Readable message before registration.**
  - Every server-code render site uses the `[code, 'errors.UNHANDLED_EXCEPTION']` fallback: toasts, TextField, TextAreaField, FormRootError, DiagramImageField:74, QuestionPreviewPanel:78 and RouteError. An unregistered `QUESTION_DIAGRAM_*` code therefore shows the generic message, not a raw key.
  - The only producers of these codes reachable from the UI are the editor save path, the grade-draft preview and the validation page. All three import a registering module in the same chunk (DragDropFields via TypeSpecificFields, QuestionPreviewPanel, and DragDropPreview via ValidationQuestionContent), so in practice the specific message is shown.
- **Bundle placement, measured.**
  - `npm run build` then `perf:budget`: entry 203/210, landing 212/220, lesson 233/240, quiz 253/255, admin-dashboard 221/230. That is within 1 KB of the pre-change 203/212/232/252/221 and matches the r2 report.
  - `zoneOverCapacity`, `QUESTION_DIAGRAM_ZONES_OVERLAP`, `zoneId` and `diagramKeyOrdered` appear only in `DragDropPreview-*.js` and `QuestionEditorHeader-*.js`.
  - The entry (`index-*.js`) mentions those chunks only in the `__vite__mapDeps` lazy-route preload list. They are not in `QuestionView-*.js`.
- **Docs.** `docs/performance.md:60` names the lazy namespace, the fallback and the schema split. The bullet is in §4; the implementer's §3 → §4 correction is accurate.
- **NB-1: lesson binding.**
  - `DiagramImageKey.BelongsToLesson` (`DiagramImageKey.cs:14`) combines IsValid with the ordinal prefix `question-diagrams/{lessonId}/`. `Guid.ToString()` is lowercase, which matches the regex.
  - `QuestionBodyMedia.EnsureLessonMedia` (`QuestionBodyMedia.cs:24-30`) throws 422 `QUESTION_DIAGRAM_IMAGE_INVALID`. It runs before `CreateContent` in Create:34, Update:33 (the lesson comes from `question.LessonId`) and Resubmit:32, and before any mutation or save.
  - Tests:
    - The unit tests assert the error code, DidNotReceive on AddAsync and SaveChangesAsync, and that version and status are unchanged.
    - The positive create test asserts Received(1).
    - The BelongsToLesson theory covers another lesson, traversal and null.
    - The integration test `Post_DiagramKeyFromAnotherLesson_Returns422QuestionDiagramImageInvalid` asserts the code and that no row was written.
  - `docs/question-schemas.md:108,173` agree with the code.
- **NB-2: sent `url` is dropped.** `DragDropQuestionRulesTests.Normalize_DragDropImageUrlSentInBody_IsDropped` (line 253) sends a foreign `url`. It asserts the key is absent and that the result DeepEquals the canonical body.
- **API, CI parity.** I moved `appsettings.json` to the scratchpad, ran `dotnet test api/ -c Release` and restored the file. Result: 4079 passed, 0 failed, 0 warnings, matching the report.
- **Web.**
  - `typecheck` is clean and `lint` has 0 warnings.
  - `vitest run`: 226 files, 1303 tests passed.
  - `build` passes and `perf:budget` passes. All of this matches the report.
- **Postman.** The collection is unchanged, which is correct: no endpoint surface changed in r2.

## Test quality
- The lesson-binding handler and integration tests constrain the implementation: removing any of the three `EnsureLessonMedia` calls fails a test.
- `diagramLocales.test.ts` guards eager `common` against diagram codes and checks ar/en key parity. The overwrite assertion is vacuous (see non-blocking).
