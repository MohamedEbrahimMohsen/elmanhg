-- EXPLAIN (ANALYZE, BUFFERS) of the hot queries (docs/performance.md §6). Each mirrors the named repository method.
-- deploy/load-test.sh runs it after the load run: psql -v ON_ERROR_STOP=1 -v key=<LoadTestSeed:Key> < explain.sql
-- The student is the load-test student with the most attempts, so the per-student plans run against real history.
ANALYZE;

SELECT u."Id" AS student_id
FROM "AspNetUsers" AS u
LEFT JOIN "Attempts" AS a ON a."StudentId" = u."Id"
WHERE u."NormalizedEmail" LIKE upper(:'key' || '-student-%@loadtest.example.com')
GROUP BY u."Id", u."NormalizedEmail"
ORDER BY count(a."Id") DESC, u."NormalizedEmail"
LIMIT 1 \gset
SELECT s."Id" AS subject_id FROM "Subjects" AS s WHERE s."Name" = 'Load test ' || :'key' AND s."IsDeleted" = false \gset
SELECT l."Id" AS lesson_id
FROM "Lessons" AS l
INNER JOIN "Units" AS un ON un."Id" = l."UnitId"
WHERE un."SubjectId" = :'subject_id' AND un."Order" = 1 AND l."Order" = 1 AND l."IsDeleted" = false \gset
SELECT '{' || string_agg(un."Id"::text, ',' ORDER BY un."Order") || '}' AS unit_ids FROM "Units" AS un WHERE un."SubjectId" = :'subject_id' \gset
SELECT coalesce((SELECT se."Id" FROM "Sessions" AS se WHERE se."StudentId" = :'student_id' ORDER BY se."StartedAt" DESC LIMIT 1), '00000000-0000-0000-0000-000000000000'::uuid) AS session_id \gset
\echo 'student' :student_id 'subject' :subject_id 'lesson' :lesson_id

\echo '== Q1 quiz attempts today (SessionRepository.CountQuizAttemptsOnDayAsync)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT COUNT(*)::int AS "Value"
FROM "Attempts" AS a
INNER JOIN "Sessions" AS s ON s."Id" = a."SessionId"
WHERE a."StudentId" = :'student_id' AND a."CreatedAt" >= (current_date - 1)::timestamptz AND a."IsDeleted" = false AND s."IsDeleted" = false AND s."IsTestMode" = false AND s."Kind" = 'Quiz'
AND (a."CreatedAt" AT TIME ZONE 'Africa/Cairo')::date = current_date;

\echo '== Q2 quiz activity days (SessionRepository.GetQuizActivityDaysAsync)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT (a."CreatedAt" AT TIME ZONE 'Africa/Cairo')::date AS "Value"
FROM "Attempts" AS a
INNER JOIN "Sessions" AS s ON s."Id" = a."SessionId"
WHERE a."StudentId" = :'student_id' AND a."CreatedAt" >= now() - interval '365 days' AND a."IsDeleted" = false AND s."IsDeleted" = false AND s."IsTestMode" = false AND s."Kind" = 'Quiz';

\echo '== Q3 attempt summaries of a lesson (SessionRepository.GetAttemptSummariesAsync)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT a."QuestionId", count(*), count(*) FILTER (WHERE a."NormalisedScore" >= 0.8), max(a."CreatedAt"), max(CASE WHEN a."NormalisedScore" >= 0.8 THEN a."CreatedAt" END)
FROM "Attempts" AS a
WHERE a."IsDeleted" = false AND a."StudentId" = :'student_id'
AND a."QuestionId" IN (SELECT q."Id" FROM "Questions" AS q WHERE q."LessonId" = :'lesson_id' AND q."IsDeleted" = false)
GROUP BY a."QuestionId";

\echo '== Q4 servable question ids of a lesson (QuestionRepository.GetServableIdsInLessonAsync)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT q."Id"
FROM "Questions" AS q
WHERE q."IsDeleted" = false AND q."ValidationStatus" = 'Approved' AND q."RetiredAt" IS NULL
AND EXISTS (SELECT 1 FROM "Lessons" AS l WHERE l."IsDeleted" = false AND l."Id" = q."LessonId" AND l."State" = 'Published')
AND q."LessonId" = :'lesson_id';

