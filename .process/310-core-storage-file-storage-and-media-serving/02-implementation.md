# Implementation — Core.Storage: file storage and media serving (E21.S7, story 310)

Worktree: `D:/Personal/elmanhg-wt/310`. Not committed or pushed. Every moved file went through `git mv`, so git reports each one as a rename (R/RM).

## Files created
| Path | Lines | Purpose |
|------|-------|---------|
| `api/core-libraries/Core.Storage/Core.Storage.csproj` | 14 | Core project. Pins `AWSSDK.S3` 4.0.103.3 inline and has `FrameworkReference Microsoft.AspNetCore.App`. |
| `api/core-libraries/Core.Storage/IFileStorage.cs` | 12 | Moved from `Elmanhg.Application/Shared/Storage` (git mv). Only the namespace changed. |
| `api/core-libraries/Core.Storage/StoredFile.cs` | 6 | Moved from Application (git mv). Only the namespace changed. |
| `api/core-libraries/Core.Storage/FileStorageProvider.cs` | 3 | Moved from Infrastructure (git mv). |
| `api/core-libraries/Core.Storage/FileStorageOptions.cs` | 33 | Moved (git mv). Adds `GetPublicUrl(key)` and `ResolveLocalRoot(contentRootPath)`. |
| `api/core-libraries/Core.Storage/FileStorageOptionsValidator.cs` | 34 | Moved (git mv). Messages are built from `SectionName`; the text is identical. |
| `api/core-libraries/Core.Storage/StorageContentTypes.cs` | 41 | git mv of `MediaContentTypes.cs`, then rewritten. It allow-lists six `FileExtensionContentTypeProvider` defaults and overrides four extensions. |
| `api/core-libraries/Core.Storage/Local/LocalDiskFileStorage.cs` | 54 | Moved (git mv). Uses `GetPublicUrl`, `ResolveLocalRoot` and `StorageContentTypes`; the traversal guard is unchanged. |
| `api/core-libraries/Core.Storage/S3/S3FileStorage.cs` | 45 | Moved (git mv). Uses `GetPublicUrl` and `StorageContentTypes`. |
| `api/core-libraries/Core.Storage/Media/MediaCacheControl.cs` | 9 | New: `PublicImmutable` and `PrivateNoStore`. |
| `api/core-libraries/Core.Storage/Media/StoredFileResponseExtensions.cs` | 20 | New: `SetMediaHeaders` and `WriteStoredFileAsync`. |
| `api/core-libraries/Core.Storage/Media/PublicMediaOptions.cs` | 5 | New record `(PathString RequestPath, IReadOnlyList<string> PrivateFolders)`. |
| `api/core-libraries/Core.Storage/Media/PublicMediaMiddleware.cs` | 44 | Moved from Api (git mv). Takes `PublicMediaOptions`; `IsPublicKey(key, privateFolders)` is now public; responses go through `WriteStoredFileAsync`. |
| `api/core-libraries/Core.Storage/Media/PublicMediaFileProvider.cs` | 25 | Moved from Api (git mv). Only the namespace changed. |
| `api/core-libraries/Core.Storage/DependencyInjection.cs` | 77 | git mv of `FileStorageServiceCollectionExtensions.cs`. Contains `AddCoreFileStorage` (same body) and the new `UseCorePublicMedia`. |
| `api/Elmanhg.Tests/Core/Storage/FileStorageOptionsTests.cs` | 34 | Tests 16–18. |
| `api/Elmanhg.Tests/Core/Storage/PublicMediaFileProviderTests.cs` | 57 | Tests 19–22. |
| `api/Elmanhg.Tests/Core/Storage/StoredFileResponseExtensionsTests.cs` | 36 | Tests 23–24. |
| `api/Elmanhg.Tests/Core/Storage/PublicMediaPipelineTests.cs` | 108 | Tests 25–28. |
| `api/Elmanhg.Tests/Api/FileStorage/MediaStorageExtensionsTests.cs` | 13 | Test 29. |
| Moved tests (git mv into `Elmanhg.Tests/Core/Storage/`) | — | `PublicMediaMiddlewareTests` (adds tests 13–14), `LocalDiskFileStorageTests`, `S3FileStorageTests`, `FileStorageOptionsValidatorTests` (secrets changed to `not-a-secret-*`), `CoreFileStorageDependencyInjectionTests` (renamed; methods are `AddCoreFileStorage_*`), `StorageContentTypesTests` (renamed; adds the `.jsonl` row and test 15). |

