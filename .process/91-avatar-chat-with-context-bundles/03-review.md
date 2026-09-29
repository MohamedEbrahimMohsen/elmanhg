VERDICT: CHANGES_REQUESTED

# Review — [E8.S3] Avatar chat with context bundles (#91)

## Blocking

### 1. Three throw/gate paths in the context loader have no test; one of them is the D14 free-tier gate
**Where:** `api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageHandler.Context.cs:55-58` (QuizQuestion lesson-lock gate), `:49-52` (`QUESTION_NOT_FOUND` when the served revision or the question is missing), `:86-87` (`LESSON_NOT_FOUND` when the unit or subject is missing)
**Rule:** reviewer Review order #6 ("every throw in the new code needs a test"); plan D14; testing convention (a test must fail when the code is wrong)
**Problem:** The only `LESSON_LOCKED` tests (`SendAvatarMessageHandlerContextTests.Handle_FreeStudentLockedLesson_ThrowsLessonLocked`, `AvatarMessageEndpointTests.PostMessage_FreeStudentLockedLesson_Returns403LessonLocked`) use the `Lesson` entry point. Nothing exercises:
- the not-exam lock gate on the QuizQuestion path;
- the ExamReview path that must skip that gate;
- the missing-revision/question `QUESTION_NOT_FOUND` throw;
- the unit/subject-missing throws.

A search of `Application/Features/Avatar` and `Integration/Avatar` finds no `QuestionNotFound` assertion.
**Failure:** Delete lines 55-58, or invert the condition. A Free student can then use the assistant on a QuizQuestion from a locked lesson, and the whole suite stays green. A typical case is a quiz started while subscribed and continued after a lapse, which is exactly the case `SubmitAnswerHandler` re-checks. The same holds if the `revision is null || question is null` guard is removed: the handler then NREs into a 500, and no test notices.
**Fix:** Add handler tests to `SendAvatarMessageHandlerContextTests`:
- `Handle_QuizQuestionOnLockedLesson_ThrowsLessonLocked`: stub sibling positions so the lesson is locked; assert `LESSON_LOCKED` and no `ChatAsync`.
- `Handle_ExamReviewOnLockedLesson_Passes`: the same lock, and the request succeeds.
- `Handle_ServedRevisionMissing_ThrowsQuestionNotFound`.
- `Handle_UnitMissing_ThrowsLessonNotFound`, or the same for a missing subject.

## Non-blocking
- `SendAvatarMessageHandler.Context.cs:30` and `:63` (`IncludeQuestionExplanations: true`): retrieval is a second channel that can reveal an **unanswered** question’s explanation.
  - The correct-answer field itself is safe: it is sent only after an attempt, and a quiz without an attempt returns 400 (`:41-44`).
  - But `QuestionExplanation` chunks (stem plus explanation) of any servable question in the lesson can come back as sources.
  - Example: Q1 is answered and Q2 is unanswered in an open quiz. A QuizQuestion on Q1 (or a Lesson message) that pastes Q2’s stem can get Q2’s explanation cited.
  - This is plan D16, and PRD §9.2 allows the avatar to "explain freely … in quizzes", so it is not a divergence. It does undercut D12’s own rationale (sessions.md "What is revealed").
  - Worth a product confirmation. To close it, filter out question chunks whose `questionId` is an unanswered item of one of the student’s open quiz sessions.
- `AvatarGate.cs:33-41` and `SendAvatarMessageHandler.cs:34-46`: the soft limit lets **any number** of parallel requests through, not just two. N concurrent posts at 0/5 all reach the model. D7 accepts this and #115 tracks it, but `docs/avatar.md` "Daily quota" ("two requests sent together at 4 of 5 can both pass") understates it.
- `SendAvatarMessageValidator.cs:34-41` and `ai/src/elmanhg_ai/pipelines/chat.py:136-139`: history is client-supplied (D18), so a student can forge Assistant turns.
  - Delimiter tags are stripped, and the system prompt cannot be reached.
  - Prompt v2 rule 7 does not name history as data.
  - `docs/avatar.md` "Guardrails / Untrusted text" says fields are sent "only in the last user turn and as search results", which is not true for history turns.
  - #92 persistence can make history server-owned.
- `AvatarContextBundleFactory.cs:30`: an image-only stem becomes plain "". The AI service requires `stem` min_length 1 (`ai/src/elmanhg_ai/api/chat/schemas.py`, `QuestionContextIn`). It returns 422, which the API surfaces as 503 `AI_SERVICE_UNAVAILABLE`.
- `web/src/features/avatar/hooks/useAvatarChat.ts:16-19`: if the student opens another context while a reply is pending, the `replied` action appends into the new conversation, and its turns pair a question from the other context.
- `InProgressExamSpecification.cs:11`: an abandoned untimed exam blocks the avatar until it is submitted. This follows D1 and is documented; it is listed for product awareness only.
- The reviewer did not re-run `dotnet format --verify-no-changes`; it was not in the requested command set.

## Verified
- **Reviewer runs.** Every figure matches 02-implementation.md.
  - `dotnet test api/ -c Release`, with `appsettings.json` moved aside and restored afterwards: Passed, total 2817, failed 0.
  - ai, as `ai-ci.yml` runs it (`python -m uv`):
    - `uv sync --locked`: ok.
    - `ruff format --check`: 60 files formatted.
    - `ruff check`: pass.
    - `mypy src`: clean, 35 files.
    - `pytest -m "not eval"` with branch coverage: 135 passed, 1 deselected, 98%.
  - web:
    - `npm run typecheck`: ok.
    - `npm run lint`: ok.
    - `npm test -- --run`: 144 files, 868 tests passed.
    - `prettier --check --end-of-line auto`: clean.
