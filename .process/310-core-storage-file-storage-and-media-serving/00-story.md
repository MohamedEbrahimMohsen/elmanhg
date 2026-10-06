# [E21.S7] Core.Storage: file storage and media serving

Issue: #310

Promote the file-storage abstraction and media serving.

- New `Core.Storage`: `IFileStorage`, `StoredFile`, local-disk provider (with the path-traversal guard), S3 provider, options + validator + DI, `GetPublicUrl` in one place, content types via `FileExtensionContentTypeProvider` where equivalent to `MediaContentTypes`.
- Media serving: `PublicMediaMiddleware` and `PublicMediaFileProvider` (8.3 short-name block) move to core with a `PrivateFolders` option. Today `PublicMediaMiddleware.IsPrivateFolder` and `MediaStorageExtensions` hard-code the teacher-thread-image and training-export folders separately, which can drift (a security risk); the app registers that list once.
- A shared `WriteStoredFileAsync(response, file, cacheControl)` used by both the public middleware and the app's `TeacherThreadMediaMiddleware` (which stays in the app: it is authorization-specific).
- Behaviour unchanged: private folders still 404 on both paths (existing tests kept), same headers.

### Sub-tasks
- [ ] Core.Storage abstraction and providers, with tests
- [ ] Media middleware and file provider with a PrivateFolders option; one list in the app
- [ ] Shared stored-file writer; teacher media middleware uses it
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

