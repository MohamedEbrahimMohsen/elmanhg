# Exam blueprints

An **exam blueprint** is the shape of an exam: how many questions of each type, an optional difficulty mix, an optional time limit and a pass mark (PRD §10.2). A subject has one **default** blueprint; a unit can have its own blueprint, which overrides the default for that unit. Blueprints are authored by admins on `/admin/blueprints` (#80). Exam generation (#81), multi-unit merge (#82) and retakes (#83) read them.

## Model

`ExamBlueprint` (`api/Elmanhg.Domain/ExamBlueprints/`) is an audited entity (`AuditEntity`, `IAuditedEntity`), soft-deletable.

| Field | PRD §15 | Notes |
|---|---|---|
| `Id` | `id` | |
| `SubjectId` | `subject_id` | FK `Subjects`, restrict. Always set, also for a unit blueprint. |
| `UnitId` | `unit_id?` | FK `Units`, restrict. Null means the subject default. |
| `TypeCounts` | `type_counts_json` | jsonb, canonical form (below). |
| `DifficultyMix` | `difficulty_mix_json?` | jsonb or null. |
| `QuestionCount` | `question_count` | Sum of the type counts; the merge denominator for #82. |
| `TimeLimitMinutes` | `time_limit_min?` | Null means no time limit. |
| `PassMark` | `pass_mark` | Whole number 1–100 (the score is out of 100, PRD §7.4). |

The owner link lives on the blueprint; `Subject` and `Unit` carry no blueprint id.

Canonical JSON (camelCase, `QuestionJson.SerializerOptions`). Type counts keep only types with a count above 0, sorted by `QuestionType` order (`Mcq`, `Multi`, `TrueFalse`, `Fill`, `Short`):

```json
[{"type":"mcq","count":10},{"type":"fill","count":5}]
```

```json
{"easyPercent":30,"mediumPercent":50,"hardPercent":20}
```

Indexes (partial, unique, soft-delete aware):

| Index | Columns | Filter |
|---|---|---|
| `IX_ExamBlueprints_SubjectDefault` | `SubjectId` | `"UnitId" IS NULL AND "IsDeleted" = false` |
| `IX_ExamBlueprints_UnitId` | `UnitId` | `"UnitId" IS NOT NULL AND "IsDeleted" = false` |

## Rules

- **Shortfall (PRD §17 rule 8):** a blueprint cannot be saved when, for any type, the required count is greater than the number of servable questions of that type. Servable is `ServableQuestionSpecification` (Approved, lesson Published, not retired). The check is `ExamBlueprintShortfall.Find`, enforced by the entity on create and update, and mirrored on the web by `findShortfall`.
- **Pool:** the subject default is checked against the subject's whole servable pool (all its units). A unit blueprint is checked against its own unit's pool only.
- **A default that is short for one unit** is not an error. The unit card shows a warning («عجز حالي في هذه الوحدة»), computed on the client; the exam start (#81) re-checks.
- **Difficulty mix** is optional: Easy/Medium/Hard as whole percentages 0–100 that sum to 100. It is a **target**, not part of the save check. The generator (#81) follows it as far as the pool allows.
- **Pass mark:** whole number 1–100.
- **Time limit:** optional; when set, a whole number of minutes from 1 to `ExamBlueprints:MaxTimeLimitMinutes`.
- **Size:** each count is 0 to `ExamBlueprints:MaxQuestionCount`, and so is the total; the total must be at least 1. A type may appear once. Zero counts are accepted and dropped when saved.
- **Save is an upsert:** `PUT` creates the blueprint when missing and updates it in place otherwise, keeping the same id.
- **Delete:** only a unit blueprint can be deleted; the unit then uses the subject default. Deleting the subject default is refused with `EXAM_BLUEPRINT_DEFAULT_NOT_DELETABLE`: it is the fallback for every unit.

## Resolution for exams

The contract for #81 and #82 (not coded in #80):

1. A unit exam uses the unit's blueprint; otherwise the subject default; otherwise the unit has no exam.
2. The exam start re-runs `ExamBlueprintShortfall.Find` against the unit's current servable pool, because questions can be retired after the blueprint was saved.
3. The session copies the time limit and the pass mark at start, so a later blueprint edit never changes a started exam.
4. A multi-unit exam (#82) merges the units' `GetTypeCounts()` proportionally, using `QuestionCount` as the denominator.

## Orphans

Deleting a subject or a unit does not cascade to its blueprints. `DeleteUnit` requires the unit to have no lessons and `DeleteSubject` requires no units, so a leftover blueprint has no servable pool. Every read goes through a live subject or unit, so such an orphan is never shown or used.

## Concurrency

Two first saves of the same default (or the same unit) race on the partial unique index; the loser gets 409 `EXAM_BLUEPRINT_MODIFIED_CONCURRENTLY` (mapped in `AppDbContext.SaveChangesAsync`), and a retry updates the saved row. Updates have no concurrency token: the last write wins, and every save is audited.

## API

Controller `api/exam-blueprints`, policy `Blueprints.Manage` (admin only) on every action.

| Method | Route | Name | Body | Response |
|---|---|---|---|---|
| GET | `/api/exam-blueprints/subjects/{subjectId}` | `GetSubjectExamBlueprints` | — | 200 `SubjectExamBlueprintsResult`: the default, every unit (by order) with its blueprint or null, and servable counts for every type (zeros included) for the subject and for each unit |
| PUT | `/api/exam-blueprints/subjects/{subjectId}` | `SaveSubjectExamBlueprint` | `ExamBlueprintInput` | 200 `ExamBlueprintResult` |
| PUT | `/api/exam-blueprints/units/{unitId}` | `SaveUnitExamBlueprint` | `ExamBlueprintInput` | 200 `ExamBlueprintResult` |
| DELETE | `/api/exam-blueprints/{examBlueprintId}` | `DeleteExamBlueprint` | — | 200 |

`ExamBlueprintInput`: `{ "typeCounts": [{ "type": "Mcq", "count": 10 }], "difficultyMix": { "easyPercent": 30, "mediumPercent": 50, "hardPercent": 20 } | null, "timeLimitMinutes": 45 | null, "passMark": 50 }`.

## Error codes

| Code | HTTP | When |
|---|---|---|
| `EXAM_BLUEPRINT_SHORTFALL` | 400 | Some type needs more servable questions than exist (context `types`, e.g. `Mcq 1/2`). |
| `EXAM_BLUEPRINT_DEFAULT_NOT_DELETABLE` | 400 | Delete of a subject default. |
| `EXAM_BLUEPRINT_NOT_FOUND` | 404 | Delete of an unknown id. |
| `EXAM_BLUEPRINT_ID_REQUIRED` | 422 | Empty id on delete. |
| `EXAM_BLUEPRINT_EMPTY` | 422 | No body, no type counts, or a total of 0. |
| `EXAM_BLUEPRINT_TOO_LARGE` | 422 | Total above the maximum. |
| `EXAM_BLUEPRINT_TYPE_DUPLICATE` | 422 | A type listed twice. |
| `EXAM_BLUEPRINT_TYPE_COUNT_REQUIRED` | 422 | A `null` entry in the type counts list. |
| `EXAM_BLUEPRINT_COUNT_INVALID` | 422 | A count below 0 or above the maximum. |
| `EXAM_BLUEPRINT_DIFFICULTY_MIX_INVALID` | 422 | A percentage outside 0–100, or a sum other than 100. |
| `EXAM_BLUEPRINT_TIME_LIMIT_INVALID` | 422 | Time limit below 1 or above the maximum. |
| `EXAM_BLUEPRINT_PASS_MARK_INVALID` | 422 | Pass mark outside 1–100. |
| `EXAM_BLUEPRINT_MODIFIED_CONCURRENTLY` | 409 | Concurrent first save (see Concurrency). |

Reused: `SUBJECT_ID_REQUIRED`, `SUBJECT_NOT_FOUND`, `UNIT_ID_REQUIRED`, `UNIT_NOT_FOUND`, `QUESTION_TYPE_REQUIRED`, `QUESTION_TYPE_INVALID`, `USER_NOT_AUTHENTICATED`.

## Options

| Key | Default | Meaning |
|---|---|---|
| `ExamBlueprints:MaxQuestionCount` | 100 | Cap on each count and on the total. |
| `ExamBlueprints:MaxTimeLimitMinutes` | 300 | Cap on the time limit. |

The web mirrors both values in `web/src/features/blueprints/schemas/examBlueprintSchema.ts`.

## Admin screen

`/admin/blueprints` (`web/src/features/blueprints`):

1. A subject select («المادة»), kept in the URL as `subjectId`; the first subject by order is used when none is chosen.
2. The subject's default blueprint card, captioned when no default is saved yet.
3. One card per unit. A unit with its own blueprint shows the editor and «استخدام النموذج الافتراضي» (confirmed inline) to remove it. A unit without one shows «تستخدم النموذج الافتراضي» (or that no exam is available when there is no default), a warning when the default is short for that unit, and «إنشاء نموذج للوحدة», which opens an editor prefilled from the default.
4. Each editor has a table of type / required / available (short rows highlighted, a total row), the time limit, the pass mark, an optional difficulty mix, and a live «عجز حالي» notice. «حفظ» is refused on the client while any type is short, with the same message the server uses. After every save, successful or not, the overview is refetched so the available counts are fresh.
5. Loading skeleton, error with retry, and an empty state linking to the content page when there are no subjects.

## Audit

| Command | AuditAction | Resource id |
|---|---|---|
| SaveSubjectExamBlueprint | `ExamBlueprint.SaveSubjectDefault` | from the result |
| SaveUnitExamBlueprint | `ExamBlueprint.SaveUnit` | from the result |
| DeleteExamBlueprint | `ExamBlueprint.Delete` | from the command |

Resource type `ExamBlueprint`. See `docs/audit-log.md`.
