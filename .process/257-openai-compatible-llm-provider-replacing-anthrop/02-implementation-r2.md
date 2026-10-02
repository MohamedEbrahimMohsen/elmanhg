# Implementation r2: OpenAI-compatible LLM provider replacing Anthropic (#257)

## Findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 (blocking) | Default chat prompt is now `v3` in avatar.md, and the response example shows `"promptVersion": "v3"` | `docs/avatar.md:134`, `docs/avatar.md:99` |
| NB ai-service example | `/v1/chat` response example now shows `v3` | `docs/ai-service.md:56` |
| NB citations | Marker grammar now accepts `,` or the Arabic comma `،` (`،`) and allows spaces inside the brackets (`[ ref-1 ]`, `[a ,b ]`). Only supplied references are accepted. Quantifiers are possessive (`*+`, `++`), so matching stays linear. | `ai/src/elmanhg_ai/clients/citations.py:11-12,35` |
| NB citations tests | Added Arabic-comma, inner-space and Arabic-comma-unknown-only tests. Added a stronger speed test: one `[` followed by 10000 unterminated ids, using both separators. | `ai/tests/unit/test_citations.py:40-80` |
| NB userinfo | The `llm_base_url` validator rejects any netloc that contains `@` ("must not contain credentials"). The test also asserts the secret is not echoed in the error (`hide_input_in_errors` was already set). The rule is documented in the ai-service.md settings table. | `ai/src/elmanhg_ai/settings.py:4,114-115`, `ai/tests/unit/test_settings.py:265`, `docs/ai-service.md:326` |
| NB dev compose | Dev compose now passes `ELMANHG_AI_LLM_STRUCTURED_OUTPUT`, `_MAX_TOKENS_FIELD` and `_REASONING_EFFORT` through, with the code defaults | `docker-compose.yml:29-31` |
| NB base branch | Ran `git stash`, `git merge origin/main` (fast-forward to `3c2b3fa`, #259) and `git stash pop --index`. deployment.md, implementation-report.md and observability.md auto-merged with no conflicts, and both sides were kept. Staged deletions were preserved. | n/a |

## Deviations
None.

## Build & test (ai/, `python -m uv`, uv 0.12.17, following ai-ci.yml)
- `uv sync --locked`: OK
- `uv run ruff format --check .`: 143 files already formatted
- `uv run ruff check .`: All checks passed!
- `uv run mypy src`: Success: no issues found in 75 source files
- `uv run pytest -m "not eval" --cov=elmanhg_ai --cov-branch --cov-report=term-missing`: 487 passed, 4 deselected; TOTAL 96%; citations.py and openai_compatible_model.py at 100%
- `uv export ...` + `uv tool run pip-audit==2.10.1 -r requirements-audit.txt`: No known vulnerabilities found
- `docker build`: OK. I ran the container on host port 18257 and `/health/ready` returned `{"status":"ok"}`

## Notes for review
- Nothing is committed.
