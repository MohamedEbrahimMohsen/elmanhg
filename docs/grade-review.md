# AI grade review (#128)

## Overview

Teachers review AI grades that wait in review, in the subjects they are assigned to (PRD §8.3). They open a grade, see the question, the grading key, the student's answer and the AI result, and then **accept** the AI score or **override** it with their own score and a note. The decision makes the score final in one save: it writes the attempt (`GradedBy = Teacher`), updates mastery and the quiz or exam score, and writes the training rows. The student sees «قيد المراجعة» turn into the graded result, with the teacher's note, through a realtime push.

## What is listed

- `EssayGrade` rows with `Status = InReview`: reason `LowConfidence` or `GradingFailed` ([essay-grading.md](essay-grading.md)).
- `MathStepGrade` rows with `Status = InReview`: reason `LowConfidence`, `GradingFailed` or `FinalAnswerUnchecked` ([math-step-grading.md](math-step-grading.md)).
- Grades from admin test-mode sessions are never listed or counted, and opening or reviewing one directly returns 404 `GRADE_REVIEW_NOT_FOUND`.
- Routing to review uses the existing thresholds `EssayGrading:ReviewConfidenceThreshold` and `MathStepGrading:ReviewConfidenceThreshold` (0.7), the defaults of the runtime settings `grading.essayReviewConfidenceThreshold` and `grading.mathStepReviewConfidenceThreshold` ([configuration.md](configuration.md)). They are applied when the grade completes; review adds no threshold of its own.
- Legacy `mathUnchecked` attempts written before #123 are not listed ([math-cas.md](math-cas.md)).
- One list per subject and kind (`Essay` or `MathSteps`), oldest first (`RequestedAt`, then `Id`), paged.

## Decisions

| Rule | Detail |
|---|---|
| Accept | Only when the AI produced a score (`LowConfidence`). Copies the AI score into `ReviewedScore`. Otherwise 400 `GRADE_REVIEW_NO_AI_SCORE`. The note is optional. |
| Override | A total score from 0 to the max score, at most 2 decimals; the normalised score is `round(score / MaxScore, 4)`. The note is required. There are no per-criterion or per-step marks. |
| Note | At most `GradeReview:CommentMaxLength` characters; whitespace only counts as none. The student sees it; it is rendered as text. |
| AI score kept | `Score` / `NormalisedScore` are never overwritten. Readers use `FinalScore => ReviewedScore ?? Score`. |
| `GradedBy` | `Teacher` for both accept and override. |
| Final | A second decision returns 409 `GRADE_NOT_IN_REVIEW`. |

What the student sees after a review (`GET …/essay-grade`, `GET …/math-step-grade`): `status: Graded`, the final score and the outcome from the final normalised score, and `review { decision, comment, reviewedAt }` (no teacher identity). After an **override** the AI criteria or steps are `[]` and the AI justification is null; an overridden math grade's attempt has no feedback, and `finalAnswerVerdict` stays null if the CAS never checked the answer. After an **accept** the AI detail and the stored feedback stay.

## Model

Both `EssayGrades` and `MathStepGrades` gain nullable columns: `ReviewDecision` (`Accepted` / `Overridden`), `ReviewedBy`, `ReviewedAt`, `ReviewComment` (text), `ReviewedScore` (9,2) and `ReviewedNormalisedScore` (5,4). `ReviewReason` is kept as the history of why the grade was reviewed. A review sets `Status = Graded`, `GradedAt` (when there was no AI grade), `UpdatedBy` and `UpdationDate`, and the handler then stamps `AppliedAt`. Both aggregates are `IAuditedEntity`. Migration `AddGradeReviews`.

## API

Controller `GradeReviewsController`; every action has policy `AiGrades.Override` (Teacher, Admin). Every request except the summary carries `subjectId` in the route and is an `ISubjectScopedRequest`: an unassigned teacher gets 403 `SUBJECT_OUT_OF_SCOPE`; an Admin is never scoped. A grade id from another subject, a grade that was never in review, or a grade from an admin test-mode session is 404 `GRADE_REVIEW_NOT_FOUND`. Results never carry student identity.

| Method | Route | Result |
|---|---|---|
| GET | `/api/grade-reviews/subjects` | `GradeReviewSubjectResult[]`: `{ subjectId, name, essayCount, mathStepsCount }` for the teacher's assigned subjects (every subject for Admin), zero counts included, by `Order` then name |
| GET | `/api/subjects/{subjectId}/grade-reviews?kind=Essay\|MathSteps&pageNumber&pageSize` | `PageData<GradeReviewItemResult>`: `{ id, kind, questionId, stem, unitName, lessonName, reviewReason, maxScore, aiScore, confidence, requestedAt }` |
| GET | `/api/subjects/{subjectId}/grade-reviews/essays/{essayGradeId}` | `GradeReviewDetailResult` |
| POST | `/api/subjects/{subjectId}/grade-reviews/essays/{essayGradeId}` | body `{ decision, score, comment }` → `GradeReviewDetailResult`; audited `EssayGrade.Review` |
| GET | `/api/subjects/{subjectId}/grade-reviews/math-steps/{mathStepGradeId}` | `GradeReviewDetailResult` |
| POST | `/api/subjects/{subjectId}/grade-reviews/math-steps/{mathStepGradeId}` | as essays; audited `MathStepGrade.Review` |

