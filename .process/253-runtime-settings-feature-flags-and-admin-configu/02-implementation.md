# Implementation — Runtime settings, feature flags and admin Configuration page (#253, E18.S1)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/RuntimeSettings/RuntimeSettingOverride.cs | 33 | entity (`IAuditedEntity`, xmin `Version`, `Create`/`Override`/`Reset`) |
| api/Elmanhg.Domain/RuntimeSettings/IRuntimeSettingOverrideRepository.cs | 5 | repo port |
| api/Elmanhg.Application/Shared/RuntimeSettings/{RuntimeSettingType, RuntimeSettingGroup, RuntimeSettingKey, RuntimeSettingJson, RuntimeSettingDefinition, RuntimeSettingConstraint, IRuntimeSettingDefinitions, RuntimeSettingValueRules, RuntimeSettingValues, RuntimeSettingRegistry, IRuntimeSettings, RuntimeSettingsCache, CachedRuntimeSettings}.cs | 3–57 each | typed registry, value rules, effective values, cached accessor |
| api/Elmanhg.Application/Shared/RuntimeSettings/Definitions/{FeatureFlag, AskTeacher, PlanLimit, Grading, Upload}RuntimeSettings.cs | 15–25 each | the 14 seeded settings in 5 groups (+ reminder-order constraint) |
| api/Elmanhg.Application/Shared/Options/RuntimeSettingsOptions.cs, RuntimeSettingsOptionsValidator.cs | 11, 13 | `RuntimeSettings:CacheSeconds` + startup check of defaults |
| api/Elmanhg.Application/Subscriptions/Shared/PlanLimits.cs | 9 | plan quotas read from runtime values |
| api/Elmanhg.Application/Configuration/Shared/{RuntimeSettingResult, RuntimeSettingGroupResult, RuntimeSettingResultGenerator, IntegrationMode, AiServiceStatus, InfrastructureConfigurationResult, IInfrastructureConfigurationReader}.cs | 5–11 each | results and port |
| api/Elmanhg.Application/Configuration/GetRuntimeSettings/*, UpdateRuntimeSetting/*, ResetRuntimeSetting/*, GetInfrastructureConfiguration/* | 6–42 each | queries, commands, validators, handlers |
| api/Elmanhg.Infrastructure/RuntimeSettings/RuntimeSettingOverrideRepository.cs | 7 | repo |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.RuntimeSettings.cs | 25 | mapping (unique key index, jsonb, xmin) |
| api/Elmanhg.Infrastructure/Migrations/20261001141606_AddRuntimeSettingOverrides(.Designer).cs + snapshot | 48 | creates one table only |
| api/Elmanhg.Infrastructure/Hosting/{ConfigurationSecrets, InfrastructureIntegrations, InfrastructureConfigurationReader}.cs | 13, 32, 38 | secret list, provider modes, reader |
| api/Elmanhg.Infrastructure/AiService/HttpAiConfigurationClient.cs | 41 | typed client for AI `GET /v1/configuration` |
| api/Elmanhg.Api/Controllers/Configuration/ConfigurationController.cs, Requests.cs | 53, 5 | 4 endpoints, `Configuration.Manage` |
| ai/src/elmanhg_ai/api/configuration/{__init__, schemas, router}.py | 0, 18, 37 | AI `GET /v1/configuration` |
| ai/tests/integration/test_configuration_endpoint.py | 88 | 4 tests |
| web/src/features/configuration/** (index, locales, 4 i18n files, api/runtimeSettingValue.ts, schemas/runtimeSettingSchemas.ts, 3 hooks, 13 components, pages/ConfigurationPage.tsx) | 1–87 each | the page |
| web/src/routes/admin/configuration.tsx | 6 | route |
| web/src/test/configurationFixtures.ts | 118 | fixtures |
| web tests: ConfigurationPage.test.tsx, runtimeSettingValue.test.ts, runtimeSettingSchemas.test.ts | 238, 43, 53 | |
| api tests: FakeRuntimeSettings fixture, RuntimeSettingOverrideTests, RuntimeSettingValueRulesTests, RuntimeSettingValuesTests, RuntimeSettingRegistryTests, CachedRuntimeSettingsTests, AskTeacherRuntimeSettingsTests, RuntimeSettingsOptionsValidatorTests, Get/Update/Reset handler + validator tests, EntitlementResultGeneratorTests, PlanCatalogueResultGeneratorTests, InfrastructureConfigurationReaderTests, HttpAiConfigurationClientTests, RuntimeSettingsCollection, ConfigurationTestData, ConfigurationEndpointTests, RuntimeSettingOverridePersistenceTests | 7–203 | Test plan rows 1–94 |
| docs/configuration.md | 100 | new doc (§1–§8 per plan) |

## Files modified
| Path | Change |
|---|---|
| DefaultCodes.cs, PermissionMatrixPolicies.cs | `Configuration.Manage`, Admin only |
| ErrorCodes.cs, Messages.ar.resx, Messages.en.resx | 5 codes |
| Application/DependencyInjection.cs, Infrastructure/DependencyInjection.cs, AiServiceServiceCollectionExtensions.cs | registrations, typed client |
| AppDbContext.cs | configure call, global filter, xmin + unique-key conflict catches |
| PlaceholderSecretGuard.cs | delegates to `ConfigurationSecrets` (behaviour unchanged) |
| appsettings.example.json | `RuntimeSettings.CacheSeconds` 30 |
| EntitlementResultGenerator, PlanCatalogueResultGenerator, StudentEntitlementLoader | signatures per plan |
| 13 loader callers, 3 exam handlers, 2 dashboard handlers, Create/FollowUp/GetDueSla/ProcessSla handlers, GetPlanCatalogue, GradeEssay, GradeMathSteps, GetVoiceReplySettings | read through `IRuntimeSettings` |
| CreateTeacherThreadValidator, RecordVoiceDraftValidator | `MustAsync` on runtime upload limits |
| api/openapi/v1.json, web/src/shared/api/generated/**, web/src/routeTree.gen.ts, ai/openapi/v1.json | regenerated |
| postman/elmanhg.postman_collection.json | `Configuration` folder: list → update → reset → infrastructure |
| ai/src/elmanhg_ai/main.py | router included |
| web permissions.ts, navConfig.ts, shell i18n ar/en | capability + nav item |
| Existing tests (plan rows 66–78, 117, 118) | construction only; validator tests → `ValidateAsync`; PermissionMatrix row; AppDbContextTests migration list; permissions + MorePage |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | see Deviations |
| docs: PRD (§3.3, §6.1, §7.4, §9.3, new §10.6, §11.1, §12.1, §16, §17), backlog.json (E18, 6 stories), implementation-report §4, audit-log, ask-teacher, subscriptions, essay-grading, math-step-grading, grade-review, exams, sessions, security, deployment, ai-service, claude-design-prompt §4, prototype.md | per Docs table |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Modify only the listed files | Repo convention (PROGRESS.md) puts new config keys in the test `ApiFactory`; test 90 must prove the JWT key text is absent but `ApiFactory.TestJwtKey` was private | `ApiFactory`: added `UseSetting("RuntimeSettings:CacheSeconds", "30")` and made `TestJwtKey` public |
| AI secrets `ELMANHG_AI_ANTHROPIC_API_KEY` + `ELMANHG_AI_OPENAI_API_KEY`; test 95 `chatModel` "claude-sonnet-5" | #257 (merged, fast-forwarded to f6f43b3 before the ai/ part) removed Anthropic: keys are `llm_api_key` and `openai_api_key`; default chat model is `DEFAULT_LLM_MODEL` | Secrets `ELMANHG_AI_LLM_API_KEY`, `ELMANHG_AI_OPENAI_API_KEY`; test 95 asserts `chatModel == DEFAULT_LLM_MODEL` and `essayGradingModel` "claude-sonnet-5" (conftest value). `llm_base_url` is not returned. .NET/web fixtures and web test 114 use `openai_compatible` / `gpt-5.6-luna` |
| Test 97 sets embedding/transcription providers "as Settings requires" | Settings does not require a provider to set `openai_api_key` | Only the key is set |
| deployment.md: append "default of a runtime setting" | The rows also stated ranges (1–20, 1–25, 1–168) that now stop the boot | Ranges corrected to 1–9 / 1–9 / 1–167 to avoid divergence |
| Plan rows 10, 11, 13, 24 list two method names | — | Written as separate `[Fact]`s with those names |

## Build & test
- `dotnet build api -c Release` (no `appsettings.json` present in the worktree, so CI parity): Build succeeded, no new warnings outside core-libraries.
- `dotnet test api -c Release`: `total: 4820, failed: 0, succeeded: 4820` (Docker/Testcontainers ran).
- `dotnet format --verify-no-changes`: only CRLF/LF whitespace noise on untouched files of this Windows checkout; no finding in any changed or new file.
- `dotnet ef migrations add AddRuntimeSettingOverrides`: one CreateTable + index, no Drop/Rename.
- web: `npm run gen:api` (no drift on re-run), `npx tsc -b` ok, `eslint . --max-warnings=0` ok, `prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` "All matched files use Prettier code style!", `vitest run` 275 files / 1553 tests passed, `npm run build` ok, `npm run perf:budget` all ok.
- ai (`python -m uv`, uv not on PATH): `ruff format --check .` ok, `ruff check` ok, `mypy src` "no issues found in 78 source files" (mypy on `tests` has 4 pre-existing errors, untouched files), `pytest` 491 passed, 4 skipped; `openapi_export` regenerated.
- Mutation checks: removing the constraint check / cache clear in Update, the value-rule fallback in `RuntimeSettingValues`, the zod range rule, and the placeholder check in the AI router each fail tests.

## Notes for review
- Survived mutation: in `ResetRuntimeSettingHandler`, `row?.Value is null` → `row is null` is not caught (no planned test resets an already-reset row).
- Forms remount on a new value (`key={JSON.stringify(setting.value)}`) and use `defaultValues`; `values` caused a reset loop.
- `formatSettingValue` orders a list by `allowedValues`. Helpers `settingBound`, `formatSettingNumber`, `settingErrorMessage` were added inside planned files.
- Sonner toasts persist across tests, so the feature-flag test asserts the request body with `vi.waitFor` instead of the toast text.
- The branch was fast-forwarded to origin/main (#257, #258); no merge commit, nothing committed.
