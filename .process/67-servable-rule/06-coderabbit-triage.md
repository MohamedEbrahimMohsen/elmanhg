TRIAGE: 1 to implement, 1 rejected, 0 dev-decisions

# CodeRabbit triage: #67 Servable rule (PR #154)

## RC1: cache fill vs invalidation race. Minor, REJECT
**Where:** `api/Elmanhg.Application/Questions/GetServableQuestionCount/GetServableQuestionCountHandler.cs:19-20`, `api/Elmanhg.Application/Events/ServableQuestionCountInvalidation/ServableQuestionCountInvalidationHandler.cs:75-79`
**Claim verified:** yes. `Handle` awaits `CountServableAsync` at line 19, then calls `Set` without any check at line 20. An `Invalidate()` that runs in between is overwritten with the count read before it.
**Why it is only Minor:** the outcome is the same stale-count-cached-for-at-most-TTL state that plan Decision 9 already accepts. `docs/question-schemas.md` § Servable (line 150) documents it. `CoreDbContext.SaveChangesAsync` publishes events *before* commit, so a fill that starts *after* invalidation but before commit reads the old rows and re-caches them. A generation token cannot catch that case, because that fill's generation is current. CodeRabbit asks to "keep the documented pre-commit consistency behavior unchanged", so after a coordinator the system still has exactly the same guarantee: the value is eventually correct within `ServableCountCacheSeconds` (60 s). The coordinator only closes one sub-window of a window that stays open, and the TTL bounds both.
**Why REJECT:**
- There is no observable improvement in the guarantee. The worst case is still "old count for up to 60 s" on a public marketing counter ("100,000 questions") with no per-user or correctness-critical consumer. No serving path reads this cache. Serving uses `WhereServable` against the database.
- The fix adds a singleton coordinator type, a lock, DI wiring and concurrency tests (which are timing-sensitive and flaky-prone) to guard an integer whose staleness is already bounded and documented. Plan Decision 8 chose `IMemoryCache` deliberately for its simplicity, and Decision 9 names the TTL as the accepted backstop.
- The real fix for the whole class of staleness is to publish domain events after commit, in `CoreDbContext`. That is out of scope under "mirror, don't modernize" (Decision 9). If it is ever done, it belongs in its own story, not a per-cache lock.
- Nothing to change in docs: code and `docs/question-schemas.md` § Servable already agree.
**Suggested reply on PR:** "Accepted design: staleness is bounded by the 60 s TTL and documented in docs/question-schemas.md § Servable. Events are published pre-commit (vendored CoreDbContext), so a generation guard would not remove the window, only narrow one sub-case of it."

## RC2: `PROGRESS.md:4` resume instruction. Minor, IMPLEMENT (orchestrator-owned)
**Claim verified:** yes, at PR head. Committed `PROGRESS.md:4` says "A Claude Code cloud session resumes at **#65**", although line 3 says #66 merged.
**Status:** the working tree already rewrites lines 4-5 to "The laptop run stopped after #64 … The next story to run is the first row of "Remaining stories"." This resolves it and does not go stale per story. The fix is still uncommitted (`git status`: ` M PROGRESS.md`). The orchestrator commits and pushes it. There is no code or reviewer action.
