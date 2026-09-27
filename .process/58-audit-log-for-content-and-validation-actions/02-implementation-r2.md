# Implementation r2 — Audit log for content and validation actions (#58, E1.S5)

## Rework table
| # | Change | File:line |
|---|---|---|
| 1 | Rewrote the `diff_json` note from "(always null on failure)" to "null when no audited entity changed. On failure it is null unless the handler committed changes before it threw; those committed changes are listed". This matches `AuditBehaviour.cs:57` and plan D6. No code change. | `docs/audit-log.md:32` |

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| `docs/audit-log.md` | `diff_json` row in § Record fields now matches the code |

## Deviations
None.

## Build & test
Not re-run. The only change is to one line of Markdown. The reviewer's run on the unchanged code was green (`dotnet test` 256/256, web 141/141).

## Notes for review
Grepped `docs/` for other "null on failure" / "always null" claims. There are none. The non-blocking findings are not addressed, as rework mode requires.
