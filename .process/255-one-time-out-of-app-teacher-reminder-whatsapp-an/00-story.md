# [E18.S3] One-time out-of-app teacher reminder (WhatsApp and email)

Issue: #255

Epic: #252

As a teacher I get one reminder outside the app, on WhatsApp and by email, when a student question in my subjects is close to its SLA deadline. That way I don't miss it when I'm not logged in.

Dev decision on #222 (d): "one-time reminder on both WhatsApp & Email is better, but this also has to be configured."

**Rules**
- **Once per thread:** exactly one out-of-app reminder is sent per thread, even across repeated sweeps and restarts. It goes out at a configurable stage (default: the second reminder).
- **Recipient:** the teacher who claimed the thread. If nobody has claimed it, every teacher assigned to the subject.
- **Settings** (on the Configuration page): enabled on or off, channels (WhatsApp, email, or both), and the stage that triggers it.
- **Channels:** it reuses the existing messaging adapters (Meta WhatsApp, Resend email), with fakes by default.
  - WhatsApp needs its own **utility template** approved by Meta, with a configurable name and language. Getting it approved is a go-live item for the dev.
  - Email uses an ar/en template.
- **In-app reminders** (SignalR plus the inbox card) stay as they are.
- **Failures:** if one channel fails, the other still sends and the sweep does not fail. Each attempt is logged without contact details or tokens.

### Sub-tasks
- [ ] A notification channel abstraction for non-OTP messages (WhatsApp template message, email) that reuses the #171 adapters and config
- [ ] Send the one-time reminder from the SLA sweep, with an idempotency marker on the thread
- [ ] Settings and Configuration page entries; the fake channel logs in Development
- [ ] Tests: sent only once, claimed versus unclaimed recipients, one channel failing without affecting the other
- [ ] Docs: the PRD section on Ask a Teacher reminders, plus the new template and variables in docs/otp-delivery.md or a messaging doc

