# Question schemas

The storage contract for a question's `Body` and `GradingSpec` (PRD §5.4, §6). The schema records (`Elmanhg.Domain.Questions.Schemas`), the per-type rules (`Elmanhg.Application.Questions.Shared.*QuestionRules`), this document and the web question editor change together.

## Types

The wire names are the `QuestionType` enum values. v1 grading follows PRD §6.

- `Mcq`: one option. Exact match, no partial credit.
- `Multi`: a set of options. Set match; optional partial credit (correct − wrong) / total, floor 0.
- `TrueFalse`: a boolean. Exact match.
- `Fill`: one string per blank. Normalised match (PRD §6.2) against an accepted-answers list per blank; credit per blank.
- `Short`: a number or a string. Numeric within a tolerance (absolute or percent); text against an accepted list with normalisation.

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
{"blanks":[{"id":"1","acceptedAnswers":["20","٢٠"]}],"unifyLetterVariants":true}
```

**Short**, numeric answer:

```json
{"answerKind":"numeric"}
```
```json
{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}
```

`toleranceMode` is `absolute` (±tolerance) or `percent` (±tolerance % of the value).

**Short**, text answer:

```json
{"answerKind":"text"}
```
```json
{"acceptedAnswers":["ماء"],"unifyLetterVariants":true}
```

`unifyLetterVariants` (PRD §6.2 "configurable per question, default on") defaults to `true` for fill and text short answers.

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
| Fill, Short text | 1 to `Content:QuestionAcceptedAnswersMaxCount` accepted answers, none empty, each at most `Content:QuestionAnswerMaxLength` after trimming | `QUESTION_ACCEPTED_ANSWERS_INVALID` |
| Short | `answerKind` is present (`numeric` or `text`; any other value is `QUESTION_BODY_INVALID`) | `QUESTION_ANSWER_KIND_REQUIRED` |
| Short numeric | `value` is present | `QUESTION_NUMERIC_VALUE_REQUIRED` |
| Short numeric | `tolerance` is present and 0 or more, and `toleranceMode` is present | `QUESTION_TOLERANCE_INVALID` |

- **Id format** (option and blank ids): `^[a-z0-9-]{1,20}$`. Ids are referenced from grading specs and `[[id]]` placeholders, so they stay short, lowercase ASCII.
- **Fields** outside the body and spec (stem, explanation, difficulty, tags, max score) are checked by `QuestionFieldsValidator`. Caps: `Content:QuestionStemMaxLength`, `Content:QuestionExplanationMaxLength`, `Content:QuestionTagsMaxCount`, `Content:QuestionTagMaxLength`, `Content:QuestionMaxScoreMax` (max score is a whole number from 1).

## Canonical storage

- `Body` and `GradingSpec` are `jsonb` columns. The server re-serialises the typed records, so unknown properties are dropped and defaults are written out (`partialCredit`, `unifyLetterVariants`). Numeric short specs keep only `value`, `tolerance` and `toleranceMode`; text short specs keep only `acceptedAnswers` and `unifyLetterVariants`. The true/false body is always `{}`.
- Choice option `text` is sanitised rich text, like the stem and explanation (`docs/rich-text.md`).
- Accepted answers are plain text, trimmed. They are normalised at grading time (PRD §6.2), never at save time.
- Fill spec entries follow the body's blank order.
- Two JSON values are equal when they are semantically equal (`JsonNode.DeepEquals`), never by string comparison: PostgreSQL re-formats `jsonb` text on read.

## Versioning

- **Content** is the stem, body, grading spec, explanation and max score. **Metadata** is difficulty, objective link and tags. The type never changes: changing it returns `400 QUESTION_TYPE_IMMUTABLE`.
- A question is created at version 1 with a revision snapshot of version 1.
- Every content edit, in any status, increments `version` and appends a `QuestionRevision` holding the snapshot of the new version. A content edit on an Approved question also returns it to Pending and clears `validatedBy` and `validatedAt`. A Rejected question stays Rejected on edit; resubmission is its own flow.
- A metadata-only edit changes neither the status nor the version, and adds no revision. An edit that changes nothing is a no-op.
- The snapshot (`QuestionRevisionSnapshot`) is jsonb: `{"type","stem","body","gradingSpec","explanation","maxScore"}`, so an attempt's `question_version` always resolves to an exact snapshot.
- There is no concurrency token yet. An approval must name the version the teacher reviewed, so an edit made while the teacher was reviewing cannot be approved unseen.

## Validation status

- Every question is created Pending. No request, command or endpoint carries a status.
- The only way to Approved is `Question.Approve(TeacherSubject)`: it needs a live teacher assignment for the question's subject (`QUESTION_VALIDATOR_NOT_ASSIGNED`) and a Pending question (`QUESTION_NOT_PENDING`). A `TeacherSubject` can only be created for a teacher, so an admin cannot approve (PRD §16, §17 rule 3).

## Changing a schema

A new field or rule changes the schema record, its rules, this document and the web editor in the same change. Rows already stored in the old shape need a data migration that rewrites them to the new canonical shape.
