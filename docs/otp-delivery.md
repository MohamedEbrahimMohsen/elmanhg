# OTP delivery

Sign-in codes (PRD §7.1) are sent through one of three **channels**: WhatsApp (Meta WhatsApp Cloud API), Email (Resend) and SMS (a generic HTTP gateway for a local telecom). `POST /api/auth/otp/send` takes `{ "phoneNumber": "010…" }` or `{ "email": "…" }` (exactly one; otherwise 422 `OTP_RECIPIENT_REQUIRED`) and returns the channel it used in `channel` (`WhatsApp`, `Sms` or `Email`). The code screen names that channel.

Email codes sign in existing **student** email accounts through `POST /api/auth/login/email-code { verificationId }`. Teachers and admins get 403 `EMAIL_CODE_SIGN_IN_NOT_ALLOWED` and keep email and password. Phone sign-in and registration reject an email-verified code (400 `OTP_INVALID`), and the email-code sign-in rejects a phone-verified one.

## 1. Channels and routing

| Recipient | Channel order | Nothing enabled |
|---|---|---|
| Phone | `OtpDelivery:DefaultPhoneChannel` (WhatsApp) if enabled, then the other phone channel (SMS) if enabled | 503 `OTP_CHANNEL_UNAVAILABLE` |
| Email | Email if enabled | 503 `OTP_CHANNEL_UNAVAILABLE` |

The code is delivered **before** the OTP row is saved. If delivery fails, nothing is stored and the 60 s resend cooldown does not start. Any provider failure (a non-2xx response, a transport error, a timeout or an open circuit) returns 503 `OTP_DELIVERY_FAILED`. There is no fallback from WhatsApp to SMS on a runtime failure: the fallback applies only when WhatsApp is disabled. Logs name the channel and the HTTP status only, never the recipient, the code or the provider's response body.

## 2. Configuration reference

All keys live under `OtpDelivery` (environment variables use `__`, for example `OtpDelivery__WhatsApp__Provider`). The committed defaults are in `api/Elmanhg.Api/appsettings.example.json`. **Secrets** go in environment variables only, never in a committed file.

| Key | Default | Secret | Notes |
|---|---|---|---|
| `DefaultPhoneChannel` | `WhatsApp` | no | `WhatsApp` or `Sms` |
| `CountryCallingCode` | `20` | no | Replaces the leading `0` of a local number for WhatsApp and `{internationalPhoneNumber}` |
| `AttemptTimeoutSeconds` | `10` | no | 1–15, per HTTP attempt |
| `TotalTimeoutSeconds` | `30` | no | 1–120, across retries |
| `WhatsApp:Enabled` | `true` | no | |
| `WhatsApp:Provider` | `Fake` | no | `Fake` or `Meta` |
| `WhatsApp:BaseUrl` | `https://graph.facebook.com` | no | |
| `WhatsApp:ApiVersion` | `v23.0` | no | Graph API version |
| `WhatsApp:PhoneNumberId` | empty | no | The WhatsApp Business phone number id |
| `WhatsApp:AccessToken` | empty | **yes** | System-user permanent token |
| `WhatsApp:TemplateName` | empty | no | The approved authentication template |
| `WhatsApp:LanguageCode` | `ar` | no | The template language |
| `WhatsApp:CopyCodeButton` | `true` | no | Adds the button component a copy-code template needs |
| `Email:Enabled` | `true` | no | |
| `Email:Provider` | `Fake` | no | `Fake` or `Resend` |
| `Email:BaseUrl` | `https://api.resend.com` | no | |
| `Email:ApiKey` | empty | **yes** | Resend sending-only key |
| `Email:FromAddress` | empty | no | For example `Elmanhg <otp@your-domain>` |
| `Email:Subject` | `رمز الدخول إلى المنهج` | no | |
| `Sms:Enabled` | `false` | no | |
| `Sms:Provider` | `Fake` | no | `Fake` or `Http` |
| `Sms:Url` | empty | no | The gateway endpoint |
| `Sms:AuthHeaderName` | empty | no | For example `X-Api-Key` or `Authorization` |
| `Sms:AuthHeaderValue` | empty | **yes** | |
| `Sms:ContentType` | `application/json` | no | `application/json` or `application/x-www-form-urlencoded` |
| `Sms:BodyTemplate` | empty | no | Tokens: `{phoneNumber}`, `{internationalPhoneNumber}`, `{message}` |
| `Sms:MessageTemplate` | `رمز الدخول إلى المنهج: {code}` | no | `{code}` is replaced by the code |

The email body is always Arabic (RTL HTML plus a plain-text part). Its templates are embedded in `Elmanhg.Infrastructure/OtpDelivery/Email/Templates/`, and the expiry shown is `CoreOtp:ExpirationMinutes`. `CoreOtp:EmailMaxLength` (256) caps the email a code can be requested for.

## 3. Fakes

