# Implementation — One-time out-of-app teacher reminder (WhatsApp and email) · #255 [E18.S3]

Worktree `D:/Personal/elmanhg-wt/255`, branch `feature/255-one-time-out-of-app-teacher-reminder-whatsapp-an` on `origin/main` a91848e (#253 merged). Nothing committed.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/TeacherThreads/TeacherThreadOutOfAppReminder.cs` | 29 | Append-only marker entity, `Record(...)` with Breach guard |
| `api/Elmanhg.Domain/TeacherThreads/ITeacherThreadOutOfAppReminderRepository.cs` | 8 | `IsRecordedAsync` |
| `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadOutOfAppReminderRepository.cs` | 17 | Repository |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.OutOfAppReminders.cs` | 19 | DbSet + config (FK restrict, unique `ThreadId`) |
| `api/Elmanhg.Application/Shared/Messaging/MessageChannel.cs` | 3 | enum |
| `api/Elmanhg.Application/Shared/Messaging/IMessageChannel.cs` | 9 | port (one-line doc comment: never throws except cancellation) |
| `api/Elmanhg.Application/Shared/Messaging/OutboundMessage.cs` | 3 | abstract record |
| `api/Elmanhg.Application/Shared/Messaging/TeacherThreadReminderMessage.cs` | 3 | message |
| `api/Elmanhg.Application/Shared/RuntimeSettings/Definitions/OutOfAppReminderRuntimeSettings.cs` | 26 | 3 runtime settings (Boolean/Choice/Choice), no constraints |
| `api/Elmanhg.Application/TeacherThreads/ProcessTeacherThreadSla/OutOfAppTeacherReminder.cs` | 63 | `ClaimAsync` / `SendAsync` helper |
| `api/Elmanhg.Infrastructure/Messaging/OutOfAppReminderOptions.cs` | 34 | options (`EnglishLanguage` const) |
| `api/Elmanhg.Infrastructure/Messaging/OutOfAppReminderOptionsValidator.cs` | 25 | time zone + https link validation |
| `api/Elmanhg.Infrastructure/Messaging/MessagingServiceCollectionExtensions.cs` | 30 | `AddMessaging`, `UsesMeta` |
| `api/Elmanhg.Infrastructure/Messaging/MessagingHttpExtensions.cs` | 30 | send → bool, never throws (WHY comment) |
| `api/Elmanhg.Infrastructure/Messaging/FakeMessageChannel.cs` | 24 | fake |
| `api/Elmanhg.Infrastructure/Messaging/MetaWhatsAppMessageChannel.cs` | 31 | Meta utility template adapter |
| `api/Elmanhg.Infrastructure/Messaging/ResendEmailMessageChannel.cs` | 35 | Resend adapter, deterministic idempotency key |
| `api/Elmanhg.Infrastructure/Messaging/TeacherReminderEmailTemplate.cs` | 39 | ar/en render, HTML-encoded |
| `api/Elmanhg.Infrastructure/Messaging/ReminderDeadlineFormatter.cs` | 10 | `yyyy-MM-dd HH:mm` in zone |
| `api/Elmanhg.Infrastructure/Messaging/Templates/TeacherReminderEmail.{ar,en}.{html,txt}` | 28/8/28/8 | email templates (embedded) |
| `api/Elmanhg.Infrastructure/Migrations/20261002161421_AddTeacherThreadOutOfAppReminders.cs` (+ `.Designer.cs`) | 51 | CreateTable, FK restrict, unique index; no drops |
| `api/Elmanhg.Application/Teachers/SetTeacherPhoneNumber/SetTeacherPhoneNumberCommand.cs` | 11 | auditable command |
| `api/Elmanhg.Application/Teachers/SetTeacherPhoneNumber/SetTeacherPhoneNumberValidator.cs` | 19 | `ValidatePhoneNumber(CoreOtp)` |
| `api/Elmanhg.Application/Teachers/SetTeacherPhoneNumber/SetTeacherPhoneNumberHandler.cs` | 27 | handler |
| `api/Elmanhg.Api/Controllers/Teachers/Requests.cs` | 3 | `SetTeacherPhoneNumberRequest` |
| `api/Elmanhg.Tests/Integration/Infrastructure/MessageOutbox.cs` | 22 | test outbox |
| `api/Elmanhg.Tests/Integration/Infrastructure/RecordingMessageChannel.cs` | 14 | recording channel |
| `api/Elmanhg.Tests/Domain/TeacherThreads/TeacherThreadOutOfAppReminderTests.cs` | 30 | tests 1–2 |
| `api/Elmanhg.Tests/Application/Shared/RuntimeSettings/OutOfAppReminderRuntimeSettingsTests.cs` | 31 | tests 3–4 |
| `api/Elmanhg.Tests/Application/Features/TeacherThreads/ProcessTeacherThreadSla/ProcessTeacherThreadSlaOutOfAppReminderTests.cs` | 323 | tests 5–20 |
| `api/Elmanhg.Tests/Infrastructure/Messaging/MetaWhatsAppMessageChannelTests.cs` | 113 | tests 21–25 |
| `api/Elmanhg.Tests/Infrastructure/Messaging/ResendEmailMessageChannelTests.cs` | 92 | tests 26–31 |
| `api/Elmanhg.Tests/Infrastructure/Messaging/FakeMessageChannelTests.cs` | 44 | tests 32–33 |
| `api/Elmanhg.Tests/Infrastructure/Messaging/MessagingServiceCollectionExtensionsTests.cs` | 82 | tests 34–38 |
| `api/Elmanhg.Tests/Infrastructure/Messaging/OutOfAppReminderOptionsValidatorTests.cs` | 35 | tests 39–41 |
| `api/Elmanhg.Tests/Infrastructure/Messaging/ReminderDeadlineFormatterTests.cs` | 15 | test 42 |
| `api/Elmanhg.Tests/Integration/Persistence/TeacherThreadOutOfAppReminderPersistenceTests.cs` | 39 | test 46 |
| `api/Elmanhg.Tests/Application/Features/Teachers/SetTeacherPhoneNumber/SetTeacherPhoneNumberHandlerTests.cs` | 82 | tests 56–60 |
| `api/Elmanhg.Tests/Application/Features/Teachers/SetTeacherPhoneNumber/SetTeacherPhoneNumberValidatorTests.cs` | 69 | tests 61–63 |
| `web/src/features/users/schemas/teacherPhoneSchema.ts` | 16 | zod schema + shared `phoneNumberServerErrorFields` |
| `web/src/features/users/schemas/teacherPhoneSchema.test.ts` | 14 | W6 |
| `web/src/features/users/hooks/useTeacherPhoneNumber.ts` | 28 | mutation wrapper, toast, invalidation |
| `web/src/features/users/components/TeacherPhoneDialog.tsx` | 100 | dialog (set / remove) |
| `web/src/shared/api/generated/model/setTeacherPhoneNumberRequest.ts` | 11 | Orval output (generated) |

## Files modified
| Path | Change |
|---|---|
| `api/.../ProcessTeacherThreadSla/ProcessTeacherThreadSlaHandler.cs` | +3 ctor deps (after `teacherThreadNotifier`), touch point A (`ClaimAsync` before the single `SaveChangesAsync`), touch point B (`SendAsync` after `NotifyReminderAsync`); 2 usings. Nothing else. |
| `api/Elmanhg.Application/Shared/Options/AskTeacherOptions.cs` | 3 options |
| `api/Elmanhg.Application/DependencyInjection.cs` | 1 registration after `AskTeacherRuntimeSettings` |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `AddMessaging()`, repository registration |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | configure call + soft-delete filter |
| `api/Elmanhg.Infrastructure/OtpDelivery/WhatsApp/MetaWhatsAppTemplateMessage.cs` | `CreateUtility`; `CopyCodeButtonIndex` → `FirstButtonIndex` |
| `api/Elmanhg.Infrastructure/Hosting/InfrastructureIntegrations.cs`, `InfrastructureConfigurationReader.cs` | 2 rows, new options param |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | embedded templates (see Deviations, `WithCulture="false"`) |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | generated |
| `api/Elmanhg.Domain/Identity/User.Administration.cs` | `SetContactPhoneNumber` |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | `PhoneNumberTeachersOnly` |
| `api/Elmanhg.Application/Users/InviteUser/{Command,Validator,Handler}.cs` | optional `PhoneNumber` as planned |
| `api/Elmanhg.Api/Controllers/Teachers/TeachersController.cs` | `PUT {teacherId}/phone-number`, `UsersManage` policy |
| `api/Elmanhg.Api/Resources/Messages.{ar,en}.resx` | `PHONE_NUMBER_TEACHERS_ONLY` |
| `api/Elmanhg.Api/appsettings.example.json`, `deploy/api.env.example` | as planned |
| `api/openapi/v1.json` | regenerated by build: new operation + schema + `phoneNumber` on invite only |
| `api/Elmanhg.Tests/...` | `FakeRuntimeSettings`, `ProcessTeacherThreadSlaHandlerTests` (ctor only), `InfrastructureConfigurationReaderTests` (43–45), `AppDbContextTests` (migration 42), `ApiFactory` (outbox + channels + one `UseSetting`), `TeacherThreadSlaSweepTests` (47–49), `ConfigurationEndpointTests` (50–51), `UserTests` (53–55), `InviteUserValidatorTests` (field + 64–66), `InviteUserHandlerTests` (67), `TeachersEndpointTests` (68–74), `UsersEndpointTests` (75–76); plus `RuntimeSettingRegistryTests` and `RuntimeSettingValuesTests` (see Deviations) |
| `web/src/shared/api/generated/**` | `gen:api` output: teachers.ts/.msw.ts, model index, inviteUserCommand, zod files. No other drift. |
| `web/src/features/session/index.ts` | export `egyptianMobilePattern` |
| `web/src/features/users/{schemas/inviteUserSchema.ts, hooks/useInviteUser.ts, components/InviteUserDialog.tsx, components/UserListRow.tsx, components/UserListTable.tsx, pages/UsersPage.tsx, i18n/en.json, i18n/ar.json}` | as planned |
| `web/src/features/users/schemas/inviteUserSchema.test.ts` | W3–W5, existing cases pass `phoneNumber: ''` |
| `web/src/features/users/pages/UsersPage.actions.test.tsx` | W7–W11 (+ one existing expectation, see Deviations) |
| `web/src/features/configuration/{i18n/en.json,i18n/ar.json,pages/ConfigurationPage.test.tsx}`, `web/src/test/configurationFixtures.ts` | choices, 2 integrations, W1–W2 |
| `web/src/shared/i18n/{en,ar}.json` | `errors.PHONE_NUMBER_TEACHERS_ONLY` |
| `postman/elmanhg.postman_collection.json` | Invite teacher body gains `phoneNumber`; new "Set teacher phone number" (PUT, collection bearer, null removes) |
| `docs/PRD.md`, `ask-teacher.md`, `otp-delivery.md` (§10 + intro), `configuration.md`, `deployment.md`, `implementation-report.md`, `user-administration.md`, `audit-log.md`, `claude-design-prompt.md` | as planned; `ask-teacher.md` Options also lists the 3 new `AskTeacherOptions` keys |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `<EmbeddedResource Include="Messaging\Templates\*" LogicalName=…/>` | MSBuild treats `TeacherReminderEmail.ar.html` as an Arabic culture resource and moves it into the `ar/` satellite assembly, so `GetManifestResourceStream` returned null (6 tests failed) | Added `WithCulture="false"` to that item |
| `TextField … inputMode="numeric"` | `TextField` has no `inputMode` prop | Used `type="tel"` (numeric keypad on mobile) and the existing `description` prop for the hint |
| "Existing tests are unchanged" (web, configuration) | `runtimeSettingValue.test.ts` asserted the ar join `'WhatsApp، Email'`; the planned ar `choices` labels make it `'واتساب، البريد الإلكتروني'` | Updated that one expectation (same intent: Arabic comma join) |
| UsersPage.actions: only add W7–W11 | Existing test "the invite teacher dialog shows the invitation link after success" asserted the exact body; it now carries `phoneNumber: null` (the planned W8 contract) | Added `phoneNumber: null` to that expectation |
| Test-file list for the settings | `RuntimeSettingRegistryTests.Definitions_DefaultOptions_FourteenOrderedByGroup` (count 14) and `RuntimeSettingValuesTests.Get_EveryRegisteredKey_DeserialisesToItsType` (registry-completeness guard) fail when settings are added | Count → 17; added the 3 `values.Get(...)` lines. Method name still says "Fourteen" (not renamed, to avoid a test identity change); #256 will touch the same lines |
| Remove button: `save(id, null)`, then close | An error on remove had no visible outcome | On failure it applies the server error to the form (root/field), on success it closes |
| `teacherPhone.current` with `{phone}` | A bare masked number inside the Arabic sentence is reordered by the bidi algorithm (`678*****010`) | The interpolated value is wrapped in LRI/PDI isolates (`\u2066…\u2069`); W10 matches with an optional-isolate regex (repo precedent in `TeacherStatsPage.test.tsx`) |
| ApiFactory: only the outbox and the two channels | PROGRESS.md convention: new config also goes into ApiFactory via `UseSetting` | Added `UseSetting("OutOfAppReminders:ThreadLinkBaseUrl", "https://elmanhg.test/teacher/thread")` (safe; exercises the validator) |
| Phone `serverErrorFields` repeated in both dialogs | — | One `phoneNumberServerErrorFields` const in `teacherPhoneSchema.ts`, spread into both |

## Build & test
- `dotnet build api/Elmanhg.Api`: Build succeeded, only the pre-existing `core-libraries` CS8618/CS8602 warnings. OpenAPI regenerated (+64 lines, new operation/schema/invite field only).
- `dotnet ef migrations add AddTeacherThreadOutOfAppReminders`: Done; CreateTable + FK + unique index only.
- `dotnet test api/ -c Release` (worktree has no `appsettings.json`, so CI parity): **total 4900, failed 0, succeeded 4900, exit 0** (second run, after updating the two registry guard tests; the first run failed exactly those two).
- `dotnet format api/Elmanhg.slnx --verify-no-changes`: only `Elmanhg.Tests/Builders/SubscriptionBuilder.cs(57)` WHITESPACE (pre-existing, not touched; CRLF noise noted in PROGRESS.md). No new file flagged.
- Guard grep: only `new HttpClient(_handler)` in two new test classes (same pattern as the existing sibling tests); no production hit.
- Web: `npm ci`, `npm run gen:api` (no drift outside the planned files), `npm run typecheck` clean, `npm run lint` clean (0 warnings), `npx vitest run` **276 files / 1565 tests passed**, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` all formatted, `npm run build` ok, `npm run perf:budget`: entry 208/210, admin-users 261/270, all ok.
- Mutation checks (each restored afterwards, verified by grep): .NET — ClaimAsync moved after SaveChanges, `IsRecordedAsync` short-circuited, `PasswordHash` filter removed, non-2xx returns true, HTML encoding removed, `UsesMeta` ignores the template, domain role guard removed, invite role rule removed → 15 tests failed (tests 5, 8, 17, 23/25, 29, 30, 36, 45, 55, 58, 66, 71, 76). Web — phone field shown for admins, cellular-code mapping removed, removed-toast collapsed → W8, W10, W11 failed.

## Notes for review
- The `ProcessTeacherThreadSlaHandler` diff is exactly: 2 usings, 3 ctor params, line A, block B. `DueSlaStages`, `GetSlaDueIdsAsync` and the worker are untouched (for the #254 merge).
- `IMessageChannel` carries an XML doc line (the hidden "returns false, never throws" invariant, as planned); `MessagingHttpExtensions` has the one WHY comment; `TeacherPhoneDialog` has a one-line WHY comment on the bidi isolates.
- `MessagingHttpExtensions` catches `Exception when (!cancellationToken.IsCancellationRequested)` as the plan says — broader than the OTP filter; intended (Decision 7).
- Test 23 (`SendAsync_ProviderRejects_ReturnsFalse`, Meta) builds the channel through DI with the real resilience pipeline so "1 call, no retry" is meaningful; the other adapter tests use a bare `HttpClient` like their siblings.
- `VALIDATION_PHONE_NUMBER_IS_REQUIRED` has no web `errors.*` text (only the other three phone codes exist); the web never sends `""`, it sends null, so it cannot occur from the UI.
- `InviteUserDialog.tsx` (123) and `UserListRow.tsx` (119) were already ~110 lines and are now over ~100; `ProcessTeacherThreadSlaOutOfAppReminderTests.cs` is 323 lines (16 tests, test class).
- `shared/i18n` gained one error string (entry chunk 208/210 KB, still within budget).
