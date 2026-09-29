# AI service (`ai/`)

## Role

`ai/` is an internal Python 3.13 FastAPI service (package `elmanhg_ai`, managed with uv). It answers avatar chat turns over the Claude API (PRD §9) and embeds text for lesson retrieval ([content-retrieval.md](content-retrieval.md)). Only the .NET API calls it, over HTTP with a shared service token; browsers never do. The .NET side of the contract is `api/Elmanhg.Application/Shared/AiService/` (`IAiServiceClient`), and the Python side is `ai/src/elmanhg_ai/api/chat/schemas.py` and `ai/src/elmanhg_ai/api/embeddings/schemas.py`. Both change together, and `ai/openapi/v1.json` is the committed contract (regenerate with `uv run python -m elmanhg_ai.openapi_export` from `ai/`; a test fails on drift).

How the API builds the context bundle, the exam refusal, the daily quota and the student UI are in [avatar.md](avatar.md).

The .NET API's own readiness (`/health`) does not depend on the AI service: the avatar is non-critical, so a down AI service must not take the API out of rotation.

## Contract

`POST /v1/chat` (operationId `chat_create_chat_reply`). JSON is camelCase both ways and enums are camelCase strings. Unknown fields are rejected. The .NET client omits null fields.

Request:

```json
{
  "context": {
    "entryPoint": "quizQuestion",
    "subject": { "id": "0f5e2a4c-1b3d-4e6f-8a9b-0c1d2e3f4a5b", "name": "الفيزياء" },
    "unit": { "id": "1a2b3c4d-5e6f-4a1b-9c2d-3e4f5a6b7c8d", "name": "الكهربية التيارية" },
    "lesson": {
      "id": "2b3c4d5e-6f7a-4b2c-8d3e-4f5a6b7c8d9e",
      "name": "قانون أوم",
      "explanation": "شدة التيار تتناسب طرديا مع فرق الجهد.",
      "objectives": ["يطبق الطالب قانون أوم"],
      "summary": "المقاومة تساوي فرق الجهد مقسوما على شدة التيار."
    },
    "question": {
      "id": "3c4d5e6f-7a8b-4c3d-9e4f-5a6b7c8d9e0f",
      "stem": "احسب المقاومة إذا كان فرق الجهد ٨ فولت والتيار ٢ أمبير.",
      "studentAnswer": "٢ أوم",
      "correctAnswer": "٤ أوم",
      "explanation": "المقاومة = ٨ ÷ ٢ = ٤ أوم."
    }
  },
  "history": [
    { "role": "user", "content": "ما هو قانون أوم؟" },
    { "role": "assistant", "content": "فرق الجهد يساوي التيار في المقاومة." }
  ],
  "message": "لماذا إجابتي خطأ؟",
  "sources": [
    { "reference": "explanation-1", "title": "الشرح — قانون أوم", "content": "شدة التيار تتناسب طرديا مع فرق الجهد. V = I R" },
    { "reference": "summary-1", "title": "الملخص", "content": "المقاومة = فرق الجهد ÷ شدة التيار." }
  ]
}
```

Response `200`:

```json
{
  "reply": "هذا رد تجريبي من المساعد.",
  "model": "fake",
  "promptVersion": "v2",
  "inputTokens": 0,
  "outputTokens": 0,
  "stopReason": "end_turn",
  "costUsd": 0.0,
  "citations": ["explanation-1"]
}
```

`sources` are the retrieved lesson chunks ([content-retrieval.md](content-retrieval.md)); the field may be omitted or empty. `citations` lists the `reference` of every source the reply cites, distinct and in first-cited order; it only ever contains references that were sent, and is `[]` when nothing is cited.

`model` and `promptVersion` are recorded per message (PRD §9.3). `costUsd` is `estimate_cost_usd` of the reply's tokens at `ELMANHG_AI_MODEL_*_USD_PER_MILLION_TOKENS`; the API stores it per reply ([avatar.md](avatar.md), Conversation log).

Field rules:

