# Implementation r2: promoted review notes (#171, E1.S6)

## Promoted notes → changes
| # | What I changed | File:line |
|---|---|---|
| Note 4 (disabled real provider, blank BaseUrl) | Each channel factory now switches on the whole channel options: `{ Enabled: false } or { Provider: Fake }` resolves `FakeOtpChannel`. A disabled real provider's typed client is never built, so its `BaseUrl` is never parsed. Test: `AddOtpDelivery_DisabledMetaWithBlankBaseUrl_ResolvesFakeAndStillSendsEmail` sends an email through the real `IOtpSender` and asserts the WhatsApp channel is `FakeOtpChannel`. | `api/Elmanhg.Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensions.cs:22-39`; `api/Elmanhg.Tests/Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensionsTests.cs:63-76` |
| Note 3 (User-Agent) | `OtpProviderHttpExtensions.UserAgent = "Elmanhg/1.0"`, added to the request inside `SendOtpRequestAsync`. All three typed clients send through that helper, and it runs once before the resilience pipeline, so it is not duplicated on retries. Added the assertion `Headers.UserAgent.ToString() == "Elmanhg/1.0"` to the three existing request-shape tests. | `OtpProviderHttpExtensions.cs:11,15`; `MetaWhatsAppOtpChannelTests.cs:28`; `ResendEmailOtpChannelTests.cs:28`; `HttpSmsOtpChannelTests.cs:28` |
| Note 1 (retry policy) | New `OtpDeliveryResilienceTests`. It resolves each typed client through `AddOtpDelivery`, with `StubHttpMessageHandler` as the primary handler returning 500, and retry delay post-configured to zero (`{Client}-standard`). Assertions: Meta `CallCount == 1`, SMS `CallCount == 1`, Resend `CallCount > 1` and exactly one distinct, non-blank `Idempotency-Key` across all attempts. The stub now records a header snapshot per call (`RequestHeaders`). | `api/Elmanhg.Tests/Infrastructure/OtpDelivery/OtpDeliveryResilienceTests.cs` (new, 76 lines); `StubHttpMessageHandler.cs:17,24` |
| Note 5 (review item 6, Postman order) | The Auth folder now runs SendOtp, VerifyOtp, RegisterWithPhone, RegisterWithEmail, LoginWithPhone, **SendEmailOtp, LoginWithEmailCode**, LoginWithEmail, RefreshAccessToken, Logout. The admin `LoginWithEmail` is last before Refresh, so the admin token and cookie carry into the admin folders. This is a pure block move; no request content changed. | `postman/elmanhg.postman_collection.json` (Auth folder) |

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Tests/Infrastructure/OtpDelivery/OtpDeliveryResilienceTests.cs` | 76 | Pins the retry-idempotency policy (note 1) |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensions.cs` | Disabled channel resolves Fake (note 4) |
| `api/Elmanhg.Infrastructure/OtpDelivery/OtpProviderHttpExtensions.cs` | `UserAgent` const + header (note 3) |
| `api/Elmanhg.Tests/Infrastructure/OtpDelivery/StubHttpMessageHandler.cs` | Per-call `RequestHeaders` snapshots |
| `api/Elmanhg.Tests/Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensionsTests.cs` | +1 test (note 4) |
| `api/Elmanhg.Tests/Infrastructure/OtpDelivery/{MetaWhatsApp,ResendEmail,HttpSms}OtpChannelTests.cs` | +1 User-Agent assertion each |
| `postman/elmanhg.postman_collection.json` | Auth request order (note 5) |
| `docs/otp-delivery.md` | §4: a disabled channel is never built. §6: every provider request carries `User-Agent: Elmanhg/1.0`. This is a docs-sync fix for the behaviour change. |

## Deviations
| Asked | Reality | What I did |
|---|---|---|
| Set User-Agent "on all three typed HttpClients" and assert it in the existing request-shape tests | The request-shape tests build a bare `new HttpClient(stub)`. A `DefaultRequestHeaders` value set in the DI configure delegates would never reach them, so they could not assert it. | I set the header per request in the shared `SendOtpRequestAsync`, which all three typed clients use. The effect on the wire is the same, it is covered by the existing tests, and it lives in one place. |

## Mutation checks (each run, observed red, then reverted)
1. Dropped `{ Enabled: false } or` for WhatsApp. `DisabledMetaWithBlankBaseUrl` failed with `UriFormatException`. On Linux, `BeOfType<FakeOtpChannel>` would still catch it.
2. Meta `retryUnsafeMethods: true`. `MetaWhatsApp_ServerError_MakesExactlyOneAttempt` failed: "expected 1, found 4".
3. Removed `DisableForUnsafeHttpMethods()`. The Meta and SMS one-attempt tests both failed.
4. Resend `retryUnsafeMethods: false`. `ResendEmail_ServerError_RetriesWithTheSameIdempotencyKey` failed: "greater than 1, found 1".
5. Added a temporary per-attempt `DelegatingHandler` that re-keys `Idempotency-Key`. The Resend test failed with 4 distinct keys. The temporary file was deleted.
6. Removed the `UserAgent.ParseAdd` line. All 3 request-shape tests failed.

## Build & test
- `dotnet build api/ -c Release`: Build succeeded, 0 Warning(s).
- `dotnet test /d/Personal/elmanhg/api/ -c Release`, with `api/Elmanhg.Api/appsettings.json` moved to the scratchpad: `total: 1990, failed: 0, succeeded: 1990, skipped: 0`. That is 1986 plus the 4 new tests. The file was moved back afterwards, and its presence was confirmed.
- Web and ai were not touched, so they were not run.

## Notes for review
- The Resend retry test needs `PostConfigure<HttpStandardResilienceOptions>("ResendEmailOtpChannel-standard", Retry.Delay = 0)` to avoid the roughly 14 s real backoff. The name follows the library's `{clientName}-standard` convention. If that convention changed, the test would only get slow, not wrong.
- The Python edits normalised the touched files to LF. `core.autocrlf=true` normalises on commit, and most sibling files in these folders are already LF.
- `PROGRESS.md` and `scripts/` were not touched. Nothing is committed.
