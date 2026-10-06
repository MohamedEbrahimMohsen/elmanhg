# [E21.S5] Core.Http and Core.Messaging: outbound HTTP, email/WhatsApp/SMS clients, OTP channels

Issue: #308

Promote the outbound HTTP plumbing and the message transports; give `Core.OTP` real channel implementations.

- New `Core.Http`: `IsTransientFailure(exception, ct)` (replaces 10 copies), `SendJsonAsync<TRequest,TResponse>(..., errorCode)` returning or throwing `ServiceUnavailableCoreException`, `AddTimeoutResilience(attempt, total, retryUnsafe)` wrapping `AddStandardResilienceHandler` (8 copies), base-address normalisation (13 `TrimEnd('/')` copies), a configurable User-Agent (today the constant lives in the OTP namespace and AI/Paymob reach into it).
- New `Core.Messaging`: transport clients `ResendEmailClient` (replaces 3 copies of the Resend request builder incl. idempotency headers), `MetaWhatsAppClient` (2 copies), `HttpSmsClient` + `HttpSmsBodyRenderer`, `PhoneNumberFormatter`. App-specific error codes, metrics and templates are passed in.
- `Core.OTP` channel adapters on top of Core.Messaging: WhatsApp, email, SMS, fake, and the preferred-channel-with-fallback router (`OtpChannelRouter`), with `OtpDeliveryOptions` + validator. The app keeps its templates, error codes (`OtpDeliveryFailed`, `OtpChannelUnavailable`) and metrics via options/callbacks.
- App: the 6 AI HTTP clients share one base using Core.Http; one shared AI JSON options instance instead of 5 copies; one Fake/Http provider-switch registration helper instead of ~10 copies.
- Behaviour, endpoints, payload shapes and config keys unchanged.

### Sub-tasks
- [ ] Core.Http, with tests
- [ ] Core.Messaging clients, with tests
- [ ] Core.OTP channels and router; app behaviour unchanged
- [ ] AI client base, shared JSON options, provider-switch helper
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

