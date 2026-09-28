# Unit exams

A **unit exam** is a session of kind `UnitExam` (`docs/sessions.md`) built from the unit's exam blueprint (`docs/exam-blueprints.md`, PRD §7.4). All questions are on one page, every answer is auto-saved as a draft with no feedback, and the whole exam is graded when it is submitted. [Multi-unit exams](#multi-unit-exams) (#82) reuse the sitting endpoints; best score and the attempts list are #83.

## Start and resolution

`POST /api/exams/units/{unitId}` starts or resumes the exam.

1. The unit must exist (404 `UNIT_NOT_FOUND`).
2. **One open exam per student.** If the student has an open exam (any kind):
   - for this unit, it is resumed with no new draw; if it is past `Deadline + Exams:DeadlineGraceSeconds`, it is submitted first and returned submitted;
   - for another unit, the call returns 409 `EXAM_ALREADY_IN_PROGRESS`.
   The unique partial index `IX_Sessions_OneOpenExam` backs the rule; a concurrent start that loses the race gets the same 409.
3. **Resolution.** `ExamBlueprintResolution.ForUnit`: the unit's blueprint, else the subject default, else none (400 `UNIT_EXAM_NO_BLUEPRINT`).
4. **Servability.** `ExamBlueprint.EnsureServable` re-runs the shortfall check against the unit's live servable pool (400 `EXAM_SHORTFALL`, context `types`, for example `"Mcq 1/2"`).
5. The session copies `TimeLimitMinutes` and `PassMark` from the blueprint and sets `Deadline = StartedAt + TimeLimitMinutes` (null when untimed). A later blueprint edit never changes a started exam.
6. An Admin gets a test-mode exam (`IsTestMode = true`): no mastery and no history effect, but it still counts as the admin's one open exam.

The lesson-open gate of PRD §7.4 ships with its default (no gate); it needs lesson-open tracking from #85.

## Selection

`ExamQuestionSelector.Select` is a pure domain function with an injected `Random`. The candidates are the unit's servable questions (`GetServableExamCandidatesAsync`, across every lesson of the unit). A question is **mastered** when the student's `QuestionMastery.IsMastered` is true (`docs/mastery.md`).

Per blueprint type, in `QuestionType` order:

1. Sort the type's candidates by id (so the result depends only on the candidates, the mastery set and the seed).
2. With a difficulty mix, apportion the type's count across Easy/Medium/Hard by largest remainder (`ExamDifficultyTargets.Apportion`; ties go Easy, then Medium, then Hard), and take up to each target from that difficulty.
3. Fill the rest of the count from the type's remaining candidates of any difficulty.
4. Every "take" prefers not-mastered questions: the shuffled not-mastered candidates first, then the shuffled mastered ones.
5. Items are grouped by type in `QuestionType` order; within a type, Easy, then Medium, then Hard, random within a difficulty.

The difficulty mix is a target followed as far as the pool allows. A short pool yields what exists; the start-time servability check has already rejected a pool below the type counts.

Example: 10 Mcq with a 30/50/20 mix targets 3 Easy, 5 Medium and 2 Hard. With no Hard question in the pool, the two missing places are filled from the remaining Easy and Medium questions, not-mastered first.

## Sitting

- `PUT /api/exams/{sessionId}/answers/{questionId}` saves the canonical answer on the item (`SessionItem.SavedAnswer`, jsonb, and `AnswerSavedAt`). It overwrites the previous draft and creates no attempt. A wrong shape for the served type is 422 `QUESTION_ANSWER_INVALID`; any readable shape is kept, including a cleared answer (which grades 0).
- A save after `Deadline + Exams:DeadlineGraceSeconds` (30 s) returns 400 `EXAM_TIME_EXPIRED`. The grace absorbs the client's last in-flight save at 0:00.
- A save on a submitted exam returns 400 `SESSION_ALREADY_SUBMITTED`.
- **What is revealed.** While open, items carry `savedAnswer` only; `attempt`, `correctAnswer` and `explanation` are null. After submission, every item carries `correctAnswer` and `explanation`, and answered items carry `attempt`.
- `GET /api/exams/{sessionId}` never mutates; a refresh resumes with the saved answers.
- The quiz endpoints (`/api/sessions/{id}/answers` and `/finish`) return 404 `SESSION_NOT_FOUND` for an exam id.

