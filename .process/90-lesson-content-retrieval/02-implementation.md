# Implementation — [E8.S2] Lesson content retrieval (#90)

Lane A, branch `feature/90-lesson-content-retrieval`, working tree `D:\Personal\elmanhg`. Nothing committed.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/ContentRetrieval/LessonContentSection.cs | 3 | D1 enum |
| api/Elmanhg.Domain/ContentRetrieval/LessonContentChunkDraft.cs | 6 | D2 draft + `EmbeddingText` |
| api/Elmanhg.Domain/ContentRetrieval/LessonContentChunk.cs | 51 | D3 entity (pgvector `Vector`), dimension guard, 3 WHY constants |
| api/Elmanhg.Domain/ContentRetrieval/LessonContentIndex.cs | 37 | D4 entity, `Create` / `MarkIndexed` |
| api/Elmanhg.Domain/ContentRetrieval/LessonContentMatch.cs | 3 | D5 |
| api/Elmanhg.Domain/ContentRetrieval/ILessonContentChunkRepository.cs | 8 | D6 |
| api/Elmanhg.Domain/ContentRetrieval/ILessonContentIndexRepository.cs | 9 | D7 |
| api/Elmanhg.Application/Shared/AiService/AiEmbeddingInputType.cs | 3 | A1 |
| api/Elmanhg.Application/Shared/AiService/AiEmbeddingRequest.cs | 3 | A2 |
| api/Elmanhg.Application/Shared/AiService/AiEmbeddingResult.cs | 3 | A3 |
| api/Elmanhg.Application/Shared/RichText/RichTextBlockKind.cs | 3 | A4 |
| api/Elmanhg.Application/Shared/RichText/RichTextBlock.cs | 3 | A5 |
| api/Elmanhg.Application/Shared/RichText/IRichTextExtractor.cs | 6 | A6 port |
| api/Elmanhg.Application/Shared/Options/ContentRetrievalOptions.cs | 33 | A7 options (sweep on by default) |
| api/Elmanhg.Application/ContentRetrieval/Shared/LessonContentChunker.cs | 82 | A8 pure chunker |
| api/Elmanhg.Application/ContentRetrieval/Shared/LessonContentChunkPlanner.cs | 26 | A9 section order |
| api/Elmanhg.Application/ContentRetrieval/Shared/LessonContentSearchResult.cs | 3 | A10 |
| api/Elmanhg.Application/ContentRetrieval/Shared/LessonContentMatchResult.cs | 5 | A11 |
| api/Elmanhg.Application/ContentRetrieval/Shared/LessonContentMatchResultGenerator.cs | 24 | A12 reference + score |
| api/Elmanhg.Application/ContentRetrieval/SearchLessonContent/SearchLessonContentQuery.cs | 6 | A13 |
| api/Elmanhg.Application/ContentRetrieval/SearchLessonContent/SearchLessonContentValidator.cs | 24 | A14 |
| api/Elmanhg.Application/ContentRetrieval/SearchLessonContent/SearchLessonContentHandler.cs | 41 | A15 |
| api/Elmanhg.Application/ContentRetrieval/ReindexLessonContent/ReindexLessonContentCommand.cs | 5 | A16 |
| api/Elmanhg.Application/ContentRetrieval/ReindexLessonContent/ReindexLessonContentValidator.cs | 13 | A17 |
| api/Elmanhg.Application/ContentRetrieval/ReindexLessonContent/ReindexLessonContentHandler.cs | 69 | A18, one save, batching in a private method |
| api/Elmanhg.Application/ContentRetrieval/GetStaleLessonContentIds/GetStaleLessonContentIdsQuery.cs | 5 | A19 |
| api/Elmanhg.Application/ContentRetrieval/GetStaleLessonContentIds/GetStaleLessonContentIdsHandler.cs | 14 | A20 |
| api/Elmanhg.Application/ContentRetrieval/RebuildContentIndex/RebuildContentIndexCommand.cs | 11 | A21, audited `ContentIndex.Rebuild` |
| api/Elmanhg.Application/ContentRetrieval/RebuildContentIndex/RebuildContentIndexHandler.cs | 12 | A22 |
| api/Elmanhg.Infrastructure/RichText/RichTextExtractor.cs | 61 | I1 AngleSharp (transitive via HtmlSanitizer) |
| api/Elmanhg.Infrastructure/AiService/FakeEmbeddingVectors.cs | 53 | I2 lexical hashing over `AnswerNormalizer` |
| api/Elmanhg.Infrastructure/ContentRetrieval/LessonContentChunkRepository.cs | 29 | I3 cosine search with query-time servable-at-version filter |
| api/Elmanhg.Infrastructure/ContentRetrieval/LessonContentIndexRepository.cs | 39 | I4 stale ids (see Deviation 1), `ExecuteDeleteAsync` rebuild |
| api/Elmanhg.Infrastructure/Migrations/20260929131149_AddLessonContentIndex.cs | 108 | I5: `vector` extension, 2 tables, 3 indexes, no drop/rename in `Up` |
| api/Elmanhg.Infrastructure/Migrations/20260929131149_AddLessonContentIndex.Designer.cs | 2215 | I5 generated |
| api/Elmanhg.Api/Controllers/ContentRetrieval/ContentRetrievalController.cs | 33 | P-A1, both actions `ContentManage` |
| api/Elmanhg.Api/Controllers/ContentRetrieval/Requests.cs | 3 | P-A2 |
| api/Elmanhg.Api/Workers/LessonContentIndexWorker.cs | 74 | P-A3, mirror of `SubscriptionLapseWorker` |
| api/Elmanhg.Tests/Domain/ContentRetrieval/LessonContentChunkTests.cs | 45 | T1 (tests 1–2) |
| api/Elmanhg.Tests/Domain/ContentRetrieval/LessonContentIndexTests.cs | 43 | T2 (3–4) |
| api/Elmanhg.Tests/Domain/ContentRetrieval/LessonContentChunkDraftTests.cs | 23 | T3 (5–6) |
| api/Elmanhg.Tests/Application/Features/ContentRetrieval/Shared/LessonContentChunkerTests.cs | 102 | T4 (7–14) |
| api/Elmanhg.Tests/Application/Features/ContentRetrieval/Shared/LessonContentChunkPlannerTests.cs | 73 | T5 (15–18) |
| api/Elmanhg.Tests/Application/Features/ContentRetrieval/Shared/LessonContentMatchResultGeneratorTests.cs | 26 | T6 (19, 4 rows) |
| api/Elmanhg.Tests/Application/Features/ContentRetrieval/SearchLessonContent/SearchLessonContentHandlerTests.cs | 125 | T7 (20–26) |
| api/Elmanhg.Tests/Application/Features/ContentRetrieval/SearchLessonContent/SearchLessonContentValidatorTests.cs | 59 | T8 (27–31) |
| api/Elmanhg.Tests/Application/Features/ContentRetrieval/ReindexLessonContent/ReindexLessonContentHandlerTests.cs | 158 | T9 (32–38) |
| api/Elmanhg.Tests/Application/Features/ContentRetrieval/ReindexLessonContent/ReindexLessonContentValidatorTests.cs | 26 | T10 (39–40) |
| api/Elmanhg.Tests/Application/Features/ContentRetrieval/GetStaleLessonContentIds/GetStaleLessonContentIdsHandlerTests.cs | 27 | T11 (41) |
| api/Elmanhg.Tests/Application/Features/ContentRetrieval/RebuildContentIndex/RebuildContentIndexHandlerTests.cs | 30 | T12 (42–43) |
| api/Elmanhg.Tests/Infrastructure/RichText/RichTextExtractorTests.cs | 62 | T13 (44–49) |
| api/Elmanhg.Tests/Api/Workers/LessonContentIndexWorkerTests.cs | 121 | T14 (59–62) |
| api/Elmanhg.Tests/Integration/ContentRetrieval/ContentRetrievalTestData.cs | 74 | T15 helpers |
| api/Elmanhg.Tests/Integration/ContentRetrieval/LessonContentSearchEndpointTests.cs | 164 | T16 (74–82) |
| api/Elmanhg.Tests/Integration/ContentRetrieval/LessonContentReindexTests.cs | 147 | T17 (65–73) |
| api/Elmanhg.Tests/Integration/ContentRetrieval/ContentIndexRebuildEndpointTests.cs | 59 | T18 (83–84) |
| api/Elmanhg.Tests/Integration/ContentRetrieval/ContentRetrievalCollection.cs | 7 | T19, `DisableParallelization = true` |
| ai/src/elmanhg_ai/clients/embedding.py | 43 | P2 protocol, request/reply, cost, provider switch |
| ai/src/elmanhg_ai/clients/fake_embedding.py | 55 | P3 (folding table built from `chr()`, no `\u` literals) |
| ai/src/elmanhg_ai/clients/openai_embedding.py | 119 | P4 raw httpx2, retries 429/5xx/transport with injected sleep |
| ai/src/elmanhg_ai/api/embeddings/__init__.py | 0 | P5 |
| ai/src/elmanhg_ai/api/embeddings/schemas.py | 23 | P6 |
| ai/src/elmanhg_ai/api/embeddings/router.py | 30 | P7 (`embeddings_create_embeddings`) |
| ai/src/elmanhg_ai/pipelines/embed.py | 86 | P8 limits, output check, `embedding.completed` log |
| ai/tests/fixtures/openai/embeddings_success.json | 1 | P9 |
| ai/tests/unit/test_embedding_schemas.py | 40 | tests 88–92 |
| ai/tests/unit/test_fake_embedding.py | 58 | tests 93–97 |
| ai/tests/unit/test_openai_embedding.py | 155 | tests 98–105 |
| ai/tests/unit/test_embed_pipeline.py | 84 | tests 106–111 |
| ai/tests/integration/test_embeddings_endpoint.py | 105 | tests 112–117 |
| docs/content-retrieval.md | 144 | DOC1 |
| web/src/shared/api/generated/content-retrieval/**, zod/content-retrieval/**, model/lessonContent*.ts, model/searchLessonContentRequest.ts | gen | Orval output (not hand-edited) |

## Files modified
| Path | Change |
|---|---|
| api/Directory.Packages.props | `Pgvector` 0.3.2, `Pgvector.EntityFrameworkCore` 0.3.0 |
| api/Elmanhg.Domain/Elmanhg.Domain.csproj / api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj | package references |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | `// CONTENT RETRIEVAL` + `ContentEmbeddingDimensionsInvalid` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `// CONTENT RETRIEVAL` + 3 codes |
| api/Elmanhg.Api/Resources/Messages.ar.resx, Messages.en.resx | 4 keys each, Arabic without tashkeel |
| api/Elmanhg.Application/Shared/AiService/IAiServiceClient.cs | `EmbedAsync` |
| api/Elmanhg.Infrastructure/AiService/FakeAiServiceClient.cs | `FakeEmbeddingModel = "fake"`, `EmbedAsync`, shared Production guard |
| api/Elmanhg.Infrastructure/AiService/HttpAiServiceClient.cs | transport/status/JSON handling extracted to `PostAsync` (logs parameterised with `{Path}`); `EmbedAsync` with reply validation (95 lines) |
| api/Elmanhg.Domain/Questions/IQuestionRepository.cs, api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs | `GetServableInLessonAsync` |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | 2 DbSets, `HasPostgresExtension("vector")`, `ConfigureContentRetrieval`, soft-delete filters |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | extractor + 2 repositories |
| api/Elmanhg.Application/DependencyInjection.cs | `ContentRetrievalOptions` validated on start, `DefaultTopK <= MaxTopK` |
| api/Elmanhg.Api/Program.cs | `.UseVector()`, `LessonContentIndexWorker` hosted after `SubscriptionLapseWorker` |
| api/Elmanhg.Api/appsettings.example.json | `ContentRetrieval` section (also copied into the local, gitignored `appsettings.json`) |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | `UseSetting("ContentRetrieval:IndexSweepEnabled","false")` with WHY; 7 in-memory keys; `Persist Security Info=true` on the test connection string (Deviation 2) |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | 25th `_AddLessonContentIndex`; new `Migrate_LessonContentChunks_CreatesVectorColumn` |
| api/Elmanhg.Tests/Infrastructure/AiService/FakeAiServiceClientTests.cs | tests 50–53 (existing two unchanged) |
| api/Elmanhg.Tests/Infrastructure/AiService/HttpAiServiceClientTests.cs | tests 54–58 (existing tests unchanged) |
| api/openapi/v1.json | regenerated by `dotnet build` |
| web/src/shared/api/generated/index.ts, model/index.ts, zod/index.zod.ts | regenerated by `npm --prefix web run gen:api` |
| postman/elmanhg.postman_collection.json | new `ContentRetrieval` folder after `Lessons`: Search lesson content, Rebuild content index (collection bearer auth, same style as `Lessons`) |
| ai/pyproject.toml, ai/uv.lock | `httpx2==2.13.0` moved to runtime dependencies (lock: only the dev marker moved) |
| ai/src/elmanhg_ai/settings.py | 7 embedding fields + `_openai_needs_key` |
| ai/src/elmanhg_ai/main.py | `embedding_client` injection, lifespan build/close, startup log fields, router |
| ai/src/elmanhg_ai/api/deps.py | `embedding_client_from_app`, `EmbeddingClientDep` |
| ai/tests/conftest.py | `fake_embedding`, `app` passes it, `openai_fixture` |
| ai/tests/unit/test_settings.py | tests 85–87 |
| ai/tests/integration/test_openapi_document.py | `/v1/embeddings` assertion (test 118) |
| ai/openapi/v1.json | regenerated |
| docker-compose.yml, .env.example | 3 embedding variables; commented go-live block with `ContentRetrieval__IndexSweepEnabled` |
| docs/ai-service.md | role, `/v1/embeddings` contract and limits, error rows, .NET validation, 7 config rows, fakes, `embedding.completed` logging, "Go live with OpenAI embeddings" |
| docs/PRD.md | §15 `LessonContentChunk`, `LessonContentIndex`; §18 Embeddings bullet |
| docs/audit-log.md | `RebuildContentIndex` row; "Not audited" entry for the derived index |
| README.md | docs table row; embeddings sentence after the ai paragraph |

`PROGRESS.md` and `scripts/pipeline_orch.py` were already modified before I started (orchestrator changes); I did not touch them.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| I4: `GetStaleLessonIdsAsync` lists only Published lessons (`lessons.Where(LessonCondition)…`). Doc plan: "unpublish removes". | With that query, the sweep never sends an unpublished, archived or deleted lesson to `ReindexLessonContent`, so the handler's "not Published → delete chunks and index" branch (A18 step 4) is unreachable from the sweep and those chunks stay forever. They are hidden at query time, but the table never shrinks. | The query now `Concat`s the outdated Published lessons with index rows whose lesson is no longer Published (ordered by lesson `UpdationDate` / `IndexedAt`, then id; exclusions and `Take` applied after the union). Verified on PostgreSQL. Test 68 has one extra assertion (after unpublish, the lesson is listed stale) and `ContentRetrievalTestData` has one extra helper, `ReadStaleIdsAsync`. |
| Program.cs: `.UseVector()`; no other change. | `UseVector()` makes Npgsql EF build an `NpgsqlDataSource`, and `Database.GetConnectionString()` then drops the password. The existing `QuestionImportEndpointTests.PostImport_ConcurrentConfirmOfSameBatch_ReplaysInsteadOfFailing` opens a raw connection from it and failed ("No password has been provided"). No production code reads the connection string back (grep). | Did not edit that test. In `ApiFactory` (already in scope) the test connection string gets `;Persist Security Info=true`, with a WHY comment. Full suite green afterwards. |
| `HttpAiServiceClient.PostAsync<TResponse>(string path, object request, …)` | Skill §1 prohibits `object` parameters. | `PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken)`, same behaviour. |
| A14: `RuleFor(x => x.Top!.Value)…When(HasValue)` | The repo's pattern for nullable ints is `GetValueOrDefault()` + `When` + `OverridePropertyName` (`ExamBlueprintInputValidator`). | Used the repo pattern; same rule and code. |
| T15/T19: the collection is declared both "in `ContentRetrievalTestData.cs`" and as its own file `ContentRetrievalCollection.cs`. | Contradictory. | Own file only (T19), like `ServableCountCollection`. |

## Build & test
All run locally on Windows (Docker Desktop 29.6.2 up). Other lanes were not disturbed; the only container I started (`elmanhg-ai-lane-a-90`) and its image were removed.

- **Dependencies** (nuget.org / PyPI, checked this session): `Pgvector` 0.3.2 (MIT, published 2025-05-20); `Pgvector.EntityFrameworkCore` 0.3.0 (MIT, 2025-12-21, depends on Npgsql.EF ≥ 9.0.1 and Pgvector ≥ 0.3.2); `httpx2` 2.13.0 (BSD-3-Clause, uploaded 2026-09-14, 15 days old, before `exclude-newer`). **Pgvector.EntityFrameworkCore works on EF 10 / Npgsql.EF 10.0.3**: the build, the migration (`CREATE EXTENSION vector`, `vector(1536)` column: test 64), and the cosine-distance search through EF (tests 74–77) all pass. No fallback needed.
- `dotnet build api/` → `Build succeeded. 0 Warning(s) 0 Error(s)` (incremental; a clean build shows only the existing vendored `core-libraries` warnings).
- New tests only: `dotnet test --project Elmanhg.Tests -- --filter-class …` → 80/80 unit and 32/32 integration passed.
- **CI parity**: moved `api/Elmanhg.Api/appsettings.json` aside, ran `dotnet test api/ -c Release` → `Test run summary: Passed! total: 2711 failed: 0 succeeded: 2711 skipped: 0`, then restored the file. The first run showed 1 failure (Deviation 2), which the fix resolved.
- `dotnet format Elmanhg.slnx --verify-no-changes --exclude core-libraries` → one WHITESPACE finding in `Elmanhg.Tests/Builders/SubscriptionBuilder.cs`, a file I did not touch (local CRLF noise listed in PROGRESS Gotchas). Nothing in the changed files.
- Guard grep (skill §9) on the diff plus new files: only `new HttpClient(_handler)` in the new Http client tests (the existing stub-handler pattern in that test class) and `print(` in `scripts/pipeline_orch.py` (orchestrator's change, not mine).
- **ai, as `ai-ci.yml` runs it**: `uv sync --locked` ok; `ruff format --check .` → 52 files already formatted; `ruff check .` → All checks passed; `mypy src` → Success, 32 source files; `pytest -m "not eval" --cov=elmanhg_ai --cov-branch` → **97 passed**, 97% coverage; `uv export --frozen --no-dev … && pip-audit==2.10.1 -r …` → No known vulnerabilities found; `docker build` ok; container `/health/ready` → `{"status":"ok"}`, and `POST /v1/embeddings` returned model `fake-embedding`, 1536 dimensions.
- **Web**: `npm --prefix web run gen:api` ok; `npm --prefix web run typecheck` clean; `npm --prefix web run lint` clean; `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → All matched files use Prettier code style. Web unit tests were not run (no web source changed, only regenerated client files).
- **Mutation checks** (break on purpose, confirm the test fails, restore; every restore verified):
  - Test 8: the chunker flushes on every piece → failed.
  - Test 20: the query is not trimmed → failed.
  - Tests 36 and 68: the withdrawn branch keeps the index row → both failed.
  - Test 76: the servable-at-version filter is removed from the search → failed.
  - Test 93: the Python fake skips L2 normalisation → failed.
  - No auth or sanitiser code was mutated.

**Test counts:**
- .NET: 83 new test methods (73 in new files, 9 in the Fake/Http client test classes, 1 in `AppDbContextTests`), plus the edited migration-list test. That covers plan rows 1–84.
- Python: 33 new test functions, plus the edited OpenAPI test. That covers rows 85–118.

## Notes for review
- **Stale detection has a known gap (plan Decision 9).** A retirement, or a content edit that sends an Approved question back to Pending, changes `questionsUpdatedAt` only when that question was the newest servable one. Otherwise its chunks stay until the lesson's next re-index. The search never returns them, because it checks servability at the chunk's version at query time. `docs/content-retrieval.md` §Freshness describes this exactly; it does not claim retirement always triggers a re-index. A full fix would store the servable count or id set in the index row. I left that out because it is a design change.
- `LessonContentChunk` keeps three entity constants (`EmbeddingDimensions`, `SectionTitleMaxLength`, `EmbeddingModelMaxLength`), each with a WHY comment. The plan classifies them as schema invariants (skill §8.1 exception). The title length is truncated, never rejected, so no product owner tunes it.
- Positions come from `chunks.Count + 1` instead of a separate counter. The result is the same.
- The planner tests use the real `RichTextExtractor` in all four tests, not only test 18.
- The Python `FakeEmbeddingClient` takes the width from `request.dimensions`, which the pipeline sets from `settings.embedding_dimensions`. So the `fake_embedding` fixture needs no dimensions argument.
- OpenAI failures log at `warning`, which mirrors the Anthropic adapter's `model.call_failed`. The API key is only in the client's default headers and is never logged.
- `/health/ready` still checks only the model client and the prompts. The embedding client is set in the same lifespan step before `yield`, so readiness is unchanged in practice. `health.py` was not in scope.
- Postman: both requests expect 200. The folder description says `lessonId` must be a Published lesson, because the `Lessons` folder ends by deleting its sample lesson, and it notes the 30 s sweep delay.
- The integration collection runs non-parallel. Rebuild deletes every index row, and no other test class touches the index tables.

**Deferred, for the orchestrator to file:** live OpenAI verification and an Arabic retrieval-quality eval (a dataset of at least 20 queries scored against `text-embedding-3-small`). This repo has no OpenAI key. The adapter is unit-tested against the recorded fixture.
