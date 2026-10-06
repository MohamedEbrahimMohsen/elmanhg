VERDICT: APPROVED

# Review — E21.S5 Core.Http and Core.Messaging: outbound HTTP, email/WhatsApp/SMS clients, OTP channels (round 1)

## Blocking
None.

## Non-blocking
1. `api/core-libraries/Core.OTP/Delivery/CoreOtpDeliveryServiceCollectionExtensions.cs:22-23`: OTP, invitations and reminders now share one `MetaWhatsAppClient` pipeline and one `ResendEmailClient` pipeline. That means one circuit breaker per provider instead of one per caller. Timeouts, retry rules and base URLs are identical (D6). The one new effect: a burst of 5xx/timeouts on reminder sends (at least 100 calls, at least 10% failing within the 30 s window) can now open the breaker for OTP sends to the same provider. OTP then returns 503 OTP_DELIVERY_FAILED sooner. The plan says the wire behaviour is identical, which is true on the happy path. Worth one sentence in `docs/otp-delivery.md` section 8, or separate named pipelines later if it matters.
2. `api/core-libraries/Core.OTP/Core.OTP.csproj:11-12`: the 10.0.7 to 10.0.9 bump of `Microsoft.Extensions.Configuration.Abstractions` / `Options.ConfigurationExtensions` is a justified deviation. NU1605 is a restore error against `Microsoft.Extensions.Http.Resilience` 10.7.0. The package ids stay the same, it is a patch-level change, and it was disclosed. It does not break "no new NuGet packages". Two notes: `Core.Utilities.csproj` still pins 10.0.7, so core pins are now uneven, and lane 304 also works in Core.OTP, so expect a possible merge touch on this csproj.
3. `api/Elmanhg.Infrastructure/DependencyInjection.cs:69`: no test checks that the real composition root registers `AddCoreHttp("Elmanhg/1.0")`. Test 36 covers `AddCoreHttp` alone, and the client tests use `CoreHttpTestSettings`. If that line were dropped, every provider call would silently lose its User-Agent and all tests would still pass. A one-line test such as AddInfrastructure, then `IOptions<CoreHttpOptions>.Value.UserAgent == "Elmanhg/1.0"`, would close this gap.
4. `api/core-libraries/Core.OTP/Delivery/OtpDeliveryProviders.cs:20,27`: the unknown-provider throws for Email and Sms have no test. Only WhatsApp has one (test 53, as the plan asked). `PaymentsServiceCollectionExtensions.UsesPaymob` (`_ => throw`) is also untested.
5. `api/Elmanhg.Infrastructure/Invitations/InvitationEmailServiceCollectionExtensions.cs` `UsesResend` and `Core.OTP/Delivery/OtpDeliveryProviders.UsesResend` now answer the same question in two different ways: the app helper does not throw on an unknown provider, the core one does. The plan kept this deliberately (A2). It could be merged later.
6. `api/core-libraries/Core.OTP/Delivery/OtpDeliveryResultExtensions.cs:18`: a provider 4xx rejection is still logged at Error. This is unchanged from the old `OtpProviderHttpExtensions` and is arguably right, because a 4xx from Meta or Resend is a misconfiguration on our side, not a caller outcome. Noted only against SKILL section 8.7.

## Verified
- **Build:** `dotnet build api/Elmanhg.slnx` gives 0 errors and 0 warnings.
- **Tests:** `dotnet test --no-build` gives total 5173, failed 0, skipped 0. This matches the report.
- **Format:** `dotnet format --verify-no-changes --include <every changed/new .cs>` exits 0. The solution-wide failure in untouched vendored files is pre-existing, as the report says.
- **`git status --porcelain api/openapi`:** empty. The Postman collection is untouched, and no endpoint changed, so that is correct.
- **Core is app-agnostic:** the DoD grep (elmanhg, OTP_DELIVERY_FAILED, OTP_CHANNEL_UNAVAILABLE) over `Core.Http`, `Core.Messaging` and `Core.OTP/Delivery` is empty.
  - `CoreHttpOptions.UserAgent` defaults to an empty string in core.
  - "Elmanhg/1.0" comes only from the app (`DependencyInjection.cs:64`) and `appsettings.example.json`.
  - The moved options classes have empty defaults, with no Egypt or Arabic defaults.
  - Trunk prefix '0' is kept per D13.
