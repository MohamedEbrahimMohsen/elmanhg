VERDICT: CHANGES_REQUESTED

# CI-fix verify — #58 audit log, PR #140

## Blocking
### 1. Production runs with auditing OFF unless the host sets `CoreAuditing__Enabled=true`
**Where:** `api/core-libraries/Core.Auditing/AuditOptions.cs:6` (`public bool Enabled { get; set; }`, defaults to `false`); `api/core-libraries/Core.Auditing/DependencyInjection.cs:13-18` (`?? new AuditOptions()` and then `if (options.Enabled)`)
**Rule:** PRD §17 rule 13 (`docs/PRD.md:497`: "Every content change and validation decision is audit-logged."); `docs/audit-log.md:17` ("on in every environment"); `docs/constitution.md` §2 (CI and fresh environments have NO configuration, and a new section must not silently default to 0/empty)
**Problem:** `appsettings.json` is gitignored (`.gitignore`: `/api/Elmanhg.Api/appsettings.json`, plus `.Production.json`). The only committed file, `appsettings.example.json`, is loaded only for `GetDocument.Insider` (`Program.cs:39-42`). No deployment config or workflow sets `CoreAuditing__Enabled`. A deployed host with no `appsettings.json` therefore binds `Enabled=false`, `AuditBehaviour<,>` is never registered, and no audit rows are written. Nothing reports this. It is the same mechanism that broke CI. The test fix hides it in tests but does not fix it in the product.
**Failure:** A deployment that supplies only the secret env vars (connection string, JWT, OTP) does not set `CoreAuditing__Enabled`. An Admin then assigns or unassigns content. No `AuditLog` row is written, which violates PRD rule 13, and `docs/audit-log.md:17` ("on in every environment") no longer matches the code.
**Fix:** Make on the code default: `public bool Enabled { get; set; } = true;` in `AuditOptions`. That way, missing configuration still audits, and turning it off becomes an explicit opt-out. A committed `appsettings.json` is not allowed (constitution §2), so the code default is the right place. Once it is on by default, the `UseSetting` line in `ApiFactory` is optional. Keep it as an explicit assertion, or drop it.

## Non-blocking
- `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs:39` — the comment states a hidden invariant (eager read vs deferred config sources). That meets the skill's "No Comments" exception, so it is fine.
- `api/Elmanhg.Api/bin/Release/net10.0/appsettings.json` and `api/Elmanhg.Tests/bin/Release/...` hold a stale local copy with `"CoreAuditing": { "Enabled": false }`. This is build output and not a finding. The Release run still passed, so the content root is the project directory and `UseSetting` is effective.

## Verified
- Root cause confirmed: `DependencyInjection.cs:13` reads `CoreAuditing` eagerly during `Program` service registration (`Program.cs:59`). The CI log (`09-ci-failure.log:10-11`) shows `ValidationBehaviour` outermost instead of `AuditBehaviour`, plus the 4 `AuditLogsEndpointTests` failures. That matches the missing registration. CI (`.github/workflows/api-ci.yml`) supplies no `appsettings.json` and no auditing env var.
- Fix diff: `ApiFactory.cs` only. It adds `builder.UseSetting("CoreAuditing:Enabled", "true")` and removes the redundant in-memory entry. No production code changed, as claimed.
- Re-ran as CI does. I moved `api/Elmanhg.Api/appsettings.json` aside and ran `dotnet test api/ -c Release`: Passed, total 256, failed 0, succeeded 256. `appsettings.json` has been restored and is present.

## Test quality
- The 5 previously failing tests do constrain the behaviour: they failed when `AuditBehaviour` was absent. Because `ApiFactory` forces the flag on, however, no test covers the product default. Once Finding 1 is fixed, add a unit test that asserts `new AuditOptions().Enabled` is `true`, or that `AddCoreAuditing` with empty configuration registers `AuditBehaviour<,>`.
