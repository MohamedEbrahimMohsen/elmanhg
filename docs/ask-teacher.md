# Ask a Teacher

A student who holds the Ask a Teacher add-on (on top of an entitled Base plan) sends a written question, with its context attached and an optional photo, to the teachers of a subject (PRD §12). #94 creates the thread and the student's side. #95 adds the teacher inbox, claiming, text replies and the student's new-reply mark. Voice arrives with #96. Follow-up, rating, closing, reminders and realtime push arrive with #97.

## Model

Both tables map to PRD §15. #94 ships only the columns it writes; later stories add theirs in their own migrations (migrations `AddTeacherThreads` and `AddTeacherThreadClaims`).

### TeacherThread (aggregate root)

| Field | PRD §15 | Notes |
|---|---|---|
| `Id` | `id` | |
| `StudentId` | `student_id` | FK `Users`, restrict. `CreatedBy` is the student too. |
| `SubjectId` | `subject_id` | FK `Subjects`, restrict. Copied from the context for inbox routing (#95). |
| `Context` | `context_json` | jsonb snapshot, see Context. |
| `Status` | `status` | `Open`, `Answered`, `Closed`, stored as a string. #94 only creates `Open`. |
| `SubmittedAt` | `submitted_at` | Truncated to microseconds so the create response equals later reads. |
| `SlaDueAt` | `sla_due_at` | `SubmittedAt + Subscriptions:AskTeacherReplySlaHours` (24). |
| `TeacherId` | `teacher_id?` | FK `Users`, restrict. The teacher (or admin) who claimed the thread; null until claimed (#95). |
| `ClaimedAt` | `claimed_at?` | Set by the first claim, truncated to microseconds (#95). |
| `Version` | — | `xmin` row version; no DDL. |

Indexes: `(StudentId, SubmittedAt)` for the student's list and the quota count, `(SubjectId, Status, SubmittedAt)` for the teacher inbox.

### TeacherMessage (child)

| Field | PRD §15 | Notes |
|---|---|---|
| `Id` | `id` | Set by the domain. |
| `ThreadId` | `thread_id` | FK `TeacherThreads`, restrict. |
| `SenderId` | `sender_id` | FK `Users`, restrict. The student for the first message. |
| `Kind` | `kind` | `Text` or `Voice` (string). #94 writes `Text`. |
| `Text` | `text` | Required, trimmed. A blank text is `TEACHER_MESSAGE_TEXT_REQUIRED`. |
| `ImageUrl` | `image_url?` | The stored photo, see Image. |
| `CreatedAt` | `created_at` | Equals `SubmittedAt` for the first message. |
| `StudentReadAt` | `student_read_at?` | Set when the student opens the thread; null for student messages. Kept on the messages so a read never changes the thread's `xmin` under a teacher's in-flight reply (#95). |

Indexes: `(ThreadId, CreatedAt)` and a filtered index on `ImageUrl` for the photo access check.

Both entities are in the global soft-delete filter. A thread always has at least one message, and the first is the student's.

**Status machine:** #95: `Open` → `Answered` when the claiming teacher replies. #97: the one follow-up (`Answered` → `Open`), the second reply and `Closed` (rating). The server computes `isOverdue = Status == Open && now >= SlaDueAt`; the web never compares clocks for it.

Threads are not audited: a student question is activity, and the row itself is the record (PRD §14).

## Teacher inbox

- **Scope.** A teacher sees the threads of the subjects they are assigned to (`TeacherSubject`). No assignment means an empty list (fail closed). Opening, claiming or replying to a thread of another subject is `403 SUBJECT_OUT_OF_SCOPE`. An admin sees every subject and may claim and reply like a teacher (PRD §16); there is no admin inbox screen yet.
- **Filters.** `filter=All` (default), `Unclaimed` (no teacher yet) or `Mine` (claimed by the caller). An unknown value is refused by model binding with `400`.
- **Order.** `Open` threads first, then by `SlaDueAt` ascending, then by id. Paged with `pageNumber` / `pageSize` (max `AskTeacher:ThreadListMaxPageSize`, 50; `422 TEACHER_THREAD_PAGE_NUMBER_INVALID` / `PAGE_SIZE_INVALID`).
- **Identity.** The teacher sees the student's display name only, and the claimer's display name (PRD §8.4, §14).
- **Claim.** The first claim wins. Claiming a thread you already own changes nothing. Claiming a thread owned by someone else is `409 TEACHER_THREAD_ALREADY_CLAIMED`. Two claims at the same moment race on the thread's `xmin`: the loser gets `409 TEACHER_THREAD_MODIFIED_CONCURRENTLY` (mapped once in `AppDbContext.SaveChangesAsync`).
- **Reply.** Only the claiming user may reply, and only while the thread is `Open`. Unclaimed is `409 TEACHER_THREAD_NOT_CLAIMED`, claimed by someone else is `409 TEACHER_THREAD_ALREADY_CLAIMED` (the claim check runs first), not `Open` is `409 TEACHER_THREAD_NOT_AWAITING_REPLY`. The text is required and at most `AskTeacher:ReplyTextMaxLength` (4000) (`422 TEACHER_THREAD_REPLY_TEXT_REQUIRED` / `TOO_LONG`). A reply is a `Text` message from the teacher and moves the thread to `Answered`.
- Claims and replies are not audited; `TeacherId`, `ClaimedAt` and the message rows are the record. The question, its context ids and the teacher's final reply text stay in `TeacherThread.Context` and `TeacherMessage`, which #109 turns into training records.

## Student notification

In-app only. A thread with a teacher message whose `StudentReadAt` is null reports `hasUnreadReply` in the student's list and thread results, and the list shows «رد جديد». Opening the thread calls `POST /api/teacher-threads/{threadId}/read`, which stamps the teacher's messages (the first read time is kept). #95 sends no WhatsApp or Email; #97 adds realtime push.

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
- A thread counts in the calendar month of `Subscriptions:DailyQuotaTimeZone` (Africa/Cairo) in which it was submitted. The month's bounds are converted to UTC (`AskTeacherGate.CurrentQuotaMonth`). A follow-up (#95) does not count.
- Gate order on create: validation `422` → user `401` → no add-on `403 ASK_TEACHER_REQUIRES_SUBSCRIPTION` → quota `403 ASK_TEACHER_MONTHLY_LIMIT_REACHED` (context `limit`) → context `404`s / `409 TEACHER_THREAD_EXAM_IN_PROGRESS` → store the photo → one save. The context preview applies the same add-on check (`403 ASK_TEACHER_REQUIRES_SUBSCRIPTION`) before it resolves anything; it does not check the quota.
- `GET /api/subscriptions/usage` exposes `monthlyAskTeacherQuestionLimit`, `askTeacherQuestionsUsedThisMonth` and `askTeacherQuestionsRemainingThisMonth` (never below 0). See `docs/subscriptions.md`.

## Image

- One photo per question, sent in the same multipart request (field `image`). Allowed: `.png`, `.jpg`, `.jpeg`, `.webp` with a matching content type and matching file signature (magic bytes: PNG `89 50 4E 47 0D 0A 1A 0A`, JPEG `FF D8 FF`, WEBP `RIFF….WEBP`), at most `AskTeacher:ImageMaxSizeInMb` (5). GIF, SVG and a file whose bytes do not match its extension are refused (`422 TEACHER_THREAD_IMAGE_TYPE_INVALID`, `422 TEACHER_THREAD_IMAGE_TOO_LARGE`).
- Stored through `IFileStorage` under the key `teacher-threads/{random}{ext}` after every check has passed. `TeacherMessage.ImageUrl` is `/api/media/teacher-threads/{random}{ext}`.
- **Photos are private.** `GET /api/media/teacher-threads/...` is served only to an authenticated caller who is the owning student, a teacher assigned to the thread's subject, or an admin (`CanViewTeacherThreadImageQuery`, `TeacherThreadMediaMiddleware`). Everyone else, including anonymous callers and unknown files, gets an empty `404`. The response carries `nosniff` and `Cache-Control: private, no-store`.
- The public static-file mount for `/api/media` never serves anything inside `teacher-threads` (`PublicMediaFileProvider`), whatever the spelling of the path (case, doubled or encoded separators, trailing dots, `..`, `~` short names).
- The web loads the photo with the signed-in session (`http` mutator) and shows it as a data URL, because an `<img>` tag cannot send the bearer token.

## SLA

`SlaDueAt` is set at submission. Lists and the thread view return `isOverdue`. #97 adds the reminders and the breach alert.

## Options

`AskTeacherOptions` (section `AskTeacher`, validated on start): `QuestionTextMaxLength` 2000, `ImageMaxSizeInMb` 5, `ThreadListMaxPageSize` 50, `ReplyTextMaxLength` 4000. The quota and the SLA stay in `SubscriptionsOptions`.

## API

The student endpoints use `DefaultCodes.AskTeacherSubmit` (Student only). Reads are owner-scoped.

| Method | Route | Response | Errors |
|---|---|---|---|
| GET | `/api/teacher-threads/context?lessonId|questionId|attemptId` | `TeacherThreadContextResult` (names, plus the question stem for a question or attempt) | `403 ASK_TEACHER_REQUIRES_SUBSCRIPTION`; `422 TEACHER_THREAD_CONTEXT_INVALID`; `404 LESSON_NOT_FOUND` / `QUESTION_NOT_FOUND` / `ATTEMPT_NOT_FOUND`; `409 TEACHER_THREAD_EXAM_IN_PROGRESS` |
| POST | `/api/teacher-threads` (multipart: `text`, one context id, optional `image`) | `TeacherThreadResult` | `422 TEACHER_THREAD_TEXT_REQUIRED` / `TEXT_TOO_LONG` / `CONTEXT_INVALID` / `IMAGE_TYPE_INVALID` / `IMAGE_TOO_LARGE`; `403 ASK_TEACHER_REQUIRES_SUBSCRIPTION` / `ASK_TEACHER_MONTHLY_LIMIT_REACHED`; context `404`s; `409 TEACHER_THREAD_EXAM_IN_PROGRESS` |
| GET | `/api/teacher-threads?pageNumber&pageSize` | `PageData<TeacherThreadSummaryResult>`, newest first, with the first student message as `questionText` | `422 TEACHER_THREAD_PAGE_NUMBER_INVALID` / `PAGE_SIZE_INVALID` |
| GET | `/api/teacher-threads/{threadId}` | `TeacherThreadResult` with the context and the messages (`isFromStudent`) | `404 TEACHER_THREAD_NOT_FOUND` (also for another student's thread) |
| POST | `/api/teacher-threads/{threadId}/read` | empty `200`; marks the teacher's replies read | `404 TEACHER_THREAD_NOT_FOUND` (also for another student's thread) |
| GET | `/api/media/teacher-threads/{file}` | the photo bytes | empty `404` for everyone but the owner, a teacher of the subject or an admin |

The teacher endpoints use `DefaultCodes.AskTeacherReply` (a Teacher assigned to the thread's subject, or an Admin).

| Method | Route | Response | Errors |
|---|---|---|---|
| GET | `/api/teacher-inbox?filter&pageNumber&pageSize` | `PageData<TeacherInboxItemResult>` (question, subject / lesson, `studentName`, `teacherName?`, `isClaimedByMe`, status, `isOverdue`, dates) | `400` unknown filter; `422 TEACHER_THREAD_PAGE_NUMBER_INVALID` / `PAGE_SIZE_INVALID` |
| GET | `/api/teacher-inbox/{threadId}` | `TeacherInboxThreadResult` (context, names, `isClaimedByMe`, `canClaim`, `canReply`, `claimedAt`, messages) | `404 TEACHER_THREAD_NOT_FOUND`; `403 SUBJECT_OUT_OF_SCOPE` |
| POST | `/api/teacher-inbox/{threadId}/claim` | `TeacherInboxThreadResult` | `404`; `403 SUBJECT_OUT_OF_SCOPE`; `409 TEACHER_THREAD_ALREADY_CLAIMED` / `TEACHER_THREAD_MODIFIED_CONCURRENTLY` |
| POST | `/api/teacher-inbox/{threadId}/replies` (`{ "text" }`) | `TeacherInboxThreadResult` | `422 TEACHER_THREAD_REPLY_TEXT_REQUIRED` / `TOO_LONG`; `404`; `403 SUBJECT_OUT_OF_SCOPE`; `409 TEACHER_THREAD_NOT_CLAIMED` / `ALREADY_CLAIMED` / `NOT_AWAITING_REPLY` / `MODIFIED_CONCURRENTLY` |

`canClaim` is `TeacherId == null && Status != Closed`; `canReply` is "claimed by the caller and `Open`". The web holds no rules of its own.

## Web

- `/student/ask`: the threads list (newest first, paged with `?page=`), each with its question, subject / lesson / date and a badge: «بانتظار الرد · متبقٍ N ساعة», «متأخر», «تم الرد» or «مغلق». The allowance line «الرصيد الشهري: X / N» and «سؤال جديد». Without the add-on: the upsell «هذه الخدمة إضافة مدفوعة وتتطلب الباقة الأساسية.» with «الاشتراك». Loading, empty, error-with-retry states.
- `/student/ask-new?lessonId=|questionId=|attemptId=`: the attached context (with the question stem when there is one), the question text, the optional photo, the SLA note from the plan catalogue and «إرسال». Without a context the student picks a subject and then a lesson (grouped by unit). A used-up quota shows a notice instead of the form. On success the thread view opens.
- `/student/thread/$threadId`: the context card with the reply-due time and badge, then the messages; the photo is fetched with the session. Opening a thread with an unread reply marks it read, and the list's «رد جديد» disappears.
- `/teacher/inbox` «أسئلة الطلاب»: pill tabs الكل / غير مُستلمة / الخاصة بي (`?filter=Unclaimed|Mine`, changing the tab clears `?page=`), then the threads, each with its question, «subject / lesson · student · date», who claimed it («غير مُستلم», «مستلم بواسطتك» or «المعلّم: …») and the SLA badge. Loading, empty, error-with-retry states, paged.
- `/teacher/thread/$threadId`: the context card with «الطالب: … · المعلّم: …», the messages (the student's labelled with their display name), then «استلام السؤال» for an unclaimed thread, the text reply form («الرد», «ردّك», «إرسال الرد») for the claimer, or a note: «هذه المحادثة مستلمة بواسطة معلّم آخر.», «تم الرد على هذا السؤال.» or «أُغلق هذا السؤال.». A lost claim race shows «استلم معلم آخر هذا السؤال.» and refreshes the thread. A thread outside the teacher's subjects shows the `SUBJECT_OUT_OF_SCOPE` error.
- Entry points: «اسأل معلّم» on an unlocked lesson page (`lessonId`) and in the quiz feedback next to «اسأل المساعد» (`attemptId`).

## For later stories

- **#96:** `AudioUrl`, `TranscriptFinal` and the S3-compatible storage adapter (private bucket, presigned GET URLs), which will replace the Local provider's guarded path. `ReplyWithVoice` reuses the claim and status guards.
- **#97:** `ClosedAt`, `Rating`, the follow-up (`Answered` → `Open`, resets `SlaDueAt`), closing after the second reply, the `(Status, SlaDueAt)` index, reminders and realtime push.

## Known limits

- The quota is a soft limit: two questions sent in parallel at 19/20 can both pass.
- The photo is written just before the save; a failed save leaves an orphan file (no cleanup job).
- The photo access check looks the file up by its URL on every request (indexed).
- A teacher unassigned from a subject loses access to threads they claimed there.
- Admins can claim and reply through the API; there is no admin inbox screen yet.
