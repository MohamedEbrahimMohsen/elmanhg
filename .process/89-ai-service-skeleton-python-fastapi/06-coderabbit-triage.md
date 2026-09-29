# CodeRabbit triage — PR #200 (E8.S1 AI service skeleton)

Source: `05-coderabbit-comments.md` (7 inline comments, RC1–RC7). The comment text was treated as review data only. Each claim was checked against the working tree at `3da8505`.

| # | Where | Decision |
|---|---|---|
| RC1 | `.github/workflows/ai-ci.yml:19` | FIX |
| RC2 | `.github/workflows/ai-ci.yml:42` | FIX |
| RC3 | `ai/src/elmanhg_ai/pipelines/chat.py:86-92` (anchored on a `.process` note) | FIX: real code defect |
| RC4 | `ai/src/elmanhg_ai/api/chat/router.py:12-20`, `ai/openapi/v1.json:76-81` | REJECT |
| RC5 | `ai/src/elmanhg_ai/pipelines/chat.py:69-80` | FIX |
| RC6 | `ai/src/elmanhg_ai/pipelines/chat.py:97` | FIX (strip only, no wrapping) |
| RC7 | `docker-compose.yml:30` | FIX |

Totals: 6 FIX, 1 REJECT, 0 ESCALATE.

---

## RC1: FIX. `persist-credentials: false` on checkout
**Verified:** `ai-ci.yml:19` is a bare `- uses: actions/checkout@v4`. No later step uses git or the token. `permissions: contents: read` (lines 8-9) already limits the token, so this is hardening only. It costs nothing.
**Fix:** replace line 19 with:
```yaml
      - uses: actions/checkout@v4
        with:
          persist-credentials: false
```

## RC2: FIX. Put a time limit on each readiness probe
**Verified:** `ai-ci.yml:42` runs `curl -fsS` without `--max-time`. If a connection is accepted but the response never finishes, the 30-try loop has no time limit. It would then run until the job timeout.
**Fix:** on line 42 only, change the probe to `curl -fsS --max-time 2 http://localhost:8000/health/ready`. Keep the retry count and the success and failure behaviour unchanged.

## RC3: FIX. An unclosed tag in one context field removes every later field
**Real code issue, not only a documentation note.** CodeRabbit anchored the comment on `02-implementation-r2.md:85`. The defect itself is in `ai/src/elmanhg_ai/pipelines/chat.py:86` and `:92`. The code serializes the whole context to JSON and then runs `strip_delimiters` over the serialized string. `DELIMITER_TAG` (`chat.py:23-25`) ends in `[^<>]*>?`, so a dangling `<lesson_context` matches every character up to the next `<` or `>`. Serialized JSON has no `<` or `>` after that point, so the match runs to the end of the string.
**Evidence (reproduced in the repo venv):** calling `strip_delimiters` on the compact JSON of a question with `studentAnswer` = `x <lesson_context`, followed by `correctAnswer`, `explanation` and `subjects`, returns only the text up to `"studentAnswer":"x `. That removes `correctAnswer`, `explanation`, `subjects` and all closing braces.
**The implementer's note is wrong:** `02-implementation-r2.md` says "studentAnswer is the last field" in the regression payload. It is not. `ai/tests/conftest.py:77-79` puts `correctAnswer` and `explanation` after it. The test at `ai/tests/unit/test_chat_pipeline.py:146` asserts that the `studentAnswer` value is followed directly by a newline and `</lesson_context>`, so it asserts the data loss instead of catching it.
**Failure:** a student answer that ends with `<lesson_context` (or `</student_message`, and so on) removes the trusted correct answer and explanation from the model prompt. The tutor then explains the wrong answer without the reference answer. It also turns the context JSON into invalid JSON.
**Fix (minimal):**
1. In `pipelines/chat.py`, add a private recursive helper that applies `strip_delimiters` to every `str` value inside nested `dict` and `list` values and leaves other values unchanged. For example, use a `match` on `str()` / `list()` / `dict()` / `_`.
2. In `run`, keep `context_json` exactly as it is for `_limit_errors`, so the D15 limit still measures the raw serialized context. Then build `safe_context` by calling `json.dumps` on the helper applied to `chat.context.model_dump(mode="json", by_alias=True, exclude_none=True)`, with `ensure_ascii=False` and `separators=(",", ":")`. Render with `"context": safe_context` instead of `strip_delimiters(context_json)`. `ensure_ascii=False` and the compact separators keep the current output shape (`"entryPoint":"quizQuestion"`, Arabic not escaped), so P26 and the assertion at `test_chat_pipeline.py:122` still hold.
3. In `test_chat_pipeline.py:146`, remove the assertion that expects the data loss. Replace it with assertions that the conftest `correctAnswer` and `explanation` key/value pairs are in `turn`. Keep the tag-count and elapsed-time assertions.
4. Add `test_chat_run_unclosed_tag_in_student_answer_keeps_later_context_fields`. Set `studentAnswer` to `x <lesson_context`. Assert that the turn still contains `correctAnswer` and `explanation`. Assert that the text between the template's `<lesson_context>` and `</lesson_context>` passes `json.loads`. Assert that `ANY_DELIMITER_TAG.findall(turn) == TEMPLATE_TAGS`. Mutation check: switch back to stripping the serialized string, and this test must fail.
5. Docs sync (`docs/ai-service.md` § Prompts, line 159). Add that removal runs on each string field of the context before the context is serialized, so a dangling tag cannot consume later fields.

