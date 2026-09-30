# Training data

## Purpose

PRD §13: the platform keeps a text-only training copy of every student attempt, every Avatar exchange, every Ask a Teacher thread and every completed AI essay grade, so later stories can calibrate difficulty and train or evaluate models. #109 captures the copies. #110 adds the essay-grade copy, the JSONL export with PII stripping, and the admin export page (see Export below).

## Student hash

Training rows never carry the student id. `StudentHash` is the lower-case hex HMAC-SHA256 (64 characters) of `studentId.ToString("D")`, keyed by `TrainingData:StudentIdHashKey` (`IStudentIdHasher`, `HmacStudentIdHasher`). Without the server secret, a leaked export cannot be joined back to user ids; a plain SHA-256 of a known GUID could be.

The startup validator (`TrainingDataOptionsValidator`) enforces:

- In **every environment except Development and Testing** (Staging, Production, or any other name) the key is required, and it must not be the public development key.
- In **every** environment a non-empty key shorter than 32 characters is refused.
- In **Development and Testing** an empty key falls back to `TrainingDataOptions.DevelopmentStudentIdHashKey`, so local runs and CI need no secret.

Generate the key with `openssl rand -hex 32` and put it in `api.env` as `TrainingData__StudentIdHashKey` ([deployment.md](deployment.md)). **Never rotate it**: a new key gives every student a new pseudonym and splits their training history in two. The key is never logged.

## Tables

