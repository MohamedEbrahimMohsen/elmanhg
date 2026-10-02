# Implementation r2 — Runtime settings, feature flags and admin Configuration page (#253)

## Blocking findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Added `Handle_AlreadyReset_ReturnsDefaultWithoutSaving`. It seeds a row created with "12", then `Reset` by a different admin, and calls Reset again. It asserts: `IsOverridden` false with value 10, `UpdationDate` and `UpdatedBy` unchanged, `SaveChangesAsync` `DidNotReceive()`, and the cache entry still present. Mutation check: with `row?.Value is null` changed to `row is null`, this test failed (UpdationDate/UpdatedBy changed) and the other 5 passed. The handler is restored to `row?.Value is null` (verified at line 26). | `api/Elmanhg.Tests/Application/Features/Configuration/ResetRuntimeSetting/ResetRuntimeSettingHandlerTests.cs:63-77` |

## Non-blocking items taken (requested by the orchestrator)
| Item | Change | File |
|---|---|---|
| Postman Content-Type | Added `Content-Type: application/json` to "Update runtime setting" | `postman/elmanhg.postman_collection.json:5627` |
| AI status timeout | New `AiService:ConfigurationTimeoutSeconds` (`[Range(1, 30)]`, default 5). `HttpAiConfigurationClient` now uses it as the attempt and total timeout, with `HttpClient.Timeout` infinite, the same pattern as the other dedicated clients. Wired into appsettings.example.json, ApiFactory, `.env.example`, `deploy/api.env.example`, docs/ai-service.md (options table), docs/deployment.md (AiService table) and docs/configuration.md §6 | `api/Elmanhg.Infrastructure/AiService/AiServiceOptions.cs:34-35`, `AiServiceServiceCollectionExtensions.cs:85-98` |
| AI llm_api_key test | Generalised the helper to `app_with_secret(settings, field, key)` and added the parametrised `test_configuration_llm_key_reports_only_set_status`: a real-looking key gives isSet true, `change-me-llm` gives false, and the key never appears in the body | `ai/tests/integration/test_configuration_endpoint.py` |
| AskTeacherOptions ranges | `ImageMaxSizeInMb` and `VoiceMaxSizeInMb` are now `[Range(1, 9)]`. `First/SecondReminderAfterHours` are now `[Range(1, 167)]`. All committed configs use 5/5/12/20, so startup still works. docs/deployment.md and docs/configuration.md already state 1 to 9 and 1 to 167 | `api/Elmanhg.Application/Shared/Options/AskTeacherOptions.cs:12,21,53,56` |

Skipped: the ApiFactory `RuntimeSettings:CacheSeconds` redundancy (harmless, and not requested).

## Deviations
None.

## Build & test
- No `appsettings.json` under `api/` (checked with find).
- `dotnet test api/ -c Release`: Passed, total 4821, failed 0, succeeded 4821.
- Mutation run (`--filter-class "*ResetRuntimeSettingHandlerTests"`, mutant applied): total 6, failed 1 (`Handle_AlreadyReset_ReturnsDefaultWithoutSaving`). The mutant was then reverted.
- `python -m uv run pytest -q` (ai): 493 passed, 4 skipped. `ruff check`: All checks passed. `ruff format` was applied to the edited test file.
- `mypy .` reports 4 errors, all in untouched test files (test_delimiters, test_prompt_loader, test_openai_embedding, test_metered_clients). They are not from this change.
- Postman JSON parses.
- web: not touched, not re-run.

## Notes for review
- The new test file is now about 115 lines, over the ~100-line guide. It is a test class, and splitting it would scatter one handler's cases.
- The status probe keeps the standard retry. Because attempt timeout = total timeout (5 s), a hang is bounded at 5 s.
