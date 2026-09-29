# Plan — [E14.S1] Essay question authoring with rubric (#117)

## Goal
An admin can create and edit an **Essay** question in the existing question editor. The question has a free-form rubric: criteria, each with points (its weight) and a scale of levels. It also has one or more model answers and an optional word limit. The admin sees the student-facing essay box in the live preview. The assigned teacher reviews the essay in the validation queue, where the rubric and model answers are shown, then approves or rejects it through the existing flow. Approved essays are **never served** to students (not in quizzes, exams, counts or the avatar) until student essay input (#119) ships. The rubric shape is the contract the LLM grader (#118) will grade against.

## Scope
**In:**
- `QuestionType.Essay`.
- Essay body and grading spec schema records, validation rules, canonicalisation and config caps.
- Excluding essays from servability.
- Grade-draft refuses essays (422).
- Import ignores `Essay` sheets.
- Web:
  - rubric editor (criteria, levels, model answers, word limit);
  - essay preview box with word count;
  - rubric view on the teacher validation page;
  - essay type in the list and queue filters;
  - blueprints limited to served types.
- Docs, OpenAPI, Orval client and Postman.

**Out:**
- LLM grading of essays, the `ai/` service and grader output shapes (#118).
- The student essay answer shape and its input in quiz or exam (#119).
- Teacher grading-review queue (E17).

**Deferred:** none. Nothing in this story needs credentials or an external service.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | PRD §19 Q7: free criteria list, or a fixed platform template? | **Free criteria list per question** (1..`QuestionRubricCriteriaMaxCount`). Update PRD §19 Q7 to "Decided (#117)". | PRD §6 already says "rubric (criteria + weights)" per question. A template can be added later as an editor preset without a schema change. |
| 2 | How are weights expressed? | Each criterion has integer `points` (1..`QuestionRubricPointsMax`). Weight = points / Σpoints. Question score = (Σ awarded / Σ points) × `maxScore`. `maxScore` stays independent (1..100). | Keeps the existing `QuestionGrade.FromNormalised(normalised, maxScore)` contract for #118. There is no cross-field coupling for admins to maintain. |
| 3 | Levels | Each criterion has 2..`QuestionRubricLevelsMaxCount` levels `{points, description}`. Level points are distinct integers in 0..criterion points. The set must include both 0 and the criterion's full points. Levels are stored sorted ascending by points. | This gives #118 an anchored scale per criterion (it awards one of the level points or any integer between them). It is deterministic to validate. |
| 4 | Model answers | `modelAnswers`: 1..`QuestionModelAnswersMaxCount` rich-text strings, sanitised like the stem. They live in the grading spec (server-only). | The story says "model answer(s)". The grading spec is never sent to students (docs/question-schemas.md "Body vs grading spec"). |
| 5 | Essay body | `{"maxWords": n}` with n optional, 1..`QuestionEssayMaxWordsMax`. With no limit the body is `{}`. | The body is student-visible, so a word limit belongs there (#119 renders it). Nothing else in an essay is student-visible besides the stem. |
| 6 | How are essays kept out of quizzes and exams before #119? | Add `&& x.Type != QuestionType.Essay` to `ServableQuestionSpecification.QuestionCondition`. Add `ServableQuestionSpecification.ServedTypes` (the 5 v1 types) for per-type listings. #119 removes both. | The spec is the **only** servable definition, and every serving read already goes through it: quiz, exam, blueprint shortfall, mastery, landing count, avatar retrieval and teacher-thread context. A single clause fails closed everywhere. There is no stored flag and no migration. |
| 7 | What do admins see for an approved essay? | `isServable = false` in `GET /api/questions`. `servableQuestionCount` excludes it. | This follows from D6 and is truthful: it will not be served. |
| 8 | Blueprints | API: `ServableTypeCounts.ToResults` lists `ServedTypes` only, so Essay is not in the counts. A blueprint that asks for essays fails with the existing `EXAM_BLUEPRINT_SHORTFALL`, because 0 are available. Web: the blueprint editor iterates `servedQuestionTypes`. | There is no new error path, and the existing `GetSubjectExamBlueprintsHandlerTests` (5 rows) stays valid. Showing an essay row that is always 0 would be noise until #119. |
| 9 | `POST /api/questions/grade-draft` with an Essay | 422 `QUESTION_TYPE_NOT_GRADABLE`. The web preview hides "جرّب الإجابة" for essays and shows a note instead. | The deterministic graders cannot grade essays. The LLM path is #118. |
| 10 | Bulk import (#66) | It does not apply. PRD §10.1 says "v1: deterministic types only". `QuestionImportColumns.Types` (5 types) drives `TypeForSheet` and the template, so an `Essay` sheet is ignored like any unknown sheet. | Without this, today's `Enum.GetValues` would map an "Essay" sheet to `Columns.For(Essay)`, which throws and returns a 500. A nested rubric does not fit spreadsheet columns. |
| 11 | Validation queue | No backend change. Detail already returns raw `body` and `gradingSpec`. The type filter accepts `Essay` through `IsInEnum`. The web adds `EssayRubricView` to the review page. | Reuse the existing flow: approve, reject, bulk approve and version check all apply unchanged. |
| 12 | Migration | None. `Questions.Type` is a string conversion with no check constraint (verified), and jsonb columns are schemaless. | — |
| 13 | Essay answer shape | Not defined here: #119 owns it. The web `toAnswerPayload` sends `{text}` for Essay only so the switch is exhaustive (never sent: D9). The backend `QuestionAnswerRules.CanRead(Essay)` stays `false` (default arm). | Avoids committing #119's contract early. |
| 14 | Criterion ids | `id` uses the existing id format `^[a-z0-9-]{1,20}$`, unique. The editor generates `c1`, `c2`, … | #118 returns a score per criterion and needs a stable key. It reuses `QuestionSchemaReader.IsValidId`. |
| 15 | Text canonicalisation | `title` is trimmed. `description` is trimmed, with blank → omitted (null). Level `description` is trimmed. Titles and descriptions are plain text. Model answers are rich text passed through `IRichTextSanitizer.Sanitize`. | Mirrors accepted answers (plain text, trimmed) and option text (sanitised rich text). |
| 16 | New caps | New `ContentOptions` properties **with code defaults**. They are added to `appsettings.example.json` and the `ApiFactory` settings. | The PROGRESS CI-parity rule. The existing test `ContentOptions` initialisers keep compiling and passing. |
| 17 | Editor default for Essay | Selecting Essay keeps the other values. The defaults already hold `criteria: [c1 with points 1, levels 0 and 1]`, one empty model answer and `maxWords: ''`. `maxScore` is not auto-changed. | The editor has no type-switch side effects today; don't add one. |
| 18 | Morabh | Morabh has no question, rubric or essay model (grep of `apis/` for rubric/essay: 0 hits). Every piece is **new — no Morabh equivalent**. It follows the in-repo `ShortQuestionRules`/`ChoiceQuestionRules` pattern. | Reuse-first check done. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/QuestionType.cs` | `public enum QuestionType { Mcq, Multi, TrueFalse, Fill, Short, Essay }` (append only). |
| `api/Elmanhg.Domain/Questions/ServableQuestionSpecification.cs` | `QuestionCondition = x => x.ValidationStatus == QuestionValidationStatus.Approved && x.RetiredAt == null && x.Type != QuestionType.Essay;`. Add the comment `// #119: essays have no student input yet, so they are never served.` above it. Add `public static IReadOnlyList<QuestionType> ServedTypes { get; } = [QuestionType.Mcq, QuestionType.Multi, QuestionType.TrueFalse, QuestionType.Fill, QuestionType.Short];` |
| `api/Elmanhg.Application/Questions/Shared/QuestionSchemaRules.cs` | `Validate`: add the arm `QuestionType.Essay => EssayQuestionRules.Validate(fields.Body, fields.GradingSpec, options),`. `Normalize`: add the arm `QuestionType.Essay => EssayQuestionRules.Normalize(fields.Body, fields.GradingSpec, sanitizer),`. |
| `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftValidator.cs` | Add `RuleFor(x => x.Question.Type).Must(x => x != QuestionType.Essay).WithErrorCode(ErrorCodes.QuestionTypeNotGradable);`. The existing answer rule's `.When(...)` becomes `.When(x => x.Question.Type.HasValue && Enum.IsDefined(x.Question.Type.Value) && x.Question.Type != QuestionType.Essay)`. |
| `api/Elmanhg.Application/Questions/Shared/Import/QuestionImportColumns.cs` | Add `public static IReadOnlyList<QuestionType> Types { get; } = [QuestionType.Mcq, QuestionType.Multi, QuestionType.TrueFalse, QuestionType.Fill, QuestionType.Short];`. `TypeForSheet` iterates `Types` instead of `Enum.GetValues<QuestionType>()`. |
| `api/Elmanhg.Application/Questions/Shared/Import/QuestionImportTemplate.cs` | Delete the private `Types` field and use `Columns.Types` in both places. |
| `api/Elmanhg.Application/ExamBlueprints/Shared/ServableTypeCounts.cs` | `ToResults` iterates `ServableQuestionSpecification.ServedTypes` instead of `Enum.GetValues<QuestionType>()`. |
| `api/Elmanhg.Application/Shared/Options/ContentOptions.cs` | Add 7 properties, each `[Range(1, int.MaxValue)]` with a default: `QuestionEssayMaxWordsMax = 2000`, `QuestionRubricCriteriaMaxCount = 10`, `QuestionRubricLevelsMaxCount = 6`, `QuestionRubricPointsMax = 100`, `QuestionRubricTextMaxLength = 1000`, `QuestionModelAnswersMaxCount = 3`, `QuestionModelAnswerMaxLength = 20000`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add the 14 constants listed in Error codes, after `QuestionToleranceInvalid`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Add the 14 `<data>` entries (strings below). |
| `api/Elmanhg.Api/appsettings.example.json` | Append the 7 keys and defaults inside `"Content"`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add the 7 `["Content:<Key>"] = "<default>"` entries next to the other `Content:` keys. |
| `api/Elmanhg.Tests/Builders/QuestionBuilder.cs` | Add the field `private bool _essay;` and `public QuestionBuilder Essay() { _essay = true; return this; }`. `Build()` uses `_essay ? QuestionType.Essay : QuestionType.Mcq` and `_essay ? EssayContent() : McqContent()`. Add `public static QuestionContent EssayContent()` (stem `<p>Explain inertia.</p>`, body `{"maxWords":200}`, spec `EssaySpecJson`, explanation `<p>Newton 1.</p>`, maxScore 5), `public static QuestionFields EssayFields()` (same content, `QuestionDifficulty.Medium`, no objective, no tags, maxScore 5), and `public const string EssaySpecJson = """{"criteria":[{"id":"c1","title":"Definition","points":2,"levels":[{"points":0,"description":"Missing"},{"points":1,"description":"Partial"},{"points":2,"description":"Complete"}]}],"modelAnswers":["<p>Inertia is resistance to change in motion.</p>"]}""";` |
| `api/openapi/v1.json` | Regenerated by `dotnet build` (`QuestionType` enum gains `Essay`). |
| `postman/elmanhg.postman_collection.json` | In the `Questions` folder, right after "Create question", add "Create essay question". It copies that request's method, URL, auth and headers. Body: the essay request from I1. A test script stores `essayQuestionId` from `id`. |
| `web/src/shared/api/generated/**` | Regenerated with `npm --prefix web run gen:api`. |
| `web/src/features/questions/api/questionOptions.ts` | Add `export const servedQuestionTypes = ['Mcq','Multi','TrueFalse','Fill','Short'] as const satisfies readonly QuestionType[];`. `questionTypes` becomes `['Mcq','Multi','TrueFalse','Fill','Short','Essay'] as const satisfies readonly QuestionType[]`. Add constants, each with a `// mirrors Content:<Key>` comment: `essayMaxWordsMax = 2000`, `rubricCriteriaMax = 10`, `rubricLevelsMin = 2`, `rubricLevelsMax = 6`, `rubricPointsMax = 100`, `modelAnswersMax = 3`. |
| `web/src/features/questions/index.ts` | Also export `servedQuestionTypes`. |
| `web/src/features/blueprints/api/blueprintValues.ts`, `components/TypeCountsTable.tsx`, `schemas/examBlueprintSchema.ts` | Import and iterate `servedQuestionTypes` instead of `questionTypes`. |
| `web/src/features/questions/schemas/questionEditorSchema.ts` | Add object fields: `maxWords: z.string()`, `criteria: z.array(z.object({ id: z.string(), title: z.string(), description: z.string(), points: z.string(), levels: z.array(z.object({ points: z.string(), description: z.string() })) }))`, `modelAnswers: z.array(z.object({ text: z.string() }))`. In `superRefine`: `if (values.type === 'Essay') { addEssayIssues(values, issue); }`. |
| `web/src/features/questions/schemas/questionContentSchemas.ts` | Add `essayBodySchema = z.object({ maxWords: z.number().int().optional() })` and `essaySpecSchema = z.object({ criteria: z.array(z.object({ id: z.string(), title: z.string(), description: z.string().optional(), points: z.number(), levels: z.array(z.object({ points: z.number(), description: z.string() })) })), modelAnswers: z.array(z.string()) })`. |
| `web/src/features/questions/api/questionValues.ts` | `emptyQuestionValues` adds `maxWords: ''`, `criteria: [emptyCriterion('c1')]`, `modelAnswers: [{ text: '' }]`. `readQuestionContent` gets `case 'Essay': return readEssay(body, spec);`. `toContent` gets `case 'Essay': return toEssayContent(values);`. Imports come from `./essayValues`. |
| `web/src/features/questions/api/studentQuestion.ts` | `StudentQuestion` gets `maxWords?: number \| null \| undefined`. `toStudentQuestion` sets `maxWords: type === 'Essay' && /^\d+$/.test(values.maxWords ?? '') ? Number(values.maxWords) : null`. `toAnswerPayload` gets `case 'Essay': return { text: answer.text };`. |
| `web/src/features/questions/api/answerKey.ts` | `case 'Essay': return answer;` |
| `web/src/features/questions/api/questionErrorFields.ts` | Map `QUESTION_ESSAY_MAX_WORDS_INVALID` to `'maxWords'`. Map the eight `QUESTION_RUBRIC_*` codes to `'criteria'`. Map `QUESTION_MODEL_ANSWERS_COUNT_INVALID`, `QUESTION_MODEL_ANSWER_REQUIRED` and `QUESTION_MODEL_ANSWER_TOO_LONG` to `'modelAnswers'`. |
| `web/src/features/questions/components/TypeSpecificFields.tsx` | `case 'Essay': return <EssayFields />;` |
| `web/src/features/questions/components/QuestionEditorForm.tsx` | Type option label: `value === 'Essay' ? t('editor.fields.typeV2', { type: t('types.Essay') }) : t(`types.${value}`)`. |
| `web/src/features/questions/components/QuestionRichTextField.tsx` | The `name` union gains `` `modelAnswers.${number}.text` ``. |
| `web/src/features/questions/components/QuestionView.tsx` | When `question.type === 'Essay'`, `inputs = <EssayAnswerInput question answer onAnswerChange disabled />`. The other branches are unchanged. |
| `web/src/features/questions/components/QuestionPreviewPanel.tsx` | When `values.type === 'Essay'`: do not render the try-answer button, the error alert or the result. Render `<p className="text-caption text-text-muted">{t('preview.essayNotGradable')}</p>` instead. |
| `web/src/features/questions/components/ValidationQuestionContent.tsx` | After the preview section, `{values.type === 'Essay' ? <EssayRubricView criteria={values.criteria} modelAnswers={values.modelAnswers} /> : null}`. |
| `web/src/features/quiz/api/quizItem.ts` | `fromAnswerPayload`: `case 'Essay': return { ...answer, text: text ?? '' };`. `isAnswerEmpty`: `case 'Essay': return answer.text.trim() === '';`. `quizQuestionTypes` is unchanged. |
| `web/src/features/quiz/api/correctAnswer.ts` | `describeCorrectAnswer`: `case 'Essay': return null;`. `choiceReview`: add `case 'Essay':` to the `Fill`/`Short` group that returns `undefined`. |
| `web/src/features/questions/i18n/ar.json`, `en.json` | The keys in "i18n keys" below. |
| `web/src/shared/i18n/ar.json`, `en.json` | Under `errors`: the 14 codes, with the same text as the resx. |
| `docs/question-schemas.md` | See "Docs" below. |
| `docs/PRD.md` | §5.3 Servable row, §17 rule 1 and §19 Q7 (see "Docs"). |
| `docs/exam-blueprints.md` | Line 41: the servable definition gains "and not an essay (until #119)". Line 74: "servable counts for every type" → "for every served type (the five v1 types; zeros included)". |
| `docs/question-import.md` | Line 25 gains: "`Essay` (v2) is authored in the editor only; an `Essay` sheet is ignored." |
| `docs/claude-design-prompt.md` | §4 line 145 (teacher `#/teacher/q/:id`): add "; an essay also shows its rubric (criteria with points and level descriptions, total points) and its model answers". §4 line 153 (question editor): add "; essay (v2) has an optional word limit, a rubric editor (criteria with title, optional description, points and at least two levels from 0 to full points; add/remove criteria and levels; a total-points line) and one to three model answers, and its preview shows the essay box with a word count and a note that essays are graded by the AI grader instead of «جرّب الإجابة»". |
| `docs/prototype.md` | After line 66, add: "The built app replaces the prototype's essay keyword list with a rubric (criteria, points, levels) and model answers (#117); essays are not served until student essay input (#119)." |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Questions/Schemas/EssaySchemas.cs` | records | `namespace Elmanhg.Domain.Questions.Schemas;` `public sealed record EssayBody(int? MaxWords);` `public sealed record RubricLevel(int? Points, string? Description);` `public sealed record RubricCriterion(string? Id, string? Title, string? Description, int? Points, List<RubricLevel>? Levels);` `public sealed record EssayGradingSpec(List<RubricCriterion>? Criteria, List<string>? ModelAnswers);` |
| 2 | `api/Elmanhg.Application/Questions/Shared/EssayQuestionRules.cs` | static class | `namespace Elmanhg.Application.Questions.Shared;` `public static class EssayQuestionRules`. **`public static List<string> Validate(JsonElement body, JsonElement gradingSpec, ContentOptions options)`**, in order: (1) `bodyRead = QuestionSchemaReader.TryRead<EssayBody>(body, out var essayBody)` and `specRead = TryRead<EssayGradingSpec>(gradingSpec, out var spec)`; `AddIf(!bodyRead, QuestionBodyInvalid)`; `AddIf(!specRead, QuestionGradingSpecInvalid)`; return if either failed. (2) `AddIf(essayBody.MaxWords is { } words && (words < 1 \|\| words > options.QuestionEssayMaxWordsMax), QuestionEssayMaxWordsInvalid)`. (3) `EssayRubricRules.AddErrors(errors, spec.Criteria, options)`. (4) `answers = spec.ModelAnswers ?? []`; `AddIf(answers.Count < 1 \|\| answers.Count > options.QuestionModelAnswersMaxCount, QuestionModelAnswersCountInvalid)`; `AddIf(answers.Any(string.IsNullOrWhiteSpace), QuestionModelAnswerRequired)`; `AddIf(answers.Any(x => x is not null && x.Length > options.QuestionModelAnswerMaxLength), QuestionModelAnswerTooLong)`. Return `errors`. **`public static (string Body, string GradingSpec) Normalize(JsonElement body, JsonElement gradingSpec, IRichTextSanitizer sanitizer)`**: read both with `QuestionSchemaReader.Read<T>`. Criteria map in input order to `new RubricCriterion(x.Id, x.Title!.Trim(), string.IsNullOrWhiteSpace(x.Description) ? null : x.Description.Trim(), x.Points, x.Levels!.OrderBy(l => l.Points).Select(l => new RubricLevel(l.Points, l.Description!.Trim())).ToList())`. `ModelAnswers` map through `sanitizer.Sanitize`. Return `(Serialize(new EssayBody(read.MaxWords)), Serialize(new EssayGradingSpec(criteria, modelAnswers)))`. LINQ one operator per line. |
| 3 | `api/Elmanhg.Application/Questions/Shared/EssayRubricRules.cs` | static class | `public static class EssayRubricRules`, with `// A scale needs a zero level and a full-points level.` above `private const int MinimumLevels = 2;` and `private const int MinimumCriteria = 1;`. **`public static void AddErrors(List<string> errors, List<RubricCriterion>? criteria, ContentOptions options)`**, in order, each via `QuestionSchemaReader.AddIf`: `list = criteria ?? []`; count outside `MinimumCriteria..QuestionRubricCriteriaMaxCount` → `QuestionRubricCriteriaCountInvalid`; any `!IsValidId(x?.Id)` → `QuestionRubricCriterionIdInvalid`; duplicate ids (Ordinal) → `QuestionRubricCriterionIdDuplicate`; any blank `x?.Title` → `QuestionRubricCriterionTitleRequired`; any trimmed length of `Title`, `Description` or any level `Description` over `QuestionRubricTextMaxLength` → `QuestionRubricTextTooLong`; any `x?.Points` null, <1 or >`QuestionRubricPointsMax` → `QuestionRubricPointsInvalid`; any `x?.Levels` count outside `MinimumLevels..QuestionRubricLevelsMaxCount` (null counts as 0) → `QuestionRubricLevelsCountInvalid`; any level null or with a blank description → `QuestionRubricLevelDescriptionRequired`; any criterion with valid `Points p` and `!IsFullScale(x.Levels ?? [], p)` → `QuestionRubricLevelPointsInvalid`. `private static bool IsFullScale(List<RubricLevel> levels, int points)`: every level non-null with non-null `Points` in `0..points`, the points distinct, the set containing both `0` and `points`. `private static bool IsTooLong(string? text, int max) => text is not null && text.Trim().Length > max;` |
| 4 | `web/src/features/questions/api/essayValues.ts` | module | `import type { JsonElement } from generated model; import type { QuestionValues }`. Exports: `emptyCriterion(id: string): QuestionValues['criteria'][number]` → `{ id, title: '', description: '', points: '1', levels: [{ points: '0', description: '' }, { points: '1', description: '' }] }`. `nextCriterionId(ids: readonly string[]): string` → the first `c${n}` (n = 1, 2, …) not in ids. `readEssay(body: JsonElement, spec: JsonElement): Partial<QuestionValues>` → `{}` when `essayBodySchema` fails. Otherwise `maxWords` is `String(n)` or `''`. `criteria` (only when `essaySpecSchema` parses) maps points to `String`, description to `?? ''` and levels' points to `String`. `modelAnswers` is `spec.modelAnswers.map(text => ({ text }))`. `toEssayContent(values: QuestionValues): Pick<UpdateQuestionRequest,'body'\|'gradingSpec'>` → `body: values.maxWords === '' ? {} : { maxWords: Number(values.maxWords) }` and `gradingSpec: { criteria: values.criteria.map(c => ({ id: c.id, title: c.title, description: c.description, points: Number(c.points), levels: c.levels.map(l => ({ points: Number(l.points), description: l.description })) })), modelAnswers: values.modelAnswers.map(m => m.text) }`. `rubricTotalPoints(criteria: readonly { points: string }[]): number` sums the points matching `/^\d+$/`. `countWords(text: string): number` → `text.trim() === '' ? 0 : text.trim().split(/\s+/).length`. |
| 5 | `web/src/features/questions/schemas/essayRules.ts` | module | `export interface EssayRuleInput { maxWords: string; criteria: { title: string; points: string; levels: { points: string; description: string }[] }[]; modelAnswers: { text: string }[] }`. `export function addEssayIssues(values: EssayRuleInput, issue: (path: (string \| number)[], message: string) => void): void`. Rules use `errorKey = k => \`questions:editor.errors.${k}\``: `maxWords !== ''` and not (`/^\d+$/` and 1..`essayMaxWordsMax`) → `['maxWords']` `maxWords`. `criteria.length === 0` → `['criteria']` `criteriaCount`. Per criterion i: blank trimmed `title` → `['criteria',i,'title']` `'validation.required'`. `points` not a digit string or outside 1..`rubricPointsMax` → `['criteria',i,'points']` `rubricPoints`. `levels.length < rubricLevelsMin` → `['criteria',i,'levels']` `levelsCount`. Per level j: blank trimmed description → `['criteria',i,'levels',j,'description']` `'validation.required'`. Level points not a digit string, or (criterion points valid and level > criterion points) → `['criteria',i,'levels',j,'points']` `levelPoints`. When the criterion points and every level's points are valid and `levels.length >= 2`, a set that is not distinct or lacks 0 or the full points → `['criteria',i,'levels']` `levelScale`. `modelAnswers.length === 0` → `['modelAnswers']` `modelAnswersCount`. Each `!hasRichTextContent(text)` → `['modelAnswers',k,'text']` `'validation.required'`. |
| 6 | `web/src/features/questions/components/EssayFields.tsx` | component | `export function EssayFields()`: `<div className="flex flex-col gap-3">` holding `<TextField<QuestionValues> name="maxWords" label={t('editor.essay.maxWords')} description={t('editor.essay.maxWordsHint')} dir="ltr" />`, `<RubricCriteriaField />` and `<ModelAnswersField />`. |
| 7 | `web/src/features/questions/components/RubricCriteriaField.tsx` | component | `useFieldArray<QuestionValues,'criteria'>({ name: 'criteria' })`, `useWatch` of `criteria`, and `useFormState` for `criteria` errors (message = `errors.criteria?.message ?? errors.criteria?.root?.message`). It renders `<fieldset>` with `<legend>{t('editor.essay.rubricLegend')}</legend>`, a caption `t('editor.essay.rubricHint', { total: rubricTotalPoints(criteria) })`, and a `RubricCriterionCard` per field (`key={field.id}`, `index`, `canRemove={fields.length > 1}`, `onRemove`). An error `<p className="text-caption text-danger">` follows, as in `FillBlanksField`. The secondary sm button `t('editor.essay.addCriterion')` with a `Plus` icon is disabled at `rubricCriteriaMax` and appends `emptyCriterion(nextCriterionId(ids))`. |
| 8 | `web/src/features/questions/components/RubricCriterionCard.tsx` | component | Props `RubricCriterionCardProps { index: number; canRemove: boolean; onRemove: () => void }`. It renders a bordered `rounded-md border border-border p-3 flex flex-col gap-3` block. The heading row has `t('editor.essay.criterion', { number })` and a ghost sm remove button (`Trash2`, `aria-label={t('editor.essay.removeCriterion', { number })}`, `disabled={!canRemove}`). Then `TextField` `criteria.${i}.title` (label `criterionTitle`), `TextAreaField` `criteria.${i}.description` (label `criterionDescription`), `TextField` `criteria.${i}.points` dir ltr (label `criterionPoints`), and `<RubricLevelsField criterionIndex={index} />`. `number = index + 1`. |
| 9 | `web/src/features/questions/components/RubricLevelsField.tsx` | component | Props `{ criterionIndex: number }`. Uses `useFieldArray` with `name: \`criteria.${criterionIndex}.levels\``. It renders `<fieldset>`, legend `t('editor.essay.levelsLegend', { number })`, and per level a `flex items-start gap-2` row. Each row has a `TextField` for points (dir ltr, label `levelPoints` `{number, level}`), a `TextField` for description (label `levelDescription`) and a ghost remove button (`removeLevel`, disabled at `rubricLevelsMin`). The level error message comes from `useFormState` (`errors.criteria?.[i]?.levels?.message ?? ...root?.message`). The add button `t('editor.essay.addLevel', { number })` is disabled at `rubricLevelsMax` and appends `{ points: '', description: '' }`. |
| 10 | `web/src/features/questions/components/ModelAnswersField.tsx` | component | `useFieldArray<QuestionValues,'modelAnswers'>`, `<fieldset>` with legend `editor.essay.modelAnswersLegend` and a hint `modelAnswersHint`. Per item: `<QuestionRichTextField name={\`modelAnswers.${i}.text\`} label={t('editor.essay.modelAnswer', { number })} compact />` with no `onUploadImage`, because `TypeSpecificFields` has no lesson id and model answers need no images. Also a ghost remove button (`removeModelAnswer`, disabled when there is 1), the root error `<p>`, and add (`addModelAnswer`, disabled at `modelAnswersMax`, appends `{ text: '' }`). |
| 11 | `web/src/features/questions/components/EssayAnswerInput.tsx` | component | Props `EssayAnswerInputProps { question: StudentQuestion; answer: QuestionAnswer; onAnswerChange: (a: QuestionAnswer) => void; disabled?: boolean \| undefined }`. `useId` supplies the ids. It renders a `Label` `t('view.essayLabel')` and a `<textarea rows={6}>` with the same className as `TextAreaField`, `value={answer.text}`, `placeholder={t('view.essayPlaceholder')}`, `disabled`, `aria-describedby={countId}` and `onChange` → `onAnswerChange({ ...answer, text: e.target.value })`. Then `<p id={countId} className="text-caption text-text-muted">` showing `question.maxWords ? t('view.essayWordsOf', { count, max: question.maxWords }) : t('view.essayWords', { count })`, where `count = countWords(answer.text)`. |
| 12 | `web/src/features/questions/components/EssayRubricView.tsx` | component | Props `EssayRubricViewProps { criteria: QuestionValues['criteria']; modelAnswers: QuestionValues['modelAnswers'] }`. `<section aria-label={t('validation.detail.rubric')} className={card classes as in ValidationQuestionContent}>`, containing `h2` `rubric`, `<p>` `rubricTotal {total: rubricTotalPoints(criteria)}` and `<ol className="flex flex-col gap-3">`. Each criterion `<li key={c.id}>` has an `h3` of `c.title` with `t('validation.detail.criterionPoints', { points: c.points })`, a description `<p>` when non-empty, and `<ul>` of levels with `<li key={\`${c.id}-${l.points}\`}>{t('validation.detail.level', { points: l.points, description: l.description })}</li>`. Then `h3` `modelAnswers` and, per answer, `<div className="rounded-md border border-border p-3" aria-label={t('validation.detail.modelAnswer', { number })}>` wrapping `<RichTextViewer html={m.text} />` (key: `\`model-${number}\``; model answers are static, not reordered). |
| 13 | `api/Elmanhg.Tests/Application/Features/Questions/Shared/EssayQuestionRulesTests.cs` | tests | See the test plan. |
| 14 | `api/Elmanhg.Tests/Application/Features/ExamBlueprints/Shared/ServableTypeCountsTests.cs` | tests | See the test plan. |
| 15 | `api/Elmanhg.Tests/Integration/Content/EssayQuestionEndpointTests.cs` | tests | `public sealed class EssayQuestionEndpointTests(ApiFactory factory)`, declared like `QuestionsEndpointTests`. |
| 16 | `api/Elmanhg.Tests/Integration/QuestionValidation/EssayValidationEndpointTests.cs` | tests | Declared like `ValidationQueueEndpointTests`, and uses `ValidationTestData`. |
| 17 | `web/src/features/questions/api/essayValues.test.ts` | tests | — |
| 18 | `web/src/features/questions/pages/NewEssayQuestion.test.tsx` | tests | Same harness as `NewQuestionPage.test.tsx` (lesson and create handlers). |
| 19 | `web/src/features/questions/pages/ValidationEssayQuestion.test.tsx` | tests | Same harness as `ValidationQuestionPage.test.tsx`, with an Essay detail fixture. |

`Validate`/`Normalize` in #2 and `AddErrors` in #3 use `QuestionSchemaReader.AddIf`, `IsValidId`, `Serialize`, `TryRead` and `Read`. Do not add any new helper to `QuestionSchemaReader`.

### i18n keys (questions namespace; en / ar)
- `types.Essay`: Essay / مقالي
- `editor.fields.typeV2`: {type} (v2) / {type} (v2)
- `editor.essay.maxWords`: Word limit (optional) / الحد الأقصى للكلمات (اختياري)
- `editor.essay.maxWordsHint`: Shown to the student under the answer box. / يظهر للطالب أسفل مربع الإجابة.
- `editor.essay.rubricLegend`: Grading rubric / معايير التصحيح
- `editor.essay.rubricHint`: Total points: {total}. The score is the points earned ÷ {total} × the question's points. / مجموع النقاط: {total}. الدرجة = النقاط المحققة ÷ {total} × درجة السؤال.
- `editor.essay.criterion`: Criterion {number} / المعيار {number}
- `editor.essay.criterionTitle`: Criterion {number} title / عنوان المعيار {number}
- `editor.essay.criterionDescription`: What criterion {number} checks (optional) / ما يقيسه المعيار {number} (اختياري)
- `editor.essay.criterionPoints`: Criterion {number} points / نقاط المعيار {number}
- `editor.essay.addCriterion`: Add criterion / إضافة معيار
- `editor.essay.removeCriterion`: Remove criterion {number} / حذف المعيار {number}
- `editor.essay.levelsLegend`: Levels of criterion {number} / مستويات المعيار {number}
- `editor.essay.levelPoints`: Criterion {number}, level {level} points / نقاط المستوى {level} في المعيار {number}
- `editor.essay.levelDescription`: Criterion {number}, level {level} description / وصف المستوى {level} في المعيار {number}
- `editor.essay.addLevel`: Add level to criterion {number} / إضافة مستوى للمعيار {number}
- `editor.essay.removeLevel`: Remove level {level} of criterion {number} / حذف المستوى {level} من المعيار {number}
- `editor.essay.modelAnswersLegend`: Model answers / الإجابات النموذجية
- `editor.essay.modelAnswersHint`: Used by the AI grader; never shown to the student. / يستخدمها مصحح الذكاء الاصطناعي ولا تظهر للطالب.
- `editor.essay.modelAnswer`: Model answer {number} / الإجابة النموذجية {number}
- `editor.essay.addModelAnswer`: Add model answer / إضافة إجابة نموذجية
- `editor.essay.removeModelAnswer`: Remove model answer {number} / حذف الإجابة النموذجية {number}
- `editor.errors.maxWords`: Enter a word limit from 1 to 2000, or leave it empty. / اكتب حدًا للكلمات من 1 إلى 2000 أو اتركه فارغًا.
- `editor.errors.criteriaCount`: Add at least one criterion. / أضف معيارًا واحدًا على الأقل.
- `editor.errors.rubricPoints`: Enter whole points from 1 to 100. / اكتب نقاطًا صحيحة من 1 إلى 100.
- `editor.errors.levelsCount`: Add at least two levels. / أضف مستويين على الأقل.
- `editor.errors.levelPoints`: Enter whole points from 0 to the criterion's points. / اكتب نقاطًا صحيحة من 0 إلى نقاط المعيار.
- `editor.errors.levelScale`: Levels need different points, including 0 and the criterion's full points. / يجب أن تختلف نقاط المستويات وأن تشمل 0 ونقاط المعيار كاملة.
- `editor.errors.modelAnswersCount`: Add at least one model answer. / أضف إجابة نموذجية واحدة على الأقل.
- `preview.essayNotGradable`: Essays are graded by the AI grader; the test grader does not cover them. / تُصحَّح الأسئلة المقالية بمصحح الذكاء الاصطناعي، ولا يدعمها المصحح التجريبي.
- `view.essayLabel`: Your essay / إجابتك المقالية
- `view.essayPlaceholder`: Write your answer / اكتب إجابتك المقالية
- `view.essayWords`: {count} words / عدد الكلمات: {count}
- `view.essayWordsOf`: {count} of {max} words / عدد الكلمات: {count} من {max}
- `validation.detail.rubric`: Grading rubric / معايير التصحيح
- `validation.detail.rubricTotal`: Total points: {total} / مجموع النقاط: {total}
- `validation.detail.criterionPoints`: ({points} points) / ({points} نقاط)
- `validation.detail.level`: {points} points: {description} / {points} نقاط: {description}
- `validation.detail.modelAnswers`: Model answers / الإجابات النموذجية
- `validation.detail.modelAnswer`: Model answer {number} / الإجابة النموذجية {number}

## Error codes
All are `ValidationException` from FluentValidation (the existing `QuestionFieldsValidator` custom rule, or the grade-draft validator) → HTTP 422 problem+json `code`.

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `QuestionTypeNotGradable` | `QUESTION_TYPE_NOT_GRADABLE` | `GradeQuestionDraftValidator` | ValidationException | 422 |
| `QuestionEssayMaxWordsInvalid` | `QUESTION_ESSAY_MAX_WORDS_INVALID` | `EssayQuestionRules` | ValidationException | 422 |
| `QuestionRubricCriteriaCountInvalid` | `QUESTION_RUBRIC_CRITERIA_COUNT_INVALID` | `EssayRubricRules` | ValidationException | 422 |
| `QuestionRubricCriterionIdInvalid` | `QUESTION_RUBRIC_CRITERION_ID_INVALID` | `EssayRubricRules` | ValidationException | 422 |
| `QuestionRubricCriterionIdDuplicate` | `QUESTION_RUBRIC_CRITERION_ID_DUPLICATE` | `EssayRubricRules` | ValidationException | 422 |
| `QuestionRubricCriterionTitleRequired` | `QUESTION_RUBRIC_CRITERION_TITLE_REQUIRED` | `EssayRubricRules` | ValidationException | 422 |
| `QuestionRubricTextTooLong` | `QUESTION_RUBRIC_TEXT_TOO_LONG` | `EssayRubricRules` | ValidationException | 422 |
| `QuestionRubricPointsInvalid` | `QUESTION_RUBRIC_POINTS_INVALID` | `EssayRubricRules` | ValidationException | 422 |
| `QuestionRubricLevelsCountInvalid` | `QUESTION_RUBRIC_LEVELS_COUNT_INVALID` | `EssayRubricRules` | ValidationException | 422 |
| `QuestionRubricLevelPointsInvalid` | `QUESTION_RUBRIC_LEVEL_POINTS_INVALID` | `EssayRubricRules` | ValidationException | 422 |
| `QuestionRubricLevelDescriptionRequired` | `QUESTION_RUBRIC_LEVEL_DESCRIPTION_REQUIRED` | `EssayRubricRules` | ValidationException | 422 |
| `QuestionModelAnswersCountInvalid` | `QUESTION_MODEL_ANSWERS_COUNT_INVALID` | `EssayQuestionRules` | ValidationException | 422 |
| `QuestionModelAnswerRequired` | `QUESTION_MODEL_ANSWER_REQUIRED` | `EssayQuestionRules` | ValidationException | 422 |
| `QuestionModelAnswerTooLong` | `QUESTION_MODEL_ANSWER_TOO_LONG` | `EssayQuestionRules` | ValidationException | 422 |

Resource strings (en / ar). Arabic follows the resx house style: plain letters, no hamza forms, and the same text in `web/src/shared/i18n`.
- `QUESTION_TYPE_NOT_GRADABLE`: This question type is graded by the AI grader, not the test grader. / هذا النوع من الاسئلة يصححه مصحح الذكاء الاصطناعي وليس المصحح التجريبي.
- `QUESTION_ESSAY_MAX_WORDS_INVALID`: Enter a whole-number word limit within the allowed range, or leave it empty. / اكتب حدا للكلمات رقما صحيحا في النطاق المسموح او اتركه فارغا.
- `QUESTION_RUBRIC_CRITERIA_COUNT_INVALID`: Add at least one criterion, up to the allowed maximum. / اضف معيارا واحدا على الاقل وبما لا يتجاوز الحد المسموح.
- `QUESTION_RUBRIC_CRITERION_ID_INVALID`: Each criterion id must be 1 to 20 lowercase letters, digits or dashes. / معرف كل معيار من 1 الى 20 حرفا لاتينيا صغيرا او رقما او شرطة.
- `QUESTION_RUBRIC_CRITERION_ID_DUPLICATE`: Criterion ids must be unique. / يجب الا يتكرر معرف المعيار.
- `QUESTION_RUBRIC_CRITERION_TITLE_REQUIRED`: Give every criterion a title. / اكتب عنوانا لكل معيار.
- `QUESTION_RUBRIC_TEXT_TOO_LONG`: A criterion title or description, or a level description, is too long. / عنوان المعيار او وصفه او وصف المستوى اطول من المسموح.
- `QUESTION_RUBRIC_POINTS_INVALID`: Each criterion's points must be a whole number from 1 to the allowed maximum. / نقاط كل معيار رقم صحيح من 1 الى الحد المسموح.
- `QUESTION_RUBRIC_LEVELS_COUNT_INVALID`: Each criterion needs two or more levels, up to the allowed maximum. / يحتاج كل معيار الى مستويين او اكثر بما لا يتجاوز الحد المسموح.
- `QUESTION_RUBRIC_LEVEL_POINTS_INVALID`: Level points must be different whole numbers from 0 to the criterion's points, including 0 and the full points. / نقاط المستويات ارقام صحيحة مختلفة من 0 الى نقاط المعيار وتشمل 0 والنقاط كاملة.
- `QUESTION_RUBRIC_LEVEL_DESCRIPTION_REQUIRED`: Describe every level. / اكتب وصفا لكل مستوى.
- `QUESTION_MODEL_ANSWERS_COUNT_INVALID`: Add at least one model answer, up to the allowed maximum. / اضف اجابة نموذجية واحدة على الاقل وبما لا يتجاوز الحد المسموح.
- `QUESTION_MODEL_ANSWER_REQUIRED`: A model answer cannot be empty. / لا يمكن ان تكون الاجابة النموذجية فارغة.
- `QUESTION_MODEL_ANSWER_TOO_LONG`: A model answer is too long. / الاجابة النموذجية اطول من المسموح.

## Domain behaviour
- No new entity method, and `Question` is unchanged. Create, update, resubmit, approve, reject, retire, versioning, revisions, `SubmittedAt`, events, audit and `UpdationDate` all work for Essay exactly as for the other types, because the content is opaque `QuestionContent`.
- `ServableQuestionSpecification.QuestionCondition` gains `&& x.Type != QuestionType.Essay`. `IsSatisfiedBy` and `WhereServable` pick it up automatically (compiled and SQL). `ServedTypes` = the 5 v1 types.
- `QuestionGrader.Grade(QuestionType.Essay, …)` hits the existing `_ => throw new InvalidOperationException("Unsupported question type.")`. It stays unreachable in production (not served; grade-draft 422).

## API surface
No new endpoints, routes or policies. Behaviour changes on existing ones:
- `POST /api/questions`, `PUT /api/questions/{id}`, `PUT /api/questions/{id}/resubmit` (`ContentManage`) accept `type: "Essay"` with the essay body and spec. They return 422 with the codes above.
- `POST /api/questions/grade-draft` (`ContentManage`) with `type: "Essay"` → 422 `QUESTION_TYPE_NOT_GRADABLE`.
- `GET /api/questions`, `GET /api/validation-queue` and `GET /api/validation-queue/questions/{id}` accept or return `Essay` as a type. An approved essay has `isServable: false`.
- `GET /api/exam-blueprints/subjects/{id}`: `servable` arrays list the 5 served types only.
- OpenAPI: `QuestionType` gains `Essay`.

Essay request example (used in Postman and I1):
```json
{"lessonId":"…","type":"Essay","stem":"<p>Explain inertia.</p>","body":{"maxWords":200},
 "gradingSpec":{"criteria":[{"id":"c1","title":"  Definition ","description":"  ","points":2,
   "levels":[{"points":2,"description":"Complete"},{"points":0,"description":"Missing"},{"points":1,"description":" Partial "}]}],
   "modelAnswers":["<p>Inertia is resistance to change in motion.</p>"]},
 "explanation":"<p>Newton 1.</p>","difficulty":"Medium","tags":[],"maxScore":5}
```
Canonical stored spec: `{"criteria":[{"id":"c1","title":"Definition","points":2,"levels":[{"points":0,"description":"Missing"},{"points":1,"description":"Partial"},{"points":2,"description":"Complete"}]}],"modelAnswers":["<p>Inertia is resistance to change in motion.</p>"]}`

## Docs (exact content changes)
- **`docs/question-schemas.md`**:
  - **Types:** add `- \`Essay\` (v2): rich-text answer (#119), graded by the AI grader against the rubric and model answers (#118). Not servable until #119.`
  - **Per-type shapes:** add an **Essay** block with body `{"maxWords":200}` (optional; `{}` without a limit) and the canonical spec above. Follow it with prose on the D2 score formula and the D3 level-scale rule. Criterion `id` uses the id format. `title`, `description` and level `description` are plain text, trimmed, with a blank `description` omitted. `modelAnswers` are sanitised rich text. Levels are stored in ascending points.
  - **Rules table:** add one row per new code with its caps (`Content:QuestionEssayMaxWordsMax`, `Content:QuestionRubricCriteriaMaxCount`, `Content:QuestionRubricLevelsMaxCount`, `Content:QuestionRubricPointsMax`, `Content:QuestionRubricTextMaxLength`, `Content:QuestionModelAnswersMaxCount`, `Content:QuestionModelAnswerMaxLength`). Note that a non-integer `maxWords`, `points` or level `points` fails reading: `QUESTION_BODY_INVALID` / `QUESTION_GRADING_SPEC_INVALID`.
  - **Answer shapes:** add "Essay: defined by #119."
  - **Grading:** add a bullet: "Essay is not graded by these graders; `grade-draft` returns `422 QUESTION_TYPE_NOT_GRADABLE`."
  - **Servable:** the definition becomes "Approved ∧ lesson Published ∧ not retired ∧ not an essay (essays are excluded until student essay input, #119)". Add a line on `ServedTypes`.
  - **Changing a schema:** add "Essay is not importable (PRD §10.1)."
- **`docs/PRD.md`**:
  - §5.3 Servable row: `Approved AND lesson.state == Published AND question.not_retired AND type != Essay (until student essay input ships, E14.S3)`.
  - §17 rule 1: append "; essays are not servable until student essay input ships".
  - §19 Q7: "Decided (#117): a free criteria list per question; each criterion has points (its weight) and a level scale from 0 to full points; one to three model answers."

## Test plan
The dotnet tests use FluentAssertions (repo standard), `TestContext.Current.CancellationToken`, and `QuestionBuilder.Json`. The `_options` in the new rules tests is `new ContentOptions { …same initialiser as ShortQuestionRulesTests… }`; the new properties use their code defaults unless a test sets them. `Sanitizer()` is the NSubstitute pattern from `ChoiceQuestionRulesTests` (`clean:{x}`).

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| D1 | `Domain/Questions/ServableQuestionSpecificationTests` (add) | `IsSatisfiedBy_ApprovedEssayInPublishedLesson_ReturnsFalse` | `_builder.Lesson.Publish(...)`; `_builder.Essay().Approved().Build()` → `false`. |
| D2 | same (add) | `ServedTypes_EveryTypeExceptEssay` | `ServedTypes` equals `Enum.GetValues<QuestionType>()` without `Essay`, in order. |
| D3 | `Domain/Questions/Grading/QuestionGraderTests` (add) | `Grade_Essay_ThrowsInvalidOperationException` | `QuestionGrader.Grade(QuestionType.Essay, EssaySpecJson, 5, Json("{}"))` throws `InvalidOperationException`. |
| A1 | `EssayQuestionRulesTests` | `Validate_ValidEssay_ReturnsNoErrors` | Body `{"maxWords":200}` + `EssaySpecJson` → empty. |
| A2 | same | `Validate_NoWordLimit_ReturnsNoErrors` | Body `{}` → empty. |
| A3 | same | `Validate_BodyNotObject_ReturnsQuestionBodyInvalid` | Body `[]` → contains `QuestionBodyInvalid`. |
| A4 | same | `Validate_SpecNotObject_ReturnsQuestionGradingSpecInvalid` | Spec `"x"` → contains `QuestionGradingSpecInvalid`. |
| A5 | same | `Validate_MaxWordsOutOfRange_ReturnsQuestionEssayMaxWordsInvalid` | `[Theory]` 0 and 2001 → contains the code. |
| A6 | same | `Validate_NoCriteria_ReturnsQuestionRubricCriteriaCountInvalid` | `"criteria":[]` → the code. |
| A7 | same | `Validate_TooManyCriteria_ReturnsQuestionRubricCriteriaCountInvalid` | `_options.QuestionRubricCriteriaMaxCount = 1` with 2 criteria → the code. |
| A8 | same | `Validate_CriterionIdInvalid_ReturnsQuestionRubricCriterionIdInvalid` | id `"C 1"` → the code. |
| A9 | same | `Validate_CriterionIdDuplicate_ReturnsQuestionRubricCriterionIdDuplicate` | Two criteria with id `c1` → the code. |
| A10 | same | `Validate_BlankTitle_ReturnsQuestionRubricCriterionTitleRequired` | title `"  "` → the code. |
| A11 | same | `Validate_TitleTooLong_ReturnsQuestionRubricTextTooLong` | `_options.QuestionRubricTextMaxLength = 3`, title `"Long"` → the code. |
| A12 | same | `Validate_LevelDescriptionTooLong_ReturnsQuestionRubricTextTooLong` | Max 3, a level description `"Complete"`, title `"Def"` → the code. |
| A13 | same | `Validate_PointsOutOfRange_ReturnsQuestionRubricPointsInvalid` | `[Theory]` 0 and 101 (levels adjusted to 0/points) → the code. |
| A14 | same | `Validate_FractionalPoints_ReturnsQuestionGradingSpecInvalid` | `"points":1.5` → `QuestionGradingSpecInvalid`. |
| A15 | same | `Validate_OneLevel_ReturnsQuestionRubricLevelsCountInvalid` | Levels `[{0}]` → the code. |
| A16 | same | `Validate_TooManyLevels_ReturnsQuestionRubricLevelsCountInvalid` | `_options.QuestionRubricLevelsMaxCount = 2` with 3 levels → the code. |
| A17 | same | `Validate_BlankLevelDescription_ReturnsQuestionRubricLevelDescriptionRequired` | description `" "` → the code. |
| A18 | same | `Validate_BrokenLevelScale_ReturnsQuestionRubricLevelPointsInvalid` | `[Theory]` level points (criterion points 4): `[1,4]` (no 0), `[0,2]` (no full), `[0,0,4]` (duplicate), `[0,4,5]` (above), `[0,null]` (null) → the code. |
| A19 | same | `Validate_NoModelAnswers_ReturnsQuestionModelAnswersCountInvalid` | `"modelAnswers":[]` → the code. |
| A20 | same | `Validate_TooManyModelAnswers_ReturnsQuestionModelAnswersCountInvalid` | Max 1, 2 answers → the code. |
| A21 | same | `Validate_BlankModelAnswer_ReturnsQuestionModelAnswerRequired` | `["  "]` → the code. |
| A22 | same | `Validate_ModelAnswerTooLong_ReturnsQuestionModelAnswerTooLong` | `_options.QuestionModelAnswerMaxLength = 5`, `["<p>long</p>"]` → the code. |
| A23 | same | `Normalize_Essay_TrimsSortsLevelsOmitsBlankDescriptionAndSanitises` | The I1 input → the spec `JsonNode.DeepEquals` the canonical spec, with model answer `clean:<p>…</p>`; body `{"maxWords":200}`; unknown props dropped (add `"extra":1`). |
| A24 | same | `Normalize_NoWordLimit_WritesEmptyBody` | Body `{"maxWords":null}` → `"{}"`. |
| A25 | `Questions/Shared/QuestionFieldsValidatorTests` (add) | `Validate_EssayWithoutModelAnswer_ReturnsCodeOnBody` | `EssayFields()` with spec lacking `modelAnswers` → an error with code `QuestionModelAnswersCountInvalid` and property `Body`. |
| A26 | `Questions/GradeQuestionDraft/GradeQuestionDraftValidatorTests` (add) | `Validate_Essay_ReturnsQuestionTypeNotGradableOnly` | `EssayFields()`, answer `{}` → contains `QuestionTypeNotGradable` and does not contain `QuestionAnswerInvalid`. |
| A27 | `Questions/Shared/Import/QuestionImportColumnsTests` (add) | `TypeForSheet_EssaySheet_ReturnsNull` | `TypeForSheet("Essay")` → `null`. |
| A28 | `ExamBlueprints/Shared/ServableTypeCountsTests` (new) | `ToResults_EssayAvailable_ListsServedTypesOnly` | Input `{Mcq:2, Essay:3}` → 5 results in `ServedTypes` order, `Mcq` = 2, no `Essay`. |
| I1 | `Integration/Content/EssayQuestionEndpointTests` | `Post_AdminEssay_StoresCanonicalBodyAndRubric` | 200. DB (`QuestionTestData.ReadQuestionAsync`): `Type == Essay`, `Pending`, version 1, `GradingSpec` DeepEquals canonical, `Body` DeepEquals `{"maxWords":200}`, one revision. |
| I2 | same | `Post_EssayBrokenLevelScale_Returns422QuestionRubricLevelPointsInvalid` | Levels `[0,1]` with points 2 → 422, `code` = `QUESTION_RUBRIC_LEVEL_POINTS_INVALID`. Nothing persisted for the lesson. |
| I3 | same | `Post_EssayAsTeacher_Returns403` | Teacher client → 403. |
| I4 | same | `Put_EssayRubricChange_BumpsVersionAndSnapshotsRubric` | Create, then PUT with criterion points 3 (levels 0/3) → 200. DB: version 2, 2 revisions, the latest snapshot `gradingSpec.criteria[0].points == 3`. |
| I5 | same | `PostGradeDraft_Essay_Returns422QuestionTypeNotGradable` | `POST /api/questions/grade-draft` with the essay + `answer: {}` → 422, code `QUESTION_TYPE_NOT_GRADABLE`. |
| I6 | `Integration/QuestionValidation/EssayValidationEndpointTests` | `GetQuestion_Essay_ReturnsRubricAndModelAnswers` | Assigned teacher `GET /api/validation-queue/questions/{id}` → 200. `type == "Essay"`, `gradingSpec.criteria[0].levels` has 3 entries, `modelAnswers[0]` equals the stored HTML. |
| I7 | same | `GetQueue_TypeEssay_ListsPendingEssaysOnly` | One essay + one MCQ in the subject; `GET /api/validation-queue?type=Essay` → only the essay id. |
| I8 | same | `Approve_EssayInPublishedLesson_ApprovedButNotServable` | Lesson seeded with `ContentTestData.SeedLessonInStateAsync(..., LessonState.Published, ...)`. Admin creates an essay; teacher approves `{version:1}` → 200. Admin `GET /api/lessons?unitId=` → that lesson has `questionCount 1` and `servableQuestionCount 0` (SQL path). Admin `GET /api/questions?lessonId=` → item `validationStatus "Approved"`, `isServable false` (in-memory path). |
| W1 | `essayValues.test.ts` — `describe('essayValues')` | `it('reads a stored essay into editor values')` | `readEssay({maxWords:200}, spec)` → `maxWords '200'`, points `'2'`, level points strings, `modelAnswers [{text}]`, missing description → `''`. |
| W2 | same | `it('reads an essay without a word limit as empty')` | `readEssay({}, spec).maxWords === ''`. |
| W3 | same | `it('builds the essay body and grading spec from values')` | `toEssayContent` → numbers for points; `body {}` when `maxWords ''`; `{maxWords:150}` otherwise. |
| W4 | same | `it('picks the first free criterion id')` | `nextCriterionId(['c1','c3'])` → `'c2'`; `[]` → `'c1'`. |
| W5 | same | `it('totals only whole criterion points')` | `[{points:'2'},{points:'x'},{points:'3'}]` → 5. |
| W6 | same | `it('counts words across spaces and new lines')` | `'  a  b\nc '` → 3; `''` → 0. |
| W7 | `schemas/questionEditorSchema.test.ts` (add) | `it('accepts a complete essay')` | Essay values (title, points 2, levels 0/2 with descriptions, a model answer `<p>x</p>`) → success. |
| W8 | same | `it('requires a criterion and a model answer for an essay')` | `criteria []`, `modelAnswers []` → issue paths `['criteria']`, `['modelAnswers']`. |
| W9 | same | `it('flags an essay criterion without a title or with invalid points')` | title `''`, points `'0'` → paths `criteria.0.title`, `criteria.0.points`. |
| W10 | same | `it('requires two levels on a full 0-to-points scale')` | One level → `criteria.0.levels` `levelsCount`; levels `1`/`2` with points 2 → `criteria.0.levels` `levelScale`. |
| W11 | same | `it('flags an essay level without a description or above the criterion points')` | Description `''`, points `'5'` for criterion points 2 → `criteria.0.levels.1.description`, `criteria.0.levels.1.points`. |
| W12 | same | `it('rejects an essay word limit outside 1 to 2000')` | `'0'`, `'2001'`, `'abc'` → `['maxWords']`; `''` → no maxWords issue. |
| W13 | same | `it('requires model answer text')` | `modelAnswers [{text:'<p></p>'}]` → `modelAnswers.0.text`. |
| W14 | `api/studentQuestion.test.ts` (add) | `it('carries the word limit of an essay and sends its text')` | `toStudentQuestion({...Essay, maxWords:'150'}).maxWords === 150`; `''` → null; `toAnswerPayload(essay, {...emptyAnswer(), text:'x'})` → `{text:'x'}`. |
| W15 | `api/answerKey.test.ts` (add) | `it('leaves the essay answer empty')` | `toAnswerKey(emptyQuestionValues('Essay'))` equals `emptyAnswer()`. |
| W16 | `quiz/api/quizItem.test.ts` (add) | `it('reads essay text and treats blank essays as empty')` | `fromAnswerPayload(essayQuestion, {text:'a'}).text === 'a'`; `isAnswerEmpty(essayQuestion, {...emptyAnswer(), text:'  '})` → true. |
| W17 | `quiz/api/correctAnswer.test.ts` (add) | `it('describes no correct answer or choice review for an essay')` | `describeCorrectAnswer(essay, {})` → null; `choiceReview(essay, {})` → undefined. |
| W18 | `components/QuestionView.test.tsx` (add) | `it('shows an essay box with a live word count against the limit')` | Type "one two" in textbox "Your essay" → text "2 of 150 words"; `onAnswerChange` receives the text. |
| W19 | `blueprints/api/blueprintValues.test.ts` (add) | `it('counts only the served question types')` | `countsFromValues(emptyBlueprintValues)` maps to the 5 types; no `Essay`. |
| W20 | `pages/NewEssayQuestion.test.tsx` — `describe('NewQuestionPage essay')` | `it('creates an essay question with its rubric and model answer')` | Select Type "Essay (v2)", type the stem, "Criterion 1 title", points 2, change level points to 0/2, fill both level descriptions and "Model answer 1", then Create. The request has `type 'Essay'`, `body {}`, `gradingSpec.criteria[0]` with `points 2` and 2 levels, `modelAnswers[0]` containing the text. "Question created." is shown. |
| W21 | same | `it('adds and removes criteria and levels within the limits')` | "Add criterion" → "Criterion 2" appears; "Remove criterion 1" is enabled with 2 and disabled with 1; "Add level to criterion 1" adds a third level; the "Remove level" buttons are disabled at 2 levels. |
| W22 | same | `it('shows rubric errors on submit')` | Empty criterion title and level descriptions → the required messages under those fields; no request sent. |
| W23 | same | `it('shows a server rubric error on the rubric')` | Create handler → 422 `{code:'QUESTION_RUBRIC_LEVEL_POINTS_INVALID', …}` in the problem shape used by the existing error tests → the en message text is visible in the rubric fieldset. |
| W24 | same | `it('explains that essays are not test-graded in the preview')` | After selecting Essay, the "Student preview" region has no "Try the answer" button, shows the `essayNotGradable` text, and shows the textbox "Your essay". |
| W25 | `pages/ValidationEssayQuestion.test.tsx` — `describe('ValidationQuestionPage essay')` | `it('shows the rubric, levels and model answers of an essay')` | Region "Grading rubric": "Total points: 2", heading "Definition", "0 points: Missing", "2 points: Complete"; "Model answer 1" contains the model text. |
| W26 | same | `it('renders the essay rubric right-to-left in Arabic')` | `lng: 'ar'` → root `dir="rtl"`, "معايير التصحيح" visible. |
| W27 | same | `it('has no axe violations')` | `axe(container)` → no violations after the rubric renders. |

No existing test is edited or deleted. The "(add)" rows append new `it`/`[Fact]` members to existing files; this is the full list of changes to existing test files, plus the `QuestionBuilder` and `ApiFactory` additions above. `AppDbContextTests` is untouched (no migration).

## Definition of done
- [ ] `QuestionType.Essay` exists. There is no migration, and `AppDbContextTests` has no new entry.
- [ ] `ServableQuestionSpecification.QuestionCondition` excludes `Essay`, and `ServedTypes` is the 5 v1 types. D1, D2 and I8 pass.
- [ ] `EssaySchemas.cs`, `EssayQuestionRules.cs` and `EssayRubricRules.cs` match the contracts, each file ≤ ~100 lines, no comments beyond the two constants' WHY.
- [ ] `QuestionSchemaRules` routes Essay to both `Validate` and `Normalize`.
- [ ] 14 error codes are in `ErrorCodes`, both resx files and both web `shared/i18n` files.
- [ ] 7 `ContentOptions` caps have code defaults and are in `appsettings.example.json` and `ApiFactory`.
- [ ] Grade-draft returns 422 `QUESTION_TYPE_NOT_GRADABLE` for essays (A26, I5).
- [ ] An `Essay` sheet is ignored by import. The template still has 5 type sheets (A27 plus the existing template tests green).
- [ ] Blueprint servable counts list the 5 served types (A28); the existing blueprint tests are green unchanged.
- [ ] The web editor authors essays: word limit, criteria, levels, model answers, a total-points line, add/remove within the limits, and client and server errors mapped (W7–W13, W20–W23).
- [ ] The web preview shows the essay box with a word count and hides "Try the answer" for essays (W18, W24).
- [ ] The validation page shows the rubric and model answers, renders RTL, and passes axe (W25–W27).
- [ ] The blueprint editor still shows exactly 5 types (`servedQuestionTypes`).
- [ ] All i18n keys are present in both `ar` and `en`. No hard-coded strings, no arbitrary Tailwind values, logical properties only.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated and committed (no drift).
- [ ] Postman has "Create essay question" after "Create question".
- [ ] Docs updated: `question-schemas.md`, `PRD.md` (§5.3, §17.1, §19 Q7), `exam-blueprints.md`, `question-import.md`, `claude-design-prompt.md` §4, `prototype.md`.
- [ ] `dotnet test api/ -c Release` is green with `appsettings.json` moved aside. Web `typecheck`, `lint`, `vitest run` and `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` pass.
- [ ] Every test in the Test plan exists by name and was mutation-checked: break the line and see the test fail.
