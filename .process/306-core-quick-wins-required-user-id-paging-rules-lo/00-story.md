# [E21.S3] Core quick wins: required user id, paging rules, load-or-404, soft-delete filter, validators, time helpers

Issue: #306

Promote the small generic helpers the app repeats everywhere, then use them at every matching call site.

- `ICurrentUserService` extension `GetRequiredUserId(errorCode)` (the app passes its code); replace the 110 copies of the null/default check + 401.
- Paging: a `ValidatePaging(...)` FluentValidation extension in Core.Validation that includes the overflow bound (only `GetMyAvatarConversationsValidator` has it today), used by all 14 paged validators; `PageData<T>.Map<TOut>()` replacing the 12 hand-written copies; `IQueryable<T>.ToPageDataAsync(page, size)` in Core.EntityFrameworkCore used by the infra repositories that hand-roll paging.
- `IRepository<T>.GetRequiredAsync(predicate or id, errorCode, ct, asNoTracking)` (load-or-404) replacing the ~103 find-then-throw-NotFound sites where the shape matches exactly.
- Soft-delete query filter convention: a core `ModelBuilder.ApplySoftDeleteQueryFilters()` extension for `ISoftDeletable`, called from `AppDbContext` (`CoreDbContext` unchanged); remove the 42 hand-written `HasQueryFilter(x => !x.IsDeleted)` where equivalent. The EF model snapshot must not change.
- Core.Validation: `ValidateFileSignature(params FileSignature[])` with built-in PNG/JPEG/WebP/GIF/WebM/Ogg/MP4/ZIP signatures, replacing the header checks in `LessonImageFormats`, `TeacherThreadImageFormats`, `TeacherVoiceFormats` and `QuestionImportFile.HasZipSignature` (also removes `UploadDiagramImageValidator` reaching into the TeacherThreads slice); `ValidateDateRange(from, to, maxSpan?)` replacing the 6 copies; `ValidateDistinct()` replacing the 3 copies; nullable-Guid support in `ValidateRequired`.
- Core.Utilities: `DateTimeOffset.TruncateToMicroseconds()` replacing the 6 private copies; time-zone helpers (local `DateOnly` for an instant, DST-safe `StartOfDay`) replacing the 5 + 2 copies; `AddValidatedOptions<TOptions, TValidator>()` used where the 39 registrations match.
- Core.OTP: `ConsumeAsync(verificationId, recipientType)` replacing the copied consume block in the 4 auth handlers.

### Sub-tasks
- [ ] GetRequiredUserId and call sites
- [ ] Paging rule, PageData.Map, ToPageDataAsync and call sites
- [ ] GetRequiredAsync and call sites
- [ ] Soft-delete filter convention (no model change)
- [ ] File signature, date range, distinct, nullable Guid validators and call sites
- [ ] Time and options helpers; OTP consume helper
- [ ] Tests for every new core helper; docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

