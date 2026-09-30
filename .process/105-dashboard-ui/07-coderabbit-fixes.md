# CodeRabbit fixes — PR #230 (105 dashboard UI)

| # | Finding | What I changed | File |
|---|---|---|---|
| 1 | `now` frozen by `useState(() => new Date())`, so the range goes stale after Cairo midnight | Added a `useCairoToday` hook that keeps the Cairo day string (`cairoToday`) in state and checks it again every 60 s with `setInterval`, cleaned up on unmount. Setting the same string does not re-render, so within one day nothing changes. `range` is now `useMemo` on `[days, today]` (built from `${today}T12:00:00Z`), so query params only change when the Cairo day changes | `web/src/features/dashboard/hooks/useDashboardFilters.ts` |
| 1 (test) | Fake-timer test that crosses Cairo midnight | `moves the range to the new Cairo day after midnight`: the clock starts at 2026-09-30T20:59:30Z (23:59:30 Cairo) and is advanced by 60 s. The test checks that the request uses `to=2026-10-01` and that the label reads "From Sep 18 to Oct 1". I confirmed it fails against the old hook | `web/src/features/dashboard/pages/DashboardPage.filters.test.tsx` |
| 2 | RC1/RC2 wording | Replaced the note with the requested text (doc-only) | `.process/117-essay-question-authoring-with-rubric/06-coderabbit-triage.md:3` |

## Deviations
None. The day switches up to 60 s after Cairo midnight because the hook polls. It does not set a timer for the exact midnight, since Egypt's DST transitions happen at midnight and would make that calculation fragile.

## Build & test (run in `web/`)
- `npx prettier --end-of-line auto --check src/features/dashboard …/06-coderabbit-triage.md`: all files use Prettier code style
- `npm run lint`: clean (0 problems)
- `npm run typecheck`: clean
- `npx vitest run src/features/dashboard`: 8 files, 49 tests passed
- `npx vitest run` (full): 206 files, 1196 tests passed

Not committed.
