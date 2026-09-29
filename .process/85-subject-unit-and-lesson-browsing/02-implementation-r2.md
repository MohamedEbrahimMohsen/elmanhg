# Implementation (rework r2): [E7.S1] Subject, unit and lesson browsing (#85)

## Blocking findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Removed `size="sm"` from the per-unit exam `Button`. It now uses the default size (`min-h-11`, 44 px). | `web/src/features/browse/components/UnitListItem.tsx:33` |
| 2 | Added `Handle_LessonOfMissingSubject_ThrowsLessonNotFound`. It creates a unit under an unstubbed subject, adds it to `_units` so that `GetByIdAsync` for the unit resolves, and adds a Published lesson. It asserts `NotFoundCoreException` with `LESSON_NOT_FOUND`. | `api/Elmanhg.Tests/Application/Features/Browse/GetStudentLesson/GetStudentLessonHandlerTests.cs:98-108` |

### Mutation check (finding 2)
- I could not just delete lines 36-39 of `GetStudentLessonHandler.cs`: with the lines gone, the build fails on CS8602 (nullable warnings are errors).
- So the mutant replaced the guard with `... .ConfigureAwait(false) ?? null!;`. That keeps the build green but lets a null subject through, which is the same behaviour as the review's "delete lines 36-39".
- Result: `Handle_LessonOfMissingSubject_ThrowsLessonNotFound` failed (1 failed, 9 succeeded), so the new test kills the mutant.
- I restored the handler afterwards. Lines 35-39 are byte-identical to the original.

## Non-blocking addressed
- `web/src/features/browse/components/LessonNavigation.tsx:11`: changed `break-words` to `wrap-anywhere` in `linkClassName`. It is a one-token change.
  - `overflow-wrap: anywhere` is inherited by the anonymous flex item. Unlike `break-word`, it lowers the item's min-content size, so a long unbroken lesson name wraps instead of overflowing at 375 px.
  - Tailwind is 4.3.3, which has the utility.
- I did not change the prev/next link height (below 44 px). The review says this is not gated, and the fix is not a one-line change.

## Files modified
| Path | Change |
|---|---|
| `web/src/features/browse/components/UnitListItem.tsx` | Removed `size="sm"` |
| `web/src/features/browse/components/LessonNavigation.tsx` | `break-words` changed to `wrap-anywhere` |
| `api/Elmanhg.Tests/Application/Features/Browse/GetStudentLesson/GetStudentLessonHandlerTests.cs` | Added 1 test |

## Deviations
None. I did not touch `PROGRESS.md` or `scripts/`.

## Build & test
- `dotnet test --project api/Elmanhg.Tests -c Release --filter-class ...GetStudentLessonHandlerTests`: Passed. 10 total, 0 failed.
- Mutant run with the same filter: 1 failed (the new test), 9 succeeded.
- `dotnet test api/ -c Release`, with `api/Elmanhg.Api/appsettings.json` moved to the scratchpad and restored afterwards: Passed. 2490 total, 0 failed, 0 skipped.
- `npm --prefix web test -- --run`: 123 files, 778 tests passed.
- `npm --prefix web run lint` (`eslint . --max-warnings=0`): clean.
- `npx prettier --check --end-of-line auto src` in `web/`: "All matched files use Prettier code style!"
- `npx tsc -b --noEmit` in `web/`: no output (clean).

## Notes for review
- No web test asserts the button size or the wrap class. The review said no test change was needed for #1.
