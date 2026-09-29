# Plan — [E8.S2] Lesson content retrieval (#90)

## Goal
After this ships, every Published lesson is automatically split into section-tagged chunks (explanation, objectives, summary, and the stem + explanation of each servable question), embedded through the AI service and stored in pgvector. A retrieval query (`SearchLessonContentQuery`, which #91 calls in-process) returns the top-k chunks of one lesson, each with a section reference the avatar can cite. An admin can run the same retrieval over HTTP to check grounding, and can force a full re-index after changing the embedding provider. Drafts, archived lessons, and non-servable questions are never retrievable.

## Scope
**In:**
- ai/: `POST /v1/embeddings`, the `EmbeddingClient` protocol, a deterministic `FakeEmbeddingClient` (the default), and the real `OpenAiEmbeddingClient` selected by `ELMANHG_AI_EMBEDDING_PROVIDER=openai`.
- api/:
  - The `IAiServiceClient.EmbedAsync` contract, with Fake and Http implementations.
  - `LessonContentChunk` (pgvector) and `LessonContentIndex` entities, and a migration.
  - An HTML-to-blocks extractor, a chunker, the reindex command, the stale-lesson query, and the `LessonContentIndexWorker` sweep (the re-index job).
  - The admin search endpoint, the admin rebuild endpoint, options, error codes, and Postman.
- web/: regenerated Orval client only, because the OpenAPI document changes. No UI.
- docs: new `docs/content-retrieval.md`; updates to `docs/ai-service.md`, `docs/PRD.md` §15 and §18, `docs/audit-log.md`, `README.md`, `.env.example` and `docker-compose.yml`.

**Out:**
- Avatar chat using retrieval, citations in replies, the "اسأل المساعد" button (#91).
- Student-facing retrieval over HTTP.
- An admin UI for search or rebuild.
- An ANN (HNSW) index.
- Cross-lesson or subject-wide retrieval.

**Deferred:**
- **Live OpenAI verification and an Arabic retrieval-quality eval** (a ≥20-query dataset scored against the real model). There is no `OPENAI` key in this repo. The adapter itself is built and unit-tested against recorded fixtures. The orchestrator files this as a `deferred` issue.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Context bundles or embeddings? | **Embeddings in pgvector**, lesson-scoped. | PRD §9.2 says "grounded on lesson content via retrieval (embeddings over explanation/summary/explanation-of-questions)". PRD §18 says "pgvector for Avatar retrieval". The backlog sub-task names "into pgvector". A context-bundle-only design would diverge from the PRD. The full bundle (§9.1) is still built by #91; retrieval adds the ranked, citable chunks. |
| 2 | Who owns the vectors and the SQL? | **The .NET API.** EF migration, `Pgvector.EntityFrameworkCore`, search in `LessonContentChunkRepository`. Python only turns text into vectors. | One migration owner. Python skill delta 4 says no DB in ai/ until needed. Servable filtering must reuse `ServableQuestionSpecification`, which lives in .NET (PROGRESS: "the only definition"). |
| 3 | Embedding provider | **OpenAI `text-embedding-3-small`, 1536 dimensions**, called from ai/ over `httpx2`. `FakeEmbeddingClient` is the default. | Anthropic has no embeddings API. The dev already committed to an OpenAI key for Whisper (#96), so this adds no extra vendor. The model is multilingual (Arabic) and cheap. Calling it with raw `httpx2` avoids a new SDK: skill delta 3 says httpx2 is the HTTP library, and it is already locked at 2.13.0. |
| 4 | Vector dimension | `vector(1536)`, as `LessonContentChunk.EmbeddingDimensions = 1536`, a named constant with a WHY comment (column width). Python `ELMANHG_AI_EMBEDDING_DIMENSIONS` defaults to 1536 and is sent as the OpenAI `dimensions` parameter. | Changing the dimension is a schema migration, so it is an invariant, not an option (skill §8.1 exception). |
| 5 | Retrieval scope | **One lesson** (`LessonId` required). | Every avatar entry point in PRD §9.1 is lesson-bound, and "Global" asks the student to pick a lesson. At about 10–100 chunks per lesson, an exact scan is fine. |
| 6 | Index | B-tree on `LessonContentChunks(LessonId)`. **No HNSW.** | The query is always filtered to one lesson, so an exact cosine scan over its rows is enough. |
| 7 | Which content is indexed | Explanation and summary: chunked by heading. Objectives: one numbered list chunk (split if too long). One chunk group per **servable** question: stem + explanation. | This matches the backlog sub-task: "explanation, summary, objectives and question explanations". The stem gives the explanation retrievable context. |
| 8 | What "only published or servable" means | Indexed only when the lesson is Published and the question is servable. **Checked again at query time:** the lesson must be Published (404 otherwise), and a question chunk needs a servable question whose `Version == chunk.QuestionVersion`. | Query-time filtering is the guarantee. Index-time filtering only keeps the table small. A retire, an edit back to Pending, or an unpublish hides content immediately, with no re-index. |
| 9 | Re-index trigger (story: "on lesson publish and question approval") | **A derived-staleness sweep, not event handlers.** `LessonContentIndex` stores the `Lesson.UpdationDate` and the max servable-question `UpdationDate` that the indexer read. A Published lesson is stale when it has no index row, or either stamp differs from the current DB value. `LessonContentIndexWorker` polls every 30 s. | Publish, approve, edit, retire and reorder all stamp `UpdationDate`, so all of them are caught. Domain events publish *before* commit (`CoreDbContext.SaveChangesAsync`), so an event-driven enqueue could race the commit, and adding entities while `ChangeTracker.Entries` is being enumerated risks "collection modified". Comparing stamps that were read makes the job idempotent and self-healing. Existing published lessons are backfilled automatically. |
| 10 | Race: a commit lands while the indexer reads | Safe. The stamp stored is the value read, so a later commit changes the DB value, and the next sweep sees a mismatch. | Correct by construction. No wall-clock comparison. |
| 11 | Chunk replacement | The reindex handler hard-deletes the lesson's chunks (`Repository.DeleteRange`) and inserts the new set in **one** `SaveChangesAsync`, after the embedding calls. | Chunks are derived, non-audited data. No transaction is held across HTTP. |
| 12 | Model mismatch after a provider switch | Chunks store `EmbeddingModel`, and search matches only chunks whose model equals the query embedding's model. Admin `POST /api/content-index/rebuild` deletes every `LessonContentIndex` row (`ExecuteDeleteAsync`, named bulk maintenance), so the sweep re-embeds everything. | .NET cannot know the Python-side model in advance. A mixed-model cosine would be silently wrong. |
| 13 | HTTP search: who may call it | **Admin only** (`DefaultCodes.ContentManage`). No new policy. | Question chunks contain answers. A student-callable search would leak exam answers (PRD §17 rule 10). Students reach retrieval only through the avatar (#91, in-process `SearchLessonContentQuery`). |
| 14 | Search on a non-Published or unknown lesson | `404 LESSON_NOT_FOUND` (`NotFoundCoreException`). | "Only published is retrievable", and it matches the browse rules. No new code needed. |
| 15 | Search on a Published but not-yet-indexed lesson | `200` with `indexedAt: null, matches: []`. The AI service is **not** called. | Skill §8.7: a normal state is a result, not a 4xx. #91 falls back to the full bundle. |
| 16 | HTTP verb for search | `POST .../content-chunks/search` with a JSON body. | Keeps free text out of URLs and access logs. |
| 17 | Embedding text vs stored content | Stored `Content` is the plain chunk text. The text sent to the embedder is `SectionTitle + "\n" + Content` when there is a title. | The heading improves recall. The stored text stays clean for citation. |
| 18 | Rich text to plain text | New `IRichTextExtractor` (Application/Shared/RichText) with `RichTextExtractor` (Infrastructure, AngleSharp, already transitive via HtmlSanitizer). Output is `RichTextBlock(Kind, Text)`. | No existing capability converts sanitised HTML to text. Headings drive section titles. This is a port, the same as `IRichTextSanitizer`. |
| 19 | Where the chunking logic lives | Static `LessonContentChunker` and `LessonContentChunkPlanner` in `Application/ContentRetrieval/Shared`, pure and fully unit-tested. | It needs `RichTextBlock` (Application). This is not a service layer, just a pure function like a result generator. |
| 20 | Fake vectors | Lexical feature hashing (normalise, tokenise, SHA-256/BLAKE2b bucket and sign, L2-normalised; an empty text gives unit vector `e0`). .NET fake: model `fake`, reusing `AnswerNormalizer` (#70). Python fake: model `fake-embedding`. | Deterministic and offline. Shared words rank higher, so retrieval tests are meaningful. The model names differ on purpose, so a mixed fake index never matches (Decision 12). |
| 21 | Retries for OpenAI | The adapter retries 429, 5xx and transport errors up to `ELMANHG_AI_MODEL_MAX_RETRIES`, with `0.5 s × 2^attempt` backoff through an injected `sleep`. Other 4xx raise immediately. | Python skill §4: "every model call: timeout, retry with backoff, cost record". No tenacity dependency. |
| 22 | Python eval | None in this story. | Skill delta 7: evals start with the first production *prompt* (#91). Embeddings have no prompt. The retrieval-quality eval is Deferred. |
| 23 | Is the reindex audited? | No. Rebuild **is** audited (`ContentIndex.Rebuild`). | Reindex writes derived data, not content (PRD §17 rule 13 covers content and validation changes). Rebuild is a human admin action. |
| 24 | Sweep switch | `ContentRetrieval:IndexSweepEnabled` **defaults to true in code**. The test `ApiFactory` sets it false with `UseSetting`. | PROGRESS: product-critical switches default ON (the #58 lesson). The sweep would race integration tests. |
| 25 | `input_type` in the embeddings contract | Kept (`document` or `query`). The fake and OpenAI adapters ignore it. | Swapping in a provider that distinguishes them (for example Voyage) needs no contract change. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Directory.Packages.props` | Add `<PackageVersion Include="Pgvector" Version="0.3.2" />` and `<PackageVersion Include="Pgvector.EntityFrameworkCore" Version="0.3.0" />`. Both are MIT; 0.3.0 depends on Npgsql.EF ≥ 9.0.1, which our 10.0.3 satisfies. |
| `api/Elmanhg.Domain/Elmanhg.Domain.csproj` | `<PackageReference Include="Pgvector" />` |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | `<PackageReference Include="Pgvector.EntityFrameworkCore" />` |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | New group `// CONTENT RETRIEVAL` with `ContentEmbeddingDimensionsInvalid = "CONTENT_EMBEDDING_DIMENSIONS_INVALID"` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | New group `// CONTENT RETRIEVAL` with `ContentSearchQueryRequired`, `ContentSearchQueryTooLong`, `ContentSearchTopInvalid` (values below) |
| `api/Elmanhg.Application/Shared/AiService/IAiServiceClient.cs` | Add `Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/AiService/FakeAiServiceClient.cs` | Add `public const string FakeEmbeddingModel = "fake";` and `EmbedAsync`: throws `ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable)` in Production. Otherwise it returns `new AiEmbeddingResult(FakeEmbeddingModel, LessonContentChunk.EmbeddingDimensions, request.Texts.Select(x => FakeEmbeddingVectors.Create(x, LessonContentChunk.EmbeddingDimensions)).ToList(), 0)`. |
| `api/Elmanhg.Infrastructure/AiService/HttpAiServiceClient.cs` | Extract the transport/status/JSON handling into `private async Task<TResponse?> PostAsync<TResponse>(string path, object request, CancellationToken cancellationToken)`: same catch filter, same `ServiceUnavailableCoreException`, log messages parameterised with `{Path}`. `ChatAsync` keeps the blank-reply guard. Add `private const string EmbeddingsPath = "v1/embeddings";` and `EmbedAsync`. `EmbedAsync` throws `ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable)` (logged Error, "AI service returned an invalid embeddings reply.") unless all of these hold: result non-null, `Model` non-blank, `Dimensions > 0`, `Embeddings.Count == request.Texts.Count`, and every vector `Length == Dimensions`. |
| `api/Elmanhg.Domain/Questions/IQuestionRepository.cs` | Add `Task<List<Question>> GetServableInLessonAsync(Guid lessonId, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | Implement: `_dbSet.WhereServable(_context.Set<Lesson>()).Where(x => x.LessonId == lessonId).OrderBy(x => x.CreationDate).ThenBy(x => x.Id).AsNoTracking().ToListAsync(...)` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `DbSet<LessonContentChunk> LessonContentChunks { get; set; }` and `DbSet<LessonContentIndex> LessonContentIndexes { get; set; }`. `modelBuilder.HasPostgresExtension("vector");` in `OnModelCreating`. New `ConfigureContentRetrieval(modelBuilder)` (below). Add both entities to `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddSingleton<IRichTextExtractor, RichTextExtractor>();`, `services.AddScoped<ILessonContentChunkRepository, LessonContentChunkRepository>();`, `services.AddScoped<ILessonContentIndexRepository, LessonContentIndexRepository>();` |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<ContentRetrievalOptions>().BindConfiguration(ContentRetrievalOptions.SectionName).ValidateDataAnnotations().Validate(x => x.DefaultTopK <= x.MaxTopK, "ContentRetrieval:DefaultTopK must not exceed MaxTopK.").ValidateOnStart();` |
| `api/Elmanhg.Api/Program.cs` | `npgsql => npgsql.EnableRetryOnFailure().UseVector()` (`using Pgvector.EntityFrameworkCore;`). `builder.Services.AddHostedService<LessonContentIndexWorker>();` after `SubscriptionLapseWorker`. |
| `api/Elmanhg.Api/appsettings.example.json` | `"ContentRetrieval": { "IndexSweepEnabled": true, "IndexSweepIntervalSeconds": 30, "IndexSweepBatchSize": 20, "ChunkMaxCharacters": 1500, "EmbeddingBatchSize": 32, "DefaultTopK": 5, "MaxTopK": 20, "QueryMaxLength": 2000 }` |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Four keys (see Error codes) |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `builder.UseSetting("ContentRetrieval:IndexSweepEnabled", "false");` with a WHY comment (the sweep would race the tests, which reindex through the mediator). Add in-memory keys `ContentRetrieval:IndexSweepIntervalSeconds=30`, `IndexSweepBatchSize=20`, `ChunkMaxCharacters=1500`, `EmbeddingBatchSize=32`, `DefaultTopK=5`, `MaxTopK=20`, `QueryMaxLength=2000`. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `twentyFifth => twentyFifth.Should().EndWith("_AddLessonContentIndex")` (the accepted pattern). Add test `Migrate_LessonContentChunks_CreatesVectorColumn`. |
| `api/Elmanhg.Tests/Infrastructure/AiService/FakeAiServiceClientTests.cs` | Add the tests listed in the Test plan |
| `api/Elmanhg.Tests/Infrastructure/AiService/HttpAiServiceClientTests.cs` | Add the tests listed in the Test plan |
| `api/openapi/v1.json` | Regenerated by `dotnet build` |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api`. Expect a new `content-retrieval/` tag folder, new `model/` files and `zod/` files; do not edit them by hand. |
| `postman/elmanhg.postman_collection.json` | New folder `ContentRetrieval` after `Lessons`, containing: (1) `Search lesson content` `POST {{baseUrl}}/api/lessons/{{lessonId}}/content-chunks/search`, body `{"query":"قانون أوم","top":5,"includeQuestionExplanations":true}`, admin auth as in the `Lessons` folder; (2) `Rebuild content index` `POST {{baseUrl}}/api/content-index/rebuild`. Mirror the variable and auth style of the `Lessons` folder. |
| `ai/pyproject.toml` | Move `"httpx2==2.13.0"` from the dev group into `[project].dependencies`. It is already the locked transitive version; the implementer verifies its licence per skill §12. Run `uv lock` and commit `uv.lock`. |
| `ai/src/elmanhg_ai/settings.py` | New fields and validator (see Files to create, row P1) |
| `ai/src/elmanhg_ai/main.py` | `create_app(settings=None, *, model_client=None, embedding_client=None)`. `app.state.injected_embedding_client`. Lifespan: `app.state.embedding_client = injected or build_embedding_client(settings)`. `logger.info("service.started", …, embedding_provider=settings.embedding_provider, embedding_model=settings.embedding_model)`. `finally`: `await embedding_client.aclose()` after the model client. `app.include_router(embeddings_router.router)`. |
| `ai/src/elmanhg_ai/api/deps.py` | `embedding_client_from_app(request) -> EmbeddingClient` (raises `ServiceNotReadyError` if missing) and `EmbeddingClientDep` |
| `ai/tests/conftest.py` | Fixture `fake_embedding() -> FakeEmbeddingClient` (dimensions from `settings.embedding_dimensions`). `app` fixture passes `embedding_client=fake_embedding`. Fixture `openai_fixture() -> Callable[[str], str]` reading `tests/fixtures/openai/<name>`. |
| `ai/tests/unit/test_settings.py` | Add the 3 tests listed |
| `ai/tests/integration/test_openapi_document.py` | Add `assert "/v1/embeddings" in document["paths"]` (intentional behaviour change) |
| `ai/openapi/v1.json` | Regenerated: `uv run python -m elmanhg_ai.openapi_export` |
| `docker-compose.yml` | `ai.environment`: `ELMANHG_AI_EMBEDDING_PROVIDER: ${ELMANHG_AI_EMBEDDING_PROVIDER:-fake}`, `ELMANHG_AI_OPENAI_API_KEY: ${ELMANHG_AI_OPENAI_API_KEY:-}`, `ELMANHG_AI_EMBEDDING_MODEL: ${ELMANHG_AI_EMBEDDING_MODEL:-text-embedding-3-small}` |
| `.env.example` | Commented `# ELMANHG_AI_EMBEDDING_PROVIDER=openai`, `# ELMANHG_AI_OPENAI_API_KEY=`, `# ELMANHG_AI_EMBEDDING_MODEL=text-embedding-3-small`, and `# ContentRetrieval__IndexSweepEnabled=true`, with a one-line pointer to `docs/content-retrieval.md` |
| `docs/ai-service.md` | **Role:** "…answers avatar chat turns and embeds text for lesson retrieval". **Contract:** a `POST /v1/embeddings` section with request/response JSON, field rules and limits. **Config table:** 7 new variables. **Fakes:** Python `FakeEmbeddingClient` and .NET fake embeddings. **Health/logging:** the `embedding.completed` line. **Go live:** a new "Go live with OpenAI embeddings" list (set the provider and key, then `POST /api/content-index/rebuild`). |
| `docs/PRD.md` | §15: add `LessonContentChunk(id, lesson_id, section[Explanation|Objectives|Summary|QuestionExplanation], section_title?, position, question_id?, question_version?, content, embedding vector(1536), embedding_model, created_at)  -- derived; docs/content-retrieval.md` and `LessonContentIndex(id, lesson_id, source_updated_at, questions_updated_at?, chunk_count, embedding_model?, indexed_at)`. §18: add the bullet `- **Embeddings**: OpenAI text-embedding-3-small (1536) through the AI service; Anthropic has no embeddings API. Fake by default.` |
| `docs/audit-log.md` | Row: `\| RebuildContentIndex \| \`ContentIndex.Rebuild\` \| ContentIndex \| none (bulk delete of derived index state; no diff) \|` |
| `README.md` | Docs table row `\| [docs/content-retrieval.md](docs/content-retrieval.md) \| Lesson content retrieval: chunking, re-indexing, search, rebuild \|`. One sentence after the ai paragraph (line ~60): embeddings are fake until `ELMANHG_AI_EMBEDDING_PROVIDER=openai`. |

## Files to create

### api/ — Domain (`namespace Elmanhg.Domain.ContentRetrieval;`)
| # | Path | Type | Contract |
|---|------|------|----------|
| D1 | `api/Elmanhg.Domain/ContentRetrieval/LessonContentSection.cs` | enum | `public enum LessonContentSection { Explanation, Objectives, Summary, QuestionExplanation }` |
| D2 | `api/Elmanhg.Domain/ContentRetrieval/LessonContentChunkDraft.cs` | sealed record | `public sealed record LessonContentChunkDraft(LessonContentSection Section, string? SectionTitle, int Position, Guid? QuestionId, int? QuestionVersion, string Content) { public string EmbeddingText => SectionTitle is null ? Content : $"{SectionTitle}\n{Content}"; }` |
| D3 | `api/Elmanhg.Domain/ContentRetrieval/LessonContentChunk.cs` | class `LessonContentChunk : Entity` | Constants, each with a WHY comment (column widths): `EmbeddingDimensions = 1536` ("vector column width; changing it is a migration"), `SectionTitleMaxLength = 200`, `EmbeddingModelMaxLength = 200`. Properties, all `{ get; private set; }`: `Guid LessonId`, `LessonContentSection Section`, `string? SectionTitle`, `int Position`, `Guid? QuestionId`, `int? QuestionVersion`, `string Content`, `Vector Embedding` (`Pgvector`), `string EmbeddingModel`, `DateTimeOffset CreatedAt`. Private constructor `(Guid id) : base(id)`. `public static LessonContentChunk Create(Guid lessonId, LessonContentChunkDraft draft, float[] embedding, string embeddingModel, DateTimeOffset createdAt)`: guard `embedding.Length != EmbeddingDimensions` → `throw new BusinessRuleViolationCoreException(ErrorCodes.ContentEmbeddingDimensionsInvalid)`, then copy the draft fields with `Embedding = new Vector(embedding)` and `Id = Guid.NewGuid()`. |
| D4 | `api/Elmanhg.Domain/ContentRetrieval/LessonContentIndex.cs` | class `LessonContentIndex : Entity` | Properties (`private set`): `Guid LessonId`, `DateTimeOffset SourceUpdatedAt`, `DateTimeOffset? QuestionsUpdatedAt`, `int ChunkCount`, `string? EmbeddingModel`, `DateTimeOffset IndexedAt`. `public static LessonContentIndex Create(Guid lessonId, DateTimeOffset sourceUpdatedAt, DateTimeOffset? questionsUpdatedAt, int chunkCount, string? embeddingModel, DateTimeOffset indexedAt)`. `public void MarkIndexed(DateTimeOffset sourceUpdatedAt, DateTimeOffset? questionsUpdatedAt, int chunkCount, string? embeddingModel, DateTimeOffset indexedAt)` sets all five. |
| D5 | `api/Elmanhg.Domain/ContentRetrieval/LessonContentMatch.cs` | sealed record | `public sealed record LessonContentMatch(Guid ChunkId, LessonContentSection Section, string? SectionTitle, int Position, Guid? QuestionId, string Content, double Distance);` |
| D6 | `api/Elmanhg.Domain/ContentRetrieval/ILessonContentChunkRepository.cs` | interface | `: IRepository<LessonContentChunk>` with `Task<List<LessonContentMatch>> SearchAsync(Guid lessonId, float[] queryEmbedding, string embeddingModel, int top, bool includeQuestionExplanations, CancellationToken cancellationToken);` |
| D7 | `api/Elmanhg.Domain/ContentRetrieval/ILessonContentIndexRepository.cs` | interface | `: IRepository<LessonContentIndex>` with `Task<List<Guid>> GetStaleLessonIdsAsync(IReadOnlyCollection<Guid> excludedIds, int batchSize, CancellationToken cancellationToken);` and `Task DeleteAllAsync(CancellationToken cancellationToken);` |

### api/ — Application
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `api/Elmanhg.Application/Shared/AiService/AiEmbeddingInputType.cs` | enum | `public enum AiEmbeddingInputType { Document, Query }` (wire form: `document` / `query` via the existing camelCase converter) |
| A2 | `api/Elmanhg.Application/Shared/AiService/AiEmbeddingRequest.cs` | sealed record | `public sealed record AiEmbeddingRequest(AiEmbeddingInputType InputType, IReadOnlyList<string> Texts);` |
| A3 | `api/Elmanhg.Application/Shared/AiService/AiEmbeddingResult.cs` | sealed record | `public sealed record AiEmbeddingResult(string Model, int Dimensions, IReadOnlyList<float[]> Embeddings, int InputTokens);` |
| A4 | `api/Elmanhg.Application/Shared/RichText/RichTextBlockKind.cs` | enum | `public enum RichTextBlockKind { Heading, Paragraph }` |
| A5 | `api/Elmanhg.Application/Shared/RichText/RichTextBlock.cs` | sealed record | `public sealed record RichTextBlock(RichTextBlockKind Kind, string Text);` |
| A6 | `api/Elmanhg.Application/Shared/RichText/IRichTextExtractor.cs` | interface | `IReadOnlyList<RichTextBlock> ExtractBlocks(string? html);` |
| A7 | `api/Elmanhg.Application/Shared/Options/ContentRetrievalOptions.cs` | sealed class | `SectionName = "ContentRetrieval"`. `bool IndexSweepEnabled = true`. `[Range(5, 86400)] int IndexSweepIntervalSeconds = 30`. `[Range(1, 500)] int IndexSweepBatchSize = 20`. `[Range(200, 6000)] int ChunkMaxCharacters = 1500` (6000 + a 200-character title stays under the AI service's 8000-character text limit; the WHY goes on the attribute line). `[Range(1, 64)] int EmbeddingBatchSize = 32` (≤ `ELMANHG_AI_EMBEDDING_MAX_TEXTS`). `[Range(1, 50)] int DefaultTopK = 5`. `[Range(1, 50)] int MaxTopK = 20`. `[Range(1, 4000)] int QueryMaxLength = 2000`. |
| A8 | `api/Elmanhg.Application/ContentRetrieval/Shared/LessonContentChunker.cs` | static class | `public static List<LessonContentChunkDraft> Chunk(LessonContentSection section, Guid? questionId, int? questionVersion, IReadOnlyList<RichTextBlock> blocks, bool headingsAsTitles, int maxCharacters)`. Algorithm: `position = 0; title = null; buffer = []` (buffer length = sum of piece lengths plus one per join). For each block: if `Kind == Heading && headingsAsTitles`, then `Flush(); title = Truncate(block.Text, LessonContentChunk.SectionTitleMaxLength); continue;`. Otherwise, for each `piece` in `Split(block.Text, maxCharacters)`: if the buffer is non-empty and `bufferLength + 1 + piece.Length > maxCharacters`, `Flush()`; then append `piece`. At the end, `Flush()`. `Flush`: if the buffer is non-empty, `position++`, emit `new LessonContentChunkDraft(section, title, position, questionId, questionVersion, string.Join("\n", buffer))`, and clear. `Split(text, max)`: while `text.Length > max`, cut at the last `' '` at index ≤ `max` (a hard cut at `max` when none is found or the index is 0), trim, and yield; then yield the remainder when it is non-empty. Blocks with blank text are skipped. |
| A9 | `api/Elmanhg.Application/ContentRetrieval/Shared/LessonContentChunkPlanner.cs` | static class | `public static List<LessonContentChunkDraft> Plan(Lesson lesson, IReadOnlyList<Question> servableQuestions, IRichTextExtractor extractor, int maxCharacters)`. The result concatenates, in order: (1) `Chunk(Explanation, null, null, extractor.ExtractBlocks(lesson.Explanation), headingsAsTitles: true, max)`; (2) `Chunk(Objectives, null, null, objectives ordered by Order → Paragraph($"{index + 1}. {text}"), false, max)`; (3) `Chunk(Summary, null, null, extractor.ExtractBlocks(lesson.Summary), true, max)`; (4) for each question in the given order, `Chunk(QuestionExplanation, q.Id, q.Version, [.. ExtractBlocks(q.Stem), .. ExtractBlocks(q.Explanation)], false, max)`. |
| A10 | `api/Elmanhg.Application/ContentRetrieval/Shared/LessonContentSearchResult.cs` | sealed record (admin/internal, no LocalizedText) | `public sealed record LessonContentSearchResult(Guid LessonId, DateTimeOffset? IndexedAt, List<LessonContentMatchResult> Matches);` |
| A11 | `api/Elmanhg.Application/ContentRetrieval/Shared/LessonContentMatchResult.cs` | sealed record | `public sealed record LessonContentMatchResult(Guid ChunkId, LessonContentSection Section, string? SectionTitle, int Position, Guid? QuestionId, string Reference, string Content, double Score);` |
| A12 | `api/Elmanhg.Application/ContentRetrieval/Shared/LessonContentMatchResultGenerator.cs` | static class | `Generate(LessonContentMatch match)`: `Score = 1 - match.Distance`. `Reference = match.Section switch { Explanation => $"explanation-{p}", Objectives => $"objectives-{p}", Summary => $"summary-{p}", QuestionExplanation => $"question-{match.QuestionId}-{p}", _ => $"section-{p}" }`. |
| A13 | `api/Elmanhg.Application/ContentRetrieval/SearchLessonContent/SearchLessonContentQuery.cs` | sealed record | `public sealed record SearchLessonContentQuery(Guid LessonId, string Query, int? Top, bool IncludeQuestionExplanations = true) : IRequest<LessonContentSearchResult>;` |
| A14 | `.../SearchLessonContent/SearchLessonContentValidator.cs` | validator(`IOptions<ContentRetrievalOptions>`) | `LessonId.ValidateRequired(ErrorCodes.LessonIdRequired)`. `Query.ValidateRequired(ErrorCodes.ContentSearchQueryRequired).ValidateMaxLength(options.QueryMaxLength, ErrorCodes.ContentSearchQueryTooLong)`. `RuleFor(x => x.Top!.Value).ValidateRange(1, options.MaxTopK, ErrorCodes.ContentSearchTopInvalid).When(x => x.Top.HasValue)`. |
| A15 | `.../SearchLessonContent/SearchLessonContentHandler.cs` | handler | Constructor: `(ILessonRepository lessonRepository, ILessonContentIndexRepository lessonContentIndexRepository, ILessonContentChunkRepository lessonContentChunkRepository, IAiServiceClient aiServiceClient, IOptions<ContentRetrievalOptions> contentRetrievalOptions)`. Steps: (1) `lesson = await lessonRepository.GetByIdAsync(request.LessonId, ct, asNoTracking: true)`; if null or `lesson.State != LessonState.Published`, throw `NotFoundCoreException(ErrorCodes.LessonNotFound)`. (2) `index = await lessonContentIndexRepository.FirstOrDefaultAsync(x => x.LessonId == lesson.Id, ct, asNoTracking: true)`. (3) If `index is null \|\| index.ChunkCount == 0`, return `new(lesson.Id, index?.IndexedAt, [])`. (4) `embedding = await aiServiceClient.EmbedAsync(new AiEmbeddingRequest(AiEmbeddingInputType.Query, [request.Query.Trim()]), ct)`. (5) If `embedding.Embeddings[0].Length != LessonContentChunk.EmbeddingDimensions`, throw `ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable)`. (6) `matches = await lessonContentChunkRepository.SearchAsync(lesson.Id, embedding.Embeddings[0], embedding.Model, request.Top ?? options.DefaultTopK, request.IncludeQuestionExplanations, ct)`. (7) Return `new(lesson.Id, index.IndexedAt, matches.Select(LessonContentMatchResultGenerator.Generate).ToList())`. |
| A16 | `.../ContentRetrieval/ReindexLessonContent/ReindexLessonContentCommand.cs` | sealed record | `public sealed record ReindexLessonContentCommand(Guid LessonId) : IRequest;` (not auditable, Decision 23) |
| A17 | `.../ReindexLessonContent/ReindexLessonContentValidator.cs` | validator | `LessonId.ValidateRequired(ErrorCodes.LessonIdRequired)` |
| A18 | `.../ReindexLessonContent/ReindexLessonContentHandler.cs` | handler | Constructor: `(ILessonRepository lessonRepository, IQuestionRepository questionRepository, ILessonContentChunkRepository lessonContentChunkRepository, ILessonContentIndexRepository lessonContentIndexRepository, IRichTextExtractor richTextExtractor, IAiServiceClient aiServiceClient, IOptions<ContentRetrievalOptions> contentRetrievalOptions, TimeProvider timeProvider)`. Steps: (1) `lesson = GetWithObjectivesAsync(id, asNoTracking: true, ct)`. (2) `index = FirstOrDefaultAsync(x => x.LessonId == id, ct)` (tracked). (3) `existing = lessonContentChunkRepository.FindAsync(x => x.LessonId == id, ct)` (tracked). (4) If `lesson is null \|\| lesson.State != Published`: `DeleteRange(existing)`; if `index != null`, `Delete(index)`; `SaveChangesAsync`; return. (5) `questions = questionRepository.GetServableInLessonAsync(id, ct)`. (6) `drafts = LessonContentChunkPlanner.Plan(lesson, questions, richTextExtractor, options.ChunkMaxCharacters)`. (7) `questionsUpdatedAt = questions.Count == 0 ? null : questions.Max(x => x.UpdationDate)`; `now = timeProvider.GetUtcNow()`. (8) For each consecutive batch of `options.EmbeddingBatchSize` drafts (`drafts.Chunk(size)`), sequentially: `result = await aiServiceClient.EmbedAsync(new(AiEmbeddingInputType.Document, batch.Select(x => x.EmbeddingText).ToList()), ct)`; for each index i, `LessonContentChunk.Create(lesson.Id, batch[i], result.Embeddings[i], result.Model, now)`; `model ??= result.Model`. No drafts means no call and `model = null`. (9) `DeleteRange(existing)`, then `AddRangeAsync(chunks)`. (10) If `index is null`, `AddAsync(LessonContentIndex.Create(lesson.Id, lesson.UpdationDate, questionsUpdatedAt, chunks.Count, model, now))`; else `index.MarkIndexed(...)` with the same arguments. (11) `await lessonContentIndexRepository.SaveChangesAsync(ct)` exactly once. Nothing is saved when any embed call throws. Keep the file ≤ 100 lines; the batching may be a private method. |
| A19 | `.../ContentRetrieval/GetStaleLessonContentIds/GetStaleLessonContentIdsQuery.cs` | sealed record | `public sealed record GetStaleLessonContentIdsQuery(IReadOnlyCollection<Guid> ExcludedIds) : IRequest<List<Guid>>;` |
| A20 | `.../GetStaleLessonContentIds/GetStaleLessonContentIdsHandler.cs` | handler | Constructor: `(ILessonContentIndexRepository lessonContentIndexRepository, IOptions<ContentRetrievalOptions> contentRetrievalOptions)`. Returns `GetStaleLessonIdsAsync(request.ExcludedIds, options.IndexSweepBatchSize, ct)`. |
| A21 | `.../ContentRetrieval/RebuildContentIndex/RebuildContentIndexCommand.cs` | sealed record | `public sealed record RebuildContentIndexCommand : IRequest, IAuditableCommand { AuditAction => "ContentIndex.Rebuild"; AuditResourceType => "ContentIndex"; AuditResourceId => null; }` |
| A22 | `.../RebuildContentIndex/RebuildContentIndexHandler.cs` | handler | Constructor: `(ILessonContentIndexRepository lessonContentIndexRepository)`. `await lessonContentIndexRepository.DeleteAllAsync(ct)`. No `SaveChangesAsync`: `ExecuteDeleteAsync` commits by itself. |

### api/ — Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| I1 | `api/Elmanhg.Infrastructure/RichText/RichTextExtractor.cs` | `sealed class RichTextExtractor : IRichTextExtractor` | Null or blank input returns `[]`. Otherwise `new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html)` and walk `document.Body!.ChildNodes`. `h2`/`h3` → Heading(`Text(el)`). `ul` → one Paragraph per child `li`: `"- " + Text(li)`. `ol` → `$"{start + i}. " + Text(li)`, where `start` is the `start` attribute, default 1. `div[data-type=block-math]` → Paragraph(`$"$${latex}$$"`). `hr` is skipped. Any other element or a non-blank text node → Paragraph(`Text(node)`). `Text(node)` walks recursively: text nodes give their content; `span[data-type=inline-math]` → `$"${latex}$"`; `div[data-type=block-math]` → `$"$${latex}$$"`; `img` → its `alt`; `br` → `" "`; any other element → its children joined. The result collapses whitespace runs to one space and is trimmed. Blocks with empty text are dropped. |
| I2 | `api/Elmanhg.Infrastructure/AiService/FakeEmbeddingVectors.cs` | static class | `public static float[] Create(string text, int dimensions)`. `normalized = AnswerNormalizer.Normalize(text, AnswerNormalization.Default)`; tokens are maximal runs of `char.IsLetterOrDigit`. For each token: `hash = SHA256.HashData(Encoding.UTF8.GetBytes(token))`; `index = (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % (uint)dimensions)`; `vector[index] += (hash[4] & 1) == 0 ? 1f : -1f`. If the L2 norm is 0, set `vector[0] = 1f`; otherwise divide by the norm. |
| I3 | `api/Elmanhg.Infrastructure/ContentRetrieval/LessonContentChunkRepository.cs` | `LessonContentChunkRepository(AppDbContext context) : Repository<LessonContentChunk>(context), ILessonContentChunkRepository` | `SearchAsync`: `var vector = new Vector(queryEmbedding); var servable = _context.Set<Question>().WhereServable(_context.Set<Lesson>());` then `_dbSet.Where(x => x.LessonId == lessonId && x.EmbeddingModel == embeddingModel).Where(x => x.QuestionId == null \|\| (includeQuestionExplanations && servable.Any(q => q.Id == x.QuestionId && q.Version == x.QuestionVersion))).OrderBy(x => x.Embedding.CosineDistance(vector)).ThenBy(x => x.Id).Take(top).Select(x => new LessonContentMatch(x.Id, x.Section, x.SectionTitle, x.Position, x.QuestionId, x.Content, x.Embedding.CosineDistance(vector))).AsNoTracking().ToListAsync(ct).ConfigureAwait(false)` (one operator per line) |
| I4 | `api/Elmanhg.Infrastructure/ContentRetrieval/LessonContentIndexRepository.cs` | `LessonContentIndexRepository(AppDbContext context) : Repository<LessonContentIndex>(context), ILessonContentIndexRepository` | `GetStaleLessonIdsAsync`: `lessons = _context.Set<Lesson>(); servable = _context.Set<Question>().WhereServable(lessons);` then `lessons.Where(ServableQuestionSpecification.LessonCondition).Where(l => !excludedIds.Contains(l.Id)).Where(l => !_dbSet.Any(i => i.LessonId == l.Id && i.SourceUpdatedAt == l.UpdationDate && i.QuestionsUpdatedAt == servable.Where(q => q.LessonId == l.Id).Max(q => (DateTimeOffset?)q.UpdationDate))).OrderBy(l => l.UpdationDate).ThenBy(l => l.Id).Select(l => l.Id).Take(batchSize).ToListAsync(...)`. `DeleteAllAsync`: `await _dbSet.ExecuteDeleteAsync(ct)`, with a WHY comment: derived, non-audited index state; the command itself is audited. |
| I5 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddLessonContentIndex.cs` (+ `.Designer.cs`, updated `AppDbContextModelSnapshot.cs`) | migration | Generated with `dotnet ef migrations add AddLessonContentIndex -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. It must contain `CREATE EXTENSION vector` (from `HasPostgresExtension`), both tables and their indexes, and no Drop or Rename operations. |

`ConfigureContentRetrieval` (inside `AppDbContext`):
```
modelBuilder.Entity<LessonContentChunk>(builder =>
{
    builder.Property(x => x.Id).ValueGeneratedNever();
    builder.Property(x => x.Section).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
    builder.Property(x => x.SectionTitle).HasMaxLength(LessonContentChunk.SectionTitleMaxLength);
    builder.Property(x => x.Content).IsRequired();
    builder.Property(x => x.Embedding).HasColumnType($"vector({LessonContentChunk.EmbeddingDimensions})").IsRequired();
    builder.Property(x => x.EmbeddingModel).IsRequired().HasMaxLength(LessonContentChunk.EmbeddingModelMaxLength);
    builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => x.LessonId);
    builder.HasIndex(x => x.QuestionId);
});
modelBuilder.Entity<LessonContentIndex>(builder =>
{
    builder.Property(x => x.Id).ValueGeneratedNever();
    builder.Property(x => x.EmbeddingModel).HasMaxLength(LessonContentChunk.EmbeddingModelMaxLength);
    builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => x.LessonId).IsUnique();
});
```

### api/ — Api
| # | Path | Type | Contract |
|---|------|------|----------|
| P-A1 | `api/Elmanhg.Api/Controllers/ContentRetrieval/ContentRetrievalController.cs` | controller | `[ApiController][Route("api")][Authorize] public class ContentRetrievalController(IMediator mediator) : ControllerBase`. (a) `[HttpPost("lessons/{lessonId:guid}/content-chunks/search", Name = "SearchLessonContent")] [Authorize(Policy = DefaultCodes.ContentManage)] [ProducesResponseType<LessonContentSearchResult>(200)] Search([FromRoute] Guid lessonId, [FromBody] SearchLessonContentRequest request, CancellationToken)` sends `new SearchLessonContentQuery(lessonId, request.Query, request.Top, request.IncludeQuestionExplanations ?? true)` and returns `Ok(result)`. (b) `[HttpPost("content-index/rebuild", Name = "RebuildContentIndex")] [Authorize(Policy = DefaultCodes.ContentManage)] [ProducesResponseType(200)] Rebuild(CancellationToken)` sends `new RebuildContentIndexCommand()` and returns `Ok()`. |
| P-A2 | `api/Elmanhg.Api/Controllers/ContentRetrieval/Requests.cs` | record | `public sealed record SearchLessonContentRequest(string Query, int? Top, bool? IncludeQuestionExplanations);` |
| P-A3 | `api/Elmanhg.Api/Workers/LessonContentIndexWorker.cs` | `sealed class LessonContentIndexWorker(IServiceScopeFactory scopeFactory, IOptions<ContentRetrievalOptions> contentRetrievalOptions, TimeProvider timeProvider, ILogger<LessonContentIndexWorker> logger) : BackgroundService` | A line-for-line mirror of `SubscriptionLapseWorker`: returns immediately when `!IndexSweepEnabled`; `PeriodicTimer(IndexSweepIntervalSeconds, timeProvider)`; a `_deferredIds` HashSet cleared when a page is smaller than `IndexSweepBatchSize`; `GetStaleLessonContentIdsQuery([.. _deferredIds])` in its own scope (failure → `LogError("Listing stale lesson content failed.")`, `[]`); `ReindexLessonContentCommand(id)` in a new scope per id (failure → `LogWarning(exception, "Reindex of lesson {LessonId} failed.", id)` and the id is deferred). Catch filters are `when (!stoppingToken.IsCancellationRequested)`. |

### api/ — Tests (new files)
| # | Path |
|---|------|
| T1 | `api/Elmanhg.Tests/Domain/ContentRetrieval/LessonContentChunkTests.cs` |
| T2 | `api/Elmanhg.Tests/Domain/ContentRetrieval/LessonContentIndexTests.cs` |
| T3 | `api/Elmanhg.Tests/Domain/ContentRetrieval/LessonContentChunkDraftTests.cs` |
| T4 | `api/Elmanhg.Tests/Application/Features/ContentRetrieval/Shared/LessonContentChunkerTests.cs` |
| T5 | `api/Elmanhg.Tests/Application/Features/ContentRetrieval/Shared/LessonContentChunkPlannerTests.cs` |
| T6 | `api/Elmanhg.Tests/Application/Features/ContentRetrieval/Shared/LessonContentMatchResultGeneratorTests.cs` |
| T7 | `api/Elmanhg.Tests/Application/Features/ContentRetrieval/SearchLessonContent/SearchLessonContentHandlerTests.cs` |
| T8 | `api/Elmanhg.Tests/Application/Features/ContentRetrieval/SearchLessonContent/SearchLessonContentValidatorTests.cs` |
| T9 | `api/Elmanhg.Tests/Application/Features/ContentRetrieval/ReindexLessonContent/ReindexLessonContentHandlerTests.cs` |
| T10 | `api/Elmanhg.Tests/Application/Features/ContentRetrieval/ReindexLessonContent/ReindexLessonContentValidatorTests.cs` |
| T11 | `api/Elmanhg.Tests/Application/Features/ContentRetrieval/GetStaleLessonContentIds/GetStaleLessonContentIdsHandlerTests.cs` |
| T12 | `api/Elmanhg.Tests/Application/Features/ContentRetrieval/RebuildContentIndex/RebuildContentIndexHandlerTests.cs` |
| T13 | `api/Elmanhg.Tests/Infrastructure/RichText/RichTextExtractorTests.cs` |
| T14 | `api/Elmanhg.Tests/Api/Workers/LessonContentIndexWorkerTests.cs` (reuses `ManualTimeProvider`) |
| T15 | `api/Elmanhg.Tests/Integration/ContentRetrieval/ContentRetrievalTestData.cs`: `SeedPublishedLessonAsync(factory, string explanationHtml, string summaryHtml, IReadOnlyList<string> objectives, ct)` (via `Lesson.Update` and `Publish`, then `ClearDomainEvents`), `ReindexAsync(factory, lessonId, ct)` (`ISender.Send(new ReindexLessonContentCommand(lessonId))` in a new scope), `ReadChunksAsync`, `ReadIndexAsync`, `RetireQuestionAsync`, `TouchLessonAsync` (calls `lesson.MoveTo(order + 1, …)` and saves) |
| T16 | `api/Elmanhg.Tests/Integration/ContentRetrieval/LessonContentSearchEndpointTests.cs` |
| T17 | `api/Elmanhg.Tests/Integration/ContentRetrieval/LessonContentReindexTests.cs` |
| T18 | `api/Elmanhg.Tests/Integration/ContentRetrieval/ContentIndexRebuildEndpointTests.cs` |

### ai/ (Python; package `elmanhg_ai`)
| # | Path | Contract |
|---|------|----------|
| P1 | `settings.py` (edit, listed here for the fields) | `embedding_provider: Literal["fake", "openai"] = "fake"`. `openai_api_key: SecretStr \| None = None`. `embedding_model: str = Field(default="text-embedding-3-small", min_length=1)`. `embedding_dimensions: int = Field(default=1536, ge=1, le=2000)`. `embedding_max_texts: int = Field(default=64, ge=1, le=2048)`. `embedding_max_text_chars: int = Field(default=8000, ge=1)`. `embedding_usd_per_million_tokens: Decimal = Field(default=Decimal("0.02"), ge=0)`. `@model_validator(mode="after") _openai_needs_key`: raises `ValueError("openai_api_key is required when embedding_provider is openai")` when the provider is openai and the key is None or blank. |
| P2 | `src/elmanhg_ai/clients/embedding.py` | `@dataclass(frozen=True, slots=True) class EmbeddingRequest: texts: tuple[str, ...]; input_type: Literal["document", "query"]; dimensions: int`. `EmbeddingReply: model: str; vectors: tuple[tuple[float, ...], ...]; input_tokens: int`. `class EmbeddingClient(Protocol): async def embed(self, request: EmbeddingRequest) -> EmbeddingReply: ...; async def aclose(self) -> None: ...`. `estimate_embedding_cost_usd(input_tokens: int, settings: Settings) -> Decimal` (quantised with `COST_QUANTUM` from `clients/model.py`). `build_embedding_client(settings) -> EmbeddingClient`: `match` "fake" → `FakeEmbeddingClient()`, "openai" → `OpenAiEmbeddingClient.from_settings(settings)`. |
| P3 | `src/elmanhg_ai/clients/fake_embedding.py` | `FAKE_EMBEDDING_MODEL: Final = "fake-embedding"`. `class FakeEmbeddingClient(script: Sequence[EmbeddingReply \| Exception] = ())`, which records `self.requests`. When the script is empty, it returns `EmbeddingReply(FAKE_EMBEDDING_MODEL, tuple(fake_vector(t, request.dimensions) for t in request.texts), 0)`. `fake_vector(text: str, dimensions: int) -> tuple[float, ...]`: `unicodedata.normalize("NFKC", text).casefold()`, then remove code points in `range(0x064B, 0x0653)` and `0x0640`, and map `0x0622`, `0x0623`, `0x0625` to `0x0627` (build the table with `str.maketrans` from `chr()` values; **no `\u` literals**). Tokens are `re.findall(r"\w+", …)`. For each token, `digest = hashlib.blake2b(token.encode(), digest_size=8).digest()`; `index = int.from_bytes(digest[:4], "big") % dimensions`; add `+1.0` if `digest[4] & 1 == 0`, else `-1.0`. L2-normalise; a zero norm gives `e0`. |
| P4 | `src/elmanhg_ai/clients/openai_embedding.py` | `PROVIDER: Final = "openai"`, `OPENAI_BASE_URL: Final = "https://api.openai.com/v1"`, `RETRY_BASE_SECONDS: Final = 0.5`, `RETRY_STATUSES: Final = frozenset({429, 500, 502, 503, 504})`. `class OpenAiEmbeddingClient(http: httpx2.AsyncClient, model: str, max_retries: int, sleep: Callable[[float], Awaitable[None]] = asyncio.sleep)`. `from_settings(settings)` builds `httpx2.AsyncClient(base_url=OPENAI_BASE_URL, headers={"Authorization": f"Bearer {key}"}, timeout=httpx2.Timeout(settings.model_timeout_seconds))`. `embed(request)` POSTs `"/embeddings"` with json `{"model", "input": list(texts), "dimensions", "encoding_format": "float"}` and runs `max_retries + 1` attempts. A transport error or a status in `RETRY_STATUSES` retries after `await sleep(RETRY_BASE_SECONDS * 2 ** attempt)`, while attempts remain. When they are exhausted: log `embedding.call_failed` (provider, model, status_code, error_type), then `raise ModelUnavailableError()`. Any other non-2xx → the same log, then `ModelUnavailableError`, with no retry. A 2xx body is parsed with private Pydantic models `_OpenAiItem(index: int, embedding: list[float])`, `_OpenAiUsage(prompt_tokens: int)`, `_OpenAiEmbeddings(data: list[_OpenAiItem], model: str, usage: _OpenAiUsage)` (`extra="ignore"`). A `ValidationError` or JSON error logs `embedding.output_invalid` and raises `ModelOutputInvalidError`. Vectors are ordered by `index`. `aclose()` closes the http client. The API key is never logged. |
| P5 | `src/elmanhg_ai/api/embeddings/__init__.py` | empty |
| P6 | `src/elmanhg_ai/api/embeddings/schemas.py` | `class EmbeddingInputType(StrEnum): DOCUMENT = "document"; QUERY = "query"`. `class EmbeddingsIn(ApiInModel): input_type: EmbeddingInputType; texts: list[Annotated[str, Field(min_length=1)]] = Field(min_length=1)`. `class EmbeddingsOut(ApiOutModel): model: str; dimensions: int; embeddings: list[list[float]]; input_tokens: int` |
| P7 | `src/elmanhg_ai/api/embeddings/router.py` | `router = APIRouter(prefix="/v1", tags=["embeddings"], dependencies=[Depends(require_service_token)])`. `@router.post("/embeddings", responses={400, 401, 502, 503: Problem}) async def create_embeddings(payload: EmbeddingsIn, settings: SettingsDep, client: EmbeddingClientDep) -> EmbeddingsOut`: `result = await embed.run(payload, client=client, settings=settings)`, then map to `EmbeddingsOut`. operationId: `embeddings_create_embeddings`. |
| P8 | `src/elmanhg_ai/pipelines/embed.py` | `PIPELINE_NAME: Final = "embeddings"`. `@dataclass(frozen=True, slots=True) class EmbeddingsResult: model: str; dimensions: int; vectors: tuple[tuple[float, ...], ...]; input_tokens: int`. `async def run(payload: EmbeddingsIn, *, client: EmbeddingClient, settings: Settings) -> EmbeddingsResult`: (1) limit errors: `len(texts) > embedding_max_texts` → `FieldError("texts", "TOO_MANY_ITEMS", …)`; each `len(texts[i]) > embedding_max_text_chars` → `FieldError(f"texts[{i}]", "TOO_LONG", …)`; any errors → `raise ValidationFailedError(errors)`. (2) Start `time.perf_counter()` and call `client.embed(EmbeddingRequest(tuple(texts), input_type.value, settings.embedding_dimensions))`. (3) If `len(vectors) != len(texts)` or any `len(v) != settings.embedding_dimensions`, log `embedding.output_invalid` and `raise ModelOutputInvalidError()`. (4) Log `embedding.completed` with `pipeline`, `model`, `input_type`, `count`, `tokens_in`, `latency_ms`, `cost_usd` (never text). (5) Return the result. |
| P9 | `tests/fixtures/openai/embeddings_success.json` | `{"object":"list","data":[{"object":"embedding","index":1,"embedding":[0.0,1.0,0.0]},{"object":"embedding","index":0,"embedding":[1.0,0.0,0.0]}],"model":"text-embedding-3-small","usage":{"prompt_tokens":7,"total_tokens":7}}` |
| P10 | `tests/unit/test_embedding_schemas.py`, `tests/unit/test_fake_embedding.py`, `tests/unit/test_openai_embedding.py`, `tests/unit/test_embed_pipeline.py`, `tests/integration/test_embeddings_endpoint.py` | see Test plan |

### docs
| # | Path | Contract |
|---|------|----------|
| DOC1 | `docs/content-retrieval.md` | Sections, in order. **Role** (PRD §9.2, the #91 consumer). **What is indexed:** sections, chunking rules, the `ChunkMaxCharacters` split, title truncation, question chunks = stem + explanation of servable questions only. **Section references:** the `Reference` formats. **Freshness:** the stale rule (two stamps), the sweep (interval, batch, deferral), what triggers a reindex (publish, content edit, reorder, approve, question edit or retire, unpublish removes). **Retrieval:** `SearchLessonContentQuery`; the query-time filters (Published lesson, servable question at the chunk's version, same `EmbeddingModel`); cosine distance; `score = 1 − distance`; empty result when not indexed. **HTTP (admin only):** search request/response JSON and error codes; why students have no endpoint. **Rebuild:** when to use it (provider or model change). **Configuration:** the `ContentRetrieval` table. **Fakes:** lexical vectors, .NET `fake` vs Python `fake-embedding`. **Limits:** no ANN index, lesson scope, stale reads only between sweeps. |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.ContentSearchQueryRequired` (Application) | `CONTENT_SEARCH_QUERY_REQUIRED` | `SearchLessonContentValidator` | validation | 422 |
| `ErrorCodes.ContentSearchQueryTooLong` (Application) | `CONTENT_SEARCH_QUERY_TOO_LONG` | `SearchLessonContentValidator` | validation | 422 |
| `ErrorCodes.ContentSearchTopInvalid` (Application) | `CONTENT_SEARCH_TOP_INVALID` | `SearchLessonContentValidator` | validation | 422 |
| `ErrorCodes.ContentEmbeddingDimensionsInvalid` (Domain) | `CONTENT_EMBEDDING_DIMENSIONS_INVALID` | `LessonContentChunk.Create` | `BusinessRuleViolationCoreException` | 400 (in practice only reached in the worker, which logs it) |
| existing `LessonNotFound` | `LESSON_NOT_FOUND` | `SearchLessonContentHandler` | `NotFoundCoreException` | 404 |
| existing `LessonIdRequired` | `LESSON_ID_REQUIRED` | both validators | validation | 422 |
| existing `AiServiceUnavailable` | `AI_SERVICE_UNAVAILABLE` | Http/Fake client, `SearchLessonContentHandler` | `ServiceUnavailableCoreException` | 503 |

Resource strings (Arabic without tashkeel):
| Key | ar | en |
|---|---|---|
| `CONTENT_SEARCH_QUERY_REQUIRED` | اكتب نص البحث. | Enter the search text. |
| `CONTENT_SEARCH_QUERY_TOO_LONG` | نص البحث أطول من المسموح. | The search text is too long. |
| `CONTENT_SEARCH_TOP_INVALID` | عدد النتائج المطلوبة غير صالح. | The number of results requested is not valid. |
| `CONTENT_EMBEDDING_DIMENSIONS_INVALID` | أبعاد التمثيل الرقمي للمحتوى غير متوافقة. | The content embedding has the wrong number of dimensions. |

## Domain behaviour
- `LessonContentChunk.Create`: guard first (`embedding.Length != EmbeddingDimensions` → `BusinessRuleViolationCoreException(ErrorCodes.ContentEmbeddingDimensionsInvalid)`), then construct. The chunk is immutable after creation (no mutators) and is replaced, never updated, so there is no `UpdationDate` (base `Entity`, which has none).
- `LessonContentIndex.Create` / `MarkIndexed`: assign the passed stamps verbatim. `IndexedAt` is the record's own timestamp; there is no `UpdationDate` on `Entity`. No guards: counts come from the handler.
- No change to `Lesson` or `Question`. Staleness reads the `UpdationDate` values they already stamp.
- Invariant (documented in `docs/content-retrieval.md`, enforced in SQL, not the domain): a chunk is returned only when its lesson is Published, and, for a question chunk, the question is servable at `QuestionVersion`.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| POST | `/api/lessons/{lessonId:guid}/content-chunks/search` | `DefaultCodes.ContentManage` (Admin) | `SearchLessonContentRequest { query: string, top?: int, includeQuestionExplanations?: bool }` | `200 LessonContentSearchResult { lessonId, indexedAt, matches: [{ chunkId, section, sectionTitle, position, questionId, reference, content, score }] }` · 404 `LESSON_NOT_FOUND` · 422 · 503 `AI_SERVICE_UNAVAILABLE` |
| POST | `/api/content-index/rebuild` | `DefaultCodes.ContentManage` | — | `200` (empty) |
| POST (ai/, internal) | `/v1/embeddings` | Bearer service token | `{ "inputType": "document"\|"query", "texts": [string] }` | `200 { model, dimensions, embeddings: [[float]], inputTokens }` · 400 `VALIDATION_FAILED` · 401 · 502 `MODEL_OUTPUT_INVALID` · 503 `DEPENDENCY_UNAVAILABLE` |
| in-process | `SearchLessonContentQuery` | — (the caller, #91, enforces student access) | as above | `LessonContentSearchResult` |

## Test plan
### .NET unit
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | LessonContentChunkTests | Create_ValidEmbedding_CopiesDraftAndVector | All draft fields, `Embedding.ToArray()` equals the input, model, `CreatedAt` |
| 2 | LessonContentChunkTests | Create_WrongDimensions_ThrowsContentEmbeddingDimensionsInvalid | Exception type and code |
| 3 | LessonContentIndexTests | Create_SetsStampsCountAndModel | All fields |
| 4 | LessonContentIndexTests | MarkIndexed_ReplacesStampsCountAndModel | All five fields changed |
| 5 | LessonContentChunkDraftTests | EmbeddingText_WithTitle_PrefixesTitleLine | `"T\nC"` |
| 6 | LessonContentChunkDraftTests | EmbeddingText_WithoutTitle_ReturnsContent | `"C"` |
| 7 | LessonContentChunkerTests | Chunk_Heading_StartsNewChunkWithTitle | 2 chunks; the second has the heading as title; positions 1 and 2 |
| 8 | LessonContentChunkerTests | Chunk_SmallParagraphs_MergedUpToMax | Joined with `\n` into one chunk |
| 9 | LessonContentChunkerTests | Chunk_ParagraphsOverMax_StartsNextChunk | Boundary exactly at `max` |
| 10 | LessonContentChunkerTests | Chunk_LongParagraph_SplitsAtWhitespace | Every chunk ≤ max; no word is broken |
| 11 | LessonContentChunkerTests | Chunk_UnbrokenText_HardSplitsAtMax | Pieces of length max, max, remainder |
| 12 | LessonContentChunkerTests | Chunk_HeadingsAsTitlesFalse_TreatsHeadingAsText | Heading text in content; title null |
| 13 | LessonContentChunkerTests | Chunk_LongHeading_TruncatesTitleTo200 | `SectionTitle.Length == 200` |
| 14 | LessonContentChunkerTests | Chunk_NoBlocks_ReturnsEmpty | Empty list |
| 15 | LessonContentChunkPlannerTests | Plan_FullLesson_EmitsSectionsInOrder | Sequence Explanation…, Objectives, Summary…, QuestionExplanation |
| 16 | LessonContentChunkPlannerTests | Plan_Objectives_NumberedInOrder | Content `"1. a\n2. b"` |
| 17 | LessonContentChunkPlannerTests | Plan_Question_CombinesStemAndExplanationWithVersion | `QuestionId`, `QuestionVersion`, content has both texts |
| 18 | LessonContentChunkPlannerTests | Plan_EmptyLessonNoQuestions_ReturnsEmpty | Empty (uses the real `RichTextExtractor`) |
| 19 | LessonContentMatchResultGeneratorTests | Generate_Section_FormatsReference (Theory × 4 sections) | Reference string; `Score == 1 - Distance` |
| 20 | SearchLessonContentHandlerTests | Handle_IndexedLesson_EmbedsQueryAndReturnsScoredMatches | `EmbedAsync` received `InputType == Query` with the trimmed text; `SearchAsync` got model, `DefaultTopK`, include=true; mapped result |
| 21 | SearchLessonContentHandlerTests | Handle_ExplicitTopAndExclusion_PassedToSearch | `SearchAsync(…, 3, false, …)` |
| 22 | SearchLessonContentHandlerTests | Handle_NotIndexed_ReturnsEmptyWithoutCallingAiService | `IndexedAt` null; `Matches` empty; `EmbedAsync` DidNotReceive |
| 23 | SearchLessonContentHandlerTests | Handle_IndexWithNoChunks_ReturnsEmptyWithoutCallingAiService | `IndexedAt` set; `Matches` empty; DidNotReceive |
| 24 | SearchLessonContentHandlerTests | Handle_UnknownLesson_ThrowsLessonNotFound | `NotFoundCoreException` + code |
| 25 | SearchLessonContentHandlerTests | Handle_DraftLesson_ThrowsLessonNotFound | Same |
| 26 | SearchLessonContentHandlerTests | Handle_QueryVectorWrongDimensions_ThrowsAiServiceUnavailable | `ServiceUnavailableCoreException` + code; `SearchAsync` DidNotReceive |
| 27 | SearchLessonContentValidatorTests | Validate_ValidQuery_Passes | No errors (`Top` null and `Top = MaxTopK` both pass) |
| 28 | SearchLessonContentValidatorTests | Validate_EmptyLessonId_FailsLessonIdRequired | Code |
| 29 | SearchLessonContentValidatorTests | Validate_BlankQuery_FailsContentSearchQueryRequired | Code |
| 30 | SearchLessonContentValidatorTests | Validate_QueryOverMax_FailsContentSearchQueryTooLong | Code |
| 31 | SearchLessonContentValidatorTests | Validate_TopOutOfRange_FailsContentSearchTopInvalid (Theory: 0, MaxTopK+1) | Code |
| 32 | ReindexLessonContentHandlerTests | Handle_PublishedLesson_EmbedsDocumentsReplacesChunksAndCreatesIndex | `EmbedAsync` `InputType == Document` with the `EmbeddingText`s; `DeleteRange(existing)`; `AddRangeAsync` count; index created with `lesson.UpdationDate`, max question `UpdationDate`, count, model; `SaveChangesAsync` Received(1) |
| 33 | ReindexLessonContentHandlerTests | Handle_ExistingIndex_MarksIndexedInsteadOfAdding | Index fields updated; `AddAsync` DidNotReceive; Save Received(1) |
| 34 | ReindexLessonContentHandlerTests | Handle_MoreDraftsThanBatchSize_EmbedsInSequentialBatches | `EmbeddingBatchSize = 2`, 3 drafts → 2 calls with sizes 2 and 1 |
| 35 | ReindexLessonContentHandlerTests | Handle_NoContent_StoresEmptyIndexWithoutCallingAiService | `ChunkCount` 0; model null; `QuestionsUpdatedAt` null; `EmbedAsync` DidNotReceive; Save Received(1) |
| 36 | ReindexLessonContentHandlerTests | Handle_UnpublishedLesson_DeletesChunksAndIndex | `DeleteRange`, `Delete(index)`, Save Received(1), `EmbedAsync` DidNotReceive |
| 37 | ReindexLessonContentHandlerTests | Handle_MissingLesson_DeletesChunksAndIndex | Same as 36 |
| 38 | ReindexLessonContentHandlerTests | Handle_AiServiceUnavailable_ThrowsAndDoesNotSave | `ServiceUnavailableCoreException`; Save DidNotReceive; `DeleteRange` DidNotReceive |
| 39 | ReindexLessonContentValidatorTests | Validate_EmptyLessonId_FailsLessonIdRequired | Code |
| 40 | ReindexLessonContentValidatorTests | Validate_LessonId_Passes | No errors |
| 41 | GetStaleLessonContentIdsHandlerTests | Handle_PassesExcludedIdsAndConfiguredBatchSize | Repository received the exact excluded set and `IndexSweepBatchSize`; returns its ids |
| 42 | RebuildContentIndexHandlerTests | Handle_DeletesAllIndexState | `DeleteAllAsync` Received(1); Save DidNotReceive |
| 43 | RebuildContentIndexCommand (in RebuildContentIndexHandlerTests) | Command_AuditMetadata_IsContentIndexRebuild | Action `ContentIndex.Rebuild`, type `ContentIndex`, id null |
| 44 | RichTextExtractorTests | ExtractBlocks_HeadingsAndParagraphs_ReturnsKindsInOrder | Kinds and texts |
| 45 | RichTextExtractorTests | ExtractBlocks_OrderedListWithStart_NumbersItems | `"3. a"`, `"4. b"` |
| 46 | RichTextExtractorTests | ExtractBlocks_BulletList_PrefixesDash | `"- a"` |
| 47 | RichTextExtractorTests | ExtractBlocks_InlineAndBlockMath_EmitsLatexDelimiters | `$F=ma$` inline; `$$x^2$$` block paragraph |
| 48 | RichTextExtractorTests | ExtractBlocks_ImageAndBreak_UsesAltAndCollapsesWhitespace | Alt text; single spaces |
| 49 | RichTextExtractorTests | ExtractBlocks_NullOrBlankOrEmptyParagraphs_ReturnsNoBlocks (Theory) | Empty list |
| 50 | FakeAiServiceClientTests | EmbedAsync_Development_ReturnsUnitVectorsOfChunkDimensions | Model `fake`; each vector length 1536; norm ≈ 1 |
| 51 | FakeAiServiceClientTests | EmbedAsync_SameTextWithAndWithoutTashkeel_ReturnsSameVector | Equal arrays (build the diacritics with `(char)0x064E`, no `\u` literal) |
| 52 | FakeAiServiceClientTests | EmbedAsync_SharedWords_ScoreHigherThanUnrelated | cos(q, related) > cos(q, unrelated) |
| 53 | FakeAiServiceClientTests | EmbedAsync_Production_ThrowsAiServiceUnavailable | Type + code |
| 54 | HttpAiServiceClientTests | EmbedAsync_ValidRequest_PostsCamelCaseBodyToV1Embeddings | URI `http://ai.test/v1/embeddings`; Bearer; body `inputType == "query"`, `texts` |
| 55 | HttpAiServiceClientTests | EmbedAsync_Success_ReturnsVectorsModelAndTokens | Mapped result |
| 56 | HttpAiServiceClientTests | EmbedAsync_CountMismatch_ThrowsAiServiceUnavailable | Type + code |
| 57 | HttpAiServiceClientTests | EmbedAsync_VectorLengthNotDimensions_ThrowsAiServiceUnavailable | Type + code |
| 58 | HttpAiServiceClientTests | EmbedAsync_ServerError_ThrowsAiServiceUnavailable | Type + code |
| 59 | LessonContentIndexWorkerTests | Sweep_FailingLessons_LogsAndReindexesTheRest | Mirror of the SubscriptionLapse test: two Warnings; third reindexed |
| 60 | LessonContentIndexWorkerTests | Sweep_FailedIds_AreSkippedUntilTheBacklogEnds | Excluded sets per page, as in the lapse test |
| 61 | LessonContentIndexWorkerTests | Stop_DuringSweep_EndsTheLoopWithoutLogging | No logs; the task completes |
| 62 | LessonContentIndexWorkerTests | Execute_Disabled_EndsWithoutSweeping | The sender never receives a query |

### .NET integration (Testcontainers, pgvector image)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 63 | AppDbContextTests | Migrate_FreshDatabase_LeavesNoPendingMigrations (edit) | 25th `_AddLessonContentIndex` |
| 64 | AppDbContextTests | Migrate_LessonContentChunks_CreatesVectorColumn | `information_schema.columns.udt_name == "vector"` for `LessonContentChunks.Embedding` |
| 65 | LessonContentReindexTests | Reindex_PublishedLessonWithServableQuestion_StoresChunksPerSection | Persisted sections {Explanation, Objectives, Summary, QuestionExplanation}; the question chunk has `QuestionVersion` 1; index `SourceUpdatedAt == lesson.UpdationDate`, `ChunkCount` equals the rows |
| 66 | LessonContentReindexTests | Reindex_Twice_ReplacesChunksWithoutDuplicates | Same row count; new ids |
| 67 | LessonContentReindexTests | Reindex_PendingQuestion_IsNotIndexed | No QuestionExplanation rows |
| 68 | LessonContentReindexTests | Reindex_AfterUnpublish_RemovesChunksAndIndex | 0 rows; no index |
| 69 | LessonContentReindexTests | StaleIds_PublishedLessonWithoutIndex_IsListed | Via `ILessonContentIndexRepository.GetStaleLessonIdsAsync([], 100000, ct)`: contains the id |
| 70 | LessonContentReindexTests | StaleIds_FreshlyIndexedLesson_IsNotListed | Does not contain it (including a lesson with no questions: null stamp equality) |
| 71 | LessonContentReindexTests | StaleIds_LessonTouchedAfterIndexing_IsListed | After `TouchLessonAsync`: contains it |
| 72 | LessonContentReindexTests | StaleIds_QuestionApprovedAfterIndexing_IsListed | Seed an approved question after the reindex: contains it |
| 73 | LessonContentReindexTests | StaleIds_DraftLessonAndExcludedId_AreNotListed | Neither is present |
| 74 | LessonContentSearchEndpointTests | PostSearch_IndexedLesson_ReturnsBestMatchFirstWithReference | Explanation `<h2>قانون أوم</h2><p>…المقاومة…</p><h2>القدرة</h2><p>…</p>`; the query shares words with paragraph 1 → first match `Section == Explanation`, `sectionTitle == "قانون أوم"`, `reference == "explanation-1"`, `score > 0`; `indexedAt` set |
| 75 | LessonContentSearchEndpointTests | PostSearch_ServableQuestion_ReturnsQuestionChunk | Query "add the numbers" → a match with `section == QuestionExplanation`, `questionId` |
| 76 | LessonContentSearchEndpointTests | PostSearch_RetiredQuestionAfterIndexing_IsExcluded | After `RetireQuestionAsync`: no QuestionExplanation match |
| 77 | LessonContentSearchEndpointTests | PostSearch_IncludeQuestionExplanationsFalse_ExcludesQuestionChunks | No QuestionExplanation match |
| 78 | LessonContentSearchEndpointTests | PostSearch_NotYetIndexed_ReturnsEmptyMatches | 200; `indexedAt` null; `matches` [] |
| 79 | LessonContentSearchEndpointTests | PostSearch_DraftLesson_Returns404LessonNotFound | Status + problem `code` |
| 80 | LessonContentSearchEndpointTests | PostSearch_BlankQuery_Returns422ContentSearchQueryRequired | Status + code |
| 81 | LessonContentSearchEndpointTests | PostSearch_Student_Returns403 | 403 |
| 82 | LessonContentSearchEndpointTests | PostSearch_Anonymous_Returns401 | 401 |
| 83 | ContentIndexRebuildEndpointTests | PostRebuild_Admin_ClearsIndexStateAndAudits | 200; the index row for the seeded lesson is gone; the lesson is listed stale again; an `AuditLogs` row with `Action == "ContentIndex.Rebuild"` and `Outcome == "Success"` |
| 84 | ContentIndexRebuildEndpointTests | PostRebuild_Teacher_Returns403 | 403; the index row still exists |

Rebuild deletes **all** index rows, and the shared DB is used by other ContentRetrieval tests. Put T16, T17 and T18 in one `[Collection(ContentRetrievalCollection.Name)]` with `DisableParallelization = true`, declared in `ContentRetrievalTestData.cs` the way `ServableCountCollection` is. Also add the file `api/Elmanhg.Tests/Integration/ContentRetrieval/ContentRetrievalCollection.cs` (**T19**).

### Python (pytest; the `integration` marker for ASGI tests)
| # | File | Test | Asserts |
|---|------|------|---------|
| 85 | test_settings.py | test_settings_defaults_select_fake_embeddings_1536 | provider fake, model `text-embedding-3-small`, dims 1536, max texts 64 |
| 86 | test_settings.py | test_settings_openai_without_api_key_raises_validation_error | Message contains `openai_api_key is required` |
| 87 | test_settings.py | test_settings_embedding_dimensions_above_2000_raises_validation_error | loc `("embedding_dimensions",)` |
| 88 | test_embedding_schemas.py | test_embeddings_in_valid_payload_parses | `input_type`, texts |
| 89 | test_embedding_schemas.py | test_embeddings_in_empty_texts_raises_at_texts | loc `("texts",)` |
| 90 | test_embedding_schemas.py | test_embeddings_in_blank_text_raises_at_texts_index | loc `("texts", 0)` |
| 91 | test_embedding_schemas.py | test_embeddings_in_unknown_input_type_raises_at_input_type | loc `("inputType",)` |
| 92 | test_embedding_schemas.py | test_embeddings_in_extra_field_raises_forbidden | loc of the extra key |
| 93 | test_fake_embedding.py | test_fake_vector_same_text_is_deterministic_and_unit_length | Equal; norm ≈ 1 |
| 94 | test_fake_embedding.py | test_fake_vector_ignores_tashkeel_and_alef_variants | Equal vectors (build strings with `chr()`) |
| 95 | test_fake_embedding.py | test_fake_vector_shared_words_score_higher_than_unrelated | Cosine ordering |
| 96 | test_fake_embedding.py | test_fake_vector_no_tokens_returns_first_axis | `e0` |
| 97 | test_fake_embedding.py | test_fake_embedding_client_script_raises_scripted_error | The scripted exception propagates; the request is recorded |
| 98 | test_openai_embedding.py | test_openai_embed_sends_model_input_dimensions_and_bearer | Path `/v1/embeddings` (base_url includes `/v1`); header; json body incl. `encoding_format` |
| 99 | test_openai_embedding.py | test_openai_embed_success_orders_vectors_by_index_and_maps_usage | Vectors `((1,0,0),(0,1,0))`; `input_tokens` 7; model |
| 100 | test_openai_embedding.py | test_openai_embed_rate_limited_then_success_retries_once | 2 requests; `sleep` awaited with 0.5 |
| 101 | test_openai_embedding.py | test_openai_embed_server_error_exhausts_retries_raises_model_unavailable | `max_retries+1` requests; `ModelUnavailableError`, code `DEPENDENCY_UNAVAILABLE` |
| 102 | test_openai_embedding.py | test_openai_embed_bad_request_raises_without_retry | 1 request; `ModelUnavailableError` |
| 103 | test_openai_embedding.py | test_openai_embed_transport_error_raises_model_unavailable | `MockTransport` raises `httpx2.ConnectError` |
| 104 | test_openai_embedding.py | test_openai_embed_malformed_body_raises_model_output_invalid | `ModelOutputInvalidError` |
| 105 | test_openai_embedding.py | test_build_embedding_client_selects_provider (parametrize fake/openai, ids) | Instance types |
| 106 | test_embed_pipeline.py | test_embed_run_success_returns_vectors_and_logs_completion | Result fields; the `embedding.completed` log has `pipeline="embeddings"`, `tokens_in`, `cost_usd` and no text key |
| 107 | test_embed_pipeline.py | test_embed_run_too_many_texts_raises_validation_failed | Field `texts`, code `TOO_MANY_ITEMS` |
| 108 | test_embed_pipeline.py | test_embed_run_text_too_long_raises_validation_failed | Field `texts[1]`, code `TOO_LONG`; the client is not called |
| 109 | test_embed_pipeline.py | test_embed_run_wrong_vector_count_raises_model_output_invalid | Error type |
| 110 | test_embed_pipeline.py | test_embed_run_wrong_dimensions_raises_model_output_invalid | Error type |
| 111 | test_embed_pipeline.py | test_estimate_embedding_cost_usd_uses_configured_price | 1,000,000 tokens × 0.02 → `Decimal("0.020000")` |
| 112 | test_embeddings_endpoint.py | test_embeddings_valid_request_returns_vectors | 200; keys `{model, dimensions, embeddings, inputTokens}`; `len(embeddings) == len(texts)`; `dimensions == 1536` |
| 113 | test_embeddings_endpoint.py | test_embeddings_missing_token_returns_401_problem | 401; problem+json; `UNAUTHENTICATED` |
| 114 | test_embeddings_endpoint.py | test_embeddings_empty_texts_returns_400_validation_failed | 400; problem+json; `VALIDATION_FAILED` |
| 115 | test_embeddings_endpoint.py | test_embeddings_too_many_texts_returns_400_too_many_items | `errors[0].code == "TOO_MANY_ITEMS"` |
| 116 | test_embeddings_endpoint.py | test_embeddings_provider_failure_returns_503_dependency_unavailable | Scripted `ModelUnavailableError` |
| 117 | test_embeddings_endpoint.py | test_embeddings_invalid_output_returns_502 | Scripted reply with the wrong count → `MODEL_OUTPUT_INVALID` |
| 118 | test_openapi_document.py | test_openapi_document_matches_committed_file (edit) | Also `/v1/embeddings` in paths |

Mutation-check tests 8, 20, 36, 68, 76 and 93 (break the code on purpose, confirm the test fails, restore). Do not mutate the auth or sanitiser code (PROGRESS gotcha).

## Definition of done
- [ ] `/v1/embeddings` exists with In/Out models, the service-token dependency, problem+json errors, and `ai/openapi/v1.json` regenerated. The ai-ci command set (ruff format, ruff check, mypy strict, `pytest -m "not eval"`, pip-audit, Docker build and ready) passes, run exactly as `.github/workflows/ai-ci.yml` does.
- [ ] `FakeEmbeddingClient` is the default. `OpenAiEmbeddingClient` is chosen only by `ELMANHG_AI_EMBEDDING_PROVIDER=openai`, needs a key, retries 429/5xx/transport with backoff, never logs the key or the texts, and logs `embedding.completed` with cost.
- [ ] `httpx2==2.13.0` is a runtime dependency, `uv.lock` is updated, and no other Python package is added.
- [ ] .NET adds exactly `Pgvector` 0.3.2 and `Pgvector.EntityFrameworkCore` 0.3.0 through CPM. `UseVector()` is wired. The migration `AddLessonContentIndex` creates the `vector` extension and both tables with no destructive operations.
- [ ] `IAiServiceClient.EmbedAsync` is implemented by the Fake (refuses in Production) and Http clients (validates count and dimensions → 503).
- [ ] Only Published lessons and servable questions at their current version are ever returned. This is verified by integration tests 67, 76 and 79 and by the query-time filters in `LessonContentChunkRepository`, which reuses `ServableQuestionSpecification`.
- [ ] The staleness sweep re-indexes after publish, lesson edit and question approval (tests 69–72). The worker mirrors `SubscriptionLapseWorker` (scope per iteration, deferral, logging).
- [ ] The search endpoint is Admin-only. It returns section, title, position, reference and score; an unindexed lesson gives an empty result without an AI call.
- [ ] Rebuild is Admin-only, audited as `ContentIndex.Rebuild`, and documented in `docs/audit-log.md`.
- [ ] `ContentRetrievalOptions` is validated on start. `IndexSweepEnabled` defaults to true in code. The keys are in `appsettings.example.json` and `ApiFactory`, and `ApiFactory` disables the sweep with `UseSetting`.
- [ ] Four new error codes are in both resx files.
- [ ] `api/openapi/v1.json` and the Orval client are regenerated with no drift. The Postman `ContentRetrieval` folder is added.
- [ ] Every test in the Test plan exists with the stated name and assertion. No existing test is weakened. `dotnet test api/ -c Release` passes with `appsettings.json` moved aside (CI parity).
- [ ] `dotnet format --verify-no-changes` is clean outside `core-libraries`, and the guard grep from skill §9 prints nothing. Every new .cs file is ≤ ~100 lines, file-scoped, with no comments except WHY.
- [ ] Web: `npm --prefix web run typecheck`, lint, and `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` are clean after the regeneration.
- [ ] Docs agree with the code: `docs/content-retrieval.md` (new), `docs/ai-service.md`, `docs/PRD.md` §15 and §18, `docs/audit-log.md`, `README.md`, `.env.example`, `docker-compose.yml`.
- [ ] The deferred item (live OpenAI verification and the Arabic retrieval eval) is listed in the implementation report for the orchestrator to file.
