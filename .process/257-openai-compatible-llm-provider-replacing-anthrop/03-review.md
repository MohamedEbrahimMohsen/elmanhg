VERDICT: CHANGES_REQUESTED

# Review — OpenAI-compatible LLM provider replacing Anthropic (#257, E18.S5)

Reviewed `git diff HEAD` plus untracked and staged files. The worktree HEAD is `6083cca`, one commit behind `origin/main` (`3c2b3fa`, #259). Because of that, `git diff origin/main` also shows #259 reversed (alertmanager, deploy/lib.sh, observability.md and so on). Those lines are not part of this story.

## Blocking

### 1. docs/avatar.md still gives the AI service default chat prompt version as `v2`
**Where:** `docs/avatar.md:134` ("`ELMANHG_AI_CHAT_PROMPT_VERSION` (default `v2`)") vs `ai/src/elmanhg_ai/settings.py:36` (`chat_prompt_version` default `"v3"`)
**Rule:** `.claude/rules/docs-sync.md` (divergence); plan Decision 15
**Problem:** The change moves the default prompt to v3. ai-service.md, deployment.md, .env.example and deploy/ai.env.example were updated to match, but avatar.md, the doc that owns this feature, still states the old default.
**Failure:** A reader asks "what prompt does the AI service use by default?". avatar.md §Configuration answers `v2`; the code (and `test_settings_defaults_select_fake_provider_and_v3_prompt`) answers `v3`.
**Fix:** Change `docs/avatar.md:134` to "default `v3`". It would also be tidy to update the response example at `docs/avatar.md:99` (`"promptVersion": "v2"`, next to the new `gpt-5.6-luna` model) to `v3`.

## Non-blocking
- Base branch: the worktree must be rebased onto `origin/main` before the PR. #259 also edits `docs/deployment.md`, `docs/implementation-report.md` and `docs/observability.md`, so expect conflicts there.
- `docs/ai-service.md:56`: the `/v1/chat` response example still shows `"promptVersion": "v2"`. This is illustrative only, but it now sits next to a v3 default.
- `ai/src/elmanhg_ai/clients/citations.py:11`: the marker grammar accepts only an ASCII comma and no inner spaces. An Arabic reply that writes `[explanation-1، summary-1]` or `[ explanation-1 ]` keeps the raw marker in the text and loses the citation. This fails safe (nothing unsupplied is accepted). Watch for it in the live avatar eval (D-1).
- `ai/tests/unit/test_citations.py:40`: the perf input ends every run at the next `[`, so it never exercises a long unterminated id list. One `[` followed by 10000 repetitions of `a-1, ` would be the stronger case. The regex is linear either way: the comma is outside the id class, so backtracking is O(1) per id.
- `docker-compose.yml:27-29`: the dev compose passes only the provider, key, base URL and model. `LLM_STRUCTURED_OUTPUT`, `_MAX_TOKENS_FIELD` and `_REASONING_EFFORT` cannot be set for a DeepSeek dev run without editing compose. Prod uses `env_file`, so it is unaffected.
- `ai/src/elmanhg_ai/main.py:76` logs `llm_base_url`. `_llm_base_url_is_https` (`settings.py:107`) does not reject userinfo, so a URL of the form https://user:key@host would be logged. Rejecting userinfo in the validator would close this.
- Skill §6.3 (prompt v3 shipped without an eval run): accepted, because the plan deferred it explicitly as D-1 (no key). The live scores must be recorded at go-live.

## Verified
- **Intent:** `OpenAiCompatibleModelClient` posts to {base}/chat/completions through `post_with_retries`: 429, 5xx and transport errors are retried with 0.5 x 2^n backoff; any other 4xx fails at once and becomes DEPENDENCY_UNAVAILABLE; the per-request `httpx2.Timeout` comes from the pipeline timeout or `model_timeout_seconds`. `max_completion_tokens`/`max_tokens` follow the setting, `reasoning_effort` is sent unless `default`, and `temperature` is never sent. `usage` maps to the input and output token counts; cost is metered by the unchanged `MeteredModelClient` (main.py:59) with the 0.20/1.20 defaults. A missing choices or usage, an unreadable body or empty content raises MODEL_OUTPUT_INVALID. `aclose` is called in the lifespan (main.py:93).
- **No secrets or prompt text in logs:** the client logs only provider, model, error_type, status_code and stop_reason. The ValidationError is chained but not logged, because problems.py logs exc_info only for unhandled errors. The key appears only in the Authorization header built in `from_settings`. `api_key_for` re-checks for blank keys, which covers `model_copy` bypassing the validators.
- **Citations:** `extract_citations` accepts only ids in `request.sources` (set membership), keeps them distinct and in first-appearance order, and leaves unknown-only brackets verbatim. The pipeline filter (chat.py:134-135) still applies as defence in depth. Only the model reply is parsed, never lesson content or student text. Echoed brackets can at most cite a reference that was actually supplied. .NET references are explanation-N and summary-N (LessonContentMatchResultGenerator.cs:17-19), so math such as [x-1] is never touched.
- **Injection defences:** `lesson_sources` was added to DELIMITER_TAG (chat.py:27), so it is stripped from the message, history, context and sources. `sources_json` strips it again from title and content. `render` substitutes in a single pass (loader.py:40), so a placeholder inside a source cannot expand. JSON escaping plus tag stripping means a source cannot close the block. The 58.7k-char perf test and every existing delimiter test pass unchanged (test_chat_pipeline.py only has appends).
- **Structured output:** strict json_schema by default. Both grading schemas are strict-compatible: every property is required and additionalProperties is false at every level. json_object mode appends the rendered json_output.v1.md to the system prompt (the schema comes from code, so it is trusted). Replies are still validated by the pipelines.
- **Contract:** ModelRequest and ModelReply are unchanged. ai/openapi/v1.json, api/, web/ and postman/ have no diff (`git diff --quiet HEAD`). There is no new endpoint, so no Postman change was needed (and ai/ has no Postman, delta 6).
- **Plan contract fidelity:** all 13 files to create exist and match the contracts. avatar_system.v3.md differs from v2 only in rules 3 and 7, with the exact plan text. avatar_turn.v3.md is byte-identical to v2 (checked with cmp). The settings fields, validators, `_filled` and defaults match. anthropic_model.py, its tests and its fixtures are deleted. anthropic is gone from pyproject.toml and uv.lock; the only ai/ match is test #39, which the plan itself requires (disclosed).
- **Conftest deviations:** acceptable, not masking. The price pin keeps the cost-arithmetic tests independent of the defaults. The essay and math-step model pin (claude-sonnet-5) makes the forwarding tests stronger, because the pinned grading models now differ from chat_model (gpt-5.6-luna), so a pipeline that sent the chat model would fail. The new defaults are asserted in test_settings.py. Both deviations are disclosed.
- **Docs:** constitution, PRD §18, implementation report §3/§4/§6, ai-service, deployment, security, observability, essay-grading, math-step-grading, prototype and the skill deltas were all updated. The remaining anthropic or claude matches are "never Anthropic" statements, historical rows (implementation-report L147/L270, backlog.json:375), design-tool references and test data. The one divergence is finding 1.
- **CI re-run** from ai/ with `python -m uv` (uv 0.12.17):
  - uv sync --locked: OK.
  - ruff format --check: 143 files already formatted.
  - ruff check: all checks passed.
  - mypy src: no issues in 75 files.
  - pytest -m "not eval" with coverage: 482 passed, 4 deselected, TOTAL 96%; citations.py and openai_compatible_model.py at 100% branch.
  - uv export + pip-audit 2.10.1: no known vulnerabilities found.
  - Docker build and the readiness check were not re-run.

## Test quality
- test_openai_compatible_model.py: constrains the implementation. It asserts the real wire body (path, bearer, messages, token field, no temperature, response_format shapes, read timeout via request extensions), retry counts and delays, the error codes, the provider label in logs, and citation stripping against recorded fixtures. No test only asserts a value it fed to a substitute.
- test_citations.py: constrains order, distinctness, mixed groups, unknown-only passthrough, adjacent markers, compact JSON and nested-tag stripping. The perf case is weaker than it could be (see non-blocking).
- test_openai_compatible_endpoints.py: a real end-to-end through create_app with a MockTransport client. It checks the public citations, the clean reply, promptVersion v3 and the schema sent.
- test_settings.py additions: each targets one validator or default (https, trailing slash, blank key to None, key requirement, anthropic rejected).
- test_chat_pipeline.py #40 and test_prompt_loader.py #41/#42: meaningful. #40 fails if SOURCES_TAG is dropped from DELIMITER_TAG.
