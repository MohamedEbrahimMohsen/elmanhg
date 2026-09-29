# Implementation r2 — [E8.S3] Avatar chat with context bundles (#91)

Rework for `03-review.md` (CHANGES_REQUESTED): blocking #1, plus two orchestrator-promoted items (#2 explanation leak, #3 quota docs).

## Findings addressed
| # | What I changed | file:line |
|---|---|---|
| 1 | Added six handler tests. `Handle_QuizQuestionOnLockedLesson_ThrowsLessonLocked` checks the D14 gate and that `ChatAsync` is not called. `Handle_ExamReviewOnLockedLesson_Passes` checks that ExamReview skips the gate. `Handle_ServedRevisionMissing_ThrowsQuestionNotFound` and `Handle_QuestionMissing_ThrowsQuestionNotFound` cover both halves of the `revision is null \|\| question is null` guard. `Handle_UnitMissing_ThrowsLessonNotFound` and `Handle_SubjectMissing_ThrowsLessonNotFound` cover `LoadUnitAndSubjectAsync`. The lock stub moved into a private `LockLesson()` helper, which the existing Lesson lock test also uses now. **Mutation check:** I deleted the `if (!exam) { EnsureLessonOpenAsync }` block (now lines 56-59, one line lower than before because of a new `using`) and rebuilt. `Handle_QuizQuestionOnLockedLesson_ThrowsLessonLocked` failed (1 of 87 avatar tests). I then restored the file. | `api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerContextTests.cs:156-224`, `:233-237` |
| 2 | New `AvatarWithheldQuestions`. It loads the student's unsubmitted sessions, including their items and attempts. It withholds any question that is an unanswered item of an open quiz, or any item of an unsubmitted exam (the sessions.md "What is revealed" rule). A new private `SearchAsync` in the context partial runs the search and then drops the chunks of withheld questions. The Lesson and question entry points both use it, so dropped chunks are never sent as sources and can never become citations (`AvatarCitationMapper` maps only the sent matches). `matches.Count > 0` now counts the filtered list, so the lesson text is sent when every match was withheld. The QuizQuestion entry requires an answer, so the question's own chunk is always allowed there. | `api/Elmanhg.Application/Avatar/Shared/AvatarWithheldQuestions.cs:1-22`; `api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageHandler.Context.cs:4,18,26,31-32,64-73` |
| 2 | Unit tests (new file). Five cases: a Lesson entry withholds the unanswered item's chunk from sources and citations; QuizQuestion sends its own chunk but withholds the other unanswered item; a submitted quiz sends everything; another student's open quiz does not withhold; when every match is withheld, the lesson text is sent. The harness `StubSessions` now also stubs `FindAsync` with the compiled real predicate. | `api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerWithheldSourcesTests.cs:1-90`; `api/Elmanhg.Tests/Application/Features/Avatar/AvatarTestData.cs:32-33` |
| 2 | Integration tests. The lesson is indexed and has only question chunks, and a 5-question quiz is open. `PostMessage_LessonEntryWithUnansweredOpenQuiz_WithholdsItsQuestionChunks`: nothing is answered, so the response has 0 citations. The fake always cites the first source, so without the filter this test fails deterministically. `PostMessage_QuizQuestionAnswered_CitesOnlyTheAnsweredQuestionChunk`: the single citation is a `QuestionExplanation` for the answered question. **Mutation check:** I made `Filter` keep everything. 5 tests failed: 3 unit and both integration tests. I then restored the file. | `api/Elmanhg.Tests/Integration/Avatar/AvatarMessageEndpointTests.cs:81-104`, `:239-254` |
| 2 | Docs. `avatar.md` has a new "Withheld questions" bullet under Context bundle. `content-retrieval.md` §Consumer now says the avatar drops these chunks before sending sources. | `docs/avatar.md:28`; `docs/content-retrieval.md:8` |
| 3 | `avatar.md` "Daily quota" now says that any number of parallel requests can pass, and that the count can end up to N−1 above the limit (for example, 5 requests at 4 of 5 end at 9). | `docs/avatar.md:56` |

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Application/Avatar/Shared/AvatarWithheldQuestions.cs` | 22 | Load the question ids whose explanation is not yet revealed, and filter the matches |
| `api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerWithheldSourcesTests.cs` | 90 | Unit tests for finding 2 |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageHandler.Context.cs` | `SearchAsync` helper with the withheld filter; the Lesson loader now takes `studentId` |
| `api/Elmanhg.Tests/Application/Features/Avatar/AvatarTestData.cs` | `StubSessions` also stubs `FindAsync` |
| `api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerContextTests.cs` | 6 tests plus the `LockLesson()` helper |
| `api/Elmanhg.Tests/Integration/Avatar/AvatarMessageEndpointTests.cs` | 2 tests plus the `SeedIndexedQuizAsync` helper |
| `docs/avatar.md` | Withheld questions bullet; soft-quota wording |
| `docs/content-retrieval.md` | Consumer paragraph mentions the withheld filter |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Finding 2: "when the context is a quiz question the student has NOT answered yet, exclude that question's own chunk(s)" | A QuizQuestion on an unanswered question already returns 400 (`AVATAR_QUESTION_NOT_ANSWERED`), so that literal case never reaches retrieval. The real leak is another question's chunk, as in the review's example: Q2 is unanswered while the student asks about Q1 or the lesson. | I applied the rule to every withheld question for the student, whatever the entry point. The rule is the same as sessions.md "What is revealed": an unanswered item of an open quiz. I also included every item of an unsubmitted exam, the conservative reading, because exam explanations are revealed only on submission. An expired exam that has not been submitted is not "in progress" for the exam guard, so without this its items could leak. |
| The filter could be pushed into `SearchLessonContentQuery` | That query belongs to #90 and has its own contract, plus an admin endpoint | I filter after the search inside the avatar, so the retrieval contract is unchanged. The trade-off is that a message can get fewer than `top` sources. This is documented in avatar.md. |
| The rework touches only files listed in the review | Finding 2 needed a new production file and a new test file | I created `AvatarWithheldQuestions.cs` and `SendAvatarMessageHandlerWithheldSourcesTests.cs`. `Context.cs` would otherwise have gone over about 100 lines, and the context test file was already 170 lines. |

