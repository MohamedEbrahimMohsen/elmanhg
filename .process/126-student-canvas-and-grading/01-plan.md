# Plan — [E16.S2] Student canvas and grading (#126, epic #124, follow-up #242)

## Goal
A student meets drag-and-drop diagram questions in quizzes and unit/multi-unit exams: on a phone or desktop they place item chips into the numbered zones of the diagram by dragging (mouse, touch, pen) or by choosing an item and then a zone (tap or keyboard), with screen-reader announcements. The server grades the placements deterministically per item, returns a per-item tally feedback line, and after checking (quiz) or submitting (exam) the student sees each item marked in place / wrong place / not placed plus a key view with the correct placements. The admin editor's preview uses the same canvas and «جرّب الإجابة» now grades drag-and-drop. Drag-and-drop questions become servable (counts, blueprints, mastery).

## Scope
**In:**
- Domain `DragDropGrader` (per-item partial credit, ordered zones, distractor penalty) wired into `QuestionGrader.Grade`.
- Answer shape `{"placements":[{"zoneId","itemIds"}]}`: read check, canonicalisation, per-type caps (`Sessions:DragDropAnswerMaxLength`, `Sessions:DragDropPlacementsMaxCount`, `Sessions:DragDropPlacedItemsMaxCount`) on quiz answer, exam save and `grade-draft`.
- Feedback kind `PlacementTally` + Arabic/English lines.
- DragDrop servable (remove the exclusion; `ServedTypes` = all 8 types) → blueprints, exams, quizzes, counts.
- Diagram image URL resolved server-side (`QuestionBodyMedia.Resolve`) in every quiz/exam item result.
- `grade-draft` grades DragDrop; `QUESTION_TYPE_NOT_GRADABLE` retired.
- Web: lazy student canvas (`DragDropAnswerInput`), pointer drag + tap/keyboard alternative, aria-live announcements, RTL (canvas physical LTR), per-item marks, lazy correct-placements view in the feedback panel / review items, admin preview on the student canvas with «جرّب الإجابة».
- #242 item 1: drop point → zone mapping with half-open intervals `[x, x+w)` (compared in hundredths), documented.
- Docs: PRD §5.3/§6/§17, question-schemas, sessions, exams, mastery, exam-blueprints, performance, design-system (both), claude-design-prompt §4, prototype.md.

**Out:**
- Avatar context text for drag-and-drop answers (`AvatarAnswerText` returns `null` for DragDrop, as today) — follow-up issue.
- Other #242 non-blocking review notes (locale test pin, upload-handler test, editor RTL test, error fallback before editor load).
- Spreadsheet import of DragDrop (PRD §10.1 keeps it editor-only).
- Chunk preloading of the canvas before a DragDrop item is shown (next-item image is preloaded).

