# [E21.S9] Core.Settings: runtime settings and feature flags

Issue: #312

Promote the runtime settings store (705 lines, 33 consumers) to a new `Core.Settings`.

- Core: typed `RuntimeSettingKey<T>`, definitions, a registry with startup duplicate/default validation, constraints, JSON values, the cached reader, and the override-store contract. `RuntimeSettingGroup` becomes a string (the app keeps its group names and order). Cache invalidation is per process: document the multi-instance staleness window (`CacheSeconds`), or use Core.Cache if E21.S10 has landed.
- The override entity mapping stays in the app's `AppDbContext` (`CoreDbContext` unchanged); no data migration.
- The app keeps its concrete settings, feature flags, Configuration page, endpoints and handlers; only the framework moves.
- Behaviour, API and admin page unchanged.

### Sub-tasks
- [ ] Core.Settings framework, with tests
- [ ] App switched over, no migration, configuration tests green
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

