# Implementation — Graders for fill-in-the-blank and short answer (#72), rework round 2

## Findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Added the theory `GradeShort_PercentProductOverflows_BandFollowsValueSign(string value, string tolerance, string text, bool correct)` with the six review inputs and their exact expectations. `Numeric(-Max, 150, Percent)`: `-Max` → 1, `39e27` → 1, `40e27` → 0. `Numeric(Max, 150, Percent)`: `-39e27` → 1, `-40e27` → 0. `Numeric(Max, 300, Percent)`: `-Max` → 1. | `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs:254-270` |

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs` | Added one theory with 6 InlineData rows. This is the only change. |

Production code is unchanged. No new test exposed a bug.

## Mutation check
Each mutation was applied to `TextGrader.cs` on its own. After each one I ran only `--filter-method "*TextGraderTests.GradeShort_PercentProductOverflows*"`, which covers the 6 new rows plus T32. I then restored the file from a byte copy and checked lines 102, 103 and 116.

| Mutation | Result |
|---|---|
| A: `near = -magnitude` (drops `fraction - 1`) | 2 failed: `(-Max,150,"40e27",false)` and `(Max,150,"-40e27",false)` |
| B: `SaturatingProduct` returns `magnitude` | 2 failed: the same two rows |
| C: line 103 always returns `(near, decimal.MaxValue)` | 2 failed: `(-Max,150,"-Max",true)` and `(-Max,150,"40e27",false)` |
| Extra: `SaturatingProduct` without its saturation guard (`magnitude * factor`) | 1 failed: `(Max,300,"-Max",true)` threw `System.OverflowException` |

Unmutated baseline: 7/7 passed.

## Deviations
None.

## Build & test
- `dotnet build api/ -c Release`: Build succeeded. 0 Warning(s), 0 Error(s).
- `dotnet test api/ -c Release`: Passed. total 1267, failed 0, succeeded 1267, skipped 0. That is 1261 + 6.
- `git status --porcelain api/openapi`: empty output, so no drift.
- `dotnet format api/Elmanhg.Tests --verify-no-changes --include <TextGraderTests.cs>`: clean.
- Web and ai are untouched, so they were not re-run.

## Notes for review
- I did not apply the optional T31 change. It is not a one-line edit: it needs a `bool correct` parameter and three InlineData rows. With value 9.8, `-Max` is expected to be 0, which contradicts the planned method name `..._AcceptsEveryNumber`. Renaming a planned test is outside rework scope.
- The new test has a new method name. The plan's T32 and T33 are kept exactly as they were.
