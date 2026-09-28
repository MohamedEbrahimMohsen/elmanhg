# Plan — Servable rule (#67, E3.S4)

## Goal
The platform can answer "may this question be shown to a student right now?" from one domain definition: Approved AND lesson Published AND not retired (PRD §5.3, §17 rule 1), derived and never stored. An Admin can retire a question, which takes it out of serving for good while keeping its history. Anyone, including anonymous visitors to the landing page, can read the live platform-wide servable total (the marketed "100,000 questions" counter) from a cached endpoint. The cache is invalidated on every event that can change the total. The admin question bank still lists every question in every status, and now shows each question's `retiredAt` and `isServable`. The admin lesson list shows a servable count per lesson.

## Scope
**In:**
- `ServableQuestionSpecification` in `Elmanhg.Domain/Questions`. It is the only place the rule is defined, with an EF-translatable `IQueryable` path and an in-memory path.
- `Question.RetiredAt`, `Question.Retire`. Retirement is terminal: edit, resubmit, approve and reject on a retired question are refused.
- Question domain events: `QuestionApproved`, `QuestionRejected`, `QuestionReturnedToPending`, `QuestionRetired`.
- `POST /api/questions/{questionId}/retire` (Admin, audited as `Question.Retire`).
- `GET /api/questions/servable-count` (anonymous, cached in `IMemoryCache`).
- One MediatR notification handler removes the cache entry on: `LessonPublished`, `LessonUnpublished`, `LessonArchived`, `QuestionApproved`, `QuestionRejected`, `QuestionReturnedToPending`, `QuestionRetired`.
- Admin reads annotated through the spec: `QuestionListItemResult.RetiredAt` and `.IsServable`, `QuestionDetailResult.RetiredAt`, `LessonResult.ServableQuestionCount`.
- Migration `AddQuestionRetiredAt`, resx strings, `appsettings.example.json`, `ApiFactory`, OpenAPI plus Orval regeneration, web fixture updates for the new required fields, Postman, and docs (PRD §5.3, `docs/question-schemas.md`, `docs/audit-log.md`).