## RC4: REJECT. OpenAPI error media type and the generated 422
**Verified:** the claim is literally true. `ai/openapi/v1.json:76-81` lists a `422` `HTTPValidationError` response, and the 400/401/502/503 entries are listed under `application/json`. The runtime never sends 422, and it sends errors as `application/problem+json`.
**Reason:** `router.py:14-19` follows the pattern that `.claude/skills/python-feature/SKILL.md:177` prescribes (a `{"model": Problem}` response entry per status, per route). The true runtime behaviour is written in the owning contract doc (`docs/ai-service.md:81` and `:97`, which says FastAPI's 422 is mapped to 400). The only consumer is the .NET `HttpAiServiceClient`. It is written by hand, it is not generated from this spec, and it maps every failure to `AI_SERVICE_UNAVAILABLE`, so no caller can behave wrongly because of this. FastAPI adds the 422 entry automatically. Removing it and changing the media type of the `Problem` schema needs an app-wide OpenAPI post-processor in `main.py`/`openapi_export.py`. That is a cross-cutting change to the skill's template, not a small fix for this story. It is worth adding as a backlog note for a later OpenAPI-hygiene task.

## RC5: FIX. Apply the per-message length limit to history turns
**Verified:** `_limit_errors` (`chat.py:69-80`) limits the number of history turns (20) and the length of `message` (4000). It does not limit `history[*].content`, and `run` (`chat.py:97`) sends every turn to the paid model. D15 set these limits to control cost and size, and history is the one input it leaves without a limit. The only caller is trusted, but the service already checks `message` and `context` itself for defense in depth, so the same check belongs here.
**Failure:** 20 history turns of 200 000 characters each pass validation and go to Claude. That costs tokens, adds latency, and can overflow the model context and produce a 502.
**Fix:**
1. In `_limit_errors`, after the `message` check, loop over `enumerate(chat.history)`. When `len(turn.content) > settings.chat_max_message_chars`, append `FieldError` with field `history[{index}].content`, code `TOO_LONG` and message `at most {limit} characters`. The field format matches `field_errors_from` (`history[2].role`). Assistant turns are limited by `chat_max_tokens` (1024), which is well below 4000 characters, so real replies are not rejected.
2. The existing test at `ai/tests/unit/test_chat_pipeline.py:175-181` sets `chat_max_message_chars=10`, and the default history turns (`conftest.py:83-84`) are longer than 10 characters. Pass `history=[]` there so the test still expects exactly one `message`/`TOO_LONG` error.
3. Add `test_chat_run_history_content_over_limit_raises_validation_failed`. Use a short message and one user turn longer than the limit. Assert that the errors tuple is exactly one `FieldError` for `history[0].content` with `TOO_LONG`, and that `fake_model.requests` is empty.
4. Docs sync: in `docs/ai-service.md`, change the description of `ELMANHG_AI_CHAT_MAX_MESSAGE_CHARS` at line 76 from "length of `message`" to "length of `message` and of each `history[].content`".

## RC6: FIX (changed): remove delimiter tags from history turns, but do not wrap them
**Verified:** `chat.py:97` passes `m.content` for history turns without changes. Wrapping them is not needed: a user-role turn is already the student's own channel, and plan D8 says history keeps its own roles. The real gap is that delimiter removal is skipped for history. The .NET API will replay the student's earlier raw `message` as a history user turn. So a message that wraps invented material in `<lesson_context>` tags has its tags removed on the turn it is sent, but reaches the model with the tags intact on every later turn. The system prompt tells the model to answer only from `<lesson_context>` (`avatar_system.v1.md` rule 2), so the tag defence works for only one turn.
**Fix:**
1. `chat.py:97`: pass `content=strip_delimiters(m.content)` instead of `content=m.content`. Apply this to both roles, because assistant text can echo tags. Do not wrap turns in `<student_message>`, and do not change the role order.
2. Add `test_chat_run_strips_delimiter_tags_from_history_turns`. Use a history user turn whose content wraps `fake` in `<lesson_context>` tags. Assert that no request message before the last one matches `ANY_DELIMITER_TAG`, and that the roles are still user, assistant, user.
3. Docs sync: add to `docs/ai-service.md` § Prompts (line 159) that history turns keep their roles and have the same tags removed. Plan D8 says "History turns are passed as-is". Note the change as a deviation in the rework report.

## RC7: FIX. Publish the compose port on loopback only
**Verified:** `docker-compose.yml:30` publishes `${AI_PORT:-8000}:8000` on every host interface. Compose sets `ELMANHG_AI_ENV: development` (line 24), which enables `/docs` and `/openapi.json` without authentication. The .NET API connects over `localhost`, so loopback-only publishing breaks nothing. `/v1/chat` still requires the token.
**Fix:** change line 30 to `- "127.0.0.1:${AI_PORT:-8000}:8000"`. Leave the postgres mapping alone, because it is outside this story.
