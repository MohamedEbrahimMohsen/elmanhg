VERDICT: APPROVED

# Review r2 — Runtime settings, feature flags and admin Configuration page (#253, E18.S1)

## Blocking
None.

## Round-1 finding 1 — resolved
- `api/Elmanhg.Tests/Application/Features/Configuration/ResetRuntimeSetting/ResetRuntimeSettingHandlerTests.cs:63-77`: `Handle_AlreadyReset_ReturnsDefaultWithoutSaving` seeds a row created with "12", resets it as a different admin (`earlierAdminId`), and then calls Reset again as `AdminId`. It asserts IsOverridden false with value 10, unchanged `UpdationDate`/`UpdatedBy`, `SaveChangesAsync` `DidNotReceive()`, and that the cache entry is still present.
- The mutation claim holds. `RuntimeSettingOverride.Reset` (`api/Elmanhg.Domain/RuntimeSettings/RuntimeSettingOverride.cs:27-32`) always sets `UpdatedBy = updatedBy`. Under the mutant `row is null`, the handler would call `row.Reset(AdminId)`, which changes `UpdatedBy` away from `earlierAdminId`. It would also call `SaveChangesAsync` and clear the cache, so four separate assertions fail and the result does not depend on timing. The handler is restored to `row?.Value is null` (`ResetRuntimeSettingHandler.cs:26`).

## r2 changes checked for regressions
- `AiService:ConfigurationTimeoutSeconds`: `[Range(1, 30)]`, default 5 (`AiServiceOptions.cs:34-35`). It is bound through the existing `ValidateDataAnnotations().ValidateOnStart()`. The client uses the infinite `HttpClient.Timeout` plus the attempt=total=sampling/2 pattern (`AiServiceServiceCollectionExtensions.cs:85-98`), which is the same as the other dedicated clients, so the standard-handler constraints are met (sampling 10 s >= 2 x attempt). `AiServiceOptionsValidator` cross-checks only Attempt <= Total, so the new key is unaffected. The key is wired in appsettings.example.json:36, ApiFactory.cs:146, .env.example:90, deploy/api.env.example:125, docs/ai-service.md:415, docs/deployment.md:177 and docs/configuration.md §6. All of them say 5 and 1 to 30, so there is no divergence.
- `AskTeacherOptions` ranges are now 1-9 / 1-9 / 1-167 / 1-167 (`AskTeacherOptions.cs:12,21,53,56`). Committed configs: appsettings.example.json (5, 5, 12, 20), deploy/api.env.example (5, 5, 12, 20), .env.example (5). All are in range. Test literals above the new caps (`RuntimeSettingRegistryTests.cs:56` = 12, `RuntimeSettingsOptionsValidatorTests.cs:22` = 20) build options objects directly to exercise the runtime validator's rejection. They do not go through DataAnnotations and are still valid. The docs (deployment.md:230,233,240; configuration.md:28-29,37-38; security.md:130-131) already state these caps.
- AI `llm_api_key` test (`ai/tests/integration/test_configuration_endpoint.py:91-102`): it is parametrised over a real-looking key (True) and `change-me-llm` (False). `_is_set` (`router.py:35-37`) treats the `change-me` prefix as unset, so both cases test real logic, and the test also checks that the key never appears in the body.
- Postman `postman/elmanhg.postman_collection.json:5627`: the `Content-Type: application/json` header is present, and the URL, method and body are unchanged.

## Non-blocking
- `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs:72`: the redundant `RuntimeSettings:CacheSeconds` remains. It is harmless.

## Verified (re-run here)
- There is no `appsettings.json` under `api/`. `dotnet test api/ -c Release`: 4821/4821 passed, which matches the claim.
- `python -m uv run pytest -q` in ai/: 493 passed, 4 skipped, which matches the claim.
- r2 "Deviations: None." is confirmed: every r2 change is either the requested fix or a non-blocking item from round 1.

## Test quality
- ResetRuntimeSettingHandlerTests now constrains every handler branch (no user, unknown key, no row, already-reset row, overridden, constraint violation).
- The AI configuration tests now cover both secrets, each with a set and a placeholder case.
