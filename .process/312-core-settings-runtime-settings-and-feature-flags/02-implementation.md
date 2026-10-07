# Implementation — Core.Settings: runtime settings and feature flags (E21.S9)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.Settings/Core.Settings.csproj` | 19 | New core project (net10.0; `Microsoft.Extensions.Caching.Memory` 10.0.5; refs Core.DDD, Core.Errors, Core.Utilities) |
| `api/core-libraries/Core.Settings/RuntimeSettingKey.cs` | 3 | `git mv` from Application, namespace only |
| `api/core-libraries/Core.Settings/RuntimeSettingType.cs` | 10 | `git mv`, namespace only |
| `api/core-libraries/Core.Settings/RuntimeSettingDefinition.cs` | 32 | `git mv`; `Group` and factory `group` are now `string` |
| `api/core-libraries/Core.Settings/RuntimeSettingConstraint.cs` | 3 | `git mv`, namespace only |
| `api/core-libraries/Core.Settings/IRuntimeSettingDefinitions.cs` | 8 | `git mv`, namespace only |
| `api/core-libraries/Core.Settings/RuntimeSettingJson.cs` | 30 | `git mv`, namespace only |
| `api/core-libraries/Core.Settings/RuntimeSettingValueRules.cs` | 28 | `git mv`, namespace only |
| `api/core-libraries/Core.Settings/IRuntimeSettingOverride.cs` | 8 | Override row contract (`Key`, `Value`) |
| `api/core-libraries/Core.Settings/IRuntimeSettingOverrideStore.cs` | 6 | `GetOverridesAsync` contract the app implements |
| `api/core-libraries/Core.Settings/RuntimeSettingValues.cs` | 43 | `git mv`; `From` takes `IEnumerable<IRuntimeSettingOverride>` |
| `api/core-libraries/Core.Settings/RuntimeSettingRegistry.cs` | 65 | `git mv`; `groupOrder` ctor/`FindProblems` param, `IndexOf` ordering, `unknownGroups` problem |
| `api/core-libraries/Core.Settings/IRuntimeSettings.cs` | 8 | `git mv`, namespace only |
| `api/core-libraries/Core.Settings/RuntimeSettingsCache.cs` | 6 | `git mv`, namespace only (`runtime-settings:values`) |
| `api/core-libraries/Core.Settings/CachedRuntimeSettings.cs` | 26 | `git mv`; reads through `IRuntimeSettingOverrideStore` |
| `api/core-libraries/Core.Settings/RuntimeSettingsOptions.cs` | 11 | `git mv` from `Shared/Options`, namespace only |
| `api/core-libraries/Core.Settings/RuntimeSettingsOptionsValidator.cs` | 12 | `git mv`; gains `groupOrder` |
| `api/core-libraries/Core.Settings/DependencyInjection.cs` | 19 | `AddCoreRuntimeSettings<TOverrideStore>(groupOrder)` |
| `api/Elmanhg.Application/Shared/RuntimeSettings/RuntimeSettingOverrideStore.cs` | 12 | App store over `IRuntimeSettingOverrideRepository` (same query as before) |
| `api/Elmanhg.Tests/Core/Settings/RuntimeSettingsProbe.cs` | 32 | `ProbeOverride`, `ProbeOverrideStore`, `ProbeRuntimeSettings` |
| `api/Elmanhg.Tests/Core/Settings/RuntimeSettingRegistryTests.cs` | 79 | Tests #1–#9 |
| `api/Elmanhg.Tests/Core/Settings/RuntimeSettingValuesTests.cs` | 72 | Tests #10–#16 |
| `api/Elmanhg.Tests/Core/Settings/RuntimeSettingValueRulesTests.cs` | 65 | Test #17 (`git mv` from app tests; groups `"Probe"`) |
| `api/Elmanhg.Tests/Core/Settings/RuntimeSettingJsonTests.cs` | 29 | Tests #18–#20 |
| `api/Elmanhg.Tests/Core/Settings/RuntimeSettingDefinitionTests.cs` | 40 | Tests #21–#23 |
| `api/Elmanhg.Tests/Core/Settings/CachedRuntimeSettingsTests.cs` | 55 | Tests #24–#26 (`git mv` from app tests, rewritten on the probe store) |
| `api/Elmanhg.Tests/Core/Settings/RuntimeSettingsOptionsValidatorTests.cs` | 35 | Tests #27–#29 |
| `api/Elmanhg.Tests/Core/Settings/CoreSettingsDependencyInjectionTests.cs` | 58 | Tests #30–#32 |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.slnx` | `Core.Settings` added between `Core.Queues` and `Core.Spreadsheets` |
| `api/Elmanhg.Domain/Elmanhg.Domain.csproj` | ProjectReference to Core.Settings after Core.DDD |
| `api/Elmanhg.Application/Elmanhg.Application.csproj` | ProjectReference to Core.Settings before Core.Spreadsheets (Caching.Memory package kept) |
| `api/Elmanhg.Domain/RuntimeSettings/RuntimeSettingOverride.cs` | `using Core.Settings;`, implements `IRuntimeSettingOverride` |
| `api/Elmanhg.Application/DependencyInjection.cs` | `using Core.Settings;`; options/registry/reader registrations replaced by `AddCoreRuntimeSettings<RuntimeSettingOverrideStore>(Enum.GetNames<RuntimeSettingGroup>())` |
| 7 `Shared/RuntimeSettings/Definitions/*.cs` | `using Core.Settings;`; `RuntimeSettingGroup.X` → `nameof(RuntimeSettingGroup.X)` |
| `Configuration/GetRuntimeSettings/GetRuntimeSettingsHandler.cs` | `using Core.Settings;`; `Enum.Parse<RuntimeSettingGroup>(group.Key)` |
| `Configuration/Shared/RuntimeSettingResultGenerator.cs` | `using Core.Settings;`; `Enum.Parse<RuntimeSettingGroup>(definition.Group)` |
| `Configuration/Shared/RuntimeSettingResult.cs` | `using Core.Settings;` |
| 35 Application consumers listed in the plan | Using swap only |
| `Elmanhg.Tests/Fixtures/RuntimeSettings/FakeRuntimeSettings.cs` | `using Core.Settings;`; registry gets `Enum.GetNames<RuntimeSettingGroup>()` |
| `Elmanhg.Tests/Application/Shared/RuntimeSettings/RuntimeSettingRegistryTests.cs` | #34 assertion → `Distinct().Should().Equal(Enum.GetNames<RuntimeSettingGroup>())`; #35/#36 new argument |
| `Elmanhg.Tests/Application/Shared/Options/RuntimeSettingsOptionsValidatorTests.cs` | `using Core.Settings;` (old using already present); validator gets group order |
| `FeatureFlag/OutOfAppReminder/SlaCalendarRuntimeSettingsTests.cs` | `using Core.Settings;`; `nameof(RuntimeSettingGroup.X)` |
| 7 test files listed in the plan | Using swap only |
| `Elmanhg.Tests/Integration/Persistence/RuntimeSettingOverridePersistenceTests.cs` | `using Core.Settings;`; new test #33 |
| `docs/constitution.md` | Core.Settings added to the promoted list (line 3) |
| `.claude/skills/dotnet-feature/SKILL.md` | Delta 5 sentence + usage sentence; §3 tree line |
| `docs/configuration.md` | §3 Framework bullet, Cache and Startup-validation bullets; §4 steps 2, 3, 6 |

