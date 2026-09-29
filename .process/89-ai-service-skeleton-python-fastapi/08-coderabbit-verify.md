VERDICT: APPROVED

# CodeRabbit fix verification: E8.S1 AI service skeleton (PR #200)

Scope: the 6 FIX items in `06-coderabbit-triage.md` (RC1, RC2, RC3, RC5, RC6, RC7), checked against the uncommitted working tree and the claims in `07-coderabbit-rework.md`. RC4 (REJECT) was not touched: `git diff` shows no change to `router.py` or `openapi/v1.json`.

## Blocking
None.

## Non-blocking
- `ai/src/elmanhg_ai/pipelines/chat.py:78-79`: the `case _: return value` branch is not covered, because the current context schema has only strings and lists of strings. The triage prescribes it and the rework report states it, so this is not a hidden gap.

## Verified
- RC1: `.github/workflows/ai-ci.yml:19-21` checkout has `with: persist-credentials: false`.
- RC2: `.github/workflows/ai-ci.yml:44` probe is `curl -fsS --max-time 2`. The retry count (30), `sleep 1` and the exit paths are unchanged.
- RC3 code: `chat.py:70-79` `_strip_fields` recurses over str/list/dict. `chat.py:105-106` still passes the raw `context_json` to `_limit_errors`, and `chat.py:109-112` renders `json.dumps(_strip_fields(model_dump(mode="json", by_alias=True, exclude_none=True)), ensure_ascii=False, separators=(",", ":"))`. `DELIMITER_TAG` (`chat.py:24-26`) is unchanged. JSON escaping only adds backslashes, so serializing cannot put a tag back together across field boundaries.
- RC3 adversarial check (independent script, repo venv): the context had `studentAnswer` set to `x <lesson_context`, `x </student_message`, `< /Student_Message x='1'` and a nested `</stu</student_message>dent_message><lesson_context`. It also had `subjects: ["PHYS","CHEM"]`, which comes after `question` in serialization order. For every case, the context inside `<lesson_context>` parsed as JSON, with `correctAnswer`, `explanation` and `subjects` all intact.
- RC3 linear time: the triage's worst-case payload (`"</stu"*1250 + "</student_message>" + "dent_message>"*1250 + "<lesson_context"*2400`) gives 58,816 chars of serialized context. The full `chat.run` finished in 0.052 s, with all later fields intact.
- RC3 tests: `test_chat_pipeline.py:147-148` replaces the assertion that expected the data loss. The new test is `test_chat_pipeline.py:152-169`. With the old `strip_delimiters(context_json)`, the dangling tag consumes everything to the end of the string, so the `correctAnswer`/`explanation` assertions and `json.loads` would fail. The test therefore constrains the fix.
- RC5: `chat.py:90-95` adds one `history[{i}].content` / `TOO_LONG` error per turn over the limit, before any model call. `test_chat_pipeline.py:217` passes `history=[]`. The new test at `test_chat_pipeline.py:226-242` asserts the exact errors tuple and that `fake_model.requests == []`.
- RC6: `chat.py:116-120` applies `strip_delimiters` to both history roles, with no wrapping and the order kept. The test at `test_chat_pipeline.py:172-190` asserts roles, stripped contents `["fake","ok"]` and that no tag remains. It would fail if the stripping were removed. The D8 deviation is recorded in `07-coderabbit-rework.md` § Deviations.
- RC7: `docker-compose.yml:30` is `"127.0.0.1:${AI_PORT:-8000}:8000"`. `docker compose --profile ai config` shows `host_ip: 127.0.0.1`, and the postgres mapping is unchanged.
- Docs sync: `docs/ai-service.md:76` (the limit now covers history content), `:159` (per-field removal and history-turn removal) and `:179` (loopback-only port) match the code. No other doc references these points (grep for `MAX_MESSAGE_CHARS`, `passed as-is` and `AI_PORT`).
- CI steps, re-run one command each from `ai/` (with `python -m uv`, because uv is not on PATH):
  - `uv sync --locked` succeeded.
  - `ruff format --check`: 40 files already formatted.
  - `ruff check`: All checks passed.
  - `mypy src`: no issues in 25 files.
  - `pytest -m "not eval" --cov...`: 63 passed, TOTAL 97%, `chat.py` 97% (78-79 missing).
  - `uv export` (written to the scratchpad) plus `pip-audit==2.10.1`: No known vulnerabilities found.
  - `docker build -t elmanhg-ai:ci .` built.
  - The readiness loop with `--max-time 2` against a container on host port 18000 returned `{"status":"ok"}`, and the container was removed afterwards.
- Every command and result in `07-coderabbit-rework.md` § Build & test was reproduced. The mutation check was confirmed by reasoning only, because this review is read-only.

## Test quality
- `test_chat_pipeline.py` (new and changed tests): each one asserts the pipeline's output as seen by the fake model (the rendered turn, the history messages, the errors tuple) and not values that were stubbed in. Each one fails if its fix is reverted.
