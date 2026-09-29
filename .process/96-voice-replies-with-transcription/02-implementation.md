# Implementation — [E9.S3] Voice replies with transcription (#96)

Branch `feature/96-voice-replies-with-transcription` in worktree `D:\Personal\elmanhg-wt\96`. `git merge origin/main` was a fast-forward to `ad70cfb` (#91 avatar), so no merge commit was needed. The story work is not committed.

## Files created

### ai/
| Path | Lines | Purpose |
|---|---|---|
| `ai/src/elmanhg_ai/api/transcriptions/__init__.py` | 0 | package (A1) |
| `ai/src/elmanhg_ai/api/transcriptions/schemas.py` | 24 | `AudioContentType`, `TranscriptionIn` (Base64Bytes), `TranscriptionOut` (A2) |
| `ai/src/elmanhg_ai/api/transcriptions/router.py` | 27 | `POST /v1/transcriptions`, operationId `transcriptions_create_transcription` (A3) |
| `ai/src/elmanhg_ai/clients/transcription.py` | 44 | protocol, request/reply, cost estimate, `build_transcription_client` (A4) |
| `ai/src/elmanhg_ai/clients/fake_transcription.py` | 26 | scripted fake, fixed Arabic transcript (A5) |
| `ai/src/elmanhg_ai/clients/openai_http.py` | 47 | shared `post_with_retries` (moved from the embeddings client) (A6) |
| `ai/src/elmanhg_ai/clients/openai_transcription.py` | 78 | Whisper adapter (multipart, `language`, `response_format=json`) (A7) |
| `ai/src/elmanhg_ai/pipelines/transcribe.py` | 66 | limits, timing, `transcription.completed` log without text (A8) |
| `ai/src/elmanhg_ai/eval/transcription.py` | 39 | Arabic normalisation, `score_word_error_rate`, `WER_THRESHOLD` 0.35 (A10) |
| `ai/tests/fixtures/openai/transcription_success.json` | 1 | Whisper reply fixture (A11) |
| `ai/tests/unit/test_transcription_schemas.py` | 55 | 6 tests (A12) |
| `ai/tests/unit/test_transcribe_pipeline.py` | 94 | 6 tests (A13) |
| `ai/tests/unit/test_fake_transcription.py` | 33 | 2 tests (A14) |
| `ai/tests/unit/test_openai_transcription.py` | 167 | 9 tests (one parametrized ×2) (A15) |
| `ai/tests/unit/test_transcription_scorer.py` | 38 | 4 tests (A16) |
| `ai/tests/integration/test_transcriptions_endpoint.py` | 113 | 6 tests (A17) |
| `ai/tests/eval/test_eval_transcription.py` | 38 | eval, skips without manifest or key (A18) |

### api/
| Path | Lines | Purpose |
|---|---|---|
| `Elmanhg.Domain/TeacherThreads/TeacherVoiceDraftStatus.cs` | 3 | D1 |
| `Elmanhg.Domain/TeacherThreads/TeacherVoiceDraft.cs` | 100 | D2, the transcription job aggregate |
| `Elmanhg.Domain/TeacherThreads/ITeacherVoiceDraftRepository.cs` | 8 | D3 |
| `Elmanhg.Application/Shared/Storage/StoredFile.cs` | 6 | P1 |
| `Elmanhg.Application/Shared/Storage/MediaContentTypes.cs` | 21 | P2 |
| `Elmanhg.Application/Shared/AiService/IAiTranscriptionClient.cs`, `AiTranscriptionRequest.cs`, `AiTranscriptionResult.cs` | 6 / 3 / 3 | P3–P5 |
| `Elmanhg.Application/TeacherInbox/Shared/TeacherVoiceDraftResult.cs`, `TeacherVoiceDraftResultGenerator.cs` | 5 / 8 | P6–P7 |
| `Elmanhg.Application/TeacherInbox/RecordVoiceDraft/{TeacherVoiceFormats,RecordVoiceDraftCommand,RecordVoiceDraftValidator,RecordVoiceDraftHandler}.cs` | 29 / 7 / 30 / 39 | P8–P11 |
| `Elmanhg.Application/TeacherInbox/GetVoiceDraft/{GetVoiceDraftQuery,GetVoiceDraftHandler}.cs` | 6 / 23 | P12–P13 |
| `Elmanhg.Application/TeacherInbox/SendVoiceReply/{SendVoiceReplyCommand,SendVoiceReplyValidator,SendVoiceReplyHandler}.cs` | 6 / 21 / 37 | P14–P16 |
| `Elmanhg.Application/TeacherInbox/GetVoiceReplySettings/{GetVoiceReplySettingsQuery,VoiceReplySettingsResult,GetVoiceReplySettingsHandler}.cs` | 5 / 3 / 14 | P17–P19 |
| `Elmanhg.Application/TeacherInbox/TranscribeVoiceDraft/{TranscribeVoiceDraftCommand,TranscribeVoiceDraftHandler}.cs` | 5 / 31 | P20–P21 |
| `Elmanhg.Application/TeacherInbox/FailVoiceDraftTranscription/{FailVoiceDraftTranscriptionCommand,FailVoiceDraftTranscriptionHandler}.cs` | 5 / 23 | P22–P23 |
| `Elmanhg.Application/TeacherInbox/GetDueVoiceDraftIds/{GetDueVoiceDraftIdsQuery,GetDueVoiceDraftIdsHandler}.cs` | 5 / 14 | P24–P25 |
| `Elmanhg.Infrastructure/TeacherThreads/TeacherVoiceDraftRepository.cs` | 22 | I1 |
| `Elmanhg.Infrastructure/Storage/S3FileStorage.cs` | 39 | I2 (the only catch maps a missing object to null) |
| `Elmanhg.Infrastructure/Storage/FileStorageOptionsValidator.cs` | 34 | I3 |
| `Elmanhg.Infrastructure/Storage/FileStorageServiceCollectionExtensions.cs` | 50 | I4 (`AddFileStorage`, R2/S3 client config per D8) |
| `Elmanhg.Infrastructure/AiService/FakeAiTranscriptionClient.cs` | 22 | I5 |
| `Elmanhg.Infrastructure/AiService/HttpAiTranscriptionClient.cs` | 76 | I6 |
| `Elmanhg.Infrastructure/Migrations/20260929162717_AddTeacherVoiceReplies.cs` (+ Designer) | 122 / 2520 | I7: 3 AddColumns, filtered `AudioUrl` index, `TeacherVoiceDrafts` table + indexes + restrict FKs; no drops |
| `Elmanhg.Api/FileStorage/MediaStorageExtensions.cs` | 35 | W1 |
| `Elmanhg.Api/FileStorage/PublicMediaMiddleware.cs` | 50 | W2 |
| `Elmanhg.Api/Workers/TeacherVoiceTranscriptionWorker.cs` | 76 | W3 (scope per item, the #81/LessonContentIndexWorker pattern) |
| Tests (18 files) | — | `Domain/TeacherThreads/TeacherThreadVoiceReplyTests` (9), `TeacherVoiceDraftTests` (13); `Application/Features/TeacherInbox/RecordVoiceDraft/RecordVoiceDraftHandlerTests` (7), `RecordVoiceDraftValidatorTests` (11), `TeacherVoiceSignatures` (helper); `GetVoiceDraftHandlerTests` (4); `SendVoiceReplyHandlerTests` (9); `SendVoiceReplyValidatorTests` (4); `TranscribeVoiceDraftHandlerTests` (6); `FailVoiceDraftTranscriptionHandlerTests` (4); `GetDueVoiceDraftIdsHandlerTests` (1); `GetVoiceReplySettingsHandlerTests` (1); `Application/Shared/Storage/MediaContentTypesTests` (2); `Infrastructure/Storage/S3FileStorageTests` (4), `FileStorageOptionsValidatorTests` (4), `FileStorageServiceCollectionExtensionsTests` (2); `Infrastructure/AiService/FakeAiTranscriptionClientTests` (2), `HttpAiTranscriptionClientTests` (5); `Api/Workers/TeacherVoiceTranscriptionWorkerTests` (4); `Api/FileStorage/PublicMediaMiddlewareTests` (5); `Integration/TeacherInbox/VoiceReplyEndpointTests` (13) |

### web/
| Path | Lines | Purpose |
|---|---|---|
| `features/askTeacher/api/blobToDataUrl.ts` | 14 | F1 |
| `features/askTeacher/api/formatDuration.ts` | 9 | F2 |
| `features/askTeacher/api/recorderMimeType.ts` | 15 | F3 |
| `features/askTeacher/hooks/useVoiceRecorder.ts` | 128 | F4 (MediaRecorder, auto-stop, cleanup on unmount) |
| `features/askTeacher/hooks/useRecordVoiceDraft.ts` | 31 | F5 |
| `features/askTeacher/hooks/useVoiceDraft.ts` | 12 | F6 (polls every 2 s while Pending) |
| `features/askTeacher/hooks/useSendVoiceReply.ts` | 34 | F7 |
| `features/askTeacher/schemas/voiceTranscriptFormSchema.ts` | 9 | F8 |
| `features/askTeacher/components/ReplyPanel.tsx` | 42 | F9 («نص / صوت» radios) |
| `features/askTeacher/components/VoiceReplyPanel.tsx` | 79 | F10 |
| `features/askTeacher/components/VoiceRecorderControls.tsx` | 66 | F11 (Lucide Mic/Square, no red) |
| `features/askTeacher/components/VoiceTranscriptForm.tsx` | 45 | F12 |
| `features/askTeacher/components/ThreadAudio.tsx` | 40 | F13 |
| `web/src/test/fakeMediaRecorder.ts` | 72 | F14 |
| Tests (7 files) | — | `api/blobToDataUrl.test.ts` (2), `api/formatDuration.test.ts` (2), `api/recorderMimeType.test.ts` (4), `schemas/voiceTranscriptFormSchema.test.ts` (2), `hooks/useVoiceRecorder.test.ts` (4), `pages/InboxThreadPage.voice.test.tsx` (12), `pages/TeacherThreadPage.voice.test.tsx` (3) |
| `shared/api/generated/model/{recordTeacherVoiceDraftBody,sendVoiceReplyRequest,teacherVoiceDraftResult,teacherVoiceDraftStatus,voiceReplySettingsResult}.ts` | — | Orval output |

## Files modified
| Path | Change |
|---|---|
| `ai/src/elmanhg_ai/settings.py` | 6 `transcription_*` settings + `_openai_transcription_needs_key` |
| `ai/src/elmanhg_ai/main.py` | `transcription_client` in `create_app` and the lifespan (build, `aclose`), router included, `service.started` fields |
| `ai/src/elmanhg_ai/api/deps.py` | `transcription_client_from_app`, `TranscriptionClientDep` |
| `ai/src/elmanhg_ai/clients/openai_embedding.py` | uses `openai_http.post_with_retries`; behaviour unchanged, existing test unchanged and green |
| `ai/tests/conftest.py` | `fake_transcription` fixture, passed to `app`; `transcription_payload` builder |
| `ai/tests/unit/test_settings.py` | +3 tests |
| `ai/openapi/v1.json` | regenerated |
| `api/Directory.Packages.props`, `Elmanhg.Infrastructure.csproj` | `AWSSDK.S3` 4.0.103.3 (see Deviations) |
| `Elmanhg.Domain/TeacherThreads/TeacherThread.cs` | `ToMicroseconds` → `internal static` |
| `Elmanhg.Domain/TeacherThreads/TeacherThread.Replies.cs` | `EnsureCanReply`, `ReplyWithVoice`, private `Answer`; `Reply` refactored onto them |
| `Elmanhg.Domain/TeacherThreads/TeacherMessage.cs` | `AudioUrl`, `AudioDurationSeconds`, `TranscriptFinal`, `CreateVoice` |
| `Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs`, `Elmanhg.Application/Exceptions/ErrorCodes.cs` | 3 domain + 7 application codes |
| `Elmanhg.Application/Shared/Options/AskTeacherOptions.cs` | 8 voice/transcription options |
| `Elmanhg.Application/Shared/Storage/IFileStorage.cs` | `OpenReadAsync` |
| `Elmanhg.Application/TeacherThreads/Shared/TeacherMessageResult.cs`, `TeacherThreadResultGenerator.cs` | `AudioUrl`, `AudioDurationSeconds` |
| `Elmanhg.Application/TeacherThreads/CanViewTeacherThreadImage/*` → `CanViewTeacherThreadMedia/*` | renamed (git mv); predicate matches `ImageUrl` or `AudioUrl` |
| `Elmanhg.Infrastructure/Storage/{FileStorageProvider,FileStorageOptions,LocalDiskFileStorage}.cs` | `S3` provider, S3 keys, `ResolvePath` + `OpenReadAsync` |
| `Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddFileStorage()`, `ITeacherVoiceDraftRepository` |
| `Elmanhg.Infrastructure/AiService/{AiServiceOptions,AiServiceServiceCollectionExtensions}.cs` | `TranscriptionTimeoutSeconds` 150; typed transcription HttpClient + Fake/Http switch |
| `Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | DbSet, `TeacherMessage` audio columns + filtered index + default, `ConfigureTeacherVoiceDrafts`, 2 WHY-commented consts, global filter line |
| `Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | regenerated (additive only, +112 lines) |
| `Elmanhg.Api/Controllers/TeacherInbox/TeacherInboxController.cs`, `Requests.cs` | 4 actions (`AskTeacherReply` policy), `SendVoiceReplyRequest` |
| `Elmanhg.Api/FileStorage/TeacherThreadMediaMiddleware.cs` | streams through `IFileStorage.OpenReadAsync` after `CanViewTeacherThreadMediaQuery` |
| `Elmanhg.Api/FileStorage/LocalFileStorageExtensions.cs` | deleted (replaced by `MediaStorageExtensions`) |
| `Elmanhg.Api/Program.cs` | `UseMediaStorage()`, `TeacherVoiceTranscriptionWorker` |
| `Elmanhg.Api/Resources/Messages.{ar,en}.resx` | 10 keys |
| `Elmanhg.Api/appsettings.example.json` | 8 `AskTeacher` keys, `AiService:TranscriptionTimeoutSeconds`, 6 `FileStorage:S3*` keys |
| `api/openapi/v1.json` | regenerated by `dotnet build` |
| `Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `UseSetting("AskTeacher:TranscriptionSweepEnabled", "false")` with the plan's comment |
| `Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | `twentyNinth => …"_AddTeacherVoiceReplies"` |
| `Elmanhg.Tests/Builders/TeacherThreadBuilder.cs` | `AnsweredByVoice(teacherId, audioUrl)` |
| `Elmanhg.Tests/Application/Features/TeacherThreads/CanViewTeacherThreadMedia/CanViewTeacherThreadMediaHandlerTests.cs` | moved + type renames (existing bodies unchanged; ctor seeds one extra voice thread) + 3 tests |
| `Elmanhg.Tests/Infrastructure/Storage/LocalDiskFileStorageTests.cs` | +3 tests |
| `Elmanhg.Tests/Infrastructure/AiService/AiServiceServiceCollectionExtensionsTests.cs` | +2 tests |
| `Elmanhg.Tests/Integration/TeacherThreads/TeacherThreadMediaEndpointTests.cs` | +3 tests |
| `Elmanhg.Tests/Integration/TeacherInbox/TeacherInboxTestData.cs` | `VoiceForm`, `WebmBytes` |
| `postman/elmanhg.postman_collection.json` | AskTeacher «Create thread (voice)» (sets `voiceThreadId`); TeacherInbox «Claim thread (voice)», «Get voice reply settings», «Record voice draft» (sets `voiceDraftId`), «Get voice draft», «Send voice reply», placed before «Get inbox (mine)»; variables `voiceThreadId`, `voiceDraftId` |
| `web/src/features/askTeacher/components/{ReplyForm,InboxThreadActions,ThreadMessage,ThreadImage}.tsx` | card moved to `ReplyPanel`; `ReplyPanel`; audio player + «نص التفريغ الصوتي:»; `blobToDataUrl` |
| `web/src/features/askTeacher/i18n/{ar,en}.json`, `web/src/shared/i18n/{ar,en}.json` | 22 feature strings; 10 `errors.*` |
| `web/src/shared/api/generated/**` | `npm run gen:api` |
| `web/src/test/{askTeacherFixtures,teacherInboxFixtures}.ts` | `audioUrl`/`audioDurationSeconds` on literals; `voiceAnsweredThread`, `voiceSettings`, `voiceDraft`, `voiceDraftId` |
| `web/src/features/askTeacher/pages/TeacherThreadPage.test.tsx` | one message literal gets `audioUrl: null, audioDurationSeconds: null` (see Deviations) |
| `docs/ask-teacher.md`, `docs/ai-service.md`, `docs/PRD.md` (§15, §18, §19 Q6), `docs/deployment.md`, `docs/claude-design-prompt.md` §4, `docs/prototype.md` | per the plan's Docs table |
| `.env.example`, `deploy/api.env.example`, `deploy/ai.env.example`, `deploy/docker-compose.prod.yml` | voice + S3 keys (placeholders, secrets empty); `FileStorage__Provider` removed from compose (D21) |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `AWSSDK.S3` **4.0.103.4** | Published 2026-09-23, 6 days old; the lane rule requires ≥ 7 days | Pinned **4.0.103.3** (published 2026-09-14, Apache-2.0). Its dependency `AWSSDK.Core` ≥ 4.0.102.6 was published 2026-09-14 |
| Verify the openai/httpx Python packages | The plan adds no Python package: the Whisper adapter uses the existing `httpx2==2.13.0` (like the embeddings adapter), and no `openai` SDK | Nothing added; `uv sync --locked` unchanged |
| Create A9 `ai/src/elmanhg_ai/eval/__init__.py` | Already exists on main (#91) | Not created |
| `AppDbContextTests`: `twentyEighth => …_AddTeacherVoiceReplies` | Main already has 28 migrations (`twentyEighth` = `_AddTeacherThreadClaims`) | Appended `twentyNinth => …_AddTeacherVoiceReplies` |
| `RecordVoiceDraftValidator`: `.ValidateRequired(TeacherVoiceAudioRequired)` first | Core's `IFormFile` `ValidateRequired` is `NotNull().Must(...)` with the code only on `Must`, so a missing file would report `NotNullValidator` | Added `.NotNull().WithErrorCode(TeacherVoiceAudioRequired)` before it, the same as `UploadLessonImageValidator` |
| `openai_embedding.py` imports `OPENAI_BASE_URL`, `RETRY_BASE_SECONDS`, `RETRY_STATUSES` from `openai_http` | The two `RETRY_*` names would be unused there (ruff F401) | Imports `OPENAI_BASE_URL`, `PROVIDER`, `post_with_retries`; the existing test import `from …openai_embedding import OPENAI_BASE_URL` still resolves |
| F5 toast: `t([...], { ns: 'translation' })` | The shared error strings are the `common` namespace; every hook uses `common:errors.{code}` | Followed the existing pattern (`common:errors.${code}`, fallback `common:errors.UNHANDLED_EXCEPTION`) |
| Existing web tests unchanged except fixture files | `TeacherThreadPage.test.tsx` builds a `TeacherMessageResult` literal inline; the generated type now requires `audioUrl` and `audioDurationSeconds`, so `tsc -b` fails without them | Added `audioUrl: null, audioDurationSeconds: null` to that literal only; no assertion changed |
| `<audio>` elements (F11, F13) | `jsx-a11y/media-has-caption` fails lint | One `eslint-disable-next-line … -- the transcript … is its text alternative` comment on each `<audio>` |

## Build & test
- `git fetch origin main` + `git merge origin/main`: fast-forward `7b0d212..ad70cfb`, no conflicts.
- `npm ci` (web) and `python -m uv sync` (ai): done first.
- **ai** (as `ai-ci.yml`): `uv sync --locked` → "Checked 39 packages"; `ruff format --check .` → "76 files already formatted"; `ruff check .` → "All checks passed!"; `mypy src` → "Success: no issues found in 44 source files"; `pytest -m "not eval" --cov=elmanhg_ai --cov-branch` → **172 passed, 2 deselected**, TOTAL coverage 98% (new modules 95–100%). `ai/openapi/v1.json` regenerated; `test_openapi_document` passes. `test_openai_embedding.py` unchanged and green.
- **api**: `dotnet build api/` → "Build succeeded." (only the pre-existing core-libraries warnings). `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (restored afterwards) → **Test run summary: Passed! total: 3162, failed: 0, succeeded: 3162, skipped: 0**. The story adds 121 `[Fact]`/`[Theory]` methods (Theories expand to more cases). `dotnet format --verify-no-changes` → only `Elmanhg.Tests/Builders/SubscriptionBuilder.cs(57,27) WHITESPACE` outside core-libraries, a file this story does not touch (the known local CRLF noise).
- **web**: `npm run typecheck` exit 0; `npm run lint` (`eslint . --max-warnings=0`) exit 0; `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → "All matched files use Prettier code style!"; `npm test -- --run` → **965 passed (965)**. That full run came before the last two edits (the lint comments and the `getMarkTeacherThreadReadMockHandler()` fix in `TeacherThreadPage.voice.test.tsx`); afterwards `src/features/askTeacher` ran again with 20 files and 94 passing, and the voice page file with 3/3. `npm run gen:api` gives no further diff.
- **Mutation checks** (applied, run, restored; restoration verified by grep): FailAttempt backoff exponent, `EnsureCanReply` removed from record, `IsDueAt` guard removed, worker `RecordFailureAsync` removed, `DisablePayloadSigning` false, transcript added to the completion log, `voice.m4a` → `voice.mp4`, transcript label removed from `ThreadMessage`, recorder auto-stop disabled. Each was caught by the targeted tests. **Not caught:** `.ogg` signature check → `true` (see Notes). The security predicate (`CanViewTeacherThreadMediaQuery` `|| AudioUrl ==`) was verified by reading: `Handle_OwningStudentVoiceAudio_ReturnsTrue` uses a thread with no image, so it depends on the audio match.

## Notes for review
- **Untested signature branches:** the plan's validator tests only cover a webm file with PNG bytes. A mutation of the `.ogg` signature check (and likewise `.m4a`/`.mp4`) passes every test. Consider adding a Theory row per extension in a follow-up.
- `SendVoiceReplyHandler` follows the plan's order: `ReplyWithVoice` changes the tracked thread before `MarkSent` can throw `NOT_READY`/`ALREADY_SENT`. No save happens, so nothing persists, but the in-memory thread is changed for the rest of the request.
- `VoiceReplyPanel` calls `useVoiceRecorder` before the settings load, with `maxSeconds = 0`, because hooks cannot be conditional. The Record button renders only after the settings load, so `start` always has the real limit.
- MSW: `*/api/teacher-inbox/:threadId` also matches `voice-settings`, so in the voice page tests the settings handler is registered first. The real route uses `{threadId:guid}`, so the API is not affected.
- Integration data: `TeacherThreadBuilder` dates (2026-10-01) are later than the factory's clock, so a voice reply can sort before the question. `GetMyThread_VoiceReply…` therefore picks the message by `kind`.
- The `api-media`-backed Local provider is still the default; the compose `FileStorage__Provider` line is gone and the image bakes `appsettings.example.json` (`Local`), so existing hosts are unaffected.
- The worker logs a missing audio file (`TEACHER_VOICE_AUDIO_NOT_FOUND`) as a Warning and records a failed attempt, so the draft ends `Failed` after 4 attempts.
- Lane collision risk with #92: another migration would need the snapshot and the `AppDbContextTests` list merged (then `twentyNinth`/`thirtieth` order), and both OpenAPI files regenerated.
- Deferred (for the orchestrator to file): (1) the Egyptian-dialect eval run (needs ≥ 20 recorded clips + an OpenAI key); (2) a live check of R2/S3 and Whisper (needs credentials).
- My local `api/Elmanhg.Api/appsettings.json` (gitignored) was not given the new sections; code defaults cover them.
