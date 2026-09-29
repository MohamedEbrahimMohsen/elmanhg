# Question schemas

The storage contract for a question's `Body` and `GradingSpec` (PRD §5.4, §6). The schema records (`Elmanhg.Domain.Questions.Schemas`), the per-type rules (`Elmanhg.Application.Questions.Shared.*QuestionRules`), the graders (`Elmanhg.Domain.Questions.Grading`), this document and the web question editor change together.

## Types

The wire names are the `QuestionType` enum values. v1 grading follows PRD §6.

- `Mcq`: one option. Exact match, no partial credit.
- `Multi`: a set of options. Set match; optional partial credit (correct − wrong) / total, floor 0.
- `TrueFalse`: a boolean. Exact match.
- `Fill`: one string per blank. Normalised match (PRD §6.2) against an accepted-answers list per blank; credit per blank.
- `Short`: a number or a string. Numeric within a tolerance (absolute or percent); text against an accepted list with normalisation.
- `Essay` (v2): rich-text answer (#119), graded by the AI grader against the rubric and model answers (#118). Not servable until #119.

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
| Essay | no model answer is blank | `QUESTION_MODEL_ANSWER_REQUIRED` |
| Essay | each model answer is at most `Content:QuestionModelAnswerMaxLength` | `QUESTION_MODEL_ANSWER_TOO_LONG` |

A non-integer `maxWords`, criterion `points` or level `points` does not read as the type's shape and fails with `QUESTION_BODY_INVALID` / `QUESTION_GRADING_SPEC_INVALID`.

- **Id format** (option, blank and rubric criterion ids): `^[a-z0-9-]{1,20}$`. Ids are referenced from grading specs and `[[id]]` placeholders, so they stay short, lowercase ASCII.
- **Fields** outside the body and spec (stem, explanation, difficulty, tags, max score) are checked by `QuestionFieldsValidator`. Caps: `Content:QuestionStemMaxLength`, `Content:QuestionExplanationMaxLength`, `Content:QuestionTagsMaxCount`, `Content:QuestionTagMaxLength`, `Content:QuestionMaxScoreMax` (max score is a whole number from 1).

## Canonical storage

- `Body` and `GradingSpec` are `jsonb` columns. The server re-serialises the typed records, so unknown properties are dropped and defaults are written out (`partialCredit`, all eight `normalization` rules). Numeric short specs keep only `value`, `tolerance` and `toleranceMode`; text short specs keep only `acceptedAnswers` and `normalization`. The true/false body is always `{}`.
- **Migration** (`AddAnswerNormalizationRules`): the earlier single `unifyLetterVariants` flag was replaced by `normalization`. Every stored `Questions.GradingSpec` and every `QuestionRevisions.Snapshot` `gradingSpec` that held the flag was rewritten: the three letter rules (`unifyAlef`, `unifyTaaMarbuta`, `unifyAlefMaqsura`) take the old value (missing meant `true`), the other five rules are `true`, and the old key is removed. Numeric short specs never had the flag and are unchanged. Audit diffs (`AuditLogs`) are append-only history and keep the old key. The migration's `Down` reverses the rewrite (`unifyLetterVariants` takes `unifyAlef`).
- Choice option `text` is sanitised rich text, like the stem and explanation (`docs/rich-text.md`).
- Accepted answers are plain text, trimmed. They are normalised at grading time (PRD §6.2), never at save time.
- Fill spec entries follow the body's blank order.
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
- Every approve and reject appends a `QuestionDecision` (append-only, never edited): `version`, `outcome` (`Approved`/`Rejected`), `reason`, `difficulty` (after the decision), `difficultyChangedFrom` (null when unchanged), `decidedBy`, `decidedAt`. It keeps prior rejections visible after a resubmit clears `rejectionReason`. The migration backfilled one decision per question that was already Approved or Rejected.
- Teacher endpoints (`Questions.Validate`, all under `api/validation-queue`): `POST review-sessions` (start a review session), `GET filters` (assigned subjects, units, lessons), `GET` (the Pending, unretired questions of the caller's subjects; filters unit, lesson, type, difficulty, `minAgeDays`; oldest `submittedAt` first), `GET questions/{id}` (detail with revisions and decisions; `403 SUBJECT_OUT_OF_SCOPE` outside the caller's subjects), `POST review-sessions/{sessionId}/openings/{id}` (records that the question was opened at its current version), `POST questions/{id}/approve` (`{version, difficulty?}`), `POST questions/{id}/reject` (`{version, reason}`), and `POST bulk-approve` (`{reviewSessionId, questionIds}`). Bulk approve is all-or-nothing: the session must belong to the caller (`404 REVIEW_SESSION_NOT_FOUND`) and be unexpired (`400 REVIEW_SESSION_EXPIRED`), and each question must have been opened in it at its current version (`400 QUESTION_NOT_OPENED_IN_SESSION`).
- `PUT /api/questions/{id}/resubmit` (`Question.Resubmit`) needs a Rejected question (`400 QUESTION_NOT_REJECTED` otherwise). It applies the edit exactly like `PUT /api/questions/{id}` (a content change bumps the version and adds a revision; the type still cannot change), then returns the question to Pending and clears `rejectionReason`, `validatedBy` and `validatedAt`. It does not require a content change. The audit entry `Question.Resubmit` keeps the cleared reason in its diff.

## Retirement

- `POST /api/questions/{id}/retire` (Admin, `ContentManage`; audited as `Question.Retire`) stamps `retiredAt` and raises `QuestionRetired`. Any status can be retired (Pending, Approved or Rejected); the status, version and revisions are unchanged, and `updatedBy` plus the audit row record who retired it.
- Retirement is terminal. There is no un-retire. Retiring again returns `400 QUESTION_ALREADY_RETIRED`. Edit, resubmit, approve and reject on a retired question return `400 QUESTION_RETIRED` before any other check.
- `retiredAt` (nullable) is returned by `GET /api/questions` (each item) and `GET /api/questions/{id}`. Retired questions stay in the admin bank and every historical attempt keeps pointing at them.

## Servable

- Servable = Approved ∧ lesson Published ∧ not retired ∧ not an essay (essays are excluded until student essay input, #119) (PRD §5.3, §17 rule 1). It is derived on every read and never stored.
- `ServableQuestionSpecification.ServedTypes` lists the five served v1 types (`Mcq`, `Multi`, `TrueFalse`, `Fill`, `Short`) for per-type listings such as blueprint servable counts. #119 adds `Essay` back to both.
- `ServableQuestionSpecification` (`Elmanhg.Domain/Questions`) is the only definition. `WhereServable(questions, lessons)` composes the rule into SQL; `IsSatisfiedBy(question, lesson)` runs compiled copies of the same expressions in memory.
- Every serving query (quizzes, exams, blueprints, anything a student is shown) must filter through `WhereServable`. Admin reads never filter by it: `GET /api/questions` lists every status and annotates each item with `isServable`, and `GET /api/lessons` returns `servableQuestionCount` next to `questionCount`.
- `GET /api/questions/servable-count` is anonymous and returns `{"count": n}`, the platform-wide total shown on the landing page. It is cached in `IMemoryCache` under `questions:servable-count`.
- The cache entry is removed on every event that can change the total: `LessonPublished`, `LessonUnpublished`, `LessonArchived`, `QuestionApproved`, `QuestionRejected`, `QuestionReturnedToPending` (a content edit on an Approved question) and `QuestionRetired`. Creating, importing, metadata-only edits and resubmitting (Rejected → Pending) cannot change the total and do not invalidate.
- The entry also expires after `Content:ServableCountCacheSeconds` (default 60). Domain events are published before the transaction commits, so a read that lands between the invalidation and the commit can re-cache the old value; the expiry bounds that staleness.

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

**Essay**: defined by #119.

## Grading

The graders live in `Elmanhg.Domain.Questions.Grading` and are pure functions of the stored grading spec, the max score and the answer. `POST /api/questions/grade-draft` (admin, `Content.Manage`) validates an unsaved draft with the same rules as create and update, canonicalises it, and grades the `answer` with the same graders; it saves nothing and is not audited. Attempts reuse the same graders.

- **Mcq, TrueFalse**: exact match (1 or 0). A missing `optionId` or `value` is unanswered and scores 0.
- **Multi**: the rule uses three counts:
  - `right`: the distinct selected ids that are correct.
  - `wrong`: the distinct selected ids that are not correct. An id that is not an option counts as wrong. Ids compare exactly, so case matters. A repeated id counts once. A null id is ignored.
  - `total`: the number of correct options.

  Without `partialCredit` (the default), the score is 1 when `right = total` and `wrong = 0`, else 0. With `partialCredit`, the score is `max(0, (right − wrong) / total)`. For example, with 3 correct options, choosing all three plus one wrong option scores 2/3. With no selection, the answer is unanswered and scores 0 in both modes.
- **Fill**: `hits / blanks`. Each blank is compared only with its own accepted answers; it hits when its normalised answer equals any of them. A blank the answer leaves out, or whose text normalises to empty, is a miss. A response whose id is not a blank is ignored; a repeated id uses its first response.
- **Short numeric** (the spec has `value`): the answer is parsed as a number and is correct when `value − allowed ≤ answer ≤ value + allowed` (both bounds inclusive). `allowed` is `tolerance` (`absolute`) or `|value| × tolerance / 100` (`percent`). A negative `value` gets the same band as its magnitude, and a `value` of 0 with `percent` accepts only 0. A missing or negative tolerance counts as 0 and a missing mode as `absolute` (unreachable after validation). Only the spec takes part in the arithmetic; when a bound would pass the decimal range (±79228162514264337593543950335) it is clamped to that limit, so grading never fails.
- **Short text**: the normalised answer must equal one of the normalised accepted answers.
- **Essay** is not graded by these graders; `grade-draft` returns `422 QUESTION_TYPE_NOT_GRADABLE`.
- An answer that normalises to empty never matches.
- **Normalisation** (PRD §6.2): the student answer and every accepted answer go through the same steps. Always, in this order: drop unpaired UTF-16 surrogates and the noncharacter U+FFFE (both make NFC fail), and invisible marks (U+061C, U+200B–U+200F, U+202A–U+202E, U+2060, U+2066–U+2069, U+FEFF), so none of them can split a letter from its mark; Unicode NFC (so a decomposed hamza or madda, such as ا + U+0654, becomes أ); map ، (U+060C) to `,` and ی (U+06CC) to ي (U+064A). Then the question's `normalization` rules (see **Fill** and **Short** above), each applied only when on. Finally the answer is always trimmed. With `collapseWhitespace` off, inner whitespace is kept verbatim (tabs and runs included); the ends are still trimmed. Hamza seats (ئ ؤ ء) are never unified.
- **Numeric parsing**: numeric answers ignore the question's `normalization` rules and use a fixed profile: every rule on except the three letter rules. After normalisation, `٬` (U+066C, the Arabic thousands separator) is removed, then `٫` (U+066B) and `,` become `.`, and `−` (U+2212) becomes `-`; the rest must be a plain decimal number: an optional leading sign, digits and one decimal point (invariant culture). Exponents such as `9.8e0`, inner whitespace, thousands separators other than `٬`, and anything else (such as a trailing unit) do not parse and score 0. `,` always means a decimal point, so `1,000` reads as 1.
- **Result**: the normalised score is in [0, 1]. The outcome is `Correct` (≥ 1), `Partial` (> 0) or `Incorrect`. `score = round(normalised × maxScore, 2)` and `normalisedScore = round(normalised, 4)`, both rounding half away from zero.
- **Feedback**: every grade carries an optional feedback line, returned as `feedback`. It is localised to the request language (`Accept-Language`, Arabic by default) and is `null` when there is nothing to add.
  - An unanswered answer returns «لم تتم الإجابة عن السؤال.» / "No answer was given." This rule is checked first; the rules below apply only to an answer that is not unanswered. An answer is unanswered when: Mcq `optionId` or TrueFalse `value` is missing; Multi has no non-null id; every Fill blank is missing or normalises to empty; a text Short answer normalises to empty; a numeric Short answer normalises to empty under the numeric profile.
  - A Multi answer that is not exactly the correct set returns «الاختيارات الصحيحة: {right} من {total}، والخاطئة: {wrong}.» / "Correct choices: {right} of {total}; wrong choices: {wrong}." This applies in both partial-credit modes.
  - A Fill answer with two or more blanks that is not fully correct returns «الفراغات الصحيحة: {right} من {total}.» / "Correct blanks: {right} of {total}." A single-blank Fill returns `null`.
  - A numeric Short answer that does not parse as a number returns «اكتب الإجابة رقمًا فقط، بدون وحدات.» / "Write the answer as a plain number, without units." A number outside the tolerance returns `null`: a direction hint would reveal part of the answer.
  - Every other case returns `null`.

  The feedback never contains the verdict, the correct answer or the explanation.

## Changing a schema

A new field or rule changes the schema record, its rules, this document and the web editor in the same change. The spreadsheet import (`docs/question-import.md`) builds the same shapes from columns; a schema change updates its columns, parser and template in the same change. Essay is not importable (PRD §10.1). Rows already stored in the old shape need a data migration that rewrites them to the new canonical shape.