- **Transient-failure filter:** `IsTransientFailure` (`Core.Http/HttpFailureExtensions.cs:7`) is the same expression as the 10 removed copies. `ExecutionRejectedException` remains only there. Non-transient exceptions still propagate from `TrySendAsync`/`TrySendJsonAsync`, so:
  - OTP channels propagate them.
  - Invitations propagate them (test 61).
  - Reminders still swallow them through the kept catch-all `when (!ct.IsCancellationRequested)` (test 60).
- **Retries and timeouts:**
  - Retries are on for Resend only (OTP, invitation and reminder email), as before.
  - Meta, SMS, Paymob and all AI clients still call `DisableForUnsafeHttpMethods`.
  - Attempt and total timeouts read the same option per client.
  - `HttpClient.Timeout = Infinite` is kept for Transcription, Essay, MathCheck, MathStep and Configuration, and not for `HttpAiServiceClient`, matching the old code.
  - Circuit-breaker sampling is max(default 30 s, 2 x attempt). This matches D7: only `HttpAiConfigurationClient` moves, from 10 s to 30 s.
- **OTP routing and error codes:** `OtpChannelRouter` is the same except for `Notify` and `setup.ChannelUnavailableErrorCode`. The preferred phone channel falls back to the other channel only when the preferred one is disabled, as before. Metric name `elmanhg.otp.sends` and its tags are unchanged (existing metric tests pass). `EnsureDelivered` keeps both old log templates and OTP_DELIVERY_FAILED with and without the inner exception.
- **Secrets are never logged.**
  - `EnsureDelivered`, `ToDeliveredAsync` and the invitation helpers log only channel, status and exception.
  - `SendJsonAsync` logs Method and Path (AI paths only).
  - Authorization and auth-header values never reach a log template.
  - `MetaWhatsAppMessageChannelTests.SendAsync_Failure_LogsWithoutPhoneOrToken` still passes.
- **Payload shapes are unchanged:**
  - `ResendEmailRequest` has the same JSON attributes.
  - The merged `MetaWhatsAppTemplateMessage.Create` gives the OTP JSON (body [code] plus button code when CopyCodeButton), and the moved OTP body assertions pass.
  - `HttpSmsClient` is the same request construction.
  - The AI options are identical to the 5 removed copies. `AiServiceConfigurationResult` has only string and bool fields, so the converter added to the configuration client does not change reads.
- **Config keys:** the only new key is `CoreHttp:UserAgent` (`appsettings.example.json:34`, `docs/deployment.md`). `Configure(default)` comes before `BindConfiguration`, so config overrides the default (test 37).
- **Plan contract:**
  - Every file in H1-H14, M1-M11, O1-O16, A0a/A0b and A1-A16 exists with the planned signature.
  - `OtpProviderHttpExtensions.cs` and the app `ResendEmailMessage.cs` are deleted.
  - Nothing extra was created beyond the test files listed in the plan.
- **Deviations confirmed:** `ResendEmailRequest.Create` instead of `From` (CS0102 is real, because the positional `From` property exists). `ToDeliveredAsync` uses `if`s. `MessagingServiceCollectionExtensionsTests` keeps its `Elmanhg.Infrastructure.OtpDelivery` using. The csproj bump is judged above.
- **Out-of-scope paths untouched:** `git diff` shows no change under `Core.OTP/Entities`, `Core.OTP/Repositories`, `Core.OTP/DependencyInjection.cs`, `Core.Notifications`, `Core.EntityFrameworkCore`, `Program.cs`, `Storage/`, `RichText/` or `ObservabilityExtensions.cs`.
- **Comments:** no '#' followed by digits in any changed or new `.cs` file. The only comments are the three WHY comments the plan specified.
- **Style:**
  - File-scoped namespaces throughout.
  - Concrete types are `sealed`. `AiServiceHttpClient` is abstract by design.
  - Class declarations are on one line.
  - `.ConfigureAwait(false)` is on every await.
  - No try/catch outside the planned transient filters and the kept reminder catch-all.
  - No new `DateTime`.
  - No file over 100 lines.
  - The AI clients log through `Logger`, avoiding CS9107.
