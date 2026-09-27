# CI fix — #58 audit log, PR #140 (`api-ci`: 5/256 failing)

## Root cause (verified)
- `Core.Auditing.DependencyInjection.AddCoreAuditing` (api/core-libraries/Core.Auditing/DependencyInjection.cs:13) reads `CoreAuditing:Enabled` **eagerly**, while `Program` registers services, to decide whether to add `AuditBehaviour<,>`.
- `ApiFactory` supplied the flag through `ConfigureAppConfiguration(AddInMemoryCollection(...))`. Under minimal hosting (`WebApplication.CreateBuilder`), `WebApplicationFactory` applies those sources only at `builder.Build()`. That is after `AddCoreAuditing` has already run. Lazily bound options (`IOptions<T>`) still see the values. The eager read does not.
- Locally, the gitignored `api/Elmanhg.Api/appsettings.json` has `"CoreAuditing": { "Enabled": true }`, so the eager read happened to pass. CI has no such file, so the flag was false: no `AuditBehaviour` was registered and no audit rows were written. That caused all 5 failures (`PipelineCompositionTests.Resolve_PipelineBehaviours_AuditBehaviourIsOutermost` + 4 `AuditLogsEndpointTests`).
- This is not the #56 OpenAPI path. `appsettings.example.json` is loaded only when the entry assembly is `GetDocument.Insider`, never under tests.

## Change
`api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs`:
- :39-40 — added `builder.UseSetting("CoreAuditing:Enabled", "true");` right after `UseEnvironment`, with a one-line comment on why. `UseSetting` values reach the builder configuration before `Program`'s service registration, just like `UseEnvironment`.
- Removed the now-redundant `["CoreAuditing:Enabled"] = "true"` entry from the in-memory collection.

No production code changed.

## Test runs (`dotnet test api/`, Debug, Docker/Testcontainers)
| Run | Result |
|---|---|
| Before fix, local `appsettings.json` moved aside | `Failed!` total 256, failed 5, succeeded 251 — the same 5 tests as the CI log |
| After fix, local `appsettings.json` moved aside | `Passed!` total 256, failed 0, succeeded 256 |
| After fix, local `appsettings.json` restored | `Passed!` total 256, failed 0, succeeded 256 |

The local `appsettings.json` was restored after each run and is present.

## Notes for review
- Other eager config reads in `Program` (e.g. `GetConnectionString` inside the `UseNpgsql` lambda) are lambdas evaluated later, so they are not affected.
- A production-side alternative would be for `AuditBehaviour` to check `IOptions<AuditOptions>` at runtime. That was not done because it is a larger change and outside the smallest fix.
