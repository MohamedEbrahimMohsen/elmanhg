# Browsing subjects, units and lessons

## Purpose

A student walks the published curriculum tree: from a home subject card to a **subject page** (its units), then to a **unit page** (its lessons and the unit exam), then to a **lesson page** with the tabs الشرح / الأهداف / الملخص / التدريب. This implements PRD §7.1 steps 3–4. Only Published lessons are visible to students (PRD §5.2). Every level shows the student's own mastery.

## Endpoints

All four endpoints use the policy `Progress.ViewOwn` (`DefaultCodes.ProgressViewOwn`): Students only. Teachers and admins get 403; an anonymous caller gets 401. Teachers browse the published tree through `GET /api/subjects{/id}` (`Content.Browse`).

| Method | Route | Body | Response |
|---|---|---|---|
| GET | `/api/browse/subjects/{subjectId}` | — | 200 `StudentSubjectResult` |
| GET | `/api/browse/units/{unitId}` | — | 200 `StudentUnitResult` |
| GET | `/api/browse/lessons/{lessonId}` | — | 200 `StudentLessonResult` |
| POST | `/api/browse/lessons/{lessonId}/openings` | — | 200 (empty) |

Errors: 422 `SUBJECT_ID_REQUIRED` / `UNIT_ID_REQUIRED` / `LESSON_ID_REQUIRED` for an empty id; 404 `SUBJECT_NOT_FOUND`, `UNIT_NOT_FOUND` (also for a unit whose subject is gone) and `LESSON_NOT_FOUND`; 409 `LESSON_ALREADY_OPENED` (see Lesson openings).

## Rules

- **Published lessons only.** A draft or archived lesson is 404 `LESSON_NOT_FOUND`, on read and on opening. Unit lists and lesson counts include Published lessons only.
- **Order.** Units and lessons are ordered by `Order`, then `CreationDate`. A unit with no Published lesson is still listed on the subject page, with a lesson count of 0 and 0 % mastery; its unit page shows the empty state.
- **Mastery per node.** Subject, unit and lesson numbers come from `GetLessonCountsAsync` and `MasteryTotals`, the same servable-rule counts as `docs/mastery.md`: servable, mastered and seen questions, and `mastered × 100 / servable` (0 when nothing is servable). A lesson with no servable question shows zeros.
- **Best score.** A unit's best unit-exam score is the student's best submitted, non-test sitting for the `unit:{id}` scope, as in `docs/progress.md`; null when there is none.
- **Previous and next.** The sequence is the subject's units (Order, CreationDate, Id), then each unit's Published lessons (Order, CreationDate, Id) (`LessonSequence` in Domain). The last lesson of a unit links to the first Published lesson of the next unit that has one. At the end of the subject `nextLesson` is null. Drafts and archived lessons are skipped.

## Result shapes

- `StudentSubjectResult { id, name, servableCount, masteredCount, seenCount, masteryPercent, units[] }` with `StudentUnitSummaryResult { id, name, lessonCount, servableCount, masteredCount, seenCount, masteryPercent, bestExamScorePercent? }`.
- `StudentUnitResult { id, name, subjectId, subjectName, servableCount, masteredCount, seenCount, masteryPercent, bestExamScorePercent?, lessons[] }` with `StudentLessonSummaryResult { id, name, servableCount, masteredCount, seenCount, masteryPercent }`.
- `StudentLessonResult { id, name, unitId, unitName, subjectId, subjectName, explanation, summary, videoUrl?, objectives[] { id, text, order }, servableCount, masteredCount, seenCount, masteryPercent, previousLesson?, nextLesson? }` with `LessonLinkResult { id, name, unitId, unitName }`.

## Lesson openings

`POST /api/browse/lessons/{lessonId}/openings` records that the student opened a lesson (`LessonOpening`: student, lesson, opened-at; table `LessonOpenings`).

- It is idempotent: a second call for the same student and lesson stores nothing and returns 200.
- An unknown, draft or archived lesson returns 404 `LESSON_NOT_FOUND` and stores nothing.
- The unique index `IX_LessonOpenings_StudentId_LessonId` backs the rule; a concurrent duplicate that loses the race returns 409 `LESSON_ALREADY_OPENED`.
- Openings are not audited: the audit log covers content, validation, publishing, grants and exports, not student reading activity.

## Unit-exam gate

PRD §7.4 allows a gate: the unit exam unlocks only after every lesson is opened. It is off by default (`Exams:RequireAllLessonsOpened` = false). When it is on, a new unit exam and a new multi-unit exam need an opening for every Published lesson of the unit (or of every selected unit), else 400 `EXAM_LESSONS_NOT_OPENED`. A resume is never blocked and an Admin's test-mode exam is exempt. The unit-exam overview reports `unopenedLessonCount` (0 when the gate is off). A lesson published later locks the gate again until it is opened. Details in [exams](exams.md).

## Web

Feature `web/src/features/browse/`.

- **`/student/subject/{subjectId}`**: breadcrumb الرئيسية › subject; the subject name, the mastery line «إتقانك X٪ · أسئلة متاحة: N · شاهدت: M» and bar; «الوحدات» with one card per unit (name linking to the unit page, mastery bar, «إتقان X٪ · N درس», «أفضل درجة امتحان: S / 100» or «—», and «امتحان الوحدة» to the exam start); «امتحان متعدد الوحدات» opens the builder for this subject.
- **`/student/unit/{unitId}`**: breadcrumb الرئيسية › subject › unit; the unit name, mastery line and bar; «الدروس» with one card per Published lesson (name linking to the lesson page, mastery bar, «إتقان X٪ · N سؤال»); the unit-exam card with the best score or «لم تمتحن هذه الوحدة بعد.» and «افتح امتحان الوحدة».
- **`/student/lesson/{lessonId}`**: breadcrumb الرئيسية › subject › unit › lesson; the lesson title, mastery line and bar on every tab; pill tabs, each a route: الشرح (`/`, the explanation and «شاهد فيديو الدرس» when there is a safe video URL), الأهداف (`/objectives`), الملخص (`/summary`) and التدريب (`/practice`, pick 5 / 10 / 20). Each tab has its empty message. Previous/next links follow the sequence above; at the end of the subject the link is «العودة إلى {unit}».
- Every page has loading, empty, error-with-retry and RTL states. Pages are one column on mobile, names wrap, and rich text wraps long words.
- Once the lesson read succeeds, the page posts the lesson opening once (a ref guards the StrictMode double effect; no toast). On success it invalidates that unit's exam overview, so the exam-start warning is current.
- `invalidateMastery` marks the `/api/browse*` queries stale after each quiz answer and exam submission.
- Entry points: home subject cards, progress subject and unit names, the exam-start breadcrumb, and «العودة للدرس» on the quiz result.

## Free tier

Free-tier locks arrive with #87. It will add `IsLocked` to `StudentLessonSummaryResult` and `StudentLessonResult`, a lock check to `GetStudentLessonHandler` and to starting a quiz, and a locked variant of the lesson card. Lessons are already returned in curriculum order, which the "first lesson per unit" rule reads.