`GradeReviewDetailResult` reads the **served revision** (the version the student answered): `questionType`, `stem`, `body`, `gradingSpec`, plus `unitName`, `lessonName`, the student's `answer` JSON, `maxScore`, `status`, `reviewReason`, `requestedAt`, the AI result (`aiScore`, `confidence`, `justification`, `criteria` for essays, `steps` and `finalAnswerVerdict` for math), `finalScore` (null until reviewed) and `review`. The teacher always sees the AI fields, also after an override.

| Code | HTTP | When |
|---|---|---|
| `GRADE_NOT_IN_REVIEW` | 409 | the grade is not `InReview` (already decided, or still pending) |
| `GRADE_REVIEW_NO_AI_SCORE` | 400 | accept without an AI score |
| `GRADE_REVIEW_SCORE_OUT_OF_RANGE` | 400 | override score below 0 or above the max score |
| `GRADE_REVIEW_NOT_FOUND` | 404 | missing, other subject, or never in review |
| `GRADE_MODIFIED_CONCURRENTLY` | 409 | a lost race on the grade row or its training row |
| `GRADE_REVIEW_ID_REQUIRED`, `GRADE_REVIEW_KIND_INVALID`, `GRADE_REVIEW_DECISION_INVALID`, `GRADE_REVIEW_SCORE_REQUIRED`, `GRADE_REVIEW_SCORE_NOT_ALLOWED`, `GRADE_REVIEW_SCORE_INVALID`, `GRADE_REVIEW_COMMENT_REQUIRED`, `GRADE_REVIEW_COMMENT_TOO_LONG`, `GRADE_REVIEW_PAGE_NUMBER_INVALID`, `GRADE_REVIEW_PAGE_SIZE_INVALID` | 422 | validation |

## Concurrency

A grade in review changes only through a review; the worker only touches `Pending` or unapplied `Graded` grades. The domain guard gives 409 `GRADE_NOT_IN_REVIEW` for a second decision. Two decisions in the same instant: the loser's save fails on the grade's `xmin` token, or on the unique `(EssayGradeId, Trigger)` training index (EF inserts that row first), and both map to 409 `GRADE_MODIFIED_CONCURRENTLY`. A race on the session row keeps 409 `SESSION_MODIFIED_CONCURRENTLY` / `SESSION_QUESTION_ALREADY_ANSWERED`. The web treats every 409 alike.

## Training data

- The applied attempt raises `AttemptsRecorded`, so `AttemptTrainingRecords` gets one row with `GradedBy = Teacher` and the final score, for essays and math.
- An essay review raises `EssayGradeReviewed`, which writes an `EssayGradeTrainingRecords` row with `Trigger = TeacherReviewed`: every AI field plus the decision, reviewed score, note and `ReviewedAt`, with `OccurredAt` = the AI `GradedAt`. A `GradingFailed` review writes no such row. The export returns one line per essay grade: the `TeacherReviewed` row replaces the `Completed` one. See [training-data.md](training-data.md).
- Test-mode sessions write neither row.

## Realtime

After the commit the API sends `gradeReviewed { sessionId, questionId }` to the student over the notifications hub (best effort). The web refreshes the essay or math grade, the session, the exam session and mastery, and toasts «راجع معلمك إحدى إجاباتك، وأصبحت درجتها نهائية.».

## Options

| Key | Default | Notes |
|---|---|---|
| `GradeReview:CommentMaxLength` | 2000 | 1 to 10000 characters in a review note |
| `GradeReview:QueueMaxPageSize` | 50 | 1 to 100 rows per queue page |

## Web

- `/teacher/grades?subjectId&kind&page` «مراجعة التصحيح»: subject pills with counts (a missing or unassigned `subjectId` opens the first assigned subject), kind tabs, the list (question excerpt, unit › lesson, reason, waiting time, AI score or «بدون درجة آلية», confidence), paging. States: loading, error with retry, no subjects, nothing waiting.
- `/teacher/grade/$subjectId/$kind/$gradeId` (`essay` or `math-steps`): the question, the grading key, the student's answer, the AI grading, then the decision form (accept disabled without an AI score; score and note for an override) or, once reviewed, the reviewed card. States: loading, error with retry, not found. A save returns to the list; a 409 toasts that another teacher reviewed it.
- The teacher tab bar is three tabs (review queue, AI grades, student questions) plus «المزيد» (`/teacher/more`: stats).
- Student: `TeacherReviewNote` under the essay and math grade results.