## Submission

`POST /api/exams/{sessionId}/submit` is always allowed, also after the deadline, and grades what was saved (saves are already closed, so a late submit cannot add answers).

- Every item with a saved answer gets one append-only `Attempt`, graded through `QuestionRevision.Grade` at the served version. Unanswered items get no attempt and count 0.
- `ScorePercent = round(Σ attempt.Score / Σ item.MaxScore × 100, 2)`, the session formula. `IsPassed = ScorePercent >= PassMark`.
- Exam attempts store `TimeTakenMilliseconds = 0`: all questions share one page, so per-question time is not observable. The exam's time is `elapsedMilliseconds = (SubmittedAt ?? now) − StartedAt`.
- In a non-test exam each new attempt updates `QuestionMastery` exactly as a quiz answer does, in the same save. The day streak stays quiz-only.
- Submitting twice returns the same result and writes nothing new.

## Auto-submit

An expired exam is submitted by whichever comes first:

1. **The client** submits when its countdown reaches 0 or a save returns `EXAM_TIME_EXPIRED`.
2. **Start** on an expired open exam of the same unit submits it and returns it.
3. **The worker.** `ExpiredExamSubmissionWorker` (a `BackgroundService` with a `PeriodicTimer` on the injected `TimeProvider`) runs every `Exams:AutoSubmitIntervalSeconds`. Each tick lists up to `Exams:AutoSubmitBatchSize` open exams whose `Deadline + grace` has passed (`GetExpiredExamSessionIdsQuery`, index `IX_Sessions_OpenExamDeadline`) and sends one `AutoSubmitExamCommand` per session in its own scope, so one failing session never blocks the rest. Failures are logged. A failed id is left out of later batches until a tick lists fewer than a full batch (the end of the backlog), then retried, so failing exams cannot hold the head of every batch. Only the host's shutdown ends the loop; any other exception, including a cancellation that is not a shutdown, is logged and the loop continues. `Exams:AutoSubmitEnabled` turns the worker off (the integration tests do this; the handlers are tested directly).

Untimed exams never expire and stay open until submitted.

## Breakdown and weakest objectives

The result of a submitted exam has two breakdowns. Each item is placed by its live question's lesson and objective; lesson and objective text and order come from the live lesson. An item whose question or lesson cannot be loaded is left out of the breakdown but still counts in the score; a soft-deleted objective is ignored.

- **Per lesson.** `Σ score / Σ maxScore × 100` (2 decimals) per lesson, with the question count and the correct count (normalised score ≥ `Mastery:CorrectThreshold`), ordered by lesson order.
- **Weakest objectives.** The same ratio per objective, keeping only objectives below 100 %, ordered ascending by percent, then lesson order, objective order and id, and cut to `Exams:WeakestObjectiveCount` (3). An empty list means no weak objectives.

## Multi-unit exams

