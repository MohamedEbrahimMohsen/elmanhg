VERDICT: APPROVED

# Review — Servable rule (#67, E3.S4), round 1

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Api/Controllers/Questions/QuestionsController.cs:35`: the anonymous `servable-count` has no `[EnableRateLimiting]`. PRD §Security only asks for rate limits on auth and Avatar, and the value comes from `IMemoryCache`, so this does not block. On a cold cache (after each invalidation), concurrent anonymous requests each run one `COUNT` (`GetServableQuestionCountHandler.cs:14-20`, no single-flight). A partitioned fixed-window policy would cheaply harden the only public data read if the orchestrator wants it.
- `prototype/app.js:794,1083`: the prototype admin bank toggles retire and un-retire ("إعادة تفعيل"). The new PRD §5.3 rule says "Retirement is final", and so do plan Decision 7 and `docs/question-schemas.md` § Retirement. The prototype is not in the docs-sync ownership map, and the retire UI is out of scope, so this is not a divergence finding. The follow-up retire-button story should drop the un-retire, or put the rule to the dev.
- `api/Elmanhg.Application/Questions/GetQuestions/GetQuestionsHandler.cs:44`: the `bool` from `TryGetValue` is discarded and the code relies on `out var lesson` being null. It is correct, but `lessonsById.GetValueOrDefault(question.LessonId)` would read better.
- `api/Elmanhg.Domain/Questions/Question.Editing.cs:48`: `Resubmit` guards, then `Update` (line 11) guards again. Harmless, and the order `QUESTION_RETIRED` before `QUESTION_NOT_REJECTED` holds.
- `api/Elmanhg.Domain/Questions/Question.cs:33`: `IsRetired` is used only by tests. EF does not map it (snapshot and `has-pending-model-changes` are clean).
- No integration test covers invalidation through `QuestionRejected`. There is no reject endpoint yet (#68), and the unit theory covers the handler. #68 should add the delta test.

## Verified
- **Single source.** `ServableQuestionSpecification.cs` holds the only `Approved && RetiredAt == null` / `State == Published` expressions. `IsSatisfiedBy` uses `.Compile()` of the same two fields, and `WhereServable` composes them as `IN (subquery)`. `git grep` over Application/Infrastructure finds no other Approved/Published/RetiredAt logic. `QuestionRepository.CountServableAsync`/`CountServableByLessonAsync` and `GetQuestionsHandler` all call the spec. EF translation is proven against real Postgres by `ServableQuestionSpecificationPersistenceTests` (an untranslatable expression would throw, not client-evaluate) and by the endpoint tests through `CountServableAsync`.
- **Invalidation completeness.** I walked every state mutator in Domain:
  - `Lesson.Publish`/`Unpublish`/`Archive` raise events. Archived→Draft raises nothing and cannot change the count.
  - `Lesson.Delete` needs non-Published with no questions. `CurriculumUnit.Delete` needs no lessons. `Subject.Delete` needs no units. None of these can change the count.
  - `Question.Create`/`CreateImported` (bulk import) create Pending.
  - `Update` raises `QuestionReturnedToPending` only on an Approved→Pending content edit. `LessonId` never changes.
  - `Resubmit` is Rejected→Pending.
  - `Approve`/`Reject`/`Retire` raise events.
  - The handler covers exactly the 7 events and removes only the one key.
  - Pre-commit publish staleness is bounded by the 60 s TTL and documented (Decision 9, question-schemas § Servable).
- **Anonymous endpoint.** `[AllowAnonymous]` returns `ServableQuestionCountResult(int Count)` only, with no per-user data. `EndpointAuthorizationTests` accepts it. `Get_Anonymous_Returns200WithCount` proves a 200 without a token.
- **Retirement guard.** Every public mutator on `Question` (`Update`, `Resubmit`, `Approve`, `Reject` via `EnsureValidatorCanDecide`, `Retire`) checks `RetiredAt` first. The setters are private, so no other path exists.
- All files in *Files to create* exist, and all signatures match. The migration is `AddColumn`/`DropColumn` of nullable `RetiredAt` `timestamptz` only. Error codes and resx (ar and en) match. `ContentOptions.ServableCountCacheSeconds` is `[Range(1,3600)]`, default 60, and `ValidateOnStart` applies. It is present in `appsettings.example.json` and `ApiFactory`. CPM pins `Microsoft.Extensions.Caching.Memory` 10.0.5 with no version in the csproj.
- Both deviations are real and fine. `NewQuestionPage.test.tsx` needed `retiredAt`, and the private helper rename is cosmetic.
- Postman: "Get servable question count" (`noauth`, GET) and "Retire question" (POST, inherits bearer) sit after "Resubmit question", and no later request uses `{{questionId}}`.
- Docs: PRD §5.3 retire bullet, `docs/question-schemas.md` (guard order, § Retirement, § Servable) and `docs/audit-log.md` row all agree with the code. No divergence.
- The skill §1/§8/§9 checks pass: file-scoped namespaces, sealed types, `ConfigureAwait(false)` outside controllers, `DateTimeOffset`, no try/catch, one `SaveChangesAsync` in the handler, `UserId` null-check, `IAuditableCommand`, `ValidateOnStart`, and the guard grep is clean.
- I re-ran every check myself:
  - `dotnet build -c Release`: 0 errors, vendored-core warnings only.
  - OpenAPI: `--no-incremental` rebuild leaves `v1.json` hash unchanged (no drift).
  - `dotnet test api/ -c Release`: **938/938 passed**.
  - `dotnet list package --vulnerable --include-transitive`: clean.
  - `has-pending-model-changes`: none.
  - `dotnet format --verify-no-changes`: clean outside `core-libraries`.
  - web: `gen:api` with no drift (same 9 modified + 1 new, same diffstat), `typecheck`, `lint`, `format:check`, **344/344 tests (60 files)**, and `build` are all green.

## Test quality
- `ServableQuestionSpecificationTests`: constraining. Each of the three conjuncts plus the lesson-id match has a dedicated false case. The `WhereServable` LINQ test pins the composed query shape.
- `QuestionRetirementTests`: constraining. Each guard asserts the code and that state is unchanged (stem, version, status, reason).
- `QuestionServabilityEventsTests`: constraining. `Equal(...)` on the exact event list catches both missing and extra events, and the negative cases pin "no event".
- `RetireQuestionHandlerTests`: constraining. It asserts state on the real entity plus `Received(1)`/`DidNotReceive()` on save.
- `RetireQuestionValidatorTests`: adequate for the single rule.
- `GetServableQuestionCountHandlerTests`: constraining. It uses a real `MemoryCache`, the stub returns 42 then 43, and `Received(1)`/`Received(2)` is the caching behaviour itself, not a vacuous mock echo.
- `ServableQuestionCountInvalidationHandlerTests`: constraining across all 7 overloads, and it proves the removal is key-scoped.
- `ServableQuestionCountEndpointTests`: constraining. The baseline is read after seeding, which warms the cache, so a missing invalidation leaves the cached baseline and fails the ±1 delta. The class is serialised through `DisableParallelization`.
- `QuestionRetireEndpointTests`: constraining. It checks HTTP status plus `code` plus DB state through a fresh scope, the audit row, and 401/403 with DB unchanged.
- `ServableQuestionSpecificationPersistenceTests`: constraining. It uses real Postgres, a mixed seed and exact-set equality.
- `GetQuestionsHandlerTests` / `GetQuestionHandlerTests` / `GetLessonsHandlerTests` additions: they constrain the annotation wiring (`IsServable`, `RetiredAt`, `ServableQuestionCount` 2/0).
