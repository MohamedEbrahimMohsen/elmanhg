# Implementation r2 — [E5.S5] Progress page (#78)

## Findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | A10 `Handle_ConfiguredCounts_LimitsBothLists`: limits are now `WeakLessonCount = 2`, `WeakObjectiveCount = 1`. The builder lesson gets three real objectives through `Lesson.Update(...)` (it keeps the existing first objective and adds two). All three objective candidates use those ids. The test asserts lessons equal `[builder lesson, Energy]` and exactly one objective, `objectives[0]`. | `api/Elmanhg.Tests/Application/Features/Progress/GetWeakSpots/GetWeakSpotsHandlerTests.cs:99-114` |
| 2 | PRD §7.6: added the bullet "Summary: headline counter and day streak (student-wide, shown once: consecutive days with ≥ 1 non-test quiz attempt, `docs/mastery.md`)." and removed the streak from the "Per subject" bullet. | `docs/PRD.md:248-249` |

## Files created
None, apart from this report.

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Tests/Application/Features/Progress/GetWeakSpots/GetWeakSpotsHandlerTests.cs` | A10 rewritten as described in finding 1 |
| `docs/PRD.md` | §7.6: the streak moved to a new Summary bullet |

## Deviations
None.

## Build & test
- Targeted run: `dotnet test api/Elmanhg.Tests -c Release -- --filter-class Elmanhg.Tests.Application.Features.Progress.GetWeakSpots.GetWeakSpotsHandlerTests` gave total 6, failed 0.
- Mutation check on `GetWeakSpotsHandler.cs:29`:
  - Changing `options.WeakObjectiveCount` to `options.WeakLessonCount` failed `Handle_ConfiguredCounts_LimitsBothLists` ("Expected result.Objectives to contain a single item") with 1 of 6 failing.
  - Changing it to `int.MaxValue` also failed `Handle_ConfiguredCounts_LimitsBothLists` with 1 of 6 failing.
  - Line 29 is restored to `options.WeakObjectiveCount`, and I confirmed this with `sed -n`.
- Full run: I moved `api/Elmanhg.Api/appsettings.json` aside and ran `dotnet test api/ -c Release`. Result: "Test run summary: Passed! total: 1538, failed: 0, succeeded: 1538, skipped: 0". The file has been restored.
- I did not run web or ai; this rework did not touch them.

## Notes for review
- The lesson limit is now checked as well: a swap (lessons capped at 1) would return one lesson instead of two.
- `PROGRESS.md` and `scripts/` were not touched. Nothing was committed.