A **multi-unit exam** (#82, PRD §7.5) is a session of kind `MultiUnitExam` over **two or more units of one subject**, with a size of exactly **20, 40 or 60** questions (`MultiUnitExamSizes`). It is sat, saved, submitted, auto-submitted and graded through the same endpoints and rules as a unit exam; only the start and the result differ.

**Resolution.** Each selected unit resolves through `ExamBlueprintResolution.ForUnit`: its own blueprint, else the subject default. A unit with neither returns 400 `MULTI_UNIT_EXAM_NO_BLUEPRINT` (context `units`, the unit names joined by `", "`). Units are ordered by unit order, then id; that order is used for tie-breaks, for the scope and for the result.

**Merge** (`MultiUnitBlueprintMerge`, Domain). With `total` = the sum of the selected units' `QuestionCount`, every (unit, type) cell with count `c` gets `floor(c × size / total)`. The places left over go one each to the cells with the largest remainder; ties go to the earlier unit, then the earlier `QuestionType`. The merged type counts are the per-unit cells summed by type. A unit can round to 0 questions; the preview shows it.

Worked example: A = Mcq 10, 30 min, pass 50; B = Mcq 20, 40 min, pass 60; size 20. A gets 6 r20 and B 13 r10, so the one spare place goes to A: A 7, B 13.

**Time limit and pass mark** use only the units with at least one allocated question:

- If any of those blueprints is untimed, the exam is untimed. Otherwise the time is `ceil(Σ TimeLimit_u × n_u / QuestionCount_u)`, clamped to `[1, ExamBlueprints:MaxTimeLimitMinutes]`. In the example: 30·7/10 + 40·13/20 = 47 minutes.
- The pass mark is `round(Σ PassMark_u × n_u / size)`, half away from zero, clamped to 1–100. In the example: (350 + 780) / 20 = 56.5 → 57.
- Both are copied onto the session at start and never change afterwards.

**Selection** (`MultiUnitExamQuestionSelector`, Domain) reuses `ExamQuestionSelector`:

1. For each unit, in order, it draws that unit's planned type counts from the unit's servable pool, each capped at what the unit has, using the unit's own difficulty mix.
2. For each merged type still short, the rest is drawn from the other selected units' remaining questions (no mix), not-mastered first.
3. Items are ordered by type, then difficulty; ties keep unit order.

Only a shortfall of the **union** pool blocks the exam: 400 `EXAM_SHORTFALL`, context `types` (for example `"Mcq 10/20"`).

**Scope.** `MultiUnitExamScope { subjectId, unitIds, size }` is stored as jsonb, with `unitIds` in unit order. The key is `units:{size}:{sorted lowercase unit ids joined by ","}`, so the selection order does not matter and a retake of the same units and size has the same key.

**Start and resume.** `POST /api/exams/subjects/{subjectId}/multi-unit` validates the selection (422 codes below), then loads the subject (404 `SUBJECT_NOT_FOUND`) and the units (404 `UNIT_NOT_FOUND` for a missing unit or a unit of another subject). The one-open-exam rule is unchanged: an open exam with the same key is resumed (and submitted first when past the deadline plus grace), and any other open exam returns 409 `EXAM_ALREADY_IN_PROGRESS`. The exam is planned and drawn only when nothing is open, so a resume never fails on a blueprint edited later. An Admin gets a test-mode exam.

**Preview.** `GET /api/exams/subjects/{subjectId}/multi-unit/preview?unitIds=…&unitIds=…&size=20` returns the merged blueprint (`typeCounts` with the union availability, the time, the pass mark and `difficultyMix: null`), `isAvailable` (false on a union shortfall) and each unit's share. It saves nothing.

**Result.** `ExamSessionResult.subjectId` is the scope's subject, `units` lists every selected unit in scope order (a deleted unit has a null name), and `unitBreakdown` sums the per-lesson shares by each lesson's live unit, in scope order. Only units with at least one placed item appear; the list is empty while the exam is open. A unit exam returns one row. Multi-unit sessions never feed a unit's best score (`docs/progress.md`).

## API

Policy `Assessments.Take` (Students and Admins). Teachers get 403, anonymous callers 401. Another student's session id and a quiz id both return 404 `SESSION_NOT_FOUND`.

| Method | Route | Body | Response |
|---|---|---|---|
| GET | `/api/exams/units/{unitId}` | — | 200 `UnitExamOverviewResult` |
| POST | `/api/exams/units/{unitId}` | — | 200 `ExamSessionResult` (new, resumed or auto-submitted) |
| GET | `/api/exams/subjects/{subjectId}/multi-unit` | — | 200 `MultiUnitExamOverviewResult` |
| GET | `/api/exams/subjects/{subjectId}/multi-unit/preview?unitIds=&unitIds=&size=` | — | 200 `MultiUnitExamPreviewResult` |
| POST | `/api/exams/subjects/{subjectId}/multi-unit` | `{ unitIds, size }` | 200 `ExamSessionResult` (new, resumed or auto-submitted) |
| GET | `/api/exams/{sessionId}` | — | 200 `ExamSessionResult` |
| PUT | `/api/exams/{sessionId}/answers/{questionId}` | `{ answer }` | 200 `ExamAnswerSavedResult` |
| POST | `/api/exams/{sessionId}/submit` | — | 200 `ExamSessionResult` |

`UnitExamOverviewResult { unitId, unitName, subjectId, subjectName, blueprint? { isSubjectDefault, questionCount, typeCounts[] { type, required, available }, difficultyMix?, timeLimitMinutes?, passMark }, isAvailable, inProgressExam? { sessionId, isThisUnit } }`
`MultiUnitExamOverviewResult { subjectId, subjectName, units[] { unitId, name, hasBlueprint, isSubjectDefault, servableCount }, sizes[], inProgressExam? { sessionId, isThisUnit: false } }`
`MultiUnitExamPreviewResult { subjectId, size, blueprint { isSubjectDefault, questionCount, typeCounts[] { type, required, available }, difficultyMix: null, timeLimitMinutes?, passMark }, isAvailable, units[] { unitId, name, questionCount, isSubjectDefault } }`
`ExamSessionResult { id, kind, isTestMode, subjectId?, subjectName?, units[] { unitId, name? }, startedAt, timeLimitMinutes?, deadline?, serverNow, passMark, submittedAt?, scorePercent?, isPassed?, elapsedMilliseconds, items[], lessons[], unitBreakdown[] { unitId, name?, questionCount, correctCount, score, maxScore, scorePercent }, weakestObjectives[] }`
`ExamItemResult { position, questionId, questionVersion, type, stem, body, maxScore, savedAnswer?, answerSavedAt?, attempt?, correctAnswer?, explanation? }`
`ExamLessonResult { lessonId, name?, questionCount, correctCount, score, maxScore, scorePercent }`
`ExamObjectiveResult { objectiveId, text, lessonId, lessonName?, questionCount, scorePercent }`
`ExamAnswerSavedResult { questionId, answerSavedAt }`

`serverNow` lets the client anchor its countdown to the server clock.

## Error codes

| Code | HTTP | When |
|---|---|---|
| `EXAM_ALREADY_IN_PROGRESS` | 409 | Another exam is open for the student (start, or a lost start race). |
| `UNIT_EXAM_NO_BLUEPRINT` | 400 | Neither the unit nor its subject has a blueprint. |
| `MULTI_UNIT_EXAM_NO_BLUEPRINT` | 400 | A selected unit has no blueprint and its subject has no default (context `units`). |
| `MULTI_UNIT_EXAM_UNITS_TOO_FEW` | 422 | Fewer than two units, or no selection. |
| `MULTI_UNIT_EXAM_UNIT_DUPLICATE` | 422 | A unit id appears twice. |
| `MULTI_UNIT_EXAM_SIZE_INVALID` | 422 | The size is not 20, 40 or 60. |
| `EXAM_SHORTFALL` | 400 | The live servable pool is below the blueprint's type counts (context `types`). |
| `EXAM_TIME_EXPIRED` | 400 | A save after the deadline plus the grace period. |
| `SESSION_ALREADY_SUBMITTED` | 400 | A save on a submitted exam. |
| `SUBJECT_ID_REQUIRED`, `SUBJECT_NOT_FOUND`, `UNIT_ID_REQUIRED`, `UNIT_NOT_FOUND`, `SESSION_ID_REQUIRED`, `SESSION_NOT_FOUND`, `SESSION_QUESTION_NOT_FOUND`, `QUESTION_ID_REQUIRED`, `QUESTION_NOT_FOUND`, `QUESTION_ANSWER_INVALID`, `ATTEMPT_ANSWER_TOO_LONG`, `SESSION_MODIFIED_CONCURRENTLY`, `USER_NOT_AUTHENTICATED` | 422 / 404 / 422 / 404 / 422 / 404 / 404 / 422 / 404 / 422 / 422 / 409 / 401 | Reused codes. |

## Options

| Key | Default | Meaning |
|---|---|---|
| `Exams:DeadlineGraceSeconds` | 30 | Seconds after the deadline during which a save is still accepted (0–600). |
| `Exams:AutoSubmitEnabled` | true | Runs the expired-exam worker. |
| `Exams:AutoSubmitIntervalSeconds` | 60 | Seconds between sweeps (5–3600). |
| `Exams:AutoSubmitBatchSize` | 50 | Expired exams handled per sweep (1–1000). |
| `Exams:WeakestObjectiveCount` | 3 | Objectives shown in the weakest list (1–20). |

`Sessions:AnswerMaxLength` caps a saved answer, as for quizzes.

## Student screens (web)

Feature `web/src/features/exam/`.

- **`/student/exam-start/{unitId}`**: "امتحان: {unit}", the blueprint summary (type, count, available; the available cell turns red when short), the time or «مفتوح», the pass mark and the note «لا تظهر الإجابات الصحيحة إلا بعد التسليم. تُحفظ إجاباتك تلقائيًا.». The subject default shows «النموذج الافتراضي للمادة». Actions: «ابدأ الامتحان»; «استكمل الامتحان» when this unit's exam is open; a warning with a link when another exam is open; the shortfall warning with no start button; «لا يوجد امتحان لهذه الوحدة بعد.» with no blueprint. Loading, error-with-retry and RTL states. Reached from the progress unit table until #85 adds the unit page.
- **`/student/exam/{sessionId}`**: a sticky header with the title, the countdown and the save status («محفوظ تلقائيًا», «جارٍ الحفظ…», «تم الحفظ {time}», or the save error). The countdown is anchored to `serverNow`, turns red and is announced once in the last two minutes, and at 0 shows «انتهى الوقت. جارٍ تسليم امتحانك…» and submits. Every question is on the page with its saved answer restored. Each change is saved after 800 ms; saves for one question are chained so the newest lands last, and pending saves are flushed before submitting. «تسليم الامتحان» (sticky at the bottom on mobile) asks for confirmation with the count of unanswered questions. A submitted exam opens its result.
- **`/student/exam-result/{sessionId}`**: the score out of 100 with «ناجح» or «لم تبلغ درجة النجاح ({passMark})», answered count and time, the per-lesson table with «درّب الآن» to `/student/lesson/{lessonId}/practice`, the weakest objectives or «لا توجد أهداف ضعيفة. أحسنت!», a review of every question (the student's answer with feedback, or «لم تُجب عن هذا السؤال.» with the correct answer), «إعادة الامتحان» to the exam start and «تقدّمي». An open exam opens the exam screen.
- **`/student/multi-exam`** (the builder, «امتحان متعدد الوحدات»): a subject select, the subject's units as checkboxes with «({count} سؤال متاح)» (a unit with no blueprint is disabled and says «لا يوجد امتحان لهذه الوحدة»), and the size as radios «20 / 40 / 60 سؤال». With fewer than two units it says «اختر وحدتين على الأقل.»; with two or more it shows the live merged preview «النموذج المدمج (تناسبيًا)» (the blueprint summary and «الأسئلة من كل وحدة»), then «ابدأ الامتحان», or the shortfall warning with no button. When any exam is open it shows «لديك امتحان جارٍ.» with a link and no start button. The selection lives in the URL (`?subjectId=&unitIds=[…]&size=`); changing the subject clears the units and size. Loading, error-with-retry, no-subjects, no-units and RTL states.
- A multi-unit exam is titled «امتحان متعدد: {units}» and its result «نتيجة امتحان متعدد: {units}» (the unit names joined by « + »). The result adds the per-unit table «حسب الوحدة» (unit, score, correct answers), and «إعادة الامتحان» opens the builder with the same subject, units and size.
- Progress history links a finished exam «عرض» to its result and an open one «متابعة» to the exam (`docs/progress.md`).