## Build & test
- `dotnet build api/ -c Release`: Build succeeded, 0 warnings, 0 errors.
- `dotnet format api/ --verify-no-changes --include <avatar app, unit and integration test folders>`: no output (clean).
- `dotnet test api/ -c Release`, run with `api/Elmanhg.Api/appsettings.json` moved aside and restored afterwards: `total: 2830, failed: 0, succeeded: 2830, skipped: 0`. The previous run had 2817; 13 tests were added.
- Avatar-only runs during the mutation checks (`--filter-namespace "*Avatar*"`):
  - lock gate deleted: 1 failed of 87;
  - filter disabled: 5 failed of 87;
  - both restored: 87 of 87 passed.
- ai and web: unchanged, not run.

## Notes for review
- `AvatarWithheldQuestions.LoadAsync` runs one extra query per message: the unsubmitted sessions with their items and attempts, as a split query. It runs even when no match is a question chunk. At most one open session exists per scope, so the set is small.
- The withheld set is not limited to the lesson being asked about. That is harmless, because only chunks of that lesson are searched.
- The one comment in `AvatarWithheldQuestions` points to the sessions.md invariant, because the exam-versus-quiz rule is not obvious from the code.
- `docs/subscriptions.md:165` and `docs/sessions.md:63` describe the **quiz** quota as "two parallel answers can both pass". That understates it in the same way, but it is outside #91 and I left it unchanged.

## Main merge

Story work committed as `d2f75dd` (`feat(E8.S3): avatar chat with context bundles`), then `origin/main` (#94 teacher threads, #95 teacher inbox) merged in.

| Conflict | Resolution |
|---|---|
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Migration list holds all 28 in timestamp order: ... `_AddLessonContentIndex`, `_AddTeacherThreads`, `_AddAvatarMessageUsages`, `_AddTeacherThreadClaims`. The model snapshot auto-merged; the pending-model-changes check passes. |
| `deploy/api.env.example`, `docs/deployment.md` | Both sections kept: AI Avatar, then Ask a Teacher. |
| `docs/claude-design-prompt.md` | Main's Ask a Teacher lines kept; lesson line says «اسأل المساعد عن الدرس» opens the assistant, quiz feedback lists both «اسأل المساعد» and «اسأل معلّم». |
| `docs/subscriptions.md` | "For later stories": #91 (done) and #94 (done) both recorded. |
| `postman/elmanhg.postman_collection.json` | Main's collection plus the `Avatar` folder (before `AskTeacher`). |
| `web/src/app/i18n.ts` | Both `avatar` and `askTeacher` namespaces. |
| `web/src/features/browse/pages/LessonPage.tsx` | `AskAvatarButton` (lesson context) followed by `AskTeacherLink`. |
| `web/src/features/quiz/components/FeedbackPanel.tsx`, `QuizQuestionCard.tsx` | Story interaction: main added a `children` slot next to the old quiz-local `AskAvatarButton` (deleted by this story). `FeedbackPanel` now takes both `ask` (avatar context) and `children`, rendering `<AskAvatarButton context={ask} />` and `AskTeacherLink attemptId` in main's flex row. |

Generated files (`api/openapi/v1.json`, Orval output) regenerated with `dotnet build` and `npm --prefix web run gen:api`; both matched the merged content, no diff.

### Verification (after the merge)
- `dotnet build` (api/): 0 errors.
- `dotnet test api/ -c Release` (appsettings.json moved aside, restored): 3019 passed, 0 failed.
- web `typecheck`: clean. `lint`: clean. `test -- --run`: 159 files, 936 tests passed.
- `prettier --check . --end-of-line auto`: clean (after formatting `FeedbackPanel.tsx`).
- ai (`python -m uv`): `sync --locked` ok, `ruff format --check` ok, `ruff check` ok, `mypy src` ok, `pytest -m "not eval"` 135 passed.