**Out:**
- Web UI for retiring (a button), a retired badge, and a servable column or lesson count in the admin screens. The story's sub-tasks are backend only, and the prototype shows them as later admin polish. The orchestrator may file a follow-up. This story only regenerates the client and updates fixtures.
- A "retired" filter on `GET /api/questions`.
- Un-retire.
- Student-serving queries (quiz #E5, blueprints #E6). They do not exist yet. When they are built they MUST go through `ServableQuestionSpecification.WhereServable` (recorded in `docs/question-schemas.md`).
- Mastery recalculation on retire or unpublish (backlog E5 "Recalculation job").
- Teacher approve/reject commands (#68). The domain events ship here, so #68 gets invalidation for free.

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Which existing queries does "used by every question query" cover? | The repo has no student-facing or serving question query today. The real question reads are admin-only: `GetQuestions`, `GetQuestion` (`ContentManage`) and `GetLessons.QuestionCount` (`ContentManage`). These keep returning every status and are *annotated* through the spec (`IsServable`, `RetiredAt`, `ServableQuestionCount`), never filtered. The spec *filters* only serving reads: the new servable count now, and quiz/exam/blueprint queries later (documented contract). | Orchestrator: the admin bank must see every status. The spec is still used by every question query in the repo, and none of them re-implements the rule. |
| 2 | How is the rule single-sourced when it spans two aggregates (Question, Lesson)? | `ServableQuestionSpecification` holds two `Expression`s (`QuestionCondition`, `LessonCondition`). `WhereServable(IQueryable<Question>, IQueryable<Lesson>)` composes them into SQL (`IN (SELECT Id FROM Lessons WHERE State='Published')`). `IsSatisfiedBy(Question, Lesson)` runs the *compiled same expressions* in memory. No navigation property is added to `Question`. | Question references Lesson only by id (existing mapping `HasOne<Lesson>().WithMany()`). Adding a navigation would change the aggregate shape. |
| 3 | What is "not retired", and is it stored? | Nullable `RetiredAt` (`timestamptz`) on `Question`, matching the PRD §15 data model `retired_at?`. Servable itself is never stored. No `RetiredBy` column: `UpdatedBy` plus the audit row record who retired it. | PRD data model. |
| 4 | Who may retire? | Admin only (`DefaultCodes.ContentManage`). | PRD §16: "Create / edit content" is Admin. The prototype puts retire in the admin bank. |
| 5 | Which statuses can be retired? | Any (Pending, Approved, Rejected). The status and version are unchanged. | PRD §5.3: "Retiring a question removes it from future quizzes but preserves all historical attempts". There is no status precondition. |
| 6 | Retiring an already-retired question | Throws `QUESTION_ALREADY_RETIRED` (400). | Mirrors `LessonAlreadyArchived` / `LessonAlreadyPublished`. |
| 7 | Can a retired question be edited or validated? | No. `Update`, `Resubmit`, `Approve` and `Reject` first call `EnsureNotRetired()`, which throws `QUESTION_RETIRED` (400). Retirement is terminal and there is no un-retire. | Otherwise a retired question could sit in the teacher's Pending queue and bump versions invisibly. This is a product-rule addition, so PRD §5.3 is updated (docs-sync). |
| 8 | Cache technology | `IMemoryCache` (`Microsoft.Extensions.Caching.Memory` 10.0.5, already resolved transitively through EF Core 10.0.5, now referenced explicitly). Registered with `services.AddMemoryCache()` in `AddApplication`. | `Core.Cache` is an empty placeholder in both Elmanhg and Morabh (`Core/Core.Cache/Class1.cs`). The single API instance and one integer do not justify a distributed cache. No new package id. |
| 9 | Cache key and lifetime | Key `ServableQuestionCountCache.Key = "questions:servable-count"`. Absolute expiration is `ContentOptions.ServableCountCacheSeconds` (default **60** in code, `[Range(1, 3600)]`). | The TTL is a backstop, not the main mechanism. `CoreDbContext.SaveChangesAsync` publishes domain events *before* `base.SaveChangesAsync`, so a read landing between invalidation and commit can re-cache the old value. The TTL bounds that staleness to 60 s. The vendored `CoreDbContext` is not changed ("mirror, don't modernize"). |
| 10 | Invalidation triggers | Removing the key on: `LessonPublished`, `LessonUnpublished`, `LessonArchived` (existing), `QuestionApproved`, `QuestionRejected`, `QuestionRetired`, and `QuestionReturnedToPending` (raised only when a content edit moves Approved → Pending). Not on create, import, metadata-only edit, or resubmit (Rejected → Pending). | These are exactly the transitions that can change the count, plus reject, which the orchestrator named explicitly (a validation event; cheap). |
| 11 | Where does the invalidator live? | `Elmanhg.Application/Events/ServableQuestionCountInvalidation/ServableQuestionCountInvalidationHandler.cs`, one class implementing 7 `INotificationHandler<T>`. | Mirrors Morabh `Morabh.Application/Events/OrderWorkflowNotificationEvent/OrderWorkflowNotificationEventHandler.cs` (folder `Application/Events/<Name>/`, `INotificationHandler<TDomainEvent>`). |
| 12 | Endpoint access for the count | `[AllowAnonymous]` on `GET /api/questions/servable-count`. | The landing page (`docs/claude-design-prompt.md` §4: "`#/` … live servable count") is public, and the student Home and admin dashboard read it too. It is a public catalogue figure, cached, with no per-user data. `EndpointAuthorizationTests` accepts `IAllowAnonymous`. |
| 13 | Count type | `int`. | `CountAsync` returns `int`. 100,000 is far from the limit. |
| 14 | Route verb for retire | `POST /api/questions/{questionId:guid}/retire`, returning bare `Ok()`. | Mirrors `POST /api/lessons/{id}/archive` (a state transition with no body). |
| 15 | Integration tests on a global count | Delta assertions in a test collection with `DisableParallelization = true` (`ServableCountCollection`). Every test seeds its data first, reads a baseline (which warms the cache), performs one API action, and reads again. | The count is platform-wide and `ApiFactory` is an assembly fixture, so parallel classes would race. Warming the cache *after* seeding makes a missing invalidation fail the test. |
| 16 | Spec SQL translation proof | Integration test through a real `AppDbContext` scope, filtered to the test's own lesson ids. | Unit tests over LINQ-to-objects cannot prove that EF translates the composed expression. |
| 17 | Migration order test | Add `eleventh => …"_AddQuestionRetiredAt"` to `AppDbContextTests.Migrate_FreshDatabase_LeavesNoPendingMigrations`. | Accepted pattern (PROGRESS.md). |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/Question.cs` | Add `public DateTimeOffset? RetiredAt { get; private set; }` after `ImportBatchId`. Add `public bool IsRetired => RetiredAt is not null;` after `CurrentContent`. |
| `api/Elmanhg.Domain/Questions/Question.Approval.cs` | `Approve`: after the stamps, `RaiseDomainEvent(new QuestionApproved(Id, LessonId));`. `Reject`: after the stamps, `RaiseDomainEvent(new QuestionRejected(Id, LessonId));`. `EnsureValidatorCanDecide`: first statement `EnsureNotRetired();`. |
| `api/Elmanhg.Domain/Questions/Question.Editing.cs` | `Update`: first statement `EnsureNotRetired();`. Inside `if (ValidationStatus == QuestionValidationStatus.Approved)`, after clearing `ValidatedAt`, add `RaiseDomainEvent(new QuestionReturnedToPending(Id, LessonId));`. `Resubmit`: first statement `EnsureNotRetired();` (before the Rejected check). |
| `api/Elmanhg.Domain/Questions/IQuestionRepository.cs` | Add `Task<int> CountServableAsync(CancellationToken cancellationToken);` and `Task<Dictionary<Guid, int>> CountServableByLessonAsync(IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken);`. |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// QUESTIONS` add `QuestionAlreadyRetired = "QUESTION_ALREADY_RETIRED"` and `QuestionRetired = "QUESTION_RETIRED"`. |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | Implement the two methods (see Files to create, row I1 contract). |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add`. |
| `api/Elmanhg.Application/Shared/Options/ContentOptions.cs` | Append `[Range(1, 3600)] public int ServableCountCacheSeconds { get; set; } = 60;`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | Add `services.AddMemoryCache();` before the options registrations (`using Microsoft.Extensions.DependencyInjection;` already present). |
| `api/Elmanhg.Application/Elmanhg.Application.csproj` | Add `<PackageReference Include="Microsoft.Extensions.Caching.Memory" />`. |
| `api/Directory.Packages.props` | Add `<PackageVersion Include="Microsoft.Extensions.Caching.Memory" Version="10.0.5" />` in the first ItemGroup. |
| `api/Elmanhg.Application/Questions/Shared/QuestionListItemResult.cs` | Append `DateTimeOffset? RetiredAt, bool IsServable` after `DateTimeOffset UpdatedAt`. |
| `api/Elmanhg.Application/Questions/Shared/QuestionDetailResult.cs` | Append `DateTimeOffset? RetiredAt` after `string? RejectionReason`. |
| `api/Elmanhg.Application/Questions/Shared/QuestionResultGenerator.cs` | `GenerateDetail` passes `question.RetiredAt`. `GenerateListItem(Question question, string lessonName, string? teacherName, bool isServable)` passes `question.RetiredAt, isServable`. |
| `api/Elmanhg.Application/Questions/GetQuestions/GetQuestionsHandler.cs` | Replace `lessonNames` with `var lessonsById = lessons.ToDictionary(x => x.Id);`. Per item: `lessonsById.TryGetValue(x.LessonId, out var lesson)`, lesson name `lesson?.Name ?? string.Empty`, `isServable = lesson is not null && ServableQuestionSpecification.IsSatisfiedBy(x, lesson)`. The filter is unchanged, so every status is still listed. Extract a private static `Generate(Question, Dictionary<Guid, Lesson>, Dictionary<Guid, string>)` if the lambda passes ~1 line. |
| `api/Elmanhg.Application/Lessons/Shared/LessonResult.cs` | Append `int ServableQuestionCount`. |
| `api/Elmanhg.Application/Lessons/Shared/LessonResultGenerator.cs` | `Generate(Lesson lesson, int questionCount, int servableQuestionCount)`. |
| `api/Elmanhg.Application/Lessons/GetLessons/GetLessonsHandler.cs` | After `questionCounts`: `var servableCounts = await questionRepository.CountServableByLessonAsync(lessonIds, cancellationToken).ConfigureAwait(false);` (hoist `lessonIds` to a local). Map with `servableCounts.GetValueOrDefault(x.Id)`. |
| `api/Elmanhg.Api/Controllers/Questions/QuestionsController.cs` | Two actions (see API surface). Usings: `Elmanhg.Application.Questions.RetireQuestion`, `Elmanhg.Application.Questions.GetServableQuestionCount`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Two keys each (see Error codes), placed after `QUESTION_REJECTION_REASON_REQUIRED`. |
| `api/Elmanhg.Api/appsettings.example.json` | Add `"ServableCountCacheSeconds": 60` to the `Content` object (end). |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["Content:ServableCountCacheSeconds"] = "60",` after `Content:QuestionImportMaxFileSizeInMb`. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `eleventh => eleventh.Should().EndWith("_AddQuestionRetiredAt")`. |
| `api/Elmanhg.Tests/Builders/QuestionBuilder.cs` | Add `private bool _retired;` and `public QuestionBuilder Retired() { _retired = true; return this; }`. In `Build()`, after the approve/reject blocks: `if (_retired) { question.Retire(Guid.NewGuid()); }`. |
| `api/Elmanhg.Tests/Integration/Content/QuestionTestData.cs` | Add `public static async Task<Guid> SeedRetiredQuestionAsync(ApiFactory factory, Guid lessonId, CancellationToken cancellationToken)`, the same body as `SeedQuestionAsync(approved: true)` plus `question.Retire(creator);` before `Add`. |
| `api/Elmanhg.Tests/Application/Features/Lessons/GetLessons/GetLessonsHandlerTests.cs` | Stub `CountServableByLessonAsync` (returns `{ [first.Id] = 2 }`). Expected results gain the 7th arg (`2`, `0`). Intended behaviour change. |
| `api/Elmanhg.Tests/Application/Features/Questions/GetQuestions/GetQuestionsHandlerTests.cs` | Add the rows listed in the Test plan. |
| `api/Elmanhg.Tests/Application/Features/Questions/GetQuestion/GetQuestionHandlerTests.cs` | Add the row listed in the Test plan. |
| `postman/elmanhg.postman_collection.json` | In the `Questions` folder, after "Resubmit question": add "Get servable question count" (`GET {{baseUrl}}/api/questions/servable-count`, no auth header), then "Retire question" (`POST {{baseUrl}}/api/questions/{{questionId}}/retire`, admin bearer like its siblings). |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api` (models, hooks, msw, zod). |
| `web/src/features/content/components/LessonItem.test.tsx` | Add `servableQuestionCount: 0` to each `LessonResult` literal. |
| `web/src/features/content/components/UnitLessons.test.tsx` | Same. |
| `web/src/features/questions/pages/QuestionListPage.test.tsx` | In the `item()` default object add `retiredAt: null, isServable: false`. |
| `web/src/features/questions/pages/QuestionEditorPage.test.tsx` | In the `question()` default object add `retiredAt: null`. |
| `web/src/features/questions/api/questionValues.test.ts` | In the `detail()` default object add `retiredAt: null`. |
| `docs/PRD.md` §5.3 Rules | Replace the retire bullet with: "Retiring a question (Admin only) removes it from future quizzes but preserves all historical attempts. Retirement is final: a retired question cannot be edited, resubmitted, approved or rejected." |
| `docs/question-schemas.md` | In "Validation status", the approve/reject order becomes `QUESTION_RETIRED`, then `QUESTION_NOT_PENDING`, then `QUESTION_VALIDATOR_NOT_ASSIGNED`. Add sections `## Retirement` (route, any status, terminal, `QUESTION_ALREADY_RETIRED` / `QUESTION_RETIRED`, `retiredAt` on list/detail) and `## Servable` (rule; `ServableQuestionSpecification` is the only definition; serving queries must use `WhereServable`; admin reads annotate and never filter; `GET /api/questions/servable-count` is anonymous; cache key, the 7 invalidation events, 60 s TTL backstop and why). |
| `docs/audit-log.md` | Table row after ResubmitQuestion: `| RetireQuestion | \`Question.Retire\` | Question | command (the diff shows \`retiredAt\`) |`. |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| D1 | `api/Elmanhg.Domain/Questions/ServableQuestionSpecification.cs` | static class, new (no Morabh equivalent) | `namespace Elmanhg.Domain.Questions;` `public static class ServableQuestionSpecification`. Members in this order: `public static readonly Expression<Func<Question, bool>> QuestionCondition = x => x.ValidationStatus == QuestionValidationStatus.Approved && x.RetiredAt == null;` · `public static readonly Expression<Func<Lesson, bool>> LessonCondition = x => x.State == LessonState.Published;` · `private static readonly Func<Question, bool> IsQuestionServable = QuestionCondition.Compile();` · `private static readonly Func<Lesson, bool> IsLessonServing = LessonCondition.Compile();` · `public static bool IsSatisfiedBy(Question question, Lesson lesson)` returns `question.LessonId == lesson.Id && IsQuestionServable(question) && IsLessonServing(lesson)` · `public static IQueryable<Question> WhereServable(this IQueryable<Question> questions, IQueryable<Lesson> lessons)`: `var servingLessonIds = lessons.Where(LessonCondition).Select(x => x.Id); return questions.Where(QuestionCondition).Where(x => servingLessonIds.Contains(x.LessonId));`. One comment above the class is allowed: `// PRD §17 rule 1: the single definition of servable; derived, never stored.` |
| D2 | `api/Elmanhg.Domain/Questions/Question.Retirement.cs` | partial class | `public partial class Question` with `public void Retire(Guid retiredBy)` and `private void EnsureNotRetired()`. Bodies in Domain behaviour. |
| D3 | `api/Elmanhg.Domain/Questions/QuestionApproved.cs` | domain event | `public sealed record QuestionApproved(Guid QuestionId, Guid LessonId) : DomainEvent;` (`using Core.DDD.Entities;`), mirroring `LessonPublished.cs`. |
| D4 | `api/Elmanhg.Domain/Questions/QuestionRejected.cs` | domain event | `public sealed record QuestionRejected(Guid QuestionId, Guid LessonId) : DomainEvent;` |
| D5 | `api/Elmanhg.Domain/Questions/QuestionRetired.cs` | domain event | `public sealed record QuestionRetired(Guid QuestionId, Guid LessonId) : DomainEvent;` |
| D6 | `api/Elmanhg.Domain/Questions/QuestionReturnedToPending.cs` | domain event | `public sealed record QuestionReturnedToPending(Guid QuestionId, Guid LessonId) : DomainEvent;` |
| A1 | `api/Elmanhg.Application/Questions/RetireQuestion/RetireQuestionCommand.cs` | command | `public sealed record RetireQuestionCommand(Guid QuestionId) : IRequest, IAuditableCommand { AuditAction => "Question.Retire"; AuditResourceType => "Question"; AuditResourceId => QuestionId; }` (mirror `ArchiveLessonCommand`). |
| A2 | `api/Elmanhg.Application/Questions/RetireQuestion/RetireQuestionValidator.cs` | validator | `public sealed class RetireQuestionValidator : AbstractValidator<RetireQuestionCommand>` with rule `RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);` (`Core.Validation.Extensions`, `Elmanhg.Application.Exceptions.ErrorCodes`). |
| A3 | `api/Elmanhg.Application/Questions/RetireQuestion/RetireQuestionHandler.cs` | handler | `public sealed class RetireQuestionHandler(IQuestionRepository questionRepository, ICurrentUserService currentUserService) : IRequestHandler<RetireQuestionCommand>`. `Handle`: (1) if `currentUserService.UserId == null \|\| == default`, throw `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; (2) `var question = await questionRepository.GetByIdAsync(request.QuestionId, cancellationToken).ConfigureAwait(false);` (tracked), and if null throw `NotFoundCoreException(ErrorCodes.QuestionNotFound)`; (3) `question.Retire(currentUserService.UserId.Value);`; (4) `await questionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);`. Mirror `ArchiveLessonHandler`. |
| A4 | `api/Elmanhg.Application/Questions/GetServableQuestionCount/GetServableQuestionCountQuery.cs` | query | `public sealed record GetServableQuestionCountQuery : IRequest<ServableQuestionCountResult>;` (no properties, no validator; precedent `GetSubjectsQuery`). |
| A5 | `api/Elmanhg.Application/Questions/GetServableQuestionCount/ServableQuestionCountResult.cs` | result, client-facing (no localized fields) | `public sealed record ServableQuestionCountResult(int Count);` |
| A6 | `api/Elmanhg.Application/Questions/GetServableQuestionCount/GetServableQuestionCountHandler.cs` | handler | `public sealed class GetServableQuestionCountHandler(IQuestionRepository questionRepository, IMemoryCache memoryCache, IOptions<ContentOptions> contentOptions) : IRequestHandler<GetServableQuestionCountQuery, ServableQuestionCountResult>`. `Handle`: (1) `if (memoryCache.TryGetValue(ServableQuestionCountCache.Key, out int cached)) { return new ServableQuestionCountResult(cached); }`; (2) `var count = await questionRepository.CountServableAsync(cancellationToken).ConfigureAwait(false);`; (3) `memoryCache.Set(ServableQuestionCountCache.Key, count, TimeSpan.FromSeconds(contentOptions.Value.ServableCountCacheSeconds));`; (4) `return new ServableQuestionCountResult(count);`. |
| A7 | `api/Elmanhg.Application/Questions/Shared/ServableQuestionCountCache.cs` | static class, new | `public static class ServableQuestionCountCache { public const string Key = "questions:servable-count"; }` |
| A8 | `api/Elmanhg.Application/Events/ServableQuestionCountInvalidation/ServableQuestionCountInvalidationHandler.cs` | notification handler (reuse shape: Morabh `Morabh.Application/Events/OrderWorkflowNotificationEvent/OrderWorkflowNotificationEventHandler.cs`) | `namespace Elmanhg.Application.Events.ServableQuestionCountInvalidation;` `public sealed class ServableQuestionCountInvalidationHandler(IMemoryCache memoryCache) : INotificationHandler<LessonPublished>, INotificationHandler<LessonUnpublished>, INotificationHandler<LessonArchived>, INotificationHandler<QuestionApproved>, INotificationHandler<QuestionRejected>, INotificationHandler<QuestionReturnedToPending>, INotificationHandler<QuestionRetired>`. There are 7 `public Task Handle(<Event> notification, CancellationToken cancellationToken) { return Invalidate(); }` and `private Task Invalidate() { memoryCache.Remove(ServableQuestionCountCache.Key); return Task.CompletedTask; }`. Picked up by the existing `RegisterServicesFromAssembly`, with no extra registration. |
| I1 | (in `QuestionRepository.cs`, existing file) | repo methods | `CountServableAsync`: `return await _dbSet.WhereServable(_context.Set<Lesson>()).CountAsync(cancellationToken).ConfigureAwait(false);`. `CountServableByLessonAsync`: `return await _dbSet.WhereServable(_context.Set<Lesson>()).Where(x => lessonIds.Contains(x.LessonId)).GroupBy(x => x.LessonId).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken).ConfigureAwait(false);` (one operator per line). Use `_context`, not the primary-ctor `context` (avoids CS9124). `using Elmanhg.Domain.Lessons;`. |
| M1 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddQuestionRetiredAt.cs` + `.Designer.cs` | migration | `dotnet tool restore && dotnet ef migrations add AddQuestionRetiredAt -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Expected `Up` content: only `AddColumn<DateTimeOffset>("RetiredAt", "Questions", "timestamp with time zone", nullable: true)`. No other operations. `Down` drops that column. No `AppDbContext` mapping change (convention maps it). |
| T1 | `api/Elmanhg.Tests/Domain/Questions/ServableQuestionSpecificationTests.cs` | tests | see Test plan |
| T2 | `api/Elmanhg.Tests/Domain/Questions/QuestionRetirementTests.cs` | tests | see Test plan |
| T3 | `api/Elmanhg.Tests/Domain/Questions/QuestionServabilityEventsTests.cs` | tests | see Test plan |
| T4 | `api/Elmanhg.Tests/Application/Features/Questions/RetireQuestion/RetireQuestionHandlerTests.cs` | tests | see Test plan |
| T5 | `api/Elmanhg.Tests/Application/Features/Questions/RetireQuestion/RetireQuestionValidatorTests.cs` | tests | see Test plan |
| T6 | `api/Elmanhg.Tests/Application/Features/Questions/GetServableQuestionCount/GetServableQuestionCountHandlerTests.cs` | tests | Real `new MemoryCache(new MemoryCacheOptions())` (disposed), `Options.Create(new ContentOptions { ServableCountCacheSeconds = 60 })`, NSubstitute `IQuestionRepository`. |
| T7 | `api/Elmanhg.Tests/Application/Features/Events/ServableQuestionCountInvalidationHandlerTests.cs` | tests | Real `MemoryCache`. |
| T8 | `api/Elmanhg.Tests/Integration/Content/ServableCountCollection.cs` | xUnit collection definition | `[CollectionDefinition(Name, DisableParallelization = true)] public sealed class ServableCountCollection { public const string Name = "ServableCount"; }` |
| T9 | `api/Elmanhg.Tests/Integration/Content/ServableQuestionCountEndpointTests.cs` | integration tests | `[Collection(ServableCountCollection.Name)] public sealed class ServableQuestionCountEndpointTests(ApiFactory factory)`. Private helpers mirror `QuestionResubmitEndpointTests` (`AdminClientAsync`, `SeedUnitAsync` via `ContentTestData.SeedSubjectAsync` + `SeedUnitAsync`, `ReadCountAsync(HttpClient)` returning `body.GetProperty("count").GetInt32()`). |
| T10 | `api/Elmanhg.Tests/Integration/Content/QuestionRetireEndpointTests.cs` | integration tests | Helpers as in `QuestionResubmitEndpointTests`. |
| T11 | `api/Elmanhg.Tests/Integration/Persistence/ServableQuestionSpecificationPersistenceTests.cs` | integration tests | Seeds its own subject, unit and lessons, reads through a fresh `AppDbContext` / `IQuestionRepository` scope, and filters by its own lesson ids. |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `Domain…ErrorCodes.QuestionAlreadyRetired` | `QUESTION_ALREADY_RETIRED` | `Question.Retire` | `BusinessRuleViolationCoreException` | 400 |
| `Domain…ErrorCodes.QuestionRetired` | `QUESTION_RETIRED` | `Question.EnsureNotRetired` (from `Update`, `Resubmit`, `Approve`, `Reject`) | `BusinessRuleViolationCoreException` | 400 |
| `Application…ErrorCodes.QuestionIdRequired` (existing) | `QUESTION_ID_REQUIRED` | `RetireQuestionValidator` | validation pipeline | 422 |
| `Application…ErrorCodes.QuestionNotFound` (existing) | `QUESTION_NOT_FOUND` | `RetireQuestionHandler` | `NotFoundCoreException` | 404 |
| `Application…ErrorCodes.UserNotAuthenticated` (existing) | `USER_NOT_AUTHENTICATED` | `RetireQuestionHandler` | `UnauthorizedCoreException` | 401 |

Resx (Arabic without tashkeel, same no-hamza style as the neighbouring entries):
| Key | ar | en |
|-----|----|----|
| `QUESTION_ALREADY_RETIRED` | `هذا السؤال متقاعد بالفعل.` | `This question is already retired.` |
| `QUESTION_RETIRED` | `لا يمكن تعديل سؤال متقاعد او اعتماده.` | `A retired question cannot be edited or validated.` |

## Domain behaviour
`Question.Retirement.cs`:
```csharp
public void Retire(Guid retiredBy)
{
    if (RetiredAt is not null)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionAlreadyRetired);
    }

    var now = DateTimeOffset.UtcNow;
    RetiredAt = now;
    UpdatedBy = retiredBy;
    UpdationDate = now;
    RaiseDomainEvent(new QuestionRetired(Id, LessonId));
}

