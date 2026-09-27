# Implementation (rework r2): Bootstrap backend solution (.NET 10, DDD/CQRS, PostgreSQL), Story #54 [E1.S1]

## Blocking findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Replaced the scaffolder and `dotnet user-secrets` sentences in constitution §2 "Configuration Pattern". The new text says `appsettings.json` is copied by hand from the committed `api/Elmanhg.Api/appsettings.example.json`. Secrets and per-machine values come from the repo-root `.env` (copied from `.env.example`), which the API loads as environment variables in Development only, using the `Section__Key` convention. It also states there are no `dotnet user-secrets` and that deployed environments supply the same keys as host env vars. The "CI and fresh clones have NO configuration" consequence and the rest of the bullet are unchanged. This matches `Program.cs:20-26` and constitution §0.4. | `docs/constitution.md:99-109` |

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| `docs/constitution.md` | §2 configuration bullet rewritten to the `.env` + `appsettings.example.json` mechanism (finding 1). |

## Deviations
None.

## Build & test
No code changed (docs only), so I did not re-run `dotnet build api/` / `dotnet test api/`. The review's run of 14/14 passing still applies.

## Notes for review
I did not act on any non-blocking items.
