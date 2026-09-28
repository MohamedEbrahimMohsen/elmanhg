VERDICT: APPROVED

# Review: OTP delivery channels, WhatsApp (Meta), Email (Resend), SMS (disabled) (#171, E1.S6)

## Blocking
None.

## Non-blocking
1. `api/Elmanhg.Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensions.cs:19-21,50-61`: no test covers the resilience wiring, which is the "retry is idempotent only" guarantee. If you flipped `retryUnsafeMethods` for Meta or SMS, or deleted `DisableForUnsafeHttpMethods()`, every test would still pass. The code is correct as written: Meta and SMS POSTs are never retried, and Resend retries reuse the same `HttpRequestMessage`, so the same `Idempotency-Key` goes out on every attempt. Suggested test: resolve each typed client through `AddOtpDelivery` with a primary `StubHttpMessageHandler` that returns 500. Assert `CallCount == 1` for Meta and SMS, and `> 1` for Resend.
2. `api/Elmanhg.Infrastructure/OtpDelivery/OtpProviderHttpExtensions.cs:18`: only the `HttpRequestException` and caller-cancel branches of the exception filter are tested. Add tests for `TimeoutRejectedException` / `BrokenCircuitException` and for an `OperationCanceledException` that the caller did not cause (a timeout). Each should map to 503 `OTP_DELIVERY_FAILED`.
3. `api/Elmanhg.Infrastructure/OtpDelivery/Email/ResendEmailOtpChannel.cs:22-27`: the adapter sends no `User-Agent` header, and .NET `HttpClient` does not add one by default. I believe the Resend API docs require a `User-Agent` and reject requests without it with 403. **I am not certain of this.** If it is true, every real Resend send would come back as 503 `OTP_DELIVERY_FAILED`. Check it during the deferred live smoke test (deferred item 1). The fix is cheap: set `client.DefaultRequestHeaders.UserAgent` in the typed-client configure delegates.
4. `api/Elmanhg.Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensions.cs:19-20,46`: when a real provider is chosen but its channel is disabled (for example `Provider=Meta`, `Enabled=false`), the validator skips it (by design). `IEnumerable<IOtpChannel>` still builds the real adapter on every send, though. If that disabled channel has a blank `BaseUrl`, `new Uri("/")` throws on Windows (on Linux it becomes a `file:` URI), and then every OTP send fails, email included. The committed config sets both BaseUrls, so this is an edge case. Fixes: resolve a fake when the channel is disabled, or validate `BaseUrl` whenever the provider is real.
5. `api/Elmanhg.Infrastructure/OtpDelivery/FakeOtpChannel.cs:13-20`: no test covers "Development logs the code" vs "otherwise a warning with no code". This is the only place that decides whether an OTP reaches the logs. A test with a recording `ILogger` would pin it.
6. `postman/elmanhg.postman_collection.json` (Auth folder): `SendEmailOtp` / `LoginWithEmailCode` sit after the admin `LoginWithEmail`. Suppose someone follows the descriptions (send, VerifyOtp, login) and then keeps running down the folder. `RefreshAccessToken` would then use the student cookie of Mona, and the admin folders after it get 403. Putting the pair before `LoginWithEmail` keeps the admin token last, which is the state-order convention in PROGRESS.md. The existing phone requests are already not runnable top to bottom, so this does not block.
7. `docs/otp-delivery.md` section 1 says logs never contain "the recipient, the code". `FakeOtpChannel.cs:15` logs both in Development. Section 3 of the same doc explains this, but a one-line qualifier in section 1 ("real adapters") would remove the apparent contradiction.
8. `api/core-libraries/Core.OTP/GenerateOTP/GenerateOTPCommand.cs:5`: `PhoneNumber` has no default, so the generated client marks `phoneNumber` required-nullable, and the web sends `{ phoneNumber: null, email }` (disclosed deviation). If `PhoneNumber` got a `= null` default, the OpenAPI contract would match the "exactly one of" intent.
9. `web/src/features/session/pages/LoginPage.test.tsx` "signs a student in with an email code": the MSW handlers accept any body, so the test does not check that `otp/send` gets `email`, or that `login/email-code` is called with the verified id. A request-body assertion would pin it.
10. Style, consistent with the plan and existing repo precedent (so not blocking): several expression-bodied static helpers and `?? throw` / `? x : throw` (`PhoneNumberFormatter.cs:7`, `OtpChannelRouter.cs:24,34,37`, `OtpEmailTemplate.cs:15-25`, `HttpSmsBodyRenderer.cs:14-19`), and a registration extension outside `DependencyInjection.cs` (constitution section 2). `Domain/ExamBlueprints/ExamBlueprintJson.cs` and `Api/RateLimiting/AuthRateLimiting.cs` already follow the same patterns on main.
11. On provider failure, `OtpProviderHttpExtensions.cs:20,28` logs at Error, and `CoreExceptionMiddleware` logs the same 503 at Error again. You get two entries per failure; neither contains a code or recipient.

