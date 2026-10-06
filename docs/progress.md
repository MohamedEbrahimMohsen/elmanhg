# Progress page

The progress page (`/student/progress`, "تقدّمي") shows a student how far they have come. It implements PRD §7.6 and reads the same mastery data as the student home page (`docs/mastery.md`) and the attempt log (`docs/sessions.md`).

## The page

The page has four sections. Each one loads, fails and retries on its own.

1. **Summary.** The headline counter and the day streak come from `GET /api/mastery/overview`, the same data and rules as the student home. The streak is student-wide (consecutive days with at least one non-test quiz attempt), so it is shown once here, not per subject.
2. **Subjects.** One card per subject with its mastery bar and a unit table: unit mastery % and best unit-exam score. The subject name links to `/student/subject/{id}` and each unit name to `/student/unit/{id}` ([browsing](browsing.md)). The score is rounded to a whole percent, and "—" means no exam yet. The last column links «امتحان الوحدة» to the unit's exam start, `/student/exam-start/{unitId}`.
3. **Weak spots.** The weakest lessons and the weakest objectives, each with "درّب الآن".
4. **History.** A paged table of the student's quiz and exam sessions, filterable by all, quizzes or exams.

## Subject progress

- Every subject (order, then creation date) is listed. Within it, every unit (order, then creation date) is listed.
- Unit and subject counts use `IQuestionMasteryRepository.GetLessonCountsAsync` and `MasteryTotals`, the same weighted rule as `docs/mastery.md`. A unit without servable questions shows 0/0/0 %.
- **Best unit-exam score** is the highest `ScorePercent` over the student's exam sessions, computed by `ISessionRepository.GetBestExamScoresAsync` over `ExamBestScoreSpecification` (submitted, not test mode, not deleted), grouped by `ScopeKey`. A unit matches the key `unit:<guid>`, built by `UnitExamScope` (Domain). The score is null when there is none. Multi-unit exams do not count toward a unit's best (PRD §7.4: the best score is the displayed one).
- Unit exams (#81) use `UnitExamScope` (`docs/exams.md`).

## Weak spots

Both lists use the product's single mastery definition (`docs/mastery.md`), not raw attempt accuracy.

| List | Qualifies when | Order | Size |
|---|---|---|---|
| Weakest lessons | The lesson has servable questions, at least one was seen, and not all are mastered. | mastered / servable ascending, then subject order, unit order, lesson order, lesson id | `Progress:WeakLessonCount` (4) |
| Weakest objectives | Over the servable questions tagged with the objective (`Question.ObjectiveId`): at least one seen, not all mastered. Untagged questions and deleted objectives are ignored. | mastered / servable ascending, then subject, unit, lesson, objective order, objective id | `Progress:WeakObjectiveCount` (3) |

"درّب الآن" opens `/student/lesson/{lessonId}/practice`. For an objective it opens the objective's lesson; there is no objective-scoped quiz. An entry whose lesson, subject or objective was deleted between the two reads is skipped.

## History

- All of the student's sessions outside test mode, open and finished, newest `StartedAt` first (then id descending).
- Filter `kind`: absent = all, `Quiz` = quizzes, `Exam` = unit and multi-unit exams. The filter and page live in the URL (`?kind=Exam&page=2`).
- Paged with `PageData`: `pageSize` defaults to 20 and may not exceed `Progress:HistoryMaxPageSize` (50).
- **Scope name.** A quiz shows its lesson's name, looked up across all lesson states, so an archived lesson keeps its name. A unit exam shows its unit's name. A multi-unit exam shows its unit names joined by « + »; a deleted lesson, or an exam whose units are all deleted, has no name and shows «غير متاح».
- **Links.** A finished quiz links "عرض" to `/student/quiz-result/{id}`. An open quiz links "متابعة" to `/student/quiz/{id}`. A finished exam links "عرض" to `/student/exam-result/{id}`; an open exam links "متابعة" to `/student/exam/{id}`.
- An open session shows "جارٍ" instead of a score.
- A finished exam whose score equals the best of its scope (same `ScopeKey`) shows «الأفضل» next to its score (`isBestScore`); ties all show it. Best scores are loaded only when the page holds a counted exam (`docs/exams.md`, Retakes and best score).

## API

| Method | Route | Query | Response |
|---|---|---|---|
| GET | `/api/progress/subjects` | — | 200 `SubjectProgressResult[]` |
| GET | `/api/progress/weak-spots` | — | 200 `WeakSpotsResult` |
| GET | `/api/progress/sessions` | `kind?` (`Quiz` \| `Exam`), `pageNumber` = 1, `pageSize` = 20 | 200 `PageData<SessionHistoryItemResult>` |

`SubjectProgressResult { subjectId, name, servableCount, masteredCount, seenCount, masteryPercent, units[] { unitId, name, servableCount, masteredCount, seenCount, masteryPercent, bestExamScorePercent? } }`
`WeakSpotsResult { lessons[] { lessonId, lessonName, subjectId, subjectName, …counts, masteryPercent }, objectives[] { objectiveId, text, lessonId, lessonName, subjectId, subjectName, …counts, masteryPercent } }`
`SessionHistoryItemResult { id, kind (Quiz | UnitExam | MultiUnitExam), lessonId?, unitId?, scopeName?, startedAt, submittedAt?, scorePercent?, isBestScore }`

## Error codes

| Code | HTTP | When |
|---|---|---|
| `USER_NOT_AUTHENTICATED` | 401 | No signed-in user. |
| — | 403 | The caller is not a Student. |
| `SESSION_HISTORY_PAGE_NUMBER_INVALID` | 422 | `pageNumber` below 1, or so large that the row offset passes 2,147,483,647. |
| `SESSION_HISTORY_PAGE_SIZE_INVALID` | 422 | `pageSize` outside `[1, Progress:HistoryMaxPageSize]`. |
| `SESSION_HISTORY_KIND_INVALID` | 422 | `kind` outside the enum. Over HTTP the model binder rejects an unknown name or an out-of-range number first, with a framework 400 (`errors.kind`); the validator code covers in-process callers. |

## Options

| Key | Default | Meaning |
|---|---|---|
| `Progress:WeakLessonCount` | 4 | How many weak lessons to show (1–20). |
| `Progress:WeakObjectiveCount` | 3 | How many weak objectives to show (1–20). |
| `Progress:HistoryMaxPageSize` | 50 | Largest history page (1–100). |

The streak options (`Progress:StreakTimeZone`, `Progress:StreakMaxDays`) are in `docs/mastery.md`.

## Access

All three endpoints require `Progress.ViewOwn` (PRD §16), which only Students hold. Every query filters on the signed-in student, so a student only ever sees their own data. Test-mode sessions are excluded everywhere. Admins see any student's progress through the admin view below.

## Admin view

The admin student page (`/admin/student/$studentId`, [user-administration.md](user-administration.md)) reads a student's progress through two endpoints. Both require `Progress.ViewAny` (PRD §16, Admin only) and return 404 `STUDENT_NOT_FOUND` for an id that is not a student (422 `STUDENT_ID_REQUIRED` for an empty id).

| Method | Route | Query | Response |
|---|---|---|---|
| GET | `/api/students/{studentId}/progress` | — | 200 `StudentProgressResult { subjects: SubjectProgressResult[], weakSpots: WeakSpotsResult }` |
| GET | `/api/students/{studentId}/sessions` | `kind?`, `pageNumber` = 1, `pageSize` = 20 | 200 `PageData<SessionHistoryItemResult>`; 422 `SESSION_HISTORY_*` as above |

They use the same loaders as the student endpoints (`SubjectProgressLoader`, `WeakSpotsLoader`, `SessionHistoryLoader` in `Progress/Shared`), scoped to the route's student instead of the signed-in user, so the numbers match what the student sees. The headline counter and the streak are not part of the admin view, and the admin page links nowhere into student-only routes.

## Freshness

`invalidateMastery` (web) marks every `/api/mastery*`, `/api/progress*` and `/api/browse*` query stale after each answer. The history query also has `staleTime: 0`, so every visit refetches it, because starting or finishing a session changes history without an answer.
