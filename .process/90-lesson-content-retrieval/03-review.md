VERDICT: CHANGES_REQUESTED

# Review — [E8.S2] Lesson content retrieval (#90)

## Blocking

### 1. PRD §18 still says the Python service does the avatar retrieval, but the .NET API does it now
**Where:** `docs/PRD.md:542` (§18 "Recommended technical shape", the **AI/grading service** bullet). Compare with `api/Elmanhg.Application/ContentRetrieval/SearchLessonContent/SearchLessonContentHandler.cs:13-40`, `api/Elmanhg.Infrastructure/ContentRetrieval/LessonContentChunkRepository.cs:14-28` and `docs/content-retrieval.md:7`.
**Rule:** `.claude/rules/docs-sync.md` (an architecture change makes the PRD diverge); plan Decision 2.
**Problem:** This story made the .NET API the owner of the chunks, the vectors and the search. The AI service only turns text into vectors. `docs/content-retrieval.md` §Role says exactly that. The plan edited PRD §18 to add the **Embeddings** bullet at line 544, but line 542 still lists "Avatar retrieval + generation (v1)" as work the Python FastAPI service does.
**Failure:** Ask "which service runs the avatar's retrieval?" and the docs give two answers. PRD §18 says Python. `docs/content-retrieval.md` and the code say the .NET API (`SearchLessonContentQuery`, which #91 calls in-process). The #91 planner reads the PRD first, so it can put retrieval in `ai/` a second time.
**Fix:** Change one clause at `docs/PRD.md:542`. For example: "Avatar generation (v1) and text embeddings for retrieval (v1); the vector search runs in the API (pgvector, docs/content-retrieval.md)".

## Non-blocking
- **Deviation 2 (UseVector and the connection string): production is not affected.**
  - Production code never calls `GetConnectionString()` or opens a raw connection. The only runtime `GetConnectionString("DbConnectionString")` is the configuration read at `api/Elmanhg.Api/Program.cs:64`. I grepped `api/` for `GetConnectionString`, `GetDbConnection`, `NpgsqlConnection`, `NpgsqlDataSource`, `OpenConnection`, `UseNpgsql`, `AddDbContext` and `IDbContextFactory`.
  - The DB paths production does use all go through the DbContext: `SessionRepository.SqlQuery`, `AuditLogRepository.ExecuteSqlAsync`, `AddDbContextCheck`, and `dotnet ef database update`.
  - The question-import concurrency is protected by EF alone: the `QuestionImportBatches` unique violation is mapped in `AppDbContext.SaveChangesAsync`. The raw `NpgsqlConnection` exists only in `QuestionImportEndpointTests.cs:187,272`.
  - I checked this with a throwaway probe: EF Npgsql 10.0.3 + `UseVector()` against a pgvector:pg17 container with a password. `GetConnectionString()` came back without the password. `CanConnectAsync`, a query, `CanConnectAsync` against a missing database (the admin-connection path), and `EnsureCreatedAsync` all succeeded.
  - The `Persist Security Info=true` in `ApiFactory.cs:61-62` is harmless. A cleaner fix would be for `QuestionImportEndpointTests.ConnectionString()` (`:269-272`) to read `IConfiguration.GetConnectionString("DbConnectionString")` instead of `Database.GetConnectionString()`, which would leave the factory's string as it was.
- `api/Elmanhg.Infrastructure/ContentRetrieval/LessonContentIndexRepository.cs:20-22`: the withdrawn branch finds a withdrawn lesson only through its index row. If a lesson is unpublished and an admin runs Rebuild before the sweep handles it, the index row is gone and the lesson is no longer Published. Its chunks are then never deleted. They stay hidden at query time and are replaced if the lesson is republished, so this only bloats the table.
- `api/Elmanhg.Infrastructure/ContentRetrieval/LessonContentChunkRepository.cs:20`: no test isolates the `question.Version == x.QuestionVersion` clause. Test 76 covers retirement, which the servable filter already excludes. Worth adding: edit an Approved question, re-approve it at v2, do not reindex, then assert the v1 chunk is not returned.
- `api/Elmanhg.Infrastructure/AiService/HttpAiServiceClient.cs:42`: three reply guards have no test: a blank `Model`, `Dimensions <= 0`, and null `Embeddings`. Only the count and length guards are tested (tests 56 and 57).
- `api/Elmanhg.Api/Workers/LessonContentIndexWorker.cs:70`: in Production with `AiService:Provider=Fake`, `FakeAiServiceClient.EmbedAsync` refuses. Every non-empty Published lesson then logs a Warning each backlog cycle. This is by design (the chat refuses the same way), but one line in `docs/content-retrieval.md` would help.
- `ai/src/elmanhg_ai/main.py`: the `service.started` log line reports `embedding_model=text-embedding-3-small` even when the provider is `fake`, which serves model `fake-embedding`. This is cosmetic.
- `api/Elmanhg.Tests/Integration/ContentRetrieval/LessonContentSearchEndpointTests.cs`: no test sends a search as a Teacher. The policy is Admin-only (`PermissionMatrixPolicies.cs:18`). Student 403, anonymous 401 and Teacher 403 on rebuild are covered.
- `02-implementation.md` Build & test, dependencies: the report states licence, dates and ages, but not the registry command output that the momenta-dependency-policy §2/§7 report block asks for. I confirmed the rest independently:
  - The packages restore.
  - `dotnet list ... --vulnerable --include-transitive` is clean for Infrastructure.
  - `pip-audit` is clean.
  - The `uv.lock` diff is the httpx2 dev-to-runtime marker move only.

## Verified
- **Builds and tests, run by me.**
  - `dotnet test api/ -c Release`, with `appsettings.json` moved aside and restored afterwards: `total: 2711, failed: 0, succeeded: 2711`, and the log has no warnings or errors.
  - ai, exactly as `ai-ci.yml` runs it:
    - `uv sync --locked`: ok.
    - `ruff format --check`: 52 files already formatted.
    - `ruff check`: all checks passed.
    - `mypy src`: no issues in 32 files.
    - `pytest -m "not eval" --cov --cov-branch`: 97 passed, 97% coverage.
    - `uv export` + `pip-audit==2.10.1`: no known vulnerabilities.
    - `docker build`: ok. The container's `/health/ready` returned `{"status":"ok"}`, and `POST /v1/embeddings` returned `fake-embedding` at 1536 dimensions. The log line `embedding.completed` has no text.
  - I removed my container and image, and touched no other container.
- **Leakage.**
  - Search returns 404 unless the lesson exists, is not soft-deleted, and is Published (`SearchLessonContentHandler.cs:17-21`).
  - At query time, a non-question chunk needs the same `EmbeddingModel`. A question chunk also needs `WhereServable` (Approved, not retired, lesson Published) at the exact `QuestionVersion` (`LessonContentChunkRepository.cs:17-20`).
  - The index is built from `GetServableInLessonAsync` (`WhereServable`), only for Published lessons (`ReindexLessonContentHandler.cs:20-34`).
  - A content edit bumps `Version` and returns the question to Pending (`Question.Editing.cs:27-38`). `LessonId` never changes after create, so no draft stem or answer can be returned.
  - Draft lessons are never listed stale or indexed (test 73).
- **Admin-only.** Both actions carry `[Authorize(Policy = DefaultCodes.ContentManage)]`, which maps to `RequireRole(Admin)`. There is no student route, and `SearchLessonContentQuery` is reachable only in-process otherwise.
- **Worker, against the #81 lessons.**
  - Catch filters are `when (!stoppingToken.IsCancellationRequested)`.
  - Deferral keeps failing ids from starving the head of the batch.
  - Each iteration and each id gets its own async scope.
  - Listing failures log at Error and per-lesson failures at Warning.
  - There is a worker-level test (T14 with `ManualTimeProvider`, 4 tests).
  - The sweep defaults ON in code and is disabled in `ApiFactory` via `UseSetting`.
- **Stale detection.**
  - Null-stamp equality works (test 70). Touch, approve and unpublish each list the lesson (tests 71, 72, 68).
  - The reindex reads lesson and questions with `asNoTracking`, so `CoreDbContext` does not re-stamp them and there is no reindex loop.
  - Timestamps round-trip at microsecond precision, so equality holds.
- **Vector column and migration.**
  - `20260929131149_AddLessonContentIndex.cs`: `Up` creates the `vector` extension annotation, `Embedding vector(1536) NOT NULL`, both tables, and the B-tree indexes `IX_LessonContentChunks_LessonId`, `IX_LessonContentChunks_QuestionId` and the unique `IX_LessonContentIndexes_LessonId`, with Restrict FKs. There is no drop or rename in `Up`.
  - Test 64 asserts `udt_name = vector`. `docker-compose.yml` and the test factory already use `pgvector/pgvector:pg17`.
  - Chunk and index deletes are hard deletes (Core `Repository.Delete` is `Remove`), so republishing cannot violate the unique index.
  - No Lesson or Question is ever hard-deleted, so the Restrict FKs are safe.
- **Secrets.**
  - `openai_api_key` is a `SecretStr`, and a validator requires it when the provider is `openai`.
  - The key appears only in the httpx2 default header (`openai_embedding.py:159-162`). The failure logs carry `provider`, `model`, `status_code` and `error_type` only.
  - The fake is the default everywhere: `Settings.embedding_provider="fake"`, compose `ELMANHG_AI_EMBEDDING_PROVIDER:-fake`, `AiServiceOptions.Provider = Fake`, `appsettings.example.json` Fake, `ApiFactory` pins Fake. The .NET fake refuses in Production.
- **Contract fidelity.**
  - Every file in *Files to create* (D1-D7, A1-A22, I1-I5, P-A1-P-A3, T1-T19, P1-P10, DOC1) exists, and nothing extra was added beyond the reported `ReadStaleIdsAsync` helper.
  - Every "Existing code touched" row is present: CPM packages, csproj references, error codes, both resx files, `IAiServiceClient.EmbedAsync`, Fake/Http clients, `GetServableInLessonAsync`, `AppDbContext` DbSets, `HasPostgresExtension`, `ConfigureContentRetrieval`, soft-delete filters, DI, options with ValidateOnStart and DefaultTopK <= MaxTopK, `UseVector()`, hosted worker, `appsettings.example.json`, `ApiFactory`, `AppDbContextTests`, Postman, docker-compose, `.env.example`, README, and the docs.
  - The four reported deviations are real and justified. Deviation 1 makes the plan's "unpublish removes" actually reachable.
- **Style.**
  - File-scoped namespaces; sealed handlers, validators and records; `DateTimeOffset` everywhere; `.ConfigureAwait(false)` on every await outside the controller.
  - No `try`/`catch` in handlers. One `SaveChangesAsync` per reindex, and none for rebuild (`ExecuteDeleteAsync`, the named bulk maintenance, with a WHY comment).
  - Every new file is ~100 lines or less, except the generated Designer.
  - The skill §9 guard grep only matches the existing stub-handler `new HttpClient(_handler)` pattern in `HttpAiServiceClientTests`.
- **Postman.** The `ContentRetrieval` folder has `POST {{baseUrl}}/api/lessons/{{lessonId}}/content-chunks/search` (JSON body) and `POST {{baseUrl}}/api/content-index/rebuild`, with collection bearer auth. No stale requests.
- **Docs.** `docs/content-retrieval.md`, `docs/ai-service.md`, `docs/audit-log.md`, PRD §15 and the §18 Embeddings bullet, README, `.env.example` and compose agree with the code, apart from finding 1.

## Test quality
- `SearchLessonContentHandlerTests`: constrains the implementation. It asserts the trimmed `Query` input type, the exact `SearchAsync` arguments, the mapped result with Score = 1 - Distance, and DidNotReceive on the not-indexed, draft and wrong-dimension paths.
- `ReindexLessonContentHandlerTests`: constrains it. It uses the real `RichTextExtractor` and asserts the exact embedding texts, `DeleteRange` of the existing chunks, index stamps, batch sizes, and no save or delete on embed failure.
- `LessonContentChunkerTests`, `LessonContentChunkPlannerTests`, `RichTextExtractorTests`, `LessonContentMatchResultGeneratorTests`: pure functions tested against exact outputs.
- `LessonContentIndexWorkerTests`: constrains the loop, deferral, shutdown and the disabled switch.
- `FakeAiServiceClientTests` and `HttpAiServiceClientTests` (embeddings): real behaviour. They check norms, tashkeel folding, cosine ordering, wire JSON, and the validation guards (with the gaps listed above).
- Integration tests T16-T18: run against real PostgreSQL with pgvector and the real cosine search. They cover leakage (76, 77, 79), stale rules (69-73) and authz (81, 82, 84).
- `GetStaleLessonContentIdsHandlerTests` and `RebuildContentIndexHandlerTests.Handle_DeletesAllIndexState` are pass-through checks. They are acceptable for one-line delegating handlers, and the real query is covered by the integration tests.
