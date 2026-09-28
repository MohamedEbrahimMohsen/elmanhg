# Implementation — OTP delivery channels: WhatsApp (Meta), Email (Resend), SMS (disabled) (#171, E1.S6)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.OTP/Delivery/OtpChannel.cs` | 3 | `WhatsApp, Sms, Email` |
| `api/core-libraries/Core.OTP/Delivery/IOtpChannel.cs` | 8 | One delivery channel |
| `api/core-libraries/Core.OTP/Delivery/IOtpSender.cs` | 8 | Routes and delivers, returns channel used (replaces `ISmsSender`) |
| `api/core-libraries/Core.OTP/Entities/OtpRecipientType.cs` | 3 | `Phone, Email` |
| `api/Elmanhg.Application/Auth/LoginWithEmailCode/LoginWithEmailCodeCommand.cs` | 6 | Command |
| `api/Elmanhg.Application/Auth/LoginWithEmailCode/LoginWithEmailCodeValidator.cs` | 14 | Validator |
| `api/Elmanhg.Application/Auth/LoginWithEmailCode/LoginWithEmailCodeHandler.cs` | 48 | Student-only email-code sign-in |
| `api/Elmanhg.Infrastructure/OtpDelivery/OtpDeliveryOptions.cs` | 32 | `OtpDelivery` section |
| `api/Elmanhg.Infrastructure/OtpDelivery/OtpDeliveryOptionsValidator.cs` | 100 | Startup validation (exact messages from the plan) |
| `api/Elmanhg.Infrastructure/OtpDelivery/OtpChannelRouter.cs` | 44 | `IOtpSender`: Decision 7 routing, 503 `OTP_CHANNEL_UNAVAILABLE` |
| `api/Elmanhg.Infrastructure/OtpDelivery/FakeOtpChannel.cs` | 24 | Default channel (replaces `FakeSmsSender`) |
| `api/Elmanhg.Infrastructure/OtpDelivery/OtpProviderHttpExtensions.cs` | 33 | Failure mapping to 503 `OTP_DELIVERY_FAILED` |
| `api/Elmanhg.Infrastructure/OtpDelivery/PhoneNumberFormatter.cs` | 8 | Local → international |
| `api/Elmanhg.Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensions.cs` | 63 | `AddOtpDelivery` (typed clients + resilience + provider switches); `AddOtpResilience` fit, no extra file |
| `api/Elmanhg.Infrastructure/OtpDelivery/WhatsApp/{WhatsAppOtpOptions,WhatsAppProvider,MetaWhatsAppOtpChannel,MetaWhatsAppTemplateMessage}.cs` | 14/3/26/34 | Meta adapter |
| `api/Elmanhg.Infrastructure/OtpDelivery/Email/{EmailOtpOptions,EmailProvider,ResendEmailOtpChannel,ResendEmailMessage,OtpEmailTemplate}.cs` | 11/3/30/5/29 | Resend adapter |
| `api/Elmanhg.Infrastructure/OtpDelivery/Email/Templates/OtpEmail.html`, `OtpEmail.txt` | 26/6 | Arabic RTL embedded templates |
| `api/Elmanhg.Infrastructure/OtpDelivery/Sms/{SmsOtpOptions,SmsProvider,HttpSmsOtpChannel,HttpSmsBodyRenderer}.cs` | 13/3/31/20 | Generic HTTP SMS adapter |
| `api/Elmanhg.Infrastructure/Migrations/20260928223838_AddOtpRecipientType.cs` (+ `.Designer.cs`) | 30 | One `AddColumn` with `defaultValue: "Phone"`; no Rename/Drop in `Up` |
| `api/Elmanhg.Tests/Integration/Infrastructure/OtpOutbox.cs`, `RecordingOtpChannel.cs` | 29/14 | Integration delivery recorder |
| `api/Elmanhg.Tests/Infrastructure/OtpDelivery/StubHttpMessageHandler.cs`, `OtpDeliveryTestSettings.cs` | 29/84 | Unit-test support |
| `api/Elmanhg.Tests/Infrastructure/OtpDelivery/*Tests.cs` (8 classes) | — | Test plan rows 24–64 |
| `api/Elmanhg.Tests/Application/Features/Auth/LoginWithEmailCode/*Tests.cs` | 123/26 | Rows 13–21 |
| `api/Elmanhg.Tests/Integration/Auth/EmailCodeAuthEndpointTests.cs` | 75 | Rows 70–73 |
| `web/src/features/session/components/EmailSignIn.tsx`, `EmailCodeStartForm.tsx`, `EmailCodeSignIn.tsx` | 25/53/35 | Email tab password/code toggle and code flow |
| `web/src/features/session/schemas/emailCodeStartSchema.ts` (+ `.test.ts`) | 6/14 | Schema + rows 81–82 |
| `web/src/shared/api/generated/model/otpChannel.ts`, `loginWithEmailCodeCommand.ts` | gen | Orval output |
| `docs/otp-delivery.md` | 108 | Config reference, fakes, validation, go-live, retries, deployment |

