# Implementation — Adaptive question selection (#75, E5.S2)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Sessions/Selection/QuestionSelectionBucket.cs` | 3 | Enum: Unseen=1, LastWrong=2, CorrectOnce=3, Rest=4 |
| `api/Elmanhg.Domain/Sessions/Selection/QuestionAttemptSummary.cs` | 9 | Sealed record with `IsLastAttemptCorrect` and `Bucket` (D3), plus the one allowed WHY comment |
| `api/Elmanhg.Domain/Sessions/Selection/QuestionSelector.cs` | 60 | Static pure selector (steps 1–9 of "Domain behaviour"), helpers `ShuffledOldestFirst` / `WeightedLeastRecentFirst`, plus the one allowed WHY comment |
| `api/Elmanhg.Application/Shared/Options/MasteryOptions.cs` | 11 | `Mastery:CorrectThreshold`, `[Range(0.01, 1.0)]`, default 0.8m |
| `api/Elmanhg.Tests/Domain/Sessions/Selection/QuestionSelectorTests.cs` | 183 | S1–S14 |
| `api/Elmanhg.Tests/Domain/Sessions/Selection/QuestionAttemptSummaryTests.cs` | 50 | B1–B4 |
| `api/Elmanhg.Tests/Application/Features/Sessions/MasteryOptionsTests.cs` | 40 | O1–O2 (the `BuildProvider` shape is copied from `SessionsOptionsTests`) |
| `api/Elmanhg.Tests/Integration/Persistence/AttemptSummaryPersistenceTests.cs` | 104 | P1–P4 |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Questions/IQuestionRepository.cs` | Removed `GetRandomServableInLessonAsync`; added `GetServableIdsInLessonAsync(Guid, CancellationToken)` |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | Same change on the implementation side, via `WhereServable` and an id projection |
| `api/Elmanhg.Domain/Sessions/ISessionRepository.cs` | Added `GetAttemptSummariesAsync` |
| `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | Implemented `GetAttemptSummariesAsync` exactly as the plan gives it. EF translated the constructor projection directly, so no anonymous-type fallback was needed |
| `api/Elmanhg.Application/Sessions/StartQuizSession/StartQuizSessionHandler.cs` | New constructor (`IOptions<MasteryOptions>`, `Random`) and `SelectQuestionsAsync`, as specified |
| `api/Elmanhg.Application/DependencyInjection.cs` | Registered `MasteryOptions` (validated on start) and `AddSingleton(Random.Shared)` |
| `api/Elmanhg.Api/appsettings.example.json` | Added `"Mastery": { "CorrectThreshold": 0.8 }` after the Sessions line |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Added `["Mastery:CorrectThreshold"] = "0.8"` |
| `api/Elmanhg.Tests/Application/Features/Sessions/StartQuizSession/StartQuizSessionHandlerTests.cs` | Rewritten as H1–H12 with the planned stubs (an intentional behaviour change, §8.11) |
| `api/Elmanhg.Tests/Integration/Sessions/StartQuizSessionEndpointTests.cs` | Added E1 and E2, plus a private `ServedQuestionIds` helper that orders by position; existing tests untouched |
| `api/Elmanhg.Tests/Integration/Sessions/SessionTestData.cs` | Added `FinishAsync` |
| `api/Elmanhg.Tests/Integration/Persistence/ServableQuestionSpecificationPersistenceTests.cs` | Added P5 |
| `docs/sessions.md` | Lifecycle step 1 now refers to adaptive selection. Added a `## Selection` section covering D1, D3–D8, D11, D12, D14 and D16. Added the Options row and the Test mode sentence |

The Postman collection, `api/openapi/v1.json`, the Orval client and `web/` are all unchanged (no API change).

## Deviations
None.

## Build & test
All commands ran in this cloud session. `api/Elmanhg.Api/appsettings.json` does not exist here, so the runs already match CI; nothing had to be moved aside.
- `dotnet build api/ -c Release`: `0 Error(s)`. The only warnings are pre-existing CS8618 warnings in `core-libraries/Core.Notifications`.
- `git status --porcelain api/openapi`: empty, so no drift.
- `dotnet test api/ -c Release`: `Test run summary: Passed! total: 1425, failed: 0, succeeded: 1425, skipped: 0`.
  - A filtered run over the 7 touched or new test classes gave `total: 51, succeeded: 51`. That is 14 S + 4 B + 2 O + 12 H + 4 P, plus the 12 endpoint tests (10 existing + E1–E2) and the 3 servable tests (2 existing + P5).
- `dotnet list api/ package --vulnerable --include-transitive`: every project reports "has no vulnerable packages".
- `dotnet tool restore`: "Restore was successful."
- `dotnet ef migrations has-pending-model-changes ... --no-build` (with the design connection string): "No changes have been made to the model since the last migration."
- `npm --prefix web run gen:api`: `git status --porcelain web/` is empty afterwards, so no drift.
- `npm --prefix web run typecheck`: clean (`tsc -b`, no output).
- `npm --prefix web test -- --run`: `Test Files 70 passed (70)`, `Tests 399 passed (399)`.

## Notes for review
- The test classes use a `private static readonly DateTimeOffset T0` field instead of a per-test `var t0` local. The value is the same (2026-01-01 UTC), and one field avoids repeating the line in 18 tests.
- `WeightedLeastRecentFirst` takes the ranked list from `ShuffledOldestFirst`. The `.ToList()` before `OrderByDescending` materialises the keys, so each element draws exactly one `NextDouble` in rank order, as the plan requires.
- H5 overrides the `FindAsync` stub through a private `StubFindQuestions(Func<Question, bool>)` helper. The constructor stub is the same helper with `_ => true`, so the planned predicate-compile shape is kept.
- `FinishSessionEndpointTests` still has its own private `FinishAsync`, as the plan says. The class member takes precedence over the `using static SessionTestData` import, so there is no ambiguity.
- Several test files go over the ~100-line guide: `QuestionSelectorTests` (183), `StartQuizSessionHandlerTests` (211) and `StartQuizSessionEndpointTests` (210). This follows from the planned test counts per class. Production files are all 71 lines or fewer.
- E1 relies on the student's answers getting strictly increasing `CreatedAt` values. They are sequential HTTP round trips, each more than 1 µs apart, so this holds.