None of the four tables has a foreign key: not to `Users`, not to content, not to the source rows. The ids are plain indexed columns. This keeps the schema from offering re-identification joins, and a future erasure of operational rows (#215) is never blocked by a training table.

### AttemptTrainingRecords

`AttemptTrainingRecord(id, student_hash, attempt_id, question_id, question_version, subject_id, unit_id, lesson_id, session_kind, answer_json, score, normalised_score, graded_by, grade_json?, time_taken_ms, occurred_at, recorded_at)`

- Unique: `AttemptId`. Plain: `OccurredAt`, `StudentHash`.
- `SubjectId`, `UnitId` and `LessonId` are read from the question and its lesson when the row is written; questions never change lesson and lessons never change unit, so the copy is exact.
- `GradedBy` and `Grade` are copied as they are, so AI-graded attempts are captured too.

### AvatarTrainingRecords

`AvatarTrainingRecord(id, student_hash, conversation_id, student_message_id, assistant_message_id, student_message_position, entry_point, subject_id?, unit_id?, lesson_id?, question_id?, student_text, assistant_text, model, prompt_version, context_json, asked_at, occurred_at, recorded_at)`

- One row per exchange (a student message and the assistant reply). Unique: `StudentMessageId`. Plain: `(ConversationId, StudentMessagePosition)`, `OccurredAt`, `StudentHash`.
- `ConversationId` and `StudentMessagePosition` let #110 rebuild whole conversations. `Context` is the context JSON the model read ([avatar.md](avatar.md)).

### TeacherThreadTrainingRecords

`TeacherThreadTrainingRecord(id, student_hash, thread_id, trigger[Closed|RatedAfterClose], subject_id, unit_id, lesson_id, question_id?, question_version?, attempt_id?, context_json, messages_json, rating?, submitted_at, occurred_at, recorded_at)`

- A full snapshot of the thread. Unique: `(ThreadId, Trigger)`; EF writes this row before the thread's `xmin` UPDATE, so a lost close/rate race hits the index first and `AppDbContext.SaveChangesAsync` maps it to `409 TEACHER_THREAD_MODIFIED_CONCURRENTLY`, like the `xmin` race. Plain: `OccurredAt`, `StudentHash`. #110 takes the latest snapshot per thread.
- `Messages` is a jsonb array of `{ author: student|teacher, kind: text|voice, text, hasImage, sentAt }` in time order. A voice reply contributes its final transcript.
- `Context` is the thread's context JSON as stored ([ask-teacher.md](ask-teacher.md)): curriculum ids and names, the question stem and `attemptId`.

### EssayGradeTrainingRecords

`EssayGradeTrainingRecord(id, student_hash, essay_grade_id, question_id, question_version, subject_id, unit_id, lesson_id, session_kind, answer_json, max_score, score, normalised_score, criteria_json, justification, confidence, outcome[Graded|InReview], model, prompt_version, occurred_at, recorded_at)`

- One row per completed AI grade ([essay-grading.md](essay-grading.md)). Unique: `EssayGradeId`. Plain: `OccurredAt` (= `GradedAt`), `StudentHash`.
- **Why both an essay-grade row and an attempt row.** A `Graded` essay produces two rows with different purposes. `ApplyEssayGradeCommand` writes the session's `Attempt`, whose `AttemptsRecorded` event writes an `AttemptTrainingRecords` row (`GradedBy = AI`): the student-performance example (answer, final score, time taken), in line with every other attempt. `EssayGradeCompleted` writes the `EssayGradeTrainingRecords` row when the AI result is stored: the grader example, carrying what the attempt row lacks (per-criterion scores and comments, justification, confidence, `Graded`/`InReview` outcome, model and prompt version). An `InReview` grade has no attempt yet (#128), so it exists only here. The two rows are never merged into one table because they are written at different times by different commands, and the attempt table stays uniform across question types.
- **No double counting in the export.** Each export holds exactly one source, and the source is in the file name (`attempts` vs `essay-grades`), so the same essay never appears twice in one file. In the `Attempts` export an AI-graded line is marked by `gradedBy = AI` (essays and step-graded or deferred MathSteps answers are AI-graded; math step grades have no grader-calibration table yet); a consumer that builds one student-performance set from both files uses `Attempts` only and treats `EssayGrades` as grader-calibration data. The two lines carry no shared key: the export never writes attempt or essay-grade ids (Raw ids).
- `Outcome = InReview` marks a low-confidence grade that a teacher reviews. A teacher override row arrives with E17 (#128).

## When rows are written

Aggregates raise domain events; handlers in `Application/Events/TrainingRecords` add the rows. `CoreDbContext.SaveChangesAsync` publishes the events before it saves, so a training row commits in the same transaction as its source row: a failed source save writes neither. Publishing runs in rounds (`DomainEventDispatcher`): each round drains the tracked entities' events first, so an event a handler raises, or raises on an entity it adds, goes out in the next round; more than `DomainEventDispatcher.MaxRounds` (10) rounds throws instead of saving.

| Source | Event | Handler | Row |
|---|---|---|---|
| Quiz answer (`RecordAttempt`), exam submission (`SubmitExam`, including the auto-submit worker), an applied AI essay grade (`RecordEssayAttempt`, [essay-grading.md](essay-grading.md)), an applied math step grade (`RecordAiGradedAttempt`, [math-step-grading.md](math-step-grading.md)) | `AttemptsRecorded` | `AttemptTrainingRecordHandler` | one per new attempt |
| Avatar reply (`RecordExchange`) | `AvatarExchangeRecorded` | `AvatarTrainingRecordHandler` | one per exchange |
| Final teacher reply, or rating an Answered thread | `TeacherThreadClosed` | `TeacherThreadTrainingRecordHandler` | `Closed` snapshot at `ClosedAt` |
| Rating a thread that is already Closed | `TeacherThreadRatedAfterClose` | `TeacherThreadTrainingRecordHandler` | `RatedAfterClose` snapshot at the rating time |
| AI essay grade completes (`EssayGrade.Complete`, `Graded` or `InReview`) | `EssayGradeCompleted` | `EssayGradeTrainingRecordHandler` | one per grade |

- Admin test-mode sessions ([sessions.md](sessions.md), Test mode) write no attempt or essay-grade rows.
- A repeated identical quiz answer returns the existing attempt and writes nothing.
- A written essay has no attempt row until its AI grade is applied; the row then has `GradedBy = AI` and `OccurredAt` = the submission time. Re-applying a grade writes nothing, because the attempt already exists.
- `RecordedAt` is the write time; `OccurredAt` is the source time (the attempt, the reply, the close, the rating or the AI grade). The export filters by date range on `OccurredAt`.
- Rows that existed before migrations `AddTrainingRecords` and `AddTrainingExports` are not backfilled.

## Append-only

Migrations `AddTrainingRecords` and `AddTrainingExports` add these triggers on the existing `reject_append_only_mutation()` function (owned by `AddSessionsAndAttempts`, see [sessions.md](sessions.md)):

| Table | Row trigger (`BEFORE UPDATE OR DELETE`) | Statement trigger (`BEFORE TRUNCATE`) |
|---|---|---|
| `AttemptTrainingRecords` | `attempt_training_records_append_only` | `attempt_training_records_no_truncate` |
| `AvatarTrainingRecords` | `avatar_training_records_append_only` | `avatar_training_records_no_truncate` |
| `TeacherThreadTrainingRecords` | `teacher_thread_training_records_append_only` | `teacher_thread_training_records_no_truncate` |
| `EssayGradeTrainingRecords` | `essay_grade_training_records_append_only` | `essay_grade_training_records_no_truncate` |

The entities have no mutating method.

## Export

Admins (policy `TrainingData.Export`) export one source at a time as JSONL at `#/admin/export`.

**Flow.**
1. `POST /api/training-exports` `{ source, from, to, subjectId? }` creates a `Pending` `TrainingExport` and is audited (`TrainingExport.Request`). `source` is `Attempts`, `Avatar`, `TeacherThreads` or `EssayGrades`; the range is `[from, to)` on `OccurredAt`, at most `TrainingExports:MaxRangeDays` (366) days; an unknown `subjectId` is `404 SUBJECT_NOT_FOUND`.
2. `TrainingExportWorker` (a scope per item, kill switch `TrainingExports:SweepEnabled`) sends `RunTrainingExportCommand`. It reads the source in keyset pages of `TrainingExports:ReadBatchSize` (500) ordered by `(OccurredAt, Id)` with the row-value comparison `(OccurredAt, Id) > (cursor)`, `AsNoTracking`, so at most one page is in memory. `EnableRetryOnFailure` buffers whole result sets, which is why the reader pages instead of streaming. Before reading anything the run claims the export: it records the file key (`training-exports/{exportId:N}-{random:N}.jsonl`, private folder) on the row, pushes `NextAttemptAt` out by `TrainingExports:RunLeaseMinutes` (30), and saves. So no file is ever uploaded without a row naming it, and that save is the `xmin` claim: a second replica that loaded the same export loses it with `409 TRAINING_EXPORT_MODIFIED_CONCURRENTLY` before uploading anything, and the worker skips it without counting a failed attempt; a replica that loads it later sees it is not due until the lease runs out. A retry reuses the recorded key and deletes whatever an earlier attempt left there first (a partial or complete file; deleting a missing file is a no-op). Lines go to a temp file (`FileOptions.DeleteOnClose`), then to `IFileStorage` under that key. The export stores the row count, byte size and SHA-256 of the file.
3. A failed run is retried with exponential backoff (`RetryBaseDelaySeconds` · 2^(n−1)) and ends `Failed` after `MaxAttempts` (3), keeping the last error code. A run that crashed without recording its failure is retried once its lease runs out.
4. `GET /api/training-exports` lists exports newest first; the page polls every 5 s while any export is `Pending`.
5. `GET /api/training-exports/{id}/file` is admin-only (`TrainingData.Export`) and audited (`TrainingExport.Download`). It streams the stored file through the API as an `application/x-ndjson` attachment with `Cache-Control: private, no-store`. The web fetches it with the admin's bearer token and saves the blob, so no download link or capability URL exists that could leak. `Pending`/`Failed` → `409 TRAINING_EXPORT_NOT_READY`; past retention → `409 TRAINING_EXPORT_EXPIRED`; unknown id or missing file → `404 TRAINING_EXPORT_NOT_FOUND`.
6. `training-exports/` is private in both media paths: `PublicMediaMiddleware` (S3) and `PublicMediaFileProvider` (Local) return 404 for it, so `/api/media/training-exports/...` never serves a file.

**Retention.** A completed file is kept for `TrainingExports:RetentionDays` (default 7) from `CompletedAt` (`ExpiresAt`). `TrainingExportRetentionWorker` (kill switch `TrainingExports:RetentionSweepEnabled`, every `RetentionSweepIntervalSeconds`, batches of `RetentionSweepBatchSize`) sends `ExpireTrainingExportCommand` (audited as `TrainingExport.Expire`, system actor): it deletes the stored file, then marks the export `Expired` and clears its `FileKey`. The same sweep also picks up every `Failed` export that still has a `FileKey` (a file, possibly partial, from its last attempt), deletes the file at once and clears the key; the export stays `Failed`. The delete happens before the save, so a failed save is retried by the next sweep (deleting a missing file is a no-op). The export row stays as the record of what was exported.

**Line shapes.** One JSON object per line, `\n` terminated, UTF-8 without BOM, camelCase keys, enums as names, nulls written, Arabic unescaped (`TrainingExportJson.SerializerOptions`). File name: `elmanhg-{attempts|avatar|teacher-threads|essay-grades}-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.jsonl` (`to` is exclusive).

| Source | Fields |
|---|---|
| Attempts | `recordId, studentHash, questionId, questionVersion, subjectId, unitId, lessonId, sessionKind, answer, score, normalisedScore, gradedBy, grade, timeTakenMilliseconds, occurredAt` |
| Avatar | `recordId, studentHash, conversationKey, position, entryPoint, subjectId?, unitId?, lessonId?, questionId?, studentText, assistantText, model, promptVersion, context, askedAt, occurredAt` |
| TeacherThreads | `recordId, studentHash, threadKey, trigger, subjectId, unitId, lessonId, questionId?, questionVersion?, context, messages, rating?, submittedAt, occurredAt` |
| EssayGrades | `recordId, studentHash, questionId, questionVersion, subjectId, unitId, lessonId, sessionKind, answer, maxScore, score, normalisedScore, criteria, justification, confidence, outcome, model, promptVersion, occurredAt` |

**Raw ids.** Never exported: `AttemptId`, `ConversationId`, message ids, `ThreadId`, `EssayGradeId`, and, at any depth inside exported JSON (case-insensitive), the keys `attemptId, sessionId, conversationId, threadId, studentId, userId, teacherId, messageId, studentMessageId, assistantMessageId, essayGradeId`. `recordId` is the training row's own random id. Conversations and threads are grouped by `conversationKey = HMAC(key, "avatar-conversation:" + id)` and `threadKey = HMAC(key, "teacher-thread:" + id)` (`IStudentIdHasher.HashSourceId`, the same server key, domain-separated by scope). Content ids (subject, unit, lesson, question) are kept.

**Teacher threads.** Only the latest snapshot per thread inside the range is exported; at equal `OccurredAt`, `RatedAfterClose` wins over `Closed`.

**PII scrubbing** (`TrainingDataScrubber`, linear-time `NonBacktracking` regexes). Every exported free-text field and every string inside every exported JSON column is scrubbed in this order: emails → `[email]`; URLs (`http(s)://…`, `www.…`) → `[url]`; `@handle` (3+ word characters) → `[handle]`; any run of 8+ digits (ASCII, Arabic-Indic, Extended Arabic-Indic; optional leading `+`; single space, `-` or `.` between digits) → `[number]`, which covers Egyptian mobiles, `+20` numbers and 14-digit national ids. A numeric answer of 8+ digits is masked too (accepted). Inside JSON columns two whole-value exemptions exist, because their digit runs would otherwise be masked: a string that is exactly a GUID (`D` format; content and option ids), and a string under a key ending in `At` (e.g. `sentAt`) that is exactly an ISO-8601 date/time and a real one (the shape and the value ranges are both checked). Student-typed strings such as `text` or an answer never get the timestamp exemption. JSON numbers and booleans are never scrubbed. This is safe because every student-typed value is a string: every answer shape carries typed input as a string, numeric short answers included, and an answer holding a number where a string is expected is rejected with `422 QUESTION_ANSWER_INVALID` ([question-schemas.md](question-schemas.md), Answer shapes). The numbers in exported JSON are server-written (scores, points, positions). If a future answer shape carries student input as a JSON number, the scrubber must be extended first.

**Known limits.**
- Names typed in free text are not stripped: no reliable detector exists, and a name dictionary would mask common Arabic words. The page copy says contact data is removed from texts, not names.

## What is not stored

The student id, names, phone, email, the teacher id, audio and image URLs, the session id, and the Avatar tokens, cost and citations.

## Retention and privacy review checklist

- [x] Text only: voice replies keep the teacher's final transcript; no audio, image or file references.
- [x] No direct identifiers: `StudentHash` replaces the student id; no teacher id.
- [x] The hash key is a secret in `api.env`, required in Staging and Production, and never rotated.
- [x] Admin test-mode sessions are excluded.
- [x] Append-only is enforced by database triggers (UPDATE, DELETE, TRUNCATE).
- [x] Written atomically with the source row: a failed save writes neither.
- [x] No foreign keys from training tables, so operational erasure is never blocked by them.
- [x] Free text may contain PII the student typed: the export masks emails, URLs, handles and 8+ digit numbers in every text and JSON string (Export, PII scrubbing).
- [ ] Names typed in free text are not stripped (residual risk, #215).
- [x] Source ids (`AttemptId`, `ConversationId`, message ids, `ThreadId`, `EssayGradeId`, id keys inside JSON) are never exported raw; conversations and threads are grouped by scoped HMAC keys (Export, Raw ids). Inside the database they can still be joined by anyone with database access.
- [x] Export files are private, downloaded only by an admin through the audited API, and deleted after `TrainingExports:RetentionDays` (default 7).
- [ ] **Retention period**: dev decision pending (#215). Until then, operational and training data are kept indefinitely with no purge job.
- [ ] **Student erasure path**: dev decision pending (#215). The triggers block DELETE, so erasure needs a migration-owned privileged procedure. Anyone holding the key can re-link hashes to ids.
- [ ] **Notice or consent** that interactions are used for training: dev or legal decision (#215).
- [x] Only the Admin export job reads these tables (PRD §16 "Export training data"); no other API reads them.

## Consumers

The JSONL export (#110): `RunTrainingExportHandler` through each repository's `GetExportPageAsync`.
