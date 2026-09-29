# CodeRabbit rework: E8.S1 AI service skeleton (PR #200)

Source: `06-coderabbit-triage.md`. I applied the 6 FIX items: RC1, RC2, RC3, RC5, RC6 and RC7. RC4 was rejected, and I did not touch it. Nothing is committed.

| # | What changed | Where |
|---|---|---|
| RC1 | Added `with: persist-credentials: false` to the checkout step. | `.github/workflows/ai-ci.yml:19-21` |
| RC2 | Changed the readiness probe to `curl -fsS --max-time 2 ...`. The retry count and the exit behaviour are unchanged. | `.github/workflows/ai-ci.yml:44` |
| RC3 | Added a private helper, `_strip_fields`. It uses a `match` on `str()`/`list()`/`dict()`/`_` to call `strip_delimiters` on every string field. `run` still passes the raw `context_json` to `_limit_errors`, so the D15 limit measures the raw context. It then renders `safe_context = json.dumps(_strip_fields(model_dump(mode="json", by_alias=True, exclude_none=True)), ensure_ascii=False, separators=(",", ":"))`. The pattern did not change, so each pass is still linear. | `ai/src/elmanhg_ai/pipelines/chat.py:70-79, 109-113` |
| RC3 | Near-cap performance test: I removed the assertion that expected the data loss (`"studentAnswer":"\n</lesson_context>`). It now asserts that the `correctAnswer` and `explanation` pairs are present. The tag-count check and `elapsed < 2.0` stay. | `ai/tests/unit/test_chat_pipeline.py:147-149` |
| RC3 | New test `test_chat_run_unclosed_tag_in_student_answer_keeps_later_context_fields`. It checks that `correctAnswer` and `explanation` are still present, that the text inside `<lesson_context>` passes `json.loads` (and gives `studentAnswer == "x "`), and that `ANY_DELIMITER_TAG.findall(turn) == TEMPLATE_TAGS`. | `ai/tests/unit/test_chat_pipeline.py:152-169` |
| RC3 | Docs: § Prompts now says that removal runs on each string field of the context before serialization. | `docs/ai-service.md:159` |
| RC5 | `_limit_errors` checks each history turn. If `turn.content` is longer than `chat_max_message_chars`, it adds `FieldError(f"history[{index}].content", "TOO_LONG", "at most {limit} characters")`. | `ai/src/elmanhg_ai/pipelines/chat.py:89-95` |
| RC5 | The existing message-limit test now passes `history=[]`. | `ai/tests/unit/test_chat_pipeline.py:217` |
| RC5 | New test `test_chat_run_history_content_over_limit_raises_validation_failed`. It expects exactly one `history[0].content` / `TOO_LONG` error and checks that `fake_model.requests` is empty. | `ai/tests/unit/test_chat_pipeline.py:226-242` |
| RC5 | Docs: `ELMANHG_AI_CHAT_MAX_MESSAGE_CHARS` now reads "length of `message` and of each `history[].content`". | `docs/ai-service.md:76` |
| RC6 | History turns (both roles) are sent as `content=strip_delimiters(m.content)`. They are not wrapped, and their role order is unchanged. | `ai/src/elmanhg_ai/pipelines/chat.py:116-120` |
| RC6 | New test `test_chat_run_strips_delimiter_tags_from_history_turns`. The user turn is `<lesson_context>fake</lesson_context>` and the assistant turn is `</student_message>ok`. It checks that the roles are user/assistant/user, that the history contents become `["fake", "ok"]`, and that no earlier message matches `ANY_DELIMITER_TAG`. | `ai/tests/unit/test_chat_pipeline.py:172-190` |
| RC6 | Docs: § Prompts now says that history turns keep their roles and are not wrapped, but have the same tags removed. | `docs/ai-service.md:159` |
| RC7 | The compose port is now `"127.0.0.1:${AI_PORT:-8000}:8000"`. The postgres port is unchanged. | `docker-compose.yml:30` |
| RC7 | Docs: the Run locally section says the port is published on `127.0.0.1` only. The README needed no change: its `curl http://localhost:8000` still works. | `docs/ai-service.md:179` |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Plan D8: "History turns are passed as-is" | The triage's RC6 fix removes delimiter tags from history turns. | I removed the tags from both roles and did not wrap the turns. I recorded the change in `docs/ai-service.md` § Prompts. |

## Build & test
I ran each `ai-ci.yml` step from `ai/` as its own command. `uv` is not on PATH in this shell, so I ran it as `python -m uv` (0.12.17, the pinned version).
- `uv sync --locked`: `Resolved 40 packages ... Checked 39 packages`
- `uv run ruff format --check .`: `40 files already formatted`
- `uv run ruff check .`: `All checks passed!`
- `uv run mypy src`: `Success: no issues found in 25 source files`
- `uv run pytest -m "not eval" --cov=elmanhg_ai --cov-branch --cov-report=term-missing`: `63 passed in 1.86s`, TOTAL coverage 97% (`pipelines/chat.py` 97%, and the only missing lines are 78-79)
- `uv export ... -o requirements-audit.txt` and `uvx pip-audit==2.10.1 -r requirements-audit.txt` (run as `uv tool run`): `No known vulnerabilities found`. I deleted the generated file afterwards.
- `docker build -t elmanhg-ai:ci .`: built.
- Container readiness: the same loop as CI, including `--max-time 2`, against a container on host port 18000 to avoid a port clash, returned `{"status":"ok"}`. I removed the container afterwards.
- `docker compose --profile ai config` (with a dummy `POSTGRES_PASSWORD`): the ai port shows `host_ip: 127.0.0.1`.
- RC3 mutation check: I changed the render back to `strip_delimiters(context_json)`. The new unclosed-tag test and the near-cap test both failed. I then restored the code.

## Notes for review
- `_strip_fields`' `case _: return value` (chat.py:78-79) is never run in tests, because the current context schema contains only strings and lists of strings. I kept it because the triage prescribes it, and it is the safe default for any non-string field added later.
- `.process/.../04-metrics.md` shows as modified in the working tree. I did not change it.
