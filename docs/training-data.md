# Training data

## Purpose

PRD §13: the platform keeps a text-only training copy of every student attempt, every Avatar exchange and every Ask a Teacher thread, so later stories can calibrate difficulty and train or evaluate models. This story (#109) only captures the copies. The JSONL export, PII stripping and the admin export page are #110.

## Student hash

Training rows never carry the student id. `StudentHash` is the lower-case hex HMAC-SHA256 (64 characters) of `studentId.ToString("D")`, keyed by `TrainingData:StudentIdHashKey` (`IStudentIdHasher`, `HmacStudentIdHasher`). Without the server secret, a leaked export cannot be joined back to user ids; a plain SHA-256 of a known GUID could be.

The startup validator (`TrainingDataOptionsValidator`) enforces:

- In **every environment except Development and Testing** (Staging, Production, or any other name) the key is required, and it must not be the public development key.
- In **every** environment a non-empty key shorter than 32 characters is refused.
- In **Development and Testing** an empty key falls back to `TrainingDataOptions.DevelopmentStudentIdHashKey`, so local runs and CI need no secret.

Generate the key with `openssl rand -hex 32` and put it in `api.env` as `TrainingData__StudentIdHashKey` ([deployment.md](deployment.md)). **Never rotate it**: a new key gives every student a new pseudonym and splits their training history in two. The key is never logged.

## Tables

None of the three tables has a foreign key: not to `Users`, not to content, not to the source rows. The ids are plain indexed columns. This keeps the schema from offering re-identification joins, and a future erasure of operational rows (#215) is never blocked by a training table.

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

## When rows are written

Aggregates raise domain events; handlers in `Application/Events/TrainingRecords` add the rows. `CoreDbContext.SaveChangesAsync` publishes the events before it saves, so a training row commits in the same transaction as its source row: a failed source save writes neither. Publishing runs in rounds (`DomainEventDispatcher`): each round drains the tracked entities' events first, so an event a handler raises, or raises on an entity it adds, goes out in the next round; more than `DomainEventDispatcher.MaxRounds` (10) rounds throws instead of saving.

| Source | Event | Handler | Row |
|---|---|---|---|
| Quiz answer (`RecordAttempt`), exam submission (`SubmitExam`, including the auto-submit worker), an applied AI essay grade (`RecordEssayAttempt`, [essay-grading.md](essay-grading.md)) | `AttemptsRecorded` | `AttemptTrainingRecordHandler` | one per new attempt |
| Avatar reply (`RecordExchange`) | `AvatarExchangeRecorded` | `AvatarTrainingRecordHandler` | one per exchange |
| Final teacher reply, or rating an Answered thread | `TeacherThreadClosed` | `TeacherThreadTrainingRecordHandler` | `Closed` snapshot at `ClosedAt` |
| Rating a thread that is already Closed | `TeacherThreadRatedAfterClose` | `TeacherThreadTrainingRecordHandler` | `RatedAfterClose` snapshot at the rating time |

- Admin test-mode sessions ([sessions.md](sessions.md), Test mode) write no attempt rows.
- A repeated identical quiz answer returns the existing attempt and writes nothing.
- A written essay has no attempt row until its AI grade is applied; the row then has `GradedBy = AI` and `OccurredAt` = the submission time. Re-applying a grade writes nothing, because the attempt already exists.
- `RecordedAt` is the write time; `OccurredAt` is the source time (the attempt, the reply, the close or the rating). #110 exports by date range on `OccurredAt`.
- Rows that existed before migration `AddTrainingRecords` are not backfilled.

## Append-only

Migration `AddTrainingRecords` adds six triggers on the existing `reject_append_only_mutation()` function (owned by `AddSessionsAndAttempts`, see [sessions.md](sessions.md)):

| Table | Row trigger (`BEFORE UPDATE OR DELETE`) | Statement trigger (`BEFORE TRUNCATE`) |
|---|---|---|
| `AttemptTrainingRecords` | `attempt_training_records_append_only` | `attempt_training_records_no_truncate` |
| `AvatarTrainingRecords` | `avatar_training_records_append_only` | `avatar_training_records_no_truncate` |
| `TeacherThreadTrainingRecords` | `teacher_thread_training_records_append_only` | `teacher_thread_training_records_no_truncate` |

The entities have no mutating method.

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
- [ ] Free text may contain PII the student typed (names, phone numbers). Stripping is #110's job at export.
- [ ] Source ids (`AttemptId`, `ConversationId`, message ids, `ThreadId`, `attemptId` in context) can be joined to operational tables by anyone with database access. #110 must not export them raw.
- [ ] **Retention period**: dev decision pending (#215). Until then, operational and training data are kept indefinitely with no purge job.
- [ ] **Student erasure path**: dev decision pending (#215). The triggers block DELETE, so erasure needs a migration-owned privileged procedure. Anyone holding the key can re-link hashes to ids.
- [ ] **Notice or consent** that interactions are used for training: dev or legal decision (#215).
- [x] No API reads these tables. The only reader will be the Admin-only export (#110, PRD §16 "Export training data").

## Consumers

#110 (JSONL export).
