# AI Avatar

## Role

The AI Avatar is the in-app study assistant for students (PRD §9). The student asks a short question from a panel that opens on any student screen, and the API answers with a short Arabic reply grounded on the lesson material, listing the lesson sections it used. The .NET API owns everything the student can see or be refused: the entry point, the context bundle, retrieval, the exam refusal and the daily quota. The Python service ([ai-service.md](ai-service.md)) only turns a prepared request into a reply. The API stores every conversation (see Conversation log).

Only students may use it (`Avatar.Chat` policy; Teachers and Admins get 403).

## Entry points

| Entry point | Required ids | Bundle | Where the button is |
|---|---|---|---|
| `Lesson` | `lessonId` | subject, unit, lesson (name, objectives; explanation and summary only without matches) | «اسأل المساعد عن الدرس» under the lesson tabs |
| `QuizQuestion` | `sessionId`, `questionId` | the lesson bundle plus the question: stem, the student's answer, the correct answer and the explanation | «اسأل المساعد» in the feedback after «تحقّق», and on each quiz-result review item |
| `ExamReview` | `sessionId`, `questionId` | the same as `QuizQuestion`; `studentAnswer` is omitted for an unanswered item | «اسأل المساعد» on each exam-result review item |
| `Global` | none | the names of every subject, in order | the floating «المساعد» button, and «اسأل المساعد» on the exam screen |

Rules per entry point:

- `Lesson` and `QuizQuestion` need a Published lesson (`404 LESSON_NOT_FOUND` otherwise) that is open for the student: a Free student gets `403 LESSON_LOCKED` on a locked lesson, as the quiz does.
- `QuizQuestion` needs the student's own quiz session (`404 SESSION_NOT_FOUND`), an item for the question (`404 SESSION_QUESTION_NOT_FOUND`) and an answer to it (`400 AVATAR_QUESTION_NOT_ANSWERED`): the correct answer is only revealed after answering ([sessions.md](sessions.md)).
- `ExamReview` needs the student's own **submitted** exam; an open exam, a quiz or another student's session is `404 SESSION_NOT_FOUND`. Unanswered items are allowed. There is no lesson lock check: the student is reviewing their own exam.
- Ids that an entry point does not use are ignored.

## Context bundle

