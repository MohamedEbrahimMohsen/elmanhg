# Quiz sessions and the attempt log

A **session** is one run of questions by one student: a lesson quiz today, a unit or multi-unit exam later (E6). Every answer is graded at once and stored as an **attempt**. Attempts are stored forever (PRD §7.3) and are the source for mastery (#77), history (#78) and training-data export (E12).

## Model

All three tables map one to one to PRD §15. Names follow constitution §3 (no abbreviations).

### Session (aggregate root)

| Field | PRD §15 | Notes |
|---|---|---|
| `Id` | `id` | |
| `StudentId` | `student_id` | FK `Users`, restrict |
| `Kind` | `kind` | `Quiz`, `UnitExam`, `MultiUnitExam`. Only `Quiz` can be started today. |
| `Scope` | `scope_json` | jsonb. A quiz stores `{"lessonId":"<guid>"}`. |
| `ScopeKey` | `scope_key` | Canonical string used for resume and uniqueness. A quiz uses `lesson:<guid>`; exams will add `unit:<guid>` and so on. |
| `IsTestMode` | `is_test_mode` | True when an Admin runs the quiz. |
| `StartedAt` | `started_at` | |
| `LastActivityAt` | `last_activity_at` | Set at start, on resume, on each attempt and on finish. |
| `SubmittedAt` | `submitted_at?` | Null while the session is open. |
| `ScorePercent` | `score_pct?` | numeric(5,2), set on finish. |
| `Version` | `xmin` | PostgreSQL system column used as the row-version concurrency token; no DDL. |
| — | `time_limit_min?` | Added by E6 (exams). |

### SessionItem (a served question)

| Field | Notes |
|---|---|
| `SessionId`, `Position` | Position is 1-based. Unique `(SessionId, Position)`. |
| `QuestionId` | FK `Questions`, restrict. Unique `(SessionId, QuestionId)`: a question never appears twice in one session (PRD §7.2). |
| `QuestionVersion` | The version the student saw. |
| `MaxScore` | Copied from the served version. |

Items are chosen and written when the session starts and never change afterwards.

### Attempt (append-only)

| Field | PRD §15 | Notes |
|---|---|---|
| `SessionId`, `StudentId`, `QuestionId` | same | FKs restrict |
| `QuestionVersion` | `question_version` | The served version, copied from the item. |
| `Answer` | `answer_json` | jsonb, the canonical form of the typed answer (unknown properties dropped). |
| `Score` | `score` | numeric(9,2) |
| `NormalisedScore` | `normalised_score` | numeric(5,4), 0 to 1 |
| `GradedBy` | `graded_by` | `Auto`, `AI`, `Teacher`. Every v1 type is `Auto`. |
| `Grade` | `grade_json?` | jsonb, the serialised `GradeFeedback` (null when the grader gives none). |
| `TimeTakenMilliseconds` | `time_taken_ms` | See Time taken. |
| `CreatedAt` | `created_at` | |

## Lifecycle

1. **Start or resume** — `POST /api/sessions/quiz { lessonId, questionCount? }`.
   - The lesson must be Published; otherwise 404 `LESSON_NOT_FOUND` (student reads see Published lessons only).
   - If the student already has an open quiz session for that lesson, it is returned as is and `questionCount` is ignored (a refresh resumes, PRD §14).
   - Otherwise up to `questionCount` (default `Sessions:DefaultQuizSize`) questions are drawn at random from the lesson's **servable** questions (`ServableQuestionSpecification`, #67). The domain re-checks each question with `ServableQuestionSpecification.IsSatisfiedBy`. A lesson with fewer servable questions gives a shorter quiz; none gives 400 `SESSION_NO_SERVABLE_QUESTIONS`. Adaptive selection (#75) replaces the random draw without changing this model.
2. **Answer** — `POST /api/sessions/{id}/answers { questionId, answer, timeTakenMilliseconds? }`. One answer per question, graded at once and saved immediately. There is no draft state for quizzes.
3. **Finish** — `POST /api/sessions/{id}/finish`. Allowed at any time, including mid-quiz. Nothing finishes a session automatically.
4. **Read** — `GET /api/sessions/{id}` returns the items, the saved attempts and `currentPosition`: the lowest unanswered position, or null when every item is answered or the session is finished.

## Grading against the served version

Grading loads the `QuestionRevision` at `item.QuestionVersion` and calls `QuestionRevision.Grade(answer)`, which calls `QuestionGrader.Grade` with that snapshot's type, grading spec and max score. The live `Question` row is never used to grade. If a question is edited, retired or its lesson unpublished mid-session, the answer is still accepted and graded against the version the student saw (PRD §17 rule 2).

The validator checks that the answer is a JSON object of at most `Sessions:AnswerMaxLength` characters. The handler then checks the shape for the served type and returns 422 `QUESTION_ANSWER_INVALID` for a wrong shape, so a malformed answer never reaches the grader.

## Idempotency

- Submitting an answer equivalent (same JSON, any formatting) to the saved one returns the saved attempt again: 200, same body, no new row. A retry after a lost response is safe.
- A different answer for an answered question returns 409 `SESSION_QUESTION_ALREADY_ANSWERED`.
- Two concurrent first answers are stopped by the unique index `IX_Attempts_SessionId_QuestionId`; the loser gets the same 409.
- Two concurrent starts for the same lesson are stopped by `IX_Sessions_InProgressScope`; the loser gets 409 `SESSION_ALREADY_IN_PROGRESS`, and a retry resumes.
- A concurrent answer, finish or resume on one session is serialised by the `xmin` row version on `Sessions`. The loser gets 409 `SESSION_MODIFIED_CONCURRENTLY` and its whole save rolls back (no attempt is written); a retry resolves it (an answer that lost to a finish then gets 400 `SESSION_ALREADY_SUBMITTED`; a finish that lost to an answer then counts it).
- Finishing twice changes nothing and returns the same result.
- Answering a finished session returns 400 `SESSION_ALREADY_SUBMITTED`, except that replaying the saved answer still returns the saved attempt.

## Time taken

The client may report `timeTakenMilliseconds`. The server measures `elapsed = now − LastActivityAt` and stores `clamp(reported, 0, elapsed)`, or `elapsed` when nothing is reported. Resume resets `LastActivityAt`, so a gap between visits is never counted. The session's time is the sum of its attempts' times.

## Scoring

`ScorePercent = round(Σ attempt.Score / Σ item.MaxScore × 100, 2)`. Unanswered items count 0. The same formula will serve exams.

## What is revealed

Each item carries the served `type`, `stem`, `body` and `maxScore`. `correctAnswer` (the served grading spec) and `explanation` are returned only when the item has an attempt or the session is finished, so unanswered keys never leak while a quiz is open.

## Append-only enforcement

Migration `AddSessionsAndAttempts` creates the function `reject_append_only_mutation()` (`RAISE EXCEPTION '% is append-only', TG_TABLE_NAME`) and two triggers on `"Attempts"`:

- `attempts_append_only`: `BEFORE UPDATE OR DELETE ... FOR EACH ROW`
- `attempts_no_truncate`: `BEFORE TRUNCATE ... FOR EACH STATEMENT`

This is the audit-log pattern from `docs/audit-log.md`. `Attempt` has no mutating method and is only created through `Session.RecordAttempt`. Every foreign key to `Users`, `Questions` and `Sessions` is restrict, and users are only soft-deleted, so attempts survive. Anonymisation happens at export time (E12), not by rewriting attempts.

## Indexes

| Index | Purpose |
|---|---|
| `IX_Attempts_StudentId_QuestionId_CreatedAt` | Per-student, per-question attempt lookups for selection (#75) and mastery (#77). |
| `IX_Attempts_SessionId_QuestionId` (unique) | One attempt per served question. |
| `IX_Sessions_InProgressScope` (unique, `WHERE "SubmittedAt" IS NULL AND "IsDeleted" = false`) on `(StudentId, Kind, ScopeKey)` | One open session per student and scope. |
| `IX_Sessions_StudentId_StartedAt` | History (#78). |
| `IX_SessionItems_SessionId_Position`, `IX_SessionItems_SessionId_QuestionId` (unique) | Item order; no repeated question. |

## Access

- Policy `Assessments.Take` (`DefaultCodes.AssessmentsTake`): Students and Admins (PRD §16). Teachers get 403, anonymous callers 401.
- Every lookup filters by `StudentId == current user` in the same predicate. Another user's session id returns 404 `SESSION_NOT_FOUND`, never 403.

## Test mode

When the caller is an Admin, the session gets `IsTestMode = true`. It otherwise behaves the same and still writes attempts. Mastery (#77) and training-data export (E12) exclude test-mode sessions.

Session commands are not audited (`docs/audit-log.md`, "Not audited"): the attempts are their own log.

## Options

| Key | Default | Meaning |
|---|---|---|
| `Sessions:DefaultQuizSize` | 10 | Questions served when `questionCount` is omitted. |
| `Sessions:MinQuizSize` | 5 | Smallest allowed `questionCount`. |
| `Sessions:MaxQuizSize` | 20 | Largest allowed `questionCount`. The UI offers 5, 10 and 20. |
| `Sessions:AnswerMaxLength` | 4000 | Maximum raw length of an answer's JSON. |

The app fails to start unless `MinQuizSize <= DefaultQuizSize <= MaxQuizSize`.

## API

| Method | Route | Body | Response |
|---|---|---|---|
| POST | `/api/sessions/quiz` | `{ lessonId, questionCount? }` | 200 `SessionResult` (new or resumed) |
| GET | `/api/sessions/{sessionId}` | — | 200 `SessionResult` |
| POST | `/api/sessions/{sessionId}/answers` | `{ questionId, answer, timeTakenMilliseconds? }` | 200 `SessionItemResult` |
| POST | `/api/sessions/{sessionId}/finish` | — | 200 `SessionResult` |

`SessionResult { id, kind, scope, isTestMode, startedAt, submittedAt?, scorePercent?, timeTakenMilliseconds, currentPosition?, items[] }`
`SessionItemResult { position, questionId, questionVersion, type, stem, body, maxScore, attempt?, correctAnswer?, explanation? }`
`AttemptResult { id, answer, score, normalisedScore, outcome, feedback?, timeTakenMilliseconds, createdAt }`

## Error codes

| Code | HTTP | When |
|---|---|---|
| `SESSION_NOT_FOUND` | 404 | The session does not exist or belongs to someone else. |
| `SESSION_ID_REQUIRED` | 422 | Empty session id. |
| `SESSION_QUESTION_COUNT_INVALID` | 422 | `questionCount` outside `[MinQuizSize, MaxQuizSize]`. |
| `SESSION_QUESTION_NOT_FOUND` | 404 | The question is not part of the session. |
| `SESSION_ALREADY_IN_PROGRESS` | 409 | A concurrent start for the same lesson won the race; retry to resume. |
| `ATTEMPT_ANSWER_TOO_LONG` | 422 | The answer JSON is over `AnswerMaxLength`. |
| `ATTEMPT_TIME_TAKEN_INVALID` | 422 | Negative `timeTakenMilliseconds`. |
| `SESSION_NO_SERVABLE_QUESTIONS` | 400 | The lesson has no servable questions. |
| `SESSION_QUESTION_NOT_SERVABLE` | 400 | A drawn question is not servable (a guard; the draw already filters). |
| `SESSION_QUESTION_DUPLICATE` | 400 | The same question was drawn twice (a guard). |
| `SESSION_ALREADY_SUBMITTED` | 400 | Answering or resuming a finished session. |
| `SESSION_QUESTION_ALREADY_ANSWERED` | 409 | A different answer for an answered question, or a concurrent first answer. |
| `SESSION_MODIFIED_CONCURRENTLY` | 409 | A concurrent answer, finish or resume changed the session first; retry. |
| `LESSON_ID_REQUIRED`, `LESSON_NOT_FOUND`, `QUESTION_ID_REQUIRED`, `QUESTION_NOT_FOUND`, `QUESTION_ANSWER_INVALID`, `USER_NOT_AUTHENTICATED` | 422 / 404 / 422 / 404 / 422 / 401 | Reused codes. |
