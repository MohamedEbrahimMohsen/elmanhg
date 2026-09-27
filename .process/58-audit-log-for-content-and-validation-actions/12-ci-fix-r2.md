# CI fix r2 — #58 audit log, PR #140

## Findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Auditing is now on by default when no configuration exists: `public bool Enabled { get; set; } = true;`. Turning it off requires an explicit `CoreAuditing:Enabled=false`. | `api/core-libraries/Core.Auditing/AuditOptions.cs:6` |
| 1 | Added tests for the product default. `Enabled_NotConfigured_DefaultsToTrue` checks the code default. `AddCoreAuditing_EmptyConfiguration_RegistersAuditBehaviour` checks that `AuditBehaviour<,>` is registered with empty configuration. `AddCoreAuditing_ExplicitlyDisabled_DoesNotRegisterAuditBehaviour` checks that the opt-out still works. | `api/Elmanhg.Tests/Core/Auditing/AuditOptionsTests.cs:11-39` |

The earlier ApiFactory fix is still in place: `builder.UseSetting("CoreAuditing:Enabled", "true")`, `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs`.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Tests/Core/Auditing/AuditOptionsTests.cs | 40 | Tests that auditing is on by default and can be turned off explicitly |

## Files modified
| Path | Change |
|---|---|
| api/core-libraries/Core.Auditing/AuditOptions.cs | `Enabled` defaults to `true` |

## Deviations
None. The verifier asked for this test file. It is not in the original plan's test plan.

## Build & test
I moved `api/Elmanhg.Api/appsettings.json` aside, ran `dotnet test ./ -c Release` from `api/`, then put the file back. It is present again.
```
Test run summary: Passed!
  total: 259
  failed: 0
  succeeded: 259
  skipped: 0
```

## Notes for review
- `docs/audit-log.md:17` ("on in every environment") now agrees with the code, so no doc change was needed.
- The stale `bin/Release/.../appsettings.json` copies that set `Enabled: false` are build output only. The Release run passed, so they did not affect it.