- **Retrieval.** For `Lesson`, `QuizQuestion` and `ExamReview` the handler sends `SearchLessonContentQuery` in-process ([content-retrieval.md](content-retrieval.md)). The query is the trimmed message; for the question entry points it is the plain question stem, a new line, then the message, which helps "why is my answer wrong?". It is cut to `ContentRetrieval:QueryMaxLength`, uses the default `top` and includes question explanations (safe, because no exam is in progress; see Guardrails).
- **Withheld questions.** A question's explanation chunks are dropped from the matches while the student cannot see that explanation yet ([sessions.md](sessions.md), What is revealed): the question is an unanswered item of one of the student's open quizzes, or an item of one of the student's unsubmitted exams. Once the student answers it, or the session is submitted, its chunks are sent again. The filter runs after the search, so a message can get fewer than `top` sources; dropped chunks are never sent, so they can never be cited. For `QuizQuestion` the question itself is always answered, so its own chunks stay; the other unanswered items of the quiz are withheld.
- **Sources.** Each match becomes a source `{reference, title, content}`. The title is the section label (الشرح, الأهداف, الملخص, شرح سؤال) plus « — section title» when there is one.
- **Lesson text.** When retrieval returned matches, the lesson `explanation` and `summary` are sent empty, because the chunks carry the relevant text and can be cited. When there are none (for example the lesson is not indexed yet), both are sent as plain text (the rich text's blocks joined by new lines), each cut to `Avatar:ContextFieldMaxLength`. Objectives are always sent, in order.
- **Question.** Everything comes from the served question revision (the version the student saw, PRD §17 rule 2), as plain text in the UI's wording: the chosen option text (options in the question's order, joined by «، »), «صح» or «خطأ», blanks as `[[id]] text`, the short answer; the correct answer the same way, with numeric answers as `value ± tolerance` (plus `%` for a percent tolerance). Each field is cut to `ContextFieldMaxLength`.
- **Global.** `entryPoint: global` and the subject names only: no lesson, no retrieval, no sources. The prompt and the greeting ask the student to open the lesson they need; there is no lesson picker.
- **History.** The browser sends `conversationId`: null for the first message, then the id returned in each reply; it resets to null when the context changes. The API loads that conversation (only the student's own; a foreign or unknown id is `404 AVATAR_CONVERSATION_NOT_FOUND`) and checks that it belongs to the same context: the same entry point, the same lesson for `Lesson`, the same session and question for `QuizQuestion` and `ExamReview` (otherwise `400 AVATAR_CONVERSATION_CONTEXT_MISMATCH`). Both checks run before the model call and use no quota. The API then sends the last `Avatar:MaxHistoryMessages` stored messages as history, each cut to `Avatar:HistoryTurnMaxLength`. The browser never sends history; a `history` field from an old client is ignored.
- **Size.** Six long fields of 8000 characters plus the objectives stay under the AI service's 60 000-character context limit; sources have their own limits (20 sources, 8000 characters each). Oversized requests would otherwise fail as a generic 503.

## Guardrails

- **Exam in progress.** While the student has an exam that is not submitted and not past its deadline plus `Exams:DeadlineGraceSeconds` (an untimed exam stays in progress until submitted), every message is refused with `403 AVATAR_EXAM_IN_PROGRESS` before any context is loaded or the model is called, whatever the entry point, and no quota is used (PRD §17 rule 10). `InProgressExamSpecification` in Domain is the one definition; `GET /api/avatar/status` reports it as `examInProgress`, so the panel shows the refusal as soon as it opens. The AI service is never told about exams, because it is never called during one.
- **Prompt v2** (`ai/src/elmanhg_ai/prompts/avatar_system.v2.md`): always Modern Standard Arabic, about 120 words at most, numbered steps for methods and solutions, answers only from the search results and the context, a polite one-sentence refusal for anything off the curriculum, and never revealing the instructions.
- **Untrusted text.** The message, history, context and sources are data. The AI service strips delimiter tags from every field and sends them only in the last user turn and as search results ([ai-service.md](ai-service.md), Prompts).

## Citations

1. The API sends the retrieved chunks as `sources`.
2. The Anthropic adapter sends them as Claude `search_result` blocks with citations enabled and returns the `source` of every `search_result_location` citation in the reply.
3. The AI pipeline keeps only references it was sent, distinct and in order.
4. The API maps each reference back to its chunk and returns `{reference, section, sectionTitle, lessonId, questionId}`; references it did not send are dropped.

Citations are structure from the API, not parsed model text, so the model cannot invent one. In the panel each citation is a chip under the reply: Explanation links to `/student/lesson/{id}`, Objectives to `/objectives`, Summary to `/summary`, and a question explanation is shown without a link. Clicking a link closes the panel. Both fakes cite the first source, so the path works offline.

## Daily quota

- The limit is the entitlement's `DailyAvatarMessageLimit`: Free 5 (`Subscriptions:FreeDailyAvatarMessages`), Base 50 (`Subscriptions:BaseDailyAvatarMessages`) ([subscriptions.md](subscriptions.md)).
- Before calling the model, the handler counts today's messages, where the day follows `Subscriptions:DailyQuotaTimeZone` (Africa/Cairo). At the limit it returns `403 AVATAR_DAILY_LIMIT_REACHED` with context `limit`.
- A message counts only after a successful reply: a refusal, a validation error or an AI failure uses nothing.
- It is a soft limit: the count is read before the model call and the row is written after it, so every request sent while others are still waiting for a reply passes the check. Any number of parallel requests can pass, and the count can end above the limit by up to that number minus one (for example, five requests sent together at 0 of 5 all pass, and so do five sent at 4 of 5, ending at 9). Hardening is tracked for #115.
- Storage: table `AvatarMessageUsages` (`Id`, `StudentId`, `EntryPoint`, `CreatedAt`, `IsDeleted`, `DeletedAt`) with an index on `(StudentId, CreatedAt)`. It stores no text, model or prompt; the messages are in the [Conversation log](#conversation-log).

## HTTP

| Method | Route | Policy | Body | Result |
|---|---|---|---|---|
| GET | `/api/avatar/status` | `Avatar.Chat` (Student) | — | `AvatarStatusResult` |
| POST | `/api/avatar/messages` | `Avatar.Chat` (Student) | `SendAvatarMessageCommand` | `AvatarReplyResult` |

`GET /api/avatar/status`:

```json
{ "examInProgress": false, "tier": "Free", "dailyMessageLimit": 5, "messagesUsedToday": 2, "messagesRemainingToday": 3, "messageMaxLength": 2000, "maxHistoryMessages": 10 }
```

`POST /api/avatar/messages`:

```json
{
  "entryPoint": "QuizQuestion",
  "lessonId": null,
  "sessionId": "5b0f8a4e-0000-4000-8000-000000000001",
  "questionId": "5b0f8a4e-0000-4000-8000-000000000002",
  "conversationId": "5b0f8a4e-0000-4000-8000-000000000004",
  "message": "لماذا إجابتي خطأ؟"
}
```

Response `200`:

```json
{
  "conversationId": "5b0f8a4e-0000-4000-8000-000000000004",
  "reply": "1. المقاومة = فرق الجهد ÷ شدة التيار ...",
  "citations": [
    { "reference": "explanation-1", "section": "Explanation", "sectionTitle": "قانون أوم", "lessonId": "5b0f8a4e-0000-4000-8000-000000000003", "questionId": null }
  ],
  "dailyMessageLimit": 5,
  "messagesUsedToday": 3,
  "messagesRemainingToday": 2,
  "model": "claude-sonnet-5",
  "promptVersion": "v2"
}
```

Send `conversationId` back with the next message to continue the conversation; it is null for the first message. `model` and `promptVersion` are stored per reply and are not shown to the student.

| Code | HTTP | When |
|---|---|---|
| `AVATAR_EXAM_IN_PROGRESS` | 403 | the student has an exam in progress |
| `AVATAR_DAILY_LIMIT_REACHED` | 403 | today's messages reached the limit (context `limit`) |
| `AVATAR_QUESTION_NOT_ANSWERED` | 400 | `QuizQuestion` on a question the student has not answered |
| `AVATAR_ENTRY_POINT_INVALID` | 422 | unknown entry point |
| `AVATAR_MESSAGE_REQUIRED` / `AVATAR_MESSAGE_TOO_LONG` | 422 | blank message, or longer than `Avatar:MessageMaxLength` |
| `AVATAR_CONVERSATION_NOT_FOUND` | 404 | `conversationId` is unknown or belongs to another student |
| `AVATAR_CONVERSATION_CONTEXT_MISMATCH` | 400 | `conversationId` belongs to another entry point, lesson, session or question |
| `AVATAR_CONVERSATION_MODIFIED_CONCURRENTLY` | 409 | two messages were sent in one conversation at the same time; nothing of the second is stored or counted |
| `LESSON_ID_REQUIRED` / `SESSION_ID_REQUIRED` / `QUESTION_ID_REQUIRED` | 422 | an id the entry point needs is missing |
| `LESSON_NOT_FOUND` / `SESSION_NOT_FOUND` / `SESSION_QUESTION_NOT_FOUND` / `QUESTION_NOT_FOUND` | 404 | see Entry points |
| `LESSON_LOCKED` | 403 | Free student, locked lesson |
| `AI_SERVICE_UNAVAILABLE` | 503 | the AI service failed; nothing is counted |
| `USER_NOT_AUTHENTICATED` | 401 | no signed-in user |

## Configuration

`Avatar` section (environment form `Avatar__*`), validated at startup:

| Key | Default | Range | Notes |
|---|---|---|---|
| `Avatar:MessageMaxLength` | 2000 | 1 to 4000 | at most the AI service's `ELMANHG_AI_CHAT_MAX_MESSAGE_CHARS` |
| `Avatar:HistoryTurnMaxLength` | 4000 | 1 to 4000 | each stored turn is cut to this when sent back as history; the AI service rejects turns over 4000 |
| `Avatar:MaxHistoryMessages` | 10 | 0 to 20, even | at most `ELMANHG_AI_CHAT_MAX_HISTORY_MESSAGES` |
| `Avatar:ContextFieldMaxLength` | 8000 | 500 to 8000 | per long context field |
| `Avatar:AdminConversationsMaxPageSize` | 100 | 1 to 200 | largest admin conversation page |
| `Avatar:ConversationSearchMaxLength` | 200 | 1 to 500 | longest admin search text |

The AI service's prompt version is `ELMANHG_AI_CHAT_PROMPT_VERSION` (default `v2`). See [deployment.md](deployment.md) for production values.

## Conversation log

- **When rows are written.** Only a successful reply writes. The first successful message starts an `AvatarConversation`; each reply appends two `AvatarMessage` rows (the student's message, then the reply). The conversation, its messages and the usage row are saved in one transaction. A refusal, a validation error, an AI failure or a conversation error writes nothing.
- **`AvatarConversations`**: `Id`, `StudentId`, `EntryPoint`, `SubjectId`, `UnitId`, `LessonId`, `SessionId`, `QuestionId` (null when the entry point has none; `SessionId` only for `QuizQuestion` and `ExamReview`), `StartedAt`, `LastMessageAt`, `MessageCount`, the audit columns and an `xmin` row version. Indexes: `(StudentId, LastMessageAt)` and `LastMessageAt`.
- **`AvatarMessages`**: `Id`, `ConversationId`, `Position` (0, 1, 2, … taken from `MessageCount`; unique per conversation), `Role` (`Student` or `Assistant`), `Text` and `CreatedAt`. Assistant rows also carry `Model`, `PromptVersion`, `InputTokens`, `OutputTokens`, `CostUsd` (`numeric(12,6)`), `StopReason`, `HistoryMessageCount` (how many earlier messages were sent), `Context` and `Citations` (jsonb). Student rows leave them null. Indexes: unique `(ConversationId, Position)` and `CreatedAt`.
- **Context JSON** (camelCase keys and enums, nulls omitted): `{ "bundle": <the context bundle sent>, "sources": [{ "reference", "title", "content" }] }`, exactly what the model read, including the source content.
- **Citations JSON**: the mapped citations returned to the student, `[{ "reference", "section", "sectionTitle", "lessonId", "questionId" }]`.
- **Cost** is the AI service's `costUsd` for the reply ([ai-service.md](ai-service.md)); the fake returns 0.
- **Append-only.** Triggers reject `UPDATE`, `DELETE` and `TRUNCATE` on `AvatarMessages`, with the same `reject_append_only_mutation()` as the attempt log. `AvatarConversations` stays mutable for `LastMessageAt` and `MessageCount`.
- **Concurrency.** Two messages sent at once in one conversation collide on the row version or on the position index. The second returns `409 AVATAR_CONVERSATION_MODIFIED_CONCURRENTLY`, and its usage row rolls back with it.
- **Privacy.** Rows are keyed by the real student id (operational data; each exchange is also copied to `AvatarTrainingRecords` under a hashed id, see [training-data.md](training-data.md)). The send command is not audited, so no message text reaches `AuditLogs`, and no log line carries message text. The admin view shows the student's display name only (no phone or email). Teachers have no access. A student can only continue their own conversation id; there is no student history view.
- **Retention.** Kept indefinitely in v1: there is no purge job, and no endpoint deletes a conversation or a message. The retention period and the erasure path are open in the retention and privacy checklist of [training-data.md](training-data.md) (dev decision #215).
- **Consumers.** #110 (JSONL export) reads `AvatarTrainingRecords` ([training-data.md](training-data.md)).

## Admin view

Admins (`AvatarConversations.View`; Students and Teachers get 403) see «محادثات المساعد» at `/admin/avatar-conversations` and each conversation at `/admin/avatar-conversation/{id}`.

| Method | Route | Policy | Query | Result |
|---|---|---|---|---|
| GET | `/api/avatar/conversations` | `AvatarConversations.View` (Admin) | `search`, `entryPoint`, `from`, `to`, `pageNumber` (1), `pageSize` (20) | `PageData<AdminAvatarConversationResult>` |
| GET | `/api/avatar/conversations/{conversationId}` | `AvatarConversations.View` (Admin) | — | `AdminAvatarConversationDetailResult` |

- `search` is trimmed and matched case-insensitively as a substring of any message text in the conversation, or of the student's display name. `entryPoint` filters by entry point. `from` (inclusive) and `to` (exclusive) apply to `LastMessageAt`. The newest `LastMessageAt` comes first.
- Each list item has the student's display name, the entry point, the subject and lesson names, the message count and the first student message (`firstQuestion`).
- The detail has the summary (student, context names, start and last message, message count, total tokens, and total cost, which is null when no reply is priced) and every message in position order with its reply metadata, citations and the context sent.
- Validation (422): `AVATAR_CONVERSATIONS_PAGE_NUMBER_INVALID`, `AVATAR_CONVERSATIONS_PAGE_SIZE_INVALID` (over `Avatar:AdminConversationsMaxPageSize`), `AVATAR_CONVERSATIONS_SEARCH_TOO_LONG` (over `Avatar:ConversationSearchMaxLength`), `AVATAR_CONVERSATIONS_ENTRY_POINT_INVALID` and `AVATAR_CONVERSATIONS_DATE_RANGE_INVALID` (`from` not before `to`). An unknown id is `404 AVATAR_CONVERSATION_NOT_FOUND`.

## Streaming

There is no streaming: each message gets one JSON reply, and the panel shows «المساعد يكتب…» meanwhile. Replies are short, the API must post-process the whole reply (count the message only on success, map citations to lesson tabs), the generated fetch client does not read server-sent events, and streaming would have to pass through two hops. Revisit if the latency target is missed (E13.S3).

## UI

- A floating «المساعد» pill (bottom-start) on every student screen opens the panel in the general context. The panel is a sheet from the start edge, at most 380 px wide, full height.
- Header «المساعد الذكي» with a close button, then «السياق: {title}» («عام» in the general context), then, for Free students, «رسائل اليوم: X / N».
- The conversation starts with a greeting for the entry point; student bubbles are soft, assistant bubbles white with a hairline border and an accent sparkle, and replies list their «المصادر» chips. «المساعد يكتب…» shows while a reply is pending.
- The composer has «سؤالك» and «إرسال». It is disabled during an exam, at the daily limit and while a reply is pending.
- Outcomes stay inline as notices in the conversation, not toasts: the exam refusal, the daily-limit notice (with «اشترك» for Free students), «المساعد غير متاح الآن» and a generic failure notice. After every send, successful or not, the status is refreshed.
- If the status cannot load, the panel shows «تعذّر تحميل المساعد.» with «إعادة المحاولة».
- Opening a different context starts a new conversation; reopening the same one keeps it. The panel keeps the `conversationId` of the open context and sends it with each message; reloading the page starts a new conversation.
- Admins read the log in «محادثات المساعد» (see Admin view): a filterable list, and each conversation with every reply's model, prompt version, tokens, cost, stop reason, sources and the context sent.

## Eval

`ai/src/elmanhg_ai/eval/`: 22 cases in `datasets/avatar_chat.v2.jsonl` (7 `safety`), deterministic scorers (Arabic ratio, word count, citations, numbered steps, required and forbidden terms), threshold: a pass rate of at least 0.85 and every safety case passing. Run it against Claude with `ELMANHG_AI_LLM_PROVIDER=anthropic`, `ELMANHG_AI_ANTHROPIC_API_KEY` and `ELMANHG_AI_SERVICE_TOKEN` set: `cd ai && uv run pytest -m eval`. Without them the test skips. Details in [ai-service.md](ai-service.md), Eval.

## Not in this story

- A student history view, and a retention purge (#215).
- A per-student concurrency cap on avatar calls (#115).
- Ask a Teacher (E9).