## Verified
- **Runs (mine):**
  - `dotnet test api/ -c Release` with `appsettings.json` moved aside: total 1986, failed 0. The file was restored afterwards.
  - web `typecheck`: exit 0. `lint`: exit 0. `test -- --run`: 104 files, 648 tests passed.
  - `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: clean.
  - `dotnet format --verify-no-changes`: only the known whitespace noise in `core-libraries`.
  - `dotnet list package --vulnerable --include-transitive` (Infrastructure): clean.
- **Deviations table:** confirmed.
  - `Microsoft.Extensions.Http.Resilience` is pinned at 10.7.0 with CPM, and the csproj reference has no version.
  - The `CodeToken` const is shared by the channel and the validator.
  - Web sends `phoneNumber: null`.
  - Postman has an `email` variable.
  - The IStartupValidator test asserts `Throw<OptionsValidationException>().WithMessage("*AccessToken*")`.
- **Security:**
  - Neither the OTP code nor any credential appears in the adapter logs or in exception messages. Failure logs carry the channel plus the status code only. The recipient and code are logged only by the Fake channel in Development, the same as the old `FakeSmsSender`.
  - Credentials are read only from `OtpDelivery` options.
  - Committed files hold only empty placeholders (`appsettings.example.json`, commented `.env.example`). Test values are obvious fakes.
  - No enumeration: `otp/send` with an email never looks up an account. `EMAIL_NOT_REGISTERED` and `EMAIL_CODE_SIGN_IN_NOT_ALLOWED` are only reachable after verifying a code sent to that inbox, the same as `PHONE_NUMBER_NOT_REGISTERED` in the phone flow.
  - Rate limits: `login/email-code` is under `AuthRateLimitPolicies.Credentials` (per IP). `otp/send` stays under `OtpRequests` for both recipient types. The per-recipient reissue cooldown and cap still apply.
- **Adapters:**
  - Meta: `POST {BaseUrl}/{ApiVersion}/{PhoneNumberId}/messages`, Bearer token. Body is `messaging_product=whatsapp`, `recipient_type=individual`, `to` in international digits, `type=template`, `template{name, language{code}, components}`. The components are a body text parameter, plus a `button` / `sub_type=url` / `index="0"` text parameter for copy-code. This matches the Meta authentication-template send as documented, to the best of my knowledge.
  - Resend: `POST https://api.resend.com/emails`, Bearer key, `{from, to[], subject, html, text}`, and an `Idempotency-Key` header, which Resend supports. The one uncertainty is User-Agent (note 3).
  - Timeouts come from config: attempt 10 s (at most 15, the circuit-breaker sampling constraint), total 30 s.
  - Timeouts and an open circuit map to 503 `OTP_DELIVERY_FAILED`. Caller cancellation propagates.
