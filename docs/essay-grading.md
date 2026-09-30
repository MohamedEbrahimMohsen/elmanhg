# Essay grading (#118)

## Overview

Essays (question type `Essay`, v2) are graded by Claude against the rubric a teacher wrote for the question ([question-schemas.md](question-schemas.md), Essay). The AI service (`POST /v1/essay-grades`, [ai-service.md](ai-service.md)) returns points per rubric criterion with a reason for each, a one-paragraph Arabic justification and a confidence. The .NET API owns everything else:

- Students are graded **asynchronously**. Each answered essay gets one `EssayGrade` row, which the `essay-grading` background worker grades, retrying with backoff. The student polls the grade.
- The admin «جرّب الإجابة» in the question editor grades **synchronously**: `POST /api/questions/grade-draft` calls the grader directly, stores nothing and does not retry.
- The score always comes from the rubric (`QuestionGrader.GradeEssay`), never from the model's own total.
- A grade whose confidence is below the threshold, or one the AI could not produce, waits for teacher review (PRD §6.1). The student sees «قيد المراجعة».

## Model

`EssayGrade` (`Elmanhg.Domain.EssayGrading`, table `EssayGrades`) is a mutable aggregate, separate from the append-only `Attempts`, so the worker never touches the session row.

| Column | Type | Notes |
|---|---|---|
| `StudentId`, `SessionId`, `QuestionId`, `SubjectId` | uuid | foreign keys (restrict); `SubjectId` scopes the #128 teacher queue |
| `QuestionVersion`, `MaxScore` | int | the served revision and its max score |
| `Answer` | jsonb | `{"text": "…"}`, trimmed |
| `Status` | text | `Pending`, `InReview` or `Graded` |
| `ReviewReason` | text, null | `LowConfidence` or `GradingFailed` when `InReview` |
| `Attempts`, `NextAttemptAt`, `LastErrorCode` | | the retry schedule; `LastErrorCode` holds at most 100 characters |
| `RequestedAt`, `GradedAt` | timestamptz | truncated to microseconds |
| `Score` (9,2), `NormalisedScore` (5,4) | numeric, null | from `QuestionGrader.GradeEssay` |
| `Criteria` | jsonb, null | `[{criterionId, title, points, maxPoints, justification}]` in rubric order |
| `Justification` | text, null | the AI's paragraph to the student |
| `Confidence` (5,4) | numeric, null | 0 to 1 |
| `Model`, `PromptVersion` | text, null | from the successful call |
| `InputTokens`, `OutputTokens`, `CostUsd` (12,6) | | the successful call's usage and cost |

