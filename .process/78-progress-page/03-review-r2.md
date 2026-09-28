VERDICT: APPROVED

# Review r2 — [E5.S5] Progress page (#78)

## Blocking
None.

## Round-1 findings
### 1. A10 did not check the `WeakObjectiveCount` wiring: fixed
- `api/Elmanhg.Tests/Application/Features/Progress/GetWeakSpots/GetWeakSpotsHandlerTests.cs:99-114`: the limits are now 2 / 1. The builder lesson gets three real objectives through `Lesson.Update` (`Lesson.cs:38-73`: it keeps the existing id and creates two new ones), so the generator can resolve all three candidates. The test asserts exact lesson ids `[builder lesson, Energy]` and a single objective `objectives[0]`, which has the lowest percent (25).
- I ran the mutation myself. With `GetWeakSpotsHandler.cs:29` changed to `options.WeakLessonCount`, `Handle_ConfiguredCounts_LimitsBothLists` failed at line 113 ("Expected result.Objectives to contain a single item", 2 found). The line is restored, and I confirmed it with `sed -n 29p`. The file is untracked, so there is no git diff to compare against. A swap on the lesson side (1 instead of 2) would fail line 112.

### 2. PRD §7.6 streak divergence: fixed
- `docs/PRD.md:248-249`: the new Summary bullet says the streak is student-wide and shown once, and the streak is gone from the Per-subject bullet. This now agrees with `docs/progress.md` "The page" item 1, `docs/mastery.md` "Streak" and `ProgressSummary.tsx`.

## Non-blocking
- `docs/backlog.json:258`: "Progress query: per subject mastery, best exam scores, streak" reads ambiguously. It is a sub-task label, not a rule, so it is not a divergence. It could be reworded later.
- The round-1 non-blocking items still stand. None of them gate.

## Verified
- Rework scope: only the A10 test and `docs/PRD.md` changed. No production code changed, as the report claims.
- Targeted run on the restored code: `GetWeakSpotsHandlerTests` gave 6 total, 0 failed.
- Full run: `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside gave **1538 total, 0 failed, 0 skipped**, the same as round 1, so nothing regressed. `appsettings.json` has been restored.
- The web and ai stacks were untouched in this round, so round 1's web results still hold.

## Test quality
- `GetWeakSpotsHandlerTests` A10 now constrains both limits: I confirmed with a mutation that a wrong option fails the test. The other classes are unchanged since round 1.
