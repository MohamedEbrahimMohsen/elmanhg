# Implementation — [E16.S1] Admin diagram authoring tool (#125)

Branch `feature/125-admin-diagram-authoring-tool`. `origin/main` (64ff902, #110 JSONL export) was fast-forward merged first, with no conflicts. Nothing is committed.

The **plan-gate condition in `00-acceptance.md` overrides the plan's image-URL design**. The diagram image is stored as a storage **key**, and the key is validated against the diagram prefix. The public URL is resolved at read time by the layer that knows `PublicBaseUrl`. Every deviation that follows from this is listed below.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Questions/Schemas/DragDropSchemas.cs | 13 | Body and spec records. `DiagramImage(Key, Width, Height, Alt)` |
| api/Elmanhg.Application/Questions/Shared/DiagramImageKey.cs | 13 | Replaces the plan's `DiagramImageUrl`. `StorageFolder` plus a NonBacktracking regex: `^question-diagrams/{guid}/{32hex}.(png\|jpg\|jpeg\|webp)$` |
| api/Elmanhg.Application/Questions/Shared/DragDropQuestionRules.cs | 84 | Validate and Normalize (image, items, key canonicalisation) |
| api/Elmanhg.Application/Questions/Shared/DiagramZoneRules.cs | 74 | Zone count, ids, bounds in hundredths, capacity, overlap |
| api/Elmanhg.Application/Questions/Shared/DiagramKeyRules.cs | 39 | Key zones, items, capacity, empty key, order |
| api/Elmanhg.Application/Questions/Shared/QuestionBodyMedia.cs | 20 | **Gate, not in plan.** On read, a DragDrop body gets `image.url = IFileStorage.GetPublicUrl(key)` |
| api/Elmanhg.Application/Lessons/UploadDiagramImage/UploadDiagramImageCommand.cs | 12 | Audited command `Lesson.UploadDiagramImage` |
| api/Elmanhg.Application/Lessons/UploadDiagramImage/UploadDiagramImageValidator.cs | 31 | Extension, content type, magic bytes and size |
| api/Elmanhg.Application/Lessons/UploadDiagramImage/UploadDiagramImageHandler.cs | 33 | Stores `question-diagrams/{lessonId}/{guid:N}{ext}` and returns `{Key, Url}` |
| api/Elmanhg.Application/Lessons/UploadDiagramImage/UploadDiagramImageResult.cs | 3 | `UploadDiagramImageResult(string Key, string Url)` |
| api/Elmanhg.Tests/.../Questions/Shared/DragDropQuestionRulesTests.cs | 273 | A1–A29 |
| api/Elmanhg.Tests/.../Questions/Shared/DiagramImageKeyTests.cs | 37 | U1–U2, rewritten for keys |
| api/Elmanhg.Tests/.../Lessons/UploadDiagramImage/UploadDiagramImageHandlerTests.cs | 70 | H1–H3 |
| api/Elmanhg.Tests/.../Lessons/UploadDiagramImage/UploadDiagramImageValidatorTests.cs | 82 | V1–V7 |
| api/Elmanhg.Tests/Integration/Content/DragDropQuestionEndpointTests.cs | 175 | I1–I6, plus a gate test that the GET resolves the URL |
| api/Elmanhg.Tests/Integration/Content/DiagramImagesEndpointTests.cs | 136 | I7–I12 |
| api/Elmanhg.Tests/Integration/QuestionValidation/DragDropValidationEndpointTests.cs | 65 | I13–I14 |
| web/src/features/questions/api/dragDropValues.ts | 156 | Value mappers, id helpers, assign, move, `toDiagramModel` |
| web/src/features/questions/api/diagramGeometry.ts | 58 | Pointer to percent, rectangle, overlap and inside checks |
| web/src/features/questions/api/imageSize.ts | 9 | `readImageSize` through `createImageBitmap` |
| web/src/features/questions/schemas/dragDropRules.ts | 93 | `addDragDropIssues` |
| web/src/features/questions/hooks/useDiagramImageUpload.ts | 28 | Wraps `useUploadDiagramImage` |
| web/src/features/questions/hooks/useZoneDrawing.ts | 54 | Pointer drawing |
| web/src/features/questions/components/DiagramSvg.tsx | 90 | SVG renderer with edit, student and key tones |
| web/src/features/questions/components/DragDropPreview.tsx | 44 | Student view or key view |
| web/src/features/questions/components/DiagramKeyLegend.tsx | 39 | Placements legend |
| web/src/features/questions/components/DragDropFields.tsx | 42 | Editor composition |
| web/src/features/questions/components/DiagramImageField.tsx | 83 | Upload, size read, alt |
| web/src/features/questions/components/DiagramCanvasEditor.tsx | 37 | Drawing canvas |
| web/src/features/questions/components/DiagramZonesField.tsx | 46 | Zone list |
| web/src/features/questions/components/DiagramZoneCard.tsx | 95 | Zone fields and ordered move buttons |
| web/src/features/questions/components/DiagramItemsField.tsx | 63 | Item list |
| web/src/features/questions/components/DiagramItemRow.tsx | 65 | Item text, correct-zone select, remove |
| web/src/features/questions/api/{dragDropValues,diagramGeometry,imageSize}.test.ts | 116/31/26 | W1–W13 |
| web/src/features/questions/pages/NewDragDropQuestion.test.tsx | 255 | W25–W32 |
| web/src/features/questions/pages/ValidationDragDropQuestion.test.tsx | 110 | W33–W35 |
| web/src/shared/api/generated/model/uploadDiagramImage{Body,Result}.ts | 11/11 | Orval output |

## Files modified
| Path | Change |
|---|---|
| Domain `QuestionType.cs` | `DragDrop` appended |
| Domain `ServableQuestionSpecification.cs` | `&& x.Type != QuestionType.DragDrop`, with the #126 comment. `ServedTypes` unchanged |
| App `QuestionSchemaRules.cs` | DragDrop arms in Validate and Normalize |
| App `GradeQuestionDraftValidator.cs` | `QuestionTypeNotGradable` rule, and the answer rule skips DragDrop |
| App `ContentOptions.cs` | 7 diagram caps with code defaults |
| App `Exceptions/ErrorCodes.cs`, Api `Messages.{ar,en}.resx` | 21 codes after `QUESTION_MODEL_ANSWER_TOO_LONG` |
| App `Shared/Storage/IFileStorage.cs` | **Gate.** `string GetPublicUrl(string key)` |
| Infra `LocalDiskFileStorage.cs`, `S3FileStorage.cs` | **Gate.** Implement `GetPublicUrl`; `SaveAsync` returns it |
| App `QuestionResultGenerator.cs`, `ValidationResultGenerator.cs`, `GetQuestionHandler.cs`, `GetValidationQuestionHandler.cs` | **Gate.** Inject `IFileStorage`; the body goes through `QuestionBodyMedia.Resolve` |
| Api `LessonsController.cs` | `UploadDiagramImage` action (`ContentManage`, multipart) |
| Api `appsettings.example.json`, Tests `ApiFactory.cs` | 7 `Content:QuestionDiagram*` keys |
| Tests `QuestionBuilder.cs` | `DragDrop()`, `DragDropImageKey` (instead of `DragDropImageUrl`), body and spec JSON, content, fields |
| Tests `ServableQuestionSpecificationTests.cs` | D1 added; D2 renamed and changed as planned |
| Tests `OpenApiEndpointTests.cs` | Enum now ends with `"DragDrop"` |
| Tests `GradeQuestionDraftValidatorTests`, `QuestionFieldsValidatorTests`, `QuestionImportColumnsTests` | G1, F1, M1 |
| Tests `GetQuestionHandlerTests.cs` | Constructor gets an `IFileStorage` substitute; **new** `Handle_DragDropQuestion_ResolvesDiagramImageUrlFromKey` |
| Tests `GetValidationQuestionHandlerTests.cs` | Constructor gets an `IFileStorage` substitute (no assertion changed) |
| api/openapi/v1.json; web/src/shared/api/generated/** | Regenerated (new operation, `UploadDiagramImageResult {key,url}`, enum) |
| postman collection | Variables `diagramImageKey` and `dragDropQuestionId`. "Upload diagram image" stores `json.key`, and "Create drag-and-drop question" (body uses `"key": "{{diagramImageKey}}"`) comes after "Grade essay draft" |
| web `content/index.ts` | Exports `optimizeImage` |
| web `questionOptions.ts`, `questionEditorSchema.ts`, `questionContentSchemas.ts`, `questionValues.ts`, `studentQuestion.ts`, `answerKey.ts`, `questionErrorFields.ts`, `quiz/api/quizItem.ts`, `quiz/api/correctAnswer.ts` | As in the plan (`diagramImage` also carries `key`) |
| web `TypeSpecificFields.tsx`, `QuestionEditorForm.tsx`, `QuestionPreviewPanel.tsx`, `ValidationQuestionContent.tsx` | As in the plan |
| web i18n: questions en/ar, shared en/ar | Plan keys; 21 error texts identical to the resx |
| web tests: `questionEditorSchema.test.ts`, `studentQuestion.test.ts`, `answerKey.test.ts`, `quiz/api/quizItem.test.ts`, `quiz/api/correctAnswer.test.ts` | W14–W24 appended |
| docs: question-schemas, PRD, exam-blueprints, mastery, question-import, audit-log, claude-design-prompt, prototype, design-system; `.claude/design-system.md` | As in the plan. question-schemas describes the key and read-time URL model (not D5's URL rule) |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| D5 and #2: `DiagramImageUrl` validates a stored URL; body `image.url` | The gate condition in 00-acceptance overrides this: store a key, and resolve the URL at read time | `DiagramImageKey` (#2 renamed) validates `question-diagrams/{guid}/{32hex}.{png,jpg,jpeg,webp}` (lower case, no host, no dot segments). The body stores `image.key`, and any `url` sent in is dropped by Normalize |
| No read-time mapping | The gate requires the URL to be resolved in the layer that knows `PublicBaseUrl` | Added `IFileStorage.GetPublicUrl` (Local and S3 already built `PublicBaseUrl/key` in `SaveAsync`; now shared). Added `QuestionBodyMedia.Resolve`, used by the admin detail and the teacher validation detail. Student serving paths (session and exam) are untouched because DragDrop is not served; #126 must call the same resolver |
| #9 `UploadDiagramImageResult(string Url)` | The editor needs the key to store and the URL to preview | `UploadDiagramImageResult(string Key, string Url)` |
| #14 `upload: (file) => Promise<string \| null>` | Needs both values | `Promise<UploadDiagramImageResult \| null>` |
| Web `diagramImage: {url,width,height,alt}`; rule "`url === ''` → diagramImage" | Key model | `{key, url, width, height, alt}` (url is display-only and never sent). The rule fires on `key === ''`; `toDragDropContent` sends `{key,width,height,alt}`; `dragDropBodySchema.image.url` is optional |
| U1/U2 `IsValid_StoredDiagramUrl_ReturnsTrue` / `IsValid_OtherUrl_ReturnsFalse` in `DiagramImageUrlTests` | The class is renamed | `DiagramImageKeyTests.IsValid_StoredDiagramKey_ReturnsTrue` / `IsValid_OtherKey_ReturnsFalse`. The false cases include `/api/media/…`, `https://…`, `.svg`, `.PNG`, `?x=1`, `..`, `javascript:`, `data:` |
| A5 `Validate_ImageUrlNotAStoredDiagram_…` | Key model | Renamed `Validate_ImageKeyNotAStoredDiagram_ReturnsQuestionDiagramImageInvalid`; it also rejects a `/api/media/...` URL |
| QuestionBuilder `DragDropImageUrl` | Key model | `DragDropImageKey`. The canonical JSON has `"key"` |
| A14 is a `[Fact]` (z1 left of z2 only) | The mutation check showed 3 of the 4 overlap clauses survived | Made it a `[Theory]` with 4 touching layouts (left/right and above/below, both orders). All 4 clause mutations are now killed |
| I8 asserts `code == QUESTION_DIAGRAM_IMAGE_TYPE_INVALID` | Two validator rules fail, so `code` is joined (`X,X`); the `LessonImagesEndpointTests` sibling uses `Contain` | Uses `.Should().Contain(...)` |
| Test files limited to the plan list | The gate adds behaviour (URL resolution) | Added `Handle_DragDropQuestion_ResolvesDiagramImageUrlFromKey` (GetQuestionHandlerTests) and `Get_AdminDragDrop_ResolvesImageUrlFromStoredKey` (DragDropQuestionEndpointTests). I13 also asserts `body.image.url`. The Get*HandlerTests constructors were updated for the new dependency |
| DiagramItemRow and DiagramImageField small details | — | The item row is a flex column for the text and select, because no `sm:` breakpoint token exists. The image field uses `useWatch`, not `watch` |

## Build & test
- `git fetch && git merge origin/main`: fast-forward to 64ff902. PROGRESS.md was preserved through a stash.
- `dotnet build` (Api and Tests, Release): succeeded, with only the pre-existing core-libraries warnings. `api/openapi/v1.json` was regenerated.
- CI parity: `appsettings.json` moved aside, `dotnet test -c Release`: **Passed, total 4069, failed 0** (2m43s). The file was restored afterwards. The first run had 1 failure (the I8 joined code), which I fixed as described above.
- `dotnet format --verify-no-changes`: only pre-existing findings in core-libraries and `SubscriptionBuilder.cs`. None of the files I touched are flagged.
- Web: `npm run typecheck` passes. `npm run lint` passes (0 warnings). `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: all files clean. `npx vitest run`: **225 files, 1300 tests passed**. `npm run build`: passes. `npm run gen:api` re-run: no further drift.
- `npm run perf:budget`: entry 205/210, landing 214/220, lesson 235/240, **quiz 255/255**, admin-dashboard 224/230. All ok. The baseline on this branch without my web changes was 203/212/232/252/221.
- Mutation check (production line changed → targeted tests re-run), everything killed:
  - API: the servable clause; the key regex; the URL resolution; the grade-draft rule; all 4 overlap clauses; the decimal tolerance; key duplicate zones and items; unordered sorting; alt trim; the signature check; the upload key folder; the Validate arm.
  - Web: the overlap issue; the move swap; the distractor legend; the pointer clamp; the drawing move; the key stored after upload.

## Notes for review
- **The quiz route is at its cap (255/255 KB).** The +2–3 KB on every route comes mostly from the 21 new shared error strings (both languages ship in the entry) and from `dragDropBodySchema`/`dragDropSpecSchema` in `questionContentSchemas.ts`, which the quiz imports through the barrel. The budget passes, but the next lane (#122 MathSteps) has no headroom. The schemas could move to a DragDrop-only module if needed.
- The key regex does not check that the key's lesson id matches the question's lesson. `QuestionSchemaRules.Validate` has no lesson id. Reusing a diagram from another lesson is harmless because the image is still one of our stored, public, raster files.
- `QuestionBodyMedia` adds `url` only when the stored key is valid. It is used only by the admin and teacher detail reads. The admin list, revisions and audit diffs contain only the key.
- `GetPublicUrl` is a new member of the port. NSubstitute test doubles are unaffected, and there are no hand-written fakes of `IFileStorage`.
- `QuestionPreviewPanel` calls `useState(showKey)` before the other hooks and returns early for DragDrop after every hook has run, so hook order is stable.
- `LessonsController.cs` is now 129 lines. It was already 118 before this change.
- Docs: question-schemas.md describes the key and read-time URL model. The plan's D5 text (URL shape, https host allowed) was intentionally not written, because the gate replaced it.
