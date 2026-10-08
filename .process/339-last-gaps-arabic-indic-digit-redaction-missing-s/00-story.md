# [E21.S15] Last gaps: Arabic-Indic digit redaction, missing spreadsheet parts, per-context parity

Issue: #339

Close the last three gaps reported after #336 (PR #337).

**Dev instruction (2026-10-08):** "yes, go ahead with those too".

- **Arabic-Indic digits in log redaction.** Phone numbers written in Arabic-Indic digits (٠١٠…, U+0660–U+0669) or Extended Arabic-Indic digits (۰–۹, U+06F0–U+06F9), or mixed with ASCII digits, are not redacted by `LogRedactor` or the OTel collector.
  - Extend the pattern so these digits count as digits, in both copies, with the same left and right boundaries. The boundary classes must also treat these digits as digits.
  - The training-data scrubber already handles Arabic-Indic digits for 8+ digit runs. Check it stays consistent; do not change its behaviour.
  - The pattern must still give identical results in .NET NonBacktracking and RE2/Go `regexp`. Verify by running all existing test strings plus new Arabic-Indic cases in .NET, Python `re`, google-re2 and Go (Docker).
  - Watch the right boundary: the Arabic letters next to digits must still allow a match.
- **Corrupt spreadsheet without core parts gives a 500.** ClosedXML throws `NullReferenceException` when required package parts are missing, for example no `xl/workbook.xml` relationship target or no sheet part.
  - Do not catch `NullReferenceException` broadly. Detect the missing parts up front in `SpreadsheetPackageGuard`: check that the package has a workbook part and that every sheet the workbook declares exists, before handing the file to ClosedXML.
  - On failure, throw the configured unreadable error (400 `SPREADSHEET_UNREADABLE`).
  - Add test rows built from valid zips with those parts removed.
- **Collector parity test.** `LogRedactorCollectorParityTests` counts phone statements across the whole collector file. It must assert the exact pair in each of the four contexts (log body, log attributes, span attributes, span-event attributes).

### Sub-tasks
- [ ] Arabic-Indic digits in the redaction pattern (C# and collector), verified in 4 engines, with tests
- [ ] Spreadsheet missing-part pre-check returning 400, with tests
- [ ] Per-context collector parity assertion
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

