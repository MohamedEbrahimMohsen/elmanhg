# Plan — [E9.S3] Voice replies with transcription (#96)

## Goal
A teacher who has claimed an Open Ask-a-Teacher thread can record a voice reply in the browser, have it stored in private object storage and transcribed to Arabic text automatically (asynchronously, with retries), correct the transcript, and send both. The student then sees an audio player with the final transcript next to it. The final transcript is the reply's text, so the Q&A stays text-only training data (PRD §12.2, §13). The same change adds the S3-compatible storage adapter (Cloudflare R2 or AWS S3, chosen by config) that #94 deferred (#207). Question photos and lesson images use the same storage abstraction. PRD §12.1 step 4 and step 5 (the audio player and text).

## Scope
**In:**
- ai/: `POST /v1/transcriptions` (JSON with base64 audio), a `TranscriptionClient` protocol, `FakeTranscriptionClient` (the default), and the real `OpenAiTranscriptionClient` (Whisper, `ELMANHG_AI_TRANSCRIPTION_PROVIDER=openai`). Retries and backoff reuse the embeddings pattern, pulled into a shared helper. Cost and latency logging. A WER scorer and an eval harness for Egyptian dialect.
- api/: the `TeacherVoiceDraft` aggregate (the async transcription job). `TeacherThread.ReplyWithVoice`. `TeacherMessage` gets `AudioUrl`, `AudioDurationSeconds` and `TranscriptFinal`. Endpoints for voice settings, record, poll and send. A background transcription worker with exponential retry. `IAiTranscriptionClient` (Fake and Http). `IFileStorage.OpenReadAsync`. The `S3FileStorage` adapter (AWSSDK.S3) selected by `FileStorage:Provider=S3`. Private media (photos and voice) is streamed through the API after the access check for both providers. On S3, public media (lesson images) goes through a guarded proxy. Migration, resx, Postman and OpenAPI.
- web/: the «نص / صوت» choice on the teacher reply card, a MediaRecorder recorder (webm/opus, ogg/opus or mp4/aac), upload, transcript polling, an editable transcript form and send. An audio player with the transcript on both thread pages (student and teacher). Orval regenerated.
- docs: `docs/ask-teacher.md`, `docs/ai-service.md`, `docs/PRD.md` §15/§18/§19, `docs/deployment.md`, `docs/claude-design-prompt.md` §4, `docs/prototype.md`, and the env examples.

