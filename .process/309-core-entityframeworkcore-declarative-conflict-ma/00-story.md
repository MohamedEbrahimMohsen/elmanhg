# [E21.S6] Core.EntityFrameworkCore: declarative conflict map and row-version convention

Issue: #309

Replace the 22 catch blocks in `AppDbContext.SaveChangesAsync` with a declarative map, without changing `CoreDbContext`.

- Core: `ConflictMap` (`MapConcurrency<TEntity>(errorCode)`, `MapUniqueConstraint(name, errorCode)`) and a helper that translates `DbUpdateConcurrencyException` and unique violations into `ConflictCoreException(code)`; unique-violation detection behind a provider hook so core does not reference Npgsql (the app supplies the Postgres `SqlState` check).
- `IVersioned` marker (`uint Version`) and a `ModelBuilder.ApplyRowVersionConvention()` extension replacing the 11 `.IsRowVersion()` lines, called from `AppDbContext`. The EF model snapshot must not change.
- `AppDbContext` uses both; every existing 409 code and its tests stay identical.

### Sub-tasks
- [ ] ConflictMap and translation helper in core, with tests
- [ ] IVersioned and row-version convention (no model change)
- [ ] AppDbContext switched over; all conflict tests green
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

