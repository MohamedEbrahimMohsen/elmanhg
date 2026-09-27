# Implementation r2 — Authentication with phone OTP and email (#56, E1.S3)

## Blocking findings addressed
| # | Change | File:line |
|---|---|---|
| 1 | `FakeSmsSender` now logs the code and phone number only when `hostEnvironment.IsDevelopment()`. In every other environment it logs a warning with neither the code nor the phone number: "FakeSmsSender is active outside Development; the OTP was not delivered." | `api/Elmanhg.Infrastructure/Sms/FakeSmsSender.cs:11-18` |
| 1 | README note now says the code is written to the API console "in Development only". | `README.md:41` |
| 2 | Removed `accessToken` and `verificationId` from the `local` environment. They now exist only as collection variables, and those are the variables the six `pm.collectionVariables.set(...)` scripts write to. The scripts are unchanged, so capture works whether or not `local` is selected. | `postman/local.postman_environment.json:3-7` |

## Not edited (approved plan)
Finding 1 implies two wording changes in `01-plan.md`, which I left untouched as instructed:
- Decision 4 (line 34): "outside Production" should read "in Development only".
- DoD (line 563): "except `FakeSmsSender` outside Production" should read "except `FakeSmsSender` in Development".

## Deviations
None.

## Build & test
`dotnet test api/` (this also builds): Passed! total: 109, failed: 0, succeeded: 109, skipped: 0.

## Notes for review
- No test covers `FakeSmsSender` directly, and the plan's test plan does not list one. Integration tests use `RecordingSmsSender`.
- The Postman Auth folder description (`postman/elmanhg.postman_collection.json:43`) still says the code is "logged by FakeSmsSender in the API console". That is still true for local Development runs, so I left it alone.
