VERDICT: APPROVED

# Review — Core.Storage: file storage and media serving (E21.S7, story 310)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Application/Elmanhg.Application.csproj:14` — Application now gets `AWSSDK.S3` and the ASP.NET Core framework transitively through Core.Storage, because the abstraction and the providers share one project (plan Decision #2). This is within the plan and adds no package, but splitting into `Core.Storage` and `Core.Storage.S3` later would keep the AWS SDK out of Application.
- `api/Elmanhg.Tests/Core/Storage/PublicMediaFileProviderTests.cs:31` — on a case-sensitive file system, `/TEACHER-THREADS/x.png` passes because the file does not exist, not because `IsPrivate` matched. The implementer disclosed this. A Windows-only assertion or a case-insensitive fixture would pin it.
- `api/Elmanhg.Tests/Core/Storage/PublicMediaMiddlewareTests.cs` (123 lines) and `PublicMediaPipelineTests.cs` (108 lines) are over the ~100-line guide. The implementer disclosed this.

## Verified
- Every file in "Files to create" exists under `api/core-libraries/Core.Storage/` with the planned namespace, and is `sealed`/`static` with a file-scoped namespace. Contracts match plan #1–#17 line for line: `FileStorageOptions.GetPublicUrl`/`ResolveLocalRoot` (`FileStorageOptions.cs:30-32`), the validator messages built from `SectionName` with identical text, the `StorageContentTypes` allow-list plus overrides with the provider declared after its inputs (`StorageContentTypes.cs:10-21`), `WriteStoredFileAsync`/`SetMediaHeaders`, `PublicMediaOptions`, `IsPublicKey(key, privateFolders)` with the `TrimEnd('.')` and `OrdinalIgnoreCase` check (`PublicMediaMiddleware.cs:32-41`), and `UseCorePublicMedia` (`DependencyInjection.cs:34-53`).
- Behaviour is unchanged against `HEAD`:
  - The `LocalDiskFileStorage` traversal guard is byte-identical (`Local/LocalDiskFileStorage.cs:43-53`).
  - The S3 NotFound catch, `DisablePayloadSigning` and `AutoCloseStream` are kept.
  - Cache-Control and `nosniff` strings are identical on all three paths.
  - The copy still uses `RequestAborted`.
  - The 8.3 `~` block and `IsPrivate` were moved verbatim (`Media/PublicMediaFileProvider.cs:10-24`).
  - Content types are equivalent to the old `MediaContentTypes` for every key: the provider's last-dot extraction only finds the same simple extensions, and `.svg` and unknown extensions still fall back to `application/octet-stream`.
- The private-folder list exists once (`api/Elmanhg.Api/FileStorage/MediaStorageExtensions.cs:10`) and reaches both the S3 middleware and the Local file provider through `UseCorePublicMedia` (`DependencyInjection.cs:37,40,48`). `TeacherThreadMediaMiddleware` stays in the app and writes through `WriteStoredFileAsync(..., MediaCacheControl.PrivateNoStore)` (`TeacherThreadMediaMiddleware.cs:35`).
- Core is app-agnostic: `grep -rni "elmanhg|teacher|training"` over Core.Storage `*.cs`/`*.csproj` finds nothing.
- No new NuGet package. `AWSSDK.S3` 4.0.103.3 moved inline into `Core.Storage.csproj:10`, following `core-libraries/Directory.Packages.props` (CPM off for core), and was removed from `Directory.Packages.props` and `Elmanhg.Infrastructure.csproj`. Core.Storage is listed in `Elmanhg.slnx` under `/core-libraries/`.
- `Program.cs`, `Core.Notifications` and `CoreDbContext` are untouched (`git diff HEAD --stat` is empty for them).
- No references remain to `Elmanhg.Application.Shared.Storage`, `Elmanhg.Infrastructure.Storage`, `MediaContentTypes`, `AddFileStorage` or `FileStorageServiceCollectionExtensions` under `api/`, docs or `.claude`.
- There is no `#<number>` in any changed code or test file.
- Moved tests keep every original method and every InlineData, including all 5 private-folder spellings, both training-export spellings and all 5 unsafe keys. Only usings, namespaces, constructors and secrets changed (`not-a-secret-*`).
- Tests 13–29 exist with the exact names from the plan.
- Build: I ran `dotnet build api/Elmanhg.slnx --no-incremental` and got 0 errors and 9 warnings, all in vendored Core.Notifications, Core.OTP and Core.Validation. There are none in Core.Storage or the app projects.
- Tests: I ran `dotnet test --project api/Elmanhg.Tests` and got 5132 total, 5132 passed, 0 failed. This includes the integration regression tests (TeacherThreadMediaEndpointTests, the TrainingExport media 404, the LessonImages immutable cache).
- The three Deviations in `02-implementation.md` are accurate and harmless: `git mv` instead of create plus delete, the new test files, and the extra ask-teacher wording.
- Postman: no endpoint was added, changed or removed, so there is nothing to sync.
- Docs-sync: `constitution.md` (core list), `SKILL.md` (delta 5, §3 tree, §10 row), `security.md:47,139`, `ask-teacher.md:151`, `training-data.md:101`, `rich-text.md:52` (the stale "S3 adapter pending" is now fixed) and `implementation-report.md:224` all agree with the code. No stale references to old paths remain.

## Test quality
- `PublicMediaMiddlewareTests`: constrains the code. `Invoke_FolderNotInPrivateFolders_IsServed` proves the option, not a hard-coded name, drives the block, and the 404 cases assert `DidNotReceive` on the read.
- `PublicMediaPipelineTests`: constrains the code. It runs the real `StaticFileMiddleware` and the S3 middleware through `UseCorePublicMedia`. A wrong list on either path, missing headers on the Local path, or a provider branch inversion would fail it.
- `PublicMediaFileProviderTests`: constrains the code. The `~` and lower-case private cases would fail if the block were removed (with the case-sensitivity caveat above).
- `StoredFileResponseExtensionsTests`: constrains the code. It asserts the exact body, type, length and header strings.
- `StorageContentTypesTests`: constrains the code. It covers the `.webm/.ogg/.mp4` overrides (which would be `video/*` with the defaults), the upper-case extension, `.jsonl`, `.svg` and the no-extension fallback.
- `FileStorageOptionsTests`, `FileStorageOptionsValidatorTests`, `CoreFileStorageDependencyInjectionTests`, `LocalDiskFileStorageTests`, `S3FileStorageTests`: constrain real behaviour. The S3 tests assert the request shape, not just a value echoed back from a substitute.
- `MediaStorageExtensionsTests`: pins the app's single list. Removing `training-exports` would fail it.
- No vacuous tests found.