private void EnsureNotRetired()
{
    if (RetiredAt is not null)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionRetired);
    }
}
```
- `Retire` does not change `ValidationStatus`, `Version`, `Revisions`, `ValidatedBy`/`ValidatedAt` or `RejectionReason`.
- Guard order in `Approve`/`Reject` (via `EnsureValidatorCanDecide`): `QUESTION_RETIRED` → `QUESTION_NOT_PENDING` → `QUESTION_VALIDATOR_NOT_ASSIGNED` → (Reject only) `QUESTION_REJECTION_REASON_REQUIRED`.
- `Update`: `EnsureNotRetired()` comes before the `QUESTION_TYPE_IMMUTABLE` check. `QuestionReturnedToPending` is raised only inside the `contentChanged && ValidationStatus == Approved` branch, once per call.
- `Resubmit`: `EnsureNotRetired()` comes before the `QUESTION_NOT_REJECTED` check. Resubmit raises no servability event (Rejected → Pending cannot change the count).
- `Approve` raises `QuestionApproved(Id, LessonId)` and `Reject` raises `QuestionRejected(Id, LessonId)`, each after the existing stamps. `UpdationDate` is already set by both.

## API surface
| Method | Route | Policy | Request | Response |
|--------|-------|--------|---------|----------|
| POST | `/api/questions/{questionId:guid}/retire` (Name `RetireQuestion`) | `[Authorize(Policy = DefaultCodes.ContentManage)]` | route `questionId`, no body → `RetireQuestionCommand(questionId)` | `200` bare `Ok()`; `[ProducesResponseType(StatusCodes.Status200OK)]` |
| GET | `/api/questions/servable-count` (Name `GetServableQuestionCount`) | `[AllowAnonymous]` | none → `new GetServableQuestionCountQuery()` | `200 ServableQuestionCountResult` JSON `{"count": n}`; `[ProducesResponseType<ServableQuestionCountResult>(StatusCodes.Status200OK)]` |
| GET | `/api/questions` (existing) | unchanged | unchanged | items gain `retiredAt` (nullable), `isServable` |
| GET | `/api/questions/{questionId}` (existing) | unchanged | unchanged | gains `retiredAt` (nullable) |
| GET | `/api/lessons?unitId=` (existing) | unchanged | unchanged | items gain `servableQuestionCount` |

Place `GetServableQuestionCount` immediately before the `GetQuestion` action and `RetireQuestion` after `ResubmitQuestion`. The `servable-count` literal does not collide with the `{questionId:guid}` constraint.

## Test plan
FluentAssertions 7, NSubstitute, xUnit v3, `TestContext.Current.CancellationToken`. Domain error codes are aliased `DomainErrorCodes` as in `ArchiveLessonHandlerTests`. For a published lesson in domain tests use `_builder.Lesson.Publish(Guid.NewGuid())`.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `ServableQuestionSpecificationTests` | `IsSatisfiedBy_ApprovedInPublishedLesson_ReturnsTrue` | true |
| 2 | 〃 | `IsSatisfiedBy_PendingQuestion_ReturnsFalse` | published lesson, pending question → false |
| 3 | 〃 | `IsSatisfiedBy_RejectedQuestion_ReturnsFalse` | `.Rejected("x")` → false |
| 4 | 〃 | `IsSatisfiedBy_RetiredApprovedQuestion_ReturnsFalse` | `.Approved().Retired()`, published → false |
| 5 | 〃 | `IsSatisfiedBy_DraftLesson_ReturnsFalse` | approved, lesson Draft → false |
| 6 | 〃 | `IsSatisfiedBy_ArchivedLesson_ReturnsFalse` | approved, lesson published then archived → false |
| 7 | 〃 | `IsSatisfiedBy_LessonOfAnotherQuestion_ReturnsFalse` | approved question, a different published `Lesson` → false |
| 8 | 〃 | `IsSatisfiedBy_ApprovedThenContentEdited_ReturnsFalse` | approved + published, `Update` with changed stem → false |
| 9 | 〃 | `WhereServable_MixedQuestionsAndLessons_KeepsOnlyServable` | LINQ-to-objects: questions [servable, pending, retired, approved-in-draft-lesson] and lessons [published, draft] `.AsQueryable()` → exactly the servable id |
| 10 | `QuestionRetirementTests` | `Retire_ActiveQuestion_StampsRetiredAtAndRaisesQuestionRetired` | `RetiredAt` not null, `IsRetired` true, `UpdatedBy == actor`, `GetDomainEvents()` ends with `new QuestionRetired(q.Id, q.LessonId)` |
| 11 | 〃 | `Retire_AnyValidationStatus_KeepsStatusAndVersion` | `[Theory]` `[InlineData(Pending)] [InlineData(Approved)] [InlineData(Rejected)]`: status and `Version` unchanged, revisions count unchanged |
| 12 | 〃 | `Retire_AlreadyRetired_ThrowsQuestionAlreadyRetired` | `BusinessRuleViolationCoreException`, `QUESTION_ALREADY_RETIRED`, `RetiredAt` unchanged |
| 13 | 〃 | `Update_RetiredQuestion_ThrowsQuestionRetired` | code `QUESTION_RETIRED`, `Stem` and `Version` unchanged |
| 14 | 〃 | `Resubmit_RetiredRejectedQuestion_ThrowsQuestionRetired` | `.Rejected("x").Retired()` → `QUESTION_RETIRED`, status stays Rejected |
| 15 | 〃 | `Approve_RetiredPendingQuestion_ThrowsQuestionRetired` | `QUESTION_RETIRED`, status Pending |
| 16 | 〃 | `Reject_RetiredPendingQuestion_ThrowsQuestionRetired` | `QUESTION_RETIRED`, `RejectionReason` null |
| 17 | `QuestionServabilityEventsTests` | `Approve_Pending_RaisesQuestionApproved` | events `Equal(new QuestionApproved(id, lessonId))` |
| 18 | 〃 | `Reject_Pending_RaisesQuestionRejected` | events `Equal(new QuestionRejected(id, lessonId))` |
| 19 | 〃 | `Update_ContentOnApproved_RaisesQuestionReturnedToPending` | after `ClearDomainEvents()`, events `Equal(new QuestionReturnedToPending(id, lessonId))` |
| 20 | 〃 | `Update_ContentOnPending_RaisesNoEvent` | events empty |
| 21 | 〃 | `Update_MetadataOnlyOnApproved_RaisesNoEvent` | difficulty change only → events empty, status Approved |
| 22 | 〃 | `Resubmit_Rejected_RaisesNoEvent` | after clear, resubmit with changed stem → events empty |
| 23 | `RetireQuestionHandlerTests` | `Handle_ExistingQuestion_RetiresAndSaves` | `RetiredAt` not null, `UpdatedBy == currentUserId`, `SaveChangesAsync` `Received(1)` |
| 24 | 〃 | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException`, `USER_NOT_AUTHENTICATED`, `DidNotReceive` save |
| 25 | 〃 | `Handle_UnknownQuestion_ThrowsQuestionNotFound` | `NotFoundCoreException`, `QUESTION_NOT_FOUND`, `DidNotReceive` save |
| 26 | 〃 | `Handle_AlreadyRetired_ThrowsQuestionAlreadyRetired` | `BusinessRuleViolationCoreException`, `QUESTION_ALREADY_RETIRED`, `DidNotReceive` save |
| 27 | `RetireQuestionValidatorTests` | `Validate_QuestionId_Passes` | valid |
| 28 | 〃 | `Validate_EmptyQuestionId_FailsWithQuestionIdRequired` | error code `QUESTION_ID_REQUIRED` |
| 29 | `GetServableQuestionCountHandlerTests` | `Handle_ColdCache_ReturnsRepositoryCount` | repo `CountServableAsync` returns 42 → `Count == 42`, cache holds 42 under `ServableQuestionCountCache.Key` |
| 30 | 〃 | `Handle_WarmCache_ReturnsCachedCountWithoutCounting` | repo returns 42 then 43, two calls → both 42, `CountServableAsync` `Received(1)` (the interaction is the caching behaviour) |
| 31 | 〃 | `Handle_AfterKeyRemoved_Recounts` | call, `cache.Remove(Key)`, call → second result 43, `Received(2)` |
| 32 | `ServableQuestionCountInvalidationHandlerTests` | `Handle_ServabilityEvent_RemovesCachedCount` | `[Theory]` `[MemberData]` over the 7 events (`LessonPublished`, `LessonUnpublished`, `LessonArchived`, `QuestionApproved`, `QuestionRejected`, `QuestionReturnedToPending`, `QuestionRetired`): seed cache 5, dispatch via a `switch` on the event type to the matching `Handle` overload → `TryGetValue(Key)` false |
| 33 | 〃 | `Handle_UnrelatedCacheEntry_IsKept` | cache has `"other"` = 1, handle `QuestionRetired` → `"other"` still present |
| 34 | `GetLessonsHandlerTests` (existing, updated) | `Handle_ExistingUnit_ReturnsMappedLessons` | expected results include `ServableQuestionCount` 2 / 0 |
| 35 | `GetQuestionsHandlerTests` (existing file) | `Handle_ApprovedInPublishedLesson_IsServable` | `_builder.Lesson.Publish(...)`, `.Approved()` → `IsServable` true, `RetiredAt` null |
| 36 | 〃 | `Handle_PendingQuestion_IsListedAndNotServable` | pending in published lesson → listed, `IsServable` false, `ValidationStatus` "Pending" |
| 37 | 〃 | `Handle_RetiredQuestion_IsListedWithRetiredAtAndNotServable` | `.Approved().Retired()`, published → listed, `RetiredAt` not null, `IsServable` false |
| 38 | `GetQuestionHandlerTests` (existing file) | `Handle_RetiredQuestion_ReturnsRetiredAt` | detail `RetiredAt == question.RetiredAt` |
| 39 | `ServableQuestionSpecificationPersistenceTests` | `WhereServable_MixedStatesAndLessonStates_ReturnsOnlyApprovedUnretiredInPublished` | Seed published lesson P (approved A, pending, rejected, approved+retired), Draft lesson D (approved), Archived lesson R (approved). `context.Questions.WhereServable(context.Lessons).Where(x => ids.Contains(x.LessonId)).Select(x => x.Id).ToListAsync()` → exactly `[A]` |
| 40 | 〃 | `CountServableByLessonAsync_MixedLessons_CountsOnlyPublished` | same seed shape, repository from scope → dictionary equals `{ [P] = 1 }` |
| 41 | `ServableQuestionCountEndpointTests` | `Get_Anonymous_Returns200WithCount` | seed an approved question in a published lesson, anonymous client → 200, `count >= 1` |
| 42 | 〃 | `Get_AfterLessonPublished_CountsItsApprovedQuestions` | seed Draft lesson + 1 approved + 1 pending question, baseline N, admin `POST /api/lessons/{id}/publish` → count == N + 1 |
| 43 | 〃 | `Get_AfterQuestionRetired_ExcludesIt` | seed Published lesson + approved Q, baseline N, admin `POST /api/questions/{Q}/retire` → N − 1 |
| 44 | 〃 | `Get_AfterApprovedContentEdited_ExcludesIt` | seed Published + approved Q, baseline N, admin `PUT /api/questions/{Q}` with a changed stem → N − 1, DB status Pending |
| 45 | 〃 | `Get_AfterLessonArchived_ExcludesItsQuestions` | seed Published + approved, baseline N, `POST …/archive` → N − 1 |
| 46 | 〃 | `Get_AfterLessonUnpublished_ExcludesItsQuestions` | seed Published + approved, baseline N, `POST …/unpublish` → N − 1 |
| 47 | `QuestionRetireEndpointTests` | `Post_ApprovedQuestion_RetiresAndAudits` | 200. DB `RetiredAt` not null, status still Approved, version 1. `ContentTestData.ReadAuditAsync(factory, "Question.Retire", id)` outcome "Success" |
| 48 | 〃 | `Post_RetiredQuestion_Returns400QuestionAlreadyRetired` | `SeedRetiredQuestionAsync` → 400, code `QUESTION_ALREADY_RETIRED` |
| 49 | 〃 | `Post_UnknownQuestion_Returns404QuestionNotFound` | 404, `QUESTION_NOT_FOUND` |
| 50 | 〃 | `Post_Teacher_Returns403` | assigned teacher → 403, DB `RetiredAt` null |
| 51 | 〃 | `Post_Anonymous_Returns401` | 401, DB `RetiredAt` null |
| 52 | 〃 | `Put_RetiredQuestion_Returns400QuestionRetired` | `PUT /api/questions/{id}` with a changed stem → 400 `QUESTION_RETIRED`, DB version 1 |
| 53 | 〃 | `GetList_RetiredQuestion_IsListedWithRetiredAtAndNotServable` | admin `GET /api/questions?lessonId=` → the item has `retiredAt` non-null and `isServable` false; an approved sibling in the same published lesson has `isServable` true |
| 54 | `AppDbContextTests` (existing, updated) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | 11th migration `_AddQuestionRetiredAt` |

