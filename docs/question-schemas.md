# Question schemas

The storage contract for a question's `Body` and `GradingSpec` (PRD §5.4, §6). The schema records (`Elmanhg.Domain.Questions.Schemas`), the per-type rules (`Elmanhg.Application.Questions.Shared.*QuestionRules`; drag-and-drop is `DragDropSchemas` checked by `DragDropQuestionRules`, `DiagramZoneRules` and `DiagramKeyRules`), the graders (`Elmanhg.Domain.Questions.Grading`), this document and the web question editor change together.

## Types

The wire names are the `QuestionType` enum values. v1 grading follows PRD §6.

- `Mcq`: one option. Exact match, no partial credit.
- `Multi`: a set of options. Set match; optional partial credit (correct − wrong) / total, floor 0.
- `TrueFalse`: a boolean. Exact match.
- `Fill`: one string per blank. Normalised match (PRD §6.2) against an accepted-answers list per blank; credit per blank.
- `Short`: a number or a string. Numeric within a tolerance (absolute or percent); text against an accepted list with normalisation.
- `Essay` (v2): plain-text answer (student input is #119), graded by the AI grader against the rubric and model answers ([essay-grading.md](essay-grading.md)); a blank answer is graded at once as Unanswered.
- `MathSteps` (v2): steps plus a final answer. The final answer is checked by the CAS (#122, [math-cas.md](math-cas.md)); with a steps weight above 0 the steps are graded against the model solution (#123, [math-step-grading.md](math-step-grading.md)).
- `DragDrop` (v2): a diagram with drop zones and draggable items (#125), answered on the student canvas and graded per item (#126).

## Body vs grading spec

- **Body** is what the student sees. It never holds an answer, so it can be sent to a student as is.
- **Grading spec** is the answer key. It stays on the server and is read only by the graders and by admins.

## Per-type shapes

Canonical JSON, exactly as stored. Keys are camelCase; enum values are camelCase strings.

**Mcq**

```json
{"options":[{"id":"a","text":"<p>3</p>"},{"id":"b","text":"<p>4</p>"}]}
```
```json
{"correctOptionId":"b"}
```

**Multi**

```json
{"options":[{"id":"a","text":"Force"},{"id":"b","text":"Velocity"},{"id":"c","text":"Mass"}]}
```
```json
{"correctOptionIds":["a","b"],"partialCredit":false}
```

`partialCredit` defaults to `false` (PRD §19 Q3 is open; false is the conservative choice).

**TrueFalse**

```json
{}
```
```json
{"correctAnswer":true}
```

**Fill**: the stem holds each blank as `[[id]]`, exactly once, for example `<p>v = [[1]] m/s</p>`.

```json
{"blanks":[{"id":"1"}]}
```
```json
{"blanks":[{"id":"1","acceptedAnswers":["20","٢٠"]}],"normalization":{"stripTashkeel":true,"stripTatweel":true,"unifyAlef":true,"unifyTaaMarbuta":true,"unifyAlefMaqsura":true,"convertDigits":true,"collapseWhitespace":true,"foldCase":true}}
```

**Short**, numeric answer:

```json
{"answerKind":"numeric"}
```
```json
{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}
```

`toleranceMode` is `absolute` (±tolerance) or `percent` (±tolerance % of the value). A spec has exactly one `tolerance` and one `toleranceMode`; absolute and percent cannot be combined.

**Short**, text answer:

```json
{"answerKind":"text"}
```
```json
{"acceptedAnswers":["ماء"],"normalization":{"stripTashkeel":true,"stripTatweel":true,"unifyAlef":true,"unifyTaaMarbuta":true,"unifyAlefMaqsura":true,"convertDigits":true,"collapseWhitespace":true,"foldCase":true}}
```

**Essay** (v2): the body holds the optional word limit shown to the student (`{}` without a limit).

```json
{"maxWords":200}
```
```json
{"criteria":[{"id":"c1","title":"Definition","points":2,"levels":[{"points":0,"description":"Missing"},{"points":1,"description":"Partial"},{"points":2,"description":"Complete"}]}],"modelAnswers":["<p>Inertia is resistance to change in motion.</p>"]}
```

- **Score**: each criterion's `points` is its weight. The grader (#118) awards each criterion one of its level points or any whole number between them; the question score is (Σ awarded ÷ Σ criterion points) × `maxScore`. `maxScore` stays independent (1 to `Content:QuestionMaxScoreMax`).
- **Level scale**: each criterion has 2 to `Content:QuestionRubricLevelsMaxCount` levels. Level points are distinct whole numbers from 0 to the criterion's points, and the set includes both 0 and the full points. Levels are stored in ascending points.
- Criterion `id` uses the id format below (the editor generates `c1`, `c2`, …); the grader reports a score per criterion id.
- `title`, `description` and level `description` are plain text, trimmed; a blank criterion `description` is omitted. `modelAnswers` are sanitised rich text, like the stem.
- The grading spec, with its rubric and model answers, is never sent to a student.

**MathSteps** (v2): the body is `{}` (nothing besides the stem is shown to the student). Canonical grading spec:

```json
{"acceptedAnswers":["x = 2","2"],"form":"equivalent","tolerance":0.01,"toleranceMode":"absolute"}
```

With step grading (#123):

```json
{"acceptedAnswers":["x = 2"],"form":"equivalent","modelSolution":["2x + 3 = 7","2x = 4","x = 2"],"stepsWeight":50}
```

- `acceptedAnswers`: one or more LaTeX final answers, trimmed; any mathematically equivalent answer is accepted. Notation, lists, `\pm` and equations are in [math-cas.md](math-cas.md).
- `form`: `equivalent` (the default, written explicitly when saved), `simplified`, `factored`, `expanded` or `exact`. The form rule applies to an answer that is already equivalent ([math-cas.md](math-cas.md), Forms).
- `tolerance` (≥ 0) and `toleranceMode` (`absolute` or `percent`) come as a pair, are optional, and are allowed only with `equivalent`. They apply when both values are constants; the bound is inclusive, as for a numeric Short answer.
- `modelSolution` (optional): the solution steps in order, one LaTeX string each, trimmed; at most `Content:QuestionModelSolutionStepsMaxCount` (20) steps of at most `Content:QuestionModelSolutionStepMaxLength` (500) characters, none blank. It is omitted from the canonical spec when empty.
- `stepsWeight` (optional): a whole number from 0 to 100, the percentage of the score given to the steps; a missing value means 0 and it is omitted when 0, so final-only specs stay byte-for-byte as before. A weight above 0 needs a model solution.
- Unknown fields are dropped. Accepted answers are not CAS-parsed when saved; an unparseable one is skipped when grading.
- The grading spec is revealed after answering, like every other type (the first accepted answer is shown).

**DragDrop** (v2): the body is the diagram the student sees: the image, the numbered drop zones and the item bank. The spec is the answer key.

```json
{"image":{"key":"question-diagrams/0b5c1f4e-3d2a-4c8e-9f1a-2b3c4d5e6f70/0123456789abcdef0123456789abcdef.png","width":800,"height":600,"alt":"Plant cell"},"zones":[{"id":"z1","x":10,"y":10,"width":20,"height":15,"capacity":2},{"id":"z2","x":50,"y":40,"width":30,"height":20.5,"capacity":2}],"items":[{"id":"i1","text":"Nucleus"},{"id":"i2","text":"Vacuole"},{"id":"i3","text":"Wall"},{"id":"i4","text":"Membrane"},{"id":"i5","text":"Engine"}]}
```
```json
{"zones":[{"zoneId":"z1","itemIds":["i1","i2"],"ordered":false},{"zoneId":"z2","itemIds":["i4","i3"],"ordered":true}]}
```

- **Image**: `key` is a storage key, never a URL. It must be a key the diagram upload writes: `question-diagrams/{lessonId}/{32 hex}.png|jpg|jpeg|webp` (lower case). `{lessonId}` must be the question's own lesson: create, update and resubmit reject a key uploaded to another lesson with 422 `QUESTION_DIAGRAM_IMAGE_INVALID`, so later orphan cleanup can reason per lesson. `POST /api/lessons/{lessonId}/diagram-images` (`Content.Manage`, multipart `file`) stores a PNG, JPEG or WebP (checked by extension, content type and magic bytes; size cap `Content:LessonImageMaxSizeInMb`) through the file storage and returns `{key, url}`. Because only a key is stored, a body can never point at another host (no hot-linked images or tracking pixels). The public URL is resolved when the question is read: `GET /api/questions/{id}` and the teacher validation detail add `image.url` (`FileStorage:PublicBaseUrl` + key) to the returned body. `url` is never stored; a `url` sent on create or update is dropped.
- `width` and `height` are the image's pixel size, 1 to `Content:QuestionDiagramImageDimensionMax`. The editor reads them in the browser; they only reserve the aspect ratio.
- `alt` is required plain text, trimmed, at most `Content:QuestionDiagramImageAltMaxLength`.
- **Zones** are rectangles in percent of the image: `x` from the image's left edge and `y` from its top. They are physical and never mirrored in right-to-left layouts (it is a picture). Every value is 0 to 100 with at most two decimals; `width` and `height` are at least `Content:QuestionDiagramZoneMinSizePercent`; the zone lies inside the image. Zones may touch but must not overlap. Values are compared in whole hundredths, so float sums never cross a bound.
- **Dropping on the student canvas** (client only; the server receives zone ids): the drop point becomes an unrounded percent of the canvas, and it lands in the zone with `x ≤ px < x + width` and `y ≤ py < y + height`, compared in hundredths (half-open, so two touching zones never share a point: a point on a shared edge belongs to the zone that starts there). An edge on the image border (`x + width = 100` or `y + height = 100`) is closed. A point in no zone drops nothing.
- `capacity` (1 to `Content:QuestionDiagramZoneCapacityMax`) is how many items the zone holds. The student sees it; it reveals nothing about which items go there.
- **Items** are plain text, trimmed, at most `Content:QuestionDiagramItemTextMaxLength`. Body order is the bank order.
- **Key**: the spec lists every body zone exactly once, in body order. An empty `itemIds` means the zone must stay empty. An item placed in no zone is a distractor: its correct place is the bank. At least one item is placed. `ordered: true` means the zone's items must be in the listed order and needs at least two items. Unordered `itemIds` are stored in body item order; ordered ones keep the author's order.
- Zone and item ids use the id format below (the editor generates `z1`, `z2`, … and `i1`, `i2`, …).
- The body carries no answer: zones have no label (the student sees 1, 2, …) and the bank order is independent of any zone.
- The diagram is rendered by React as SVG from this model. No SVG or HTML is ever stored, and every text is rendered as a text node.

`normalization` (PRD §6.2) holds the answer-normalisation rules of fill and text short answers. Each rule can be switched off per question; every rule defaults to `true`:

| Key | Effect when `true` | Default |
|---|---|---|
| `stripTashkeel` | remove tashkeel (U+064B–U+065F, U+0670, U+06D6–U+06ED) | `true` |
| `stripTatweel` | remove tatweel (U+0640) | `true` |
| `unifyAlef` | map أ إ آ ٱ (U+0623, U+0625, U+0622, U+0671) to ا (U+0627) | `true` |
| `unifyTaaMarbuta` | map ة (U+0629) to ه (U+0647) | `true` |
| `unifyAlefMaqsura` | map ى (U+0649) to ي (U+064A) | `true` |
| `convertDigits` | map Arabic-Indic (U+0660–U+0669) and Extended Arabic-Indic (U+06F0–U+06F9) digits to ASCII | `true` |
| `collapseWhitespace` | replace each inner run of whitespace with one space | `true` |
| `foldCase` | lower-case Latin letters (invariant culture) | `true` |

A missing `normalization` object means every rule is on; a partial object means the missing rules are on.

## Rules

A request whose `body` or `gradingSpec` breaks a rule gets `422` with the code below.

| Type | Rule | Code |
|---|---|---|
| all | `body` is a JSON object of the type's shape | `QUESTION_BODY_INVALID` |
| all | `gradingSpec` is a JSON object of the type's shape | `QUESTION_GRADING_SPEC_INVALID` |
| Mcq, Multi | 2 to `Content:QuestionOptionsMaxCount` options | `QUESTION_OPTIONS_COUNT_INVALID` |
| Mcq, Multi | every option id matches the id format | `QUESTION_OPTION_ID_INVALID` |
| Mcq, Multi | option ids are unique | `QUESTION_OPTION_ID_DUPLICATE` |
| Mcq, Multi | every option has text | `QUESTION_OPTION_TEXT_REQUIRED` |
| Mcq, Multi | option text is at most `Content:QuestionOptionTextMaxLength` | `QUESTION_OPTION_TEXT_TOO_LONG` |
| Mcq | `correctOptionId` is one of the option ids | `QUESTION_CORRECT_OPTION_INVALID` |
| Multi | `correctOptionIds` is not empty and every id is an option id | `QUESTION_CORRECT_OPTION_INVALID` |
| TrueFalse | `correctAnswer` is present | `QUESTION_CORRECT_ANSWER_REQUIRED` |
| Fill | 1 to `Content:QuestionBlanksMaxCount` blanks | `QUESTION_BLANKS_COUNT_INVALID` |
| Fill | every blank id matches the id format | `QUESTION_BLANK_ID_INVALID` |
| Fill | blank ids are unique | `QUESTION_BLANK_ID_DUPLICATE` |
| Fill | each blank's `[[id]]` appears in the stem exactly once | `QUESTION_BLANK_PLACEHOLDER_MISSING` |
| Fill | the spec lists every blank once, and only those blanks | `QUESTION_BLANK_ANSWERS_MISMATCH` |
| Fill, Short text | `normalization`, when present, is an object of booleans | `QUESTION_GRADING_SPEC_INVALID` |
| Fill, Short text | 1 to `Content:QuestionAcceptedAnswersMaxCount` accepted answers, none empty, each at most `Content:QuestionAnswerMaxLength` after trimming | `QUESTION_ACCEPTED_ANSWERS_INVALID` |
| Short | `answerKind` is present (`numeric` or `text`; any other value is `QUESTION_BODY_INVALID`) | `QUESTION_ANSWER_KIND_REQUIRED` |
| Short numeric | `value` is present | `QUESTION_NUMERIC_VALUE_REQUIRED` |
| Short numeric | `tolerance` is present and 0 or more, and `toleranceMode` is present | `QUESTION_TOLERANCE_INVALID` |
| Essay | `maxWords`, when present, is 1 to `Content:QuestionEssayMaxWordsMax` | `QUESTION_ESSAY_MAX_WORDS_INVALID` |
| Essay | 1 to `Content:QuestionRubricCriteriaMaxCount` criteria | `QUESTION_RUBRIC_CRITERIA_COUNT_INVALID` |
| Essay | every criterion id matches the id format | `QUESTION_RUBRIC_CRITERION_ID_INVALID` |
| Essay | criterion ids are unique | `QUESTION_RUBRIC_CRITERION_ID_DUPLICATE` |
| Essay | every criterion has a title | `QUESTION_RUBRIC_CRITERION_TITLE_REQUIRED` |
| Essay | a criterion title or description, or a level description, is at most `Content:QuestionRubricTextMaxLength` after trimming | `QUESTION_RUBRIC_TEXT_TOO_LONG` |
| Essay | every criterion's `points` is 1 to `Content:QuestionRubricPointsMax` | `QUESTION_RUBRIC_POINTS_INVALID` |
| Essay | every criterion has 2 to `Content:QuestionRubricLevelsMaxCount` levels | `QUESTION_RUBRIC_LEVELS_COUNT_INVALID` |
| Essay | every level has a description | `QUESTION_RUBRIC_LEVEL_DESCRIPTION_REQUIRED` |
| Essay | level points are distinct, from 0 to the criterion's points, and include 0 and the full points | `QUESTION_RUBRIC_LEVEL_POINTS_INVALID` |
| Essay | 1 to `Content:QuestionModelAnswersMaxCount` model answers | `QUESTION_MODEL_ANSWERS_COUNT_INVALID` |
| Essay | every model answer has text the AI grader can read: text, a formula, or an image with non-blank alt text, both as sent and after sanitising (an image with an empty alt is rejected) | `QUESTION_MODEL_ANSWER_REQUIRED` |
| Essay | each model answer is at most `Content:QuestionModelAnswerMaxLength` | `QUESTION_MODEL_ANSWER_TOO_LONG` |
| MathSteps | 1 to `Content:QuestionAcceptedAnswersMaxCount` accepted answers, none blank, each at most `Content:QuestionAnswerMaxLength` after trimming | `QUESTION_MATH_ANSWERS_INVALID` |
| MathSteps | `form`, when present, is a defined form (an unknown name does not read: `QUESTION_GRADING_SPEC_INVALID`) | `QUESTION_MATH_FORM_INVALID` |
| MathSteps | `tolerance` ≥ 0 and `toleranceMode` both present, or both absent | `QUESTION_MATH_TOLERANCE_INVALID` |
| MathSteps | a tolerance only with the `equivalent` form | `QUESTION_MATH_TOLERANCE_FORM_CONFLICT` |
| MathSteps | `modelSolution`, when present, has at most `Content:QuestionModelSolutionStepsMaxCount` steps, none blank, each at most `Content:QuestionModelSolutionStepMaxLength` after trimming | `QUESTION_MATH_MODEL_SOLUTION_INVALID` |
| MathSteps | `stepsWeight` from 0 to 100 | `QUESTION_MATH_STEPS_WEIGHT_INVALID` |
| MathSteps | a `stepsWeight` above 0 needs at least one model solution step | `QUESTION_MATH_MODEL_SOLUTION_REQUIRED` |
| DragDrop | `image` is present, `key` is a stored diagram key of the question's lesson (checked when saving), and `width`/`height` are 1 to `Content:QuestionDiagramImageDimensionMax` | `QUESTION_DIAGRAM_IMAGE_INVALID` |
| DragDrop | `image.alt` is not blank | `QUESTION_DIAGRAM_IMAGE_ALT_REQUIRED` |
| DragDrop | `image.alt` is at most `Content:QuestionDiagramImageAltMaxLength` after trimming | `QUESTION_DIAGRAM_IMAGE_ALT_TOO_LONG` |
| DragDrop | 1 to `Content:QuestionDiagramZonesMaxCount` zones | `QUESTION_DIAGRAM_ZONES_COUNT_INVALID` |
| DragDrop | every zone id matches the id format | `QUESTION_DIAGRAM_ZONE_ID_INVALID` |
| DragDrop | zone ids are unique | `QUESTION_DIAGRAM_ZONE_ID_DUPLICATE` |
| DragDrop | every zone is 0–100 with at most two decimals, at least `Content:QuestionDiagramZoneMinSizePercent` wide and high, and inside the image | `QUESTION_DIAGRAM_ZONE_BOUNDS_INVALID` |
| DragDrop | no two zones overlap (touching is allowed) | `QUESTION_DIAGRAM_ZONES_OVERLAP` |
| DragDrop | every zone's `capacity` is 1 to `Content:QuestionDiagramZoneCapacityMax` | `QUESTION_DIAGRAM_ZONE_CAPACITY_INVALID` |
| DragDrop | 1 to `Content:QuestionDiagramItemsMaxCount` items | `QUESTION_DIAGRAM_ITEMS_COUNT_INVALID` |
| DragDrop | every item id matches the id format | `QUESTION_DIAGRAM_ITEM_ID_INVALID` |
| DragDrop | item ids are unique | `QUESTION_DIAGRAM_ITEM_ID_DUPLICATE` |
| DragDrop | every item has text | `QUESTION_DIAGRAM_ITEM_TEXT_REQUIRED` |
| DragDrop | item text is at most `Content:QuestionDiagramItemTextMaxLength` after trimming | `QUESTION_DIAGRAM_ITEM_TEXT_TOO_LONG` |
| DragDrop | the key lists every zone once, and only those zones | `QUESTION_DIAGRAM_KEY_ZONES_MISMATCH` |
| DragDrop | the key places only known items, each at most once | `QUESTION_DIAGRAM_KEY_ITEM_INVALID` |
| DragDrop | no zone has more key items than its `capacity` | `QUESTION_DIAGRAM_ZONE_OVER_CAPACITY` |
| DragDrop | at least one item is placed | `QUESTION_DIAGRAM_KEY_EMPTY` |
| DragDrop | an `ordered` zone has at least two items | `QUESTION_DIAGRAM_ORDER_INVALID` |

A non-integer `maxWords`, criterion `points` or level `points` does not read as the type's shape and fails with `QUESTION_BODY_INVALID` / `QUESTION_GRADING_SPEC_INVALID`.

- **Id format** (option, blank, rubric criterion, diagram zone and item ids): `^[a-z0-9-]{1,20}$`. Ids are referenced from grading specs and `[[id]]` placeholders, so they stay short, lowercase ASCII.
- **Fields** outside the body and spec (stem, explanation, difficulty, tags, max score) are checked by `QuestionFieldsValidator`. Caps: `Content:QuestionStemMaxLength`, `Content:QuestionExplanationMaxLength`, `Content:QuestionTagsMaxCount`, `Content:QuestionTagMaxLength`, `Content:QuestionMaxScoreMax` (max score is a whole number from 1).

## Canonical storage

- `Body` and `GradingSpec` are `jsonb` columns. The server re-serialises the typed records, so unknown properties are dropped and defaults are written out (`partialCredit`, all eight `normalization` rules). Numeric short specs keep only `value`, `tolerance` and `toleranceMode`; text short specs keep only `acceptedAnswers` and `normalization`. The true/false body is always `{}`.
- **Migration** (`AddAnswerNormalizationRules`): the earlier single `unifyLetterVariants` flag was replaced by `normalization`. Every stored `Questions.GradingSpec` and every `QuestionRevisions.Snapshot` `gradingSpec` that held the flag was rewritten: the three letter rules (`unifyAlef`, `unifyTaaMarbuta`, `unifyAlefMaqsura`) take the old value (missing meant `true`), the other five rules are `true`, and the old key is removed. Numeric short specs never had the flag and are unchanged. Audit diffs (`AuditLogs`) are append-only history and keep the old key. The migration's `Down` reverses the rewrite (`unifyLetterVariants` takes `unifyAlef`).
- Choice option `text` is sanitised rich text, like the stem and explanation (`docs/rich-text.md`).
- Accepted answers are plain text, trimmed. They are normalised at grading time (PRD §6.2), never at save time.
- Fill spec entries follow the body's blank order.
- DragDrop: keys are written in record order, `ordered` is always written, `alt` and item texts are trimmed, unknown properties (including a sent `url`) are dropped, and numbers are stored as sent (`12.5`, never `12.50`). The key follows the body's zone order.
- Two JSON values are equal when they are semantically equal (`JsonNode.DeepEquals`), never by string comparison: PostgreSQL re-formats `jsonb` text on read.

## Versioning

- **Content** is the stem, body, grading spec, explanation and max score. **Metadata** is difficulty, objective link and tags. The type never changes: changing it returns `400 QUESTION_TYPE_IMMUTABLE`.
- A question is created at version 1 with a revision snapshot of version 1.
- Every content edit, in any status, increments `version` and appends a `QuestionRevision` holding the snapshot of the new version. A content edit on an Approved question also returns it to Pending and clears `validatedBy` and `validatedAt`. A Rejected question stays Rejected on a plain edit (`PUT /api/questions/{id}`); `PUT /api/questions/{id}/resubmit` applies the same edit and returns it to Pending (see Validation status).
- A metadata-only edit changes neither the status nor the version, and adds no revision. An edit that changes nothing is a no-op.
- The snapshot (`QuestionRevisionSnapshot`) is jsonb: `{"type","stem","body","gradingSpec","explanation","maxScore"}`, so an attempt's `question_version` always resolves to an exact snapshot.
- There is no concurrency token yet. An approval must name the version the teacher reviewed, so an edit made while the teacher was reviewing cannot be approved unseen.
- `submittedAt` is when the current version entered review: it is set at creation (and import), reset on every content edit (next to the version bump) and on resubmit, and kept by a metadata-only edit and by decisions. The teacher queue orders by it (oldest first) and its age filter reads it.

## Validation status

- Every question is created Pending. No request, command or endpoint carries a status.
- The only way to Approved is `Question.Approve(TeacherSubject, reviewedVersion, difficulty?)`: it needs an unretired question (`QUESTION_RETIRED`), a Pending question (`QUESTION_NOT_PENDING`), a live teacher assignment for the question's subject (`QUESTION_VALIDATOR_NOT_ASSIGNED`) and `reviewedVersion` equal to the current version (`409 QUESTION_VERSION_CHANGED`), checked in that order. A `TeacherSubject` can only be created for a teacher, so an admin cannot approve (PRD §16, §17 rule 3). An optional difficulty is applied at approval when it differs (a metadata change: no version bump, no revision).
- The only way to Rejected is `Question.Reject(TeacherSubject, reviewedVersion, reason)`. It checks the same rules as approval, in the same order (`QUESTION_RETIRED`, then `QUESTION_NOT_PENDING`, then `QUESTION_VALIDATOR_NOT_ASSIGNED`, then `QUESTION_VERSION_CHANGED`), and then needs a non-blank reason (`QUESTION_REJECTION_REASON_REQUIRED`). It stores the trimmed reason in `rejectionReason`, and `validatedBy`/`validatedAt` record the teacher who decided.
- Every approve and reject appends a `QuestionDecision` (append-only, never edited): `version`, `outcome` (`Approved`/`Rejected`), `reason`, `difficulty` (after the decision), `difficultyChangedFrom` (null when unchanged), `decidedBy`, `decidedAt`, `submittedAt` (the question's `submittedAt` at decision time, i.e. when the decided version entered review; the median time-to-decision in [docs/dashboard.md](dashboard.md) is `decidedAt − submittedAt`). It keeps prior rejections visible after a resubmit clears `rejectionReason`. The migration backfilled one decision per question that was already Approved or Rejected. The `AddDashboardMetrics` migration backfilled `submittedAt` on existing decisions from the decided version's `QuestionRevision.editedAt`, falling back to `decidedAt` when no revision exists.
- Teacher endpoints (`Questions.Validate`, all under `api/validation-queue`): `POST review-sessions` (start a review session), `GET filters` (assigned subjects, units, lessons), `GET` (the Pending, unretired questions of the caller's subjects; filters unit, lesson, type, difficulty, `minAgeDays`; oldest `submittedAt` first), `GET questions/{id}` (detail with revisions and decisions; `403 SUBJECT_OUT_OF_SCOPE` outside the caller's subjects), `POST review-sessions/{sessionId}/openings/{id}` (records that the question was opened at its current version), `POST questions/{id}/approve` (`{version, difficulty?}`), `POST questions/{id}/reject` (`{version, reason}`), and `POST bulk-approve` (`{reviewSessionId, questionIds}`). Bulk approve is all-or-nothing: the session must belong to the caller (`404 REVIEW_SESSION_NOT_FOUND`) and be unexpired (`400 REVIEW_SESSION_EXPIRED`), and each question must have been opened in it at its current version (`400 QUESTION_NOT_OPENED_IN_SESSION`).
- `PUT /api/questions/{id}/resubmit` (`Question.Resubmit`) needs a Rejected question (`400 QUESTION_NOT_REJECTED` otherwise). It applies the edit exactly like `PUT /api/questions/{id}` (a content change bumps the version and adds a revision; the type still cannot change), then returns the question to Pending and clears `rejectionReason`, `validatedBy` and `validatedAt`. It does not require a content change. The audit entry `Question.Resubmit` keeps the cleared reason in its diff.

## Retirement

- `POST /api/questions/{id}/retire` (Admin, `ContentManage`; audited as `Question.Retire`) stamps `retiredAt` and raises `QuestionRetired`. Any status can be retired (Pending, Approved or Rejected); the status, version and revisions are unchanged, and `updatedBy` plus the audit row record who retired it.
- Retirement is terminal. There is no un-retire. Retiring again returns `400 QUESTION_ALREADY_RETIRED`. Edit, resubmit, approve and reject on a retired question return `400 QUESTION_RETIRED` before any other check.
- `retiredAt` (nullable) is returned by `GET /api/questions` (each item) and `GET /api/questions/{id}`. Retired questions stay in the admin bank and every historical attempt keeps pointing at them.

## Servable

- Servable = Approved ∧ lesson Published ∧ not retired (PRD §5.3, §17 rule 1). It is derived on every read and never stored.
- `ServableQuestionSpecification.ServedTypes` lists every type (the five v1 types `Mcq`, `Multi`, `TrueFalse`, `Fill`, `Short`, plus `Essay`, #119, `MathSteps`, #122, and `DragDrop`, #126) for per-type listings such as blueprint servable counts.
- `ServableQuestionSpecification` (`Elmanhg.Domain/Questions`) is the only definition. `WhereServable(questions, lessons)` composes the rule into SQL; `IsSatisfiedBy(question, lesson)` runs compiled copies of the same expressions in memory.
- Every serving query (quizzes, exams, blueprints, anything a student is shown) must filter through `WhereServable`. Admin reads never filter by it: `GET /api/questions` lists every status and annotates each item with `isServable`, and `GET /api/lessons` returns `servableQuestionCount` next to `questionCount`.
- `GET /api/questions/servable-count` is anonymous and returns `{"count": n}`, the platform-wide total shown on the landing page. It is cached in `IMemoryCache` under `questions:servable-count`.
- The cache entry is removed on every event that can change the total: `LessonPublished`, `LessonUnpublished`, `LessonArchived`, `QuestionApproved`, `QuestionRejected`, `QuestionReturnedToPending` (a content edit on an Approved question) and `QuestionRetired`. Creating, importing, metadata-only edits and resubmitting (Rejected → Pending) cannot change the total and do not invalidate.
- The entry also expires after `Content:ServableCountCacheSeconds` (default 60). Domain events are published before the transaction commits, so a read that lands between the invalidation and the commit can re-cache the old value; the expiry bounds that staleness.
- The response carries `Cache-Control: public, max-age=<Content:ServableCountCacheSeconds>`, so browsers and shared caches may also keep a count for up to that window after an invalidation ([docs/performance.md](performance.md) §4).

## Answer shapes

A student's answer (and the `answer` of `POST /api/questions/grade-draft`) is a JSON object per type. A missing field means "no answer" and scores 0. An answer that is not a JSON object, or does not read as the type's shape (for example a number where a string is expected), gets `422 QUESTION_ANSWER_INVALID`.

**Mcq**

```json
{"optionId":"b"}
```

**Multi**

```json
{"optionIds":["a","c"]}
```

**TrueFalse**

```json
{"value":true}
```

**Fill**

```json
{"blanks":[{"id":"1","text":"20"}]}
```

**Short** (numeric and text alike: the student types a string)

```json
{"text":"9.8"}
```

**Essay** (plain text; trimmed when canonicalised; at most `Content:QuestionEssayAnswerMaxLength` characters on `grade-draft`, the quiz answer and the exam save, else `422 QUESTION_ESSAY_ANSWER_TOO_LONG`; a missing or non-string `text` gets `422 QUESTION_ANSWER_INVALID`)

```json
{"text":"القصور الذاتي هو ممانعة الجسم لتغيير حالته الحركية."}
```

**MathSteps** (`steps` optional and only strings, `finalAnswer` an optional string; a null step or a non-string gets `422 QUESTION_ANSWER_INVALID`). Canonicalising trims every step, drops blank steps and trims the final answer. At most `Sessions:MathStepsMaxCount` steps (20), `Sessions:MathStepMaxLength` characters per step (500) and `Sessions:MathFinalAnswerMaxLength` characters in the final answer (200), else `422 ATTEMPT_ANSWER_TOO_LONG` (quiz answer, exam save and `grade-draft`).

```json
{"steps":["2x = 4"],"finalAnswer":"x = 2"}
```

**DragDrop** (`placements` optional; each placement has a string `zoneId` and optional `itemIds` of strings, in the order they sit in the zone; an item in no placement is in the bank). A null placement, a missing or non-string `zoneId`, or a null item id gets `422 QUESTION_ANSWER_INVALID`. Canonicalising drops placements with no items, drops unknown properties and keeps the order. The raw answer is at most `Sessions:DragDropAnswerMaxLength` characters (4000), with at most `Sessions:DragDropPlacementsMaxCount` placements (20) and `Sessions:DragDropPlacedItemsMaxCount` item ids across all placements (30), else `422 ATTEMPT_ANSWER_TOO_LONG` (quiz answer, exam save and `grade-draft`).

```json
{"placements":[{"zoneId":"z1","itemIds":["i2","i1"]},{"zoneId":"z2","itemIds":["i4","i3"]}]}
```

## Grading

The graders live in `Elmanhg.Domain.Questions.Grading` and are pure functions of the stored grading spec, the max score and the answer. `POST /api/questions/grade-draft` (admin, `Content.Manage`) validates an unsaved draft with the same rules as create and update, canonicalises it, and grades the `answer` with the same graders; the answer's raw JSON is capped per type exactly as on the quiz answer (`Sessions:AnswerMaxLength`, or `EssayAnswerMaxLength`, `MathStepsAnswerMaxLength` or `DragDropAnswerMaxLength` for those types; [sessions.md](sessions.md)), else `422 ATTEMPT_ANSWER_TOO_LONG`. It saves nothing and is not audited. Attempts reuse the same graders.

- **Mcq, TrueFalse**: exact match (1 or 0). A missing `optionId` or `value` is unanswered and scores 0.
- **Multi**: the rule uses three counts:
  - `right`: the distinct selected ids that are correct.
  - `wrong`: the distinct selected ids that are not correct. An id that is not an option counts as wrong. Ids compare exactly, so case matters. A repeated id counts once. A null id is ignored.
  - `total`: the number of correct options.

  Without `partialCredit` (the default), the score is 1 when `right = total` and `wrong = 0`, else 0. With `partialCredit`, the score is `max(0, (right − wrong) / total)`. For example, with 3 correct options, choosing all three plus one wrong option scores 2/3. With no selection, the answer is unanswered and scores 0 in both modes.
- **Fill**: `hits / blanks`. Each blank is compared only with its own accepted answers; it hits when its normalised answer equals any of them. A blank the answer leaves out, or whose text normalises to empty, is a miss. A response whose id is not a blank is ignored; a repeated id uses its first response.
- **Short numeric** (the spec has `value`): the answer is parsed as a number and is correct when `value − allowed ≤ answer ≤ value + allowed` (both bounds inclusive). `allowed` is `tolerance` (`absolute`) or `|value| × tolerance / 100` (`percent`). A negative `value` gets the same band as its magnitude, and a `value` of 0 with `percent` accepts only 0. A missing or negative tolerance counts as 0 and a missing mode as `absolute` (unreachable after validation). Only the spec takes part in the arithmetic; when a bound would pass the decimal range (±79228162514264337593543950335) it is clamped to that limit, so grading never fails.
- **Short text**: the normalised answer must equal one of the normalised accepted answers.
- **Essay**: the AI grader awards points per rubric criterion ([essay-grading.md](essay-grading.md)); `QuestionGrader.GradeEssay` turns them into `Σ awarded ÷ Σ criterion points`, scaled by the max score like every other type. The model's own total is never used. `grade-draft` grades essays synchronously through the AI grader (`503 ESSAY_GRADING_UNAVAILABLE` when it fails) and adds an `essay` detail (criteria, justification, confidence, model, prompt version, cost); a blank essay scores 0 (Unanswered) without calling the grader.
- **MathSteps**: the final answer is checked through `AnswerGrader` and the AI service's CAS ([math-cas.md](math-cas.md)); every other type grades locally without the AI service. A blank final answer is Unanswered (0) without calling the AI service, even with steps. `QuestionGrader.Grade` does not grade MathSteps. With a steps weight w and step points from the step grader, `QuestionGrader.GradeMathStepsCombined` scores ((100 − w) × F + w × S) ÷ 100, where F is 1 for `equivalent` (else 0) and S is the step points ÷ (2 × model steps); with w = 0 it is the final-only grade (`equivalent` 1, every other verdict 0). When step grading is needed, or the verdict is `unchecked` (the AI service cannot be reached), `AnswerGrader.DecideAsync` defers the answer to a background `MathStepGrade` instead of an attempt; nothing is lost and no submit fails ([math-step-grading.md](math-step-grading.md)).
- **DragDrop**: graded per item (`DragDropGrader`). The credit units are the keyed items `K` (the items the key places in a zone).
  - The answer is read tolerantly: a placement whose zone is not in the key is ignored; a repeated zone uses its first entry; an item id repeated across placements counts only where it first appears; a null id is skipped.
  - `right`: keyed items that sit in their key zone and, when that zone is `ordered`, at their key position (the index in the zone's resolved list). An unordered zone accepts any order.
  - `wrong`: placed ids the key places nowhere (distractors or unknown ids). A keyed item in the wrong zone or position is simply not right; it costs nothing more.
  - The score is 1 when `right = K` and `wrong = 0`, else `max(0, (right − wrong) / K)`. An answer that places no item in a known zone is unanswered and scores 0.
  - Capacity is not checked by the grader (the canvas enforces it); an over-full zone cannot earn more, because each keyed item counts once.
  - Example: key z1 {i1, i2} unordered, z2 [i4, i3] ordered; the answer z1 [i2, i1], z2 [i3, i4] has `right` 2 (both z1 items), `wrong` 0, and scores 2/4.
- An answer that normalises to empty never matches.
- **Normalisation** (PRD §6.2): the student answer and every accepted answer go through the same steps. Always, in this order: drop unpaired UTF-16 surrogates and the noncharacter U+FFFE (both make NFC fail), and invisible marks (U+061C, U+200B–U+200F, U+202A–U+202E, U+2060, U+2066–U+2069, U+FEFF), so none of them can split a letter from its mark; Unicode NFC (so a decomposed hamza or madda, such as ا + U+0654, becomes أ); map ، (U+060C) to `,` and ی (U+06CC) to ي (U+064A). Then the question's `normalization` rules (see **Fill** and **Short** above), each applied only when on. Finally the answer is always trimmed. With `collapseWhitespace` off, inner whitespace is kept verbatim (tabs and runs included); the ends are still trimmed. Hamza seats (ئ ؤ ء) are never unified.
- **Numeric parsing**: numeric answers ignore the question's `normalization` rules and use a fixed profile: every rule on except the three letter rules. After normalisation, `٬` (U+066C, the Arabic thousands separator) is removed, then `٫` (U+066B) and `,` become `.`, and `−` (U+2212) becomes `-`; the rest must be a plain decimal number: an optional leading sign, digits and one decimal point (invariant culture). Exponents such as `9.8e0`, inner whitespace, thousands separators other than `٬`, and anything else (such as a trailing unit) do not parse and score 0. `,` always means a decimal point, so `1,000` reads as 1.
- **Result**: the normalised score is in [0, 1]. The outcome is `Correct` (≥ 1), `Partial` (> 0) or `Incorrect`. `score = round(normalised × maxScore, 2)` and `normalisedScore = round(normalised, 4)`, both rounding half away from zero.
- **Feedback**: every grade carries an optional feedback line, returned as `feedback`. It is localised to the request language (`Accept-Language`, Arabic by default) and is `null` when there is nothing to add.
  - An unanswered answer returns «لم تتم الإجابة عن السؤال.» / "No answer was given." This rule is checked first; the rules below apply only to an answer that is not unanswered. An answer is unanswered when: Mcq `optionId` or TrueFalse `value` is missing; Multi has no non-null id; every Fill blank is missing or normalises to empty; a text Short answer normalises to empty; a numeric Short answer normalises to empty under the numeric profile; a DragDrop answer places no item in a known zone.
  - A Multi answer that is not exactly the correct set returns «الاختيارات الصحيحة: {right} من {total}، والخاطئة: {wrong}.» / "Correct choices: {right} of {total}; wrong choices: {wrong}." This applies in both partial-credit modes.
  - A Fill answer with two or more blanks that is not fully correct returns «الفراغات الصحيحة: {right} من {total}.» / "Correct blanks: {right} of {total}." A single-blank Fill returns `null`.
  - A numeric Short answer that does not parse as a number returns «اكتب الإجابة رقمًا فقط، بدون وحدات.» / "Write the answer as a plain number, without units." A number outside the tolerance returns `null`: a direction hint would reveal part of the answer.
  - A MathSteps final answer returns one of four lines: «صُحّحت الإجابة النهائية فقط.» / "Only the final answer was graded." (equivalent or not equivalent, on a final-only question); «اكتب الإجابة النهائية بالصورة التي يطلبها السؤال.» / "Write the final answer in the form the question asks for." (wrong form; it tells the student to change the form without revealing the answer); «تعذّرت قراءة الإجابة النهائية. اكتبها بالرموز الرياضية، مثل x = 2.» / "The final answer could not be read. …" (unreadable); «تعذّر التحقق من الإجابة النهائية آليًا، وسيراجعها معلمك.» / "The final answer could not be checked automatically. Your teacher will review it." (unchecked; `grade-draft` and legacy attempts only). A step-graded answer returns «خطوات صحيحة كاملة: {right} من {total}.» / "Fully correct steps: {right} of {total}." (`mathStepTally`).
  - A DragDrop answer that is not fully correct returns «العناصر في أماكنها الصحيحة: {right} من {total}، والعناصر المشتِّتة الموضوعة: {wrong}.» / "Items in the right place: {right} of {total}; distractors placed: {wrong}.". It never says which items are wrong.
  - Every other case returns `null`.

  The feedback never contains the verdict, the correct answer or the explanation.

## Changing a schema

A new field or rule changes the schema record, its rules, this document and the web editor in the same change. The spreadsheet import (`docs/question-import.md`) builds the same shapes from columns; a schema change updates its columns, parser and template in the same change. Essay, MathSteps and DragDrop are not importable (PRD §10.1). Rows already stored in the old shape need a data migration that rewrites them to the new canonical shape.
