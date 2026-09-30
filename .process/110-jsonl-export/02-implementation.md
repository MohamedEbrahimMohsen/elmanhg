# Implementation — [E12.S2] JSONL export (+ #229 essay-grade training records)

Worktree `D:/Personal/elmanhg-wt/110`, branch `feature/110-jsonl-export`. Not committed.
The plan was implemented with the three gate conditions from `00-acceptance.md` taking precedence: (1) no anonymous download and no signed link; the file is streamed by an admin-policied, audited endpoint and the web fetches it with the admin JWT and saves the blob; (2) the separate `EssayGradeTrainingRecords` table was kept; (3) export files have a configurable retention (default 7 days), and a sweep deletes expired files and marks the export `Expired`.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/EssayGrading/EssayGradeCompleted.cs | 5 | domain event raised by `EssayGrade.Complete` |
| api/Elmanhg.Domain/TrainingData/EssayGradeTrainingRecord.cs | 70 | append-only AI-grade training row, `From(...)` |
| api/Elmanhg.Domain/TrainingData/IEssayGradeTrainingRecordRepository.cs | 8 | repo + `GetExportPageAsync` |
| api/Elmanhg.Domain/TrainingData/TrainingRecordFilter.cs / TrainingRecordCursor.cs | 3 / 3 | export filter and keyset cursor |
| api/Elmanhg.Domain/TrainingExports/TrainingExportSource.cs | 18 | enum + `ToFileSlug` |
| api/Elmanhg.Domain/TrainingExports/TrainingExportStatus.cs | 3 | `Pending, Completed, Failed, Expired` |
| api/Elmanhg.Domain/TrainingExports/TrainingExport.cs | 59 | aggregate: `Request`, `IsDueAt`, `IsExpiredAt`, `DownloadFileName` |
| api/Elmanhg.Domain/TrainingExports/TrainingExport.Lifecycle.cs | 76 | `Complete(.., retention)`, `FailAttempt`, `EnsureDownloadableAt`, `Expire` |
| api/Elmanhg.Domain/TrainingExports/ITrainingExportRepository.cs | 10 | `GetDueIdsAsync`, `GetExpiredIdsAsync` |
| api/Elmanhg.Application/Shared/Options/TrainingExportsOptions.cs | 44 | all caps, retention and sweep settings |
| api/Elmanhg.Application/Events/TrainingRecords/EssayGradeTrainingRecordHandler.cs | 26 | writes the AI-grade training row (skips test mode) |
| api/Elmanhg.Application/TrainingExports/RequestTrainingExport/{Command,Validator,Handler}.cs | 13/18/29 | audited `TrainingExport.Request` |
| api/Elmanhg.Application/TrainingExports/GetTrainingExports/{Query,Validator,Handler}.cs | 7/18/25 | paged admin list, newest first |
| api/Elmanhg.Application/TrainingExports/DownloadTrainingExport/{DownloadTrainingExportQuery,DownloadTrainingExportHandler,TrainingExportFileResult}.cs | 11/20/3 | audited (`TrainingExport.Download`) admin file stream |
| api/Elmanhg.Application/TrainingExports/GetDueTrainingExportIds/{Query,Handler}.cs | 5/14 | worker listing |
| api/Elmanhg.Application/TrainingExports/FailTrainingExport/{Command,Handler}.cs | 5/23 | worker failure/backoff |
| api/Elmanhg.Application/TrainingExports/RunTrainingExport/{RunTrainingExportCommand,RunTrainingExportHandler,TrainingExportSourceWriter,TrainingExportJsonlWriter,TrainingExportTempFile}.cs | 5/44/48/27/6 | keyset-paged JSONL writer to temp file then `IFileStorage` |
| api/Elmanhg.Application/TrainingExports/GetExpiredTrainingExportIds/{Query,Handler}.cs | 5/14 | retention sweep listing |
| api/Elmanhg.Application/TrainingExports/ExpireTrainingExport/{Command,Handler}.cs | 11/27 | audited (`TrainingExport.Expire`) delete file + mark Expired |
| api/Elmanhg.Application/TrainingExports/Shared/{TrainingExportResult,TrainingExportResultGenerator,TrainingExportFiles,TrainingExportJson,TrainingDataScrubber,TrainingExportLines,TrainingExportLineGenerator}.cs | 9/11/8/15/50/15/30 | result, keys, JSON dialect, PII scrubber, line shapes |
| api/Elmanhg.Infrastructure/TrainingData/EssayGradeTrainingRecordRepository.cs | 32 | keyset page query |
| api/Elmanhg.Infrastructure/TrainingExports/TrainingExportRepository.cs | 35 | due / expired id queries |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.TrainingExports.cs | 31 | `TrainingExports` mapping |
| api/Elmanhg.Infrastructure/Migrations/20260930131953_AddTrainingExports.cs (+ .Designer.cs) | 145 | both tables + append-only triggers on `EssayGradeTrainingRecords` |
| api/Elmanhg.Api/Controllers/TrainingExports/TrainingExportsController.cs / Requests.cs | 45 / 5 | 3 actions, all `TrainingData.Export` |
| api/Elmanhg.Api/Workers/TrainingExportWorker.cs | 98 | `training-export` job (copy of `EssayGradingWorker`) |
| api/Elmanhg.Api/Workers/TrainingExportRetentionWorker.cs | 89 | `training-export-retention` job (copy of `SubscriptionLapseWorker`) |
| api/Elmanhg.Tests/Domain/TrainingExports/TrainingExportTests.cs, TrainingExportRetentionTests.cs | 98, 90 | domain |
| api/Elmanhg.Tests/Domain/TrainingData/EssayGradeTrainingRecordTests.cs | 61 | domain |
| api/Elmanhg.Tests/Application/Features/Events/EssayGradeTrainingRecordHandlerTests.cs | 106 | handler |
| api/Elmanhg.Tests/Application/Features/TrainingExports/** (Request validator+handler, Get validator+handler, Download, Fail, Expire, GetDue, GetExpired, Run handler, JsonlWriter, Scrubber, LineGenerator) | 28–151 | unit tests |
| api/Elmanhg.Tests/Api/Workers/TrainingExportWorkerTests.cs, TrainingExportRetentionWorkerTests.cs | 146, 180 | worker tests (1:1 mirrors) |
| api/Elmanhg.Tests/Builders/TrainingExportBuilder.cs | 57 | test builder |
| api/Elmanhg.Tests/Integration/TrainingExports/{TrainingExportEndpointTests,TrainingExportRetentionEndpointTests,TrainingExportTestData}.cs | 213/50/68 | HTTP + DB |
| api/Elmanhg.Tests/Integration/TrainingData/EssayGradeTrainingRecordTests.cs | 49 | grading writes the row / test mode writes none |
| api/Elmanhg.Tests/Integration/Persistence/EssayGradeTrainingRecordAppendOnlyTests.cs, TrainingRecordExportPageTests.cs | 82, 77 | triggers; real-Postgres keyset + latest-snapshot SQL |
| web/src/features/trainingExport/api/{trainingExportParams,downloadBlob}.ts (+ tests) | 46/11 (61/31) | body/paging helpers; blob save |
| web/src/features/trainingExport/schemas/{trainingExportRequestSchema,trainingExportSearchSchema}.ts (+ tests) | 26/7 (53/13) | form + URL schemas |
| web/src/features/trainingExport/hooks/{useTrainingExports,useTrainingExportSearch,useRequestTrainingExport,useDownloadTrainingExport}.ts | 22/15/32/36 | query with Pending polling, mutations |
| web/src/features/trainingExport/components/{TrainingExportForm,TrainingExportSelectField,TrainingExportTable,TrainingExportRow,TrainingExportStatusBadge,TrainingExportSkeleton,TrainingExportEmptyState}.tsx | 63/51/48/86/24/15/13 | UI |
| web/src/features/trainingExport/pages/TrainingExportPage.tsx (+ test) | 74 (225) | page |
| web/src/features/trainingExport/{i18n/ar.json,i18n/en.json,locales.ts,index.ts} | 57/57/4/2 | namespace `trainingExport` |
| web/src/shared/api/generated/** (training-exports, zod, 6 models) | generated | Orval |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/EssayGrading/EssayGrade.Grading.cs | `Complete` raises `EssayGradeCompleted` |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | `TRAINING_EXPORT_NOT_PENDING`, `_NOT_READY`, `_EXPIRED` |
| api/Elmanhg.Domain/TrainingData/I{Attempt,Avatar,TeacherThread}TrainingRecordRepository.cs | `GetExportPageAsync` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | 7 `TRAINING_EXPORT_*` codes |
| api/Elmanhg.Application/Shared/TrainingData/IStudentIdHasher.cs | `HashSourceId(scope, id)` |
| api/Elmanhg.Application/Shared/Storage/MediaContentTypes.cs | `.jsonl` → `application/x-ndjson` |
| api/Elmanhg.Application/Shared/Storage/IFileStorage.cs | `DeleteAsync(key, ct)` (retention) |
| api/Elmanhg.Application/DependencyInjection.cs | `TrainingExportsOptions` ValidateOnStart |
| api/Elmanhg.Infrastructure/TrainingData/HmacStudentIdHasher.cs | `HashSourceId` |
| api/Elmanhg.Infrastructure/TrainingData/{Attempt,Avatar,TeacherThread}TrainingRecordRepository.cs | keyset `GetExportPageAsync` (threads: latest in-range snapshot) |
| api/Elmanhg.Infrastructure/Storage/LocalDiskFileStorage.cs, S3FileStorage.cs | `DeleteAsync` |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | `ConfigureTrainingExports`, 2 global filters, `TrainingExport` concurrency → 409 |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.TrainingData.cs | `EssayGradeTrainingRecords` table/DbSet/mapping |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | 2 repositories |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/Elmanhg.Api/Program.cs | 2 hosted workers |
| api/Elmanhg.Api/FileStorage/PublicMediaMiddleware.cs, MediaStorageExtensions.cs | `training-exports` private in S3 and Local paths |
| api/Elmanhg.Api/Resources/Messages.{ar,en}.resx | 10 keys |
| api/Elmanhg.Api/appsettings.example.json | `TrainingExports` section |
| api/openapi/v1.json | regenerated by build |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | `UseSetting` both sweeps off |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `thirtySixth … _AddTrainingExports` |
| api/Elmanhg.Tests/Integration/EssayGrading/EssayGradingTestData.cs | `SeedPendingAsync(.., bool isTestMode = false)` |
| api/Elmanhg.Tests/Domain/EssayGrading/EssayGradeTests.cs | +2 tests |
| api/Elmanhg.Tests/Infrastructure/TrainingData/HmacStudentIdHasherTests.cs | +2 tests |
| api/Elmanhg.Tests/Api/FileStorage/PublicMediaMiddlewareTests.cs | +1 test method (2 keys) |
| api/Elmanhg.Tests/Infrastructure/Storage/LocalDiskFileStorageTests.cs, S3FileStorageTests.cs | +3 / +1 `DeleteAsync` tests |
| web/src/routes/admin/export.tsx | route → `TrainingExportPage` + `validateSearch` |
| web/src/app/i18n.ts | `trainingExport` namespace |
| web/src/shared/i18n/{ar,en}.json | `errors.TRAINING_EXPORT_*` (10) |
| web/orval.config.ts | `GetTrainingExports: { zod: { generate: { query: false } } }` |
| postman/elmanhg.postman_collection.json | folder `TrainingExports` (Request, List, Download) + `trainingExportId` |
| docs/training-data.md | new table, event row, `## Export` (flow, retention, line shapes, raw ids, scrubbing, limits), checklist, consumers |
| docs/essay-grading.md, docs/PRD.md (§13, §15), docs/audit-log.md, docs/claude-design-prompt.md (§4, §7), docs/prototype.md | per plan, adjusted for the gate |
| docs/deployment.md, deploy/api.env.example, docs/observability.md | config keys; private folder note; job names |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| D5/D6: signed link — `CreateTrainingExportDownloadLink*`, `TrainingExportDownloadLinkResult`, `TrainingExportDownloadTokens`, anonymous `OpenTrainingExportFile*`, `DownloadTokenHash`/`DownloadLinkExpiresAt`, `IssueDownloadLink`/`AcceptsDownloadToken`, `DownloadLinkLifetimeMinutes` | Gate condition 1 forbids an anonymous download | Dropped all of them. `GET /api/training-exports/{id}/file` is `[Authorize(Policy = TrainingDataExport)]`, handled by `DownloadTrainingExportQuery : IAuditableCommand` (`TrainingExport.Download`), and streams the stored file with `Cache-Control: private, no-store`. Domain `EnsureDownloadableAt` gives 409 `NOT_READY` / `EXPIRED`. Token tests (plan #8–13, #34–39, #49, #57–58) were replaced by `DownloadTrainingExportHandlerTests` (5) and integration `DownloadFile_*` (4) + FullFlow. The audit read exception is documented in audit-log.md. |
| Web D16: `startDownload.ts` anchor to URL; `useCreateTrainingExportDownloadLink` | No link exists anymore | `api/downloadBlob.ts` (same as `questions/api/downloadBlob`, since features cannot import each other) + `useDownloadTrainingExport` calling `http<Blob>(getDownloadTrainingExportFileUrl(id))` through the mutator, so the admin bearer token is sent. The page test asserts `Authorization: Bearer …` on the file request. |
| Scope "Out: retention" | Gate condition 3 | Added `Expired` status, `ExpiresAt` (partial index), `Complete(..., TimeSpan retention)`, `IsExpiredAt`, `Expire`, `TRAINING_EXPORT_EXPIRED` (domain), options `RetentionDays=7`, `RetentionSweepEnabled`, `RetentionSweepIntervalSeconds=3600`, `RetentionSweepBatchSize=20`, `IFileStorage.DeleteAsync` (+ Local/S3), `GetExpiredIdsAsync`, `GetExpiredTrainingExportIds`, `ExpireTrainingExport` (audited, system actor), `TrainingExportRetentionWorker`, tests for each, ApiFactory switch, web Expired badge and «متاح حتى». |
| D8 digit rule applied to every JSON string | Would mask content GUIDs (`5717-4562…` is 8 digits with a single `-`) and ISO timestamps (`sentAt`) | JSON strings that are exactly a GUID or ISO-8601 date/time are left unchanged (commented WHY). Covered by assertions in `ScrubJson_IdentifyingKeysAtAnyDepth_Removed` and `ScrubJson_NestedStringValues_Scrubbed`. |
| `toRequestTrainingExportBody` omits `subjectId` when `''` | Generated `RequestTrainingExportRequest.subjectId` is required (`string \| null`) | Sends `subjectId: null`; test says so. |
| Files to create / tests: exact list | Needed for the gate or for verification | Extra: `TrainingExportBuilder`, `TrainingExportTestData`, `TrainingExportRetentionTests` (domain), `ExpireTrainingExportHandlerTests`, `GetDue/GetExpiredTrainingExportIdsHandlerTests`, `TrainingExportRetentionWorkerTests`, `TrainingExportRetentionEndpointTests`, `TrainingRecordExportPageTests` (proves the Npgsql row-value keyset translation and the D10 NOT EXISTS SQL on real Postgres). |
| Existing code touched: exact list | Required by the gate / tooling | Also modified: `IFileStorage` + both adapters + their tests, `web/orval.config.ts` (the generated zod for the paged query does not typecheck, same fix as the 9 other paged ops), `docs/deployment.md`, `deploy/api.env.example`, `docs/observability.md` (PROGRESS config/doc conventions). |
| `Expire` on a non-Completed export | No code in plan | Reuses `TRAINING_EXPORT_NOT_READY` (409). |
| `TRAINING_EXPORT_NOT_FOUND` text «…او انتهت صلاحية الرابط» | No link anymore | «لم يتم العثور على ملف التصدير.» / "The export was not found." |
| Row-value fallback if translation fails | Translation works (integration test green) | Kept `EF.Functions.GreaterThan(ValueTuple…)`. |

## Build & test
All run in the worktree; `api/Elmanhg.Api/appsettings.json` does not exist there (CI parity without moving anything).
- `dotnet build api/Elmanhg.slnx -c Release` → `Build succeeded.` (only pre-existing core-libraries warnings).
- `dotnet test --project api/Elmanhg.Tests -c Release` → `Test run summary: Passed! total: 3904 failed: 0 succeeded: 3904 skipped: 0` (Testcontainers/Docker). New integration namespaces alone: 95/95.
- `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude core-libraries` → one WHITESPACE finding in `api/Elmanhg.Tests/Builders/SubscriptionBuilder.cs(57)`, a file this change does not touch (pre-existing mixed line endings in HEAD; local-only noise per PROGRESS). No finding in changed files.
- `npm --prefix web run gen:api` → regenerated; `npx tsc -b` → clean; `npx eslint . --max-warnings=0` → clean; `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → `All matched files use Prettier code style!`
- `npx vitest run` → `Test Files 204 passed (204) Tests 1182 passed (1182)`.
- `npm run build` → built and precompressed 196 files; `npm run perf:budget` → `entry 203/210 ok, landing 211/220 ok, lesson 232/240 ok, quiz 250/255 ok`.
- Mutation checks (each broke the named tests, then restored): `while (page.Count == batchSize)` → `>` (MultiplePages); `ExpiresAt <= now` → `<` (5 retention/expiry tests); removing the D10 `y.OccurredAt > x.OccurredAt` branch (TeacherThreadPage integration); removing `RaiseDomainEvent(EssayGradeCompleted)` (domain + integration); disabling the test-mode guard (handler test); web: removing `anchor.remove()` and the `TRAINING_EXPORT_DATE_RANGE_TOO_WIDE` server field map (2 web tests). Security mutations (private-folder check, scrubber) were verified by reading and by exact-output tests, not mutated.

## Notes for review
- Orphans: a run whose upload succeeded but whose save failed (crash, or losing the `xmin` race on another replica) leaves a private file no row points to; the retention sweep cannot see it. Documented in training-data.md, Known limits.
- `ExpireTrainingExportHandler` deletes the file before saving, so a failed save is retried next sweep (delete is idempotent). `Expire` clears `FileKey` so the audit diff shows the deletion.
- `DownloadTrainingExportQuery` is a read that implements `IAuditableCommand` on purpose; `AuditBehaviour` appends its own row, so the handler does not save.
- Nothing logs record contents or the HMAC key: the workers log only the export id and exception; the scrubber has no logging.
- Web `downloadBlob` revokes the object URL right after `click()`, matching the existing questions helper.
- New api `.cs` files were normalized to CRLF to match the checkout (`core.autocrlf=true`); git stores LF.
- The web polling (`refetchInterval` while any row is Pending) is covered through `hasPendingExports` unit tests, not a timer-driven page test.
