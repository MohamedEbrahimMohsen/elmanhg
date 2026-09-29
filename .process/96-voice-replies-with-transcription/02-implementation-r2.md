# Implementation r2 — [E9.S3] Voice replies with transcription (#96)

## Rework: blocking findings

| # | What I changed | File:line |
|---|---|---|
| 1 | Added a `.mp4` row to `Validate_ValidOggAndM4a_Passes`. Added the Theory `Validate_SignatureMismatchPerExtension_FailsTypeInvalid`: PNG bytes as `voice.webm`, `voice.ogg`, `voice.m4a` and `voice.mp4`, each expecting exactly `TEACHER_VOICE_AUDIO_TYPE_INVALID`. Added `Validate_TruncatedMp4Header_FailsTypeInvalid`, which sends 6 bytes (`00 00 00 20 66 74`) as m4a and as mp4. Mutation check results: `".ogg" => true` makes 1 test fail, `".m4a" or ".mp4" => true` makes 4 fail, and dropping `or ".mp4"` makes 1 fail. Without `header.Length >= 8`, the 6-byte case throws. | `api/Elmanhg.Tests/Application/Features/TeacherInbox/RecordVoiceDraft/RecordVoiceDraftValidatorTests.cs:22-56` |
| 2 | The transcription typed client now sets `client.Timeout = Timeout.InfiniteTimeSpan`, so the resilience pipeline owns the 150 s budget. Added the test `AddAiService_Http_TranscriptionClientTimeoutDoesNotCapConfiguredBudget`, which resolves the named typed client and asserts the timeout is infinite. See Deviations: the finding's premise does not hold for this package version. | `api/Elmanhg.Infrastructure/AiService/AiServiceServiceCollectionExtensions.cs:25-30`; `api/Elmanhg.Tests/Infrastructure/AiService/AiServiceServiceCollectionExtensionsTests.cs` |

## Rework: non-blocking notes fixed

