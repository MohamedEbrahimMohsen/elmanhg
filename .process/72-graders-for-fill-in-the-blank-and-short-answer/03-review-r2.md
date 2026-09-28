VERDICT: APPROVED

# Review — Graders for fill-in-the-blank and short answer (#72), round 2

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs:249-252` — the round-1 note on T31 still stands: with value 0 the test reaches neither saturation guard. It does not gate this story, because T29 and T30 cover the guards. The implementer's reason for not changing it (it would need a planned test renamed) is reasonable.

## Verified
- **Blocking #1 is fixed.** `TextGraderTests.cs:260-270` adds `GradeShort_PercentProductOverflows_BandFollowsValueSign` with all six review inputs, and each expectation matches round 1:
  - `(-Max,150)`: `-Max` → 1, `39e27` → 1, `40e27` → 0.
  - `(Max,150)`: `-39e27` → 1, `-40e27` → 0.
  - `(Max,300)`: `-Max` → 1.
- **Mutations I ran myself.** I backed up `TextGrader.cs` first and ran only the filtered `GradeShort_PercentProductOverflows*` tests (7 cases).
  - C: `TextGrader.cs:103` always returns `(near, decimal.MaxValue)`. 2 failed: `(-Max,150,-Max,true)` and `(-Max,150,40e27,false)`.
  - A: `TextGrader.cs:102` changed to `near = -magnitude`. 2 failed: `(-Max,150,40e27,false)` and `(Max,150,-40e27,false)`.
  - Both results match the report. I restored the file from the backup, and `cmp` shows it is byte-identical to the pre-mutation copy.
- **Production code is unchanged since round 1.** Since `03-review.md` was written, only two tracked files have changed on disk: `TextGrader.cs`, which the implementer mutated and restored, and `TextGraderTests.cs`.
  - `TextGrader.cs` is byte-identical to the implementer's pre-mutation copy.
  - It is still 133 lines, with the round-1 cited code at the same lines (`:102-103`, `:114-117`).
  - Every other file in the diff is older than the round-1 review.
- **No regressions. CI re-run by me:**
  - `dotnet build api -c Release`: 0 errors.
  - `dotnet test api -c Release`: 1267/1267 passed (1261 + 6).
  - A forced `--no-incremental` build of `Elmanhg.Api` regenerated `api/openapi/v1.json` with no git drift. It showed 9 CS8602/CS8618 warnings in `core-libraries/Core.Validation`, `Core.OTP` and `Core.Notifications`, which this diff does not touch.
  - `dotnet format --verify-no-changes` is clean on the touched files.
  - web `npm run typecheck`: clean.
- **Report claims.** "Deviations: None", "production code is unchanged" and "1267 passed" are all confirmed.

## Test quality
- **TextGraderTests.** The overflow branch of `Bounds` is now pinned: the sign-dependent band, the `fraction - 1` near bound, and the saturating branch of `SaturatingProduct` (the `(Max,300)` row). Every other class is as assessed in round 1.