- **Exam guard.**
  - `AvatarGate.EnsureNoExamInProgressAsync` runs at `SendAvatarMessageHandler.cs:32`, for every entry point, before entitlement, quota, context load, retrieval and `ChatAsync`.
  - `InProgressExamSpecification` is the negation of `Session.IsPastDeadline(now, grace)` (`Session.Exam.cs:25`) for unsubmitted non-quiz sessions. The status handler uses the same spec.
  - A race at exam start does not leak. The start commits before any exam question reaches the client, so a message written with knowledge of an exam question is refused. A request already in flight past the guard carries only content written before the exam.
  - ExamReview also requires `SubmittedAt` to be set (`Context.cs:38`).
  - Integration test 76: 403 and 0 usage rows.
- **Quota.**
  - Enforced on the server before the model call.
  - The usage row is written only after `ChatAsync` returns (`:45-46`). Tests 26 and 29 check `Received(1)` and `DidNotReceive`; integration test 77 checks that 5 rows stay 5.
  - It fails closed: a count or entitlement error throws before the model call.
  - The day is Cairo-local, and the persistence test crosses the day boundary.
- **Prompt injection.**
  - The system prompt comes from a file (`chat.py:134`).
  - The message, history, context and each source's title and content go through `strip_delimiters` (test 94).
  - Sources are separate `search_result` blocks (test 99).
- **Citations.**
  - The adapter reads only `search_result_location` citations.
  - The pipeline intersects them with the references it sent (`chat.py:148-149`), and `AvatarCitationMapper` intersects again with the API's own matches.
  - Links are built on the client from `section` and the server `lessonId`, so the model cannot inject a URL.
  - Tests 27, 63-65, 95 and 100.
- **Correct answer.**
  - QuizQuestion requires an attempt; otherwise 400 (unit and integration tests).
  - ExamReview requires a submitted exam.
  - All fields come from the served revision at `item.QuestionVersion`.
- **Secrets and roles.**
  - The Anthropic key is a `SecretStr`.
  - `chat.completed` logs counts only.
  - `HttpAiServiceClient` logs only the path and the status; the service token is never logged.
  - `Avatar.Chat` is Student-only (policy rows, test 88). A Teacher gets 403 (integration test 82), and the policy test refuses Admin.
- **Eval.**
  - 22 cases, 7 of them `safety` (checked by parsing the jsonl), matching the plan table.
  - The scorers are deterministic.
  - The threshold is a 0.85 pass rate plus all safety cases passing; test 120 proves that a single safety failure fails it.
  - `-m eval` skips with a reason when no key is set.
- **Contract fidelity.**
  - All *Files to create* exist: A1-A4, B1-B21, C1-C3, D1-D16, E1-E10, F1-F28 and G1.
  - Nothing extra was created beyond the generated migration, OpenAPI and Orval output.
  - The v2 turn prompt is byte-identical to v1.
  - The migration is CreateTable plus the index only.
- **Deviations.** Each listed deviation matches the code:
  - the TextField and SubmitButton props;
  - the merged session lambda;
  - % only after a tolerance;
  - the eval ValidationError skip;
  - the scroll mechanism;
  - the ghost close button;
  - axe run on the dialog;
  - the 5-question quiz seed.

  No hidden deviation was found.
- **Postman.** The `Avatar` folder has status, lesson-message and global-message requests: correct URLs and methods, bodies matching the contract, and bearer auth inherited as in `Subscriptions`.
- **Docs.** No divergence.
  - These agree with the code: PRD §9.2, §9.3, §15 and §16; `ai-service.md`, `content-retrieval.md`, `subscriptions.md`, `sessions.md`, `claude-design-prompt.md` §4, `prototype.md`, `deployment.md` and `README.md`.
  - The PRD §9.1 Global row ("asks the student to pick a lesson") is met by the prompt and the greeting.
  - Conversation storage and per-message model recording (PRD §9.2, §9.3) belong to #92: incompleteness, not a finding.
- **i18n.** The ar and en key sets are identical for the avatar, quiz and exam namespaces. `avatar.ask` and `avatar.soon` were removed.

## Test quality
- `InProgressExamSpecificationTests`: strong. It covers the boundaries at deadline +10 s and +31 s, and the other-student, quiz and submitted cases.
- `SendAvatarMessageHandlerTests` and `SendAvatarMessageHandlerContextTests`: strong. `StubSessions` compiles the handler's real predicate, so the exam guard and the session-ownership filters are really evaluated. Assertions on the captured `AiChatRequest` check the real mapping. The gaps are listed in Blocking #1.
- `GetAvatarStatusHandlerTests`: pins the counts, the limit and `examInProgress`.
- `SendAvatarMessageValidatorTests`: one failing case per rule except the child role-enum rule. That rule emits the same code as `Alternates`, which already catches the case, so this is acceptable.
- `AvatarAnswerTextTests`, `AvatarCitationMapperTests`, `AvatarSourceFactoryTests` and `AvatarContextBundleFactoryTests`: they use a real `RichTextExtractor` and assert exact strings.
- Integration `AvatarMessageEndpointTests` and `AvatarStatusEndpointTests`: real end-to-end paths (reindex, the fake cites the first source, the DB rows).
- ai: the pipeline, adapter and schema tests assert the request bodies that are sent and the filtering. The eval unit tests use a scripted fake with real scoring.
- web: `AvatarPanel.test.tsx` asserts the POST bodies, the history, the disabled states, the notices and the link hrefs. The reducer and schema tests pin behaviour.
- No test was found that only checks a value a substitute was told to return.