\echo '== Q5 open quiz of the lesson (StartQuizSessionHandler)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT s.*
FROM "Sessions" AS s
WHERE s."IsDeleted" = false AND s."StudentId" = :'student_id' AND s."Kind" = 'Quiz' AND s."ScopeKey" = 'lesson:' || :'lesson_id' AND s."SubmittedAt" IS NULL
LIMIT 1;

\echo '== Q6 session items and attempts (split query of a session load)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT i.* FROM "SessionItems" AS i WHERE i."IsDeleted" = false AND i."SessionId" = :'session_id' ORDER BY i."SessionId", i."Id";
EXPLAIN (ANALYZE, BUFFERS)
SELECT a.* FROM "Attempts" AS a WHERE a."IsDeleted" = false AND a."SessionId" = :'session_id' ORDER BY a."SessionId", a."Id";

\echo '== Q7 lesson mastery counts of the subject (QuestionMasteryRepository.GetLessonCountsAsync)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT un."SubjectId", su."Order", l."UnitId", un."Order", l."Id", l."Order", count(*), sum(CASE WHEN m."IsMastered" THEN 1 ELSE 0 END), sum(CASE WHEN m."Id" IS NOT NULL THEN 1 ELSE 0 END)
FROM "Questions" AS q
INNER JOIN "Lessons" AS l ON l."Id" = q."LessonId" AND l."IsDeleted" = false
INNER JOIN "Units" AS un ON un."Id" = l."UnitId" AND un."IsDeleted" = false
INNER JOIN "Subjects" AS su ON su."Id" = un."SubjectId" AND su."IsDeleted" = false
LEFT JOIN "QuestionMasteries" AS m ON m."StudentId" = :'student_id' AND m."QuestionId" = q."Id" AND m."IsDeleted" = false
WHERE q."IsDeleted" = false AND q."ValidationStatus" = 'Approved' AND q."RetiredAt" IS NULL AND l."State" = 'Published' AND un."SubjectId" = :'subject_id'
GROUP BY un."SubjectId", su."Order", l."UnitId", un."Order", l."Id", l."Order";

\echo '== Q8 entitlement subscriptions (StudentEntitlementLoader)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT s.*
FROM "Subscriptions" AS s
WHERE s."IsDeleted" = false AND s."StudentId" = :'student_id'
AND ((s."Status" IN ('Active', 'PastDue') AND s."CurrentPeriodEnd" > now() - interval '3 days') OR (s."Status" = 'Cancelled' AND s."CurrentPeriodEnd" > now()));

\echo '== Q9 servable exam candidates of 3 units (QuestionRepository.GetServableExamCandidatesAsync)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT q."Id", q."LessonId", q."Type", q."Difficulty"
FROM "Questions" AS q
INNER JOIN "Lessons" AS l ON l."Id" = q."LessonId" AND l."IsDeleted" = false
WHERE q."IsDeleted" = false AND q."ValidationStatus" = 'Approved' AND q."RetiredAt" IS NULL
AND EXISTS (SELECT 1 FROM "Lessons" AS p WHERE p."IsDeleted" = false AND p."Id" = q."LessonId" AND p."State" = 'Published')
AND l."UnitId" = ANY (:'unit_ids'::uuid[])
ORDER BY q."Id";

\echo '== Q10 avatar messages today (AvatarMessageUsageRepository.CountOnDayAsync)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT COUNT(*)::int AS "Value"
FROM "AvatarMessageUsages" AS u
WHERE u."StudentId" = :'student_id' AND u."CreatedAt" >= (current_date - 1)::timestamptz AND u."IsDeleted" = false
AND (u."CreatedAt" AT TIME ZONE 'Africa/Cairo')::date = current_date;

\echo '== Q11 served revisions of a session (QuestionRepository.GetRevisionsAsync)'
EXPLAIN (ANALYZE, BUFFERS)
SELECT r.*
FROM "QuestionRevisions" AS r
WHERE r."IsDeleted" = false AND r."QuestionId" IN (SELECT i."QuestionId" FROM "SessionItems" AS i WHERE i."SessionId" = :'session_id');
