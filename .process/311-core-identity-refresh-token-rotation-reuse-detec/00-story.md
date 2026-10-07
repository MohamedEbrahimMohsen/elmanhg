# [E21.S8] Core.Identity: refresh-token rotation, reuse detection and security-stamp claim

Issue: #311

Promote the app's refresh-token security into core.

- Core.Identity gains single-use refresh-token rotation with reuse detection (grace window), family revocation, hashed token storage (`RefreshTokenHash`) and the hashed security-stamp claim checked in constant time (`SecurityStampClaim`), behind interfaces. The issued-token contract lives in core; the EF mapping stays in the app (`CoreDbContext` unchanged); no data migration.
- `RefreshAccessTokenHandler` becomes a thin call into core; core `RefreshTokenService` uses `TimeProvider`.
- Behaviour unchanged: same tokens, cookies, error codes and grace window; all auth tests green.

### Sub-tasks
- [ ] Core rotation, reuse detection, family revocation and security-stamp claim, with tests
- [ ] App switched over, no migration, auth tests green
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