## Files modified
| Path | Change |
|---|---|
| `api/core-libraries/Core.Errors/Exceptions.cs` | + `ServiceUnavailableCoreException` (503) |
| `api/core-libraries/Core.OTP/Entities/OTP.cs` | `PhoneNumber` → `Recipient`, + `RecipientType`, `Create(recipientType, recipient, …)` |
| `api/core-libraries/Core.OTP/GenerateOTP/*` | Command `(string? PhoneNumber, string? Email = null)`; result + `Channel`; validator rules; handler sends **before** `SaveChangesAsync` |
| `api/core-libraries/Core.OTP/{OtpOptions,Exceptions/ErrorCodes,Repositories/IOtpRepository}.cs` | `EmailMaxLength = 256`; 4 codes; param rename |
| `api/core-libraries/Core.OTP/Sms/ISmsSender.cs` | Deleted (staged `git rm`) |
| `api/core-libraries/Core.EntityFrameworkCore/{Context/CoreDbContext,Repositories/OtpRepository}.cs` | `Recipient` → column `PhoneNumber` (WHY comment), `RecipientType` string(50); predicate |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | 4 AUTH codes |
| `api/Elmanhg.Application/Auth/{LoginWithPhone,RegisterWithPhone}/*Handler.cs` | Reject non-Phone OTPs with `OTP_INVALID`; `otp.Recipient` |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | Sms wiring removed, `services.AddOtpDelivery()` first |
| `api/Elmanhg.Infrastructure/Sms/*` | Deleted (3 files, staged) |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj`, `api/Directory.Packages.props` | Resilience package + embedded templates |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated |
| `api/Elmanhg.Api/Controllers/Auth/AuthController.cs` | `LoginWithEmailCode` action (`login/email-code`, anonymous, Credentials rate limit) |
| `api/Elmanhg.Api/Resources/Messages.{en,ar}.resx` | 5 keys each |
| `api/Elmanhg.Api/appsettings.example.json` | `Sms` removed; `CoreOtp.EmailMaxLength`; `OtpDelivery` block (all Fake) |
| `api/openapi/v1.json` | Regenerated by `dotnet build` |
| `api/Elmanhg.Tests/...` | `OtpBuilder` (`ForEmail`), `ApiFactory` (OtpOutbox + 3 `RecordingOtpChannel`, OtpDelivery rows), `AuthTestClient` (email helpers), `RecordingSmsSender.cs` deleted, tests per plan, `AppDbContextTests` migration list + `_AddOtpRecipientType` |
| `web/src/features/session/components/{OtpForm,PhoneStartForm,PhoneSignIn,PhoneSignUp}.tsx`, `pages/LoginPage.tsx` | Channel-aware `OtpForm` contract; `EmailSignIn` on the Email tab |
| `web/src/features/session/i18n/{en,ar}.json`, `web/src/shared/i18n/{en,ar}.json` | `otp.sentTo` removed; 3 otp + 3 action keys; 5 error keys |
| `web/src/features/session/pages/LoginPage.test.tsx` | Row 75 modified, rows 76–80 added |
| `web/src/shared/api/generated/**` | `npm run gen:api` |
| `docs/PRD.md` §7.1, `docs/backlog.json` (E1 story), `README.md`, `.env.example`, `postman/elmanhg.postman_collection.json` | Per the plan's Docs section |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `Microsoft.Extensions.Http.Resilience` **10.10.0** | Orchestrator: prefer the version matching the repo's Microsoft.Extensions packages. 10.10.0 depends on the 10.0.12 runtime band (ObjectPool 10.0.12); **10.7.0** depends on 10.0.9, matching `Microsoft.AspNetCore.OpenApi` / `ApiDescription.Server` 10.0.9. | Pinned **10.7.0** (exists: `dotnet package search … --exact-match` lists 10.7.0 and 10.10.0; published 2026-06-09; MIT; owner Microsoft, repo dotnet/extensions; `dotnet list package --vulnerable --include-transitive` clean; not deprecated). |
| Row 47: `IStartupValidator.Validate()` throws `OptionsValidationException` (message contains `AccessToken`) | The standard resilience handler registers its own options with `ValidateOnStart`; its `Configure` callback reads `OtpDeliveryOptions`, so startup fails with an `AggregateException` of 4 identical `OptionsValidationException`s. `.Which` cannot pick one. | Asserted `act.Should().Throw<OptionsValidationException>().WithMessage("*AccessToken*")`. Same behaviour, the boot still fails. |
| Web `request={{ email: start.email }}` and `sendOtp.mutateAsync({ data: { email } })` | The generated `GenerateOTPCommand` has `phoneNumber: string \| null` as **required** because the C# positional `string? PhoneNumber` has no default (the plan's signature is kept). | The web sends `{ phoneNumber: null, email }` in `EmailCodeStartForm` and `EmailCodeSignIn`. The API is unchanged; `{ email }` alone still binds server-side. |
| `HttpSmsOtpChannel` replaces the literal `"{code}"` | The validator needs the same token, and the plan bans magic values. | Added `public const string CodeToken = "{code}"` on `HttpSmsOtpChannel`, used by the channel and the validator. |
| Postman bodies use `{{email}}` | The collection had no `email` variable. | Added collection variable `email` = `mona@elmanhg.local` (the RegisterWithEmail student), gave both new requests an item description, and placed them after `LoginWithEmail`. |

## Build & test
- `dotnet build` (api): **Build succeeded**, and there are no new warnings. `CS8618` on `Otp.Recipient` replaces the same pre-existing warning on `PhoneNumber`.
- `dotnet ef migrations add AddOtpRecipientType …`: done. `defaultValue` was edited from `""` to `"Phone"`.
- `dotnet test api/` (Debug, Docker up): `total: 1986, failed: 0, succeeded: 1986, skipped: 0`.
- **CI parity** `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside: `total: 1986, failed: 0, succeeded: 1986, skipped: 0`. The file was restored afterwards.
- Test counts: API plan rows 1–74 are all present by name. That is 71 new methods plus 3 modified (rows 1, 3, 65). Web plan rows 75–82 are all present (1 modified, 7 new).
- `dotnet format --verify-no-changes --exclude core-libraries`: exit 0, no output. The touched core-library files are clean except for the known whitespace-only noise.
- `dotnet list package --vulnerable --include-transitive`: no vulnerable packages in any project.
- `npm --prefix web run gen:api`: regenerated. `npm run typecheck`: exit 0. `npm run lint`: exit 0 (max-warnings 0). `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: "All matched files use Prettier code style!" (3 new files were formatted with `--write` first). `npm test -- --run`: **Test Files 104 passed, Tests 648 passed**. `npm run build`: exit 0.
- **Mutation checks.** Each mutation was reverted afterwards, and each was caught by the listed test(s):
  - Router fallback removed → `…FallsBackToSms`
  - Email-enabled check forced → `…EmailWithEmailDisabled…`
  - Recipient-type guards removed in 3 handlers → both `Handle_EmailOtp_ThrowsOtpInvalid` and `Handle_PhoneOtp_ThrowsOtpInvalid`
  - Student-role check removed → `Handle_StaffAccount_ThrowsForbidden`
  - Save moved before send → `Handle_DeliveryUnavailable_ThrowsAndSavesNothing`
  - Email normalisation removed → `Handle_NewEmail_…`
  - Caller-cancel filter removed → `SendAsync_CallerCancels_…`
  - JSON escaping removed → `Render_Json_…`
  - AccessToken requirement removed → validator row 32 and DI row 47
  - Web: SMS message key swapped → row 76; resent verification id dropped → existing "sends a new code…" test

## Notes for review
- **ApiFactory placement.** The `OtpDelivery` rows sit in the `AddInMemoryCollection` block, where the `Sms:Provider` row was (per the plan), not in `UseSetting`. `OtpDeliveryOptions` binds lazily (`BindConfiguration`) and is not read while `Program` registers services, so this is correct. The Release run without `appsettings.json` is green.
- **Guard grep.** `new HttpClient(` appears only in the new unit tests, where it wraps `StubHttpMessageHandler`, as `conventions/dotnet-testing.md` prescribes. Production code uses typed clients only. The skill §9 guard grep will print those test lines once they are tracked.
- **URL checks.** `RequireHttps` also fires on an empty URL, next to the "is required" message, as the plan's literal rule says.
- **Resilience validation.** The resilience handler validates its own options at startup. With the committed 10/30 s values this passes, and the app boots in every integration test and in the build-time OpenAPI run.
- **Staged deletions.** `git rm` staged the deletions of `ISmsSender.cs`, `Infrastructure/Sms/*` and `RecordingSmsSender.cs`. Nothing is committed.
- **#137.** Not referenced with a closing keyword anywhere.
- **Local `api/Elmanhg.Api/appsettings.json` (gitignored).** The `OtpDelivery` section (all Fake) was appended. That local file has no `CoreOtp`, `Auth`, `Sessions`, `Exams`, … sections at all; that gap predates this story. `CoreOtp.EmailMaxLength` has a code default of 256, so it was not added. **Dev action:** mirror the `OtpDelivery` section (and `CoreOtp.EmailMaxLength` if you keep a `CoreOtp` block) into your own local `appsettings.json`.