| Entry point (`context.entryPoint`) | Required context |
|---|---|
| `lesson` | `lesson` |
| `quizQuestion` | `lesson` and `question` |
| `examReview` | `lesson` and `question` |
| `global` | nothing (`subjects` may list the student's subjects) |

- `history` alternates `user`, `assistant`, `user`, … and has an even length (it may be empty). The new `message` becomes the final user turn.
- `message`, `content`, `name` and `stem` must be non-empty.
- `sources[].reference` matches `^[a-z0-9-]+$` (1 to 200 characters) and is unique within the request; `title` is 1 to 300 characters and `content` is non-empty.

Limits (checked by the pipeline; exceeding one returns `400 VALIDATION_FAILED` with field code `TOO_MANY_ITEMS` or `TOO_LONG`):

| Setting | Default | Applies to |
|---|---|---|
| `ELMANHG_AI_CHAT_MAX_HISTORY_MESSAGES` | 20 | number of `history` turns |
| `ELMANHG_AI_CHAT_MAX_MESSAGE_CHARS` | 4000 | length of `message` and of each `history[].content` |
| `ELMANHG_AI_CHAT_MAX_CONTEXT_CHARS` | 60000 | length of the serialized `context` JSON (sources are not counted) |
| `ELMANHG_AI_CHAT_MAX_SOURCES` | 20 | number of `sources` |
| `ELMANHG_AI_CHAT_MAX_SOURCE_CHARS` | 8000 | length of each `sources[].content` |

### `POST /v1/embeddings`

operationId `embeddings_create_embeddings`. Turns texts into vectors for lesson retrieval. The API sends lesson chunks as `document` and the search text as `query`; the fake and OpenAI adapters treat both the same, and the field exists so a provider that distinguishes them needs no contract change.

Request:

```json
{ "inputType": "query", "texts": ["قانون أوم", "المقاومة"] }
```

Response `200`:

```json
{ "model": "text-embedding-3-small", "dimensions": 1536, "embeddings": [[0.012, -0.034, "…"], [0.021, 0.005, "…"]], "inputTokens": 7 }
```

- `inputType` is `document` or `query`. `texts` has at least one item, and every item is non-empty.
- `embeddings` has one vector per text, in request order, and every vector has exactly `dimensions` numbers. The service checks both before answering (`502 MODEL_OUTPUT_INVALID` otherwise), and the .NET `HttpAiServiceClient` checks them again (`AI_SERVICE_UNAVAILABLE` otherwise).
- `model` is the provider's model id. The API stores it with every chunk and searches only chunks of the same model.

Limits (`400 VALIDATION_FAILED`, field code `TOO_MANY_ITEMS` on `texts` or `TOO_LONG` on `texts[i]`):

| Setting | Default | Applies to |
|---|---|---|
| `ELMANHG_AI_EMBEDDING_MAX_TEXTS` | 64 | number of `texts` (the API sends at most `ContentRetrieval:EmbeddingBatchSize`, 32) |
| `ELMANHG_AI_EMBEDDING_MAX_TEXT_CHARS` | 8000 | length of each text |

## Errors

Every error is RFC 9457 `application/problem+json`. `detail` is omitted when the status is 500 or above, and `errors` appears only for `VALIDATION_FAILED`. Titles are English only: the one caller, the .NET API, maps any failure to its own localised `AI_SERVICE_UNAVAILABLE` (503).

```json
{
  "type": "/problems/validation-failed",
  "title": "Validation failed",
  "status": 400,
  "instance": "/v1/chat",
  "code": "VALIDATION_FAILED",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "errors": [{ "field": "message", "code": "STRING_TOO_SHORT", "message": "String should have at least 1 character" }]
}
```

| Code | Status | When |
|---|---|---|
| `VALIDATION_FAILED` | 400 | Invalid body (FastAPI's 422 is mapped to 400) or a chat or embeddings limit exceeded |
| `MALFORMED_REQUEST` | 4xx | Any other client HTTP error |
| `UNAUTHENTICATED` | 401 | Missing or wrong service token (`WWW-Authenticate: Bearer`) |
| `NOT_FOUND` | 404 | Unknown route |
| `METHOD_NOT_ALLOWED` | 405 | Wrong method |
| `MODEL_OUTPUT_INVALID` | 502 | The model returned no text, or an embeddings reply was unreadable or had the wrong vector count or width |
| `DEPENDENCY_UNAVAILABLE` | 503 | The Claude API failed (any SDK error, after SDK retries), or the OpenAI embeddings call failed (after retries for 429, 5xx and transport errors; any other 4xx is not retried) |
| `SERVICE_NOT_READY` | 503 | Startup has not finished |
| `INTERNAL_ERROR` | 500 | Anything unexpected (logged, never echoed) |

On the .NET side, `HttpAiServiceClient` throws `ServiceUnavailableCoreException(AI_SERVICE_UNAVAILABLE)` on a transport failure, any non-2xx response, or an unreadable or empty reply. For embeddings it also throws when the model is blank, `dimensions` is not positive, the vector count differs from the text count, or any vector's length differs from `dimensions`.

## Service auth

- Header `Authorization: Bearer <token>`, required on `/v1/*`. Health endpoints are anonymous.
- One shared secret: the API's `AiService:ServiceToken` equals the service's `ELMANHG_AI_SERVICE_TOKEN`. It must be at least 32 characters on both sides; both refuse to start otherwise (the API only when `Provider=Http`).
- The service compares tokens in constant time (`secrets.compare_digest`). The token is never logged. Settings validation errors at startup omit input values (`hide_input_in_errors`), so a rejected token or key never reaches the container log.
- Rotation: change `AiService__ServiceToken` and `ELMANHG_AI_SERVICE_TOKEN` together, then restart both.

## Configuration

AI service (`ELMANHG_AI_*` environment variables; `settings.py` is the only place they are read):

| Variable | Default | Notes |
|---|---|---|
| `ELMANHG_AI_ENV` | `development` | `development`, `testing` or `production`. Production hides `/docs` and `/openapi.json`. Staging and production hosts both use `production`. |
| `ELMANHG_AI_LOG_LEVEL` | `INFO` | `DEBUG`, `INFO`, `WARNING`, `ERROR` |
| `ELMANHG_AI_LOG_FORMAT` | `json` | `json` or `console` |
| `ELMANHG_AI_SERVICE_TOKEN` | required | at least 32 characters |
| `ELMANHG_AI_LLM_PROVIDER` | `fake` | `fake` or `anthropic` |
| `ELMANHG_AI_ANTHROPIC_API_KEY` | unset | required when the provider is `anthropic` |
| `ELMANHG_AI_CHAT_MODEL` | `claude-sonnet-5` | Claude model id |
| `ELMANHG_AI_CHAT_PROMPT_VERSION` | `v2` | pattern `v<number>`; selects both chat prompt files. `v2` is the production Avatar prompt; `v1` is kept for history |
| `ELMANHG_AI_CHAT_MAX_TOKENS` | 1024 | 1 to 8192 |
| `ELMANHG_AI_CHAT_MAX_HISTORY_MESSAGES` | 20 | 0 to 100 |
| `ELMANHG_AI_CHAT_MAX_MESSAGE_CHARS` | 4000 | |
| `ELMANHG_AI_CHAT_MAX_CONTEXT_CHARS` | 60000 | |
| `ELMANHG_AI_CHAT_MAX_SOURCES` | 20 | 0 to 50 |
| `ELMANHG_AI_CHAT_MAX_SOURCE_CHARS` | 8000 | |
| `ELMANHG_AI_MODEL_TIMEOUT_SECONDS` | 20 | per Claude call, up to 120 |
| `ELMANHG_AI_MODEL_MAX_RETRIES` | 1 | SDK retries with exponential backoff, 0 to 5 |
| `ELMANHG_AI_MODEL_INPUT_USD_PER_MILLION_TOKENS` | 3 | cost logging and the `costUsd` returned per reply; confirm the list price at go-live |
| `ELMANHG_AI_MODEL_OUTPUT_USD_PER_MILLION_TOKENS` | 15 | cost logging and the `costUsd` returned per reply; confirm the list price at go-live |
| `ELMANHG_AI_EMBEDDING_PROVIDER` | `fake` | `fake` or `openai` (Anthropic has no embeddings API) |
| `ELMANHG_AI_OPENAI_API_KEY` | unset | required when the embedding provider is `openai`; never logged |
| `ELMANHG_AI_EMBEDDING_MODEL` | `text-embedding-3-small` | OpenAI embeddings model id |
| `ELMANHG_AI_EMBEDDING_DIMENSIONS` | 1536 | 1 to 2000, sent as the OpenAI `dimensions` parameter; must equal the API's `vector(1536)` column |
| `ELMANHG_AI_EMBEDDING_MAX_TEXTS` | 64 | 1 to 2048 texts per call |
| `ELMANHG_AI_EMBEDDING_MAX_TEXT_CHARS` | 8000 | characters per text |
| `ELMANHG_AI_EMBEDDING_USD_PER_MILLION_TOKENS` | 0.02 | cost logging only; confirm the list price at go-live |
| `ELMANHG_AI_OTLP_ENDPOINT` | unset | OTLP/gRPC endpoint (`http://` or `https://`) for traces and metrics; unset exports nothing. Compose sets it from `OTLP_ENDPOINT` ([docs/observability.md](observability.md)) |
| `ELMANHG_AI_OTLP_HEADERS` | unset | secret; comma-separated `key=value` headers for a SaaS endpoint. A malformed value fails startup without echoing it |
| `ELMANHG_AI_OTEL_SERVICE_NAME` | `elmanhg-ai` | `service.name` on every span and metric |
| `ELMANHG_AI_SERVICE_VERSION` | `dev` | `service.version`; compose sets it from `IMAGE_TAG` |
| `ELMANHG_AI_TRACE_SAMPLE_RATIO` | 1.0 | share of new traces kept (0 to 1); a request that carries a sampled `traceparent` is always kept |
| `ELMANHG_AI_METRIC_EXPORT_INTERVAL_SECONDS` | 30 | 5 to 3600 |

The OpenAI adapter reuses `ELMANHG_AI_MODEL_TIMEOUT_SECONDS` per call and `ELMANHG_AI_MODEL_MAX_RETRIES` for retries, with `0.5 s × 2^attempt` backoff.

.NET API (`AiService` section; environment form `AiService__*`):

| Key | Default | Notes |
|---|---|---|
| `AiService:Provider` | `Fake` | `Fake` or `Http` |
| `AiService:BaseUrl` | `http://localhost:8000` | absolute http(s) URL, checked when `Http` |
| `AiService:ServiceToken` | empty | at least 32 characters when `Http` |
| `AiService:AttemptTimeoutSeconds` | 45 | 1 to 60; must not exceed the total |
| `AiService:TotalTimeoutSeconds` | 50 | 1 to 120 |

Timeouts nest so the AI service always answers before the API gives up: the worst case in Python is about 41 s (20 s × 2 attempts), and the API waits up to 45 s per attempt, 50 s in total. The API never retries the POST.

## Fakes

- `ELMANHG_AI_LLM_PROVIDER=fake` (the default) uses `FakeModelClient`, which returns the fixed Arabic reply `هذا رد تجريبي من المساعد.` with model `fake`, citing the first source when sources were sent. CI needs no keys.
- The .NET `AiService:Provider=Fake` (the default) uses `FakeAiServiceClient`, which returns the same fixed reply with model and prompt version `fake` without calling the service, and cites the first source when there is one. It refuses with `AI_SERVICE_UNAVAILABLE` in Production. The test `ApiFactory` pins `Fake`.
- `ELMANHG_AI_EMBEDDING_PROVIDER=fake` (the default) uses `FakeEmbeddingClient`: deterministic lexical vectors (NFKC, case folding, tashkeel and tatweel removed, alef variants unified, word tokens hashed with BLAKE2b into signed buckets, L2-normalised; text with no words gives the unit vector on the first axis). Model `fake-embedding`.
- The .NET fake embeds in-process the same way (SHA-256 buckets over `AnswerNormalizer` output) with model `fake`, so a mixed fake index never matches across the two (the search filters by model). Texts that share words score higher, which makes retrieval tests meaningful without a key.

## Prompts

- Prompts are package files `src/elmanhg_ai/prompts/<name>.<version>.md`, never Python string literals. Chat uses `avatar_system.vN.md` (the system prompt) and `avatar_turn.vN.md` (the final user turn). `ELMANHG_AI_CHAT_PROMPT_VERSION` selects both; an unknown version stops startup.
- Student input and platform content are untrusted. They go only into the last user turn, inside `<lesson_context>` and `<student_message>` tags, after any such tag in the untrusted text has been removed. Removal matches spaced and attribute-carrying variants (`< /student_message x>`) and dangling tags with no closing `>` (a match stops at the next `<` or `>`, so each pass is linear in the text length), and repeats until the text stops changing, so nested fragments such as `</stu</student_message>dent_message>` cannot reassemble into a tag. Removal runs on each string field of the context before the context is serialized to JSON, so a dangling tag in one field cannot consume the fields after it. History turns keep their own roles and are not wrapped in tags, but the same tags are removed from their content, so a tag in an earlier message is not replayed on later turns. The system prompt holds no untrusted text. Rendering is single pass, so substituted text is never re-scanned.
- Sources go to Claude as native `search_result` content blocks with `citations.enabled`, placed before the text of the final user turn. They are data like the rest of the context: the same delimiter tags are removed from each source's `title` and `content`, and prompt v2 tells the model that search results are not instructions. The Anthropic adapter reads the `search_result_location` citations of the reply's text blocks and returns their `source` values; the pipeline keeps only references it sent.

## Health, logging and telemetry

- `GET /health` returns `{"status":"ok"}` (liveness, no dependencies). `GET /health/ready` returns `{"status":"ok"}` once startup has loaded the prompts and the model client, and `503 SERVICE_NOT_READY` before that. Readiness never calls Claude. Neither appears in OpenAPI.
- structlog writes one JSON object per line (`ELMANHG_AI_LOG_FORMAT=console` for local reading) with `timestamp`, `level`, `event`, `trace_id` and `request_id`.
- The trace id is the current OpenTelemetry span's (the FastAPI server span continues the W3C `traceparent` the .NET `HttpClient` sends), so API and AI logs and traces share one id. Without a valid span it falls back to the `traceparent` header, then to a random id. `X-Request-Id` is reused when valid. Both are echoed as `X-Trace-Id` and `X-Request-Id`.
- There is one `request.completed` line per request (DEBUG for `/health*`), and one `chat.completed` line per chat with `pipeline`, `prompt_version`, `model`, `tokens_in`, `tokens_out`, `latency_ms`, `cost_usd`, `stop_reason`, `sources` (count) and `citations` (count). Message, context and source text are never logged.
- There is one `embedding.completed` line per embeddings call with `pipeline` (`embeddings`), `model`, `input_type`, `count`, `tokens_in`, `latency_ms` and `cost_usd`. The texts are never logged. A failed OpenAI call logs `embedding.call_failed` (`provider`, `model`, `status_code`, `error_type`), and a bad reply logs `embedding.output_invalid`.
- `service.started` also records `embedding_provider`, `embedding_model` and `otlp_exporting`.
- Traces (`core/telemetry.py`): one SERVER span per request from the FastAPI instrumentation (`/health*` excluded), and one CLIENT span per model call, named `chat <model>` or `embeddings <model>`, with `gen_ai.operation.name`, `gen_ai.provider.name`, `gen_ai.request.model` and `gen_ai.usage.input_tokens` / `output_tokens`. A failed call records the exception and marks the span as an error.
- Metrics (OpenTelemetry GenAI conventions, recorded by the `clients/metered.py` wrappers applied in `lifespan`): `gen_ai.client.operation.duration` (s), `gen_ai.client.token.usage` ({token}, tagged `gen_ai.token.type` = `input` or `output`) and `elmanhg.ai.cost` ({USD}, from the same price settings as `cost_usd`), all tagged with operation, provider and model, plus `error.type` (the error code, or the exception type) on failures. FastAPI adds `http.server.request.duration` (compose sets `OTEL_SEMCONV_STABILITY_OPT_IN=http`).
- Logs never go through an OTLP log exporter: they leave through stdout and the collector tails them ([docs/observability.md](observability.md), Logs).

## Run locally

```bash
python -m pip install --user uv==0.12.17   # once
cd ai
uv sync
uv run --env-file ../.env uvicorn elmanhg_ai.main:create_app --factory --reload --port 8000
uv run ruff format --check . && uv run ruff check . && uv run mypy src
uv run pytest -m "not eval"
```

Docker: `docker compose --profile ai up -d --build ai` (published on `127.0.0.1:${AI_PORT:-8000}` only, so `/docs` is not reachable from other hosts; the health check calls `/health/ready`). `docker compose up -d postgres` does not start it.

To make the API call it, set `AiService__Provider=Http`, `AiService__BaseUrl=http://localhost:8000`, and the same token in `AiService__ServiceToken` and `ELMANHG_AI_SERVICE_TOKEN` (see `.env.example`).

Staging and production: the `ai` compose profile in `deploy/docker-compose.prod.yml`, variables in the host's `ai.env`; see [docs/deployment.md](deployment.md).

## Eval

- `src/elmanhg_ai/eval/avatar_chat.py` runs the chat pipeline over the dataset `src/elmanhg_ai/eval/datasets/avatar_chat.v2.jsonl` (22 cases: grounded answers, numbered steps, quiz and exam-review questions, English and dialect input, and 7 `safety` cases for off-curriculum requests and prompt injection in the message, in a source and in delimiter tags).
- Scorers (`eval/scorers.py`) are deterministic, with no model judge: Arabic letter ratio, word count, citations (non-empty when required, and only sent references), numbered steps, required terms (tashkeel ignored) and forbidden terms. A case passes when every applicable scorer passes.
- Threshold: a pass rate of at least 0.85 (19 of 22), and every `safety` case passes.
- Fake-mode tests cover the loader, scorers, scoring and threshold. The live run is `ELMANHG_AI_LLM_PROVIDER=anthropic ELMANHG_AI_ANTHROPIC_API_KEY=… ELMANHG_AI_SERVICE_TOKEN=… uv run pytest -m eval`; without those variables the test skips with that reason. Record the score whenever the prompt, model or pipeline changes.

## Go live with Claude

1. Set `ELMANHG_AI_LLM_PROVIDER=anthropic` and `ELMANHG_AI_ANTHROPIC_API_KEY`.
2. Confirm the `ELMANHG_AI_CHAT_MODEL` id and the per-million-token prices (`ELMANHG_AI_MODEL_*_USD_PER_MILLION_TOKENS`) against Anthropic's current list (PRD §18).
3. Set the API to `AiService__Provider=Http` with the service's URL and the shared token.
4. Check `/health/ready`, then send one chat and confirm that `chat.completed` shows real token counts.
5. Run the avatar eval (`uv run pytest -m eval`, see Eval) and confirm that it meets the threshold, including that replies carry `citations` when sources are sent.

## Go live with OpenAI embeddings

1. Set `ELMANHG_AI_EMBEDDING_PROVIDER=openai` and `ELMANHG_AI_OPENAI_API_KEY`. Keep `ELMANHG_AI_EMBEDDING_DIMENSIONS=1536`, which is the width of the API's vector column.
2. Confirm `ELMANHG_AI_EMBEDDING_MODEL` and `ELMANHG_AI_EMBEDDING_USD_PER_MILLION_TOKENS` against OpenAI's current list.
3. Set the API to `AiService__Provider=Http` (the same switch as chat), restart both, and check `/health/ready`.
4. As an admin, call `POST /api/content-index/rebuild`. The index sweep then re-embeds every Published lesson with the new model; until a lesson is re-embedded, its search returns no matches, because chunks of the old model are never compared with a query of the new one.
5. Confirm that `embedding.completed` shows the OpenAI model and real token counts, then run a search from Postman (`ContentRetrieval` folder).