No Postman change: no endpoint was added, changed or removed.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `CoreSettingsDependencyInjectionTests` helper `Provider(params (string Key, string Value)[] settings)` that registers "a supplied `ProbeRuntimeSettings`" | The helper as typed has no way to receive the definitions, and test #32 needs `ProbeRuntimeSettings(countGroup: "Other")` while #30/#31 need the default | Signature is `Provider(ProbeRuntimeSettings definitions, params (string Key, string Value)[] settings)`; body otherwise as specified |
| `RuntimeSettingsOptionsValidatorTests` (app): add `using Elmanhg.Application.Shared.RuntimeSettings;` | The using was already in the file | Added only `using Core.Settings;` |

## Build & test
- `dotnet build api/ -c Release` → `Build succeeded. 0 Error(s)`. The only warnings are in pre-existing vendored projects (Core.OTP, Core.Validation, Core.Notifications CS8618/CS8602), none in Core.Settings or any Elmanhg project.
- OpenAPI gate: `git status --porcelain api/openapi` after the Release build → empty.
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build` (with the CI design-time connection string) → `No changes have been made to the model since the last migration.` No file under `Migrations/` changed.
- `dotnet test api/ -c Release --no-build` (Docker/Testcontainers) → `Passed! total: 5466, failed: 0, succeeded: 5466, skipped: 0`.
- `--filter-namespace Elmanhg.Tests.Core.Settings` → 46 passed (9 + 7 + 15 + 3 + 3 + 3 + 3 + 3).
- `--filter-method *GetOverridesAsync_OverriddenAndResetRows_ReturnsOnlyOverridden` → 1 passed.
- `grep -rniE "elmanhg|egypt|cairo"` over Core.Settings sources → nothing; no comments in new code.

## Deferred
None.

## Notes for review
- Moved files were moved with `git mv` (git shows 14 + 2 renames). New files are staged intent-to-add only (`git add -N`) so they show in `git status`; nothing is committed.
- `RuntimeSettingOverrideStore` returns `List<RuntimeSettingOverride>` as `IReadOnlyList<IRuntimeSettingOverride>` through interface covariance; no mapping code.
- Handlers that call `RuntimeSettingValues.From(registry, rows)` with `List<RuntimeSettingOverride>` compile unchanged thanks to `IEnumerable<out T>` covariance.
- The CI test run uses `--no-build` after the Release build, same as here.