- **Docs-sync:** each of these matches the code as changed:
  - `docs/constitution.md` (line 3 and the section 4 Infrastructure bullet)
  - `.claude/skills/dotnet-feature/SKILL.md` sections 3 and 8.8
  - `docs/otp-delivery.md` sections 3, 8 and 10
  - `docs/implementation-report.md` OTP rows
  - `docs/deployment.md` `CoreHttp__UserAgent` row
  - `README.md` line 30

  No stale references to moved types remain in docs. `docs/otp-delivery.md:52` (email templates in `Elmanhg.Infrastructure/OtpDelivery/Email/Templates/`) is still true. `docs/paymob.md` "User-Agent: Elmanhg/1.0" and `docs/math-cas.md` "logs an Error" are still true. No divergence found.

## Test quality
- **Core.Http:**
  - `HttpFailureExtensionsTests` covers each branch of the predicate, including caller-cancel returning false, so it constrains the implementation.
  - `HttpClientSendExtensionsTests` and `HttpClientJsonExtensionsTests` use a real stub handler and check the wire request (method, URI, exact body, auth, User-Agent) plus each failure kind, error code and inner exception. A wrong mapping would fail them.
  - `HttpResilienceExtensionsTests` reads the actual named options (attempt, total, sampling widen and keep). Tests 31 and 32 work as a pair, so retry on and off are both pinned.
  - `ProviderSwitch` and `CoreHttpDependencyInjection` tests are behavioural.
- **Core.Messaging:** the client tests check the outbound request and result mapping. The template-message tests check the serialized JSON (button component, no `sub_type`/`index` when absent). None are vacuous.
- **Core.Otp/Delivery:**
  - Moved channel tests keep their wire and error-code assertions.
  - `OtpDeliveryResilienceTests` now goes through `AddCoreOtpDelivery` and still pins one attempt for Meta and SMS, and retries with a stable idempotency key for Resend.
  - Router tests 51 and 52 assert the observer interaction (which is the behaviour) and the injected code.
  - `FakeOtpChannelTests` checks log level and that the code is not leaked.
  - `CoreOtpDeliveryRegistrationTests` 53 and 54 are real registration checks. Test 54's `NotBeNull` lines are weak, but the BaseAddress assertions carry it.
- **App tests:**
  - 55 checks the observer is the same instance as `ElmanhgMetrics`.
  - 59 checks the unknown-provider error.
  - 60 and 61 pin the two different non-transient contracts (reminder swallows, invitation propagates). These would catch an accidental contract merge.
  - `OtpEmailTemplateTests` and `AiJsonSerializerOptionsTests` check the rendered or serialized output.
- No test asserts only a substitute's configured return. No existing assertion was removed: the removed lines in the test diff are only constructors, usings and generic-parameter changes.

---

# Round 2: verifying Rework r1

VERDICT: APPROVED

## Blocking
None.

## Non-blocking
7. `api/core-libraries/Core.OTP/Delivery/CoreOtpDeliveryServiceCollectionExtensions.cs:25`: the SMS named client is now `HttpSmsClient`. On origin/main it was `HttpSmsOtpChannel`. Isolation is not affected, because OTP is the only SMS caller, so it still has its own breaker. Nothing in config, appsettings or the docs depends on the old name. The only visible effect is the HttpClient log category and the `-standard` pipeline name. This is the one client name that does not match origin/main.
8. `api/core-libraries/Core.Http/HttpClientConsumerServiceCollectionExtensions.cs:14`: consumers are now scoped, where origin/main typed clients were transient. Within one scope the same instance is returned. The channels and senders are stateless, so behaviour does not change.
9. No test pins `retryUnsafeMethods: true` for the invitation and reminder Resend clients (`InvitationEmailServiceCollectionExtensions.cs:17`, `MessagingServiceCollectionExtensions.cs:22`). Only the OTP Resend client is pinned, by `OtpDeliveryResilienceTests`. The values are verified by reading the code against origin/main.