**Out:**
- The follow-up, rating, closing, SLA reminders and realtime push (#97). The training-record pipeline (#109).
- Cleanup of orphaned audio (drafts that were never sent, re-recordings). This is a known limit, the same as the #94 photo limit, and is documented.
- Restoring an unsent draft after a page reload. The draft lives in React state. After a reload the teacher records again, and the old draft becomes an orphan.
- Presigned URLs. Private media is served only through the API's guarded proxy (see D6).
- Multi-instance worker leasing. There is one API instance per environment (`docs/deployment.md`).

**Deferred (the orchestrator opens issues):**
1. **The Egyptian-dialect provider evaluation run** (the sub-task "evaluate provider on Egyptian dialect"). The harness, scorer, threshold and dataset format ship here. The run needs ≥ 20 recorded teacher clips with reference transcripts, and an OpenAI key. Neither exists in this repo, and a human has to record Egyptian speech. PRD §19 Q6 stays open until the run.
2. **A live check of the Whisper adapter and the R2/S3 adapter against real services.** There are no credentials here. Both adapters are built and unit-tested at their HTTP/SDK boundary. The go-live steps are in the docs.

## Decisions
| # | Question | Decision | Why |
|---|---|---|---|
| D1 | Where does transcription run: the Python ai service or a .NET adapter? | **The Python ai service** calls OpenAI Whisper (`/v1/transcriptions`). The **.NET API owns the job**: the `TeacherVoiceDraft` row, a background worker, retries and backoff. | PRD §18 puts transcription in the AI service. `docs/deployment.md` says the API never sees AI provider keys, and `ELMANHG_AI_OPENAI_API_KEY` already lives there for embeddings (#90). Cost and latency logging, and the eval harness, belong with the other model calls. |
| D2 | How does audio reach the ai service? | JSON `{audio: base64, contentType, language, durationSeconds}`. .NET `byte[]` serializes to base64 on its own, and Python uses pydantic `Base64Bytes`. | No new dependency: `python-multipart` is not installed. It keeps the existing "JSON camelCase, extra forbidden" contract. There is no SSRF and no storage credentials in the ai service. The payload is at most 5 MB (6.7 MB base64). |
| D3 | Sync or async transcription? | Async. The record endpoint stores the audio and creates a `Pending` draft. `TeacherVoiceTranscriptionWorker` (a PeriodicTimer, like `LessonContentIndexWorker`) transcribes due drafts. A failure increments `Attempts` and schedules `NextAttemptAt = now + base × 2^(attempts-1)`, 15 s, 30 s, 60 s. After `TranscriptionMaxAttempts` (4) the draft is `Failed`. The web polls the draft every 2 s. | The story asks for an async job with retries. Transcription can take tens of seconds. |
| D4 | Retry on a failed transcription? | Automatic only (D3). There is no manual retry endpoint. A `Failed` draft can still be sent with text the teacher types, or the teacher records again. | Fewer endpoints. PRD rule 11 ("voice always transcribed") is met because the text is always non-empty: machine or typed. |
| D5 | What does `TranscriptFinal` (PRD §15 bool) mean? | `true` on every Voice message: its `Text` is the transcript the teacher reviewed. `false` on Text messages. | A voice message is only created after the teacher reviews it. #109 exports `Text` for both kinds. |
| D6 | Private media on S3: presigned or guarded? | **Guarded proxy**: `GET /api/media/teacher-threads/{file}` runs the access check, then streams from `IFileStorage.OpenReadAsync` (Local and S3 alike). The bucket stays fully private. There are no presigned URLs. | The same URL scheme and access rule as #94. No bucket CORS. Links cannot leak. The web already fetches media with the bearer token. |
| D7 | Public media (lesson images) when the provider is S3? | `PublicMediaMiddleware` proxies `GET /api/media/{key}` from storage. It refuses dot segments, backslashes, empty segments and `~`, and any first segment equal to `teacher-threads` (case-insensitive, trailing dots trimmed). It sends `Cache-Control: public, max-age=31536000, immutable` (keys are random and never rewritten). Local keeps its existing static-file mount unchanged. | One public URL scheme (`/api/media/...`) for both providers. Stored URLs never change when a host switches provider. |
| D8 | S3 client? | `AWSSDK.S3` **4.0.103.4** (Apache-2.0). `AmazonS3Config`: when `S3ServiceUrl` is set, `ServiceURL` + `AuthenticationRegion = S3Region` + `ForcePathStyle = S3ForcePathStyle`. Otherwise `RegionEndpoint = RegionEndpoint.GetBySystemName(S3Region)`. `RequestChecksumCalculation = WHEN_REQUIRED` and `ResponseChecksumValidation = WHEN_REQUIRED`. `PutObjectRequest.DisablePayloadSigning = true`. | The standard SDK, and the documented R2 settings. Morabh has only an Azure blob uploader (`Core/Core.Azure/Clients/StorageBlobClient.cs`: Azure plus managed identity), which is not reusable. |
| D9 | Audio formats | Extensions `.webm`, `.ogg`, `.m4a`, `.mp4`. The media type (before `;`) must be `audio/webm`, `audio/ogg` or `audio/mp4`. The signature must match the extension: webm `1A 45 DF A3`, ogg `4F 67 67 53`, m4a/mp4 bytes 4..7 = `ftyp`. | These are what MediaRecorder produces on Chrome/Edge/Firefox (webm or ogg opus) and Safari (mp4 AAC). All are accepted by Whisper. It mirrors the #94 magic-byte rule. |
| D10 | Limits | `AskTeacher:VoiceMaxSizeInMb` 5 and `VoiceMaxDurationSeconds` 180. The duration is reported by the client and validated as 1..max. The size is the hard cap. The recorder stops on its own at the max. | A 3-minute recording is about 2,400 Arabic characters, which fits `ReplyTextMaxLength` 4000. A 3-minute opus or AAC file is ≤ 3 MB. |
| D11 | How does the web learn the limits? | `GET /api/teacher-inbox/voice-settings` returns `{maxDurationSeconds, maxSizeInMb}`. | No duplicated magic values in the web, and no change to the existing `TeacherInboxThreadResult` or its tests. |
| D12 | A separate .NET client interface for transcription? | Yes: `IAiTranscriptionClient`, with `FakeAiTranscriptionClient` and `HttpAiTranscriptionClient`, selected by the existing `AiService:Provider`. It is a separate typed HttpClient with its own timeout, `AiService:TranscriptionTimeoutSeconds` 150. | Whisper on 3 minutes of audio plus Python retries (60 s × 2) is longer than the chat budget (45/50 s). Only the worker calls it. It keeps #91's in-flight `IAiServiceClient` untouched. |
| D13 | Python timeouts | `ELMANHG_AI_TRANSCRIPTION_TIMEOUT_SECONDS` 60, with the retries of `ELMANHG_AI_MODEL_MAX_RETRIES` (1). The worst case is about 121 s, which is under the API's 150 s. | Timeouts nest, as in `docs/ai-service.md`. |
| D14 | Whisper request | `model` = `ELMANHG_AI_TRANSCRIPTION_MODEL` (default `whisper-1`; `gpt-4o-transcribe` also works), `language=ar`, `response_format=json`, with no prompt parameter in v1. The file name comes from the content type: `voice.webm`, `voice.ogg` or `voice.m4a`. The reply has no model field, so the configured model id is returned. | This is the dev's Whisper decision. `json` works for both models. A dialect prompt is a later eval-driven change. |
| D15 | Drafts and the media check | Unsent draft audio is **never** served. Only `TeacherMessage.AudioUrl` (sent replies) passes the access check. The teacher previews their own recording from the local Blob. | Fewer paths to secure. There is no need to serve a draft. |
| D16 | Rename `CanViewTeacherThreadImageQuery` | → `CanViewTeacherThreadMediaQuery(string MediaUrl)`, which matches `ImageUrl` **or** `AudioUrl`. The existing test class is renamed, and its existing assertions are unchanged. | One access rule for all private thread media. The old name would be wrong. |
| D17 | Storage key and folder | Voice audio is stored under the existing private folder `TeacherThreadImageFormats.StorageFolder` ("teacher-threads"): `teacher-threads/{Guid:N}{ext}`. | The existing public-mount exclusion and the guarded middleware cover it with no new folder rules. |
| D18 | Admins | An admin who claimed a thread can record and send voice (the `AskTeacherReply` policy plus the claim guard), as with text replies. | PRD §16. |
| D19 | UI colour for recording | No red. The Lucide `Mic` / `Square` icons and a muted timer. | Design-system rule 1: red means wrong or destructive only. |
| D20 | Postman ordering | The AskTeacher folder gets a second "Create thread (voice)" request that sets `voiceThreadId`. The TeacherInbox folder claims that thread and runs the voice requests on it. The existing text reply keeps `threadId`. | Each thread is answered exactly once in a folder run (the #62 lesson). |
| D21 | Compose `FileStorage__Provider` | Removed from the compose `environment:` block. Compose keeps `LocalRootPath` and `PublicBaseUrl`. The baked default is `Local`, and `api.env` may set `FileStorage__Provider=S3` plus the S3 keys. | Compose `environment:` would otherwise override `api.env`. |

Product answers assumed: the teacher cannot re-open a sent voice reply. Duration is shown to the student. A failed transcription falls back to typed text. Voice is available to the claiming teacher only.

## Existing code touched
| File | Change |
|---|---|
| `ai/src/elmanhg_ai/settings.py` | Add the transcription settings and the `_openai_transcription_needs_key` validator (see ai contracts). |
| `ai/src/elmanhg_ai/main.py` | Add `transcription_client` to `create_app(...)` and to the lifespan (`injected_transcription_client or build_transcription_client(settings)`), then `aclose`. Include `transcriptions_router.router`. `service.started` adds `transcription_provider` and `transcription_model`. |
| `ai/src/elmanhg_ai/api/deps.py` | Add `transcription_client_from_app` and `TranscriptionClientDep`. |
| `ai/src/elmanhg_ai/clients/openai_embedding.py` | Use `openai_http.post_with_retries`. Import `OPENAI_BASE_URL`, `RETRY_BASE_SECONDS` and `RETRY_STATUSES` from `openai_http` so the existing test import `from ...openai_embedding import OPENAI_BASE_URL` still resolves. Behaviour is unchanged. |
| `ai/tests/conftest.py` | Add a `fake_transcription` fixture (`FakeTranscriptionClient()`). The `app` fixture passes `transcription_client=fake_transcription`. Add `transcription_payload` (a builder) and an `openai_fixture` reuse. |
| `ai/tests/unit/test_settings.py` | Add 3 tests (Test plan). No existing test changes. |
| `ai/openapi/v1.json` | Regenerate. |
| `api/Directory.Packages.props` | `<PackageVersion Include="AWSSDK.S3" Version="4.0.103.4" />` |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | `<PackageReference Include="AWSSDK.S3" />` |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.cs` | `ToMicroseconds` becomes `internal static`. |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.Replies.cs` | Add `EnsureCanReply` and `ReplyWithVoice`, plus a private `Answer`. `Reply` is refactored onto them with the same behaviour (Domain behaviour). |
| `api/Elmanhg.Domain/TeacherThreads/TeacherMessage.cs` | Add `AudioUrl`, `AudioDurationSeconds` and `TranscriptFinal`, plus `internal static CreateVoice(...)`. |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Add 3 domain codes (Error codes). |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add 7 codes under `// ASK A TEACHER`. |
| `api/Elmanhg.Application/Shared/Options/AskTeacherOptions.cs` | Add `VoiceMaxSizeInMb` `[Range(1,25)]` =5, `VoiceMaxDurationSeconds` `[Range(10,600)]` =180, `TranscriptionLanguage` `[Required, RegularExpression("^[a-z]{2}$")]` ="ar", `TranscriptionSweepEnabled` bool =true, `TranscriptionSweepIntervalSeconds` `[Range(1,3600)]` =5, `TranscriptionSweepBatchSize` `[Range(1,100)]` =5, `TranscriptionMaxAttempts` `[Range(1,10)]` =4, `TranscriptionRetryBaseDelaySeconds` `[Range(1,3600)]` =15. |
| `api/Elmanhg.Application/Shared/Storage/IFileStorage.cs` | Add `Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken);` |
| `api/Elmanhg.Application/TeacherThreads/Shared/TeacherMessageResult.cs` | Append `string? AudioUrl, int? AudioDurationSeconds`. |
| `api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadResultGenerator.cs` | `GenerateMessage` passes `message.AudioUrl, message.AudioDurationSeconds`. |
| `api/Elmanhg.Application/TeacherThreads/CanViewTeacherThreadImage/*` | **Move** to `TeacherThreads/CanViewTeacherThreadMedia/`: `CanViewTeacherThreadMediaQuery(string MediaUrl) : IRequest<bool>` and `CanViewTeacherThreadMediaHandler`. The predicate is `x.Messages.Any(m => m.ImageUrl == request.MediaUrl \|\| m.AudioUrl == request.MediaUrl)`. The rest is unchanged. Delete the two old files. |
| `api/Elmanhg.Infrastructure/Storage/FileStorageProvider.cs` | `public enum FileStorageProvider { Local, S3 }` |
| `api/Elmanhg.Infrastructure/Storage/FileStorageOptions.cs` | Add `S3ServiceUrl` (string, ""), `S3Region` (string, "auto"), `S3BucketName` (""), `S3AccessKeyId` (""), `S3SecretAccessKey` (""), `S3ForcePathStyle` (bool, true). |
| `api/Elmanhg.Infrastructure/Storage/LocalDiskFileStorage.cs` | Extract `private string ResolvePath(string key)` (the existing root check). Add `OpenReadAsync`: resolve, return `null` if `!File.Exists`, else `new StoredFile(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous), length, MediaContentTypes.FromKey(key))`. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | Replace the inline storage block (the options, `LocalDiskFileStorage` and the `IFileStorage` switch) with `services.AddFileStorage();`. Register `services.AddScoped<ITeacherVoiceDraftRepository, TeacherVoiceDraftRepository>();`. |
| `api/Elmanhg.Infrastructure/AiService/AiServiceOptions.cs` | Add `[Range(1, 600)] public int TranscriptionTimeoutSeconds { get; set; } = 150;` |
| `api/Elmanhg.Infrastructure/AiService/AiServiceServiceCollectionExtensions.cs` | Register `AddHttpClient<HttpAiTranscriptionClient>` (same BaseAddress). Add `.AddStandardResilienceHandler().Configure(...)` with `AttemptTimeout = TotalRequestTimeout = TranscriptionTimeoutSeconds`, `CircuitBreaker.SamplingDuration = 2 × TranscriptionTimeoutSeconds` and `Retry.DisableForUnsafeHttpMethods()`. Also `AddScoped<FakeAiTranscriptionClient>()` and `AddScoped<IAiTranscriptionClient>` switched on `Provider` (Fake/Http). |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add `public DbSet<TeacherVoiceDraft> TeacherVoiceDrafts { get; set; }`. In `ConfigureTeacherThreads`, `TeacherMessage` gets `builder.Property(x => x.AudioUrl).HasMaxLength(MediaUrlMaxLength);`, `builder.HasIndex(x => x.AudioUrl).HasFilter("\"AudioUrl\" IS NOT NULL");` and `builder.Property(x => x.TranscriptFinal).HasDefaultValue(false);`. Add a new `ConfigureTeacherVoiceDrafts(modelBuilder)`, called after `ConfigureTeacherThreads` (see API surface → Persistence). Add `MediaUrlMaxLength = 400` as a private const with the WHY comment "PublicBaseUrl + folder + 32-hex name + extension". Add the global filter line for `TeacherVoiceDraft`. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add AddTeacherVoiceReplies`. |
| `api/Elmanhg.Api/Controllers/TeacherInbox/TeacherInboxController.cs` | Add 4 actions (API surface). |
| `api/Elmanhg.Api/Controllers/TeacherInbox/Requests.cs` | Add `public sealed record SendVoiceReplyRequest(Guid DraftId, string? Text);` |
| `api/Elmanhg.Api/FileStorage/TeacherThreadMediaMiddleware.cs` | The ctor becomes `(RequestDelegate next, PathString mediaPath)`. `InvokeAsync(HttpContext context, ISender sender, IFileStorage fileStorage)`: the same path match (Ordinal) → `CanViewTeacherThreadMediaQuery(context.Request.Path.Value!)` → if false, empty 404 → `await using var file = await fileStorage.OpenReadAsync($"{TeacherThreadImageFormats.StorageFolder}{fileName}", ct)` → if null, 404 → `ContentType = file.ContentType`, `ContentLength = file.Length`, nosniff, `Cache-Control: private, no-store` → `await file.Content.CopyToAsync(context.Response.Body, ct)`. Remove `PhysicalFileProvider` and `FileExtensionContentTypeProvider`. |
| `api/Elmanhg.Api/FileStorage/LocalFileStorageExtensions.cs` | **Delete** (replaced by `MediaStorageExtensions.cs`). |
| `api/Elmanhg.Api/Program.cs` | `app.UseLocalFileStorage();` → `app.UseMediaStorage();`. Add `builder.Services.AddHostedService<TeacherVoiceTranscriptionWorker>();` after `LessonContentIndexWorker`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Add 10 keys (Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | `AskTeacher` gets the 8 new keys with their defaults. `AiService` gets `"TranscriptionTimeoutSeconds": 150`. `FileStorage` gets `"S3ServiceUrl": "", "S3Region": "auto", "S3BucketName": "", "S3AccessKeyId": "", "S3SecretAccessKey": "", "S3ForcePathStyle": true`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `builder.UseSetting("AskTeacher:TranscriptionSweepEnabled", "false");`, with the comment "The sweep would race tests that transcribe through the mediator." |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `twentyEighth => twentyEighth.Should().EndWith("_AddTeacherVoiceReplies")` (the accepted pattern). |
| `api/Elmanhg.Tests/Builders/TeacherThreadBuilder.cs` | Add `AnsweredByVoice(Guid teacherId, string audioUrl)`, which claims and then calls `ReplyWithVoice(teacherId, "Voice transcript.", audioUrl, 42, _submittedAt.AddHours(1))`. |
| `api/Elmanhg.Tests/Application/Features/TeacherThreads/CanViewTeacherThreadImage/CanViewTeacherThreadImageHandlerTests.cs` | **Move** to `.../CanViewTeacherThreadMedia/CanViewTeacherThreadMediaHandlerTests.cs`. Rename the class, namespace, query and handler types only; every existing test body is unchanged. Add 3 tests (Test plan). |
| `api/Elmanhg.Tests/Infrastructure/Storage/LocalDiskFileStorageTests.cs` | Add 3 tests. |
| `api/Elmanhg.Tests/Infrastructure/AiService/AiServiceServiceCollectionExtensionsTests.cs` | Add 2 tests. |
| `api/Elmanhg.Tests/Integration/TeacherThreads/TeacherThreadMediaEndpointTests.cs` | Add 3 tests. |
| `api/Elmanhg.Tests/Integration/TeacherInbox/TeacherInboxTestData.cs` | Add `public static MultipartFormDataContent VoiceForm(byte[] bytes, string fileName = "voice.webm", string contentType = "audio/webm", int durationSeconds = 12)` and `public static readonly byte[] WebmBytes = [0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0xF7, 0x81];` |
| `postman/elmanhg.postman_collection.json` | AskTeacher: add "Create thread (voice)" after "Create thread", which sets `voiceThreadId`. TeacherInbox, after "Reply to thread" and before "Get inbox (mine)": "Claim thread (voice)", "Get voice reply settings", "Record voice draft" (form-data `audio` file + `durationSeconds`, test script sets `voiceDraftId`), "Get voice draft", "Send voice reply" (`{ "draftId": "{{voiceDraftId}}", "text": "..." }`). |
| `web/src/features/askTeacher/components/ReplyForm.tsx` | Remove the outer card `div` and the `h2` (they move to `ReplyPanel`). The form content is unchanged. |
| `web/src/features/askTeacher/components/InboxThreadActions.tsx` | `canReply` → `<ReplyPanel threadId={thread.id} />`. |
| `web/src/features/askTeacher/components/ThreadMessage.tsx` | When `message.kind === 'Voice' && message.audioUrl`, render `<ThreadAudio url durationSeconds />` and then `<p class caption>{t('thread.transcriptLabel')}</p>` before the text. |
| `web/src/features/askTeacher/components/ThreadImage.tsx` | Replace the local `toDataUrl` with `blobToDataUrl(blob, 'image/')`. Behaviour is unchanged. |
| `web/src/features/askTeacher/i18n/ar.json`, `en.json` | Add the keys (Web strings). |
| `web/src/shared/i18n/ar.json`, `en.json` | `errors.*` for the 10 new codes (the resx texts). |
| `web/src/shared/api/generated/**` | `npm run gen:api`. |
| `web/src/test/teacherInboxFixtures.ts`, `web/src/test/askTeacherFixtures.ts` | Every `TeacherMessageResult` literal gets `audioUrl: null, audioDurationSeconds: null`. Add the fixtures `voiceSettings()`, `voiceDraft(status, transcript?)` and `voiceAnsweredThread()` (a student `TeacherThreadResult` whose last message is Voice with `audioUrl: '/api/media/teacher-threads/voice01.webm'`, `audioDurationSeconds: 42`). |
| `docs/ask-teacher.md`, `docs/ai-service.md`, `docs/PRD.md`, `docs/deployment.md`, `docs/claude-design-prompt.md`, `docs/prototype.md` | See Docs. |
| `.env.example`, `deploy/api.env.example`, `deploy/ai.env.example`, `deploy/docker-compose.prod.yml` | See Docs / config. |

## Files to create

### ai/ (package `elmanhg_ai`)
| # | Path | Type | Contract |
|---|---|---|---|
| A1 | `ai/src/elmanhg_ai/api/transcriptions/__init__.py` | package | empty |
| A2 | `ai/src/elmanhg_ai/api/transcriptions/schemas.py` | models | `class AudioContentType(StrEnum): WEBM="audio/webm"; OGG="audio/ogg"; MP4="audio/mp4"`. `class TranscriptionIn(ApiInModel): audio: Base64Bytes; content_type: AudioContentType; language: str = Field(default="ar", pattern=r"^[a-z]{2}$"); duration_seconds: int = Field(ge=1, le=3600)`. `class TranscriptionOut(ApiOutModel): text: str; model: str; language: str`. |
| A3 | `ai/src/elmanhg_ai/api/transcriptions/router.py` | router | `router = APIRouter(prefix="/v1", tags=["transcriptions"], dependencies=[Depends(require_service_token)])`. `@router.post("/transcriptions", responses={400,401,502,503: Problem}) async def create_transcription(payload: TranscriptionIn, settings: SettingsDep, client: TranscriptionClientDep) -> TranscriptionOut`: `result = await transcribe.run(payload, client=client, settings=settings)` → `TranscriptionOut(text=result.text, model=result.model, language=result.language)`. operationId `transcriptions_create_transcription`. |
| A4 | `ai/src/elmanhg_ai/clients/transcription.py` | protocol | `@dataclass(frozen=True, slots=True) TranscriptionRequest(audio: bytes, content_type: str, language: str)`. `TranscriptionReply(text: str, model: str)`. `class TranscriptionClient(Protocol): async def transcribe(self, request: TranscriptionRequest) -> TranscriptionReply; async def aclose(self) -> None`. `SECONDS_PER_MINUTE: Final = Decimal(60)`. `def estimate_transcription_cost_usd(duration_seconds: int, settings: Settings) -> Decimal` = `(duration_seconds * settings.transcription_usd_per_minute / SECONDS_PER_MINUTE).quantize(COST_QUANTUM)`. `def build_transcription_client(settings) -> TranscriptionClient` matches `settings.transcription_provider`: `"fake"` → `FakeTranscriptionClient()`, `"openai"` → `OpenAiTranscriptionClient.from_settings(settings)` (local imports, like `build_embedding_client`). |
| A5 | `ai/src/elmanhg_ai/clients/fake_transcription.py` | fake | `FAKE_TRANSCRIPTION_MODEL: Final = "fake-transcription"`. `FAKE_TRANSCRIPT: Final = "هذا تفريغ تجريبي للرد الصوتي."`. `class FakeTranscriptionClient: __init__(self, script: Sequence[TranscriptionReply \| Exception] = ())`, `self.requests: list[TranscriptionRequest]`. `transcribe` records the request, then pops the script (raising if it is an Exception), else returns `TranscriptionReply(FAKE_TRANSCRIPT, FAKE_TRANSCRIPTION_MODEL)`. `aclose` → None. |
| A6 | `ai/src/elmanhg_ai/clients/openai_http.py` | helper | `OPENAI_BASE_URL: Final = "https://api.openai.com/v1"`, `RETRY_BASE_SECONDS: Final = 0.5`, `RETRY_STATUSES: Final = frozenset({429, 500, 502, 503, 504})`. `async def post_with_retries(send: Callable[[], Awaitable[httpx2.Response]], *, max_retries: int, sleep: Callable[[float], Awaitable[None]], failure_event: str, model: str) -> httpx2.Response`. Its body is the loop moved from `OpenAiEmbeddingClient._post`: `TransportError` and `RETRY_STATUSES` retry with `RETRY_BASE_SECONDS * 2**attempt`; any other non-2xx breaks; on failure it logs `failure_event` with `provider="openai", model, status_code, error_type` and raises `ModelUnavailableError()`. |
| A7 | `ai/src/elmanhg_ai/clients/openai_transcription.py` | adapter | `TRANSCRIPTIONS_PATH: Final = "/audio/transcriptions"`. `FILE_NAMES: Final[Mapping[str, str]] = {"audio/webm": "voice.webm", "audio/ogg": "voice.ogg", "audio/mp4": "voice.m4a"}`. `class _OpenAiTranscription(BaseModel)` (extra ignore) `text: str`. `class OpenAiTranscriptionClient: __init__(self, http: httpx2.AsyncClient, model: str, max_retries: int, sleep=asyncio.sleep)`. `@classmethod from_settings(cls, settings)`: the key is required (`ValueError` as in embeddings); `httpx2.AsyncClient(base_url=OPENAI_BASE_URL, headers={"Authorization": f"Bearer {key}"}, timeout=httpx2.Timeout(settings.transcription_timeout_seconds))`; `cls(http, settings.transcription_model, settings.model_max_retries)`. `async def transcribe(request)`: `response = await post_with_retries(lambda: self._http.post(TRANSCRIPTIONS_PATH, files={"file": (FILE_NAMES[request.content_type], request.audio, request.content_type)}, data={"model": self._model, "language": request.language, "response_format": "json"}), max_retries=..., sleep=..., failure_event="transcription.call_failed", model=self._model)`. Parse `_OpenAiTranscription.model_validate_json(response.content)`; on `ValidationError`, log `transcription.output_invalid` (provider, model, error_type) and raise `ModelOutputInvalidError() from error`. Return `TranscriptionReply(text=body.text, model=self._model)`. `aclose` closes http. |
| A8 | `ai/src/elmanhg_ai/pipelines/transcribe.py` | pipeline | `PIPELINE_NAME: Final = "transcription"`. `@dataclass(frozen=True, slots=True) TranscriptionResult(text: str, model: str, language: str)`. `_limit_errors(payload, settings) -> list[FieldError]`: `len(audio)==0` → `FieldError("audio","TOO_SHORT","audio is empty")`; `> settings.transcription_max_audio_bytes` → `FieldError("audio","TOO_LARGE",f"at most {n} bytes")`; `duration_seconds > settings.transcription_max_duration_seconds` → `FieldError("durationSeconds","TOO_LONG",f"at most {n} seconds")`. `async def run(payload: TranscriptionIn, *, client: TranscriptionClient, settings: Settings) -> TranscriptionResult`: raise `ValidationFailedError(errors)` if there are any. Time the call `client.transcribe(TranscriptionRequest(bytes(payload.audio), payload.content_type.value, payload.language))`, then `text = reply.text.strip()`. Log `transcription.completed` with `pipeline, model, language, duration_seconds, audio_bytes, text_chars, latency_ms, cost_usd=float(estimate_transcription_cost_usd(...))`. **Never log text.** Return the result. |
| A9 | `ai/src/elmanhg_ai/eval/__init__.py` | package | empty |
| A10 | `ai/src/elmanhg_ai/eval/transcription.py` | scorer | `def normalise_arabic(text: str) -> tuple[str, ...]`: NFKC, casefold, drop tashkeel U+064B–U+0652 and tatweel U+0640, map أ/إ/آ→ا, ى→ي, ة→ه, drop `[^\w\s]`, split on whitespace. Use `chr()` code points, never literal `\u` in tool args. `def score_word_error_rate(reference: str, hypothesis: str) -> float`: the word-level Levenshtein distance / len(reference words). An empty reference raises `ValueError("reference is empty")`. `WER_THRESHOLD: Final = 0.35`. |
| A11 | `ai/tests/fixtures/openai/transcription_success.json` | fixture | `{"text": "  أهلا، خلينا نراجع قانون أوم خطوة بخطوة.  "}` |
| A12 | `ai/tests/unit/test_transcription_schemas.py` | tests | see Test plan |
| A13 | `ai/tests/unit/test_transcribe_pipeline.py` | tests | |
| A14 | `ai/tests/unit/test_fake_transcription.py` | tests | |
| A15 | `ai/tests/unit/test_openai_transcription.py` | tests | Mirrors `test_openai_embedding.py` (`MockTransport`, `RecordingSleep`). |
| A16 | `ai/tests/unit/test_transcription_scorer.py` | tests | |
| A17 | `ai/tests/integration/test_transcriptions_endpoint.py` | tests | `pytestmark = pytest.mark.integration` (as in the siblings) |
| A18 | `ai/tests/eval/test_eval_transcription.py` | eval | `pytestmark = pytest.mark.eval`. It reads `tests/fixtures/transcription/eval/manifest.jsonl` (lines of `{"audio": "clip01.webm", "contentType": "audio/webm", "durationSeconds": 12, "reference": "..."}`). It calls `pytest.skip("Egyptian-dialect clips not recorded yet (#96 deferral)")` if the file is missing. It builds `Settings(service_token=SecretStr("x"*32), transcription_provider="openai")` inside `try/except ValidationError: pytest.skip("needs ELMANHG_AI_OPENAI_API_KEY")`. It runs every clip through `OpenAiTranscriptionClient.from_settings` and asserts the mean `score_word_error_rate ≤ WER_THRESHOLD` (it prints the per-clip WER to the report through the assertion message). |

Settings added to `Settings` (all `ELMANHG_AI_*`):
`transcription_provider: Literal["fake","openai"] = "fake"`; `transcription_model: str = Field(default="whisper-1", min_length=1)`; `transcription_timeout_seconds: float = Field(default=60.0, gt=0, le=300)`; `transcription_max_audio_bytes: int = Field(default=10_485_760, ge=1, le=26_214_400)` (with the WHY comment "OpenAI's 25 MB upload cap"); `transcription_max_duration_seconds: int = Field(default=600, ge=1, le=3600)`; `transcription_usd_per_minute: Decimal = Field(default=Decimal("0.006"), ge=0)`. Validator `_openai_transcription_needs_key`: when the provider is `openai` and the key is missing or blank → `ValueError("openai_api_key is required when transcription_provider is openai")`.

### api/ — Domain (`namespace Elmanhg.Domain.TeacherThreads;`)
| # | Path | Type | Contract |
|---|---|---|---|
| D1 | `Elmanhg.Domain/TeacherThreads/TeacherVoiceDraftStatus.cs` | enum | `public enum TeacherVoiceDraftStatus { Pending, Ready, Failed, Sent }` |
| D2 | `Elmanhg.Domain/TeacherThreads/TeacherVoiceDraft.cs` | entity `: AuditEntity` | Props (private set): `Guid ThreadId`, `Guid TeacherId`, `string AudioKey`, `string AudioUrl`, `int AudioDurationSeconds`, `TeacherVoiceDraftStatus Status`, `string? Transcript`, `string? TranscriptionModel`, `int Attempts`, `DateTimeOffset? NextAttemptAt`, `DateTimeOffset RecordedAt`, `DateTimeOffset? TranscribedAt`, `Guid? SentMessageId`. `private TeacherVoiceDraft(Guid id, Guid? createdBy) : base(id, createdBy)`. Methods in Domain behaviour. |
| D3 | `Elmanhg.Domain/TeacherThreads/ITeacherVoiceDraftRepository.cs` | repo | `public interface ITeacherVoiceDraftRepository : IRepository<TeacherVoiceDraft> { Task<List<Guid>> GetDueIdsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken); }` |

### api/ — Application
| # | Path | Type | Contract |
|---|---|---|---|
| P1 | `Application/Shared/Storage/StoredFile.cs` | record | `public sealed record StoredFile(Stream Content, long Length, string ContentType) : IAsyncDisposable { public ValueTask DisposeAsync() => Content.DisposeAsync(); }` |
| P2 | `Application/Shared/Storage/MediaContentTypes.cs` | static | `public const string Fallback = "application/octet-stream";`. `public static string FromKey(string key)`, a switch on `Path.GetExtension(key).ToLowerInvariant()`: `.png`→`image/png`, `.jpg`/`.jpeg`→`image/jpeg`, `.webp`→`image/webp`, `.gif`→`image/gif`, `.webm`→`audio/webm`, `.ogg`→`audio/ogg`, `.m4a`/`.mp4`→`audio/mp4`, `_`→`Fallback`. |
| P3 | `Application/Shared/AiService/IAiTranscriptionClient.cs` | port | `Task<AiTranscriptionResult> TranscribeAsync(AiTranscriptionRequest request, CancellationToken cancellationToken);` |
| P4 | `Application/Shared/AiService/AiTranscriptionRequest.cs` | record | `public sealed record AiTranscriptionRequest(byte[] Audio, string ContentType, string Language, int DurationSeconds);` |
| P5 | `Application/Shared/AiService/AiTranscriptionResult.cs` | record | `public sealed record AiTranscriptionResult(string Text, string Model, string Language);` |
| P6 | `Application/TeacherInbox/Shared/TeacherVoiceDraftResult.cs` | result (teacher-facing) | `public sealed record TeacherVoiceDraftResult(Guid Id, Guid ThreadId, TeacherVoiceDraftStatus Status, string? Transcript, int AudioDurationSeconds, DateTimeOffset RecordedAt, DateTimeOffset? TranscribedAt);` No LocalizedText. |
| P7 | `Application/TeacherInbox/Shared/TeacherVoiceDraftResultGenerator.cs` | static | `public static TeacherVoiceDraftResult Generate(TeacherVoiceDraft draft)`, a 1:1 mapping. |
| P8 | `Application/TeacherInbox/RecordVoiceDraft/TeacherVoiceFormats.cs` | static | `public static readonly IReadOnlyList<string> Extensions = [".webm", ".ogg", ".m4a", ".mp4"];`. `public static readonly IReadOnlySet<string> MediaTypes` = {`audio/webm`,`audio/ogg`,`audio/mp4`} (OrdinalIgnoreCase). `public static bool HasAllowedMediaType(IFormFile file) => MediaTypes.Contains(file.ContentType.Split(';')[0].Trim())`. `public static bool HasMatchingSignature(IFormFile file)`: read up to 12 bytes; `.webm` → starts with `1A 45 DF A3`; `.ogg` → starts `"OggS"u8`; `.m4a`/`.mp4` → length ≥ 8 and `[4..8]` == `"ftyp"u8`; `_` → false. Mirrors `TeacherThreadImageFormats`. |
| P9 | `Application/TeacherInbox/RecordVoiceDraft/RecordVoiceDraftCommand.cs` | command | `public sealed record RecordVoiceDraftCommand(Guid ThreadId, IFormFile? Audio, int DurationSeconds) : IRequest<TeacherVoiceDraftResult>;` |
| P10 | `.../RecordVoiceDraft/RecordVoiceDraftValidator.cs` | validator | ctor `(IOptions<AskTeacherOptions> askTeacherOptions)`. `RuleFor(x => x.Audio).Cascade(Stop).ValidateRequired(TeacherVoiceAudioRequired).ValidateAllowedExtensions(TeacherVoiceFormats.Extensions, TeacherVoiceAudioTypeInvalid).ValidateMaxFileSize(options.VoiceMaxSizeInMb, TeacherVoiceAudioTooLarge)`. `RuleFor(x => x.Audio).Must(f => f is null \|\| TeacherVoiceFormats.HasAllowedMediaType(f)).WithErrorCode(TeacherVoiceAudioTypeInvalid)`. `RuleFor(x => x.Audio).Must(f => f is null \|\| f.Length == 0 \|\| TeacherVoiceFormats.HasMatchingSignature(f)).WithErrorCode(TeacherVoiceAudioTypeInvalid)`. `RuleFor(x => x.DurationSeconds).ValidateRange(1, options.VoiceMaxDurationSeconds, TeacherVoiceDurationInvalid)`. |
| P11 | `.../RecordVoiceDraft/RecordVoiceDraftHandler.cs` | handler | ctor `(ITeacherThreadRepository teacherThreadRepository, ITeacherVoiceDraftRepository teacherVoiceDraftRepository, ITeacherSubjectRepository teacherSubjectRepository, IFileStorage fileStorage, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Handle: 1) user guard → `UnauthorizedCoreException(UserNotAuthenticated)`; 2) `thread = FirstOrDefaultAsync(x => x.Id == request.ThreadId, ct, asNoTracking: true) ?? NotFound(TeacherThreadNotFound)`; 3) `TeacherInboxAccess.EnsureCanAccessAsync(thread.SubjectId, userId, GetClaim(ClaimTypes.Role), ...)`; 4) `thread.EnsureCanReply(userId)`; 5) `key = $"{TeacherThreadImageFormats.StorageFolder}/{Guid.NewGuid():N}{Path.GetExtension(audio.FileName).ToLowerInvariant()}"`, `await using var content = audio.OpenReadStream()`, `url = await fileStorage.SaveAsync(content, key, ct)`; 6) `draft = TeacherVoiceDraft.Record(thread.Id, userId, key, url, request.DurationSeconds, timeProvider.GetUtcNow())`; 7) `AddAsync`, `SaveChangesAsync` once; 8) `return TeacherVoiceDraftResultGenerator.Generate(draft)`. |
| P12 | `.../GetVoiceDraft/GetVoiceDraftQuery.cs` | query | `public sealed record GetVoiceDraftQuery(Guid ThreadId, Guid DraftId) : IRequest<TeacherVoiceDraftResult>;` |
| P13 | `.../GetVoiceDraft/GetVoiceDraftHandler.cs` | handler | ctor `(ITeacherVoiceDraftRepository, ICurrentUserService)`. 1) user guard; 2) `FirstOrDefaultAsync(x => x.Id == request.DraftId && x.ThreadId == request.ThreadId && x.TeacherId == userId, ct, asNoTracking: true) ?? NotFound(TeacherVoiceDraftNotFound)`; 3) Generate. |
| P14 | `.../SendVoiceReply/SendVoiceReplyCommand.cs` | command | `public sealed record SendVoiceReplyCommand(Guid ThreadId, Guid DraftId, string? Text) : IRequest<TeacherInboxThreadResult>;` |
| P15 | `.../SendVoiceReply/SendVoiceReplyValidator.cs` | validator | ctor `(IOptions<AskTeacherOptions>)`. `RuleFor(x => x.DraftId).ValidateRequired(TeacherVoiceDraftIdRequired)`. `RuleFor(x => x.Text).Cascade(Stop).ValidateRequired(TeacherThreadReplyTextRequired).ValidateMaxLength(options.ReplyTextMaxLength, TeacherThreadReplyTextTooLong)`. |
| P16 | `.../SendVoiceReply/SendVoiceReplyHandler.cs` | handler | ctor `(ITeacherThreadRepository, ITeacherVoiceDraftRepository, ITeacherSubjectRepository, IUserRepository, TimeProvider, ICurrentUserService)`. 1) user guard; 2) `thread` tracked with `include: q => q.Include(x => x.Messages)` ?? NotFound(TeacherThreadNotFound); 3) `TeacherInboxAccess.EnsureCanAccessAsync`; 4) `draft` tracked `x.Id == request.DraftId && x.ThreadId == thread.Id && x.TeacherId == userId` ?? NotFound(TeacherVoiceDraftNotFound); 5) `now = timeProvider.GetUtcNow()`; 6) `var message = thread.ReplyWithVoice(userId, request.Text ?? string.Empty, draft.AudioUrl, draft.AudioDurationSeconds, now)`; 7) `draft.MarkSent(message.Id, now)`; 8) `teacherThreadRepository.SaveChangesAsync` once (the shared context saves both); 9) `names = TeacherInboxNames.LoadAsync(...)`; return `TeacherInboxResultGenerator.GenerateThread(thread, names, userId, now)`. |
| P17 | `.../GetVoiceReplySettings/GetVoiceReplySettingsQuery.cs` | query | `public sealed record GetVoiceReplySettingsQuery : IRequest<VoiceReplySettingsResult>;` |
| P18 | `.../GetVoiceReplySettings/VoiceReplySettingsResult.cs` | result | `public sealed record VoiceReplySettingsResult(int MaxDurationSeconds, int MaxSizeInMb);` |
| P19 | `.../GetVoiceReplySettings/GetVoiceReplySettingsHandler.cs` | handler | ctor `(IOptions<AskTeacherOptions>)`. Returns `new(options.VoiceMaxDurationSeconds, options.VoiceMaxSizeInMb)`. No user guard: options only, and the policy authenticates. |
| P20 | `.../TranscribeVoiceDraft/TranscribeVoiceDraftCommand.cs` | command | `public sealed record TranscribeVoiceDraftCommand(Guid DraftId) : IRequest;` |
| P21 | `.../TranscribeVoiceDraft/TranscribeVoiceDraftHandler.cs` | handler | ctor `(ITeacherVoiceDraftRepository, IFileStorage, IAiTranscriptionClient, IOptions<AskTeacherOptions>, TimeProvider)`. 1) `draft` tracked by id; if `null` or `!draft.IsDueAt(timeProvider.GetUtcNow())` → return; 2) `await using var audio = await fileStorage.OpenReadAsync(draft.AudioKey, ct) ?? throw new NotFoundCoreException(ErrorCodes.TeacherVoiceAudioNotFound)`; 3) copy to a `MemoryStream` → `byte[]`; 4) `result = await transcriptionClient.TranscribeAsync(new AiTranscriptionRequest(bytes, audio.ContentType, options.TranscriptionLanguage, draft.AudioDurationSeconds), ct)`; 5) `draft.CompleteTranscription(result.Text, result.Model, timeProvider.GetUtcNow())`; 6) `SaveChangesAsync` once. No try/catch: failures propagate to the worker. |
| P22 | `.../FailVoiceDraftTranscription/FailVoiceDraftTranscriptionCommand.cs` | command | `public sealed record FailVoiceDraftTranscriptionCommand(Guid DraftId) : IRequest;` |
| P23 | `.../FailVoiceDraftTranscription/FailVoiceDraftTranscriptionHandler.cs` | handler | ctor `(ITeacherVoiceDraftRepository, IOptions<AskTeacherOptions>, TimeProvider)`. `draft` tracked; if `null` or `Status != Pending` → return; `draft.FailAttempt(now, options.TranscriptionMaxAttempts, TimeSpan.FromSeconds(options.TranscriptionRetryBaseDelaySeconds))`; `SaveChangesAsync` once. |
| P24 | `.../GetDueVoiceDraftIds/GetDueVoiceDraftIdsQuery.cs` | query | `public sealed record GetDueVoiceDraftIdsQuery : IRequest<List<Guid>>;` |
| P25 | `.../GetDueVoiceDraftIds/GetDueVoiceDraftIdsHandler.cs` | handler | ctor `(ITeacherVoiceDraftRepository, IOptions<AskTeacherOptions>, TimeProvider)`. `return await repository.GetDueIdsAsync(timeProvider.GetUtcNow(), options.TranscriptionSweepBatchSize, ct)`. |

All folders are `Application/TeacherInbox/<UseCase>/`, namespace `Elmanhg.Application.TeacherInbox.<UseCase>`. None is `IAuditableCommand` (replies are not audited, `docs/ask-teacher.md`).

### api/ — Infrastructure
| # | Path | Type | Contract |
|---|---|---|---|
| I1 | `Infrastructure/TeacherThreads/TeacherVoiceDraftRepository.cs` | repo | `public class TeacherVoiceDraftRepository(AppDbContext context) : Repository<TeacherVoiceDraft>(context), ITeacherVoiceDraftRepository`. `GetDueIdsAsync` = `_dbSet.AsNoTracking().Where(x => x.Status == TeacherVoiceDraftStatus.Pending && x.NextAttemptAt <= now).OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Select(x => x.Id).Take(limit).ToListAsync(ct)`. |
| I2 | `Infrastructure/Storage/S3FileStorage.cs` | adapter | `public sealed class S3FileStorage(IAmazonS3 s3, IOptions<FileStorageOptions> fileStorageOptions) : IFileStorage`. `SaveAsync`: `PutObjectAsync(new PutObjectRequest { BucketName, Key = key, InputStream = content, AutoCloseStream = false, ContentType = MediaContentTypes.FromKey(key), DisablePayloadSigning = true }, ct)` → `$"{PublicBaseUrl.TrimEnd('/')}/{key}"`. `OpenReadAsync`: `var response = await s3.GetObjectAsync(bucket, key, ct)` → `new StoredFile(response.ResponseStream, response.ContentLength, MediaContentTypes.FromKey(key))`. A `catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)` → `null` (the only catch, which maps a missing object). |
| I3 | `Infrastructure/Storage/FileStorageOptionsValidator.cs` | validator | `IValidateOptions<FileStorageOptions>`. When `Provider == S3`, it fails on each of: blank `S3BucketName`, `S3AccessKeyId`, `S3SecretAccessKey` or `S3Region`; `S3ServiceUrl` non-blank and not an absolute `https` URI. Messages name the key (`FileStorage:S3BucketName is required when the S3 provider is selected.`). Mirrors `AiServiceOptionsValidator`. |
| I4 | `Infrastructure/Storage/FileStorageServiceCollectionExtensions.cs` | DI | `public static IServiceCollection AddFileStorage(this IServiceCollection services)`: the options bind + `ValidateDataAnnotations().ValidateOnStart()`; `AddSingleton<IValidateOptions<FileStorageOptions>, FileStorageOptionsValidator>()`; `AddSingleton<IAmazonS3>(sp => CreateS3Client(options))` (D8 config, `BasicAWSCredentials(S3AccessKeyId, S3SecretAccessKey)`); `AddScoped<LocalDiskFileStorage>()`; `AddScoped<S3FileStorage>()`; `AddScoped<IFileStorage>` switch `Local`/`S3`/`_ => throw InvalidOperationException("Unsupported FileStorage:Provider.")`. |
| I5 | `Infrastructure/AiService/FakeAiTranscriptionClient.cs` | fake | `public sealed class FakeAiTranscriptionClient(IHostEnvironment hostEnvironment) : IAiTranscriptionClient`. `public const string FakeTranscript = "هذا تفريغ تجريبي للرد الصوتي.";` `public const string FakeModel = "fake";`. Production → `ServiceUnavailableCoreException(AiServiceUnavailable)`; else `new(FakeTranscript, FakeModel, request.Language)`. |
| I6 | `Infrastructure/AiService/HttpAiTranscriptionClient.cs` | adapter | `public sealed class HttpAiTranscriptionClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, ILogger<HttpAiTranscriptionClient> logger) : IAiTranscriptionClient`. POST `v1/transcriptions` with the same `SerializerOptions`, Bearer and User-Agent as `HttpAiServiceClient`, and the same exception mapping (transport / `ExecutionRejectedException` / timeout → `ServiceUnavailableCoreException(AiServiceUnavailable)`; non-2xx → same; `JsonException` → same). Reply validation: `null`, `Text is null` or blank `Model` → log an error and throw `AiServiceUnavailable`. An empty `Text` is valid (silence). ≤ 100 lines; copy the private `PostAsync` shape. |
| I7 | `Infrastructure/Migrations/<ts>_AddTeacherVoiceReplies.cs` (+ Designer) | migration | Generated. It must contain only: 3 `AddColumn`s on `TeacherMessages` (`AudioUrl` varchar(400) null, `AudioDurationSeconds` int null, `TranscriptFinal` bool not null default false), the filtered index on `AudioUrl`, and `CreateTable TeacherVoiceDrafts` with its indexes and FKs. No drops. |

### api/ — Api
| # | Path | Type | Contract |
|---|---|---|---|
| W1 | `Elmanhg.Api/FileStorage/MediaStorageExtensions.cs` | ext | `public static WebApplication UseMediaStorage(this WebApplication app)`: `options`; `mediaPath = new PathString(options.PublicBaseUrl)`; `app.UseMiddleware<TeacherThreadMediaMiddleware>(mediaPath)`. If `S3`: `app.UseMiddleware<PublicMediaMiddleware>(mediaPath); return app;`. Local: the existing block (create root, `PhysicalFileProvider`, `UseStaticFiles` with `PublicMediaFileProvider(files, [TeacherThreadImageFormats.StorageFolder])`, nosniff). |
| W2 | `Elmanhg.Api/FileStorage/PublicMediaMiddleware.cs` | middleware | `public sealed class PublicMediaMiddleware(RequestDelegate next, PathString mediaPath)`. `// Keys are random and never rewritten, so a public copy can be cached for a year.` `private const string PublicCacheControl = "public, max-age=31536000, immutable";`. `InvokeAsync(HttpContext context, IFileStorage fileStorage)`: if not GET or not `StartsWithSegments(mediaPath, Ordinal, out var rest)` → next. `key = rest.Value!.TrimStart('/')`; if `!IsPublicKey(key)` → empty 404. `await using var file = await fileStorage.OpenReadAsync(key, ct)`; null → 404. Set ContentType, ContentLength, nosniff and cache, then copy. `internal static bool IsPublicKey(string key)`: split on `/`; false if the key is empty, contains `\\` or `~`, has any segment that is empty, `.` or `..`, or has a first segment where `segment.TrimEnd('.')` equals `TeacherThreadImageFormats.StorageFolder` (OrdinalIgnoreCase). |
| W3 | `Elmanhg.Api/Workers/TeacherVoiceTranscriptionWorker.cs` | worker | `public sealed class TeacherVoiceTranscriptionWorker(IServiceScopeFactory scopeFactory, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider, ILogger<TeacherVoiceTranscriptionWorker> logger) : BackgroundService`. `ExecuteAsync`: return if `!TranscriptionSweepEnabled`; `PeriodicTimer(TimeSpan.FromSeconds(TranscriptionSweepIntervalSeconds), timeProvider)`; each tick `SweepAsync`. `SweepAsync`: `ids = ListDueAsync()` (scope; send `GetDueVoiceDraftIdsQuery`; `catch (Exception) when (!stopping)` → `LogError("Listing due voice drafts failed.")`, `[]`); for each id: `TranscribeAsync(id)` (scope; send `TranscribeVoiceDraftCommand`; `catch (Exception e) when (!stopping)` → `LogWarning(e, "Transcription of voice draft {DraftId} failed.", id)` then `RecordFailureAsync(id)`). `RecordFailureAsync`: scope; send `FailVoiceDraftTranscriptionCommand`; `catch (Exception e) when (!stopping)` → `LogError(e, "Recording the failed transcription of voice draft {DraftId} failed.", id)`. Mirrors `LessonContentIndexWorker` (a scope per item). |

### web/ (`web/src/features/askTeacher/`)
| # | Path | Type | Contract |
|---|---|---|---|
| F1 | `api/blobToDataUrl.ts` | util | `export async function blobToDataUrl(blob: Blob, mediaPrefix: 'image/' \| 'audio/'): Promise<string>`: throws `Error('Unexpected media type.')` if `!blob.type.startsWith(mediaPrefix)`; chunked `btoa` (the code moved from ThreadImage). |
| F2 | `api/formatDuration.ts` | util | `export function formatDuration(totalSeconds: number, lng: string): string` → `${formatNumber(minutes, lng)}:${formatNumber(seconds, lng, 'arabic-indic', { minimumIntegerDigits: 2 })}`. |
| F3 | `api/recorderMimeType.ts` | util | `export const recorderMimeTypes = ['audio/webm;codecs=opus', 'audio/ogg;codecs=opus', 'audio/mp4'] as const;` `export function pickRecorderMimeType(isSupported: (type: string) => boolean): string \| null`. `export function voiceFileName(mimeType: string): string`: `audio/ogg*` → `voice.ogg`, `audio/mp4*` → `voice.m4a`, else `voice.webm`. |
| F4 | `hooks/useVoiceRecorder.ts` | hook | `export type RecorderStatus = 'idle' \| 'requesting' \| 'recording' \| 'recorded' \| 'denied' \| 'unsupported';` `export interface VoiceRecording { blob: Blob; mimeType: string; durationSeconds: number; previewUrl: string }`. `export function useVoiceRecorder({ maxSeconds, onRecorded }: { maxSeconds: number; onRecorded: (recording: VoiceRecording) => void })` → `{ status, elapsedSeconds, recording, start(): Promise<void>, stop(): void, reset(): void }`. Status is `unsupported` when `typeof MediaRecorder === 'undefined'`, there is no `navigator.mediaDevices?.getUserMedia`, or `pickRecorderMimeType(MediaRecorder.isTypeSupported)` is null. `start`: status `requesting` → `getUserMedia({ audio: true })` (a reject → `denied`) → `new MediaRecorder(stream, { mimeType })`, collect `dataavailable` chunks, `start()`, record `Date.now()`, and `setInterval(1000)` updates `elapsedSeconds` and calls `stop()` when `elapsed >= maxSeconds`. On the `stop` event: stop every track, `blob = new Blob(chunks, { type: mimeType })`, `durationSeconds = Math.min(maxSeconds, Math.max(1, Math.round((Date.now() - startedAt) / 1000)))`, `previewUrl = URL.createObjectURL(blob)`, status `recorded`, call `onRecorded`. `reset`: revoke `previewUrl`, go back to `idle`. A `useEffect` cleanup clears the interval, stops the tracks and revokes the URL on unmount (an external-resource cleanup, not data sync). |
| F5 | `hooks/useRecordVoiceDraft.ts` | hook | Wraps the generated `useRecordTeacherVoiceDraft`. `upload(recording): Promise<TeacherVoiceDraftResult>` sends `{ threadId, data: { audio: new File([recording.blob], voiceFileName(recording.mimeType), { type: recording.mimeType }), durationSeconds: recording.durationSeconds } }`. `onError`: `toast.error(t([`errors.${error.codes[0]}`, 'errors.UNHANDLED_EXCEPTION'], { ns: 'translation' }))`, following the existing ApiError usage. Returns `{ upload, isPending }`. |
| F6 | `hooks/useVoiceDraft.ts` | hook | `export const transcriptPollIntervalMs = 2000;` `export function useVoiceDraft(threadId: string, draftId: string \| null)` → `useGetTeacherVoiceDraft(threadId, draftId ?? '', { query: { enabled: draftId !== null, refetchInterval: (query) => (query.state.data?.status === 'Pending' ? transcriptPollIntervalMs : false) } })`. |
| F7 | `hooks/useSendVoiceReply.ts` | hook | Mirrors `useReplyToThread`: generated `useSendTeacherVoiceReply`; `onSuccess` sets `getGetInboxThreadQueryKey(threadId)`, invalidates the inbox and calls `toast.success(t('inboxThread.sent'))`; `onError` 409 → invalidates the thread. `submit(draftId, values)` → `mutateAsync({ threadId, data: { draftId, text: values.text } })`. |
| F8 | `schemas/voiceTranscriptFormSchema.ts` | schema | `z.object({ text: z.string().trim().min(1, { error: 'askTeacher:voice.transcriptRequired' }) })`; `export interface VoiceTranscriptFormValues { text: string }`. |
| F9 | `components/ReplyPanel.tsx` | component | Card (`rounded-lg border border-border bg-surface p-4 shadow-1`) → `h2` `t('inboxThread.replyTitle')` → `<fieldset>` with `<legend class sr-only>{t('replyKind.label')}</legend>` and two native radios «نص» (default) / «صوت» (`useState<'text' \| 'voice'>`) → `ReplyForm` or `VoiceReplyPanel`. |
| F10 | `components/VoiceReplyPanel.tsx` | component | ≤ 120 lines. `settings = useGetVoiceReplySettings({ query: { staleTime: Infinity } })`; pending → `ContentListSkeleton`; error → `ContentErrorState` with retry. `[draftId, setDraftId] = useState<string \| null>(null)`. `recorder = useVoiceRecorder({ maxSeconds, onRecorded: (r) => void upload(r).then((d) => setDraftId(d.id)) })`. `draft = useVoiceDraft(threadId, draftId)`. It renders `VoiceRecorderControls` (passing `recorder`, `isUploading`, and `onReRecord = () => { recorder.reset(); setDraftId(null); }`), then: `isUploading` → `<p role="status">{t('voice.uploading')}</p>`; draft `Pending` → `<p role="status" aria-live="polite">{t('voice.transcribing')}</p>`; `Ready`/`Failed` → (Failed: `<p class text-caption text-text-muted>{t('voice.transcriptionFailed')}</p>`) `<VoiceTranscriptForm key={draft.id} threadId draftId={draft.id} defaultText={draft.transcript ?? ''} />`. |
| F11 | `components/VoiceRecorderControls.tsx` | component | Props `{ recorder: ReturnType<typeof useVoiceRecorder>; maxSeconds: number; isUploading: boolean; onReRecord: () => void }`. `unsupported` → note `voice.unsupported`. `denied` → note `voice.denied` + the Record button. `idle`/`denied` → a secondary Button with Lucide `Mic` + `voice.record` and the caption `voice.limit` ({max} = `formatDuration`). `recording` → `<p role="timer" aria-live="off">{t('voice.recording', { elapsed, max })}</p>` and a secondary Button with Lucide `Square` + `voice.stop`. `recorded` → `<audio controls src={recording.previewUrl} aria-label={t('voice.preview')} class w-full />` and a ghost Button `voice.reRecord` (disabled while uploading). Buttons have `min-h-11`. |
| F12 | `components/VoiceTranscriptForm.tsx` | component | RHF + `zodResolver(voiceTranscriptFormSchema)`, `defaultValues: { text: defaultText }`. `Form` with `serverErrorFields={{ TEACHER_THREAD_REPLY_TEXT_REQUIRED: 'text', TEACHER_THREAD_REPLY_TEXT_TOO_LONG: 'text' }}`. `TextAreaField name="text" label={t('voice.transcriptLabel')} description={t('voice.transcriptHint')}`, `FormRootError`, `SubmitButton` `t('inboxThread.send')`. On submit `useSendVoiceReply(threadId).submit(draftId, values)`. |
| F13 | `components/ThreadAudio.tsx` | component | Props `{ url: string; durationSeconds: number \| null }`. `useQuery({ queryKey: ['askTeacher', 'audio', url], queryFn: async ({ signal }) => blobToDataUrl(await http<Blob>(url, { signal }), 'audio/'), staleTime: Infinity })`. pending → `div role="status" aria-busy aria-label={t('thread.audioLoading')}` (h-11, bg-soft, rounded-sm); error → `text-caption text-danger` `thread.audioError`; success → `<audio controls preload="metadata" src={data} aria-label={t('thread.voiceLabel')} className="w-full" />` + (duration) `<p class text-caption text-text-muted>{t('thread.duration', { duration: formatDuration(d, lng) })}</p>`. |
| F14 | `web/src/test/fakeMediaRecorder.ts` | test util | `export function installFakeMediaRecorder(options?: { supported?: string[]; permission?: 'granted' \| 'denied' }): { stopTrack: Mock; uninstall(): void }`. It defines `globalThis.MediaRecorder` (a class with `state`, `mimeType`, `start()`, `stop()` which dispatches `dataavailable` with `new Blob([new Uint8Array([0x1a,0x45,0xdf,0xa3])], { type })` and then `stop`, plus static `isTypeSupported`), `navigator.mediaDevices.getUserMedia` (resolves `{ getTracks: () => [{ stop: stopTrack }] }` or rejects `new DOMException('denied', 'NotAllowedError')`), and `URL.createObjectURL`/`revokeObjectURL` stubs (`'blob:voice'`). `uninstall` restores the originals. |
| F15–F24 | tests | tests | `api/blobToDataUrl.test.ts`, `api/formatDuration.test.ts`, `api/recorderMimeType.test.ts`, `schemas/voiceTranscriptFormSchema.test.ts`, `hooks/useVoiceRecorder.test.ts`, `pages/InboxThreadPage.voice.test.tsx`, `pages/TeacherThreadPage.voice.test.tsx`. |

## Error codes
| Constant | Value | Class | Thrown by | Exception | HTTP |
|---|---|---|---|---|---|
| `TeacherVoiceAudioRequired` | `TEACHER_VOICE_AUDIO_REQUIRED` | Application | RecordVoiceDraftValidator | validation | 422 |
| `TeacherVoiceAudioTypeInvalid` | `TEACHER_VOICE_AUDIO_TYPE_INVALID` | Application | RecordVoiceDraftValidator (ext, media type, signature) | validation | 422 |
| `TeacherVoiceAudioTooLarge` | `TEACHER_VOICE_AUDIO_TOO_LARGE` | Application | RecordVoiceDraftValidator | validation | 422 |
| `TeacherVoiceDurationInvalid` | `TEACHER_VOICE_DURATION_INVALID` | Application | RecordVoiceDraftValidator | validation | 422 |
| `TeacherVoiceDraftIdRequired` | `TEACHER_VOICE_DRAFT_ID_REQUIRED` | Application | SendVoiceReplyValidator | validation | 422 |
| `TeacherVoiceDraftNotFound` | `TEACHER_VOICE_DRAFT_NOT_FOUND` | Application | GetVoiceDraftHandler, SendVoiceReplyHandler | NotFoundCoreException | 404 |
| `TeacherVoiceAudioNotFound` | `TEACHER_VOICE_AUDIO_NOT_FOUND` | Application | TranscribeVoiceDraftHandler (worker only) | NotFoundCoreException | (logged) |
| `TeacherVoiceDraftNotReady` | `TEACHER_VOICE_DRAFT_NOT_READY` | Domain | `TeacherVoiceDraft.MarkSent` (Pending) | ConflictCoreException | 409 |
| `TeacherVoiceDraftAlreadySent` | `TEACHER_VOICE_DRAFT_ALREADY_SENT` | Domain | `TeacherVoiceDraft.MarkSent` (Sent) | ConflictCoreException | 409 |
| `TeacherVoiceDraftNotPending` | `TEACHER_VOICE_DRAFT_NOT_PENDING` | Domain | `CompleteTranscription` / `FailAttempt` | ConflictCoreException | (worker) |

Existing codes reused: `UserNotAuthenticated`, `TeacherThreadNotFound`, `SubjectOutOfScope`, `TeacherThreadNotClaimed`, `TeacherThreadAlreadyClaimed`, `TeacherThreadNotAwaitingReply`, `TeacherMessageTextRequired`, `TeacherThreadReplyTextRequired`/`TooLong` and `AiServiceUnavailable`.

The domain throws `ConflictCoreException` directly, as the existing `TeacherThread.Replies.cs` does.

Resx (the Arabic has no tashkeel). The same strings go into the web `errors.*`:
| Key | ar | en |
|---|---|---|
| TEACHER_VOICE_AUDIO_REQUIRED | أرفق التسجيل الصوتي. | Attach the voice recording. |
| TEACHER_VOICE_AUDIO_TYPE_INVALID | التسجيل يجب أن يكون WEBM أو OGG أو M4A. | The recording must be WEBM, OGG or M4A. |
| TEACHER_VOICE_AUDIO_TOO_LARGE | حجم التسجيل أكبر من المسموح. | The recording is larger than allowed. |
| TEACHER_VOICE_DURATION_INVALID | مدة التسجيل غير صالحة. | The recording length is not valid. |
| TEACHER_VOICE_DRAFT_ID_REQUIRED | التسجيل مطلوب. | The recording is required. |
| TEACHER_VOICE_DRAFT_NOT_FOUND | التسجيل غير موجود. | The recording was not found. |
| TEACHER_VOICE_AUDIO_NOT_FOUND | ملف التسجيل غير موجود. | The recording file was not found. |
| TEACHER_VOICE_DRAFT_NOT_READY | لم ينته تفريغ التسجيل بعد. | The recording is still being transcribed. |
| TEACHER_VOICE_DRAFT_ALREADY_SENT | أرسل هذا التسجيل من قبل. | This recording was already sent. |
| TEACHER_VOICE_DRAFT_NOT_PENDING | لا ينتظر هذا التسجيل التفريغ. | This recording is not waiting for transcription. |

## Domain behaviour
```csharp
// TeacherThread.Replies.cs
public void EnsureCanReply(Guid teacherId)
{
    EnsureClaimedBy(teacherId);
    if (Status != TeacherThreadStatus.Open)
    {
        throw new ConflictCoreException(ErrorCodes.TeacherThreadNotAwaitingReply);
    }
}

public TeacherMessage Reply(Guid teacherId, string text, DateTimeOffset repliedAt)
{
    EnsureCanReply(teacherId);
    var at = ToMicroseconds(repliedAt);
    return Answer(teacherId, TeacherMessage.CreateText(Id, teacherId, text, null, at), at);
}

public TeacherMessage ReplyWithVoice(Guid teacherId, string text, string audioUrl, int audioDurationSeconds, DateTimeOffset repliedAt)
{
    EnsureCanReply(teacherId);
    var at = ToMicroseconds(repliedAt);
    return Answer(teacherId, TeacherMessage.CreateVoice(Id, teacherId, text, audioUrl, audioDurationSeconds, at), at);
}

private TeacherMessage Answer(Guid teacherId, TeacherMessage message, DateTimeOffset at)
{
    Messages.Add(message);
    Status = TeacherThreadStatus.Answered;
    UpdatedBy = teacherId;
    UpdationDate = at;
    return message;
}
```
`TeacherMessage.CreateVoice(Guid threadId, Guid senderId, string text, string audioUrl, int audioDurationSeconds, DateTimeOffset createdAt)` (internal): a blank text → `BusinessRuleViolationCoreException(TeacherMessageTextRequired)` (the same as `CreateText`); `ArgumentException.ThrowIfNullOrWhiteSpace(audioUrl)`; `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(audioDurationSeconds)`. It sets `Kind = Voice`, `Text = text.Trim()`, `AudioUrl`, `AudioDurationSeconds`, `TranscriptFinal = true` and `CreatedAt`. `CreateText` leaves `TranscriptFinal = false` and `AudioUrl = null`.

`TeacherVoiceDraft`:
```csharp
public static TeacherVoiceDraft Record(Guid threadId, Guid teacherId, string audioKey, string audioUrl, int audioDurationSeconds, DateTimeOffset recordedAt)
{
    var at = TeacherThread.ToMicroseconds(recordedAt);
    return new TeacherVoiceDraft(Guid.NewGuid(), teacherId)
    {
        ThreadId = threadId, TeacherId = teacherId, AudioKey = audioKey, AudioUrl = audioUrl,
        AudioDurationSeconds = audioDurationSeconds, Status = TeacherVoiceDraftStatus.Pending,
        Attempts = 0, NextAttemptAt = at, RecordedAt = at,
    };
}

public bool IsDueAt(DateTimeOffset now) => Status == TeacherVoiceDraftStatus.Pending && NextAttemptAt <= now;

public void CompleteTranscription(string transcript, string model, DateTimeOffset transcribedAt)
{
    EnsurePending();
    var at = TeacherThread.ToMicroseconds(transcribedAt);
    Transcript = transcript.Trim();
    TranscriptionModel = model;
    Attempts++;
    NextAttemptAt = null;
    TranscribedAt = at;
    Status = TeacherVoiceDraftStatus.Ready;
    UpdationDate = at;
}

public void FailAttempt(DateTimeOffset failedAt, int maxAttempts, TimeSpan retryBaseDelay)
{
    EnsurePending();
    var at = TeacherThread.ToMicroseconds(failedAt);
    Attempts++;
    if (Attempts >= maxAttempts)
    {
        Status = TeacherVoiceDraftStatus.Failed;
        NextAttemptAt = null;
    }
    else
    {
        NextAttemptAt = at + retryBaseDelay * Math.Pow(2, Attempts - 1);
    }

    UpdationDate = at;
}

public void MarkSent(Guid messageId, DateTimeOffset sentAt)
{
    if (Status == TeacherVoiceDraftStatus.Sent)
    {
        throw new ConflictCoreException(ErrorCodes.TeacherVoiceDraftAlreadySent);
    }

    if (Status == TeacherVoiceDraftStatus.Pending)
    {
        throw new ConflictCoreException(ErrorCodes.TeacherVoiceDraftNotReady);
    }

    SentMessageId = messageId;
    Status = TeacherVoiceDraftStatus.Sent;
    UpdatedBy = TeacherId;
    UpdationDate = TeacherThread.ToMicroseconds(sentAt);
}

private void EnsurePending()
{
    if (Status != TeacherVoiceDraftStatus.Pending)
    {
        throw new ConflictCoreException(ErrorCodes.TeacherVoiceDraftNotPending);
    }
}
```

### Persistence (`ConfigureTeacherVoiceDrafts`)
```csharp
modelBuilder.Entity<TeacherVoiceDraft>(builder =>
{
    builder.Property(x => x.AudioKey).IsRequired().HasMaxLength(MediaUrlMaxLength);
    builder.Property(x => x.AudioUrl).IsRequired().HasMaxLength(MediaUrlMaxLength);
    builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
    builder.Property(x => x.TranscriptionModel).HasMaxLength(100);   // WHY comment: provider model ids are short
    builder.HasOne<TeacherThread>().WithMany().HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<User>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => new { x.ThreadId, x.TeacherId });
    builder.HasIndex(x => x.NextAttemptAt).HasFilter("\"Status\" = 'Pending'");
});
```
`TranscriptionModel` max: add a `private const int TranscriptionModelMaxLength = 100;` with the WHY comment.

## API surface
All four are on `TeacherInboxController` (`api/teacher-inbox`), with policy `DefaultCodes.AskTeacherReply` (a Teacher or Admin; the subject scope is checked in the handler). No new policy.

| Method | Route | Name | Request | Response | Errors |
|---|---|---|---|---|---|
| GET | `/api/teacher-inbox/voice-settings` | `GetVoiceReplySettings` | — | `VoiceReplySettingsResult` | 401/403 |
| POST | `/api/teacher-inbox/{threadId:guid}/voice-drafts` | `RecordTeacherVoiceDraft` | multipart: `IFormFile? audio`, `[FromForm] int durationSeconds`; `[Consumes("multipart/form-data")]` | `TeacherVoiceDraftResult` | 422 `TEACHER_VOICE_AUDIO_*` / `DURATION_INVALID`; 404 `TEACHER_THREAD_NOT_FOUND`; 403 `SUBJECT_OUT_OF_SCOPE`; 409 `TEACHER_THREAD_NOT_CLAIMED` / `ALREADY_CLAIMED` / `NOT_AWAITING_REPLY` |
| GET | `/api/teacher-inbox/{threadId:guid}/voice-drafts/{draftId:guid}` | `GetTeacherVoiceDraft` | — | `TeacherVoiceDraftResult` | 404 `TEACHER_VOICE_DRAFT_NOT_FOUND` (also another teacher's draft) |
| POST | `/api/teacher-inbox/{threadId:guid}/voice-replies` | `SendTeacherVoiceReply` | `[FromBody] SendVoiceReplyRequest(Guid DraftId, string? Text)` | `TeacherInboxThreadResult` | 422 `TEACHER_VOICE_DRAFT_ID_REQUIRED` / `TEACHER_THREAD_REPLY_TEXT_*`; 404 thread / draft; 403 scope; 409 claim/status codes, `TEACHER_VOICE_DRAFT_NOT_READY` / `ALREADY_SENT` / `TEACHER_THREAD_MODIFIED_CONCURRENTLY` |
| GET | `/api/media/teacher-threads/{file}` (existing) | — | — | photo **or audio** bytes | empty 404 unless owner / scoped teacher / admin; unsent draft audio is always 404 |

Every action carries `[ProducesResponseType<T>(200)]` and `CancellationToken` passed to `Send`. `TeacherMessageResult` (student and teacher thread results) gains `audioUrl`, `audioDurationSeconds`.

ai service: `POST /v1/transcriptions` (Bearer service token). Request `{ "audio": "<base64>", "contentType": "audio/webm", "language": "ar", "durationSeconds": 42 }`; response `{ "text": "...", "model": "whisper-1", "language": "ar" }`. Errors: 400 `VALIDATION_FAILED` (`audio` TOO_SHORT/TOO_LARGE, `durationSeconds` TOO_LONG, schema), 401, 502 `MODEL_OUTPUT_INVALID`, 503 `DEPENDENCY_UNAVAILABLE`.

## Web strings (`askTeacher` namespace, ar / en)
| Key | ar | en |
|---|---|---|
| replyKind.label | نوع الرد | Reply type |
| replyKind.text | نص | Text |
| replyKind.voice | صوت | Voice |
| voice.record | تسجيل | Record |
| voice.stop | إيقاف | Stop |
| voice.reRecord | إعادة التسجيل | Record again |
| voice.limit | الحد الأقصى للتسجيل {max} | Maximum length {max} |
| voice.recording | جارٍ التسجيل · {elapsed} / {max} | Recording · {elapsed} / {max} |
| voice.preview | معاينة التسجيل | Recording preview |
| voice.uploading | جارٍ رفع التسجيل… | Uploading the recording… |
| voice.transcribing | جارٍ تفريغ التسجيل… | Transcribing the recording… |
| voice.transcriptLabel | نص التفريغ الصوتي (يمكنك تصحيحه قبل الإرسال) | Voice transcript (you can correct it before sending) |
| voice.transcriptHint | راجع النص قبل الإرسال | Check the text before sending |
| voice.transcriptRequired | اكتب نص الرد | Write the reply text |
| voice.transcriptionFailed | تعذّر التفريغ التلقائي. اكتب نص الرد يدويًا. | Automatic transcription failed. Type the reply text yourself. |
| voice.denied | لم يُسمح بالوصول إلى الميكروفون. | Microphone access was not allowed. |
| voice.unsupported | المتصفح لا يدعم التسجيل الصوتي. | This browser cannot record audio. |
| thread.voiceLabel | الرد الصوتي | Voice reply |
| thread.transcriptLabel | نص التفريغ الصوتي: | Voice transcript: |
| thread.audioLoading | جارٍ تحميل التسجيل… | Loading the recording… |
| thread.audioError | تعذّر تحميل التسجيل | Could not load the recording |
| thread.duration | المدة: {duration} | Length: {duration} |

## Docs / config
| File | Change |
|---|---|
| `docs/ask-teacher.md` | Intro: "Voice arrives with #96" → voice replies are in. Model: add `AudioUrl`, `AudioDurationSeconds` and `TranscriptFinal` to TeacherMessage (migration `AddTeacherVoiceReplies`). Add a **TeacherVoiceDraft** table (fields, statuses Pending→Ready/Failed→Sent, indexes). Add a new section **Voice replies**: formats and signatures, limits (options), the flow (record → store under `teacher-threads/` → Pending → worker → Ready/Failed → teacher corrects → send), the retry schedule (15/30/60 s, 4 attempts), failure → typed text, `TranscriptFinal` meaning, only sent audio served, and no draft restore after reload. **Image** section → rename to **Private media** and add audio. Serving goes through `IFileStorage` for Local and S3, and there are no presigned URLs. API tables: add the 4 endpoints. Options: add the 8 keys. Web: the reply card with «نص / صوت», the recorder states and strings, the student player + «نص التفريغ الصوتي:». Delete the "#96" bullet from "For later stories". Known limits: orphan audio (drafts not sent, re-recordings), the client-reported duration, a single worker instance. |
| `docs/ai-service.md` | Role: add transcription. Contract: `POST /v1/transcriptions` (operationId, request/response, field rules, limits table). Errors: note the transcription failures in the `DEPENDENCY_UNAVAILABLE` / `MODEL_OUTPUT_INVALID` rows. Config table: the 6 `ELMANHG_AI_TRANSCRIPTION_*` vars, and a .NET `AiService:TranscriptionTimeoutSeconds` 150 row plus the nesting note (60 s × 2 < 150 s). Fakes: `FakeTranscriptionClient` / `FakeAiTranscriptionClient` text. Logging: the `transcription.completed` fields and `transcription.call_failed` / `output_invalid`, text never logged. New section **Evaluate transcription (Egyptian dialect)**: the dataset path and manifest format, `uv run pytest -m eval tests/eval/test_eval_transcription.py`, the threshold of mean WER ≤ 0.35 with the normalisation, and that it is pending recordings (PRD §19 Q6). New section **Go live with Whisper**. |
| `docs/PRD.md` | §15: `TeacherMessage(... audio_url?, audio_duration_seconds?, transcript_final bool, ...)` and a new line `TeacherVoiceDraft(id, thread_id, teacher_id, audio_key, audio_url, audio_duration_seconds, status[Pending\|Ready\|Failed\|Sent], transcript?, transcription_model?, attempts, next_attempt_at?, recorded_at, transcribed_at?, sent_message_id?)  -- transcription job; docs/ask-teacher.md`. §18 AI bullet: "transcription orchestration (v1)" → "speech-to-text through OpenAI Whisper (v1; the API's background worker schedules and retries it)". Media bullet: add "private media (question photos, voice replies) is served only through the API after an access check". §19 Q6: append "The evaluation harness is in `docs/ai-service.md`; the run waits for recorded Egyptian-dialect clips." |
| `docs/deployment.md` | Line 17: object storage from #96 is **available**: `FileStorage__Provider=S3` plus the keys in `api.env`, with Local as the default. The compose-set table (line 202): only `LocalRootPath` / `PublicBaseUrl`. A new subsection **Object storage (R2 / S3)**: the keys table (`S3ServiceUrl` e.g. `https://<account>.r2.cloudflarestorage.com`, `S3Region` `auto` for R2, bucket, access key, secret, `S3ForcePathStyle`); the bucket stays private with no public access; switching from Local means copying `App_Data/media` into the bucket with the same keys (for example `aws s3 sync` / `rclone copy`) before restarting, because URLs do not change; enable bucket versioning where the provider supports it. Line 279: object data lives with the provider, outside `backup.sh`. The ai.env table: the `ELMANHG_AI_TRANSCRIPTION_*` rows. The Ask-a-Teacher paragraph (line 170): voice replies too. |
| `docs/claude-design-prompt.md` | §4 line 146: replace "(voice and its editable transcript arrive with the voice story)" with the reply card «الرد» with «نص / صوت»; voice: «تسجيل» / «إيقاف» with a timer and maximum length, a local preview and «إعادة التسجيل», «جارٍ تفريغ التسجيل…», then the editable «نص التفريغ الصوتي (يمكنك تصحيحه قبل الإرسال)» and «إرسال الرد»; a failed transcription asks the teacher to type the text. Line 139: the student thread shows a voice reply as an audio player with its length and «نص التفريغ الصوتي:» and the text; the audio is private like the photo. |
| `docs/prototype.md` | Item 9: append "The product records the voice note in the browser, uploads it, transcribes it automatically in the background, and lets the teacher correct the transcript before sending." Item 7: append "The product plays the voice reply from private storage with its transcript beneath." |
| `.env.example` | Add commented `# FileStorage__Provider=S3` / `# FileStorage__S3ServiceUrl=` / `# FileStorage__S3Region=auto` / `# FileStorage__S3BucketName=` / `# FileStorage__S3AccessKeyId=` / `# FileStorage__S3SecretAccessKey=`, and `# ELMANHG_AI_TRANSCRIPTION_PROVIDER=openai` / `# ELMANHG_AI_TRANSCRIPTION_MODEL=whisper-1`, and `# AskTeacher__VoiceMaxDurationSeconds=180`. |
| `deploy/api.env.example` | An Ask-a-Teacher block with the voice keys (commented); a new **Object storage** block with the S3 keys (commented, secrets empty). Last line: `FileStorage__*` → `FileStorage__LocalRootPath, FileStorage__PublicBaseUrl`. |
| `deploy/ai.env.example` | `ELMANHG_AI_TRANSCRIPTION_PROVIDER=fake` plus the commented model / timeout / limit / price keys. |
| `deploy/docker-compose.prod.yml` | Delete the `FileStorage__Provider: Local` line (D21). |

## Test plan

### ai/ (pytest; names follow `test_<unit>_<scenario>_<expected>`)
| # | File | Test | Asserts |
|---|---|---|---|
| 1 | unit/test_transcription_schemas.py | `test_transcription_in_valid_payload_decodes_audio` | `audio` bytes equal the original, and `content_type`/`duration_seconds` are parsed from camelCase |
| 2 | 〃 | `test_transcription_in_invalid_base64_raises_at_audio` | ValidationError `loc == ("audio",)` |
| 3 | 〃 | `test_transcription_in_unknown_content_type_raises_at_content_type` | loc `("contentType",)` for `audio/mpeg` |
| 4 | 〃 | `test_transcription_in_duration_zero_raises_at_duration_seconds` | loc `("durationSeconds",)` |
| 5 | 〃 | `test_transcription_in_bad_language_raises_at_language` | `"arabic"` → loc `("language",)` |
| 6 | 〃 | `test_transcription_in_extra_field_raises_forbidden` | type `extra_forbidden` |
| 7 | unit/test_transcribe_pipeline.py | `test_transcribe_run_success_returns_trimmed_text_and_logs_completion` | result text stripped, model and language; one `transcription.completed` log with `pipeline="transcription"`, `duration_seconds`, `audio_bytes`, `text_chars`, `cost_usd`, and **no** key containing the text |
| 8 | 〃 | `test_transcribe_run_empty_audio_raises_validation_failed` | `ValidationFailedError`, errors `[("audio","TOO_SHORT")]`, client not called |
| 9 | 〃 | `test_transcribe_run_audio_too_large_raises_validation_failed` | `("audio","TOO_LARGE")` with `transcription_max_audio_bytes=4` |
| 10 | 〃 | `test_transcribe_run_duration_too_long_raises_validation_failed` | `("durationSeconds","TOO_LONG")` |
| 11 | 〃 | `test_transcribe_run_client_unavailable_propagates` | FakeTranscriptionClient scripted `ModelUnavailableError` → raised |
| 12 | 〃 | `test_estimate_transcription_cost_usd_uses_per_minute_price` | 90 s at 0.006 → `Decimal("0.009000")` |
| 13 | unit/test_fake_transcription.py | `test_fake_transcription_client_returns_fixed_arabic_text` | the text is `FAKE_TRANSCRIPT`, the model is `fake-transcription`, and the request is recorded |
| 14 | 〃 | `test_fake_transcription_client_script_returns_and_raises_in_order` | the scripted reply, then the scripted error |
| 15 | unit/test_openai_transcription.py | `test_openai_transcribe_sends_multipart_file_model_language_and_bearer` | captured request: POST `/v1/audio/transcriptions`, `Authorization: Bearer test-key`, multipart body contains `filename="voice.webm"`, `Content-Type: audio/webm`, the parts `model=whisper-1`, `language=ar`, `response_format=json` |
| 16 | 〃 | `test_openai_transcribe_mp4_uses_m4a_file_name` | `filename="voice.m4a"` |
| 17 | 〃 | `test_openai_transcribe_success_maps_text_and_configured_model` | fixture `transcription_success.json` → text as returned, model `whisper-1` |
| 18 | 〃 | `test_openai_transcribe_rate_limited_then_success_retries_once` | 2 requests, sleeps `[0.5]` |
| 19 | 〃 | `test_openai_transcribe_server_error_exhausts_retries_raises_model_unavailable` | 3 requests, sleeps `[0.5, 1.0]`, `code == DEPENDENCY_UNAVAILABLE` |
| 20 | 〃 | `test_openai_transcribe_bad_request_raises_without_retry` | 1 request, `ModelUnavailableError` |
| 21 | 〃 | `test_openai_transcribe_transport_error_raises_model_unavailable` | `ConnectError` → `ModelUnavailableError` |
| 22 | 〃 | `test_openai_transcribe_malformed_body_raises_model_output_invalid` | `{}` → `ModelOutputInvalidError` |
| 23 | 〃 | `test_build_transcription_client_selects_provider` | parametrize `fake`/`openai` (with key) → `FakeTranscriptionClient`/`OpenAiTranscriptionClient` |
| 24 | unit/test_transcription_scorer.py | `test_score_word_error_rate_identical_after_normalisation_is_zero` | tashkeel, alef variants, ى/ي and ة/ه differences → 0.0 |
| 25 | 〃 | `test_score_word_error_rate_one_substitution_in_four_words_is_quarter` | 0.25 |
| 26 | 〃 | `test_score_word_error_rate_counts_insertions_and_deletions` | ref 2 words, hyp 3 words (1 inserted) → 0.5; ref 2, hyp 1 → 0.5 |
| 27 | 〃 | `test_score_word_error_rate_empty_reference_raises_value_error` | `ValueError` |
| 28 | unit/test_settings.py | `test_settings_defaults_select_fake_transcription_whisper` | provider `fake`, model `whisper-1`, timeout 60, max bytes 10485760, max duration 600 |
| 29 | 〃 | `test_settings_openai_transcription_without_api_key_raises_validation_error` | ValidationError |
| 30 | 〃 | `test_settings_transcription_max_audio_bytes_above_25mb_raises_validation_error` | 26_214_401 → ValidationError |
| 31 | integration/test_transcriptions_endpoint.py | `test_transcriptions_valid_request_returns_text` | 200, `{"text": FAKE_TRANSCRIPT, "model": "fake-transcription", "language": "ar"}` |
| 32 | 〃 | `test_transcriptions_missing_token_returns_401_problem` | 401, problem+json, `UNAUTHENTICATED` |
| 33 | 〃 | `test_transcriptions_invalid_base64_returns_400_validation_failed` | 400, problem+json, `VALIDATION_FAILED`, `errors[0].field == "audio"` |
| 34 | 〃 | `test_transcriptions_audio_too_large_returns_400_too_large` | settings override max bytes 4 → `errors[0].code == "TOO_LARGE"` |
| 35 | 〃 | `test_transcriptions_provider_failure_returns_503_dependency_unavailable` | fake scripted `ModelUnavailableError` → 503 |
| 36 | 〃 | `test_transcriptions_invalid_output_returns_502` | scripted `ModelOutputInvalidError` → 502 `MODEL_OUTPUT_INVALID` |
| 37 | eval/test_eval_transcription.py | `test_eval_transcription_egyptian_dialect_mean_wer_within_threshold` | mean WER ≤ 0.35 (skips with a reason if there is no dataset or no key) |
| — | integration/test_openapi_document.py (existing) | unchanged | passes after the regeneration |
| — | unit/test_openai_embedding.py (existing) | unchanged | passes after the `post_with_retries` refactor |

### api/ (xUnit v3, FluentAssertions 7, NSubstitute; `Method_Scenario_Expected`)
| # | Test class (path under `Elmanhg.Tests/`) | Method | Asserts |
|---|---|---|---|
| 1 | `Domain/TeacherThreads/TeacherThreadVoiceReplyTests` | `ReplyWithVoice_ClaimerOnOpenThread_AddsVoiceMessageAndAnswers` | message Kind Voice, Text trimmed, AudioUrl, AudioDurationSeconds 42, TranscriptFinal true, CreatedAt microsecond-truncated; thread Answered, UpdatedBy, UpdationDate |
| 2 | 〃 | `ReplyWithVoice_Unclaimed_ThrowsNotClaimed` | ConflictCoreException `TEACHER_THREAD_NOT_CLAIMED`, no message added |
| 3 | 〃 | `ReplyWithVoice_ClaimedByOther_ThrowsAlreadyClaimed` | `TEACHER_THREAD_ALREADY_CLAIMED` |
| 4 | 〃 | `ReplyWithVoice_Answered_ThrowsNotAwaitingReply` | `TEACHER_THREAD_NOT_AWAITING_REPLY` |
| 5 | 〃 | `ReplyWithVoice_BlankText_ThrowsTextRequired` | BusinessRuleViolationCoreException `TEACHER_MESSAGE_TEXT_REQUIRED`, status still Open |
| 6 | 〃 | `EnsureCanReply_ClaimerOnOpenThread_DoesNotThrow` | no exception |
| 7 | 〃 | `EnsureCanReply_Unclaimed_ThrowsNotClaimed` | code |
| 8 | 〃 | `EnsureCanReply_Answered_ThrowsNotAwaitingReply` | code |
| 9 | 〃 | `Reply_Text_HasNoAudioAndTranscriptNotFinal` | AudioUrl null, AudioDurationSeconds null, TranscriptFinal false |
| 10 | `Domain/TeacherThreads/TeacherVoiceDraftTests` | `Record_SetsPendingDueNowWithZeroAttempts` | all fields; NextAttemptAt == RecordedAt (µs) |
| 11 | 〃 | `IsDueAt_PendingAtNextAttempt_ReturnsTrue` | true |
| 12 | 〃 | `IsDueAt_PendingBeforeNextAttempt_ReturnsFalse` | false |
| 13 | 〃 | `IsDueAt_Ready_ReturnsFalse` | false |
| 14 | 〃 | `CompleteTranscription_Pending_SetsReadyTrimmedTranscriptAndModel` | Status Ready, Transcript trimmed, Model, Attempts 1, NextAttemptAt null, TranscribedAt, UpdationDate |
| 15 | 〃 | `CompleteTranscription_NotPending_ThrowsNotPending` | `TEACHER_VOICE_DRAFT_NOT_PENDING` |
| 16 | 〃 | `FailAttempt_BelowMax_SchedulesExponentialRetry` | 1st: +15 s, Attempts 1; 2nd: +30 s from the 2nd failure time; Status Pending |
| 17 | 〃 | `FailAttempt_ReachingMax_MarksFailed` | Attempts == max, Failed, NextAttemptAt null |
| 18 | 〃 | `FailAttempt_NotPending_ThrowsNotPending` | code |
| 19 | 〃 | `MarkSent_Ready_SetsSentWithMessageId` | Sent, SentMessageId, UpdatedBy == TeacherId |
| 20 | 〃 | `MarkSent_Failed_SetsSent` | Sent |
| 21 | 〃 | `MarkSent_Pending_ThrowsNotReady` | `TEACHER_VOICE_DRAFT_NOT_READY`, status Pending |
| 22 | 〃 | `MarkSent_AlreadySent_ThrowsAlreadySent` | `TEACHER_VOICE_DRAFT_ALREADY_SENT` |
| 23 | `Application/Features/TeacherInbox/RecordVoiceDraft/RecordVoiceDraftHandlerTests` | `Handle_NoCurrentUser_ThrowsUnauthorized` | code; storage and Save DidNotReceive |
| 24 | 〃 | `Handle_UnknownThread_ThrowsNotFound` | `TEACHER_THREAD_NOT_FOUND` |
| 25 | 〃 | `Handle_TeacherOutsideSubject_ThrowsForbidden` | `SUBJECT_OUT_OF_SCOPE`; `SaveAsync` DidNotReceive |
| 26 | 〃 | `Handle_ThreadClaimedByOther_ThrowsAlreadyClaimed` | code; `SaveAsync` DidNotReceive |
| 27 | 〃 | `Handle_ThreadAnswered_ThrowsNotAwaitingReply` | code |
| 28 | 〃 | `Handle_ClaimerOnOpenThread_StoresAudioAndCreatesPendingDraft` | `SaveAsync` key matches `^teacher-threads/[0-9a-f]{32}\.webm$`; `AddAsync` draft Pending with the returned URL and duration; Save Received(1); result Status Pending |
| 29 | 〃 | `Handle_AdminClaimer_SkipsSubjectAssignment` | `IsAssignedAsync` DidNotReceive; draft created |
| 30 | `.../RecordVoiceDraft/RecordVoiceDraftValidatorTests` | `Validate_ValidWebm_Passes` | valid |
| 31 | 〃 | `Validate_ValidOggAndM4a_Passes` | Theory (`voice.ogg`/`audio/ogg`/OggS; `voice.m4a`/`audio/mp4`/ftyp) |
| 32 | 〃 | `Validate_ContentTypeWithCodecs_Passes` | `audio/webm;codecs=opus` valid |
| 33 | 〃 | `Validate_MissingAudio_FailsAudioRequired` | code |
| 34 | 〃 | `Validate_EmptyAudio_FailsAudioRequired` | code |
| 35 | 〃 | `Validate_DisallowedExtension_FailsTypeInvalid` | `voice.mp3` |
| 36 | 〃 | `Validate_WrongContentType_FailsTypeInvalid` | `image/png` |
| 37 | 〃 | `Validate_SignatureMismatch_FailsTypeInvalid` | `.webm` with PNG bytes |
| 38 | 〃 | `Validate_TooLarge_FailsTooLarge` | 1 MB limit, 1 MB + 1 bytes |
| 39 | 〃 | `Validate_DurationZero_FailsDurationInvalid` | code |
| 40 | 〃 | `Validate_DurationAboveMax_FailsDurationInvalid` | max + 1 |
| 41 | `.../RecordVoiceDraft/TeacherVoiceSignatures.cs` (helper, not a test) | — | byte arrays and `FormFile(bytes, fileName, contentType)` |
| 42 | `.../GetVoiceDraft/GetVoiceDraftHandlerTests` | `Handle_NoCurrentUser_ThrowsUnauthorized` | code |
| 43 | 〃 | `Handle_OwnDraft_ReturnsDraft` | all result fields |
| 44 | 〃 | `Handle_OtherTeachersDraft_ThrowsNotFound` | `TEACHER_VOICE_DRAFT_NOT_FOUND` (predicate compiled over an in-memory list) |
| 45 | 〃 | `Handle_DraftOfAnotherThread_ThrowsNotFound` | code |
| 46 | `.../SendVoiceReply/SendVoiceReplyHandlerTests` | `Handle_NoCurrentUser_ThrowsUnauthorized` | code, Save DidNotReceive |
| 47 | 〃 | `Handle_UnknownThread_ThrowsNotFound` | code |
| 48 | 〃 | `Handle_TeacherOutsideSubject_ThrowsForbidden` | code |
| 49 | 〃 | `Handle_UnknownDraft_ThrowsDraftNotFound` | code |
| 50 | 〃 | `Handle_PendingDraft_ThrowsNotReady` | `TEACHER_VOICE_DRAFT_NOT_READY`; Save DidNotReceive |
| 51 | 〃 | `Handle_SentDraft_ThrowsAlreadySent` | code |
| 52 | 〃 | `Handle_ThreadAnswered_ThrowsNotAwaitingReply` | code; draft still Ready |
| 53 | 〃 | `Handle_ReadyDraft_SendsVoiceReplyWithCorrectedText` | last message Voice, Text = request text, AudioUrl = draft URL; draft Sent with SentMessageId == message.Id; thread Answered; Save Received(1); result `CanReply` false, last message `AudioUrl` |
| 54 | 〃 | `Handle_FailedDraftWithTypedText_Sends` | Sent |
| 55 | `.../SendVoiceReply/SendVoiceReplyValidatorTests` | `Validate_Valid_Passes` | |
| 56 | 〃 | `Validate_EmptyDraftId_FailsDraftIdRequired` | code |
| 57 | 〃 | `Validate_BlankText_FailsReplyTextRequired` | code |
| 58 | 〃 | `Validate_TooLongText_FailsReplyTextTooLong` | code |
| 59 | `.../TranscribeVoiceDraft/TranscribeVoiceDraftHandlerTests` | `Handle_UnknownDraft_DoesNothing` | client and Save DidNotReceive |
| 60 | 〃 | `Handle_DraftNotDue_DoesNothing` | 〃 |
| 61 | 〃 | `Handle_ReadyDraft_DoesNothing` | 〃 |
| 62 | 〃 | `Handle_AudioMissing_ThrowsAudioNotFound` | NotFoundCoreException `TEACHER_VOICE_AUDIO_NOT_FOUND`; Save DidNotReceive |
| 63 | 〃 | `Handle_DueDraft_TranscribesAudioAndStoresTranscript` | the client request Audio equals the stored bytes, ContentType `audio/webm`, Language `ar`, DurationSeconds; draft Ready with the transcript and model; Save Received(1) |
| 64 | 〃 | `Handle_ClientUnavailable_PropagatesWithoutSaving` | ServiceUnavailableCoreException; draft still Pending; Save DidNotReceive |
| 65 | `.../FailVoiceDraftTranscription/FailVoiceDraftTranscriptionHandlerTests` | `Handle_UnknownDraft_DoesNothing` | Save DidNotReceive |
| 66 | 〃 | `Handle_NotPending_DoesNothing` | unchanged, Save DidNotReceive |
| 67 | 〃 | `Handle_Pending_SchedulesRetry` | Attempts 1, NextAttemptAt = now + 15 s; Save Received(1) |
| 68 | 〃 | `Handle_LastAttempt_MarksFailed` | MaxAttempts 1 → Failed |
| 69 | `.../GetDueVoiceDraftIds/GetDueVoiceDraftIdsHandlerTests` | `Handle_UsesClockAndBatchSize_ReturnsRepositoryIds` | `GetDueIdsAsync(now, 5, ...)` Received with the fake-clock now; returns those ids |
| 70 | `.../GetVoiceReplySettings/GetVoiceReplySettingsHandlerTests` | `Handle_ReturnsConfiguredLimits` | (180, 5) from options |
| 71 | `.../TeacherThreads/CanViewTeacherThreadMedia/CanViewTeacherThreadMediaHandlerTests` (moved) | the existing methods, renamed types only | unchanged assertions |
| 72 | 〃 | `Handle_OwningStudentVoiceAudio_ReturnsTrue` | builder `AnsweredByVoice` URL |
| 73 | 〃 | `Handle_ScopedTeacherVoiceAudio_ReturnsTrue` | assigned → true |
| 74 | 〃 | `Handle_OtherStudentVoiceAudio_ReturnsFalse` | false |
| 75 | `Application/Shared/Storage/MediaContentTypesTests` | `FromKey_KnownExtensions_MapsContentType` | Theory over all 9 extensions (upper-case `.WEBM` too) |
| 76 | 〃 | `FromKey_UnknownExtension_ReturnsFallback` | `application/octet-stream` |
| 77 | `Infrastructure/Storage/LocalDiskFileStorageTests` | `OpenReadAsync_ExistingKey_ReturnsContentLengthAndType` | bytes, Length, `audio/webm` |
| 78 | 〃 | `OpenReadAsync_MissingKey_ReturnsNull` | null |
| 79 | 〃 | `OpenReadAsync_KeyEscapingRoot_ThrowsArgumentException` | ArgumentException |
| 80 | `Infrastructure/Storage/S3FileStorageTests` | `SaveAsync_PutsObjectInBucketAndReturnsPublicUrl` | `PutObjectAsync` Received with BucketName, Key, ContentType `image/png`, DisablePayloadSigning true, the same stream; returns `/api/media/<key>` |
| 81 | 〃 | `OpenReadAsync_ExistingObject_ReturnsStreamLengthAndType` | Content is the response stream, Length, `audio/ogg` |
| 82 | 〃 | `OpenReadAsync_MissingObject_ReturnsNull` | `AmazonS3Exception { StatusCode = NotFound }` → null |
| 83 | 〃 | `OpenReadAsync_OtherS3Error_Propagates` | 403 exception propagates |
| 84 | `Infrastructure/Storage/FileStorageOptionsValidatorTests` | `Validate_Local_Succeeds` | success |
| 85 | 〃 | `Validate_CompleteS3_Succeeds` | success (R2 URL) |
| 86 | 〃 | `Validate_S3MissingSetting_Fails` | Theory: bucket / access key / secret / region blank → failure message names the key |
| 87 | 〃 | `Validate_S3NonHttpsServiceUrl_Fails` | `http://…` and a relative URL |
| 88 | `Infrastructure/Storage/FileStorageServiceCollectionExtensionsTests` | `AddFileStorage_Local_ResolvesLocalDiskFileStorage` | resolved type |
| 89 | 〃 | `AddFileStorage_S3_ResolvesS3FileStorage` | resolved type (dummy credentials, no network) |
| 90 | `Infrastructure/AiService/FakeAiTranscriptionClientTests` | `TranscribeAsync_Development_ReturnsFixedTranscript` | text, model `fake`, language echoed |
| 91 | 〃 | `TranscribeAsync_Production_ThrowsAiServiceUnavailable` | code |
| 92 | `Infrastructure/AiService/HttpAiTranscriptionClientTests` | `TranscribeAsync_Success_PostsBase64AudioWithBearerAndMapsReply` | stub captured: POST `v1/transcriptions`, Bearer token, JSON `audio` == base64 of the bytes, `contentType`, `language`, `durationSeconds`; result mapped |
| 93 | 〃 | `TranscribeAsync_EmptyText_IsAccepted` | `""` → result Text `""` |
| 94 | 〃 | `TranscribeAsync_Non2xx_ThrowsAiServiceUnavailable` | 503 → code |
| 95 | 〃 | `TranscribeAsync_TransportFailure_ThrowsAiServiceUnavailable` | HttpRequestException → code |
| 96 | 〃 | `TranscribeAsync_InvalidReply_ThrowsAiServiceUnavailable` | Theory: `not json`, `{"model":"m"}` (no text), `{"text":"x","model":""}` |
| 97 | `Infrastructure/AiService/AiServiceServiceCollectionExtensionsTests` | `AddAiService_Fake_ResolvesFakeTranscriptionClient` | type |
| 98 | 〃 | `AddAiService_Http_ResolvesHttpTranscriptionClient` | type |
| 99 | `Api/Workers/TeacherVoiceTranscriptionWorkerTests` | `Sweep_DueDrafts_TranscribesEach` | `TranscribeVoiceDraftCommand` Received for each id |
| 100 | 〃 | `Sweep_TranscriptionFails_LogsWarningAndRecordsFailure` | one Warning; `FailVoiceDraftTranscriptionCommand` Received for the failed id only; the next id still transcribed |
| 101 | 〃 | `Sweep_Disabled_NeverQueries` | `ExecuteTask` completed; the query DidNotReceive |
| 102 | 〃 | `Stop_DuringSweep_EndsTheLoopWithoutLogging` | mirror of the LessonContentIndexWorker test |
| 103 | `Api/FileStorage/PublicMediaMiddlewareTests` | `Invoke_PublicKey_StreamsFileWithPublicCache` | 200 body bytes, ContentType, nosniff, Cache-Control `public, max-age=31536000, immutable` |
| 104 | 〃 | `Invoke_PrivateFolderAnySpelling_Returns404WithoutReading` | Theory `teacher-threads/x.png`, `Teacher-Threads/x.png`, `teacher-threads./x.png`; `OpenReadAsync` DidNotReceive |
| 105 | 〃 | `Invoke_UnsafeKey_Returns404` | Theory `lessons/../x.png`, `lessons//x.png`, `lessons\\x.png`, `a~1/x.png` |
| 106 | 〃 | `Invoke_MissingFile_Returns404` | 404 |
| 107 | 〃 | `Invoke_OutsideMediaPath_CallsNext` | next called |
| 108 | `Integration/TeacherInbox/VoiceReplyEndpointTests` | `PostVoiceDraft_Claimer_StoresAudioAndReturnsPendingDraft` | 200, status Pending; DB draft row Pending with AudioKey; the file is readable through a scoped `IFileStorage.OpenReadAsync` |
| 109 | 〃 | `PostVoiceDraft_UnclaimedThread_Returns409NotClaimed` | 409 problem `TEACHER_THREAD_NOT_CLAIMED` |
| 110 | 〃 | `PostVoiceDraft_InvalidAudio_Returns422TypeInvalid` | PNG bytes as `voice.webm` → 422 `TEACHER_VOICE_AUDIO_TYPE_INVALID` |
| 111 | 〃 | `PostVoiceDraft_Student_Returns403` | 403 |
| 112 | 〃 | `PostVoiceDraft_Anonymous_Returns401` | 401 |
| 113 | 〃 | `GetVoiceDraft_AfterTranscription_ReturnsReadyTranscript` | send `TranscribeVoiceDraftCommand` through a scoped ISender → GET 200, Ready, transcript == `FakeAiTranscriptionClient.FakeTranscript` |
| 114 | 〃 | `GetVoiceDraft_OtherTeacher_Returns404` | 404 `TEACHER_VOICE_DRAFT_NOT_FOUND` |
| 115 | 〃 | `SendVoiceReply_ReadyDraft_AnswersThreadWithVoiceMessage` | 200; DB: thread Answered, message Voice, TranscriptFinal true, Text = the edited text, AudioUrl; draft Sent |
| 116 | 〃 | `SendVoiceReply_PendingDraft_Returns409NotReady` | 409 `TEACHER_VOICE_DRAFT_NOT_READY` |
| 117 | 〃 | `SendVoiceReply_BlankText_Returns422` | `TEACHER_THREAD_REPLY_TEXT_REQUIRED` |
| 118 | 〃 | `GetMyThread_VoiceReply_ReturnsAudioUrlAndTranscript` | the student GET `/api/teacher-threads/{id}` message has `kind: "Voice"`, `audioUrl`, `audioDurationSeconds`, `text` |
| 119 | 〃 | `GetVoiceSettings_Teacher_ReturnsLimits` | 200 `{maxDurationSeconds:180,maxSizeInMb:5}` |
| 120 | 〃 | `GetVoiceSettings_Student_Returns403` | 403 |
| 121 | `Integration/TeacherThreads/TeacherThreadMediaEndpointTests` | `Get_OwningStudentVoiceReply_ServesAudio` | 200, bytes == WebmBytes, `audio/webm`, private cache |
| 122 | 〃 | `Get_OtherStudentVoiceReply_Returns404` | 404 |
| 123 | 〃 | `Get_UnsentDraftAudio_Returns404ForItsTeacher` | 404 |
| — | `Integration/Persistence/AppDbContextTests` (existing) | migration list | +`_AddTeacherVoiceReplies` |
| — | the existing localization-resource and permission-matrix tests | unchanged | pass with the new keys and endpoints |

### web/ (Vitest + Testing Library + MSW; `it('<does X> when <Y>')`)
| # | File | Test | Asserts |
|---|---|---|---|
| 1 | api/blobToDataUrl.test.ts | `returns a data URL for an allowed media type` | `data:audio/webm;base64,...` |
| 2 | 〃 | `rejects a blob of another media type` | rejects |
| 3 | api/formatDuration.test.ts | `formats seconds as m:ss in English` | 42 → `0:42`, 185 → `3:05` |
| 4 | 〃 | `uses Arabic-Indic digits in Arabic` | `٠:٤٢` |
| 5 | api/recorderMimeType.test.ts | `prefers webm opus when supported` | |
| 6 | 〃 | `falls back to mp4 when only mp4 is supported` | Safari |
| 7 | 〃 | `returns null when nothing is supported` | |
| 8 | 〃 | `names the upload file after the recording type` | webm/ogg/mp4 → voice.webm / voice.ogg / voice.m4a |
| 9 | schemas/voiceTranscriptFormSchema.test.ts | `accepts a transcript` / `rejects a blank transcript with the translation key` | 2 cases |
| 10 | hooks/useVoiceRecorder.test.ts | `records and returns the blob with its duration` | fake recorder; after start, advance 3 s, stop → `onRecorded` blob type `audio/webm;codecs=opus`, durationSeconds 3, status `recorded`, the track stopped |
| 11 | 〃 | `reports denied when microphone permission is refused` | status `denied` |
| 12 | 〃 | `reports unsupported without MediaRecorder` | status `unsupported` |
| 13 | 〃 | `stops automatically at the maximum length` | max 5 → after 5 s `recorded`, duration 5 |
| 14 | pages/InboxThreadPage.voice.test.tsx | `shows the text reply form by default with a text or voice choice` | radios Text (checked) / Voice; textbox "Your reply" |
| 15 | 〃 | `records a voice note, uploads it and shows the transcript for correction` | Voice → Record → advance 3 s → Stop → upload body has `durationSeconds=3` and a `voice.webm` file; the draft GET is Ready → textbox "Voice transcript (you can correct it before sending)" has the transcript |
| 16 | 〃 | `shows the transcribing state while the draft is pending` | "Transcribing the recording…" and then, after the poll interval, the transcript |
| 17 | 〃 | `sends the corrected transcript as a voice reply` | edit text → Send reply → request body `{draftId, text: edited}`; "Reply sent."; the answered note replaces the form |
| 18 | 〃 | `lets the teacher type the text when transcription failed` | Failed → the failure note, empty textbox; send works |
| 19 | 〃 | `requires the transcript before sending` | cleared → "Write the reply text", focus on the textbox |
| 20 | 〃 | `shows the server error when the upload is rejected` | 422 `TEACHER_VOICE_AUDIO_TOO_LARGE` → "The recording is larger than allowed." |
| 21 | 〃 | `shows a message when microphone access is denied` | text |
| 22 | 〃 | `shows a message when the browser cannot record` | no MediaRecorder → text |
| 23 | 〃 | `records again after discarding the first recording` | Record again → the Record button returns and the transcript form is gone |
| 24 | 〃 | `renders right-to-left in Arabic with the voice labels` | `lng: 'ar'`, `dir="rtl"`, radio «صوت» |
| 25 | 〃 | `has no axe violations in voice mode` | axe |
| 26 | pages/TeacherThreadPage.voice.test.tsx | `plays a voice reply with its length and transcript` | the audio element labelled "Voice reply" has a `data:audio/webm` src; "Length: 0:42"; "Voice transcript:"; the text |
| 27 | 〃 | `shows an error when the recording cannot load` | media 500 → "Could not load the recording" |
| 28 | 〃 | `shows the voice reply in Arabic` | `lng: 'ar'` → «نص التفريغ الصوتي:» and `dir="rtl"` |
| — | InboxThreadPage.test.tsx, TeacherThreadPage.test.tsx and the others (existing) | unchanged | still pass (only the fixture files change) |

Voice page tests use `vi.useFakeTimers({ shouldAdvanceTime: true })`, `userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) })`, `installFakeMediaRecorder()` in `beforeEach` and `uninstall()` in `afterEach`, and the generated MSW handlers (`getRecordTeacherVoiceDraftMockHandler`, `getGetTeacherVoiceDraftMockHandler`, `getSendTeacherVoiceReplyMockHandler`, `getGetVoiceReplySettingsMockHandler`) plus `http.get('*/api/media/teacher-threads/:file', …audio/webm)`.

## Definition of done
- [ ] Every story sub-task is covered: the browser recorder and upload to storage (F4/F5, P11, I2); the transcription job through the ai service, with the Arabic language and the Egyptian eval harness (A2–A10, P21, W3; the run itself is deferred with its reason); teacher review and correction before send (F10/F12, P16); student playback with text (F13, ThreadMessage).
- [ ] `ai`: `uv sync --locked`, `ruff format --check`, `ruff check`, `mypy src` and `pytest -m "not eval"` all exit 0. `ai/openapi/v1.json` is regenerated, and `test_openapi_document` passes. The existing `test_openai_embedding.py` passes unchanged.
- [ ] Transcription text is never logged (checked in test #7). The OpenAI key is `SecretStr` and never logged.
- [ ] `api`: `dotnet build` has no new warnings. `dotnet test api/ -c Release` is green with `appsettings.json` moved aside (CI parity). `dotnet format --verify-no-changes` has only the known core-libraries noise. `dotnet list package --vulnerable --include-transitive` is clean with AWSSDK.S3 4.0.103.4.
- [ ] Migration `AddTeacherVoiceReplies` has no drops, and `TranscriptFinal` defaults to false. `AppDbContextTests` lists it.
- [ ] `TeacherVoiceDraft` is in the global soft-delete filter. Enums are stored as strings. All FKs are Restrict.
- [ ] Every new endpoint has `DefaultCodes.AskTeacherReply`. The subject scope is enforced in the handlers (403). Another teacher's draft is 404.
- [ ] Handlers have no try/catch. The worker is the only place that catches, a scope per item.
- [ ] Private media (photos and audio) is served through `IFileStorage.OpenReadAsync` after `CanViewTeacherThreadMediaQuery`, for both providers. Unsent draft audio is never served. The S3 public proxy refuses private and unsafe keys.
- [ ] All 10 new error codes are in both resx files and in the web `errors.*` (ar/en).
- [ ] `appsettings.example.json`, `ApiFactory` (the sweep is off) and code defaults carry every new key. `Provider` defaults to `Local`, and product switches default ON in code (`TranscriptionSweepEnabled = true`).
- [ ] Postman: the voice requests use `voiceThreadId`. No thread is answered twice in a folder run.
- [ ] `web`: `npm run gen:api` gives no diff after commit. `tsc -b`, `eslint --max-warnings=0` and `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` are clean. `vitest run --coverage` is green, and the feature thresholds are met.
- [ ] Web: no literal colours or sizes and no physical-direction utilities. Every string is in ar and en. The recorder uses no red (D19). Icon buttons have text labels. The audio element has an `aria-label`. Transcribing status is `aria-live="polite"`.
- [ ] The existing tests are unchanged except for: the renamed `CanViewTeacherThreadMediaHandlerTests` (type renames only), the `AppDbContextTests` migration list, and the fixture/helper files listed above.
- [ ] Docs updated in the same change: ask-teacher.md, ai-service.md, PRD §15/§18/§19, deployment.md, claude-design-prompt.md §4, prototype.md, env examples and compose (docs-sync: no divergence).
- [ ] The Arabic strings were written without the `\u` escape trap (the Arabic in the resx, the fake transcripts and the scorer code points was checked in the diff).
- [ ] The PR closes #96 and #207 (the S3 adapter deferred from #94).
- [ ] Deferred items filed: (1) the Egyptian-dialect eval run (needs recordings and an OpenAI key); (2) a live check of R2/S3 and Whisper (needs credentials).

Lane note for the orchestrator: #91/#92 touch `ai/src/elmanhg_ai/settings.py`, `main.py`, `api/deps.py`, `tests/conftest.py`, `AppDbContext`, the snapshot, the migrations list, `ErrorCodes`, resx, `appsettings.example.json`, `ApiFactory`, both OpenAPI files, `docs/ai-service.md` and `docs/PRD.md`. All the edits here are additive. Merge `origin/main` and regenerate before the merge.