Indexes: unique `(SessionId, QuestionId)` (`IX_EssayGrades_SessionId_QuestionId`), `NextAttemptAt` filtered to `Pending` (the worker's due query), and `(SubjectId, Status)` for the teacher queue. The row version is PostgreSQL `xmin`: when two API replicas grade the same row, the losing save fails and the worker records a failed attempt.

## Lifecycle

- `EssayGrade.Request(...)` creates a `Pending` grade that is due immediately. A blank essay is rejected: #119 grades blank essays directly as Unanswered, without the AI.
- Every `EssayGrading:SweepIntervalSeconds` the worker lists up to `SweepBatchSize` due ids and sends `GradeEssayCommand` for each, in a new scope per item.
- On success, `Complete` stores the result and sets `Graded`, or `InReview` / `LowConfidence` when the confidence is below `ReviewConfidenceThreshold`. The score is stored either way.
- On a failure the worker logs a warning and sends `FailEssayGradeCommand` with the exception's error code (or its type name). `FailAttempt` schedules the next try at `RetryBaseDelaySeconds × 2^(attempt − 1)` (30, 60 and 120 s by default). After `MaxAttempts` (4) the grade becomes `InReview` / `GradingFailed`.
- Only `Pending` grades are graded or failed (`ESSAY_GRADE_NOT_PENDING` otherwise). `Graded` is final; `InReview` waits for #128.
- Kill switch: `EssayGrading:SweepEnabled=false` stops the worker (the test host sets it).

## Score

The grade uses the #117 formula through `QuestionGrader.GradeEssay`: Σ awarded points ÷ Σ criterion points, scaled by the max score and rounded like every other type (`score` to 2 decimals, `normalisedScore` to 4). `EssayGrader` throws when the awards do not match the rubric (a criterion missing, repeated or unknown, or points outside 0 to the criterion's points). The job grades against the `QuestionRevision` at `EssayGrade.QuestionVersion` (`QuestionRevision.GradeEssay`), so an edit after the student answered never changes the rubric used.

## What the grader receives

- The question stem, the rubric (criterion ids, titles, descriptions, points and levels), the model answers and the essay, plus the subject name and the lesson objectives when the lesson still exists. The stem, model answers and objectives are sent as plain text, and an image contributes only its alt text. Authoring rejects a model answer with no readable text (an image with an empty alt), so none is dropped for a current question; the drop of empty model answers stays as a guard for older revisions. Every context field is cut to `EssayGrading:ContextFieldMaxLength`.
- Never: the student id or name, the session id, or anything else about the student.

## Prompt-injection hygiene

The essay is untrusted input. The defences are:

1. The system prompt is a static file with no untrusted text.
2. The context is JSON inside `<grading_context>`, and the essay is inside `<student_essay>`, in the single user turn.
3. `grading_context` and `student_essay` tags are removed from every string field before the turn is built. The removal is linear-time and repeats until the text stops changing (`prompts/delimiters.py`).
4. The prompt says the essay is data; an injection attempt means grading the content only and setting confidence 0.3 or lower, which sends the grade to teacher review.
5. Structured output plus checks in both services mean the model cannot award off-rubric points.
6. The API recomputes the score from the rubric.
7. The web renders the justification as escaped plain text.
8. The essay is never logged.
9. The eval set has six safety cases that check all of this ([ai-service.md](ai-service.md), Evaluate essay grading).

## Cost

The AI service logs `essay_grading.completed` (tokens, latency, `cost_usd`, model, prompt version) and adds to the `elmanhg.ai.cost` metric. The API stores the model, prompt version, tokens and cost on each `EssayGrade`, so spend can be queried per grade and per day.

## Student API

`GET /api/sessions/{sessionId}/questions/{questionId}/essay-grade` (policy `Assessments.Take`) returns the caller's own grade as `EssayGradeResult`: `id`, `status`, `maxScore`, `requestedAt`, and, only when the status is `Graded`, `gradedAt`, `score`, `normalisedScore`, `outcome`, `justification` and `criteria`. A pending or in-review grade never shows the AI score. Another student's grade, or a missing one, returns `404 ESSAY_GRADE_NOT_FOUND`.

The web polls it every 2 s while the status is `Pending` (`useEssayGrade`), and `EssayGradeStatus` shows «جارٍ تصحيح إجابتك…», «قيد المراجعة» or the verdict with the score, criterion marks and justification. Both are exported from the quiz feature for #119. There is no push.

## Admin test grader

`POST /api/questions/grade-draft` grades an essay draft synchronously. The request may carry `lessonId`; the subject name and objectives are then sent too, and an unknown lesson returns `404 LESSON_NOT_FOUND`. A blank essay scores 0 (Unanswered) without calling the grader. The response adds `essay` with the criteria, justification, confidence, model, prompt version and cost. A grader failure returns `503 ESSAY_GRADING_UNAVAILABLE`, with no retry.

## Options

| Key | Default | Notes |
|---|---|---|
| `EssayGrading:SweepEnabled` | `true` | the worker's kill switch |
| `EssayGrading:SweepIntervalSeconds` | 10 | 1 to 3600 |
| `EssayGrading:SweepBatchSize` | 5 | 1 to 100 grades per sweep |
| `EssayGrading:MaxAttempts` | 4 | 1 to 10; after the last failure the grade is `InReview` / `GradingFailed` |
| `EssayGrading:RetryBaseDelaySeconds` | 30 | 1 to 3600; doubles each retry |
| `EssayGrading:ReviewConfidenceThreshold` | 0.7 | 0 to 1; a lower confidence goes to teacher review |
| `EssayGrading:ContextFieldMaxLength` | 20000 | 1 to 100000 characters per context field sent to the grader |
| `AiService:EssayGradingTimeoutSeconds` | 100 | the essay client's attempt and total timeout; above the AI service's worst case of about 91 s |
| `Content:QuestionEssayAnswerMaxLength` | 20000 | the longest essay `grade-draft` accepts |

With `AiService:Provider=Fake` (the default), `FakeAiEssayGradingClient` awards every criterion its full points with confidence 0.9 and model `fake`, and refuses in Production.

## Error codes

| Code | Status | When |
|---|---|---|
| `QUESTION_ESSAY_ANSWER_TOO_LONG` | 422 | a `grade-draft` essay is longer than `Content:QuestionEssayAnswerMaxLength` |
| `QUESTION_ANSWER_INVALID` | 422 | the essay answer has no string `text` |
| `QUESTION_MODEL_ANSWER_REQUIRED` | 422 | a model answer has no readable text (no text, formula or image alt) as sent or after sanitising |
| `ESSAY_GRADE_NOT_FOUND` | 404 | no grade for this session, question and student |
| `ESSAY_GRADING_UNAVAILABLE` | 503 | the AI grader failed, timed out or returned an invalid reply; the fake in Production |
| `ESSAY_GRADE_NOT_PENDING` | 409 | the worker tried to grade or fail a grade that is no longer pending (worker only) |

## What #119 adds

- Student essay input and submission, which calls `EssayGrade.Request`.
- Writing the `Attempt` and mastery from a final `EssayGrade`, and adding essays to the session `ScorePercent`.
- Grading a blank essay without the AI.
- Removing the essay exclusion from `ServableQuestionSpecification`.
- Mounting `EssayGradeStatus` in the quiz and exam screens.

## What #128 adds

The teacher review queue for `InReview` grades (scoped by `SubjectId`), and accepting or overriding a grade, which makes it final.
