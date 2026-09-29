VERDICT: APPROVED

# Review r2 — [E8.S3] Avatar chat with context bundles (#91)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Application/Avatar/Shared/AvatarWithheldQuestions.cs:14`: the `session.IsExam ||` branch is not tested.
  - No unit or integration test puts an unsubmitted exam into the withheld set. Deleting `session.IsExam ||` would keep the suite green.
  - It only matters for an exam that is past its deadline plus grace but not yet auto-submitted, because the in-progress guard refuses every other case. `AutoSubmitExam` runs every 60 s, so the exposure is narrow.
  - Worth adding later: `Handle_ExpiredUnsubmittedExam_WithholdsItsAnsweredItemChunk`.
- `SendAvatarMessageHandler.Context.cs:71`: the withheld set is loaded on every search, even when no match is a question chunk.
  - It is one query per message (a split query over sessions, items and attempts), not N+1. It also loads whole items and attempts for all of the student's unsubmitted sessions.
  - It could be skipped when `search.Matches` has no `QuestionId`, or projected to question ids.
- `docs/subscriptions.md:165` and `docs/sessions.md` (quiz quota, "two parallel answers"): the implementer already noted that this wording understates the soft limit. That text predates #91, so it is not a divergence caused by this change.
- The r1 non-blocking items (forged history turns, image-only stem 422, the reducer race in `useAvatarChat.ts:16-19`) were not in scope for the rework and are unchanged.

## Verified
- **Full suite.** The reviewer ran `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside, then restored it: Passed, total 2830, failed 0, skipped 0. This matches the r2 claim (2817 + 13).
- **r1 Blocking #1 (lock and throw tests).** All six tests exist in `SendAvatarMessageHandlerContextTests.cs:143-210`:
  - `Handle_QuizQuestionOnLockedLesson_ThrowsLessonLocked` asserts `LESSON_LOCKED` and `DidNotReceive` on `ChatAsync`. It fails if `Context.cs:56-59` is deleted, which matches the implementer's mutation result.
  - `Handle_ExamReviewOnLockedLesson_Passes` uses the same `LockLesson()` stub. It fails if the `!exam` guard is dropped, because the gate would then throw.
  - `Handle_ServedRevisionMissing_ThrowsQuestionNotFound` and `Handle_QuestionMissing_ThrowsQuestionNotFound` cover each half of `Context.cs:50`.
  - `Handle_UnitMissing_ThrowsLessonNotFound` and `Handle_SubjectMissing_ThrowsLessonNotFound` cover `Context.cs:94-95`.
- **Withheld filter, every entry point.** The only `SearchLessonContentQuery` call in `Application/Avatar` is inside `SearchAsync` (`Context.cs:68-73`).
  - Lesson (`:31`) and QuizQuestion/ExamReview (`:64`) both go through it. Global does no retrieval.
  - Sources (`SendAvatarMessageHandler.cs:40-42`) and citations (`:49`, `AvatarCitationMapper.Map(..., context.Matches, ...)`) are both built from the filtered `context.Matches`. A withheld chunk is never sent, and even a forged model reference to one cannot be mapped.
  - `matches.Count > 0` uses the filtered list, so the lesson text is sent when every match is withheld (unit test `Handle_AllMatchesWithheld_SendsLessonTextInstead`).
- **No false positives.** Only items of the student's own unsubmitted sessions are withheld: items without an attempt in an open quiz, and every item of an unsubmitted exam.
  - An answered quiz item stays citable (`Handle_QuizQuestionEntry_SendsOwnAnsweredChunkAndWithholdsOtherUnansweredItem`; integration `PostMessage_QuizQuestionAnswered_CitesOnlyTheAnsweredQuestionChunk` asserts a single `QuestionExplanation` citation for the answered question).
  - A submitted quiz releases everything (`Handle_SubmittedQuiz_SendsUnansweredQuestionChunk`).
  - Another student's open quiz is ignored (`Handle_OtherStudentsOpenQuiz_DoesNotWithhold`).
  - Non-question chunks (`QuestionId` null) always pass (`AvatarWithheldQuestions.cs:20`).
  - This matches sessions.md "What is revealed".
- **No N+1.** `LoadAsync` is one `FindAsync` with `Include` and `AsSplitQuery` per message. The filtering is in memory against a `HashSet`, and there is no per-match query.
- **Tests constrain the filter.** Each predicate term in `AvatarWithheldQuestions.cs:12`/`:14` has a test that fails if it is removed:
  - `StudentId`: the other-student test.
  - `SubmittedAt == null`: the submitted-quiz test.
  - `FindAttempt(...) is null`: the QuizQuestion test.
  - `Filter`: the Lesson-entry test, which asserts exact `Sources` equality.

  The harness `StubSessions.FindAsync` compiles the handler's real predicate (`AvatarTestData.cs:32-33`). The `IsExam` term has no test (non-blocking above).
- **Style.** The new file is in the house style: file-scoped namespace, `ConfigureAwait(false)` and `asNoTracking`. Its one comment states a hidden cross-document invariant, which skill §"No Comments" allows. The handler has no try/catch.
- **Docs accurate.**
  - The `docs/avatar.md` Context bundle "Withheld questions" bullet and the `docs/content-retrieval.md` §Role consumer paragraph describe the code exactly: the rule, filtering after search (fewer than `top`), and the QuizQuestion own-chunk case.
  - The `docs/avatar.md` "Daily quota" soft-limit wording now matches `AvatarGate.cs:33-41` and `SendAvatarMessageHandler.cs:34-46` (any number of parallel requests).
  - No divergence was found.
- **Deviations.** All three listed deviations match the code:
  - withholding across entry points and not only the literal own-question case;
  - filtering after search, with the #90 contract left unchanged;
  - two new files.

  No hidden deviation was found. Postman is unaffected (no API surface change in r2).
- **No regression.** The r1 figure of 2817 is now 2830, all green. `appsettings.json` was restored.

## Test quality
- `SendAvatarMessageHandlerContextTests`: strong. The new lock tests pair a positive case (ExamReview passes) with a negative one (QuizQuestion throws), so they pin the `!exam` branch in both directions.
- `SendAvatarMessageHandlerWithheldSourcesTests`: strong. It asserts exact source sequences and citation references against the real compiled predicate. The only gap is the exam branch (non-blocking).
- Integration `AvatarMessageEndpointTests`:
  - The withheld test asserts 0 citations, which on its own would also pass on an empty index.
  - The paired answered-question test proves that the index is populated and the path is real, so together they constrain the behaviour.
