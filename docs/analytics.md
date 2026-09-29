# Sign-up funnel analytics

First-party events that measure the PRD §2.1 goal 1: a new visitor goes from the landing page to a first graded quiz answer in under 3 minutes. The events are written here; the read side (the "Sign-up funnel" dashboard row, PRD §10.3) is built by #104 (dashboard metrics queries) and #105 (dashboard UI).

There is no third-party tracker. Nothing leaves the platform, and no credentials are needed.

## Events

| Type | Emitted where | Once only? |
|---|---|---|
| `LandingViewed` | The landing page (`/`) mounts. | No |
| `SignUpStarted` | The sign-up page (`/signup`) mounts. | No |
| `SignUpCompleted` | A phone or email registration succeeds, before the session starts. | No |
| `OnboardingCompleted` | The first save of subject interests (or «تخطّي الآن»), only while the student still needs onboarding. Later edits send nothing. | By state |
| `FirstQuizAnswered` | A quiz answer is graded successfully. | Yes, per browser (`localStorage['elmanhg.funnel.FirstQuizAnswered']`) |

Duplicates are harmless: the funnel counts distinct visitors per step.

## Anonymous id

The browser keeps a random `crypto.randomUUID()` in `localStorage['elmanhg.anonymousId']` and sends it with every event. It is not derived from the device, the user or any fingerprint, and it holds no personal data. Clearing site data starts a new id.

## Endpoint

`POST /api/analytics/funnel-events`, anonymous (`[AllowAnonymous]`).

```json
{ "anonymousId": "3f1c2b1e-8a57-4f0e-9a1b-2c3d4e5f6a7b", "type": "LandingViewed" }
```

- The body is only a GUID and a whitelisted event name. There is no free text and no PII.
- `anonymousId` is required and must not be the empty GUID (422 `FUNNEL_ANONYMOUS_ID_REQUIRED`).
- `type` must be one of the five names above (422 `FUNNEL_EVENT_TYPE_INVALID`).
- It is rate limited per client IP with a fixed window: `Analytics:FunnelEventPermitLimit` requests per `Analytics:FunnelEventWindowSeconds` (code defaults 60 per 60 s). Over the limit it returns 429 `TOO_MANY_REQUESTS`.
- The web client is fire-and-forget: a failed request is swallowed and never blocks or breaks the journey.

## Storage

Table `FunnelEvents`: `Id`, `AnonymousId`, `UserId?`, `Type`, `OccurredAt`, plus soft-delete columns.

- `OccurredAt` is stamped by the server clock, so a client cannot backdate an event.
- `UserId` is filled from the bearer token when the caller is signed in (for example `OnboardingCompleted`, `FirstQuizAnswered`), and null otherwise.
- Indexes: (`Type`, `OccurredAt`) for date-range counts, and `AnonymousId` for per-visitor timing.

## Funnel definition (for #104)

For a date range:

- **Step count**: distinct `AnonymousId` per `Type`, in the order LandingViewed → SignUpStarted → SignUpCompleted → OnboardingCompleted → FirstQuizAnswered.
- **Conversion**: step count ÷ previous step count.
- **Time to first answer**: per `AnonymousId`, seconds from its first `LandingViewed` to its first `FirstQuizAnswered`; report the median. The goal is under 180 s.