| Note | Change | File |
|---|---|---|
| The S3 public proxy accepted `%` | `IsPublicKey` now allow-lists `[A-Za-z0-9._/-]`. This replaces the separate `\` and `~` checks. New test rows: `teacher-threads%2Fx.webm` and `teacher-threads%2fx.webm` in `Invoke_PrivateFolderAnySpelling_Returns404WithoutReading`, and `lessons/x%25.png` in `Invoke_UnsafeKey_Returns404`. Mutation check: allowing `%` makes 3 tests fail. | `api/Elmanhg.Api/FileStorage/PublicMediaMiddleware.cs:40-51`; `api/Elmanhg.Tests/Api/FileStorage/PublicMediaMiddlewareTests.cs` |
| A text/voice switch while recording uploaded the abandoned clip | On unmount the recorder is marked `discarded` and stopped. The `stop` listener releases the capture and then returns before building the blob, creating the object URL or calling `onRecorded`. `discarded` is reset when the effect mounts, which keeps StrictMode remounts working. The fake `MediaRecorder` now stops when its tracks stop, as browsers do. New test: `discards an in-progress recording when unmounted`. Mutation check: removing the guard makes it fail. | `web/src/features/askTeacher/hooks/useVoiceRecorder.ts`; `web/src/test/fakeMediaRecorder.ts`; `web/src/features/askTeacher/hooks/useVoiceRecorder.test.ts` |

## Merge of origin/main (#113 observability, #92/#214 conversation logging)

Story commit: `0ba29b3 feat(E9.S3): voice replies with transcription`. Merge commit: `3fd9594`. Not pushed.

| Conflict | Resolution |
|---|---|
| `api/openapi/v1.json`, Orval output | Both merged automatically. I regenerated them with `dotnet build` and `npm --prefix web run gen:api`, and the regenerated files match the merged ones. The ai `openapi/v1.json` was also re-exported and does not differ. |
| `AppDbContextTests` migration list | Kept every migration, in timestamp order: `..._AddTeacherThreadClaims` → `20260929162717_AddTeacherVoiceReplies` → `20260929163757_AddAvatarConversations` (thirtieth). |
| `AppDbContext.cs` | Kept both sets of constants: `MediaUrlMaxLength`, `TranscriptionModelMaxLength` and `AiIdentifierMaxLength`. |
| `Directory.Packages.props`, `ai/settings.py`, `ai/tests/unit/test_settings.py`, `deploy/ai.env.example` | Kept both sides. |
| `appsettings.example.json` | Kept main's two new `Avatar` keys and this story's S3 `FileStorage` keys. |
| `deploy/api.env.example` | Kept both sections. The "Set by compose" line keeps main's additions but lists `FileStorage__LocalRootPath, FileStorage__PublicBaseUrl` rather than `FileStorage__*`, because `FileStorage__Provider`/`S3*` are set in `api.env` (the merged compose does not set `FileStorage__Provider`). |
| `docs/deployment.md` | The compose-keys table keeps main's Observability/CoreLogging/OTEL rows and this story's FileStorage row and Provider note. The deferred table keeps this story's Object storage and Voice transcription rows and main's Observability row. |
| `docs/ai-service.md` | The config table keeps both sides. The logging section keeps both, with `service.started`, spans and metrics updated for transcription. |
| `postman` variables | Both kept (`voiceThreadId`, `voiceDraftId`, `avatarConversationId`, `adminAvatarConversationId`). |
| `ai/main.py` | Both kept. The transcription client is wrapped like the others (see below) and is closed before `telemetry.shutdown()`. |

### Observability coverage (the #113 patterns in docs/observability.md)
- **Worker:** `TeacherVoiceTranscriptionWorker` now takes `BackgroundJobMetrics`. It registers `teacher-voice-transcription` with its sweep interval, and each sweep has a `StartRun`. A failed listing calls `MarkListingFailed`, and each item calls `ItemSucceeded`/`ItemFailed`. That gives it the stale-job and items-failing alerts. New tests: `Sweep_OneTranscriptionFails_RecordsPartiallyFailedRun` and `Sweep_ListingFails_RecordsFailedRun`.
- **.NET AI client:** `HttpAiTranscriptionClient` is an `IHttpClientFactory` client, so the global HttpClient instrumentation already emits its CLIENT span and `http.client.request.duration` and propagates `traceparent`. No change was needed.
- **Python AI client:** added `MeteredTranscriptionClient` to `clients/metered.py`, applied in `lifespan`. It emits a CLIENT span `transcription <model>` and records `gen_ai.client.operation.duration` and `elmanhg.ai.cost` with operation `transcription`, plus `error.type` on failure. It records no token usage. To price by duration, `TranscriptionRequest` gained a `duration_seconds` field, which the pipeline passes through. New tests: 2 unit tests in `test_metered_clients.py` and 1 app-level test in `test_telemetry_app.py`.
- **Docs:** `docs/observability.md` now lists the new job, the `transcription` operation and span name, and the AI-dependency hint in the `BackgroundJobItemsFailing` alert row. `docs/ai-service.md` was updated as described above.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Finding 2: the transcription client keeps the default 100 s `HttpClient.Timeout`, so 150 s never applies | I checked this with a probe test. With `Microsoft.Extensions.Http.Resilience` 10.7.0, `AddStandardResilienceHandler()` already sets `HttpClient.Timeout` to infinite: a plain client gave `00:01:40`, and a client with the standard handler gave `-00:00:00.001`. The reported 100 s cut-off does not happen in this repo. | I kept the explicit `Timeout.InfiniteTimeSpan`, so the budget does not depend on a package default, and added the requested test. **The test does not fail if the explicit line is removed, because the package sets the same value.** It guards the resolved client's behaviour, not the line. |
| (merge) keep both sides | `TranscriptionRequest` needed a new required `duration_seconds` field so the metered wrapper can price transcription. | I updated the 5 construction sites: the pipeline, 3 tests and the eval. |

## Build & test
- `dotnet build` (in `api/`): 0 errors, 9 existing warnings in core-libraries.
- `dotnet test api/ -c Release`, with `appsettings.json` moved aside and then restored: exit 0, total 3315, failed 0, succeeded 3315, skipped 0.
- ai: `python -m uv sync --locked` OK. `ruff format --check`: 81 files already formatted. `ruff check`: all passed. `mypy src`: no issues in 46 files. `pytest -m "not eval"`: 196 passed, 2 deselected.
- web: `npm run typecheck` exit 0. `npm run lint` (`--max-warnings=0`) exit 0. `prettier --check --end-of-line auto .`: all files formatted. `npm test -- --run`: 173 files, 1006 tests passed.

## Notes for review
- `fakeMediaRecorder.ts` uses a `// eslint-disable-next-line @typescript-eslint/no-this-alias` so the fake can stop itself when its tracks stop. This is test code only.
- `ai/src/elmanhg_ai/clients/metered.py` is now 223 lines. It follows main's single-module pattern for the metered wrappers.
- The GenAI semantic conventions do not define a `transcription` operation name. I chose it to match the existing `chat`/`embeddings` tags, and the Grafana panels group by the label generically.
- Not addressed (non-blocking, out of this brief): the undisposed `GetObjectResponse` and the 403-without-ListBucket mapping in S3, `S3Region=auto` without a service URL, the missing `SendVoiceReplyHandlerTests` cases, and `PROGRESS.md:141`.
