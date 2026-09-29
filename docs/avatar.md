# AI Avatar

## Role

The AI Avatar is the in-app study assistant for students (PRD §9). The student asks a short question from a panel that opens on any student screen, and the API answers with a short Arabic reply grounded on the lesson material, listing the lesson sections it used. The .NET API owns everything the student can see or be refused: the entry point, the context bundle, retrieval, the exam refusal and the daily quota. The Python service ([ai-service.md](ai-service.md)) only turns a prepared request into a reply. Conversations live in the browser's memory; persisting them is #92.

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
- **History.** The browser keeps one conversation per opened context and sends the last `Avatar:MaxHistoryMessages` completed turns. The API checks the count, that turns alternate starting with the student and end with the assistant, the role and each turn's length.
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
- Storage: table `AvatarMessageUsages` (`Id`, `StudentId`, `EntryPoint`, `CreatedAt`, `IsDeleted`, `DeletedAt`) with an index on `(StudentId, CreatedAt)`. It stores no text, model or prompt; #92 owns conversations and messages.

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
  "history": [
    { "role": "User", "content": "ما هو قانون أوم؟" },
    { "role": "Assistant", "content": "قانون أوم يربط فرق الجهد بشدة التيار والمقاومة." }
  ],
  "message": "لماذا إجابتي خطأ؟"
}
```

Response `200`:

```json
{
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

`model` and `promptVersion` are returned for #92 and are not shown to the student.

| Code | HTTP | When |
|---|---|---|
| `AVATAR_EXAM_IN_PROGRESS` | 403 | the student has an exam in progress |
| `AVATAR_DAILY_LIMIT_REACHED` | 403 | today's messages reached the limit (context `limit`) |
| `AVATAR_QUESTION_NOT_ANSWERED` | 400 | `QuizQuestion` on a question the student has not answered |
| `AVATAR_ENTRY_POINT_INVALID` | 422 | unknown entry point |
| `AVATAR_MESSAGE_REQUIRED` / `AVATAR_MESSAGE_TOO_LONG` | 422 | blank message, or longer than `Avatar:MessageMaxLength` |
| `AVATAR_HISTORY_TOO_LONG` | 422 | more than `Avatar:MaxHistoryMessages` turns |
| `AVATAR_HISTORY_INVALID` | 422 | turns do not alternate from the student, a role is unknown, or a turn is blank or longer than `Avatar:HistoryTurnMaxLength` |
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
| `Avatar:HistoryTurnMaxLength` | 4000 | 1 to 4000 | per history turn |
| `Avatar:MaxHistoryMessages` | 10 | 0 to 20, even | at most `ELMANHG_AI_CHAT_MAX_HISTORY_MESSAGES` |
| `Avatar:ContextFieldMaxLength` | 8000 | 500 to 8000 | per long context field |

The AI service's prompt version is `ELMANHG_AI_CHAT_PROMPT_VERSION` (default `v2`). See [deployment.md](deployment.md) for production values.

## Streaming

There is no streaming: each message gets one JSON reply, and the panel shows «المساعد يكتب…» meanwhile. Replies are short, the API must post-process the whole reply (count the message only on success, map citations to lesson tabs), the generated fetch client does not read server-sent events, and streaming would have to pass through two hops. Revisit if the latency target is missed (E13.S3).

## UI

- A floating «المساعد» pill (bottom-start) on every student screen opens the panel in the general context. The panel is a sheet from the start edge, at most 380 px wide, full height.
- Header «المساعد الذكي» with a close button, then «السياق: {title}» («عام» in the general context), then, for Free students, «رسائل اليوم: X / N».
- The conversation starts with a greeting for the entry point; student bubbles are soft, assistant bubbles white with a hairline border and an accent sparkle, and replies list their «المصادر» chips. «المساعد يكتب…» shows while a reply is pending.
- The composer has «سؤالك» and «إرسال». It is disabled during an exam, at the daily limit and while a reply is pending.
- Outcomes stay inline as notices in the conversation, not toasts: the exam refusal, the daily-limit notice (with «اشترك» for Free students), «المساعد غير متاح الآن» and a generic failure notice. After every send, successful or not, the status is refreshed.
- If the status cannot load, the panel shows «تعذّر تحميل المساعد.» with «إعادة المحاولة».
- Opening a different context starts a new conversation; reopening the same one keeps it.

## Eval

`ai/src/elmanhg_ai/eval/`: 22 cases in `datasets/avatar_chat.v2.jsonl` (7 `safety`), deterministic scorers (Arabic ratio, word count, citations, numbered steps, required and forbidden terms), threshold: a pass rate of at least 0.85 and every safety case passing. Run it against Claude with `ELMANHG_AI_LLM_PROVIDER=anthropic`, `ELMANHG_AI_ANTHROPIC_API_KEY` and `ELMANHG_AI_SERVICE_TOKEN` set: `cd ai && uv run pytest -m eval`. Without them the test skips. Details in [ai-service.md](ai-service.md), Eval.

## Not in this story

- Persisting conversations and messages, and the admin conversation view (#92).
- A per-student concurrency cap on avatar calls (#115).
- Ask a Teacher (E9).
