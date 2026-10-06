# Implementation — E21.S5 Core.Http and Core.Messaging: outbound HTTP, email/WhatsApp/SMS clients, OTP channels

Worktree: `D:/Personal/elmanhg-wt/308`. Nothing is committed or pushed. Moves were done with `git mv`, so git shows them as renames (R/RM).

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.Http/Core.Http.csproj` | 21 | H1: AspNetCore.App framework reference, Http.Resilience 10.7.0 pinned inline, references Core.Errors |
| `api/core-libraries/Core.Http/CoreHttpOptions.cs` | 8 | H2: `CoreHttp:UserAgent` |
| `api/core-libraries/Core.Http/DependencyInjection.cs` | 13 | H3: `AddCoreHttp(defaultUserAgent)`. Sets the default, then binds config over it |
| `api/core-libraries/Core.Http/HttpBaseAddress.cs` | 8 | H4: `From` and `Combine` |
| `api/core-libraries/Core.Http/HttpCallFailure.cs` | 3 | H5 |
| `api/core-libraries/Core.Http/HttpSendResult.cs` | 6 | H6 |
| `api/core-libraries/Core.Http/HttpJsonReply.cs` | 6 | H7 |
| `api/core-libraries/Core.Http/HttpJsonCall.cs` | 19 | H8: `Post`/`Get` factories and `ToRequest` |
| `api/core-libraries/Core.Http/HttpFailureExtensions.cs` | 8 | H9: `IsTransientFailure` (the only `ExecutionRejectedException` left in the code) |
| `api/core-libraries/Core.Http/HttpRequestMessageExtensions.cs` | 14 | H10: `WithUserAgent` (a blank value sends no header) |
| `api/core-libraries/Core.Http/HttpClientSendExtensions.cs` | 17 | H11: `TrySendAsync` |
| `api/core-libraries/Core.Http/HttpClientJsonExtensions.cs` | 54 | H12: `TrySendJsonAsync` and `SendJsonAsync` |
| `api/core-libraries/Core.Http/HttpResilienceExtensions.cs` | 25 | H13: `AddTimeoutResilience`, which widens the sampling window only when it is too small |
| `api/core-libraries/Core.Http/ProviderSwitchServiceCollectionExtensions.cs` | 23 | H14: both `AddProviderSwitch` overloads |
| `api/core-libraries/Core.Messaging/Core.Messaging.csproj` | 13 | M1 |
| `api/core-libraries/Core.Messaging/Email/EmailMessage.cs` | 3 | M3 |
| `api/core-libraries/Core.Messaging/Email/ResendEmailClient.cs` | 24 | M5 |
| `api/core-libraries/Core.Messaging/WhatsApp/MetaWhatsAppSender.cs` | 3 | M7 |
| `api/core-libraries/Core.Messaging/WhatsApp/MetaWhatsAppClient.cs` | 21 | M8 |
| `api/core-libraries/Core.Messaging/Sms/HttpSmsGateway.cs` | 3 | M10 |
| `api/core-libraries/Core.Messaging/Sms/HttpSmsClient.cs` | 24 | M11 |
| `api/core-libraries/Core.OTP/Delivery/OtpDeliverySetup.cs` | 3 | O3 |
| `api/core-libraries/Core.OTP/Delivery/OtpEmailContent.cs` | 3 | O4 |
| `api/core-libraries/Core.OTP/Delivery/IOtpDeliveryObserver.cs` | 6 | O5 |
| `api/core-libraries/Core.OTP/Delivery/OtpDeliveryProviders.cs` | 29 | O8: `UsesMeta` / `UsesResend` / `UsesHttp`, each throwing on an unknown provider |
| `api/core-libraries/Core.OTP/Delivery/OtpDeliveryResultExtensions.cs` | 25 | O9: `EnsureDelivered` |
| `api/core-libraries/Core.OTP/Delivery/CoreOtpDeliveryServiceCollectionExtensions.cs` | 43 | O10: `AddCoreOtpDelivery(setup)` |
| `api/Elmanhg.Infrastructure/AiService/AiJsonSerializerOptions.cs` | 13 | A0a |
| `api/Elmanhg.Infrastructure/AiService/AiServiceHttpClient.cs` | 22 | A0b: abstract base with `PostAsync`, `TryPostAsync`, `TryGetAsync` and `Logger` |
| Moved with `git mv`, then edited (O1, O2, O6, O7, O11–O16, M2, M4, M6) | — | `Core.OTP/Delivery/{OtpDeliveryOptions,OtpDeliveryOptionsValidator,OtpChannelRouter,FakeOtpChannel}.cs`, `Core.OTP/Delivery/{Email,Sms,WhatsApp}/*`, `Core.Messaging/{PhoneNumberFormatter,Sms/HttpSmsBodyRenderer,WhatsApp/MetaWhatsAppTemplateMessage,Email/ResendEmailRequest}.cs` |
| **Tests (new)** `api/Elmanhg.Tests/Core/Http/CoreHttpTestSettings.cs` | 11 | Helper |
| `Tests/Core/Http/{HttpFailureExtensions,HttpBaseAddress,HttpRequestMessageExtensions,HttpClientSendExtensions,HttpClientJsonExtensions,HttpResilienceExtensions,ProviderSwitchServiceCollectionExtensions,CoreHttpDependencyInjection}Tests.cs` | — | Tests 1–37 |
| `Tests/Core/Messaging/{ResendEmailClient,MetaWhatsAppClient,MetaWhatsAppTemplateMessage,HttpSmsClient}Tests.cs` | — | Tests 38–48 |
| `Tests/Core/Otp/Delivery/{FakeOtpChannel,CoreOtpDeliveryRegistration}Tests.cs` | — | Tests 49–50 and 53–54 |
| `Tests/Infrastructure/OtpDelivery/OtpEmailTemplateTests.cs` | 16 | Test 56 |
| `Tests/Infrastructure/AiService/AiJsonSerializerOptionsTests.cs` | 27 | Tests 57–58 |
| Tests moved with `git mv` | — | `HttpSmsBodyRendererTests` and `PhoneNumberFormatterTests` → `Core/Messaging/`. `MetaWhatsAppOtpChannel`, `ResendEmailOtpChannel`, `HttpSmsOtpChannel`, `OtpChannelRouter` (plus tests 51 and 52), `OtpDeliveryOptionsValidator` and `OtpDeliveryResilience` tests → `Core/Otp/Delivery/`. Names and assertions are unchanged |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.slnx` | Core.Http added after Core.Exceptions, Core.Messaging after Core.Logging |
| `api/core-libraries/Core.OTP/Core.OTP.csproj` | Core.Messaging ProjectReference added, plus a version bump (see Deviations) |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | Core.Http and Core.Messaging ProjectReferences |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `DefaultUserAgent` const with the plan's WHY comment; `AddCoreHttp(DefaultUserAgent)` is the first line |
| `api/Elmanhg.Api/appsettings.example.json` | `"CoreHttp": { "UserAgent": "Elmanhg/1.0" },` before `CoreOtp` |
| `api/Elmanhg.Application/Shared/Observability/ElmanhgMetrics.cs` | `: IOtpDeliveryObserver` |
| `Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensions.cs` | A1 rewrite: `AddCoreOtpDelivery(setup)` and the observer forward |
| `Infrastructure/OtpDelivery/Email/OtpEmailTemplate.cs` | `Render(code, minutes)` |
| `Infrastructure/OtpDelivery/{OtpProviderHttpExtensions.cs}` | Deleted (`git rm`) |
| `Infrastructure/Invitations/{InvitationEmailServiceCollectionExtensions,ResendInvitationEmailSender,InvitationEmailOptionsValidator}.cs` | A2, A3, usings |
| `Infrastructure/Messaging/{MessagingServiceCollectionExtensions,MessagingHttpExtensions,MetaWhatsAppMessageChannel,ResendEmailMessageChannel,OutOfAppReminderOptionsValidator}.cs` | A4–A7, usings |
| `Infrastructure/AiService/AiServiceServiceCollectionExtensions.cs` + 6 `HttpAi*Client.cs` | A8–A14 |
| `Infrastructure/Payments/{PaymentsServiceCollectionExtensions,FakePaymentGateway}.cs`, `Paymob/{PaymobPaymentGateway,PaymobPaymentGateway.Refund,PaymobIntentionRequest}.cs` | A15, A16, `HttpBaseAddress.Combine` |
| `Infrastructure/Hosting/{InfrastructureIntegrations,InfrastructureConfigurationReader}.cs` | Usings only |
| Tests adapted in place | `OtpDeliveryServiceCollectionExtensionsTests` (+ test 55), `OtpDeliveryTestSettings` (+ `Setup()`), `MetaWhatsAppMessageChannelTests` (+ test 60), `ResendEmailMessageChannelTests`, `MessagingServiceCollectionExtensionsTests`, `ResendInvitationEmailSenderTests` (+ test 61), 6 `HttpAi*ClientTests`, `AiServiceServiceCollectionExtensionsTests` (+ test 59), `InfrastructureConfigurationReaderTests`, `PaymobPaymentGateway{,Refund}Tests`, `OtpEndpointTests` |
| `docs/constitution.md` | Line 3 core list; §4 Infrastructure bullet |
| `.claude/skills/dotnet-feature/SKILL.md` | §3 tree line; §8.8 example and text |
| `docs/otp-delivery.md` | §3 location paragraph; §8 typed clients and `CoreHttp:UserAgent` sentence; §10 adapters on `Core.Messaging` |
| `docs/implementation-report.md` | OTP rows: fake path and real adapter paths now under `Core.OTP/Delivery` |
| `docs/deployment.md` | General API table: `CoreHttp__UserAgent` row after `CoreOtp__Secret` |
| `README.md` | Line 30: "… plus the Elmanhg-built `Core.Http` and `Core.Messaging`" |

The Postman collection is untouched because no endpoint changed.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| M4: `public static ResendEmailRequest From(EmailMessage message)` on the record | The record already has a positional property `From`. A static method of the same name is CS0102 (duplicate member) | Named the factory `ResendEmailRequest.Create(EmailMessage)`. `ResendEmailClient` calls `Create`. JSON shape unchanged |
| `Core.OTP.csproj`: only add the Core.Messaging ProjectReference (D17: "no new NuGet package") | Core.OTP pins `Microsoft.Extensions.Configuration.Abstractions` and `Microsoft.Extensions.Options.ConfigurationExtensions` at `10.0.7` inline. `Microsoft.Extensions.Http.Resilience 10.7.0` (via Core.Messaging → Core.Http) needs ≥ `10.0.9`, so restore fails with NU1605 (package downgrade, an error by default) | Bumped those two inline pins in `Core.OTP.csproj` from `10.0.7` to `10.0.9`. Same package ids and no new package. `dotnet list package --vulnerable` stays clean |
| A5: switch on `None` / `Rejected` / `Unreachable` (switch expression implied) | Each arm logs, and logging returns void | `MessagingHttpExtensions.ToDeliveredAsync` uses two `if`s (Succeeded → true; Rejected → warn and false; otherwise warn and false). `ResendInvitationEmailSender` (A3) uses the switch expression the plan asked for, with two small private `Rejected`/`Unreachable` helpers that log and return false |
| Test plan, `MessagingServiceCollectionExtensionsTests`: "Usings only" (drop `Elmanhg.Infrastructure.OtpDelivery`) | The test still calls `services.AddOtpDelivery()`, which lives in `Elmanhg.Infrastructure.OtpDelivery` | Kept that using and added `Core.OTP.Delivery` |

## Build & test
- `dotnet build api/` (Debug): **Build succeeded**, no warnings in any touched or new file. The remaining warnings are the existing CS8618/CS8602 in vendored `Core.Notifications`, `Core.OTP/Entities` and `Core.Validation`, all untouched.
- `dotnet build api/ -c Release`: **Build succeeded**. `git status --porcelain api/openapi` is empty, so the OpenAPI document is unchanged.
- `dotnet test api/` (Docker up, Testcontainers): **Passed! total 5173, failed 0, succeeded 5173, skipped 0** (1m 50s).
- New and moved core tests only (`--filter-namespace Elmanhg.Tests.Core.Http/.Messaging/.Otp.Delivery`): **95 passed**, which is 37 Core.Http + 15 Core.Messaging + 43 Core.OTP delivery.
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build` (with the CI connection string): **"No changes have been made to the model since the last migration."**
- `dotnet list api/ package --vulnerable --include-transitive`: no vulnerable packages.
- `dotnet format api/Elmanhg.slnx --verify-no-changes`: **fails for the whole solution, but only in files this story did not touch.** There are 100 WHITESPACE diagnostics in `Core.Notifications`, `Core.OTP/Entities`, `Core.OTP/VerifyOTP`, `Core.Utilities`, `Core.CQRS`, `Core.DDD`, `Tests/Builders` and similar. Scoped to the files this change touches (`--include <every changed or new .cs>`), it exits 0 with no diagnostics. The solution-wide DoD item "`dotnet format --verify-no-changes` exits 0" was therefore not met before this change and is not met after it. CI does not run format.
- DoD greps:
  - The core app-agnostic grep over `Core.Http`, `Core.Messaging` and `Core.OTP/Delivery` prints nothing.
  - `ExecutionRejectedException` appears only in `Core.Http/HttpFailureExtensions.cs`.
  - `AddStandardResilienceHandler|OtpProviderHttpExtensions|new JsonSerializerOptions(JsonSerializerDefaults.Web)` under the five Infrastructure folders prints nothing. `AiJsonSerializerOptions` uses `new(JsonSerializerDefaults.Web)`, so it does not match the literal.
  - `git diff` shows no change under `Core.OTP/Entities`, `Core.OTP/Repositories`, `Core.OTP/DependencyInjection.cs`, `Core.Notifications`, `Core.EntityFrameworkCore`, `Program.cs`, `Storage/`, `RichText/` or `ObservabilityExtensions.cs`.
  - No code comment contains `#` followed by digits.
  - No `TrimEnd('/')` is left in Infrastructure outside Storage and RichText, which lane #310 owns.

## Deferred
None from the plan. Storage and RichText `TrimEnd('/')` and their provider-switch copies stay as they are on purpose (lanes #310 and #313).

## Notes for review
1. **Circuit-breaker window (D7).** `AddTimeoutResilience` keeps the default 30 s window unless 2×attempt is larger. With today's defaults (AiService attempt 45, transcription 150, essay 100, math step 100, math check 15), every AI client keeps its window. Only `HttpAiConfigurationClient` (5 s) moves from 10 s to 30 s, as the plan says. A deployment that overrides `AiService:AttemptTimeoutSeconds` below 15 would also get 30 s instead of 2×attempt. That is valid and not observable from any endpoint.
2. **Wire contract.** All 7 pre-existing client tests that assert `User-Agent: Elmanhg/1.0`, `Authorization` and `Idempotency-Key` pass unchanged. The OTP WhatsApp JSON comes from the merged `Create`, and the moved `MetaWhatsAppOtpChannelTests` body assertions still pass.
3. **Typed-client sharing (D6).** `MetaWhatsAppClient` and `ResendEmailClient` are now registered once, in `AddCoreOtpDelivery`. Invitations and reminders inject them. `MetaWhatsAppMessageChannelTests.BuildResilientProvider` now wires the stub through `AddHttpClient<MetaWhatsAppClient>()` and the `MetaWhatsAppClient-standard` pipeline, as the plan says.
4. **Test 54** reads base addresses with `IHttpClientFactory.CreateClient(nameof(TClient))` rather than reflecting into the typed client. Typed clients use their type name as the client name, so this checks the same configuration.
5. **Test 52** builds the setup with `OtpDeliveryTestSettings.Setup() with { ChannelUnavailableErrorCode = "NOT_A_REAL_CODE" }` and passes zero observers (`[]`), which also covers the "zero observers needs no null class" case.
6. `Core.OTP/Delivery/OtpDeliveryOptionsValidator.cs` is now 100 lines, one more using line than before (`Core.Messaging.Sms`).
7. Working-copy line endings follow the repo (LF in the worktree, with `core.autocrlf=true`). Git prints "LF will be replaced by CRLF" for every touched file, as it does for existing files.

## Rework r1

| Finding | What I changed | File:line |
|---|---|---|
| NB1 (treated as required): OTP, invitations and reminders shared one Meta and one Resend pipeline, so one circuit breaker | New `Core.Http` helper `AddScopedHttpConsumer<TConsumer, TClient>(configureClient)`. It registers a named `HttpClient` called `typeof(TConsumer).Name` and a scoped `TConsumer` built through `ActivatorUtilities`, using a `TClient` made by `ITypedHttpClientFactory<TClient>` over that named client. The pipeline names are back to what they were before the change (`MetaWhatsAppOtpChannel`, `ResendEmailOtpChannel`, `ResendInvitationEmailSender`, `MetaWhatsAppMessageChannel`, `ResendEmailMessageChannel`), and each has its own resilience pipeline and breaker. The client code is still the shared `Core.Messaging` one. Added public `AddOtpProviderResilience(retryUnsafeMethods)` in Core.OTP: the same timeouts as before, from `OtpDeliveryOptions`, reused by the app callers. SMS keeps `AddHttpClient<HttpSmsClient>` because OTP is its only caller. | `api/core-libraries/Core.Http/HttpClientConsumerServiceCollectionExtensions.cs:1-18` (new); `api/core-libraries/Core.OTP/Delivery/CoreOtpDeliveryServiceCollectionExtensions.cs:23-26,35`; `api/Elmanhg.Infrastructure/Invitations/InvitationEmailServiceCollectionExtensions.cs:17,25`; `api/Elmanhg.Infrastructure/Messaging/MessagingServiceCollectionExtensions.cs:21-22` |
| NB1 test | `ProviderClientIsolationTests.ReminderWhatsAppBreakerOpen_OtpWhatsAppStillReachesProvider` sends 120 reminders through a 500-returning reminder client and asserts the reminder breaker opened (handler calls < 120). It then sends an OTP over WhatsApp and asserts it reached its own handler once and did not throw 503. Under the old shared registration the OTP send would hit the open breaker. | `api/Elmanhg.Tests/Infrastructure/Messaging/ProviderClientIsolationTests.cs` (new) |
| NB1 test fallout | Switched the handler overrides and client-name lookups from the transport type name to the caller name. | `Core/Otp/Delivery/OtpDeliveryResilienceTests.cs`, `Core/Otp/Delivery/CoreOtpDeliveryRegistrationTests.cs` (the `EnabledProviders_ResolveTransportTypedClients` body now resolves the OTP channels and checks `CreateClient(nameof(MetaWhatsAppOtpChannel/ResendEmailOtpChannel))` base addresses), `Infrastructure/Messaging/MetaWhatsAppMessageChannelTests.cs:121-122` |
| NB3: no test that the composition root sets `Elmanhg/1.0` | `DependencyInjectionTests.AddInfrastructure_NoUserAgentConfigured_SendsElmanhgProductToken` calls the real `AddInfrastructure()` with empty config and asserts `IOptions<CoreHttpOptions>.Value.UserAgent == "Elmanhg/1.0"` | `api/Elmanhg.Tests/Infrastructure/DependencyInjectionTests.cs` (new) |
| NB4: unknown-provider throws untested (Email, Sms, Paymob) | Theory `AddCoreOtpDelivery_UnknownEmailOrSmsProvider_ThrowsUnsupportedProvider` (Email, Sms) and `AddPayments_UnknownProvider_ThrowsUnsupportedProvider` | `Core/Otp/Delivery/CoreOtpDeliveryRegistrationTests.cs:30-43`; `Infrastructure/Payments/PaymentsServiceCollectionExtensionsTests.cs:63-72` |
| Docs-sync for NB1 | `docs/otp-delivery.md` section 8 (provider calls) and the reminders paragraph now describe per-caller named clients and breakers. SKILL section 8.8 gains the `AddScopedHttpConsumer` rule. The `docs/constitution.md` line 3 Core.Http list gains "per-caller named clients". | `docs/otp-delivery.md:109,119`; `.claude/skills/dotnet-feature/SKILL.md:995`; `docs/constitution.md:3` |

### Deviations (r1)
| Plan said | Reality | What I did |
|---|---|---|
| Only the planned Core.Http files | Per-caller isolation needs a registration helper that both Core.OTP and the app can use | Added one new file, `Core.Http/HttpClientConsumerServiceCollectionExtensions.cs`, and two new test files, `ProviderClientIsolationTests.cs` and `Infrastructure/DependencyInjectionTests.cs`. All three were requested by this rework. |
| Plan A-rows: the app channels are plain `AddScoped<...>` over shared typed clients | That registration was the cause of NB1 | Replaced it with `AddScopedHttpConsumer` registrations |

### Build & test (r1)
- `dotnet build api/Elmanhg.slnx`: 0 errors. The 2 CS8618 warnings come from the untouched `Core.OTP/Entities/OTP.cs:31`. They appear because Core.OTP was recompiled, not because of this change.
- Affected classes (`--filter-class` over the 9 touched/new test classes): total 66, failed 0.
- `dotnet test api/Elmanhg.slnx`: total 5178, failed 0, succeeded 5178, skipped 0. That is the previous 5173 plus the 5 new test cases.
- `dotnet format --verify-no-changes --include <the 10 changed/new .cs files>`: exit 0.

## Merge with main

The branch was fast-forwarded to `origin/main`, which includes the merged Core.Storage story: storage code moved from `Elmanhg.Infrastructure/Storage` and `Application/Shared/Storage` into `api/core-libraries/Core.Storage`, namespace `Core.Storage`. Popping the stash then conflicted in seven files. In every one the two sides had changed the same `using` lines or the same reference list, and the resolution keeps both.

| File | Resolution |
|---|---|
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `using Core.Http;` and `using Core.Storage;` (keeps both `AddCoreHttp` and main's `AddCoreFileStorage`) |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | References Core.Http, Core.Messaging and Core.Storage |
| `api/Elmanhg.Infrastructure/Hosting/InfrastructureConfigurationReader.cs` | `using Core.OTP.Delivery;` and `using Core.Storage;` |
| `api/Elmanhg.Infrastructure/Hosting/InfrastructureIntegrations.cs` | The `Core.OTP.Delivery.*` usings plus `using Core.Storage;` |
| `api/Elmanhg.Tests/Infrastructure/Hosting/InfrastructureConfigurationReaderTests.cs` | The `Core.OTP.Delivery.*` usings and `using Core.Storage;`. Kept `Elmanhg.Tests.Core.Http` and removed the stale `Elmanhg.Infrastructure.Storage` using |
| `.claude/skills/dotnet-feature/SKILL.md` | Core library line keeps `Core.Storage`, and the `Core.Http · Core.Messaging` line follows it |
| `docs/constitution.md` | Stack line lists `Core.Storage` and also the Elmanhg-built `Core.Http` and `Core.Messaging` description |

Changes that merged without conflict were also checked:
- `api/Elmanhg.slnx` and `docs/implementation-report.md` merged on their own and contain both sides.
- No `Elmanhg.Infrastructure.Storage` or `Application.Shared.Storage` reference is left anywhere under `api/`.
- I compared every stashed file with the working tree, ignoring line endings. The only files that differ are the seven resolved above plus the two auto-merged ones, so every other story change is present as stashed.
- This story's plan did not touch storage. It left the Storage `TrimEnd('/')` and provider-switch copies alone, and they now live in `Core.Storage` (`FileStorageOptions.GetPublicUrl`, `DependencyInjection` provider check). They are still unchanged and remain for their own lane.

Verification after the merge:
- `dotnet build api/`: **Build succeeded**, 0 errors (9 warnings, all from existing core libraries).
- `dotnet test api/`: **Passed!** total 5198, failed 0, succeeded 5198, skipped 0.
- `dotnet build api/ -c Release`, then `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build` (with the CI connection string): **"No changes have been made to the model since the last migration."**