- **Defaults and startup:**
  - An enabled real provider with missing credentials fails `ValidateOnStart` (row 47). Disabled or Fake channels are not checked.
  - SMS is disabled in the example config, ApiFactory, and the code default. `Provider` defaults to `Fake` (enum 0) everywhere. Build-time OpenAPI reads `appsettings.example.json`.
- **Plan items:**
  - Files 1-36 exist with the planned signatures.
  - `ISmsSender`, `FakeSmsSender`, `SmsOptions`, `SmsProvider` and `RecordingSmsSender` are deleted. No `Sms:Provider` / `Sms__Provider` references remain.
  - `GenerateOTPHandler` sends before it saves and returns `Channel`.
  - Recipient-type guards are in all three auth handlers.
  - The migration is a single `AddColumn` with `defaultValue: "Phone"`, with no Rename or Drop. The snapshot maps `Recipient` to the `PhoneNumber` column, and the migration is added to `AppDbContextTests`.
  - `openapi/v1.json` and the Orval client are regenerated (`login/email-code`, `OtpChannel` enum).
  - The 5 resx keys and the 5 web error keys are present in both languages.
- **Test plan:** all 82 rows are present by name. The API rows were checked by grep; the web rows were read.
- **Docs sync:**
  - PRD section 7.1 matches the code (sign-up and sign-in paths, routing, 503 codes).
  - New `docs/otp-delivery.md` matches the options, validator rules and retry policy.
  - README row and fake note, `.env.example`, and `docs/backlog.json` (E1 story, the 10 sub-tasks verbatim from issue #171) are in place.
  - No UI doc covers auth screens (`claude-design-prompt.md`, `prototype.md`, `prototype/app.js`), so there is no divergence there.
  - Constitution section 0.4 (credential-less providers run behind a Fake) still holds.
- **Postman:** the `SendOtp` description is updated. `SendEmailOtp` (POST `/api/auth/otp/send`, noauth, `{ email }`) and `LoginWithEmailCode` (POST `/api/auth/login/email-code`, noauth, `{ verificationId }`) are added. No request is stale or orphaned.
- **Not independently re-verified:** "zero new build warnings". My Release build was incremental and printed no warnings.

## Test quality
- `GenerateOTPHandlerTests`: constrains the code. The sender stub returns `Sms`, not the natural default `WhatsApp`, so a hard-coded channel fails. The normalisation test uses mixed case and whitespace. The save-not-called check on the delivery-failure path pins send-before-save.
- `GenerateOTPValidatorTests`, `LoginWithEmailCodeValidatorTests`: every new rule has a failing case.
- `LoginWithEmailCodeHandlerTests`: every throw is covered with type, code and `SaveChangesAsync` DidNotReceive. The success path has `Received(1)`.
- `LoginWithPhone` / `RegisterWithPhone` `Handle_EmailOtp_ThrowsOtpInvalid`: constrain the new guard.
- `OtpChannelRouterTests`: constrain the code. Every routing branch has both positive and negative `Received` checks.
- `OtpDeliveryOptionsValidatorTests`: constrain the code. Real providers with credentials missing, disabled real providers, and each SMS rule are covered.
- `OtpDeliveryServiceCollectionExtensionsTests`: provider switching and startup failure are constrained. Resilience wiring is not (note 1).
- `MetaWhatsAppOtpChannelTests`, `ResendEmailOtpChannelTests`, `HttpSmsOtpChannelTests`, `HttpSmsBodyRendererTests`, `PhoneNumberFormatterTests`: the stub handler checks the real request shape, URI, headers, and escaping. The `Contain("5")` minutes check is weak but not vacuous: the template has no other "5".
- Integration (`OtpEndpointTests`, `EmailCodeAuthEndpointTests`, `PhoneAuthEndpointTests`): the real router runs, and status plus `code` are asserted. The 503 test checks that no OTP row was written.
- Web `LoginPage.test.tsx`: the channel-specific copy and error paths are constrained. Request bodies are not (note 9).
- No vacuous tests found.