Web: no new tests. `npm --prefix web run typecheck`, `lint`, `format:check` and `test` stay green after the fixture updates.

## Definition of done
- [ ] `ServableQuestionSpecification` is the only place in `api/` that combines Approved, Published and `RetiredAt`. `git grep -n "LessonState.Published" api/Elmanhg.Application api/Elmanhg.Infrastructure` shows no new servable logic, and the repository and `GetQuestionsHandler` call the spec.
- [ ] `IsSatisfiedBy` uses compiled copies of the same two expressions that `WhereServable` uses.
- [ ] No servable column or flag is persisted. The migration adds only nullable `Questions.RetiredAt`.
- [ ] `Question.Retire` guards, stamps `RetiredAt`/`UpdatedBy`/`UpdationDate`, and raises `QuestionRetired`. The status and version are unchanged.
- [ ] `Update`, `Resubmit`, `Approve` and `Reject` on a retired question throw `QUESTION_RETIRED` before any other guard.
- [ ] `Approve`, `Reject` and Approved → Pending content edits raise their events. Metadata-only edits, Pending edits and resubmit raise none.
- [ ] `ServableQuestionCountInvalidationHandler` handles exactly the 7 listed events and removes only `questions:servable-count`.
- [ ] `GET /api/questions/servable-count` is `[AllowAnonymous]` and returns `{"count": n}`, read through `IMemoryCache` with the `ContentOptions.ServableCountCacheSeconds` TTL (default 60 in code, in `appsettings.example.json` and in `ApiFactory`).
- [ ] `POST /api/questions/{id}/retire` uses `ContentManage` and is audited as `Question.Retire`.
- [ ] The admin `GET /api/questions` still returns every status, including retired questions, with `retiredAt` and `isServable`. `GET /api/lessons` returns `servableQuestionCount`.
- [ ] Both error codes are in both resx files. No new Application error codes, exception types or services.
- [ ] `Microsoft.Extensions.Caching.Memory` 10.0.5 is pinned in `Directory.Packages.props` and referenced by Application. `dotnet list package --vulnerable --include-transitive` is clean.
- [ ] `dotnet test api/ -c Release` is green with `appsettings.json` moved aside. All 54 test rows exist with these names.
- [ ] `dotnet ef migrations has-pending-model-changes` reports no changes.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated and committed, with no drift.
- [ ] web typecheck, lint, format and tests are green.
- [ ] Postman has "Get servable question count" and "Retire question" after "Resubmit question".
- [ ] Docs are updated: PRD §5.3 retire bullet, `docs/question-schemas.md` (guard order, Retirement, Servable), and `docs/audit-log.md` row.
- [ ] `dotnet format --verify-no-changes` is clean (outside vendored core). The guard grep prints nothing. No file exceeds ~100 lines.