## Files modified
| Path | Change |
|------|--------|
| `api/Elmanhg.slnx` | Added Core.Storage between Core.Queues and Core.Utilities. |
| `api/Directory.Packages.props` | Removed the `AWSSDK.S3` PackageVersion. |
| `api/Elmanhg.Application/Elmanhg.Application.csproj`, `api/Elmanhg.Api/Elmanhg.Api.csproj` | Added a Core.Storage ProjectReference after Core.OTP. |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | Removed the `AWSSDK.S3` PackageReference. Added a Core.Storage ProjectReference after Core.EntityFrameworkCore. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `using` swap; `AddFileStorage()` becomes `AddCoreFileStorage()`. |
| `api/Elmanhg.Infrastructure/Hosting/InfrastructureConfigurationReader.cs`, `InfrastructureIntegrations.cs` | `using` swap only. |
| `api/Elmanhg.Infrastructure/RichText/RichTextSanitizer.cs` | `using` swap. The prefix is now `GetPublicUrl(string.Empty)`. |
| `api/Elmanhg.Api/FileStorage/MediaStorageExtensions.cs` | Rewritten. Holds `PrivateFolders`, then calls TeacherThreadMediaMiddleware followed by `UseCorePublicMedia(PrivateFolders)`. |
| `api/Elmanhg.Api/FileStorage/TeacherThreadMediaMiddleware.cs` | `using`s are now Core.Storage and Core.Storage.Media. The tail is `WriteStoredFileAsync(file, MediaCacheControl.PrivateNoStore)`. |
| 24 Application files and 28 test files | `using Elmanhg.Application.Shared.Storage;` / `using Elmanhg.Infrastructure.Storage;` replaced by `using Core.Storage;` in alphabetical position (done by script; no other change). |
| `docs/constitution.md`, `docs/security.md`, `docs/ask-teacher.md`, `docs/training-data.md`, `docs/rich-text.md`, `docs/implementation-report.md`, `.claude/skills/dotnet-feature/SKILL.md` | Edited as listed under "Docs to update" in the plan. |

`Program.cs`, Core.Notifications and CoreDbContext are untouched. Postman is untouched because no endpoint changed.

## Deviations
| Plan said | Reality | What I did |
|-----------|---------|------------|
| Lists `DependencyInjection.cs`, `StorageContentTypes.cs`, `PublicMediaMiddleware.cs`, `PublicMediaFileProvider.cs` etc. as "create" and the old files as "delete". | The orchestrator asked for `git mv` to keep history. | Each old file was `git mv`'d to its new core path and then edited to the plan's contract. The end state is the same, and history is kept. |
| "Files to create" does not list the new test files. | The Test plan requires `FileStorageOptionsTests`, `PublicMediaFileProviderTests`, `StoredFileResponseExtensionsTests`, `PublicMediaPipelineTests` and `MediaStorageExtensionsTests`. | Created them with the exact class and method names from the Test plan. |
| ask-teacher.md: "name the classes as Core.Storage". | — | Added "(in `Core.Storage`)" to the two class mentions and appended one sentence about the single `MediaStorageExtensions.PrivateFolders` list. |

## Build & test
- `dotnet build api/Elmanhg.slnx --no-incremental`: **Build succeeded, 0 errors, 9 warnings.** All 9 warnings are already present in vendored `Core.Notifications`, `Core.OTP` and `Core.Validation` (CS8618/CS8602). There are 0 warnings in Core.Storage or in any app project.
- `dotnet test --project api/Elmanhg.Tests -- --filter-namespace Elmanhg.Tests.Core.Storage`: **68 passed, 0 failed.**
- `dotnet test api/` (full suite, Testcontainers with Docker): **Passed: total 5132, failed 0, succeeded 5132, skipped 0** (1m 07s).
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --no-build`: "No changes have been made to the model since the last migration."
- `git status --porcelain api/openapi`: empty, so OpenAPI is unchanged.
- DoD greps:
  - `Elmanhg.Application.Shared.Storage|Elmanhg.Infrastructure.Storage|MediaContentTypes|AddFileStorage(` under `api/` sources: no matches.
  - `grep -rni "elmanhg|teacher|training" api/core-libraries/Core.Storage` (*.cs/*.csproj): no matches.
  - `TrainingExportFiles.StorageFolder` in non-test code: only `MediaStorageExtensions.PrivateFolders`.
  - `#<number>` in the touched code and test folders: none.

## Notes for review
- Three test files are over the ~100-line guide: `PublicMediaMiddlewareTests` (123, because tests 13–14 were added to it), `PublicMediaPipelineTests` (108) and `LocalDiskFileStorageTests` (105, unchanged from before the move).
- `UseCorePublicMedia` now reads `IHostEnvironment.ContentRootPath` from DI. It used to read `WebApplication.Environment`, which is the same singleton, so behaviour is unchanged.
- `PublicMediaPipelineTests` builds a real `ApplicationBuilder` with `StaticFileMiddleware` against a temp directory. That proves the Local path keeps `nosniff` and immutable caching and returns 404 for private folders.
- On a case-sensitive file system, the `/TEACHER-THREADS/x.png` case in test 20 passes because the file is not found, not because `IsPrivate` matches. It still asserts `Exists == false` as planned.
- I used `git add -N` on the new files to show them in the diff stats. Nothing is committed.
