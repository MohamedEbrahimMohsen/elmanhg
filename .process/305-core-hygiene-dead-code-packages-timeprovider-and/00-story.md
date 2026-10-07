# [E21.S2] Core hygiene: dead code, packages, TimeProvider and audit actor stamping

Issue: #305

Clean up the vendored core so it builds from its own references and uses the app's clock.

- Delete commented-out and dead code: `Core.Identity/CoreAuthEndpoints.cs`, `Core.Identity/Register/*`, `Core.Logging/RequestLog.cs` (stray namespace), `Core.CQRS/Exceptions/ErrorCodes.cs`, `LocalizationOptions`, `ILocalizationManager` if still unused, `NotificationTemplateClaimsChecker`, the unused `Azure.Core` reference in Utilities and the unused CQRS→Utilities reference, the stale Latitude/Longitude columns in the Azure Table sink, the wrong namespace in `SeedLocalizedExtensions.cs`. `Core.Notifications` itself stays.
- Packages: replace the deprecated ASP.NET Core 2.3.9 packages with `<FrameworkReference Include="Microsoft.AspNetCore.App" />` where HTTP types are needed; add the missing direct references (MediatR in OTP and Notifications, FluentValidation in Notifications, ASP.NET in CQRS/OTP) so no project compiles only through transitive packages; align `Microsoft.Extensions.*` versions; bring core under the repo's central package management if feasible.
- Clock: inject `TimeProvider` into core where it uses `DateTimeOffset.UtcNow` (OTP, AuditBehaviour, Repository stamping, RefreshTokenService, AuditEntity defaults where feasible).
- Audit actor: `Repository.SaveChangesAsync` stamps `CreatedBy`/`UpdatedBy` from the current user when not already set (commented out today), and stamps `UpdationDate` only when the aggregate did not set it, so transitions that share one instant keep winning. Remove redundant hand-stamping of `UpdationDate = DateTimeOffset.UtcNow` in the Domain (about 50 sites) only where the repository stamp is equivalent.
- `Core.Auditing` depends on all of `Core.Identity` only for the current user id: introduce a tiny `ICurrentUser` (id only) in a lower core project, implemented by `CurrentUserService`. Remove the Morabh-only `NationalId`/`LoginType` members from `ICurrentUserService` if the app does not use them.
- `ValidationBehaviour` runs validators in parallel with `Task.WhenAll`; run them sequentially (scoped DbContext safety).
- OTP Egyptian defaults (`PhoneCodes`, `PhoneLength`) become config-only (appsettings already sets them). The `"PhoneNumber"` column mapping in `CoreDbContext` stays (dev decision: keep `CoreDbContext`).
- Missing `ConfigureAwait(false)` in core handlers/services.

### Sub-tasks
- [ ] Remove dead and commented-out core code
- [ ] Package references and versions
- [ ] TimeProvider in core
- [ ] CreatedBy/UpdatedBy stamping and Domain UpdationDate cleanup, with tests
- [ ] ICurrentUser for Auditing; Morabh-only claims removed
- [ ] Sequential validators; OTP defaults config-only; ConfigureAwait
- [ ] Docs (SKILL.md statements on audit stamping, IGenerator, Queues)

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

