# AI service (`ai/`)

## Role

`ai/` is an internal Python 3.13 FastAPI service (package `elmanhg_ai`, managed with uv). It answers avatar chat turns over the Claude API (PRD §9). Only the .NET API calls it, over HTTP with a shared service token; browsers never do. The .NET side of the contract is `api/Elmanhg.Application/Shared/AiService/` (`IAiServiceClient`), and the Python side is `ai/src/elmanhg_ai/api/chat/schemas.py`. Both change together, and `ai/openapi/v1.json` is the committed contract (regenerate with `uv run python -m elmanhg_ai.openapi_export` from `ai/`; a test fails on drift).

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
  "message": "لماذا إجابتي خطأ؟"
}
```

Response `200`:

```json
{
  "reply": "هذا رد تجريبي من المساعد.",
  "model": "fake",
  "promptVersion": "v1",
  "inputTokens": 0,
  "outputTokens": 0,
  "stopReason": "end_turn"
}
```

`model` and `promptVersion` are recorded per message (PRD §9.3).

Field rules:

| Entry point (`context.entryPoint`) | Required context |
|---|---|
| `lesson` | `lesson` |
| `quizQuestion` | `lesson` and `question` |
| `examReview` | `lesson` and `question` |
| `global` | nothing (`subjects` may list the student's subjects) |

- `history` alternates `user`, `assistant`, `user`, … and has an even length (it may be empty). The new `message` becomes the final user turn.
- `message`, `content`, `name` and `stem` must be non-empty.

Limits (checked by the pipeline; exceeding one returns `400 VALIDATION_FAILED` with field code `TOO_MANY_ITEMS` or `TOO_LONG`):

| Setting | Default | Applies to |
|---|---|---|
| `ELMANHG_AI_CHAT_MAX_HISTORY_MESSAGES` | 20 | number of `history` turns |
| `ELMANHG_AI_CHAT_MAX_MESSAGE_CHARS` | 4000 | length of `message` and of each `history[].content` |
| `ELMANHG_AI_CHAT_MAX_CONTEXT_CHARS` | 60000 | length of the serialized `context` JSON |

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
| `VALIDATION_FAILED` | 400 | Invalid body (FastAPI's 422 is mapped to 400) or a chat limit exceeded |
| `MALFORMED_REQUEST` | 4xx | Any other client HTTP error |
| `UNAUTHENTICATED` | 401 | Missing or wrong service token (`WWW-Authenticate: Bearer`) |
| `NOT_FOUND` | 404 | Unknown route |
| `METHOD_NOT_ALLOWED` | 405 | Wrong method |
| `MODEL_OUTPUT_INVALID` | 502 | The model returned no text |
| `DEPENDENCY_UNAVAILABLE` | 503 | The Claude API failed (any SDK error, after SDK retries) |
| `SERVICE_NOT_READY` | 503 | Startup has not finished |
| `INTERNAL_ERROR` | 500 | Anything unexpected (logged, never echoed) |

On the .NET side, `HttpAiServiceClient` throws `ServiceUnavailableCoreException(AI_SERVICE_UNAVAILABLE)` on a transport failure, any non-2xx response, or an unreadable or empty reply.

## Service auth

- Header `Authorization: Bearer <token>`, required on `/v1/*`. Health endpoints are anonymous.
- One shared secret: the API's `AiService:ServiceToken` equals the service's `ELMANHG_AI_SERVICE_TOKEN`. It must be at least 32 characters on both sides; both refuse to start otherwise (the API only when `Provider=Http`).
- The service compares tokens in constant time (`secrets.compare_digest`). The token is never logged. Settings validation errors at startup omit input values (`hide_input_in_errors`), so a rejected token or key never reaches the container log.
- Rotation: change `AiService__ServiceToken` and `ELMANHG_AI_SERVICE_TOKEN` together, then restart both.

## Configuration

AI service (`ELMANHG_AI_*` environment variables; `settings.py` is the only place they are read):

| Variable | Default | Notes |
|---|---|---|
| `ELMANHG_AI_ENV` | `development` | `development`, `testing` or `production`. Production hides `/docs` and `/openapi.json`. |
| `ELMANHG_AI_LOG_LEVEL` | `INFO` | `DEBUG`, `INFO`, `WARNING`, `ERROR` |
| `ELMANHG_AI_LOG_FORMAT` | `json` | `json` or `console` |
| `ELMANHG_AI_SERVICE_TOKEN` | required | at least 32 characters |
| `ELMANHG_AI_LLM_PROVIDER` | `fake` | `fake` or `anthropic` |
| `ELMANHG_AI_ANTHROPIC_API_KEY` | unset | required when the provider is `anthropic` |
| `ELMANHG_AI_CHAT_MODEL` | `claude-sonnet-5` | Claude model id |
| `ELMANHG_AI_CHAT_PROMPT_VERSION` | `v1` | pattern `v<number>`; selects both chat prompt files |
| `ELMANHG_AI_CHAT_MAX_TOKENS` | 1024 | 1 to 8192 |
| `ELMANHG_AI_CHAT_MAX_HISTORY_MESSAGES` | 20 | 0 to 100 |
| `ELMANHG_AI_CHAT_MAX_MESSAGE_CHARS` | 4000 | |
| `ELMANHG_AI_CHAT_MAX_CONTEXT_CHARS` | 60000 | |
| `ELMANHG_AI_MODEL_TIMEOUT_SECONDS` | 20 | per Claude call, up to 120 |
| `ELMANHG_AI_MODEL_MAX_RETRIES` | 1 | SDK retries with exponential backoff, 0 to 5 |
| `ELMANHG_AI_MODEL_INPUT_USD_PER_MILLION_TOKENS` | 3 | cost logging only; confirm the list price at go-live |
| `ELMANHG_AI_MODEL_OUTPUT_USD_PER_MILLION_TOKENS` | 15 | cost logging only; confirm the list price at go-live |

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

- `ELMANHG_AI_LLM_PROVIDER=fake` (the default) uses `FakeModelClient`, which returns the fixed Arabic reply `هذا رد تجريبي من المساعد.` with model `fake`. CI needs no keys.
- The .NET `AiService:Provider=Fake` (the default) uses `FakeAiServiceClient`, which returns the same fixed reply with model and prompt version `fake` without calling the service. It refuses with `AI_SERVICE_UNAVAILABLE` in Production. The test `ApiFactory` pins `Fake`.

## Prompts

- Prompts are package files `src/elmanhg_ai/prompts/<name>.<version>.md`, never Python string literals. Chat uses `avatar_system.vN.md` (the system prompt) and `avatar_turn.vN.md` (the final user turn). `ELMANHG_AI_CHAT_PROMPT_VERSION` selects both; an unknown version stops startup.
- Student input and platform content are untrusted. They go only into the last user turn, inside `<lesson_context>` and `<student_message>` tags, after any such tag in the untrusted text has been removed. Removal matches spaced and attribute-carrying variants (`< /student_message x>`) and dangling tags with no closing `>` (a match stops at the next `<` or `>`, so each pass is linear in the text length), and repeats until the text stops changing, so nested fragments such as `</stu</student_message>dent_message>` cannot reassemble into a tag. Removal runs on each string field of the context before the context is serialized to JSON, so a dangling tag in one field cannot consume the fields after it. History turns keep their own roles and are not wrapped in tags, but the same tags are removed from their content, so a tag in an earlier message is not replayed on later turns. The system prompt holds no untrusted text. Rendering is single pass, so substituted text is never re-scanned.

## Health and logging

- `GET /health` returns `{"status":"ok"}` (liveness, no dependencies). `GET /health/ready` returns `{"status":"ok"}` once startup has loaded the prompts and the model client, and `503 SERVICE_NOT_READY` before that. Readiness never calls Claude. Neither appears in OpenAPI.
- structlog writes one JSON object per line (`ELMANHG_AI_LOG_FORMAT=console` for local reading) with `timestamp`, `level`, `event`, `trace_id` and `request_id`.
- The trace id comes from the W3C `traceparent` header (the .NET `HttpClient` sends it), so API and AI logs share one id; otherwise the service generates one. `X-Request-Id` is reused when valid. Both are echoed as `X-Trace-Id` and `X-Request-Id`.
- There is one `request.completed` line per request (DEBUG for `/health*`), and one `chat.completed` line per chat with `pipeline`, `prompt_version`, `model`, `tokens_in`, `tokens_out`, `latency_ms`, `cost_usd` and `stop_reason`. Message and context text are never logged.

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

## Go live with Claude

1. Set `ELMANHG_AI_LLM_PROVIDER=anthropic` and `ELMANHG_AI_ANTHROPIC_API_KEY`.
2. Confirm the `ELMANHG_AI_CHAT_MODEL` id and the per-million-token prices (`ELMANHG_AI_MODEL_*_USD_PER_MILLION_TOKENS`) against Anthropic's current list (PRD §18).
3. Set the API to `AiService__Provider=Http` with the service's URL and the shared token.
4. Check `/health/ready`, then send one chat and confirm that `chat.completed` shows real token counts.
