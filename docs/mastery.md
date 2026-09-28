# Mastery and the headline counter

Mastery tells a student how much of the platform they have really learned. It is built from the attempt log (`docs/sessions.md`) and feeds the student home page, and later browsing (#85), [progress (#78)](progress.md) and exam selection (E6).

## Definitions

These restate PRD §7.3. The threshold is `Mastery:CorrectThreshold` (0.8).

| Term | Definition |
|---|---|
| Correct attempt | `NormalisedScore` at or above 0.8. Partial credit below 0.8 is wrong. |
| Question mastered | The **two most recent** attempts, by attempt time, are both correct. One wrong attempt clears it. |
| Seen | A servable question the student has attempted at least once outside test mode, in a quiz or a submitted exam. |
| Lesson mastery % | mastered / servable questions in the lesson. |
| Unit / subject mastery % | Weighted by question count: Σ mastered / Σ servable over the lessons. |
| Questions remaining | Servable total − mastered. |

## Model

`QuestionMastery` has one row per student and question. It is materialised from attempts and maps to PRD §15 `QuestionMastery`.

| Field | PRD §15 | Notes |
|---|---|---|
| `StudentId` | `student_id` | FK `Users`, restrict |
| `QuestionId` | `question_id` | FK `Questions`, restrict |
| `IsMastered` | `mastered` | |
| `LatestAttemptId`, `LatestNormalisedScore`, `LatestAttemptedAt` | `latest_*` | The most recent attempt. |
| `PreviousAttemptId?`, `PreviousNormalisedScore?`, `PreviousAttemptedAt?` | `previous_*` | The one before it. Null after the first attempt. |
| `UpdationDate` | `updated_at` | |
| `Version` | — | `xmin` row version (see Concurrency). |

The pair (`StudentId`, `QuestionId`) is unique (`IX_QuestionMasteries_StudentId_QuestionId`).

## Update rule

- `SubmitAnswerHandler` updates mastery in the **same save** as the new attempt, so the two are atomic. There is no domain event.
- Exam attempts (non-test) update mastery when the exam is submitted, in the same save as the submission (`ExamSubmission`, `docs/exams.md`); each attempt applies `Start` or `Record` exactly as a quiz answer does. Saved exam drafts never touch mastery. Test-mode exams never do.
- Only a newly created attempt counts. A replayed answer (the same answer again) does not update mastery a second time.
- Test-mode (admin) sessions never write mastery.
- The first attempt creates the row with `IsMastered = false`, even for a full score: mastery needs two attempts.
- Later attempts are ordered by attempt time, not by arrival:
  - newer than the latest: the latest moves to previous, and the new attempt becomes the latest;
  - older than the latest but newer than the previous (or no previous yet): it replaces the previous;
  - otherwise, or when the attempt is already one of the two slots: nothing changes.
- After a change, `IsMastered` is true only when both slots are at or above the threshold.
- A wrong attempt clears mastery; the student then needs two correct attempts in a row again.
- Changing `Mastery:CorrectThreshold` affects new attempts only. Existing rows are not recomputed.

## Aggregates

- Every aggregate counts **servable** questions only (`ServableQuestionSpecification`: Approved, lesson Published, not retired), joined at read time. Mastered and seen are intersected with servable, so remaining is never negative.
- Nothing that depends on servability is stored (PRD §17 rule 1).
- Percentages are integers 0–100, rounded down: `floor(mastered × 100 / servable)`, and 0 when there are no servable questions. Rounding down never shows 100 % before everything is mastered.
- Unit and subject percentages pool their lessons (Σ mastered / Σ servable), so a lesson with more questions weighs more.

## Headline counter

- `servableTotal`, `masteredCount` and `seenCount` come from one grouped query per lesson, summed. `remainingCount = servableTotal − masteredCount`.
- The total is live; it does not use the 60 s cached `questions:servable-count`, so all numbers come from one snapshot.

## Streak

- The number of consecutive calendar days, in `Progress:StreakTimeZone` (default `Africa/Cairo`), with at least one attempt in a non-test quiz session. Exam attempts do not count toward the streak (PRD §7.6).
- It counts back from today if the student was active today, otherwise from yesterday. Two or more idle days give 0.
- It looks back at most `Progress:StreakMaxDays` (365) days.

## Next recommended lesson

- Candidates: Published lessons with at least one servable question that are not fully mastered.
- The lesson with the lowest mastered / servable ratio wins. Ties go to curriculum order: subject order, unit order, lesson order, then lesson id.
- Null when no lesson qualifies. Free-tier locks are not applied yet (#87).

## Retirement and unpublishing

There is no recalculation job. A `QuestionMastery` row depends only on attempts, which retiring a question or unpublishing a lesson does not change. Every aggregate joins the servable rule at read time, so the change shows on the next read. The rows are kept, so re-publishing a lesson restores its counts.

## Backfill

The `AddQuestionMastery` migration builds rows from existing attempts in non-test sessions (`AddQuestionMastery.BackfillSql`). It uses a fixed threshold of 0.8, because migrations cannot read configuration. Existing rows are left alone (`ON CONFLICT DO NOTHING`).

## Concurrency

Two answers updating the same row at once are serialised by the `xmin` row version. Two first answers for the same question hit the unique index. Both return 409 `SESSION_MODIFIED_CONCURRENTLY`; the whole save rolls back and a retry resolves it.

## API

| Method | Route | Response |
|---|---|---|
| GET | `/api/mastery/overview` | 200 `MasteryOverviewResult` |
| GET | `/api/mastery/subjects/{subjectId}` | 200 `SubjectMasteryDetailResult` |

`MasteryOverviewResult { headline { servableTotal, masteredCount, remainingCount, seenCount }, streakDays, nextLesson? { lessonId, lessonName, subjectId, subjectName, masteryPercent }, subjects[] { subjectId, name, servableCount, masteredCount, seenCount, masteryPercent } }`
`SubjectMasteryDetailResult { subjectId, name, servableCount, masteredCount, seenCount, masteryPercent, units[] { unitId, name, …counts, masteryPercent, lessons[] { lessonId, name, …counts, masteryPercent } } }`

The subject detail lists every unit (order, then creation date) and its Published lessons (same order). A lesson without servable questions shows 0/0/0 %.

| Code | HTTP | When |
|---|---|---|
| `USER_NOT_AUTHENTICATED` | 401 | No signed-in user. |
| — | 403 | The caller is not a Student. |
| `SUBJECT_NOT_FOUND` | 404 | Unknown subject. |
| `SUBJECT_ID_REQUIRED` | 422 | Empty subject id. |

## Options

| Key | Default | Meaning |
|---|---|---|
| `Mastery:CorrectThreshold` | 0.8 | Correct-attempt threshold, shared with selection (`docs/sessions.md`). |
| `Progress:StreakTimeZone` | `Africa/Cairo` | IANA time zone for streak days. The app fails to start on an unknown id. |
| `Progress:StreakMaxDays` | 365 | How far back the streak looks (1–3650). |

## Access

Both endpoints require `Progress.ViewOwn` (PRD §16), which only Students hold. Admin views of a student's progress come in #106.

The progress endpoints (`docs/progress.md`) use the same policy.

## Student home (web)

`/student` shows:
- the greeting "أهلًا {name}";
- the headline card: "متبقّي لك X سؤال من Y", plus seen, mastered and the day streak;
- the suggested next lesson with its mastery bar and "درّب الآن", which opens `/student/lesson/{id}/practice`;
- one card per subject with a mastery bar and its available questions. Cards are not links until #85 adds subject pages.

It has loading, error-with-retry and empty states. Every quiz answer and every exam submission invalidates the `/api/mastery*` queries, so the numbers are fresh when the student returns home.
