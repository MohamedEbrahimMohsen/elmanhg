VERDICT: APPROVED

# Review — Core.Settings: runtime settings and feature flags (E21.S9, story 312), round 1

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Core/Settings/RuntimeSettingsProbe.cs:18`: `GroupOrder = [Flags, Limits]` is also the alphabetical order. Because of that, core test `Constructor_ValidDefinitions_OrdersByGroupOrderKeepingDeclarationOrder` (`RuntimeSettingRegistryTests.cs:12`) would still pass if `RuntimeSettingRegistry.cs:21` sorted by `x.Group` as a string instead of by `IndexOf`. The app test `Definitions_DefaultOptions_TwentyTwoOrderedByGroup` does catch that regression, since the enum order Features, AskTeacher, ... is not alphabetical, so behaviour is protected. Core coverage alone would be stronger with a non-alphabetical probe order, for example `[Limits, Flags]` and the expected keys adjusted.
- `api/Elmanhg.Tests/Application/Shared/RuntimeSettings/RuntimeSettingRegistryTests.cs:23`: the new assertion `Distinct().Should().Equal(Enum.GetNames<RuntimeSettingGroup>())` is stricter than the old `BeInAscendingOrder()` in two ways: every group must be present, and the exact enum order is pinned. It no longer checks contiguity, though. A sequence like Features, AskTeacher, Features would pass the new check and fail the old one. This is not observable in the API, because `GetRuntimeSettingsHandler` groups with `GroupBy`, and `IndexOf` ordering always yields contiguous groups. If full parity is wanted, add an `IndexOf`-based `BeInAscendingOrder()` check.
- `api/Elmanhg.Tests/Core/Settings/RuntimeSettingValuesTests.cs:54` `With_ReplacesOnlyThatKey` and `RuntimeSettingDefinitionTests.cs:21` `ForBoolean_HasNoRange` use two-part names instead of Method_Scenario_Expected. They match the names in the plan, so this is a naming nit only.

## Verified
- **Moved code is verbatim.** I diffed each of the 14 moved files against `HEAD`. The only changes are the namespace, `string Group` on the definition record and its factories, the `groupOrder` parameter on the registry and the validator, `IndexOf` ordering, the new `unknownGroups` problem, `From(IEnumerable<IRuntimeSettingOverride>)`, and `CachedRuntimeSettings` reading through `IRuntimeSettingOverrideStore`. Everything else is unchanged: messages, value rules, the JSON options, the cache key `runtime-settings:values`, TTL from `CacheSeconds`, and `[Range(1, 3600)] CacheSeconds = 30`.
- **The 22 settings are unchanged.** For all 7 `Definitions/*.cs` files, I took the `HEAD` version and replaced `RuntimeSettingGroup.X` with `nameof(RuntimeSettingGroup.X)`. It matches the working tree exactly, apart from the added using. Keys, defaults, ranges, labels and constraints (including the app error codes) are untouched.
- **Override query is the same.** `RuntimeSettingOverrideStore.cs` uses `FindAsync(x => x.Value != null, ct, asNoTracking: true)`, which is identical to the old `CachedRuntimeSettings` query. It is covered against PostgreSQL by the new `GetOverridesAsync_OverriddenAndResetRows_ReturnsOnlyOverridden`.
- **Cache invalidation is unchanged.** Update and reset handlers changed their usings only and still call `memoryCache.Remove(RuntimeSettingsCache.Key)`.
- **Startup checks are equivalent.** `AddValidatedOptions<T>` + factory `IValidateOptions` is the same as the old `AddValidatedOptions<T, TValidator>` (bind, data annotations, ValidateOnStart, singleton validator). The new group-order check is in `FindProblems` (`RuntimeSettingRegistry.cs:53-55`). It runs at options validation and in the registry constructor, and is tested at the registry, validator and DI levels.
- **Group string to enum round trip is safe.** The app passes `Enum.GetNames<RuntimeSettingGroup>()` (`Elmanhg.Application/DependencyInjection.cs:68`). Definitions use `nameof`. `FindProblems` rejects any group outside the names (ordinal), so `Enum.Parse<RuntimeSettingGroup>` in `GetRuntimeSettingsHandler.cs` and `RuntimeSettingResultGenerator.cs` cannot throw at request time. `Enum.GetNames` order equals the old enum-value `OrderBy`, and `OrderBy` is stable, so declaration order inside a group is kept.
- **API and OpenAPI are unchanged.** `RuntimeSettingResult` and `RuntimeSettingGroupResult` keep the enum type. `RuntimeSettingType` keeps its short name and member order. After the Release build, `git status --porcelain api/openapi` is empty. No Postman change is needed because no endpoint changed.
- **No migration.** Nothing changed under `Migrations/`, `AppDbContext*` or `CoreDbContext`. The model-vs-snapshot test in `AppDbContextTests` passes. The entity only gains `IRuntimeSettingOverride`.
- **Core stays app-agnostic.** A case-insensitive grep for elmanhg, egypt and cairo over Core.Settings finds nothing. Core.Settings has no comments, and no issue number appears in comments. No existing `Core.*` project was modified, and Core.Notifications is untouched.
- **Packages and solution.** The only package reference is `Microsoft.Extensions.Caching.Memory` 10.0.5, the same as Core.Cache. Domain already referenced Core.Utilities. In the slnx, a single line was added between Core.Queues and Core.Spreadsheets.
- **Core.Cache decision is justified.** Core.Cache exposes only `CachingBehaviour`, `ICacheableQuery`, `CachingOptions` and `AddCoreCache`, with no invalidation API, so Plan Decision 7 holds. The multi-instance staleness window is documented in `docs/configuration.md` §3 (Cache) and §8.
- **Docs-sync.** `docs/configuration.md` §3 and §4, `docs/constitution.md` line 3 and the SKILL.md Delta 5 and §3 tree match the code. `docs/PRD.md` and `docs/ask-teacher.md` describe behaviour that did not change, so there is no divergence.
- **Deviations.** Both deviations in 02-implementation.md are accurate and harmless.
- **Build and tests, re-run by me.** The Release build succeeded. The targeted run (Core.Settings, RuntimeSetting*, Configuration*, OpenApi*, AppDbContextTests and the definition tests) had 162 tests: 162 passed, 0 failed. The full suite had 5466 tests: 5466 passed, 0 failed, 0 skipped.
- Every test in the plan (#1 to #36) exists with its planned name.

## Test quality
- Core RuntimeSettingRegistryTests: these constrain the code. They pin ordering and stable declaration order, the duplicate and unknown-group messages, and the constraint error code. The one weakness is the alphabetical-order blind spot in the first non-blocking note.
- Core RuntimeSettingValuesTests: these constrain the code. They cover the out-of-range, non-JSON, null and unknown-key fallbacks, With immutability, and Raw throwing.
- RuntimeSettingValueRulesTests: moved verbatim and still constrains every type branch.
- CachedRuntimeSettingsTests: these constrain the code. `Received(1)` verifies the cache hit, and the remove test verifies invalidation. The substitute is only a data source.
- RuntimeSettingsOptionsValidatorTests and CoreSettingsDependencyInjectionTests: these constrain the code. They cover the real DI wiring, range validation and group-order validation.
- RuntimeSettingJsonTests and RuntimeSettingDefinitionTests: small, but non-vacuous.
- The new persistence test runs against real PostgreSQL. It would fail if the null-value filter were dropped.
- No vacuous tests found.
