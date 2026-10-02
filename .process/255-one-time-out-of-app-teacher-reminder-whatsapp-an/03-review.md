VERDICT: APPROVED

# Review — One-time out-of-app teacher reminder (WhatsApp and email) · #255 [E18.S3]

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Infrastructure/Messaging/TeacherReminderEmailTemplate.cs:26-31` — the placeholders are replaced in a chain, so a subject or lesson title that literally contains `{{lesson}}`, `{{deadline}}` or `{{link}}` gets the later value substituted into it. The value is HTML-encoded first, so this cannot inject markup, and titles are admin-authored. A single-pass replace would close it.
- `web/src/features/users/components/UserListRow.tsx:35` — `contact = item.maskedPhone ?? item.maskedEmail`. Once an admin sets the WhatsApp number of a teacher, the teacher row shows the masked phone instead of the masked email. This is a side effect on the teachers tab that no doc states.
- `web/src/features/users/components/TeacherPhoneDialog.tsx:57-60` — plan contract #32 asks for the masked number in mono with `dir="ltr"`. The implementation uses LRI/PDI isolates, which fixes the bidi order, but the number is not mono.
- `web/src/shared/i18n/en.json`, `ar.json` — there is no `errors.VALIDATION_PHONE_NUMBER_IS_REQUIRED` text. The UI never sends an empty string, so this only matters for API clients.
- `api/Elmanhg.Tests/Application/Shared/RuntimeSettings/RuntimeSettingRegistryTests.cs:17` — the method is still named `FourteenOrderedByGroup` but now asserts 17.
- `api/Elmanhg.Tests/Integration/TeacherThreads/TeacherThreadSlaSweepTests.cs` (`Process_TwoConcurrentSweeps_SendOutOfAppReminderOnce`) — the test does not prove the two runs actually overlapped. It still checks the outcome (1 email, 1 marker), and test 46 proves the unique index.

## Verified
- **Exactly once.** `ProcessTeacherThreadSlaHandler.cs:46-47`: `ClaimAsync` adds the marker before the single `SaveChangesAsync`. Both repositories share the scoped `AppDbContext`, so events and marker commit in one transaction. The sends happen only after that commit (`:74-77`) and only when this call inserted the marker.
- **Concurrent sweeps.** A concurrent sweep inserts the same `(ThreadId, Kind, SlaDueAt)` event and the same marker `ThreadId`. It loses on either unique index and sends nothing.
- **Restarts and follow-ups.** `IsRecordedAsync` (`OutOfAppTeacherReminder.cs:25`) blocks a second send after a restart or in a follow-up window.
- **Migration.** The migration is CreateTable, a Restrict FK and the unique `IX_TeacherThreadOutOfAppReminders_ThreadId` only. Nothing writes to `TeacherThreads`. Nothing hard-deletes threads, so the Restrict FK is safe.
- **Failure isolation.** `MessagingHttpExtensions.cs:12-28` turns any non-2xx response or non-cancellation exception into `false`. Nothing else in either adapter can throw for a valid configuration: the time zone is validated at boot, and `PhoneNumberFormatter` never throws. The dispatcher loops over recipients and channels, so a `false` result does not stop the others. Tests 14, 15, 23, 24, 30 and 31 cover this.
- **Recipients.** The claimer is used when the thread is claimed, otherwise the assigned teachers (unchanged `TeacherThreadReminderRecipients`). They are then filtered with `Status == Active && PasswordHash != null` (`OutOfAppTeacherReminder.cs:36`), which matches `IsInvitationPending`. A blank address skips only that channel and logs at Information (`:43-47`).
- **Privacy.** Logs carry the thread id, user id, channel, type, status and outcome only. The fake never logs the address. Tests 20, 25 and 32 assert that the phone, email and token are absent from logs.
- **Audit.** The audit row has no diff, and test 68 serialises the row and asserts the number is absent. `UserSummaryResult` and `StudentProfileResult` return only `ContactMask.MaskPhone`. No teacher or inbox result exposes the phone.
- **Phone sign-in.** `LoginWithPhoneHandler` and `RegisterWithPhoneHandler` look users up with `FindByNameAsync(otp.Recipient)`, and `SetContactPhoneNumber` never touches `UserName`. Test 74 confirms that phone sign-in returns 404 `PHONE_NUMBER_NOT_REGISTERED`.
- **Phone validation.** It is the same as the student flow: `ValidatePhoneNumber(CoreOtp:PhoneCodes, PhoneLength)` (Core `GenerateOTPValidator`), and on the web `egyptianMobilePattern`. `PhoneNumberConfirmed` stays false.
- **Runtime settings.** `enabled` defaults to true, `channels` is a Choice of WhatsApp, Email or Both defaulting to Both, and `stage` is a Choice of FirstReminder or SecondReminder defaulting to SecondReminder, all taken from `AskTeacherOptions`. Breach is rejected with 422 (test 51).
- **Deployment configuration.** The template name and language are deployment configuration (`OutOfAppReminders`, a RegularExpression on the language). Meta is used only when the template name is set (`UsesMeta`).
- **Email.** It has ar and en templates. HTML-encoding is proven by test 29. The Resend idempotency key is deterministic (test 28).
- **`ThreadLinkBaseUrl` validation.** It must be an absolute https URL when Resend is on or when the value is set (`OutOfAppReminderOptionsValidator.cs:17-21`, tests 38 and 40).
- **Endpoints.**
  - `PUT /api/teachers/{teacherId:guid}/phone-number` uses `DefaultCodes.UsersManage` (`TeachersController.cs:45-52`). It returns 403 for a teacher and 401 for an anonymous caller (test 73).
  - The invite change is backward compatible: `string? PhoneNumber = null` is the last parameter, it is optional in OpenAPI, and it is nullish in the Orval zod output. An admin invite with a phone returns 422 (tests 66 and 76).
- **Web.** All classes are design tokens (`text-ui text-text`, gap-2/3), and `Button` defaults to `type=button`. ar and en strings exist for every new key. The phone inputs use `dir=ltr`, and the masked number is bidi-isolated (W10).
- **Generated artifacts.** The OpenAPI file gained only the new operation, the new schema and the invite field. The Orval output matches it. Postman has the new PUT (collection bearer, teacherId variable defined) and the invite body with phoneNumber. The migration and snapshot agree.
- **Docs sync.** All the planned docs were updated: PRD §10, §10.6, §12.1 and §15, plus ask-teacher.md (the in-app-only sentence replaced), otp-delivery.md §10, configuration.md §2/§6/§7, deployment.md, implementation-report.md §4, user-administration.md, audit-log.md and claude-design-prompt.md. docs/prototype.md does not describe /admin/users, so it needs no change. I found no divergence.
- **Test runs.**
  - `dotnet test api/ -c Release` (no appsettings.json in the worktree): 4900 total, 0 failed, exit 0.
  - `npm run typecheck`: clean. `npm run lint`: exit 0.
  - `npx vitest run`: 1562 of 1565 passed. The 3 failures were in untouched files (NewDragDropQuestion, QuizPage.essay, QuizPage.mathStepGrading), and all 3 files passed when rerun alone (22/22). These were timeouts under load.
- **Deviations.** Every deviation in 02-implementation.md is justified and visible: WithCulture=false, type=tel, the ar choice-join expectation, phoneNumber null in the existing invite expectation, the registry guard tests, the remove-error handling, the bidi isolates and the ApiFactory UseSetting.
- **Plan tests.** Every .NET test name in the plan exists (grep), and so does every web test W1 to W11.

## Test quality
- **Test classes that constrain the code.**
  - ProcessTeacherThreadSlaOutOfAppReminderTests:
    - The repositories use compiled predicates, so the Active/PasswordHash filter is really exercised (test 17).
    - Received.InOrder pins the order AddAsync, then SaveChanges, then sends (test 5).
    - Full message equality pins the recipient, address and context (test 6).
  - MetaWhatsAppMessageChannelTests: test 23 goes through DI with the real resilience pipeline, so "no retry" is meaningful.
  - ResendEmailMessageChannelTests: asserts the real JSON body and headers.
  - TeacherThreadOutOfAppReminderPersistenceTests: hits Postgres 23505.
  - TeachersEndpointTests: covers the audit row, phone sign-in, 400, 404, 422, 401 and 403.
  - SetTeacherPhoneNumberHandlerTests: has Received(1) and DidNotReceive on UpdateAsync and FindByIdAsync.
  - Web W7 to W11: assert the captured request bodies and the server-error mapping.
- **Weaker tests.**
  - The concurrent-sweep integration test does not prove the two runs overlapped (see Non-blocking).
  - W2 asserts Fake, which the provider column shows anyway, so it adds little beyond confirming the row exists.
- **No vacuous tests.** None of the tests only asserts that a substitute returned what it was told to return.
