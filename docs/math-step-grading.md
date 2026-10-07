# Math step grading (#123)

## Overview

A `MathSteps` question (v2) can carry a **model solution** and a **steps weight**. When the weight is above 0, the LLM (OpenAI-compatible, [ai-service.md](ai-service.md)) grades the student's working against the model solution: 0, 1 or 2 points per model-solution step, each with a one-sentence reason, plus a one-paragraph Arabic justification and a confidence. The AI service does the grading (`POST /v1/math-step-grades`, [ai-service.md](ai-service.md)); the .NET API owns everything else:

- The step credit is combined with the CAS final-answer verdict ([math-cas.md](math-cas.md)) into one score, and the score always comes from `MathStepsGrader.Combine`, never from the model's own total.
- Students are graded **asynchronously** whenever step grading is needed or the final-answer check failed. Each deferred answer gets one `MathStepGrade` row, which the `math-step-grading` background worker checks, grades and applies, retrying with backoff. The student polls the grade.
- The admin «جرّب الإجابة» in the question editor grades **synchronously**: `POST /api/questions/grade-draft` calls the CAS and the step grader directly, stores nothing and does not retry.
- A grade whose confidence is below the threshold, a grading failure after the retries, or a final answer the CAS still cannot check after the retries waits for teacher review (PRD §6.1, §8.3). The student sees «قيد المراجعة».

## Spec

Two optional fields on the MathSteps grading spec ([question-schemas.md](question-schemas.md), MathSteps):

- `modelSolution`: the solution steps in order, one LaTeX string each (at most `Content:QuestionModelSolutionStepsMaxCount`, 20, each at most `Content:QuestionModelSolutionStepMaxLength`, 500 characters, none blank).
- `stepsWeight`: a whole number from 0 to 100, the percentage of the score given to the steps. A missing value means 0. A weight above 0 needs at least one model solution step.

Both are omitted from the canonical spec when empty or 0, so final-only questions keep their spec byte-for-byte.

## Combine rule

With w = `stepsWeight`, F = 1 when the verdict is `equivalent` (else 0) and S = Σ step points ÷ (2 × model steps):

normalised = ((100 − w) × F + w × S) ÷ 100

It is scaled by the max score and rounded like every other type (`score` to 2 decimals, `normalisedScore` to 4). When w is 0 or there is no model solution, the grade is the final-only #122 grade. The attempt's feedback line is «خطوات صحيحة كاملة: {right} من {total}.» / "Fully correct steps: {right} of {total}." (steps with 2 points out of the model steps).

**Example:** w = 50, three model steps graded 2, 1 and 0, correct final answer: (50 × 1 + 50 × 3/6) ÷ 100 = 0.75. With a max score of 4 the score is 3.

Other rules:

- A wrong, unreadable or wrong-form final answer gives F = 0, but the steps still earn their credit.
- No non-blank student step with w > 0: every step earns 0, with no AI call (score = (100 − w)% × F).
- A blank final answer is Unanswered (0), with no CAS and no AI call, even with steps (as in #122).

## When grading is deferred

`AnswerGrader.DecideAsync` decides for every MathSteps answer (quiz answer, exam submit):

1. Blank final answer → graded now (Unanswered).
2. CAS verdict `unchecked` → deferred without a verdict; the worker retries the check.
3. Step grading needed (w > 0, a model solution, at least one non-blank student step) → deferred with the verdict.
4. Otherwise → graded now with the combined rule.

A deferred quiz answer is saved on the item (`SavedAnswer`), and the response has `attempt: null` and `pendingAnswer`. There is no attempt until the grade is applied, so an `unchecked` check never becomes a dead-end attempt. A deferred exam answer requests a `MathStepGrade` at submit (time 0, `RequestedAt` = the submission time) instead of an attempt.

## Model

`MathStepGrade` (`Elmanhg.Domain.MathStepGrading`, table `MathStepGrades`) is a mutable aggregate, separate from the append-only `Attempts`.

| Column | Type | Notes |
|---|---|---|
| `StudentId`, `SessionId`, `QuestionId`, `SubjectId` | uuid | foreign keys (restrict); `SubjectId` scopes the #128 teacher queue |
| `QuestionVersion`, `MaxScore` | int | the served revision and its max score |
| `Answer` | jsonb | the canonical `{steps, finalAnswer}` |
| `FinalAnswerVerdict` | text, null | the CAS verdict; null until the final answer is checked |
| `Status` | text | `Pending`, `InReview` or `Graded` |
| `ReviewReason` | text, null | `LowConfidence`, `GradingFailed` or `FinalAnswerUnchecked` when `InReview` |
| `Attempts`, `NextAttemptAt`, `LastErrorCode` | | the retry schedule (`RetrySchedule`, Core.DDD, mapped onto these columns); `LastErrorCode` holds at most 100 characters |
| `RequestedAt`, `GradedAt` | timestamptz | truncated to microseconds |
| `Score` (9,2), `NormalisedScore` (5,4) | numeric, null | from `MathStepsGrader.Combine` |
| `Feedback` | jsonb, null | the grade's feedback (`MathStepTally` or the final-only line) |
| `Steps` | jsonb, null | `[{stepIndex, step, points, maxPoints, justification}]` in model order; null without an AI call |
| `Justification` | text, null | the AI's paragraph to the student |
| `Confidence` (5,4) | numeric, null | 0 to 1; null without an AI call |
| `Model`, `PromptVersion` | text, null | from the successful call |
| `InputTokens`, `OutputTokens`, `CostUsd` (12,6) | | the successful call's usage and cost |
| `TimeTakenMilliseconds` | int | the quiz answer time measured at submission; 0 for exam answers |
| `AppliedAt` | timestamptz, null | when a `Graded` grade was applied to its session |
| `ReviewDecision`, `ReviewedBy`, `ReviewedAt`, `ReviewComment`, `ReviewedScore` (9,2), `ReviewedNormalisedScore` (5,4) | null until reviewed | the teacher review (#128); the AI `Score` is never overwritten ([grade-review.md](grade-review.md)) |

Indexes: unique `(SessionId, QuestionId)` (`IX_MathStepGrades_SessionId_QuestionId`), `NextAttemptAt` filtered to `Pending`, `GradedAt` filtered to `Graded` with a null `AppliedAt`, and `(SubjectId, Status)`. The row version is PostgreSQL `xmin`.

## Lifecycle

- `MathStepGrade.Request(...)` creates a `Pending` grade that is due immediately. A blank final answer is rejected (it is graded directly), and an `unchecked` verdict is stored as no verdict.
- Every `MathStepGrading:SweepIntervalSeconds` the worker lists up to `SweepBatchSize` due ids and, for each, in one scope, sends `CheckMathStepAnswerCommand`, `GradeMathStepsCommand` and `ApplyMathStepGradeCommand`, each with one save.
- **Check:** a due grade without a verdict calls the CAS with the served revision's rules. `unchecked` again throws `MATH_CHECK_UNAVAILABLE` (a failed attempt); any other verdict is stored (`RecordVerdict`), so a later grading failure does not lose it.
- **Grade:** a due grade with a verdict calls the step grader when step grading is needed, and completes the grade with the combined score (`Complete`). A final-only or no-steps grade completes without an AI call. `Graded`, or `InReview` / `LowConfidence` when the AI's confidence is below `ReviewConfidenceThreshold`.
- **Apply:** a `Graded` grade with a null `AppliedAt` writes the session's `Attempt` (`GradedBy = AI`, `CreatedAt = RequestedAt`, the stored feedback), updates mastery (not in test mode), recomputes `ScorePercent` when the session is finished, and stamps `AppliedAt`. The attempt raises `AttemptsRecorded`, so its training row is written in the same save ([training-data.md](training-data.md)).
- **Failure:** the worker logs a warning and sends `FailMathStepGradeCommand` with the exception's error code (or its type name). The next try is at `RetryBaseDelaySeconds × 2^(attempt − 1)`. After `MaxAttempts` the grade becomes `InReview` with reason `FinalAnswerUnchecked` when there is still no verdict, else `GradingFailed`.
- Only `Pending` grades are checked, graded or failed (`MATH_STEP_GRADE_NOT_PENDING` otherwise). `Graded` is final; `InReview` waits for a teacher review, which sets `Graded`.
- Kill switch: `MathStepGrading:SweepEnabled=false` stops the worker (the test host sets it).

## What the grader receives

- The question stem as plain text, the model solution steps, the accepted answers, the student's non-blank steps and final answer (trimmed), plus the subject name and lesson objectives when the lesson still exists. Context fields are cut to `MathStepGrading:ContextFieldMaxLength`.
- Never: the CAS verdict (so it cannot bias the step marks), the student id or name, or the session id.

## Prompt-injection hygiene

The student's work is untrusted input. The defences mirror essay grading ([essay-grading.md](essay-grading.md)):

1. The system prompt is a static file with no untrusted text.
2. The context is JSON inside `<grading_context>` and the work is JSON inside `<student_work>`, in the single user turn.
3. Both tags are removed from every string field before the turn is built (`prompts/delimiters.py`).
4. The prompt says the work is data; an injection attempt means grading the mathematics only and setting confidence 0.3 or lower, which sends the grade to teacher review.
5. The reply is schema-validated in both services: every model step index exactly once, points 0 to 2, totals consistent.
6. The API recomputes the score with `MathStepsGrader.Combine`.
7. The web renders the justifications as escaped plain text, and neither service logs the student's work.
8. The eval set has six safety cases ([ai-service.md](ai-service.md), Evaluate math step grading).

## Rate limit

Quiz MathSteps answers with a non-blank final answer are limited per student (`MathStepGrading:CheckPermitLimit` checks per `CheckWindowSeconds`, a fixed window; 10 per 60 s by default). A denied answer returns `429 TOO_MANY_REQUESTS` and stores nothing. A replay of the same answer never calls the CAS or the limiter. Exam submits are not limited.

## Student API

`GET /api/sessions/{sessionId}/questions/{questionId}/math-step-grade` (policy `Assessments.Take`) returns the caller's own grade as `MathStepGradeResult`: `id`, `status`, `maxScore`, `requestedAt`, and, only when the status is `Graded`, `gradedAt`, `score`, `normalisedScore`, `outcome`, `finalAnswerVerdict`, `justification` and `steps`, plus `review` (`{ decision, comment, reviewedAt }`, or null). A grade that is `Graded` but not yet applied reads `Pending`. Another student's grade, or a missing one (a final-only answer graded at once), returns `404 MATH_STEP_GRADE_NOT_FOUND`.

The web polls it every 2 s while `Pending` (`useMathStepGrade`). `MathStepGradeStatus` shows «جارٍ تصحيح إجابتك…», «قيد المراجعة» or the verdict with the final-answer verdict, the marks per step and the justification, and renders nothing on a 404. In the quiz card a pending answer is shown read-only; once applied, the normal feedback panel shows the score and the step marks follow it.

## Admin test grader

`POST /api/questions/grade-draft` grades a MathSteps draft synchronously. A step-graded draft adds `mathSteps` to the response (the steps, final-answer verdict, justification, confidence, model, prompt version and cost). The request may carry `lessonId` for the subject and objectives; an unknown lesson returns `404 LESSON_NOT_FOUND`. An `unchecked` CAS still returns the provisional 0 with `mathUnchecked` feedback and no step grading. A step-grader failure returns `503 MATH_STEP_GRADING_UNAVAILABLE`, with no retry.

## Options

| Key | Default | Notes |
|---|---|---|
| `MathStepGrading:SweepEnabled` | `true` | the worker's kill switch |
| `MathStepGrading:SweepIntervalSeconds` | 10 | 1 to 3600 |
| `MathStepGrading:SweepBatchSize` | 5 | 1 to 100 grades per sweep |
| `MathStepGrading:MaxAttempts` | 4 | 1 to 10 |
| `MathStepGrading:RetryBaseDelaySeconds` | 30 | 1 to 3600; doubles each retry |
| `MathStepGrading:ReviewConfidenceThreshold` | 0.7 | 0 to 1; a lower confidence goes to teacher review. The default of the runtime setting `grading.mathStepReviewConfidenceThreshold` ([configuration.md](configuration.md)) |
| `MathStepGrading:ContextFieldMaxLength` | 20000 | 1 to 100000 characters per context field |
| `MathStepGrading:CheckPermitLimit` | 10 | 1 to 1000 quiz math checks per student per window |
| `MathStepGrading:CheckWindowSeconds` | 60 | 1 to 3600 |
| `AiService:MathStepGradingTimeoutSeconds` | 100 | the step client's attempt and total timeout; above the AI service's worst case of about 91 s |
| `Content:QuestionModelSolutionStepsMaxCount` | 20 | model solution steps per question |
| `Content:QuestionModelSolutionStepMaxLength` | 500 | characters per model solution step |

With `AiService:Provider=Fake` (the default), `FakeAiMathStepGradingClient` awards every model step 2 points with confidence 0.9 and model `fake`, and refuses in Production.

## Error codes

| Code | Status | When |
|---|---|---|
| `QUESTION_MATH_MODEL_SOLUTION_INVALID` | 422 | too many model solution steps, or a step blank or too long |
| `QUESTION_MATH_MODEL_SOLUTION_REQUIRED` | 422 | `stepsWeight` above 0 with no model solution |
| `QUESTION_MATH_STEPS_WEIGHT_INVALID` | 422 | `stepsWeight` below 0 or above 100 |
| `TOO_MANY_REQUESTS` | 429 | the per-student math check limit is reached |
| `MATH_STEP_GRADE_NOT_FOUND` | 404 | no step grade for this session, question and student |
| `MATH_STEP_GRADING_UNAVAILABLE` | 503 | the step grader failed, timed out or returned an invalid reply; the fake in Production |
| `MATH_STEP_GRADE_NOT_PENDING` | 409 | the worker tried to change a grade that is no longer pending (worker only) |

## Teacher review (#128)

`InReview` math step grades are listed in the teacher review queue, scoped by `SubjectId` and shown with their reason. See [grade-review.md](grade-review.md).

- **Accept** is allowed only when the AI produced a score (`LowConfidence`). `GradingFailed` and `FinalAnswerUnchecked` grades can only be overridden (400 `GRADE_REVIEW_NO_AI_SCORE`).
- **Override** takes a total score from 0 to the max score (2 decimals) and a required note. There are no per-step marks.
- The review writes the attempt in the same save, with `GradedBy = Teacher`, mastery and `ScorePercent`. An accepted grade keeps its stored feedback; an overridden one has none.
- Student result: after an override, `steps` is `[]`, `justification` is null, and `finalAnswerVerdict` is null if the CAS never checked the answer. An accepted grade keeps the AI detail. Both carry `review`.
- Legacy `mathUnchecked` attempts from before #123 are not reviewed (no live deploy; [math-cas.md](math-cas.md)).
