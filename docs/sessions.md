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
| `ScopeKey` | `scope_key` | Canonical string used for resume and uniqueness. A quiz uses `lesson:<guid>`; exams use `unit:<guid>` through `UnitExamScope` (Domain), already read by progress best scores (#78). |
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
   - Otherwise up to `questionCount` (default `Sessions:DefaultQuizSize`) questions are chosen by adaptive selection (see Selection) from the lesson's **servable** questions (`ServableQuestionSpecification`, #67). The domain re-checks each question with `ServableQuestionSpecification.IsSatisfiedBy`. A lesson with fewer servable questions gives a shorter quiz; none gives 400 `SESSION_NO_SERVABLE_QUESTIONS`.
2. **Answer** — `POST /api/sessions/{id}/answers { questionId, answer, timeTakenMilliseconds? }`. One answer per question, graded at once and saved immediately. There is no draft state for quizzes. A new attempt in a non-test session also updates the student's `QuestionMastery` row in the same save (`docs/mastery.md`); a replayed answer does not.
3. **Finish** — `POST /api/sessions/{id}/finish`. Allowed at any time, including mid-quiz. Nothing finishes a session automatically.
4. **Read** — `GET /api/sessions/{id}` returns the items, the saved attempts and `currentPosition`: the lowest unanswered position, or null when every item is answered or the session is finished.

## Selection

Selection implements PRD §7.2 when a new quiz starts. It never runs on resume. It is the pure domain function `QuestionSelector.Select`, fed by two queries: the ids of the lesson's servable questions, and one grouped attempt summary per question for the calling student.

An attempt counts as **correct** when its `NormalisedScore` is at or above `Mastery:CorrectThreshold` (0.8, PRD §7.3). Anything below is **wrong**, so a partial credit of 0.5 is wrong. The display outcome (`Correct`/`Partial`/`Incorrect`) is not used.

Every candidate falls into exactly one bucket, and the buckets are served in this order:

| Bucket | Rule | Order within the bucket |
|---|---|---|
| 1. Unseen | The student has no attempt on the question. | Random. |
| 2. Last wrong | The student's latest attempt is wrong (this includes never correct). | Oldest latest attempt first; ties are random. |
| 3. Correct once | The latest attempt is correct and the student has exactly one correct attempt. | Random. |
| 4. Rest | The latest attempt is correct and the student has two or more correct attempts. | Weighted random, favouring the least recently seen: ranked by latest attempt, the oldest has weight *n* and the newest weight 1. |

For example, a history of correct, wrong, correct is Rest; wrong, correct is Correct once; correct, correct, wrong is Last wrong.

- **History that counts.** Every attempt by the student on the question counts: attempts on older question versions (mastery is per question, so a typo fix does not reset history), attempts in unfinished sessions (an attempt is final once saved), and test-mode attempts.
- **Latest attempt.** "The latest attempt is correct" is read as "the latest correct attempt is the latest attempt", so the summary stays one `GROUP BY` over `IX_Attempts_StudentId_QuestionId_CreatedAt`. If a wrong and a correct attempt on one question ever shared the same microsecond, the question counts as correct; this needs two sessions answering the same question at once and is not otherwise handled.
- **Item order.** Positions follow the priority order: position 1 is the first unseen question, and so on. There is no final shuffle, so finishing mid-quiz still covers the most useful questions.
- **No repeats.** A question appears at most once per session: the selector de-duplicates, `Session.StartQuiz` rejects duplicates (`SESSION_QUESTION_DUPLICATE`), and the unique index `IX_SessionItems_SessionId_QuestionId` backs both. Items are fixed at start.
- **Small pool.** The quiz has `min(questionCount, servable questions)` items. No servable question gives 400 `SESSION_NO_SERVABLE_QUESTIONS`.
- **Races.** A chosen question that is soft-deleted before it loads is skipped, giving a shorter quiz. One that is retired or unapproved in between makes the start fail with 400 `SESSION_QUESTION_NOT_SERVABLE`; a retry selects again.
- **Randomness.** The only random source is the injected `System.Random` (`Random.Shared` in the app, seeded in tests). Candidates are sorted by id before any randomness, so the result depends only on the candidates, the history and the seed.

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

When the caller is an Admin, the session gets `IsTestMode = true`. It otherwise behaves the same and still writes attempts. Mastery (#77) and training-data export (E12) exclude test-mode sessions. Test-mode attempts still count toward that admin's own question selection.

Session commands are not audited (`docs/audit-log.md`, "Not audited"): the attempts are their own log.

## Options

| Key | Default | Meaning |
|---|---|---|
| `Sessions:DefaultQuizSize` | 10 | Questions served when `questionCount` is omitted. |
| `Sessions:MinQuizSize` | 5 | Smallest allowed `questionCount`. |
| `Sessions:MaxQuizSize` | 20 | Largest allowed `questionCount`. The UI offers 5, 10 and 20. |
| `Sessions:AnswerMaxLength` | 4000 | Maximum raw length of an answer's JSON. |
| `Mastery:CorrectThreshold` | 0.8 | Normalised score at or above which an attempt counts as correct (PRD §7.3); used by selection and mastery (`docs/mastery.md`). |

The app fails to start unless `MinQuizSize <= DefaultQuizSize <= MaxQuizSize`.

## API

| Method | Route | Body | Response |
|---|---|---|---|
| POST | `/api/sessions/quiz` | `{ lessonId, questionCount? }` | 200 `SessionResult` (new or resumed) |
| GET | `/api/sessions/{sessionId}` | — | 200 `SessionResult` |
| POST | `/api/sessions/{sessionId}/answers` | `{ questionId, answer, timeTakenMilliseconds? }` | 200 `SessionItemResult` |
| POST | `/api/sessions/{sessionId}/finish` | — | 200 `SessionResult` |

History: `GET /api/progress/sessions` (`docs/progress.md`).

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
| `SESSION_MODIFIED_CONCURRENTLY` | 409 | A concurrent answer, finish or resume changed the session first, or a concurrent answer updated the same question's mastery row; retry. |
| `LESSON_ID_REQUIRED`, `LESSON_NOT_FOUND`, `QUESTION_ID_REQUIRED`, `QUESTION_NOT_FOUND`, `QUESTION_ANSWER_INVALID`, `USER_NOT_AUTHENTICATED` | 422 / 404 / 422 / 404 / 422 / 401 | Reused codes. |

## Student screens (web)

- Routes: `/student/lesson/{lessonId}/practice` (choose 5, 10 or 20; start or resume), `/student/quiz/{sessionId}` (one question at a time), `/student/quiz-result/{sessionId}` (score out of 100, time, review of answered questions). A finished session opens the result; an open one opens the quiz.
- Every item arrives with the start (or `GET`) response, so moving to the next question makes no request. The start response is written straight into the query cache, and the images of the next question are preloaded.
- A refresh opens the lowest unanswered position (`currentPosition`); when every item is answered but the quiz is not finished, it shows the last item and "عرض النتيجة".
- The correct answer and explanation shown after "تحقّق" come from the submit-answer response (see What is revealed).
- The client reports `timeTakenMilliseconds` from the moment the question was shown to "تحقّق".
- "تدريب جديد" starts the smallest of 5/10/20 that is at least the number of questions served.
- "اسأل المساعد" is shown disabled with "متاح قريبًا" until the AI Avatar (E8) ships.