**Deferred:** none. (No external provider involved.)

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Answer shape | `{"placements":[{"zoneId":"z1","itemIds":["i1","i2"]}]}`; list order of `itemIds` = order inside the zone; an item in no placement is in the bank. Missing `placements` = no answer. | Mirrors the key shape (`zones[].zoneId/itemIds`), carries order for ordered zones. |
| 2 | Partial-credit rule | Credit units = keyed items `K` (items the key places in a zone). `right` = keyed items in their key zone and, when that zone is `ordered`, at their key index. `wrong` = placed ids the key places nowhere (distractors or unknown ids). A keyed item in the wrong zone/position is simply not right (no extra penalty). Normalised = 1 when `right = K ∧ wrong = 0`, else `max(0, (right − wrong) / K)`. | "Per item" (PRD §6) without free credit for leaving distractors in the bank; same shape as Multi's `(right − wrong)/total` (unknown id counts as wrong there too). |
| 3 | Unanswered | No item placed in any known zone → `NormalisedGrade.Unanswered` (0, feedback Unanswered). | Consistent with every other type; stops "leave everything in the bank". |
| 4 | Ordered zone credit | Exact position match per item (index in the zone's resolved list). | Deterministic, per item, simple to explain. |
| 5 | Malformed-but-readable answers | Grader tolerant: placement with a zone id not in the key → ignored; repeated zone id → first entry used; item id repeated across placements → first occurrence counts; null item id → skipped. `CanRead` rejects null placement, null/non-string `zoneId`, null item id (422 `QUESTION_ANSWER_INVALID`). | Fill/Multi precedent (ignore unknown blank ids, count once); rejection only for type violations like MathSteps null step. |
| 6 | Capacity on server | Not enforced by the grader (it has no body). Over-capacity cannot earn more: each keyed item counts once. The web canvas enforces capacity. | Keeps the Domain grader a pure function of spec + answer, like every other grader. |
| 7 | Grader inputs | `DragDropGrader.Grade(DragDropGradingSpec, DragDropAnswer)` — spec only; distractors are "not in the key". | No signature change to `QuestionGrader.Grade`/`AnswerGrader`/`QuestionRevision.Grade`. |
| 8 | Feedback | New `GradeFeedbackKind.PlacementTally` (appended last) with `right, wrong, total`; returned when answered and not fully correct. Key `GRADE_FEEDBACK_PLACEMENT_TALLY`. | Mirrors `ChoiceTally`; never reveals which items are wrong. |
| 9 | Answer caps | New `SessionsOptions`: `DragDropAnswerMaxLength` = 4000 (raw JSON), `DragDropPlacementsMaxCount` = 20, `DragDropPlacedItemsMaxCount` = 30 (Σ itemIds). Over → 422 `ATTEMPT_ANSWER_TOO_LONG`. `RequestAnswerMaxLength` includes the new raw cap. | #119/#122 pattern (per-type raw cap + structural caps in `Sessions`); 20/30 mirror `Content:QuestionDiagramZonesMaxCount`/`ItemsMaxCount`. |
| 10 | Image URL for students | `SessionResultGenerator.GenerateItem` returns `QuestionBodyMedia.Resolve(type, snapshotBodyJson, fileStorage)`, so `body.image.url` = `FileStorage:PublicBaseUrl` + stored key. `IFileStorage` is appended as the last constructor/method parameter along the chain. The web renders only `body.image.url` from the response; it never builds a URL from the key. | Only stored keys exist (#125); resolution stays on the server; appended params keep parallel-lane merges additive. |
| 11 | Servable | Remove `x.Type != QuestionType.DragDrop` from `QuestionCondition`; `ServedTypes` lists all 8 types in enum order. Web `servedQuestionTypes` adds `'DragDrop'`; blueprint form gains a DragDrop count. | The student canvas ships in this story (PRD §5.3 note). |
| 12 | grade-draft | Drop the `QuestionTypeNotGradable` rule; DragDrop goes through `CanRead` + `ExceedsLimits` + `AnswerGrader` like every local type. Delete `ErrorCodes.QuestionTypeNotGradable`, its two resx entries and the two web `diagramErrors` entries. | Every type is gradable now; a dead code is noise. |
| 13 | Admin preview | DragDrop preview renders `QuestionView` (the student canvas) and the shared «جرّب الإجابة»; «إظهار الأماكن الصحيحة» swaps in `DragDropPreview` (key only). `DragDropPreview` loses its `showKey` prop and bank branch; `preview.dragDropGradingHint` and `view.diagramBank` strings are removed. | "Live student preview" + real grader, as for every other type. |
| 14 | Interaction model | No dependency. Pointer Events (mouse/touch/pen) with a 6 px start threshold; tap/click/Enter/Space selects a chip (`aria-pressed`), then a zone button (on the image or «ضعه هنا» in the zone list) places it; Escape clears selection or cancels a drag. The zone list (≥ 44 px targets) is the equivalent control for tiny image zones (WCAG 2.5.7/2.5.8). | Accessible, touch-first, zero bundle cost. |
| 15 | Canvas rendering | Interactive canvas = HTML overlay (`<img>` + absolutely positioned zone `<button>`s) inside `dir="ltr"`, positioned with `insetInlineStart`/`top` in % (under LTR = physical left). Key view reuses `DiagramSvg` (tone `key`). | Buttons need real focus/labels; logical property rule kept while coordinates stay physical (design-system §5.13). |
| 16 | Drop mapping (#242) | Client-only: point → unrounded percent (`toCanvasPercent`), then zone with `x ≤ px < x+w` and `y ≤ py < y+h`, compared in hundredths (`Math.round(v*100)`); an edge on the image border (`x+w = 100` / `y+h = 100`) is closed. No zone → nothing dropped. Server receives zone ids only. | Touching zones share an edge; hundredths match the #125 overlap rule. |
| 17 | Order UI | Every zone holding ≥ 2 items shows move earlier/later buttons; the body never says which zones are ordered. New placements append at the end. | Body carries no answer (#125). |
| 18 | Marks after check | Placed keyed item in key zone (and key index when ordered) → `correct`; any other placed item → `wrong`; keyed item left in bank → `missed`; distractor in bank → no mark. Icon + sr-only text, not colour alone. | Mirrors grader decision #2 truthfully. |
| 19 | Lazy loading | `DragDropAnswerInput` lazy in `AnswerInputs`; `DragDropCorrectAnswer` lazy behind `LazyDragDropCorrectAnswer` (barrel export). Student strings in new namespace `diagramStudent`, registered at module load of those two lazy modules; admin `questionsDiagram` strings are never loaded by student pages. | Quiz budget 245/255 KB (docs/performance.md). |
| 20 | Student schemas | New `schemas/studentDiagramSchema.ts` (`studentDiagramBodySchema` requires `image.url`; `diagramKeySchema`). `dragDropContentSchemas.ts` re-uses `diagramKeySchema` as `dragDropSpecSchema`. | Small runtime parse needed in the quiz chunk; admin schema file stays admin-only. |
| 21 | Web answer model | `QuestionAnswer.placements: DiagramPlacements` (`Record<zoneId, readonly string[]>`), `emptyAnswer().placements = {}`; `StudentQuestion.diagram?: StudentDiagram \| null \| undefined` (optional → existing test helpers compile). `ChoiceReview.diagramKey?: DiagramKey \| undefined`. | Minimal churn; review marks flow through the existing `review` prop. |
| 22 | Focus after a non-drag placement / return / move | Focus the moved chip (by item id) after the state update. | Focus never drops to `<body>` when the source button disappears. |
| 23 | API artefacts | No endpoint, request or response schema changes (answers and bodies are `JsonElement`): OpenAPI, Orval and Postman unchanged. No migration. | Nothing to regenerate. |
| 24 | Morabh | Nothing reusable (searched `D:\Personal\Projects\Projects\Morabh\repos\apis` for drag/drop/zone/partial-credit): every new file is "new — no Morabh equivalent". | Reuse-first rule. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/ServableQuestionSpecification.cs` | Remove `&& x.Type != QuestionType.DragDrop` and the `#126` comment; `ServedTypes` = `[Mcq, Multi, TrueFalse, Fill, Short, Essay, MathSteps, DragDrop]`. |
| `api/Elmanhg.Domain/Questions/Schemas/DragDropSchemas.cs` | Append `public sealed record DiagramPlacement(string? ZoneId, List<string?>? ItemIds);` and `public sealed record DragDropAnswer(List<DiagramPlacement?>? Placements);`. |
| `api/Elmanhg.Domain/Questions/Grading/GradeFeedbackKind.cs` | Append `PlacementTally` as the last member. |
| `api/Elmanhg.Domain/Questions/Grading/GradeFeedback.cs` | Add `public static GradeFeedback PlacementTally(int right, int wrong, int total) { return new(GradeFeedbackKind.PlacementTally, right, wrong, total); }`. |
| `api/Elmanhg.Domain/Questions/Grading/QuestionGrader.cs` | Switch arm `QuestionType.DragDrop => DragDropGrader.Grade(ReadSpec<DragDropGradingSpec>(gradingSpec), ReadAnswer<DragDropAnswer>(answer)),` before `_`. |
| `api/Elmanhg.Application/Shared/Options/SessionsOptions.cs` | Add `[Range(1, 100000)] DragDropAnswerMaxLength = 4000`, `[Range(1, 100)] DragDropPlacementsMaxCount = 20`, `[Range(1, 1000)] DragDropPlacedItemsMaxCount = 30`; `RequestAnswerMaxLength => Math.Max(AnswerMaxLength, Math.Max(EssayAnswerMaxLength, Math.Max(MathStepsAnswerMaxLength, DragDropAnswerMaxLength)))`. |
| `api/Elmanhg.Application/Questions/Shared/QuestionAnswerRules.cs` | `CanRead`: `QuestionType.DragDrop => DragDropAnswerRules.CanRead(answer)`; `Canonicalize`: `QuestionType.DragDrop => DragDropAnswerRules.Canonicalize(answer)`; `ExceedsLimits` becomes `type switch { MathSteps => MathStepsAnswerRules.ExceedsLimits(answer, options), DragDrop => DragDropAnswerRules.ExceedsLimits(answer, options), _ => false }`; `RawAnswerMaxLength`: `QuestionType.DragDrop => options.DragDropAnswerMaxLength`. |
| `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftValidator.cs` | Delete the `QuestionTypeNotGradable` rule; the `CanRead` rule's `.When` drops `&& x.Question.Type != QuestionType.DragDrop`. |
| `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackKeys.cs` | Add `public const string PlacementTally = "GRADE_FEEDBACK_PLACEMENT_TALLY";`. |
| `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackText.cs` | Arm `GradeFeedbackKind.PlacementTally` → `localizer.GetMessage(GradeFeedbackKeys.PlacementTally, context: { right, wrong, total })` (same dictionary as ChoiceTally). |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Delete `QuestionTypeNotGradable`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Delete `QUESTION_TYPE_NOT_GRADABLE`; add `GRADE_FEEDBACK_PLACEMENT_TALLY` (text in Error codes section). |
| `api/Elmanhg.Application/Sessions/Shared/SessionResultGenerator.cs` | `Generate(Session, IReadOnlyCollection<QuestionRevision>, ILocalizer, IFileStorage fileStorage)`, `GenerateItem(Session, SessionItem, QuestionRevision, ILocalizer, IFileStorage fileStorage)`; body = `QuestionBodyMedia.Resolve(snapshot.Type, snapshot.Body?.ToJsonString() ?? "{}", fileStorage)`. |
| `api/Elmanhg.Application/Exams/Shared/ExamSessionResultGenerator.cs` | `Generate(..., ILocalizer localizer, IFileStorage fileStorage)` and `GenerateItem(..., ILocalizer localizer, IFileStorage fileStorage)`; pass through. |
| `api/Elmanhg.Application/Exams/Shared/ExamSessionResultLoader.cs` | `LoadAsync(..., ILocalizer localizer, IFileStorage fileStorage, CancellationToken cancellationToken)`; pass through both `Generate` calls. |
| `api/Elmanhg.Application/Sessions/StartQuizSession/StartQuizSessionHandler.cs`, `Sessions/GetSession/GetSessionHandler.cs`, `Sessions/FinishSession/FinishSessionHandler.cs`, `Sessions/SubmitAnswer/SubmitAnswerHandler.cs`, `Exams/GetExamSession/GetExamSessionHandler.cs`, `Exams/StartUnitExam/StartUnitExamHandler.cs`, `Exams/StartMultiUnitExam/StartMultiUnitExamHandler.cs`, `Exams/SubmitExam/SubmitExamHandler.cs` | Append `IFileStorage fileStorage` as the last primary-constructor parameter; pass it to the generator/loader call. |
| `api/Elmanhg.Api/appsettings.example.json` | `Sessions`: add `"DragDropAnswerMaxLength": 4000, "DragDropPlacementsMaxCount": 20, "DragDropPlacedItemsMaxCount": 30`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add the three `Sessions:DragDrop*` settings (`"4000"`, `"20"`, `"30"`). |
| `api/Elmanhg.Tests/Builders/SessionBuilder.cs` | Add `public Session BuildWithDragDrop(bool isTestMode = false) => Session.StartQuiz(StudentId, Questions.Lesson, [Questions.Approved().Build(), Questions.DragDrop().Approved().Build()], isTestMode);`. |
| `api/Elmanhg.Tests/Integration/Content/QuestionTestData.cs` | Add `SeedDragDropQuestionAsync(ApiFactory factory, Guid lessonId, CancellationToken cancellationToken)`: copy of `SeedMathStepsQuestionAsync` with `QuestionType.DragDrop` and content `QuestionBuilder.DragDropContent() with { Body = QuestionBuilder.ForLesson(QuestionBuilder.DragDropBodyJson, lessonId) }` (use the existing `ForLesson` helper; if `QuestionContent` is not a record, construct `new QuestionContent(stem, ForLesson(...), DragDropSpecJson, explanation, 4)`). |
| 12 handler test files (see Test plan M1) | Constructor call only: append `Substitute.For<IFileStorage>()` (or the test's `_fileStorage` field). |
| `web/src/features/questions/api/studentQuestion.ts` | `StudentQuestion.diagram?: StudentDiagram \| null \| undefined`; `QuestionAnswer.placements: DiagramPlacements`; `ChoiceReview.diagramKey?: DiagramKey \| undefined`; `emptyAnswer()` adds `placements: {}`; `toStudentQuestion` adds `diagram: type === 'DragDrop' ? studentDiagramFromValues(values) : null`; `toAnswerPayload` DragDrop → `{ placements: question.diagram ? toPlacementsPayload(question.diagram, answer.placements) : [] }`. |
| `web/src/features/questions/components/AnswerInputs.tsx` | Lazy `DragDropAnswerInput` (same pattern as `MathStepsAnswerInput`); `case 'DragDrop'` renders it in `<Suspense fallback={<div aria-busy="true" className="min-h-11" />}>` with `question, answer, onAnswerChange, disabled, review`. |
| `web/src/features/questions/components/QuestionPreviewPanel.tsx` | Single return: `isDragDrop && showKey ? <DragDropPreview stem diagram={toDiagramModel(values)} /> : <QuestionView question answer onAnswerChange={setAnswer} />`; the show-key checkbox only when DragDrop; essay hint unchanged; try button/error/result shared by all types (DragDrop included). |
| `web/src/features/questions/components/DragDropPreview.tsx` | Remove `showKey` prop and the bank branch: always `DiagramSvg tone="key"` + `DiagramKeyLegend`. |
| `web/src/features/questions/components/ValidationQuestionContent.tsx` | `<DragDropPreview stem=… diagram=… />` (no `showKey`). |
| `web/src/features/questions/schemas/dragDropContentSchemas.ts` | `export const dragDropSpecSchema = diagramKeySchema;` imported from `./studentDiagramSchema`. |
| `web/src/features/questions/api/questionOptions.ts` | `servedQuestionTypes` adds `'DragDrop'` last. |
| `web/src/features/questions/index.ts` | Export `LazyDragDropCorrectAnswer`; `studentDiagramBodySchema`, `diagramKeySchema`, types `StudentDiagram`, `DiagramKey`; `fromPlacementsPayload`, type `DiagramPlacements`. |
| `web/src/features/questions/i18n/diagram.ar.json`, `diagram.en.json` | Remove `preview.dragDropGradingHint` and `view.diagramBank`. |
| `web/src/features/questions/i18n/diagramErrors.ar.json`, `diagramErrors.en.json` | Remove `QUESTION_TYPE_NOT_GRADABLE`. |
| `web/src/features/quiz/api/quizItem.ts` | `quizQuestionTypes` adds `'DragDrop'`; `toQuizQuestion` adds `diagram: type === 'DragDrop' ? studentDiagramBodySchema.parse(item.body) : null`; `answerPayloadSchema` adds `placements: z.array(z.object({ zoneId: z.string(), itemIds: z.array(z.string()) })).optional()`; `fromAnswerPayload` DragDrop → `{ ...answer, placements: fromPlacementsPayload(placements ?? []) }`; `isAnswerEmpty` DragDrop → `Object.values(answer.placements).every((ids) => ids.length === 0)`; `questionImageSources` appends `studentDiagramBodySchema.safeParse(item.body).data?.image.url` when `item.type === 'DragDrop'`. |
| `web/src/features/quiz/api/correctAnswer.ts` | Union adds `{ kind: 'diagram'; diagram: StudentDiagram; diagramKey: DiagramKey }`; DragDrop → `diagramKeySchema.safeParse(correctAnswer)` success and `question.diagram` → that view, else `null`; `choiceReview` DragDrop → `{ correctKeys: [], diagramKey: spec.data.zones }` or `undefined`. |
| `web/src/features/quiz/components/CorrectAnswer.tsx` | `case 'diagram': return <LazyDragDropCorrectAnswer diagram={view.diagram} diagramKey={view.diagramKey} />;` |
| `web/src/features/blueprints/api/blueprintValues.ts`, `schemas/examBlueprintSchema.ts` | Add `DragDrop` to `emptyBlueprintValues.counts` (`'0'`), `toFormValues` (`count('DragDrop')`) and the `counts` zod object (`DragDrop: z.string()`). |
| Docs (see Docs table below) | Divergence updates. |

### Docs
| File | Change |
|------|--------|
| `docs/PRD.md` | §5.3 servable row → `Approved AND lesson.state == Published AND question.not_retired`. §6 table DragDrop row: Grading = "Deterministic per item: each keyed item's zone (and position in an ordered zone) vs the key; each distractor placed cancels one right item", Partial credit = "Per item". Add paragraph after the MathSteps paragraph: "Science drag-and-drop: the answer lists, per zone, the items placed there in order. Each item the key places scores when it sits in its key zone (at its key position when the zone is ordered); each placed item the key places nowhere cancels one right item. Score = max(0, (right − distractors placed) ÷ keyed items); an answer that places nothing is unanswered. Exact rules in docs/question-schemas.md." §17 rule 1: drop "; drag-and-drop questions are not servable until the student canvas ships". |
| `docs/question-schemas.md` | Line 16 DragDrop bullet: "…draggable items (#125), answered on the student canvas and graded per item (#126)." Zones section: add the drop-mapping bullet (decision 16). Servable section: remove the DragDrop exclusion; `ServedTypes` "lists every type". Answer shapes: replace "defined with the student canvas (#126)" with the shape, `CanRead` rules, canonicalisation (drops placements with no items, keeps order) and caps (`Sessions:DragDropAnswerMaxLength` 4000 raw, `Sessions:DragDropPlacementsMaxCount` 20, `Sessions:DragDropPlacedItemsMaxCount` 30 → 422 `ATTEMPT_ANSWER_TOO_LONG` on quiz answer, exam save and `grade-draft`). Grading: replace the #126 line with decisions 2–5 (incl. worked example: key z1 {i1,i2} unordered, z2 [i4,i3] ordered, answer z1 [i2,i1], z2 [i3,i4] → right 2, wrong 0, 2/4). Feedback: add PlacementTally line (text below) and the unanswered condition "DragDrop places no item in a known zone". |
| `docs/sessions.md` | Line 50 `GradedBy`: "Every deterministic type (the v1 types and drag-and-drop) and a blank essay are `Auto`". Line 103: add DragDrop raw cap and the two placement caps. Config table: three new rows. `ATTEMPT_ANSWER_TOO_LONG` row: add DragDrop. Quiz UI list: new bullet for the drag-and-drop card (canvas, drag/tap/keyboard, announcements, marks, correct placements). |
| `docs/exams.md` | Lines 44 and 178: add `Sessions:DragDropAnswerMaxLength` for drag-and-drop. |
| `docs/mastery.md` line 51, `docs/exam-blueprints.md` lines 41 and 74 | Remove "not drag-and-drop (until #126)"; "the seven served types" → "all eight question types". |
| `docs/performance.md` | "Lazy quiz extras": add `DragDropAnswerInput` (drag-and-drop canvas, with the `diagramStudent` strings) and `DragDropCorrectAnswer` (correct placements). Admin-strings bullet: drop `QUESTION_TYPE_NOT_GRADABLE`, note the student namespace. Update the quiz "Measured" cell with the value `npm run perf:budget` prints. |
| `docs/design-system.md` §5.13 **and** `.claude/design-system.md` DiagramCanvas row | Add the student-answer states (design section below). |
| `docs/claude-design-prompt.md` §4 | Line 135 (quiz) and 136 (exam): add the drag-and-drop answer (drag or choose item then zone, zone list with order buttons and «أعد إلى البنك», marks after check and «الأماكن الصحيحة»). Line 153: replace "and a note that «جرّب الإجابة» is not available yet" with "and its preview is the student canvas with «جرّب الإجابة» running the per-item grader". |
| `docs/prototype.md` line 72 | "…they are served since #126: students place items on a canvas and are graded per item." |

## Files to create

### API
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `api/Elmanhg.Domain/Questions/Grading/DragDropGrader.cs` | `public static class DragDropGrader` (ns `Elmanhg.Domain.Questions.Grading`) — new, no Morabh equivalent | `public static NormalisedGrade Grade(DragDropGradingSpec spec, DragDropAnswer answer)`; private `sealed record KeyPlace(string ZoneId, int Index, bool Ordered)`, `sealed record ZonePlacement(string ZoneId, List<string> ItemIds)`, `static Dictionary<string, KeyPlace> KeyPlaces(DragDropGradingSpec spec)`, `static List<ZonePlacement> Resolve(DragDropAnswer answer, HashSet<string> zoneIds)`. Bodies in Domain behaviour. |
| A2 | `api/Elmanhg.Application/Questions/Shared/DragDropAnswerRules.cs` | `public static class DragDropAnswerRules` (ns `Elmanhg.Application.Questions.Shared`) — new | `public static bool CanRead(JsonElement answer)` = `QuestionSchemaReader.TryRead<DragDropAnswer>(answer, out var drag) && (drag.Placements is null \|\| drag.Placements.All(x => x is not null && x.ZoneId is not null && (x.ItemIds is null \|\| x.ItemIds.All(id => id is not null))))`. `public static string Canonicalize(JsonElement answer)`: read; `placements = (Placements ?? []).Select(x => new DiagramPlacement(x!.ZoneId, (x.ItemIds ?? []).ToList())).Where(x => x.ItemIds!.Count > 0).ToList<DiagramPlacement?>()`; `QuestionSchemaReader.Serialize(new DragDropAnswer(placements))`. `public static bool ExceedsLimits(JsonElement answer, SessionsOptions options)`: read; `placements.Count > options.DragDropPlacementsMaxCount \|\| placements.Sum(x => x?.ItemIds?.Count ?? 0) > options.DragDropPlacedItemsMaxCount`. |

### Web (all under `web/src/features/`, new — no Morabh equivalent)
| # | Path | Type | Contract |
|---|------|------|----------|
| W1 | `questions/schemas/studentDiagramSchema.ts` | zod | `studentDiagramBodySchema = z.object({ image: z.object({ url: z.string().min(1), width: z.number().positive(), height: z.number().positive(), alt: z.string() }), zones: z.array(z.object({ id: z.string(), x: z.number(), y: z.number(), width: z.number(), height: z.number(), capacity: z.number().int().min(1) })), items: z.array(z.object({ id: z.string(), text: z.string() })) })`; `diagramKeySchema = z.object({ zones: z.array(z.object({ zoneId: z.string(), itemIds: z.array(z.string()), ordered: z.boolean() })) })`; `export type StudentDiagram = z.infer<typeof studentDiagramBodySchema>`; `export type DiagramKey = z.infer<typeof diagramKeySchema>['zones']`. |
| W2 | `questions/api/studentDiagram.ts` | pure | `studentDiagramFromValues(values: DeepPartialSkipArrayKey<QuestionValues>): StudentDiagram \| null` — `null` when `diagramImage.url` is empty or width/height ≤ 0; zones: numbers via `Number.isFinite(Number(v)) ? Number(v) : 0`, `capacity` default 1; items `{ id: id ?? '', text: text ?? '' }`. Must NOT import `dragDropValues.ts` or `dragDropContentSchemas.ts`. |
| W3 | `questions/api/diagramPlacement.ts` | pure | `export type DiagramPlacements = Record<string, readonly string[]>`; `placedZoneOf(placements, itemId): string \| null`; `unplacedItems(diagram: StudentDiagram, placements): StudentDiagram['items']` (body order); `isZoneFull(diagram, placements, zoneId): boolean`; `placeItem(diagram, placements, itemId, zoneId): DiagramPlacements \| null` (unknown zone → null; already in that zone → same object; `current.length >= capacity` → null; else remove everywhere then append); `returnItem(placements, itemId): DiagramPlacements`; `moveItem(placements, zoneId, index, delta: -1 \| 1): DiagramPlacements` (swap, out of range → unchanged); `toCanvasPercent(clientX, clientY, bounds: CanvasBounds): { x: number; y: number } \| null` (unrounded; null when bounds width/height ≤ 0 or point outside `[0,100]`; `import type { CanvasBounds } from './diagramGeometry'`); `zoneAtPoint(zones: StudentDiagram['zones'], point): string \| null` (decision 16; `h = (v) => Math.round(v * 100)`; `inX = px >= x && (px < x + w \|\| (x + w === 10000 && px <= 10000))`, same for y, `px = point.x * 100` unrounded); `toPlacementsPayload(diagram, placements): { zoneId: string; itemIds: string[] }[]` (body zone order, only item ids present in `diagram.items`, empty zones dropped); `fromPlacementsPayload(payload: readonly { zoneId: string; itemIds: readonly string[] }[]): DiagramPlacements` (first entry per zone wins). |
| W4 | `questions/api/diagramReview.ts` | pure | `export type DiagramItemMark = 'correct' \| 'wrong' \| 'missed'`; `markPlacements(diagramKey: DiagramKey, placements: DiagramPlacements): Record<string, DiagramItemMark>` — decision 18, same resolution as the grader (first occurrence of an item wins; index = position in its zone). |
| W5 | `questions/hooks/useDiagramAnswer.ts` | hook | `useDiagramAnswer(diagram: StudentDiagram, placements: DiagramPlacements, onChange: (next: DiagramPlacements) => void): DiagramAnswerControls` with `{ selectedItemId: string \| null; announcement: string; focusItemId: string \| null; select(itemId): void /* toggles; announce picked / cleared */; clear(): void /* announce cleared if something was selected */; place(itemId, zoneId): void /* placeItem; null → announce full; else onChange, clear selection, focusItemId = itemId, announce placed with 1-based position */; placeSelected(zoneId): void /* none selected → announce selectFirst */; returnToBank(itemId): void /* announce returned, focus */; move(zoneId, index, delta): void /* announce moved, focus */ }`. Texts via `t('diagramStudent:announce.*')`; item names are the item texts; zone numbers are 1-based body order. |
| W6 | `questions/hooks/useDiagramDrag.ts` | hook | `useDiagramDrag({ canvasRef: RefObject<HTMLDivElement \| null>; zones: StudentDiagram['zones']; onDropZone(itemId, zoneId): void; onDropBank(itemId): void }): { drag: { itemId: string; x: number; y: number; overZoneId: string \| null } \| null; chipHandlers(itemId: string): { onPointerDown; onPointerMove; onPointerUp; onPointerCancel; onClickCapture } }`. pointerdown (primary button or touch/pen) records start + pointer id and captures the pointer when the element supports it (guard for jsdom without disabling lint rules); pointermove starts a drag after ≥ 6 px (`Math.hypot`), then updates `x, y, overZoneId`; pointerup while dragging resolves the target (1: canvas rect contains the point → `zoneAtPoint(zones, toCanvasPercent(...))`, a miss drops nothing; 2: `document.elementFromPoint` (only when it is a function) → `closest('[data-zone-id]')`; 3: `closest('[data-diagram-bank]')` → bank; else nothing), calls the callback and sets a flag so `onClickCapture` swallows the click that follows; pointercancel or window `keydown` Escape while dragging cancels. |
| W7 | `questions/components/DiagramItemChip.tsx` | component | Props `{ item: { id; text }; selected: boolean; interactive: boolean; mark?: DiagramItemMark \| undefined; handlers?: ReturnType<…chipHandlers> \| undefined; onSelect?: (id) => void; chipRef?: (el: HTMLButtonElement \| null) => void }`. Interactive → `<button type="button" aria-pressed={selected} data-item-id className="min-h-11 touch-none select-none rounded-full border px-3.5 text-ui …">` text `<bdi>`; selected `border-text bg-soft`, default `border-border-strong bg-surface`. Not interactive → `<span>` with mark styles: correct `border-success bg-success-soft` + `Check` icon, wrong/missed `border-danger bg-danger-soft` + `X` icon (icons `aria-hidden`), plus `<span className="sr-only"> {t('mark.<mark>')}</span>`. Without a mark the text content is exactly the item text. |
| W8 | `questions/components/DiagramAnswerCanvas.tsx` | component | Props `{ diagram; placements; overZoneId; selectedItemId; interactive; onZone(zoneId): void; canvasRef }`. `<div ref role="group" aria-label={t('canvasLabel')} dir="ltr" className="relative w-full overflow-hidden rounded-md border border-border bg-surface" style={{ aspectRatio: \`${w} / ${h}\` }}>` + `<img src={image.url} alt={image.alt} width height draggable={false} decoding="async" className="absolute inset-0 size-full" />` + per zone `<button type="button" data-zone-id aria-label={t('zoneButton', { number, count, capacity })} disabled={!interactive} onClick={() => onZone(id)} style={{ insetInlineStart: \`${x}%\`, top: \`${y}%\`, width: \`${w}%\`, height: \`${h}%\` }}>` with a number badge (white circle) and, when count > 0, a `{count}/{capacity}` pill. Classes: empty `border-2 border-dashed border-text-muted bg-surface/60`; with items `border-solid border-text`; `overZoneId === id` or (selected item and focus-visible) `border-accent bg-accent/15`. |
| W9 | `questions/components/DiagramZoneRow.tsx` | component | Props `{ zone; number; items (resolved objects in order); selectedItem: { id; text } \| null; full: boolean; interactive; over: boolean; marks?: Record<string, DiagramItemMark>; chipProps(itemId) ; onPlace(zoneId); onReturn(itemId); onMove(zoneId, index, delta) }`. `<li data-zone-id className="flex flex-col gap-2 rounded-md border border-border bg-surface p-3">`: header `t('zone', { number })` + `t('zoneFill', { count, capacity })`; when interactive and `selectedItem`: `full` → `<span>{t('zoneFull')}</span>`, else `<Button variant="secondary" size="sm">` text `t('placeHere')` + `<span className="sr-only"> {t('placeHereTarget', { item, number })}</span>`; chips list `<ul>`; per placed chip when interactive: icon buttons (≥ 44 px, `aria-label`) move earlier (`t('moveUp', { item })`, hidden at index 0), move later (`t('moveDown', …)`, hidden at last), `t('returnToBank', { item })`; up/down only when the zone holds ≥ 2 items; empty zone → `<p className="text-caption text-text-muted">{t('zoneEmpty')}</p>`. |
| W10 | `questions/components/DiagramZoneList.tsx` | component | `<section aria-labelledby><h3 className="text-h3 font-semibold">{t('zonesTitle')}</h3><ol className="flex flex-col gap-2">` of `DiagramZoneRow` in body order. |
| W11 | `questions/components/DiagramItemBank.tsx` | component | `<section aria-label={t('bank')} data-diagram-bank className="flex flex-col gap-2"><h3>{t('bank')}</h3><ul className="flex flex-wrap gap-2">` `<li>` per unplaced item (chip); empty → `<p>{t('bankEmpty')}</p>`. In review mode keyed items left here show `missed`. |
| W12 | `questions/components/DiagramDragGhost.tsx` | component | `<div aria-hidden dir="ltr" className="pointer-events-none fixed inset-0 z-50"><span className="absolute -translate-x-1/2 -translate-y-1/2 rounded-full border border-border-strong bg-surface px-3.5 py-2 text-ui shadow-2" style={{ insetInlineStart: x, top: y }}>{text}</span></div>`. |
| W13 | `questions/components/DragDropAnswerInput.tsx` | lazy component (named export) | Calls `registerDiagramStudentLocales()` at module top. Props `{ question: StudentQuestion; answer: QuestionAnswer; onAnswerChange; disabled?: boolean \| undefined; review?: ChoiceReview \| undefined }`. `question.diagram` missing → `<p className="text-caption text-text-muted">{t('missing')}</p>`. Else `interactive = !disabled`; `marks = disabled && review?.diagramKey ? markPlacements(review.diagramKey, answer.placements) : undefined`; `useDiagramAnswer(diagram, answer.placements, (placements) => onAnswerChange({ ...answer, placements }))`; `useDiagramDrag({ canvasRef, zones, onDropZone: controls.place, onDropBank: controls.returnToBank })`; chip refs `Map<string, HTMLButtonElement>` + effect focusing `focusItemId`; root `<div className="flex flex-col gap-4" onKeyDown={Escape → controls.clear()}>`: hint `<p className="text-caption text-text-muted">{t('hint')}</p>` (interactive only), canvas, zone list, bank, `<p role="status" className="sr-only">{announcement}</p>`, ghost while dragging. ≤ 120 lines (move helpers into hooks if needed). |
| W14 | `questions/components/DragDropCorrectAnswer.tsx` | lazy component (named export) | Calls `registerDiagramStudentLocales()`. Props `export interface DragDropCorrectAnswerProps { diagram: StudentDiagram; diagramKey: DiagramKey }`. Renders `DiagramSvg image={diagram.image} zones={diagram.zones} tone="key" label={t('key.canvasLabel', { alt })}` + `<ol>` per body zone: `key.zone` / `key.zoneOrdered` (items joined by `key.orderSeparator`) / `key.zoneEmpty`; items joined by `key.listSeparator`; then `key.distractors` line when any body item is in no key zone. |
| W15 | `questions/components/LazyDragDropCorrectAnswer.tsx` | component | `lazy(async () => ({ default: (await import('./DragDropCorrectAnswer')).DragDropCorrectAnswer }))`; `export function LazyDragDropCorrectAnswer(props: DragDropCorrectAnswerProps)` in `<Suspense fallback={<div aria-busy="true" className="min-h-11" />}>`; props type via `import type`. |
| W16 | `questions/diagramStudentLocales.ts` | i18n | `export const diagramStudentNamespace = 'diagramStudent'`; `registerDiagramStudentLocales(): void` → `getI18n().addResourceBundle(lng, diagramStudentNamespace, json, true, true)` for `ar` and `en`. |
| W17 | `questions/i18n/diagramStudent.en.json` | i18n | Strings below. |
| W18 | `questions/i18n/diagramStudent.ar.json` | i18n | Strings below (same keys). |

**`diagramStudent` strings (en / ar):**
| Key | en | ar |
|---|---|---|
| `canvasLabel` | Diagram | الرسم |
| `hint` | Drag each item to its zone on the diagram, or choose an item and then its zone. Leave items that belong nowhere in the bank. | اسحب كل عنصر إلى منطقته في الرسم، أو اختر العنصر ثم اختر المنطقة. اترك العناصر التي لا مكان لها في البنك. |
| `missing` | The diagram image has not been uploaded yet. | لم تُرفع صورة الرسم بعد. |
| `zoneButton` | Zone {number}: {count} of {capacity} items | المنطقة {number}: {count} من {capacity} عناصر |
| `zonesTitle` | Zones | المناطق |
| `zone` | Zone {number} | المنطقة {number} |
| `zoneFill` | {count}/{capacity} | {count}/{capacity} |
| `zoneEmpty` | Empty | فارغة |
| `zoneFull` | Full | ممتلئة |
| `placeHere` | Place here | ضعه هنا |
| `placeHereTarget` | ({item} in zone {number}) | («{item}» في المنطقة {number}) |
| `moveUp` | Move {item} earlier | قدّم «{item}» |
| `moveDown` | Move {item} later | أخّر «{item}» |
| `returnToBank` | Return {item} to the bank | أعد «{item}» إلى البنك |
| `bank` | Items to place | عناصر للوضع |
| `bankEmpty` | All items are placed. | وُضعت كل العناصر. |
| `announce.picked` | {item} selected. Choose a zone. | اخترت «{item}». اختر منطقة. |
| `announce.cleared` | Selection cleared. | أُلغي الاختيار. |
| `announce.placed` | {item} placed in zone {number}, position {position}. | وُضع «{item}» في المنطقة {number}، في الموضع {position}. |
| `announce.full` | Zone {number} is full. | المنطقة {number} ممتلئة. |
| `announce.selectFirst` | Choose an item first. | اختر عنصرًا أولًا. |
| `announce.returned` | {item} returned to the bank. | أُعيد «{item}» إلى البنك. |
| `announce.moved` | {item} moved to position {position} in zone {number}. | نُقل «{item}» إلى الموضع {position} في المنطقة {number}. |
| `mark.correct` | in the right place | في مكانه الصحيح |
| `mark.wrong` | in the wrong place | في مكان خاطئ |
| `mark.missed` | not placed | لم يوضع |
| `key.canvasLabel` | Correct placements: {alt} | الأماكن الصحيحة: {alt} |
| `key.title` | Correct placements | الأماكن الصحيحة |
| `key.zone` | Zone {number}: {items} | المنطقة {number}: {items} |
| `key.zoneOrdered` | Zone {number}, in order: {items} | المنطقة {number} بالترتيب: {items} |
| `key.zoneEmpty` | Zone {number}: stays empty | المنطقة {number}: تبقى فارغة |
| `key.distractors` | Stay in the bank: {items} | تبقى في البنك: {items} |
| `key.listSeparator` | `, ` | `، ` |
| `key.orderSeparator` | ` → ` | ` ← ` |

## Error codes
No new codes.
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `QuestionAnswerInvalid` (reused) | `QUESTION_ANSWER_INVALID` | `SubmitAnswerHandler`, `SaveExamAnswerHandler`, `GradeQuestionDraftValidator` via `DragDropAnswerRules.CanRead` | `ApplicationValidationCoreException` / validation failure | 422 |
| `AttemptAnswerTooLong` (reused) | `ATTEMPT_ANSWER_TOO_LONG` | same handlers + `GradeQuestionDraftHandler` via raw cap / `DragDropAnswerRules.ExceedsLimits` | `ApplicationValidationCoreException` | 422 |
| `QuestionTypeNotGradable` | `QUESTION_TYPE_NOT_GRADABLE` | **removed** | — | — |

Resource (feedback, not an error): `GRADE_FEEDBACK_PLACEMENT_TALLY` — ar «العناصر في أماكنها الصحيحة: {right} من {total}، والعناصر المشتِّتة الموضوعة: {wrong}.» / en "Items in the right place: {right} of {total}; distractors placed: {wrong}."

## Domain behaviour
No entity changes; `UpdationDate` not involved. `DragDropGrader.Grade`:
```
var zoneIds = (spec.Zones ?? []).Where(x => x?.ZoneId is not null).Select(x => x!.ZoneId!).ToHashSet(StringComparer.Ordinal);
var keyPlaces = KeyPlaces(spec);            // itemId → (zoneId, index in key itemIds, ordered ?? false); first occurrence wins (TryAdd); null ids skipped
var placements = Resolve(answer, zoneIds);  // loop Placements ?? []: skip null / ZoneId null / unknown zone / zone already seen;
                                            // items: skip null and ids already seen in an earlier placement (Ordinal)
if (placements.All(x => x.ItemIds.Count == 0)) return NormalisedGrade.Unanswered;
if (keyPlaces.Count == 0) return new NormalisedGrade(0m, null);
right = wrong = 0;
foreach placement, foreach (itemId, index):
    if (!keyPlaces.TryGetValue(itemId, out var place)) wrong++;
    else if (place.ZoneId == placement.ZoneId && (!place.Ordered || place.Index == index)) right++;
if (right == keyPlaces.Count && wrong == 0) return new NormalisedGrade(1m, null);
return new NormalisedGrade(Math.Max(0m, (decimal)(right - wrong) / keyPlaces.Count), GradeFeedback.PlacementTally(right, wrong, keyPlaces.Count));
```
Explicit loops (no side-effecting LINQ). `QuestionGrade.FromNormalised` scales/rounds as for every type. `ServableQuestionSpecification` loses the type clause; `Session.StartQuiz`/exam starts therefore accept DragDrop.

## API surface
No new endpoints, policies or records. Behaviour changes:
| Method · route | Policy | Change |
|---|---|---|
| `POST /api/sessions/quiz`, `GET /api/sessions/{id}`, `POST /api/sessions/{id}/finish`, `POST /api/sessions/{id}/answers` | unchanged | DragDrop items served; `body.image.url` resolved; DragDrop answers read/capped/graded; `correctAnswer` = key after answering. |
| `POST /api/exams/units/{id}`, `POST /api/exams/subjects/{id}/multi-unit`, `GET /api/exams/{id}`, `PUT /api/exams/{id}/answers/{questionId}`, `POST /api/exams/{id}/submit` | unchanged | Same for exams (answer canonicalised on save, graded on submit). |
| `POST /api/questions/grade-draft` | `ContentManage` | DragDrop graded (200) instead of 422. |
| `GET /api/questions/servable-count`, `GET /api/lessons`, blueprint reads | unchanged | Count approved DragDrop in published lessons; blueprint overview lists 8 types. |

## Test plan
Backend (xUnit v3, FluentAssertions, NSubstitute; fixtures: `QuestionBuilder.DragDropSpecJson` = z1 {i1,i2} unordered, z2 [i4,i3] ordered, i5 distractor; K = 4).

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| D1 | `Domain/Questions/Grading/DragDropGraderTests` (new) | `Grade_EveryKeyedItemInPlace_ReturnsOneWithoutFeedback` | z1 [i1,i2], z2 [i4,i3] → 1, feedback null |
| D2 | same | `Grade_NoPlacements_ReturnsUnanswered` | `Placements` null → value 0, `GradeFeedback.Unanswered` |
| D3 | same | `Grade_OnlyEmptyOrUnknownZones_ReturnsUnanswered` | z1 [] + zz [i1] → Unanswered |
| D4 | same | `Grade_HalfOfKeyedItemsRight_ReturnsHalfWithPlacementTally` | z1 [i1,i2] only → 0.5, tally (2,0,4) |
| D5 | same | `Grade_DistractorPlaced_CancelsOneRightItem` | all right + i5 in z1 → 0.75, tally (4,1,4) |
| D6 | same | `Grade_MoreWrongThanRight_FloorsAtZero` | z1 [i5, x9], z2 [i4] → right 1 (i4 index 0 = key index 0), wrong 2 → 0, tally (1,2,4) |
| D7 | same | `Grade_OrderedZoneSwapped_CountsNoItemOfThatZone` | z1 [i2,i1], z2 [i3,i4] → 0.5 (unordered z1 any order, z2 none) |
| D8 | same | `Grade_UnknownItemId_CountsAsWrong` | all right + "zz" in z2 end → (4−1)/4 |
| D9 | same | `Grade_ItemPlacedTwice_CountsFirstPlacementOnly` | z1 [i4], z2 [i4,i3] → i4 first in z1 (not right), z2 resolves [i3] → i3 index 0 ≠ 1 → right 0 → value 0, tally (0,0,4) |
| D10 | same | `Grade_RepeatedZoneEntry_UsesFirstEntry` | z1 [i1], z1 [i2] → right 1 → 0.25 |
| D11 | same | `Grade_KeyedItemInWrongZone_IsNeitherRightNorWrong` | z2 [i1] → 0, tally (0,0,4) |
| D12 | `Domain/Questions/Grading/QuestionGraderTests` (add) | `Grade_DragDrop_ScalesPerItemCreditByMaxScore` | maxScore 4, z1 [i1,i2], z2 [i4] → score 3, normalised 0.75, Partial, feedback kind PlacementTally |
| D13 | `Domain/Questions/ServableQuestionSpecificationTests` (**modify**) | rename `IsSatisfiedBy_ApprovedDragDropInPublishedLesson_ReturnsFalse` → `..._ReturnsTrue` | `.BeTrue()` |
| D14 | same (**modify**) | rename `ServedTypes_EveryTypeExceptDragDrop` → `ServedTypes_EveryQuestionType` | `Equal(Enum.GetValues<QuestionType>())` |
| P1 | `Application/Features/Questions/Shared/DragDropAnswerRulesTests` (new) | `CanRead_Placements_ReturnsTrue` | valid shape |
| P2 | same | `CanRead_MissingPlacements_ReturnsTrue` | `{}` |
| P3 | same | `CanRead_TypeViolation_ReturnsFalse` [Theory] | rows: `{"placements":[null]}`, `{"placements":[{"itemIds":["i1"]}]}`, `{"placements":[{"zoneId":1,"itemIds":[]}]}`, `{"placements":[{"zoneId":"z1","itemIds":[null]}]}`, `{"placements":"x"}` |
| P4 | same | `Canonicalize_DropsEmptyPlacementsAndUnknownProperties` | `{"placements":[{"zoneId":"z1","itemIds":[],"x":1},{"zoneId":"z2","itemIds":["i4","i3"]}],"y":2}` → `{"placements":[{"zoneId":"z2","itemIds":["i4","i3"]}]}` (JsonNode.DeepEquals) |
| P5 | same | `ExceedsLimits_TooManyPlacements_ReturnsTrue` | 21 placements, default options |
| P6 | same | `ExceedsLimits_TooManyPlacedItems_ReturnsTrue` | 31 ids over 2 placements |
| P7 | same | `ExceedsLimits_AtCaps_ReturnsFalse` | 20 placements holding 30 ids |
| P8 | `Application/Features/Questions/Shared/QuestionAnswerRulesTests` (add) | `IsRawAnswerTooLong_DragDropUsesDragDropCap` | `DragDropAnswerMaxLength = 50`, `AnswerMaxLength = 4000`: 60-char DragDrop answer → true; same JSON as Mcq → false |
| P9 | same (add) | `ExceedsLimits_DragDrop_AppliesPlacementCaps` | `DragDropPlacementsMaxCount = 1`, two placements → true; same answer typed Mcq → false |
| P10 | `Application/Features/Questions/Shared/GradeFeedbackTextTests` (add) | `Localize_PlacementTally_PassesRightWrongAndTotal` | localizer called with `GRADE_FEEDBACK_PLACEMENT_TALLY` and right/wrong/total (same style as the ChoiceTally test) |
| P11 | `Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftValidatorTests` (**modify**) | `Validate_DragDrop_ReturnsQuestionTypeNotGradableOnly` → `Validate_DragDropWithPlacements_ReturnsNoAnswerErrors` | `DragDropFields()` + `{"placements":[{"zoneId":"z1","itemIds":["i1"]}]}` → codes contain neither `QuestionAnswerInvalid` nor anything |
| P12 | same (add) | `Validate_DragDropUnreadableAnswer_ReturnsQuestionAnswerInvalid` | `{"placements":[{"zoneId":"z1","itemIds":[null]}]}` |
| P13 | `Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftHandlerTests` (add) | `Handle_DragDrop_ReturnsPerItemGradeWithTally` | answer z1 [i1,i2], z2 [i4] → score 3 / max 4, outcome "Partial", feedback = localizer return for placement tally |
| P14 | same (add) | `Handle_DragDropOverPlacementCap_ThrowsAttemptAnswerTooLong` | handler with `SessionsOptions { DragDropPlacementsMaxCount = 1 }`, 2 placements → `ApplicationValidationCoreException` code `AttemptAnswerTooLong` |
| P15 | `Application/Features/Sessions/Shared/SessionResultGeneratorTests` (new) | `GenerateItem_DragDropItem_AddsResolvedImageUrl` | `SessionBuilder.BuildWithDragDrop()`, `_fileStorage.GetPublicUrl(key)` → `/api/media/{key}`; item 2 `body.image.url` equals it, `body.image.key` unchanged |
| P16 | same | `GenerateItem_McqItem_ReturnsStoredBody` | item 1 body has `options` (2) and no `image` property |
| P17 | `Application/Features/Sessions/SubmitAnswer/SubmitAnswerHandlerTests` (add) | `Handle_DragDropAnswer_RecordsPartialAttempt` | session from `BuildWithDragDrop`, answer z1 [i1,i2] → result attempt outcome Partial, score 2 (maxScore 4 → 0.5×4), `SaveChangesAsync` Received(1) |
| P18 | same (add) | `Handle_DragDropNullItemId_ThrowsQuestionAnswerInvalid` | code `QuestionAnswerInvalid`, `SaveChangesAsync` DidNotReceive |
| P19 | `Application/Features/ExamBlueprints/GetSubjectExamBlueprints/GetSubjectExamBlueprintsHandlerTests` (**modify**) | existing test with `HaveCount(7)` / `Count.Should().Be(7)` | both become 8 |
| M1 | **modify, constructor only**: `GetExamSessionHandlerTests`, `StartMultiUnitExamFreeTierTests`, `StartMultiUnitExamHandlerTests`, `StartUnitExamFreeTierTests`, `StartUnitExamHandlerTests`, `SubmitExamHandlerTests`, `FinishSessionHandlerTests`, `GetSessionHandlerTests`, `StartQuizSessionFreeTierTests`, `StartQuizSessionHandlerTests`, `SubmitAnswerFreeTierTests`, `SubmitAnswerHandlerTests` | — | append `IFileStorage` substitute; no assertion changes |
| I1 | `Integration/Sessions/DragDropAnswerEndpointTests` (new) | `Start_LessonWithDragDrop_ServesDiagramWithResolvedImageUrl` | seed 4 MCQ + DragDrop; start 5 → DragDrop item `body.image.url` = `/api/media/{key}`; `correctAnswer` null |
| I2 | same | `Post_EveryItemInPlace_RecordsCorrectAttemptAndRevealsKey` | 200; attempt Correct, score 4; response `correctAnswer.zones` length 2; DB attempt answer = canonical placements |
| I3 | same | `Post_OrderedZoneSwapped_RecordsPartialWithTally` | z1 [i2,i1], z2 [i3,i4] → Partial, score 2, `feedback` = Arabic tally «العناصر في أماكنها الصحيحة: 2 من 4، والعناصر المشتِّتة الموضوعة: 0.» (default language) |
| I4 | same | `Post_TooManyPlacements_Returns422AttemptAnswerTooLong` | 21 placements; no attempt stored |
| I5 | same | `Post_NullItemId_Returns422QuestionAnswerInvalid` | no attempt stored |
| I6 | `Integration/Exams/DragDropExamEndpointTests` (new) | `Start_UnitExamWithDragDrop_ServesResolvedImageUrl` | blueprint Mcq 1 + DragDrop 1; item image url resolved |
| I7 | same | `Put_DragDropAnswer_StoresCanonicalAnswer` | saved answer drops the empty placement |
| I8 | same | `Submit_SavedDragDropAnswer_GradesPerItem` | attempt Partial, score 3 for z1 [i1,i2], z2 [i4] |
| I9 | `Integration/Content/DragDropQuestionEndpointTests` (**modify**) | `PostGradeDraft_DragDrop_Returns422QuestionTypeNotGradable` → `PostGradeDraft_DragDrop_ReturnsPerItemGrade` | answer z1 [i1,i2], z2 [i4] → 200, `score` 3, `outcome` "Partial", `feedback` not null |
| I10 | `Integration/QuestionValidation/DragDropValidationEndpointTests` (**modify**) | `Approve_DragDropInPublishedLesson_ApprovedButNotServable` → `Approve_DragDropInPublishedLesson_BecomesServable` | `servableQuestionCount` 1, `isServable` true |

Web (Vitest + Testing Library + MSW; `renderWithProviders` / `renderApp`; `userEvent.setup()`). Diagram fixture everywhere: image `{ url: '/api/media/question-diagrams/l/abc.png', width: 800, height: 600, alt: 'Plant cell' }`, zones z1 {10,10,20,15,cap 2}, z2 {50,40,30,20.5,cap 2}, items i1 Nucleus, i2 Vacuole, i3 Wall, i4 Membrane, i5 Engine; key z1 [i1,i2] unordered, z2 [i4,i3] ordered.

| # | File | `it(...)` | Asserts |
|---|------|-----------|---------|
| T1 | `questions/api/diagramPlacement.test.ts` (new) | maps a point to the zone that contains it | (20, 15) → z1 |
| T2 | same | gives a point on a shared edge to the zone that starts there | zones [0,50) and [50,100): x = 50 → second |
| T3 | same | closes a zone on the right and bottom border of the image | zone x 80 w 20: x = 100 → that zone |
| T4 | same | compares in hundredths so float sums never cross a bound | zone x 10 w 20.1: x 30.099 → zone, 30.1 → null |
| T5 | same | returns null outside every zone | (90, 90) → null |
| T6 | same | converts a client point to unrounded percent and null outside the canvas | bounds 0,0,800,600: (200,100) → {25, 16.666…}; (801, 10) → null |
| T7 | same | places an item at the end of a zone and removes it from its previous zone | |
| T8 | same | refuses to place an item in a full zone | returns null |
| T9 | same | returns an item to the bank and lists unplaced items in bank order | |
| T10 | same | moves an item within a zone | |
| T11 | same | builds the payload in zone order without empty zones or unknown items | |
| T12 | same | reads placements from a payload keeping the first entry of a zone | |
| T13 | `questions/api/diagramReview.test.ts` (new) | marks items in their key zone as correct | |
| T14 | same | marks items at the wrong position of an ordered zone as wrong | |
| T15 | same | marks a placed distractor as wrong | |
| T16 | same | marks a keyed item left in the bank as missed and leaves distractors unmarked | |
| T17 | `questions/schemas/studentDiagramSchema.test.ts` (new) | reads a served diagram body | |
| T18 | same | rejects a body without an image url | |
| T19 | same | reads a diagram key | |
| T20 | `questions/components/DragDropAnswerInput.test.tsx` (new, controlled harness) | places the selected item from the zone list with the keyboard | focus chip Nucleus, `{Enter}` → `aria-pressed=true`, status "Nucleus selected. Choose a zone."; `{Tab}`-reach/`focus()` "Place here (Nucleus in zone 1)", `{Enter}` → zone 1 row lists Nucleus, focus on the Nucleus chip, status "Nucleus placed in zone 1, position 1." |
| T21 | same | places the selected item by tapping a zone on the diagram | click Vacuole, click button "Zone 2: 0 of 2 items" → zone 2 row has Vacuole; zone button name becomes "Zone 2: 1 of 2 items" |
| T22 | same | drags an item onto a zone of the diagram with a pointer | spy canvas (`getByRole('group', { name: 'Diagram' })`) `getBoundingClientRect` → {0,0,800,600}; `user.pointer` down on Wall, move to (200,100), up → Wall in zone 1, chip not left `aria-pressed` |
| T23 | same | announces a full zone and keeps the item in the bank | fill zone 1 with 2, select third, place → status "Zone 1 is full."; item still in bank region |
| T24 | same | asks for an item first when a zone is chosen with nothing selected | status "Choose an item first." |
| T25 | same | returns a placed item to the bank | "Return Nucleus to the bank" → bank lists Nucleus, status returned |
| T26 | same | reorders items within a zone | "Move Vacuole earlier" → order Vacuole, Nucleus; status moved |
| T27 | same | clears the selection with Escape | `aria-pressed=false`, status cleared |
| T28 | same | shows a mark for each item when answered with a key | disabled + review `{ correctKeys: [], diagramKey }`, placements z1 [i1], z2 [i3, i5]: Nucleus "in the right place", Wall + Engine "in the wrong place", Vacuole + Membrane "not placed"; no buttons named after items |
| T29 | same | keeps the diagram left to right in Arabic | `lng: 'ar'`: `document.documentElement` dir rtl, group «الرسم» has `dir="ltr"` |
| T30 | same | shows a notice when the question has no diagram | "The diagram image has not been uploaded yet." |
| T31 | same | has no axe violations | `axe(container)` no violations (with an item selected) |
| T32 | `questions/components/DragDropCorrectAnswer.test.tsx` (new) | lists each zone's correct items, ordered zones in order | "Zone 1: Nucleus, Vacuole", "Zone 2, in order: Membrane → Wall" |
| T33 | same | lists empty zones and the items that stay in the bank | extra empty zone "Zone 3: stays empty", "Stay in the bank: Engine" |
| T34 | same | labels the key diagram with the image description | role img "Correct placements: Plant cell" |
| T35 | `questions/diagramStudentLocales.test.ts` (new) | registers the student diagram strings in both languages | after register, `t('diagramStudent:zonesTitle')` en "Zones", ar "المناطق" |
| T36 | `questions/api/studentQuestion.test.ts` (**modify**) | 'sends no answer payload for drag-and-drop' → 'sends drag-and-drop placements in zone order' | `{ placements: [{ zoneId: 'z1', itemIds: ['i1'] }] }` from `{ z2: [], z1: ['i1', 'x'] }` |
| T37 | same (add) | builds the student diagram from editor values | `toStudentQuestion` DragDrop values → `diagram` with numeric zones; no image url → `diagram` null |
| T38 | `quiz/api/quizItem.dragDrop.test.ts` (new) | reads the served diagram of a drag-and-drop item | |
| T39 | same | restores placements from an attempt answer | |
| T40 | same | treats an answer with no placed item as empty | |
| T41 | same | preloads the diagram image of a drag-and-drop item | `questionImageSources` contains the url |
| T42 | `quiz/api/correctAnswer.test.ts` (**modify**) | 'describes no correct answer or choice review for drag-and-drop' → 'describes the diagram key of a drag-and-drop question' | with diagram: `{ kind: 'diagram', diagram, diagramKey }` and `{ correctKeys: [], diagramKey }`; without diagram: describe → null |
| T43 | `quiz/pages/QuizPage.dragDrop.test.tsx` (new) | sends the placements on check | submit body `{ placements: [{ zoneId: 'z1', itemIds: ['i1'] }] }` |
| T44 | same | asks for an answer when nothing is placed | "answer required" alert, no request |
| T45 | same | shows marks, score, feedback line and the correct placements after check | "Partially correct", feedback text, "Zone 2, in order: Membrane → Wall", Nucleus "in the right place" |
| T46 | `exam/pages/ExamPage.dragDrop.test.tsx` (new) | autosaves placements | PUT body `{ answer: { placements: [...] } }` |
| T47 | same | restores saved placements on resume | `savedAnswer` → zone 1 row lists Nucleus |
| T48 | `questions/pages/NewDragDropQuestion.test.tsx` (**modify** the preview test, lines 225-243) | 'previews the diagram as the student sees it and reveals the correct placements' | `await preview.findByRole('img', { name: 'Plant cell' })`; bank region listitems ['Nucleus','Engine']; button "Try the answer" present; hint text line removed; key checkbox assertions unchanged |
| T49 | same (add) | grades a drag-and-drop answer with the test grader | place Nucleus in zone 1 (tap + Place here), "Try the answer" → grade-draft request `answer` = `{ placements: [{ zoneId: 'z1', itemIds: ['i1'] }] }`; result verdict shown |
| T50 | `blueprints/api/blueprintValues.test.ts` (**modify**) | the `toFormValues` test and 'counts only the served question types' | expected counts include `DragDrop: '0'`; type list ends with `'DragDrop'` |

## Definition of done
- [ ] `DragDropGrader` implements decisions 2–5 exactly; D1–D12 pass; `QuestionGrader.Grade` grades DragDrop.
- [ ] `ServableQuestionSpecification` has no type clause; `ServedTypes` = all 8; D13/D14/I10/P19 updated, not skipped.
- [ ] DragDrop answers: `CanRead`, `Canonicalize`, raw cap and placement caps on quiz answer, exam save and grade-draft (P1–P9, P11–P14, I4, I5, I7).
- [ ] Three `Sessions:DragDrop*` keys in `SessionsOptions` (code defaults), `appsettings.example.json`, `ApiFactory`; `RequestAnswerMaxLength` includes the raw cap.
- [ ] `PlacementTally` feedback localised ar/en; string exactly as in Error codes section.
- [ ] Every quiz/exam item body carries `image.url` from `IFileStorage.GetPublicUrl`; `IFileStorage` appended last in all 8 handlers; M1 edits are constructor-only.
- [ ] `QUESTION_TYPE_NOT_GRADABLE` gone from `ErrorCodes`, both resx and both web `diagramErrors` files; grep finds it only in historical `.process` files.
- [ ] `dotnet test api/ -c Release` green with `appsettings.json` moved aside (CI parity); `dotnet build` leaves `api/openapi/v1.json` unchanged.
- [ ] Web: canvas works by pointer drag, tap-then-zone and keyboard; Escape clears/cancels; announcements in a `role=status` region; focus follows the moved chip.
- [ ] Canvas is `dir="ltr"` with `insetInlineStart`/`top`; no `left`/`right`/`ml`/`mr` utilities; chips and list buttons ≥ 44 px.
- [ ] Drop mapping half-open in hundredths, closed at the image border (T1–T6); documented in question-schemas.
- [ ] `DragDropAnswerInput` and `DragDropCorrectAnswer` are dynamic imports; `diagramStudent` strings load only with them; no import of `dragDropValues`/`dragDropContentSchemas`/`diagramLocales` from quiz-chunk code.
- [ ] `npm run build && npm run perf:budget` passes (quiz ≤ 255 KB) and the measured quiz value is written to `docs/performance.md`.
- [ ] Admin preview uses the student canvas and «جرّب الإجابة» grades DragDrop; `DragDropPreview` is key-only.
- [ ] Web `typecheck`, `lint`, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`, `vitest run` green; T1–T50 present; existing tests changed only where marked modify.
- [ ] Docs updated per the Docs table (PRD §5.3/§6/§17, question-schemas, sessions, exams, mastery, exam-blueprints, performance, both design-system files, claude-design-prompt §4, prototype.md); no doc still says DragDrop is unservable or ungradable.
- [ ] New tests mutation-checked (break the grader rule / mapping edge, see the test fail, restore).
- [ ] No new dependency; no migration; no OpenAPI/Orval/Postman change.

### Design-system addition (both files, §5.13 / DiagramCanvas row)
Student answer mode: zones are buttons — empty: dashed muted stroke on surface/60; holding items: solid text stroke; drop target (dragging over, or an item is selected and the zone has focus): accent stroke + accent/15 fill; number badge plus a `count/capacity` pill. A zone list under the image (surface cards, `--r-md`, hairline) holds each zone's chips with ≥ 44 px ghost icon buttons (earlier, later, return to bank) and «ضعه هنا» (secondary, sm) while an item is selected. Chips: pill, ≥ 44 px, surface + border.strong; selected: soft fill + text border; dragging ghost: shadow-2; after checking: correct success.soft + success border + check icon, wrong or not placed danger.soft + danger border + x icon. Correct placements reuse answer-key mode.
