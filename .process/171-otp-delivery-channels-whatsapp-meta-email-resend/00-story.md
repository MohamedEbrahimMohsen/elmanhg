# [E1.S6] OTP delivery channels: WhatsApp (Meta), Email (Resend), SMS (disabled)

Issue: #171

As a student I receive my sign-in code on WhatsApp (or email) so that I can log in without an SMS gateway. PRD §7.1.

Epic: #53 · Follows #56 (authentication). Dev decision, 2026-09-28.

### Channels
- **WhatsApp — enabled.** Meta WhatsApp Cloud API (Graph API, approved authentication template). The dev adds credentials later.
- **Email — enabled.** Resend.com (`POST https://api.resend.com/emails`). The dev adds the API key later.
- **SMS — built but disabled.** A generic HTTP adapter for a local Egyptian telecom, still to be chosen. It is off by config and can be turned on later without code changes.

### Sub-tasks
- [ ] Replace `ISmsSender` with a channel-agnostic `IOtpSender` / `IOtpChannel` abstraction: `WhatsApp`, `Sms`, `Email`
- [ ] Config section `OtpDelivery`: per-channel `Enabled`, `Provider` (`Fake` | real), credentials, plus the default phone channel (WhatsApp). Startup validation: an enabled real provider must have its credentials.
- [ ] Meta WhatsApp Cloud API adapter (typed HttpClient, template message with the code, error mapping, retries/timeouts)
- [ ] Resend email adapter (typed HttpClient, Arabic RTL HTML template + plain-text part)
- [ ] Generic HTTP SMS adapter (configurable URL, auth header, body template), disabled by default
- [ ] Fakes remain the default in Development, tests and CI, and when no credentials are set
- [ ] Phone OTP goes to WhatsApp; if WhatsApp is disabled, it falls back to SMS when that is enabled; otherwise it returns a clear 503 error code
- [ ] Frontend: OTP screen says "sent via WhatsApp" / "sent to your email" based on the channel returned by the API
- [ ] Update `docs/PRD.md` §7.1, `appsettings.example.json`, the deployment runbook, Postman
- [ ] Tests: adapter request shapes via a stub HTTP handler, channel routing, disabled-channel behaviour, startup validation

_Opened by the autopilot run on the dev's instruction._