## Verified
- **(1) Per-caller isolation.**
  - The client names match origin/main `AddHttpClient<T>` names: `MetaWhatsAppOtpChannel` and `ResendEmailOtpChannel` (Core.OTP `:23-24`), `ResendInvitationEmailSender` (`InvitationEmailServiceCollectionExtensions.cs:17`), and `MetaWhatsAppMessageChannel` and `ResendEmailMessageChannel` (`MessagingServiceCollectionExtensions.cs:21-22`).
  - Each name gets its own `AddStandardResilienceHandler`, so each has its own `<name>-standard` pipeline and breaker.
  - No shared `AddHttpClient<MetaWhatsAppClient/ResendEmailClient>` registration is left (grep).
  - `AddScopedHttpConsumer` builds `TClient` through `ITypedHttpClientFactory<TClient>` over the caller-named client. The open generic is registered by `AddHttpClient(name)`.
  - Retry-unsafe settings match origin/main: Meta false (OTP and reminder), Resend true (OTP, invitation and reminder), SMS false.
  - `AddOtpProviderResilience` reads the same `AttemptTimeoutSeconds` and `TotalTimeoutSeconds` as the old `AddOtpResilience`.
- **Isolation test.** `ProviderClientIsolationTests.ReminderWhatsAppBreakerOpen_OtpWhatsAppStillReachesProvider` constrains the code:
  - Retries are disabled for POST, so 120 sends that each get a 500 hit the breaker's minimum throughput of 100. The breaker opens and `CallCount < 120` holds.
  - If OTP shared that breaker, `SendAsync` would throw `ServiceUnavailableCoreException` and the test would fail.
  - If the clients were named after the transport type, the stub overrides would not attach, and the OTP handler count assertion would fail.
- **(2) User-Agent test.** `DependencyInjectionTests.AddInfrastructure_NoUserAgentConfigured_SendsElmanhgProductToken` runs the real `AddInfrastructure()` with empty config. If the app's `AddCoreHttp("Elmanhg/1.0")` line were dropped, the test would fail on an empty string.
- **(3) Unknown-provider tests.**
  - The Email and Sms theory (`CoreOtpDeliveryRegistrationTests.cs:30-43`) uses settings with both channels enabled. Its message pattern checks that the right switch arm threw.
  - `AddPayments_UnknownProvider_ThrowsUnsupportedProvider` (`PaymentsServiceCollectionExtensionsTests.cs:63-72`) covers the Paymob `_ => throw`.
- **(4) Docs-sync.** Each of these agrees with the code, and no divergence was found:
  - `docs/otp-delivery.md` §8 (line 109) and §10 (line 119): per-caller named clients and breakers, plus the Resend vs Meta/SMS retry rule.
  - `.claude/skills/dotnet-feature/SKILL.md` §8.8 (line 995).
  - `docs/constitution.md:3`: the Core.Http list includes "per-caller named clients".
- **(5) Core stays app-agnostic.**
  - Grepping `Core.Http`, `Core.Messaging` and `Core.OTP/Delivery` for `elmanhg|OTP_DELIVERY|OTP_CHANNEL` finds nothing.
  - Grepping changed `.cs` under core, Infrastructure and Tests for comments containing `#<digits>` finds nothing.
  - The only new comment (`HttpClientConsumerServiceCollectionExtensions.cs:8`) explains why.
- **Build:** `dotnet build api/Elmanhg.slnx` gives 0 warnings and 0 errors.
- **Tests:** the affected classes (isolation, DI, CoreOtpDeliveryRegistration, Payments DI, OtpDeliveryResilience, Meta and Resend message channels, Messaging DI, ResendInvitationEmailSender, OtpDelivery DI, HttpResilienceExtensions, OtpEndpointTests) give 66 run and 66 passed. The new isolation, DI and registration tests give 8 of 8. OtpEndpointTests give 9 of 9.
- **Deviations (r1):** the new helper file and the two new test files are disclosed. They were requested by the rework.

## Test quality (r1 additions)
- `ProviderClientIsolationTests`: behavioural, and fails under either regression (a shared breaker or shared naming).
- `DependencyInjectionTests`: real composition root, and fails if the User-Agent line is removed.
- The unknown-provider theory and the Payments test: they check the exception type and a message specific to the switch arm. Neither is vacuous.
