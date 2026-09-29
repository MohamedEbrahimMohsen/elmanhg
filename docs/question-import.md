# Question import from a spreadsheet

Admins add many questions to one lesson at once from an Excel workbook (PRD §10.1: "Bulk import of questions via spreadsheet template (v1: deterministic types only). Imported questions enter as Pending.").

## Flow

1. **Download** the template from the import page (`#/admin/question/import/:lessonId`, linked from the lesson editor and from the lesson-filtered question list).
2. **Check** (dry run): upload the filled file. The server parses every row, validates it with the same rules as the question editor and returns a report. Nothing is saved and no audit row is written.
3. **Confirm**: when the report has no problems, import. Every row becomes a **Pending**, version 1 question in the chosen lesson, with one revision, in one transaction.

## Endpoints

All three use policy `Content.Manage` (`DefaultCodes.ContentManage`, admins).

| Method | Route | Input | Response |
|---|---|---|---|
| GET | `/api/question-imports/template` | — | 200 `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, file `elmanhg-question-import-template.xlsx` |
| POST | `/api/question-imports/preview` | multipart: `lessonId`, `file` | 200 `QuestionImportPreviewResult { totalRows, validRows, types[{ type, count }], errors[{ sheet, row, column?, code }] }` |
| POST | `/api/question-imports` | multipart: `lessonId`, `batchId`, `file` | 200 `ImportQuestionsResult { batchId, createdCount, replayed }` |

The template's Instructions sheet is written in the request language (`Accept-Language`).

## Workbook

- One sheet per v1 type, named exactly `Mcq`, `Multi`, `TrueFalse`, `Fill`, `Short` (case-insensitive). Any other sheet, including `Instructions`, is ignored. `Essay` (v2) is authored in the editor only; an `Essay` sheet is ignored.
- Row 1 holds the column keys below, in any order, matched trimmed and case-insensitive. An empty header cell makes its column ignored. An unknown header is reported as `QUESTION_IMPORT_COLUMN_UNKNOWN` on row 1; a repeated header as `QUESTION_IMPORT_COLUMN_DUPLICATE`. A missing column reads as empty.
- Every row below is one question. Rows whose cells are all blank are skipped.
- `row` in the report is the Excel row number (the header is row 1).
- All template sheets are right-to-left, with the header row frozen and dropdowns on the token columns.

## Columns

Order per sheet. The common tail is `explanation, difficulty, max_score, objective, tags`.

| Sheet | Columns in order | Required |
|---|---|---|
| Mcq | `stem`, `option_a`…`option_{n}`, `correct`, tail | stem, option_a, option_b, correct, difficulty |
| Multi | `stem`, `option_a`…`option_{n}`, `correct`, `partial_credit`, tail | stem, option_a, option_b, correct, difficulty |
| TrueFalse | `stem`, `correct_answer`, tail | stem, correct_answer, difficulty |
| Fill | `stem`, `blank_1`…`blank_{m}`, `unify_letter_variants`, tail | stem, blank_1, difficulty |
| Short | `stem`, `answer_kind`, `value`, `tolerance`, `tolerance_mode`, `accepted_answers`, `unify_letter_variants`, tail | stem, answer_kind, difficulty |

`n = min(Content:QuestionOptionsMaxCount, 26)` and `m = Content:QuestionBlanksMaxCount`.

Dropdown values:

| Column | Values |
|---|---|
| `difficulty` | `easy,medium,hard` |
| `answer_kind` | `numeric,text` |
| `tolerance_mode` | `absolute,percent` |
| `partial_credit`, `correct_answer`, `unify_letter_variants` | `true,false` |

Mapping (the shapes are those in `docs/question-schemas.md`):

| Field | Source |
|---|---|
| stem, explanation | text → HTML (below) |
| difficulty | enum token; **no default** (`QUESTION_DIFFICULTY_REQUIRED` when empty) |
| max_score | whole number; empty means 1 |
| tags | list |
| objective | whole number: `1` is the lesson's first objective by order; empty means none; any other number is `QUESTION_IMPORT_OBJECTIVE_INVALID` |
| Mcq / Multi options | each non-empty `option_x` becomes option id `x` (the editor's ids `a`, `b`, …); gaps are allowed |
| Mcq `correct` | one option letter |
| Multi `correct`, `partial_credit` | letters separated by `\|`; partial credit empty means false |
| TrueFalse `correct_answer` | boolean |
| Fill `blank_N` | accepted answers for blank id `N`, separated by `\|`; an empty column adds no blank. The stem must hold each `[[N]]` once. `unify_letter_variants` empty or `true` turns the three letter rules (`unifyAlef`, `unifyTaaMarbuta`, `unifyAlefMaqsura`) on, `false` turns them off; the other normalisation rules are always on for imported questions (change them in the editor) |
| Short numeric | `value`; `tolerance` empty means 0; `tolerance_mode` empty means absolute |
| Short text | `accepted_answers` separated by `\|`; `unify_letter_variants` empty or `true` turns the three letter rules (`unifyAlef`, `unifyTaaMarbuta`, `unifyAlefMaqsura`) on, `false` turns them off; the other normalisation rules are always on for imported questions (change them in the editor) |

## Text and math

`stem`, `explanation` and each option go through the same conversion: `\r\n` becomes `\n`, each non-blank line becomes a `<p>` paragraph (HTML-encoded), and `$…$` on one line becomes an inline-math node (`<span data-type="inline-math" data-latex="…">`, `docs/rich-text.md`). The rich-text sanitiser still runs at save. Images and block math are not imported; add them later in the editor.

## Lists and tokens

- Lists (`correct` for Multi, `blank_N`, `accepted_answers`, `tags`) are separated by `|`, trimmed, and empty values are dropped. Commas are kept, so `3,5` is one answer.
- Booleans: `true/yes/1` or `false/no/0`, case-insensitive.
- Enums (`difficulty`, `answer_kind`, `tolerance_mode`): the names above, case-insensitive. Numeric text such as `1` is rejected.
- A value that cannot be read is `QUESTION_IMPORT_CELL_INVALID` on that column.

## Validation

Each row is checked by the question editor's own validator (`QuestionFieldsValidator`), so every `QUESTION_*` code in `docs/question-schemas.md` can appear as a row error (reported once per row). A row with a cell error is not validated further.

Import-specific codes:

| Code | When | HTTP |
|---|---|---|
| `QUESTION_IMPORT_FILE_REQUIRED` | no file | 422 |
| `QUESTION_IMPORT_FILE_TYPE_INVALID` | not `.xlsx` | 422 |
| `QUESTION_IMPORT_FILE_TOO_LARGE` | over the size cap | 422 |
| `QUESTION_IMPORT_BATCH_ID_REQUIRED` | import without `batchId` | 422 |
| `SPREADSHEET_UNREADABLE` | the file is not a readable workbook | 400 |
| `QUESTION_IMPORT_EMPTY` | no data rows in any type sheet | 400 |
| `QUESTION_IMPORT_TOO_MANY_ROWS` | more rows than the cap (context `max`) | 400 |
| `QUESTION_IMPORT_HAS_ERRORS` | import of a file with any problem (context `count`) | 400 |
| `QUESTION_IMPORT_BATCH_CONFLICT` | the batch id was already used for another file or lesson | 409 |
| `QUESTION_IMPORT_COLUMN_UNKNOWN` | unknown header (report) | 200 |
| `QUESTION_IMPORT_COLUMN_DUPLICATE` | repeated header (report) | 200 |
| `QUESTION_IMPORT_CELL_INVALID` | unreadable cell (report) | 200 |
| `QUESTION_IMPORT_OBJECTIVE_INVALID` | objective number not in the lesson (report) | 200 |

The report carries codes only; the web translates them.

## Limits

- `Content:QuestionImportMaxRows` (500): data rows per file, across all type sheets.
- `Content:QuestionImportMaxFileSizeInMb` (5).
- Only the first 256 columns of a type sheet are read, and sheets that are not type sheets are not read at all. Reading stops one data row past the row cap. The work stays bounded even when a small file declares a cell far away (for example `XFD1048576`).
- `.xlsx` only (checked by extension; the content type is not checked because browsers send `application/octet-stream` for it on machines without Office). CSV and `.xls` are not accepted.

## All-or-nothing commit

The import re-parses the file. If any row or header has a problem, nothing is created and no batch is stored (`QUESTION_IMPORT_HAS_ERRORS`). Otherwise the batch record and every question are saved in one transaction.

## Idempotency

- The web generates a `batchId` (UUID) when a file is chosen and reuses it while the same file stays selected, so a retried or double-clicked confirm carries the same id.
- The server stores `QuestionImportBatch { id = batchId, lessonId, fileHash (SHA-256 hex of the uploaded bytes), questionCount }` with the questions.
- A confirm whose batch id exists with the same lesson and file hash returns 200 `{ replayed: true, createdCount: <stored count> }` and writes nothing.
- The same batch id with another file or lesson returns 409 `QUESTION_IMPORT_BATCH_CONFLICT`.
- A commit that failed stores nothing, so the same id can be retried after a fix.
- Two truly concurrent confirms of one batch id: the second insert hits the batch's primary key. Infrastructure turns that into the batch conflict, and the import pipeline then reads the committed batch and answers exactly like a replay (same file) or a 409 (different file). It never surfaces as a 500.
- Every imported question carries `importBatchId`.

## Audit

The import is audited as `Question.Import` on resource type `QuestionImportBatch` with the batch id (`docs/audit-log.md`). The diff lists the new batch and every created question; a replay writes a row with no diff. The dry run is not audited.
