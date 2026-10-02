VERDICT: APPROVED

# Review r2 — OpenAI-compatible LLM provider replacing Anthropic (#257, E18.S5)

Worktree HEAD is `3c2b3fa` (origin/main, includes #259); changes uncommitted. `git diff HEAD --stat` touches only this story's files; #259 content in deployment.md / observability.md / implementation-report.md is intact (no reversal).

## Blocking
None.

## Non-blocking
- `ai/src/elmanhg_ai/settings.py:123` — the non-blank `return value` branch of `_blank_llm_api_key_is_none` is reported uncovered by coverage (96% file). A test constructing Settings with a real `llm_api_key` and asserting it survives would close it.
- Docker build and `/health/ready` were not re-run by the reviewer (claimed OK in 02-implementation-r2.md on host port 18257).

## Verified
- **Finding 1 (blocking) resolved:** `docs/avatar.md:134` now says default `v3`; `docs/avatar.md:99` example shows `"promptVersion": "v3"`; matches `settings.py:36`. `docs/ai-service.md:56` example also `v3`; `ai-service.md:332` and `deployment.md:302` agree.
- **Citation regex** (`citations.py:11-12`): ` ?\[ *+([a-z0-9-]++(?: *+[,\u060c] *+[a-z0-9-]++)*+) *+\]`. Correct: a failed group iteration is fully backtracked before the possessive closes, so `[a ]`, `[ a ، b ]`, `[a ,b ]` match; `[a,]` and unknown-only `[x-1]` stay verbatim; only supplied ids are accepted (`citations.py:36`). Independent timing on 200k-char adversarial inputs (`[`+ids without `]`, long space runs, ` [` repeated, mixed separators): all < 5 ms — linear.
- **Userinfo rejection** (`settings.py:114-115`): any `@` in `urlsplit(url).netloc` raises "must not contain credentials"; `hide_input_in_errors=True` (`settings.py:22`) keeps the key out of the error; test `test_settings.py:265` asserts both loc and that `sk-secret` is absent. Documented at `docs/ai-service.md:326`.
- **Compose pass-through** (`docker-compose.yml:29-31`): STRUCTURED_OUTPUT, MAX_TOKENS_FIELD, REASONING_EFFORT added with defaults equal to code defaults (`settings.py:32-34`).
- **New tests** exist and constrain behaviour: Arabic comma, inner spaces, Arabic-comma unknown-only passthrough, single-bracket 10000-id unterminated perf (`test_citations.py:40-80`).
- **CI re-run** (ai/, `python -m uv`, per ai-ci.yml): `uv sync --locked` OK; `ruff format --check` 143 files formatted; `ruff check` passed; `mypy src` no issues in 75 files; `pytest -m "not eval"` with branch coverage 487 passed, 4 deselected, TOTAL 96%, citations.py and openai_compatible_model.py 100%; `uv export` + `pip-audit==2.10.1` no known vulnerabilities.
- Deviations: None — confirmed; the rework matches the r2 report.

## Test quality
- test_citations.py: the new cases would fail if the Arabic comma or inner-space support were removed, or if the regex became super-linear (the 1 s budget is generous but the 10000-id input would catch catastrophic backtracking).
- test_settings.py userinfo test: fails if the `@` check is removed (no ValidationError) or if input echo is re-enabled.