`Provider=Fake` is the default for every channel in the committed config, the test host and the build-time OpenAPI run, so development, tests and CI need no keys. `FakeOtpChannel` delivers nothing: in Development it writes each code to the API console, and outside Development it logs a warning on every send. A channel that is enabled with `Provider=Fake` still counts as enabled for routing.

## 4. Startup validation

`OtpDeliveryOptionsValidator` runs at startup (`ValidateOnStart`), so a bad configuration stops the boot:

- `DefaultPhoneChannel` is required and must be `WhatsApp` or `Sms`; `CountryCallingCode` is 1–4 digits; `AttemptTimeoutSeconds` must not exceed `TotalTimeoutSeconds`.
- An **enabled real provider** must have all its settings: Meta needs `BaseUrl`, `ApiVersion`, `PhoneNumberId`, `AccessToken`, `TemplateName` and `LanguageCode`; Resend needs `BaseUrl`, `ApiKey`, `FromAddress` and `Subject`; Http SMS needs `Url`, `ContentType`, `BodyTemplate` and `MessageTemplate`. Every URL must be absolute `https`.
- Http SMS also needs a supported `ContentType`, a `BodyTemplate` with `{message}` and a phone token, a `MessageTemplate` with `{code}`, and `AuthHeaderName` and `AuthHeaderValue` set together.
- A disabled or `Fake` channel is never checked for credentials. A disabled channel is also never built: it resolves to the fake adapter, so its unchecked settings (a blank `BaseUrl`, for example) cannot break sends on the enabled channels. There is no silent fallback to `Fake`: choosing a real provider without its keys fails the boot.

## 5. Going live: WhatsApp (Meta)

1. Create a Meta app with the WhatsApp product and a WhatsApp Business Account; register and verify the sending number, and note its **phone number id**.
2. Create an **AUTHENTICATION** message template in Arabic (`ar`) with a **copy-code** button, and wait for approval.
3. Create a system user with a **permanent token** that has the `whatsapp_business_messaging` permission on the account.
4. Set `OtpDelivery__WhatsApp__Provider=Meta`, `OtpDelivery__WhatsApp__PhoneNumberId`, `OtpDelivery__WhatsApp__AccessToken` and `OtpDelivery__WhatsApp__TemplateName`. Set `OtpDelivery__WhatsApp__CopyCodeButton=false` if the template has no button.

The adapter posts to `{BaseUrl}/{ApiVersion}/{PhoneNumberId}/messages` with a Bearer token and a `template` message whose body parameter (and button parameter) is the code. The recipient is sent in international form (`201012345678`).

## 6. Going live: Email (Resend)

1. Add and verify the sending domain in Resend (the DNS records it lists).
2. Create an API key with **sending access** only.
3. Set `OtpDelivery__Email__Provider=Resend`, `OtpDelivery__Email__ApiKey` and `OtpDelivery__Email__FromAddress` (an address on the verified domain).

The adapter posts to `{BaseUrl}/emails` with a Bearer key.

## 7. Enabling SMS (generic HTTP gateway)

Set `OtpDelivery__Sms__Enabled=true`, `OtpDelivery__Sms__Provider=Http`, `OtpDelivery__Sms__Url`, `OtpDelivery__Sms__ContentType`, `OtpDelivery__Sms__BodyTemplate` and, when the gateway needs one, `OtpDelivery__Sms__AuthHeaderName` and `OtpDelivery__Sms__AuthHeaderValue`. With WhatsApp still enabled, SMS is only the fallback; set `OtpDelivery__DefaultPhoneChannel=Sms` to prefer it.

Token values are JSON-escaped for `application/json` and URL-encoded for `application/x-www-form-urlencoded`.

JSON example:

```
OtpDelivery__Sms__ContentType=application/json
OtpDelivery__Sms__BodyTemplate={"to":"{internationalPhoneNumber}","text":"{message}"}
```

Form example:

```
OtpDelivery__Sms__ContentType=application/x-www-form-urlencoded
OtpDelivery__Sms__BodyTemplate=to={phoneNumber}&msg={message}
```

## 8. Retries and timeouts

Each adapter is a typed `HttpClient` with the standard resilience handler (`Microsoft.Extensions.Http.Resilience`): retry, circuit breaker, a per-attempt timeout (`AttemptTimeoutSeconds`) and a total timeout (`TotalTimeoutSeconds`). **Resend** requests are retried, because every request carries a fresh `Idempotency-Key` header. **Meta and SMS** requests are POSTs without an idempotency key, so retries are disabled for them, and a failed send returns 503 `OTP_DELIVERY_FAILED` for the user to resend. Every provider request carries `User-Agent: Elmanhg/1.0`.

## 9. Deployment

On the host, set the `OtpDelivery__*` variables for every channel you switch on (section 2), with the secrets from the host's secret store. The production runbook (#112) must link this page.
