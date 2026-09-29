# Ask a Teacher

A student who holds the Ask a Teacher add-on (on top of an entitled Base plan) sends a written question, with its context attached and an optional photo, to the teachers of a subject (PRD §12). #94 creates the thread and the student's side. #95 adds the teacher inbox, claiming, text replies and the student's new-reply mark. #96 adds voice replies with automatic transcription and the S3-compatible storage adapter. #97 adds the one follow-up, the rating, closing, the 12 h / 20 h reminders with the breach alert, and realtime push over SignalR.

## Model

Both tables map to PRD §15. #94 ships only the columns it writes; later stories add theirs in their own migrations (migrations `AddTeacherThreads`, `AddTeacherThreadClaims`, `AddTeacherVoiceReplies` and `AddTeacherThreadSlaAndRatings`).

### TeacherThread (aggregate root)

| Field | PRD §15 | Notes |
|---|---|---|
| `Id` | `id` | |
| `StudentId` | `student_id` | FK `Users`, restrict. `CreatedBy` is the student too. |
| `SubjectId` | `subject_id` | FK `Subjects`, restrict. Copied from the context for inbox routing (#95). |
| `Context` | `context_json` | jsonb snapshot, see Context. |
| `Status` | `status` | `Open`, `Answered`, `Closed`, stored as a string. #94 only creates `Open`. |
| `SubmittedAt` | `submitted_at` | Truncated to microseconds so the create response equals later reads. |
| `SlaDueAt` | `sla_due_at` | `SubmittedAt + Subscriptions:AskTeacherReplySlaHours` (24). A follow-up resets it to `followUpAt + AskTeacherReplySlaHours` (#97). |
| `TeacherId` | `teacher_id?` | FK `Users`, restrict. The teacher (or admin) who claimed the thread; null until claimed (#95). |
| `ClaimedAt` | `claimed_at?` | Set by the first claim, truncated to microseconds (#95). |
| `ClosedAt` | `closed_at?` | Set when the teacher's reply to the follow-up closes the thread, or when a rating closes an `Answered` thread (#97). |
| `Rating` | `rating?` | 1..5, set once by the student (#97). Check constraint `CK_TeacherThreads_Rating`: `"Rating" IS NULL OR "Rating" BETWEEN 1 AND 5`. |
| `Version` | — | `xmin` row version; no DDL. |

Indexes: `(StudentId, SubmittedAt)` for the student's list and the quota count, `(SubjectId, Status, SubmittedAt)` for the teacher inbox, `(Status, SlaDueAt)` for the SLA sweep (#97).

### TeacherMessage (child)

| Field | PRD §15 | Notes |
|---|---|---|
| `Id` | `id` | Set by the domain. |
| `ThreadId` | `thread_id` | FK `TeacherThreads`, restrict. |
| `SenderId` | `sender_id` | FK `Users`, restrict. The student for the first message. |
| `Kind` | `kind` | `Text` or `Voice` (string). #94 writes `Text`; a voice reply (#96) writes `Voice`. |
| `Text` | `text` | Required, trimmed. A blank text is `TEACHER_MESSAGE_TEXT_REQUIRED`. |
| `ImageUrl` | `image_url?` | The stored photo, see Private media. |
| `AudioUrl` | `audio_url?` | The stored voice reply, `varchar(400)` (#96). Null on text messages. |
| `AudioDurationSeconds` | `audio_duration_seconds?` | The length the recorder reported (#96). |
| `TranscriptFinal` | `transcript_final` | `true` on every `Voice` message: its `Text` is the transcript the teacher reviewed. `false` on text messages (default). |
| `CreatedAt` | `created_at` | Equals `SubmittedAt` for the first message. |
| `StudentReadAt` | `student_read_at?` | Set when the student opens the thread; null for student messages. Kept on the messages so a read never changes the thread's `xmin` under a teacher's in-flight reply (#95). |

Indexes: `(ThreadId, CreatedAt)` and filtered indexes on `ImageUrl` and `AudioUrl` for the media access check.

### TeacherVoiceDraft (#96)

The transcription job for one recording. It is created when the teacher uploads a recording and is never shown to the student.

| Field | Notes |
|---|---|
| `ThreadId`, `TeacherId` | FKs `TeacherThreads` and `Users`, restrict. Only this teacher can read or send the draft. |
| `AudioKey`, `AudioUrl` | The storage key (`teacher-threads/{random}{ext}`) and its URL, `varchar(400)`. |
| `AudioDurationSeconds` | Reported by the browser, 1..`VoiceMaxDurationSeconds`. |
| `Status` | `Pending` → `Ready` or `Failed` → `Sent`, stored as a string. |
| `Transcript`, `TranscriptionModel` | Set when the transcription succeeds (`varchar(100)` model id). |
| `Attempts`, `NextAttemptAt` | Retry bookkeeping; `NextAttemptAt` is null once the draft leaves `Pending`. |
| `RecordedAt`, `TranscribedAt`, `SentMessageId` | Microsecond-truncated times; the id of the voice message once sent. |

Indexes: `(ThreadId, TeacherId)` and `NextAttemptAt` filtered on `Status = 'Pending'` for the worker. The table is in the global soft-delete filter.

### TeacherThreadSlaEvent (#97)

One row per SLA window and stage: the reminders and the breach record. Append-only (no mutators), written only by the `ask-teacher-sla` worker's `ProcessTeacherThreadSlaHandler`, never by a request.

| Field | Notes |
|---|---|
| `ThreadId` | FK `TeacherThreads`, restrict. |
| `Kind` | `FirstReminder`, `SecondReminder` or `Breach`, stored as a string. |
| `SlaDueAt` | The thread's `SlaDueAt` when the stage fired; it names the window, so a follow-up's new window has its own stages. |
| `TeacherId` | FK `Users`, restrict. The claimer at that moment, or null. |
| `OccurredAt` | Microsecond-truncated. |

Indexes: unique `(ThreadId, Kind, SlaDueAt)` (each stage fires once per window) and `TeacherId`. The table is in the global soft-delete filter.

Both entities are in the global soft-delete filter. A thread always has at least one message, and the first is the student's.

**Status machine:** `Open` →(reply)→ `Answered` →(follow-up)→ `Open` →(final reply)→ `Closed`. `Answered` →(rate)→ `Closed`. `Closed` and unrated →(rate)→ `Closed` with a rating. A reply is text or, from #96, voice. The server computes `isOverdue = Status == Open && now >= SlaDueAt`; the web never compares clocks for it.

Threads are not audited: a student question is activity, and the row itself is the record (PRD §14).

## Teacher inbox

- **Scope.** A teacher sees the threads of the subjects they are assigned to (`TeacherSubject`). No assignment means an empty list (fail closed). Opening, claiming or replying to a thread of another subject is `403 SUBJECT_OUT_OF_SCOPE`. An admin sees every subject and may claim and reply like a teacher (PRD §16); there is no admin inbox screen yet.
- **Filters.** `filter=All` (default), `Unclaimed` (no teacher yet) or `Mine` (claimed by the caller). An unknown value is refused by model binding with `400`.
- **Order.** `Open` threads first, then by `SlaDueAt` ascending, then by id. Paged with `pageNumber` / `pageSize` (max `AskTeacher:ThreadListMaxPageSize`, 50; `422 TEACHER_THREAD_PAGE_NUMBER_INVALID` / `PAGE_SIZE_INVALID`).
- **Identity.** The teacher sees the student's display name only, and the claimer's display name (PRD §8.4, §14).
- **Claim.** The first claim wins. Claiming a thread you already own changes nothing. Claiming a thread owned by someone else is `409 TEACHER_THREAD_ALREADY_CLAIMED`. Two claims at the same moment race on the thread's `xmin`: the loser gets `409 TEACHER_THREAD_MODIFIED_CONCURRENTLY` (mapped once in `AppDbContext.SaveChangesAsync`).
- **Reply.** Only the claiming user may reply, and only while the thread is `Open`. Unclaimed is `409 TEACHER_THREAD_NOT_CLAIMED`, claimed by someone else is `409 TEACHER_THREAD_ALREADY_CLAIMED` (the claim check runs first), not `Open` is `409 TEACHER_THREAD_NOT_AWAITING_REPLY`. The text is required and at most `AskTeacher:ReplyTextMaxLength` (4000) (`422 TEACHER_THREAD_REPLY_TEXT_REQUIRED` / `TOO_LONG`). A reply is a `Text` message from the teacher and moves the thread to `Answered`; the reply to the student's follow-up moves it to `Closed` and sets `ClosedAt` (#97). A reply to a `Closed` thread is `409 TEACHER_THREAD_NOT_AWAITING_REPLY`.
- Claims and replies are not audited; `TeacherId`, `ClaimedAt` and the message rows are the record. The question, its context ids and the teacher's final reply text stay in `TeacherThread.Context` and `TeacherMessage`, which #109 turns into training records.

## Student notification

In-app only. A thread with a teacher message whose `StudentReadAt` is null reports `hasUnreadReply` in the student's list and thread results, and the list shows «رد جديد». Opening the thread calls `POST /api/teacher-threads/{threadId}/read`, which stamps the teacher's messages (the first read time is kept). There is no WhatsApp or Email.

**Realtime (#97).** After a text or voice reply is saved, the API pushes `teacherReplyReceived { threadId }` to the student over the SignalR hub `/api/hubs/notifications` (`RequireAuthorization(AuthenticatedUser)`, users keyed by the JWT `NameIdentifier`). The student shell refreshes the list and the thread and shows «وصل رد من المعلّم على سؤالك.». Browsers cannot set headers on WebSocket or EventSource requests, so the hub also accepts the bearer token in `?access_token=`, on the hub path only; the API request log (`RequestLogScrubber`) and the Caddy access log redact every query value, so the token is never logged. The push is best effort: a failed send is logged as a warning (thread id only) and never turns the saved reply into an error, and pages refetch when opened.

## Follow-up and rating (#97)

- **Follow-up.** `POST /api/teacher-threads/{threadId}/follow-ups` (`{ "text" }`). Only the owner (anyone else gets `404 TEACHER_THREAD_NOT_FOUND`), only on an `Answered` thread, once: otherwise `409 TEACHER_THREAD_FOLLOW_UP_NOT_ALLOWED`. "Once" needs no column: after a follow-up the thread is never `Answered` again. The text follows the question rules (`422 TEACHER_THREAD_TEXT_REQUIRED` / `TEXT_TOO_LONG`, `QuestionTextMaxLength`). No photo, no add-on re-check and no quota: a follow-up does not count (PRD §12.1). It adds a student `Text` message, sets `Open` and starts a new SLA window.
- **Final reply.** A reply while the student has more than one message (`HasFollowUp()`) closes the thread (`Closed`, `ClosedAt`). The reply handlers load `Messages`, which this rule reads.
- **Rating.** `POST /api/teacher-threads/{threadId}/rating` (`{ "rating" }`), 1..5 (`TeacherThread.MinRating` / `MaxRating`; `422 TEACHER_THREAD_RATING_INVALID`), once (`409 TEACHER_THREAD_ALREADY_RATED`), never on `Open` (`409 TEACHER_THREAD_NOT_ANSWERED`). Rating an `Answered` thread closes it (`ClosedAt`) and gives up the follow-up; rating a thread closed by the final reply keeps its `ClosedAt`. The student, the teacher and the admin see the rating (`TeacherThreadResult.rating`, `TeacherInboxThreadResult.rating`).
- `TeacherThreadResult` carries `rating`, `closedAt`, `canFollowUp` (`Answered`) and `canRate` (unrated and not `Open`); the web shows the cards from these flags.
- Follow-ups and ratings are not audited; the rows are the record.

## Context

A question carries exactly one of `lessonId`, `questionId` or `attemptId`. None, more than one, or `Guid.Empty` is `422 TEACHER_THREAD_CONTEXT_INVALID`. The server resolves it to subject → unit → lesson (+ question) in `TeacherThreadContextResolver`:

| Kind | Rule | Failure |
|---|---|---|
| Lesson | The lesson must be Published. | `404 LESSON_NOT_FOUND` |
| Question | The question must be servable (`ServableQuestionSpecification`). The stem and version are the current ones. | `404 QUESTION_NOT_FOUND` |
| Attempt | The attempt must be the student's own, and its session must not be an exam that is still open (not submitted), so a teacher cannot be asked during an exam. The stem is the served revision's (`QuestionRevision` at `Attempt.QuestionVersion`). The question need not be servable any more, but its lesson must still be Published. | `404 ATTEMPT_NOT_FOUND` (also for another student's attempt), `409 TEACHER_THREAD_EXAM_IN_PROGRESS`, `404 QUESTION_NOT_FOUND` (missing revision), `404 LESSON_NOT_FOUND` |

The snapshot (`TeacherThreadContext`) keeps `subjectId`, `subjectName`, `unitId`, `unitName`, `lessonId`, `lessonName`, `questionId?`, `questionVersion?`, `questionStem?` and `attemptId?`, so the thread stays readable after content edits and the ids feed the training export (PRD §13).

## Quota

- The limit is `Subscriptions:AskTeacherMonthlyQuestions` (20) with the add-on, 0 without it.
- A thread counts in the calendar month of `Subscriptions:DailyQuotaTimeZone` (Africa/Cairo) in which it was submitted. The month's bounds are converted to UTC (`AskTeacherGate.CurrentQuotaMonth`). A follow-up (#97) does not count.
- Gate order on create: validation `422` → user `401` → no add-on `403 ASK_TEACHER_REQUIRES_SUBSCRIPTION` → quota `403 ASK_TEACHER_MONTHLY_LIMIT_REACHED` (context `limit`) → context `404`s / `409 TEACHER_THREAD_EXAM_IN_PROGRESS` → store the photo → one save. The context preview applies the same add-on check (`403 ASK_TEACHER_REQUIRES_SUBSCRIPTION`) before it resolves anything; it does not check the quota.
- `GET /api/subscriptions/usage` exposes `monthlyAskTeacherQuestionLimit`, `askTeacherQuestionsUsedThisMonth` and `askTeacherQuestionsRemainingThisMonth` (never below 0). See `docs/subscriptions.md`.

## Private media

- One photo per question, sent in the same multipart request (field `image`). Allowed: `.png`, `.jpg`, `.jpeg`, `.webp` with a matching content type and matching file signature (magic bytes: PNG `89 50 4E 47 0D 0A 1A 0A`, JPEG `FF D8 FF`, WEBP `RIFF….WEBP`), at most `AskTeacher:ImageMaxSizeInMb` (5). GIF, SVG and a file whose bytes do not match its extension are refused (`422 TEACHER_THREAD_IMAGE_TYPE_INVALID`, `422 TEACHER_THREAD_IMAGE_TOO_LARGE`).
- Stored through `IFileStorage` under the key `teacher-threads/{random}{ext}` after every check has passed. `TeacherMessage.ImageUrl` is `/api/media/teacher-threads/{random}{ext}`.
- Voice replies (#96) are stored the same way, under `teacher-threads/{random}{ext}` with `.webm`, `.ogg` or `.m4a`.
- **Photos and voice replies are private.** `GET /api/media/teacher-threads/...` is served only to an authenticated caller who is the owning student, a teacher assigned to the thread's subject, or an admin (`CanViewTeacherThreadMediaQuery`, which matches `ImageUrl` or `AudioUrl`, and `TeacherThreadMediaMiddleware`). Everyone else, including anonymous callers and unknown files, gets an empty `404`. Unsent draft audio is never served, not even to its teacher: only a sent message's `AudioUrl` passes the check. The response carries `nosniff` and `Cache-Control: private, no-store`.
- The bytes are streamed through `IFileStorage.OpenReadAsync` for both providers (Local disk and S3). There are no presigned URLs: the bucket stays fully private and every read goes through the API's access check.
- The public static-file mount for `/api/media` never serves anything inside `teacher-threads` (`PublicMediaFileProvider`), whatever the spelling of the path (case, doubled or encoded separators, trailing dots, `..`, `~` short names). With the S3 provider, public media (lesson images) goes through `PublicMediaMiddleware` instead, which refuses the same private folder in any spelling and any unsafe key, and sends `Cache-Control: public, max-age=31536000, immutable`.
- The web loads the photo and the audio with the signed-in session (`http` mutator) and shows them as data URLs, because `<img>` and `<audio>` cannot send the bearer token.

## Voice replies (#96)

- **Formats.** `.webm`, `.ogg`, `.m4a` or `.mp4`, with a media type (before any `;codecs=`) of `audio/webm`, `audio/ogg` or `audio/mp4`, and a signature that matches the extension: webm `1A 45 DF A3`, ogg `OggS`, m4a/mp4 `ftyp` at bytes 4..7. At most `AskTeacher:VoiceMaxSizeInMb` (5). Refusals: `422 TEACHER_VOICE_AUDIO_REQUIRED` / `AUDIO_TYPE_INVALID` / `AUDIO_TOO_LARGE`; a duration outside 1..`VoiceMaxDurationSeconds` (180) is `422 TEACHER_VOICE_DURATION_INVALID`.
- **Flow.** Only the claimer of an `Open` thread can record (the same guards as a text reply). The upload stores the audio under `teacher-threads/` and creates a `Pending` draft. `TeacherVoiceTranscriptionWorker` (every `TranscriptionSweepIntervalSeconds`, 5 s, up to `TranscriptionSweepBatchSize` drafts, a scope per draft) sends the audio to the AI service's `POST /v1/transcriptions` in `TranscriptionLanguage` (`ar`) and stores the transcript (`Ready`). The web polls the draft every 2 s, shows the transcript in an editable field, and the teacher corrects and sends it. Sending creates a `Voice` message whose `Text` is the corrected transcript, `TranscriptFinal = true`, and marks the draft `Sent`; the thread becomes `Answered`.
- **Retries.** A failed transcription schedules the next attempt at `TranscriptionRetryBaseDelaySeconds × 2^(attempts − 1)`: 15, 30 and 60 s. After `TranscriptionMaxAttempts` (4) the draft is `Failed`. There is no manual retry: the teacher types the text instead (the form stays open), or records again. So a voice message always has non-empty text (PRD rule 11).
- **Send refusals.** A `Pending` draft is `409 TEACHER_VOICE_DRAFT_NOT_READY`; a sent one is `409 TEACHER_VOICE_DRAFT_ALREADY_SENT`; another teacher's draft, or a draft of another thread, is `404 TEACHER_VOICE_DRAFT_NOT_FOUND`. The text follows the text-reply rules (`TEACHER_THREAD_REPLY_TEXT_*`).
- **`TranscriptFinal`.** Always `true` on a voice message, because the teacher reviewed the text before sending. #109 exports `Text` for both kinds, so the Q&A stays text-only training data (PRD §13).
- The draft lives in the page's state. After a reload the teacher records again, and the old draft is left behind (see Known limits).

## SLA

`SlaDueAt` is set at submission. Lists and the thread view return `isOverdue`.

- **No pause.** The clock never pauses. A follow-up starts a new window: `SlaDueAt = followUpAt + Subscriptions:AskTeacherReplySlaHours`.
- **Stages.** With `windowStart = SlaDueAt − AskTeacherReplySlaHours`: `FirstReminder` at `windowStart + AskTeacher:FirstReminderAfterHours` (12), `SecondReminder` at `windowStart + SecondReminderAfterHours` (20), `Breach` at `SlaDueAt`. Only `Open` threads count (`TeacherThread.DueSlaStages`).
- **Worker.** `TeacherThreadSlaWorker` (job `ask-teacher-sla`, every `SlaSweepIntervalSeconds`, 60 s) lists up to `SlaSweepBatchSize` (50) `Open` threads with a due stage that has no event row yet, then sends one `ProcessTeacherThreadSlaCommand` per thread in its own scope. Failed ids are skipped until the sweep reaches the end of the backlog (the #81 pattern). The worker never writes `TeacherThreads`, so it cannot bump a thread's `xmin` under a teacher's reply; the unique `(ThreadId, Kind, SlaDueAt)` index makes each stage exactly-once per window.
- **Catch-up.** When several stages are missing (the worker was down, or a first deploy), all of them are recorded in one save, one push goes out for the highest missing reminder, and a breach is never pushed.
- **Recipients.** The claimer if the thread is claimed, otherwise every teacher assigned to the subject. With no recipients the event is still recorded. Reminders are in-app only: a live toast «تذكير: سؤال طالب بانتظار ردك.» (`teacherThreadReminder { threadId, kind }` on the hub) and the «تذكيرات» card on `/teacher/inbox`, which covers teachers who were offline.
- **Breach.** A `Breach` row, the counter `elmanhg.ask_teacher.sla_events{elmanhg.kind="Breach"}`, a Warning log with the thread id, and the Prometheus alert `AskTeacherSlaBreached` through Alertmanager (docs/observability.md). A breach does not unclaim, reassign or refund (product questions, see the #97 report).

## Options

`AskTeacherOptions` (section `AskTeacher`, validated on start): `QuestionTextMaxLength` 2000, `ImageMaxSizeInMb` 5, `ThreadListMaxPageSize` 50, `ReplyTextMaxLength` 4000, and for voice replies (#96) `VoiceMaxSizeInMb` 5, `VoiceMaxDurationSeconds` 180, `TranscriptionLanguage` `ar`, `TranscriptionSweepEnabled` true, `TranscriptionSweepIntervalSeconds` 5, `TranscriptionSweepBatchSize` 5, `TranscriptionMaxAttempts` 4, `TranscriptionRetryBaseDelaySeconds` 15, and for the SLA (#97) `SlaSweepEnabled` true, `SlaSweepIntervalSeconds` 60, `SlaSweepBatchSize` 50, `FirstReminderAfterHours` 12, `SecondReminderAfterHours` 20, `ReminderListMaxCount` 20; `AskTeacherOptionsValidator` requires `FirstReminderAfterHours` < `SecondReminderAfterHours` < `Subscriptions:AskTeacherReplySlaHours`. The API's transcription call has its own timeout, `AiService:TranscriptionTimeoutSeconds` 150. The quota and the SLA stay in `SubscriptionsOptions`.

## API

The student endpoints use `DefaultCodes.AskTeacherSubmit` (Student only). Reads are owner-scoped.

| Method | Route | Response | Errors |
|---|---|---|---|
| GET | `/api/teacher-threads/context?lessonId|questionId|attemptId` | `TeacherThreadContextResult` (names, plus the question stem for a question or attempt) | `403 ASK_TEACHER_REQUIRES_SUBSCRIPTION`; `422 TEACHER_THREAD_CONTEXT_INVALID`; `404 LESSON_NOT_FOUND` / `QUESTION_NOT_FOUND` / `ATTEMPT_NOT_FOUND`; `409 TEACHER_THREAD_EXAM_IN_PROGRESS` |
| POST | `/api/teacher-threads` (multipart: `text`, one context id, optional `image`) | `TeacherThreadResult` | `422 TEACHER_THREAD_TEXT_REQUIRED` / `TEXT_TOO_LONG` / `CONTEXT_INVALID` / `IMAGE_TYPE_INVALID` / `IMAGE_TOO_LARGE`; `403 ASK_TEACHER_REQUIRES_SUBSCRIPTION` / `ASK_TEACHER_MONTHLY_LIMIT_REACHED`; context `404`s; `409 TEACHER_THREAD_EXAM_IN_PROGRESS` |
| GET | `/api/teacher-threads?pageNumber&pageSize` | `PageData<TeacherThreadSummaryResult>`, newest first, with the first student message as `questionText` | `422 TEACHER_THREAD_PAGE_NUMBER_INVALID` / `PAGE_SIZE_INVALID` |
| GET | `/api/teacher-threads/{threadId}` | `TeacherThreadResult` with the context and the messages (`isFromStudent`) | `404 TEACHER_THREAD_NOT_FOUND` (also for another student's thread) |
| POST | `/api/teacher-threads/{threadId}/read` | empty `200`; marks the teacher's replies read | `404 TEACHER_THREAD_NOT_FOUND` (also for another student's thread) |
| POST | `/api/teacher-threads/{threadId}/follow-ups` (`{ "text" }`) | `TeacherThreadResult` (`Open`, new `slaDueAt`) | `422 TEACHER_THREAD_TEXT_REQUIRED` / `TEXT_TOO_LONG`; `404 TEACHER_THREAD_NOT_FOUND`; `409 TEACHER_THREAD_FOLLOW_UP_NOT_ALLOWED` / `TEACHER_THREAD_MODIFIED_CONCURRENTLY` |
| POST | `/api/teacher-threads/{threadId}/rating` (`{ "rating" }`) | `TeacherThreadResult` (`rating`, `Closed`) | `422 TEACHER_THREAD_RATING_INVALID`; `404`; `409 TEACHER_THREAD_NOT_ANSWERED` / `ALREADY_RATED` / `MODIFIED_CONCURRENTLY` |
| GET | `/api/media/teacher-threads/{file}` | the photo or voice-reply bytes | empty `404` for everyone but the owner, a teacher of the subject or an admin; unsent draft audio is always `404` |

The teacher endpoints use `DefaultCodes.AskTeacherReply` (a Teacher assigned to the thread's subject, or an Admin).

| Method | Route | Response | Errors |
|---|---|---|---|
| GET | `/api/teacher-inbox?filter&pageNumber&pageSize` | `PageData<TeacherInboxItemResult>` (question, subject / lesson, `studentName`, `teacherName?`, `isClaimedByMe`, status, `isOverdue`, dates) | `400` unknown filter; `422 TEACHER_THREAD_PAGE_NUMBER_INVALID` / `PAGE_SIZE_INVALID` |
| GET | `/api/teacher-inbox/{threadId}` | `TeacherInboxThreadResult` (context, names, `isClaimedByMe`, `canClaim`, `canReply`, `claimedAt`, messages) | `404 TEACHER_THREAD_NOT_FOUND`; `403 SUBJECT_OUT_OF_SCOPE` |
| POST | `/api/teacher-inbox/{threadId}/claim` | `TeacherInboxThreadResult` | `404`; `403 SUBJECT_OUT_OF_SCOPE`; `409 TEACHER_THREAD_ALREADY_CLAIMED` / `TEACHER_THREAD_MODIFIED_CONCURRENTLY` |
| POST | `/api/teacher-inbox/{threadId}/replies` (`{ "text" }`) | `TeacherInboxThreadResult` | `422 TEACHER_THREAD_REPLY_TEXT_REQUIRED` / `TOO_LONG`; `404`; `403 SUBJECT_OUT_OF_SCOPE`; `409 TEACHER_THREAD_NOT_CLAIMED` / `ALREADY_CLAIMED` / `NOT_AWAITING_REPLY` / `MODIFIED_CONCURRENTLY` |
| GET | `/api/teacher-inbox/reminders` | `List<TeacherInboxReminderResult>` (`threadId`, subject / lesson, `questionText`, the latest reminder `kind` of the current window, `isClaimedByMe`, `isOverdue`, `slaDueAt`): `Open` threads claimed by the caller or unclaimed in the caller's subjects (every subject for an admin) with a reminder in their current window, by `SlaDueAt`, at most `ReminderListMaxCount` (20) | — |
| GET | `/api/teacher-inbox/voice-settings` | `VoiceReplySettingsResult` (`maxDurationSeconds`, `maxSizeInMb`) | — |
| POST | `/api/teacher-inbox/{threadId}/voice-drafts` (multipart: `audio`, `durationSeconds`) | `TeacherVoiceDraftResult` (`id`, `status`, `transcript?`, `audioDurationSeconds`, `recordedAt`, `transcribedAt?`) | `422 TEACHER_VOICE_AUDIO_REQUIRED` / `AUDIO_TYPE_INVALID` / `AUDIO_TOO_LARGE` / `DURATION_INVALID`; `404`; `403 SUBJECT_OUT_OF_SCOPE`; `409 TEACHER_THREAD_NOT_CLAIMED` / `ALREADY_CLAIMED` / `NOT_AWAITING_REPLY` |
| GET | `/api/teacher-inbox/{threadId}/voice-drafts/{draftId}` | `TeacherVoiceDraftResult` | `404 TEACHER_VOICE_DRAFT_NOT_FOUND` (also another teacher's draft) |
| POST | `/api/teacher-inbox/{threadId}/voice-replies` (`{ "draftId", "text" }`) | `TeacherInboxThreadResult` | `422 TEACHER_VOICE_DRAFT_ID_REQUIRED` / `TEACHER_THREAD_REPLY_TEXT_*`; `404` thread / draft; `403 SUBJECT_OUT_OF_SCOPE`; `409` claim and status codes, `TEACHER_VOICE_DRAFT_NOT_READY` / `ALREADY_SENT` / `TEACHER_THREAD_MODIFIED_CONCURRENTLY` |

`TeacherMessageResult` (student and teacher thread results) carries `audioUrl` and `audioDurationSeconds`. `TeacherThreadResult` carries `rating`, `closedAt`, `canFollowUp` and `canRate`; `TeacherInboxThreadResult` carries `rating` (#97).

The SignalR hub `/api/hubs/notifications` (#97) requires any signed-in user (JWT in the `Authorization` header, or `?access_token=` on the hub path only; `401` on negotiate otherwise). Server → client events: `teacherReplyReceived { threadId }` to the student, `teacherThreadReminder { threadId, kind }` to the teachers. Clients call no hub methods.

`canClaim` is `TeacherId == null && Status != Closed`; `canReply` is "claimed by the caller and `Open`". The web holds no rules of its own.

## Web

- `/student/ask`: the threads list (newest first, paged with `?page=`), each with its question, subject / lesson / date and a badge: «بانتظار الرد · متبقٍ N ساعة», «متأخر», «تم الرد» or «مغلق». The allowance line «الرصيد الشهري: X / N» and «سؤال جديد». Without the add-on: the upsell «هذه الخدمة إضافة مدفوعة وتتطلب الباقة الأساسية.» with «الاشتراك». Loading, empty, error-with-retry states.
- `/student/ask-new?lessonId=|questionId=|attemptId=`: the attached context (with the question stem when there is one), the question text, the optional photo, the SLA note from the plan catalogue and «إرسال». Without a context the student picks a subject and then a lesson (grouped by unit). A used-up quota shows a notice instead of the form. On success the thread view opens.
- `/student/thread/$threadId`: the context card with the reply-due time and badge, then the messages; the photo is fetched with the session. A voice reply shows an audio player («الرد الصوتي») with its length («المدة: ٠:٤٢»), then «نص التفريغ الصوتي:» and the text. Opening a thread with an unread reply marks it read, and the list's «رد جديد» disappears.
- `/teacher/inbox` «أسئلة الطلاب»: pill tabs الكل / غير مُستلمة / الخاصة بي (`?filter=Unclaimed|Mine`, changing the tab clears `?page=`), then the threads, each with its question, «subject / lesson · student · date», who claimed it («غير مُستلم», «مستلم بواسطتك» or «المعلّم: …») and the SLA badge. Loading, empty, error-with-retry states, paged.
- `/teacher/thread/$threadId`: the context card with «الطالب: … · المعلّم: …», the messages (the student's labelled with their display name), then «استلام السؤال» for an unclaimed thread, the reply card «الرد» for the claimer with a «نص / صوت» choice: text shows the text form («ردّك», «إرسال الرد»); voice shows «تسجيل» with the maximum length, then «إيقاف» with a timer while recording, a local preview with «إعادة التسجيل», «جارٍ رفع التسجيل…», «جارٍ تفريغ التسجيل…», and the editable «نص التفريغ الصوتي (يمكنك تصحيحه قبل الإرسال)» with «إرسال الرد». A failed transcription shows «تعذّر التفريغ التلقائي. اكتب نص الرد يدويًا.» above an empty field. A refused microphone shows «لم يُسمح بالوصول إلى الميكروفون.», and a browser without recording shows «المتصفح لا يدعم التسجيل الصوتي.». The recorder uses no red (design-system rule 1), and it stops by itself at the maximum length. Otherwise a note: «هذه المحادثة مستلمة بواسطة معلّم آخر.», «تم الرد على هذا السؤال.» or «أُغلق هذا السؤال.». A lost claim race shows «استلم معلم آخر هذا السؤال.» and refreshes the thread. A thread outside the teacher's subjects shows the `SUBJECT_OUT_OF_SCOPE` error.
- Entry points: «اسأل معلّم» on an unlocked lesson page (`lessonId`) and in the quiz feedback next to «اسأل المساعد» (`attemptId`).
- Follow-up and rating (#97), on `/student/thread/$threadId` after the messages: an answered thread shows the card «سؤال متابعة (مرة واحدة فقط)» («سؤال المتابعة», «إرسال المتابعة»; a blank text shows «اكتب سؤال المتابعة أولًا» without a request) and the card «قيّم الإجابة وأغلق السؤال» with five buttons (1–5, each a number and a star, labelled «N من 5»). After the teacher's final reply only «قيّم الإجابة» is offered. A saved rating shows «التقييم:» and five stars (`img` «N من 5») on the student's and the teacher's thread pages. Toasts: «تم إرسال سؤال المتابعة.», «شكرًا على تقييمك.»; a refused rating shows its error as a toast.
- Reminders (#97): `/teacher/inbox` shows «تذكيرات» between the heading and the tabs («اقترب موعد الرد على هذه الأسئلة:», each question with «subject / lesson · claim · التذكير الأول|الثاني» and the SLA badge, linking to the thread). It renders nothing while loading, on an error or when empty; the warning icon carries the emphasis, never red.
- Realtime (#97): the student and teacher shells hold one hub connection each (none outside them). Toasts «وصل رد من المعلّم على سؤالك.» and «تذكير: سؤال طالب بانتظار ردك.»; a malformed event is ignored.

## Known limits

- The quota is a soft limit: two questions sent in parallel at 19/20 can both pass.
- The photo is written just before the save; a failed save leaves an orphan file (no cleanup job).
- The photo access check looks the file up by its URL on every request (indexed).
- A teacher unassigned from a subject loses access to threads they claimed there.
- Admins can claim and reply through the API; there is no admin inbox screen yet.
- Voice drafts that are never sent, and recordings discarded with «إعادة التسجيل», leave their audio in storage (no cleanup job, like the photo).
- The recording's duration is reported by the browser; the size limit is the hard cap.
- The transcription worker assumes a single API instance per environment (no leasing between instances). So does the SLA worker, and the SignalR hub keeps its connections in memory on that one instance.
- Realtime pushes are best effort: a missed push is covered by the refetch when a page opens and by the reminders card.
- Reminder timing reads the current `AskTeacherReplySlaHours`; changing it moves the reminders of windows already running.
