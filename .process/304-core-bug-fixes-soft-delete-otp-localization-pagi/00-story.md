# [E21.S1] Core bug fixes: soft delete, OTP, localization, paging overflow, middleware order

Issue: #304

Fix the confirmed bugs in the vendored core libraries and the one in the API pipeline.

- `Core.DDD/Entities/Entity.cs` `SoftDelete()` sets `DeletedAt = null`; it must set the deletion time (accept a `DateTimeOffset` or `TimeProvider`; keep the 8 app call sites compiling and correct; `AvatarConversation` can drop its manual workaround). No backfill of old rows.
- `Core.OTP/Entities/OTP.cs`: compare hashes with `CryptographicOperations.FixedTimeEquals`; fix the reissue off-by-one (`ReissueCount > MaxReissueCount` allows Max+1; update the `OtpTests` that encode it); the daily reset must not depend on `CreatedAt`, which every reissue overwrites; `OtpRepository.FindAsync` filters on `RequestIP`/`UserAgent` that are never assigned — populate them or drop the filter (pick the safe option and document it).
- `Core.Localization`: one config key `CoreLocalization:DefaultLanguage` (`LocalizationManager` reads `Localization:DefaultLanguage` today, so it always falls back to English); guard the `[..2]` slice; compute the default language per call in `LocalizedTextExtensions` instead of a static captured once.
- Paging overflow: `Repository.FindPaginatedAsync` computes `(pageNumber - 1) * pageSize` in `int` (also copied in `EssayGradeRepository` and `MathStepGradeRepository`); compute the offset as `long`, count with `LongCountAsync`, compute `TotalPages` without `double`.
- `Core.Notifications` endpoints have no `RequireAuthorization`: add it (admin policy for template endpoints, authenticated user for feed/devices) so mapping them later is safe. The module stays in core and stays unregistered in the app.
- `Elmanhg.Api/Program.cs`: `UseAuthorization()` runs before `CoreExceptionMiddleware`, so exceptions during authorization skip the standard error format; move the exception middleware earlier, with an integration test.

### Sub-tasks
- [ ] SoftDelete sets the deletion time, with tests
- [ ] OTP: constant-time compare, reissue limit and daily reset, RequestIP filter, with tests
- [ ] Localization default-language key and per-call default, with tests
- [ ] Paging offset/count in long in core and the two copies, with tests
- [ ] Notifications endpoints require authorization
- [ ] Exception middleware order, with an integration test
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

