# Implementation — [E11.S3] Student and teacher administration (#106)

Worktree `D:/Personal/elmanhg-wt/106`, branch `feature/106-student-and-teacher-administration`. Nothing committed.
Plan: `01-plan.md`, with the two gate conditions from `00-acceptance.md` applied on top of it: (1) the invitation email is sent, not deferred; (2) the PRD amendment for complimentary grants ships in this change.

## Files created

### API: production
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Identity/User.Administration.cs` | 39 | `IsInvitationPending`, `CanBeSuspendedBy`, `Suspend(Guid,int)` (self, then last-admin), `Reactivate(Guid)` |
| `api/Elmanhg.Application/Shared/Options/UsersOptions.cs` | 17 | `Users:ListMaxPageSize`, `Users:SearchMaxLength`, `Users:ActiveStatusCacheSeconds` |
| `api/Elmanhg.Application/Users/Shared/ContactMask.cs` | 35 | Masks a phone (3 + 3 visible) and an email (first char + domain) |
| `api/Elmanhg.Application/Users/Shared/UserActiveCacheKey.cs` | 8 | `user-active:{id:N}` |
| `api/Elmanhg.Application/Users/Shared/UserSummaryResult.cs` | 6 | List row result |
| `api/Elmanhg.Application/Users/Shared/UserSummaryResultGenerator.cs` | 13 | Maps a user plus entitlement and subjects |
| `api/Elmanhg.Application/Users/GetUsers/GetUsersQuery.cs` | 8 | Query |
| `api/Elmanhg.Application/Users/GetUsers/GetUsersFilter.cs` | 20 | Role, status, and name-fragment / exact phone / exact normalised email search |
| `api/Elmanhg.Application/Users/GetUsers/GetUsersValidator.cs` | 25 | `USER_LIST_*` rules |
| `api/Elmanhg.Application/Users/GetUsers/GetUsersHandler.cs` | 62 | Page, admin count, entitlements (students), subject ids (teachers) |
| `api/Elmanhg.Application/Users/InviteUser/InviteUserCommand.cs` | 12 | Audited `User.Invite` |
| `api/Elmanhg.Application/Users/InviteUser/InviteUserResult.cs` | 8 | `{ userId, emailSent }`, audit id from the result |
| `api/Elmanhg.Application/Users/InviteUser/InviteUserValidator.cs` | 29 | Role, name, email rules |
| `api/Elmanhg.Application/Users/InviteUser/InviteUserHandler.cs` | 37 | Creates the password-less Teacher/Admin, then sends the invitation email |
| `api/Elmanhg.Application/Users/SuspendUser/SuspendUserCommand.cs` | 11 | Audited `User.Suspend` |
| `api/Elmanhg.Application/Users/SuspendUser/SuspendUserValidator.cs` | 13 | `USER_ID_REQUIRED` |
| `api/Elmanhg.Application/Users/SuspendUser/SuspendUserHandler.cs` | 36 | Lock, count, suspend, rotate stamp, evict cache |
| `api/Elmanhg.Application/Users/ReactivateUser/ReactivateUserCommand.cs` | 11 | Audited `User.Reactivate` |
| `api/Elmanhg.Application/Users/ReactivateUser/ReactivateUserValidator.cs` | 13 | `USER_ID_REQUIRED` |
| `api/Elmanhg.Application/Users/ReactivateUser/ReactivateUserHandler.cs` | 31 | Reactivate, update, evict cache |
| `api/Elmanhg.Application/Users/CheckUserActive/CheckUserActiveQuery.cs` | 5 | Query |
| `api/Elmanhg.Application/Users/CheckUserActive/CheckUserActiveHandler.cs` | 30 | Cached active check |
| `api/Elmanhg.Application/Auth/AcceptInvitation/AcceptInvitationCommand.cs` | 6 | `{ verificationId, password }` |
| `api/Elmanhg.Application/Auth/AcceptInvitation/AcceptInvitationValidator.cs` | 22 | Mirrors `RegisterWithEmailValidator` password rules |
| `api/Elmanhg.Application/Auth/AcceptInvitation/AcceptInvitationHandler.cs` | 49 | Email OTP, pending check, `AddPasswordAsync`, tokens |
| `api/Elmanhg.Application/Students/Shared/StudentLookup.cs` | 13 | Student-only lookup, 404 `STUDENT_NOT_FOUND` |
| `api/Elmanhg.Application/Students/Shared/StudentProfileResult.cs` | 7 | Admin profile result |
| `api/Elmanhg.Application/Students/Shared/StudentProfileResultGenerator.cs` | 22 | Maps profile, interests, subscriptions |
| `api/Elmanhg.Application/Students/GetStudentProfile/GetStudentProfileQuery.cs` | 6 | Query |
| `api/Elmanhg.Application/Students/GetStudentProfile/GetStudentProfileValidator.cs` | 13 | `STUDENT_ID_REQUIRED` |
| `api/Elmanhg.Application/Students/GetStudentProfile/GetStudentProfileHandler.cs` | 23 | Profile handler |
| `api/Elmanhg.Application/Subscriptions/Shared/AdminSubscriptionResult.cs` | 9 | With `IsComplimentary`; auditable result |
| `api/Elmanhg.Application/Subscriptions/Shared/AdminSubscriptionResultGenerator.cs` | 8 | `IsComplimentary = PaymobReference is null` |
| `api/Elmanhg.Application/Subscriptions/GrantComplimentarySubscription/GrantComplimentarySubscriptionCommand.cs` | 13 | Audited `Subscription.GrantComplimentary` |
| `.../GrantComplimentarySubscriptionValidator.cs` | 19 | Student id, plan, period rules |
| `.../GrantComplimentarySubscriptionHandler.cs` | 36 | Catalogue price, `EnsureCanGrant`, `Subscription.Start(..., null, admin)` |
| `api/Elmanhg.Application/Progress/Shared/SubjectProgressLoader.cs` | 18 | Body moved from `GetSubjectProgressHandler` |
| `api/Elmanhg.Application/Progress/Shared/WeakSpotsLoader.cs` | 34 | Body moved from `GetWeakSpotsHandler` |
| `api/Elmanhg.Application/Progress/Shared/SessionHistoryLoader.cs` | 39 | Body moved from `GetSessionHistoryHandler` |
| `api/Elmanhg.Application/Progress/Shared/StudentProgressResult.cs` | 3 | `{ Subjects, WeakSpots }` |
| `api/Elmanhg.Application/Progress/GetStudentProgress/GetStudentProgress{Query,Validator,Handler}.cs` | 6/13/24 | Admin progress |
| `api/Elmanhg.Application/Progress/GetStudentSessionHistory/GetStudentSessionHistory{Query,Validator,Handler}.cs` | 8/22/19 | Admin history |
| `api/Elmanhg.Api/Controllers/Users/UsersController.cs` | 55 | `GET /api/users`, invitations, suspend, reactivate |
| `api/Elmanhg.Api/Authorization/ActiveUserTokenValidation.cs` | 37 | `OnTokenValidated` active-user check |
| `api/Elmanhg.Application/Shared/Email/IInvitationEmailSender.cs` | 6 | **Gate condition 1**: port for the invitation email |
| `api/Elmanhg.Application/Shared/Email/InvitationEmail.cs` | 5 | **Gate 1**: `{ Email, DisplayName, Role }` |
| `api/Elmanhg.Infrastructure/Invitations/InvitationEmailOptions.cs` | 13 | **Gate 1**: `InvitationEmail:AcceptInviteUrl`, `Subject` |
| `api/Elmanhg.Infrastructure/Invitations/InvitationEmailOptionsValidator.cs` | 18 | **Gate 1**: https link required when the email channel is Resend |
| `api/Elmanhg.Infrastructure/Invitations/InvitationEmailServiceCollectionExtensions.cs` | 22 | **Gate 1**: Resend when `OtpDelivery:Email` is Enabled + Resend, otherwise Fake |
| `api/Elmanhg.Infrastructure/Invitations/FakeInvitationEmailSender.cs` | 13 | **Gate 1**: logs the role only, returns `false` |
| `api/Elmanhg.Infrastructure/Invitations/ResendInvitationEmailSender.cs` | 46 | **Gate 1**: posts to Resend with an idempotency key; a failure is a warning (no address, no link) and `false` |
| `api/Elmanhg.Infrastructure/Invitations/InvitationEmailTemplate.cs` | 38 | **Gate 1**: renders the embedded templates (name HTML-encoded) |
| `api/Elmanhg.Infrastructure/Invitations/Templates/InvitationEmail.html` / `.txt` | 28/8 | **Gate 1**: Arabic RTL invitation email |

### API: tests (every class and method in the plan's Test plan, plus the gate-1 tests)
| Path | Lines |
|---|---|
| `Tests/Api/Authorization/ActiveUserTokenValidationTests.cs` | 64 |
| `Tests/Application/Features/Users/Shared/ContactMaskTests.cs` | 43 |
| `Tests/Application/Features/Users/GetUsers/GetUsers{Filter,Validator,Handler}Tests.cs` | 63/61/117 |
| `Tests/Application/Features/Users/InviteUser/InviteUser{Validator,Handler}Tests.cs` | 71/86 |
| `Tests/Application/Features/Users/SuspendUser/SuspendUser{Validator,Handler}Tests.cs` | 26/131 |
| `Tests/Application/Features/Users/ReactivateUser/ReactivateUser{Validator,Handler}Tests.cs` | 26/100 |
| `Tests/Application/Features/Users/CheckUserActive/CheckUserActiveHandlerTests.cs` | 84 |
| `Tests/Application/Features/Auth/AcceptInvitation/AcceptInvitation{Validator,Handler}Tests.cs` | 51/162 |
| `Tests/Application/Features/Students/GetStudentProfile/GetStudentProfile{Validator,Handler}Tests.cs` | 26/84 |
| `Tests/Application/Features/Students/StudentRepositoryStub.cs` | 14 (helper, see Deviations) |
| `Tests/Application/Features/Subscriptions/GrantComplimentarySubscription/GrantComplimentarySubscription{Validator,Handler}Tests.cs` | 43/104 |
| `Tests/Application/Features/Progress/GetStudentProgress/GetStudentProgress{Validator,Handler}Tests.cs` | 26/66 |
| `Tests/Application/Features/Progress/GetStudentSessionHistory/GetStudentSessionHistory{Validator,Handler}Tests.cs` | 50/66 |
| `Tests/Infrastructure/Invitations/ResendInvitationEmailSenderTests.cs` | 58 (gate 1) |
| `Tests/Infrastructure/Invitations/InvitationEmailOptionsValidatorTests.cs` | 40 (gate 1) |
| `Tests/Integration/Users/UsersEndpointTests.cs` | 239 |
| `Tests/Integration/Users/UsersTestData.cs` | 40 (helper) |
| `Tests/Integration/Auth/AcceptInvitationEndpointTests.cs` | 112 |
| `Tests/Integration/Students/StudentAdministrationEndpointTests.cs` | 122 |
| `Tests/Integration/Subscriptions/ComplimentarySubscriptionEndpointTests.cs` | 115 |

### Web (`web/src/…`)
| Path | Lines | Purpose |
|---|---|---|
| `routes/admin/student.$studentId.tsx` | 7 | W1 |
| `routes/accept-invite.tsx` | 9 | W2 |
| `features/users/index.ts`, `locales.ts`, `i18n/en.json`, `i18n/ar.json` | 4/9/162/162 | W3–W5 |
| `features/users/schemas/{usersSearch,userFilters,inviteUser,grantPlan,studentDetailSearch}Schema.ts` | 17/9/16/8/8 | W6–W10 |
| `features/users/api/userListParams.ts` | 39 | W11 (plus `toUserListPage`, the payments pattern) |
| `features/users/hooks/{useUsersSearch,useUserList,useUserStatus,useInviteUser,useTeacherSubjects,useGrantPlan,useStudentHistorySearch}.ts` | 40/10/49/29/37/34/23 | W12–W18 |
| `features/users/pages/UsersPage.tsx`, `StudentDetailPage.tsx` | 116/99 | W19–W20 |
| `features/users/components/UserRoleTabs.tsx`, `UserFiltersForm.tsx`, `UserListTable.tsx`, `UserListRow.tsx`, `UserStatusBadge.tsx`, `TeacherSubjectsCell.tsx`, `UserStatusDialog.tsx`, `InviteUserDialog.tsx`, `GrantPlanDialog.tsx`, `UserListEmptyState.tsx`, `UserListSkeleton.tsx`, `StudentProfileCard.tsx`, `StudentSubscriptionsCard.tsx`, `StudentProgressSection.tsx`, `StudentHistorySection.tsx` | 39/69/58/96/27/39/60/110/132/24/20/68/67/57/103 | W21–W35 |
| `features/users/components/UserActionButtons.tsx`, `StudentSubjectCard.tsx`, `StudentWeakSpots.tsx`, `StudentHistoryTable.tsx` | 66/70/56/71 | Split-outs (see Deviations) |
| `features/session/pages/AcceptInvitePage.tsx`, `components/SetPasswordForm.tsx`, `schemas/setPasswordSchema.ts` | 60/58/14 | W36–W38 |
| `test/userFixtures.ts` | 128 | W39 |
| Tests: `users/schemas/*.test.ts` (5), `users/api/userListParams.test.ts`, `users/locales.test.ts`, `users/pages/UsersPage.test.tsx`, `UsersPage.actions.test.tsx`, `StudentDetailPage.test.tsx`, `session/schemas/setPasswordSchema.test.ts`, `session/pages/AcceptInvitePage.test.tsx` | 12–245 | Tests 132–143, every `it` from the plan |

### Docs
| Path | Lines | Purpose |
|---|---|---|
| `docs/user-administration.md` | 93 | File #49: endpoints, rules, revocation, invitations (with the email), masking and search, grants pointer, options, error codes, audit |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Identity/User.cs` | `partial` |
| `api/Elmanhg.Domain/Identity/IUserRepository.cs` | `ExecuteInAdminRosterLockAsync` |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | 5 domain codes |
| `api/Elmanhg.Domain/Subscriptions/StudentEntitlement.cs` | `EnsureCanGrant` |
| `api/Elmanhg.Domain/Subscriptions/SubscriptionEntitlementSpecification.cs` | `EntitledForStudents` |
| `api/Elmanhg.Infrastructure/Identity/UserRepository.cs` | Advisory-lock transaction (parameterised `ExecuteSqlAsync`, execution strategy) |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | `// USERS` group, student, complimentary and invitation codes |
| `api/Elmanhg.Application/DependencyInjection.cs` | `UsersOptions` with `ValidateOnStart` |
| `api/Elmanhg.Application/Progress/{GetSubjectProgress,GetWeakSpots,GetSessionHistory}/*Handler.cs` | Delegate to the loaders (existing tests unchanged and green) |
| `api/Elmanhg.Api/Controllers/Students/StudentsController.cs`, `Requests.cs` | 4 actions, `GrantComplimentarySubscriptionRequest` |
| `api/Elmanhg.Api/Controllers/Auth/AuthController.cs` | `AcceptInvitation` (anonymous, credential rate limit, refresh cookie) |
| `api/Elmanhg.Api/Program.cs` | `AddActiveUserTokenValidation()` |
| `api/Elmanhg.Api/Resources/Messages.{en,ar}.resx` | 20 codes each |
| `api/Elmanhg.Api/appsettings.example.json` | `Users` section, and `InvitationEmail` (gate 1) |
| `api/openapi/v1.json` | Regenerated by `dotnet build` |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `Users:*` keys and `InvitationEmail:*` keys (gate 1) |
| `api/Elmanhg.Tests/Domain/Identity/UserTests.cs` | 14 tests added; plus one existing line re-typed (see Deviations) |
| `api/Elmanhg.Tests/Domain/Subscriptions/StudentEntitlementTests.cs`, `SubscriptionEntitlementSpecificationTests.cs` | Tests added only |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `AddInvitationEmail()` (gate 1) |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | Embedded invitation templates (gate 1) |
| `api/Elmanhg.Infrastructure/OtpDelivery/OtpDeliveryServiceCollectionExtensions.cs` | `Options`, `BaseAddress`, `AddOtpResilience` made `internal` so the invitation sender reuses the Resend client setup (gate 1) |
| `web/src/routes/admin/users.tsx` | Real page with `validateSearch` |
| `web/src/routeTree.gen.ts`, `web/src/shared/api/generated/**` | Regenerated (Vite router plugin, `npm run gen:api`) |
| `web/orval.config.ts` | `GetUsers` and `GetStudentSessionHistory` added to the `zod.generate.query: false` list (see Deviations) |
| `web/src/features/session/index.ts`, `i18n/{en,ar}.json` | `AcceptInvitePage` export; `acceptInvite.*`, `fields.newPassword/confirmPassword`, `actions.setPassword`, `validation.passwordMismatch` |
| `web/src/shared/i18n/{en,ar}.json` | `errors.<CODE>` for all 20 new codes |
| `web/scripts/perf/budgets.json` | `admin-users`: measured 254 KB br → budget 270 |
| `postman/elmanhg.postman_collection.json` | Folder "Users" (List students, Invite teacher, List teachers, Assign subject, Suspend user, Reactivate user), folder "Students" (profile, progress, sessions, grant), "AcceptInvitation" in Auth after LoginWithEmailCode; variables `studentId`, `invitedUserId`, `invitedEmail`, `invitePassword` |
| `docs/PRD.md` | §7.1 invite sentence; §10.4 rewritten; §11.2 bullet 2 and §17 rule 12 amended (**gate 2**) |
| `docs/subscriptions.md` | `## Complimentary grants`, API row, #106 line |
| `docs/audit-log.md` | 4 audited rows; `AcceptInvitation` and `CheckUserActive` under Not audited |
| `docs/progress.md` | `## Admin view`; the "#106" access note |
| `docs/performance.md` | §3 `admin-users` row; §4 `users` namespace |
| `docs/claude-design-prompt.md` | §4 Admin users + student page; Landing `/accept-invite` |
| `docs/prototype.md` | Users page bullet |
| `docs/deployment.md`, `deploy/api.env.example`, `docs/otp-delivery.md` | `Users__*` and `InvitationEmail__*` (gate 1; PROGRESS config convention) |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Scope "Deferred: automatic invitation email"; `InviteUserResult(Guid UserId)`; `InviteUserHandler(UserManager, ICurrentUserService)` | Gate condition 1 overrides: send it now through the #171 email channel (Fake default, Resend when configured, placeholder config, never log the link/token). The #171 abstraction (`IOtpChannel`) only sends codes | Added `IInvitationEmailSender` (Application) with Fake and Resend senders (Infrastructure, `Invitations/`), selected from `OtpDelivery:Email` (Enabled + Resend → Resend, else Fake) and reusing its key, sender address, base URL and resilience. New config `InvitationEmail:{AcceptInviteUrl,Subject}` (empty URL in the example; startup validation requires an https URL only when Resend is on). `InviteUserResult` gains `EmailSent`; the handler gets `IInvitationEmailSender`. A send failure never fails the invite (the account is already created): it is logged as a warning without the address or link and returns `emailSent: false`; the dialog then says to share the link. The link carries no token (plan Decision 8), so there is no token to log. Added `ResendInvitationEmailSenderTests` (3) and `InvitationEmailOptionsValidatorTests` (3), and two `InviteUserHandlerTests` assertions on the sender. New web copy `invite.emailSent` / `invite.emailNotSent`. |
| PRD §11.2 / §17 rule 12 amendment (Decision 11) | Gate condition 2: ship it in this change and flag it | Done. **Dev-visible**: PRD rule 12 now reads "…or through an admin's complimentary grant (§10.4)", i.e. entitlement can now change without a Paymob event. |
| `UserTests` "add only" | Adding `Suspend(Guid, int)` makes the existing `var act = user.Suspend;` (test `Suspend_AlreadySuspended_ThrowsBusinessRuleViolation`) an ambiguous method group (CS8917), so the suite does not compile | Changed that one line to `Action act = user.Suspend;`. Same test, same assertion; compile-only change. |
| `web/orval.config.ts` not listed | The generated zod for query params with integer defaults does not type-check (TS2769); every existing paged endpoint is already in the `generate: { query: false }` list | Added `GetUsers` and `GetStudentSessionHistory` to that list. |
| `OtpDeliveryServiceCollectionExtensions.cs` not listed | Needed to reuse the Resend client's base address and resilience for gate 1 | Made three private helpers `internal`; no behaviour change. |
| `Files to create` exact list | Extra files | Gate-1 files above; test helpers `StudentRepositoryStub.cs` (unit) and `Integration/Users/UsersTestData.cs`; web split-outs `UserActionButtons.tsx` (the suspend/activate/grant rules shared by W24 and W32, as W32 says "the same rules as W24"), `StudentSubjectCard.tsx`, `StudentWeakSpots.tsx`, `StudentHistoryTable.tsx` (to keep W34/W35 under 120 lines). |
| W30 `UserListEmptyState` props `{ variant; tab; onClear }` | `tab` is unused (lint) | Props are `{ variant; onClear }`. |
| W26 `TeacherSubjectsCell` props `{ teacherId; subjectIds }`; W33 `StudentSubscriptionsCard` `{ subscriptions }` | Needed for accessible names (fieldset legend, table caption) | Added `teacherName` and `studentName`. |
| W19 error block "mirrors `PaymentLogPage`" | Kept the page under 120 lines | Uses `ContentErrorState` (same markup, same role=alert and Retry), as the plan already does for the student page. |
| Test 114 `Accept_AfterAcceptance_Returns404`: "a second OTP plus accept" | Core.OTP refuses a second send to the same email within `ReissueCooldownSeconds` (60 s) | The second verified OTP is seeded directly (`OtpBuilder...Verified()`, as `AuditChangeCaptureTests` seeds OTPs); the assertion is unchanged (404 `INVITATION_NOT_FOUND`). |
| `GetUsers` action signature `role = Student, string? search, …` | C# needs optional parameters last | `GetUsers([FromQuery] string? search, [FromQuery] UserStatus? status, [FromQuery] UserRole role = UserRole.Student, pageNumber = 1, pageSize = 20)` (the `AuditLogsController` shape). |

## Build & test

- API, CI parity: no `api/Elmanhg.Api/appsettings.json` exists in this worktree, so nothing had to be moved aside.
  - `dotnet build api/` → `0 Warning(s)` outside the vendored core-libraries (pre-existing CS8618/CS8602 there).
  - `dotnet test api/ -c Release` → `Test run summary: Passed! total: 3944 failed: 0 succeeded: 3944` (Docker/Testcontainers up).
  - `dotnet format --verify-no-changes --exclude core-libraries` → one WHITESPACE finding in `Elmanhg.Tests/Builders/SubscriptionBuilder.cs(57,27)`, a file this change does not touch (pre-existing).
  - Guard grep → only test-code hits: `context.Result` (the `TokenValidatedContext.Result` property, not `Task.Result`) and `new HttpClient(_handler)` over a stub handler in `ResendInvitationEmailSenderTests` (the same pattern as `ResendEmailOtpChannelTests`). No production hit.
- Web:
  - `npm ci`; `npm run gen:api` (then run twice more: content hash identical, so no drift).
  - `npx tsc -b` → clean. `npx eslint . --max-warnings=0` → clean. `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → `All matched files use Prettier code style!`
  - `npx vitest run --coverage` (first full run) → `Test Files 219 passed (219)`, `Tests 1261 passed (1261)`, exit 0, thresholds met (All files 95.26 % lines / 83.45 % branches).
  - Final full re-run after the last `UsersPage` trim → `Tests 2 failed | 1259 passed (1261)`: both failures are `questions/pages/NewEssayQuestion.test.tsx` ("Test timed out in 15000ms", the TipTap editor under coverage while other lanes loaded the machine). That file is untouched by this change and passes on its own: `npx vitest run src/features/questions/pages/NewEssayQuestion.test.tsx` → `Tests 7 passed (7)`. All users/session tests passed in both runs.
  - `npm run build` → `✓ built`; `npm run perf:budget` → `entry 203/210`, `landing 211/220`, `lesson 232/240`, `quiz 251/255`, `admin-dashboard 221/230`, `admin-users 254/270`, all ok.
- `ai/` not touched; not run.
- Mutation checks (each mutated, target tests failed, file restored and byte-compared):
  - remove `EnsureCanGrant` in the grant handler → 2 handler tests fail;
  - skip the cache `Set` in `CheckUserActiveHandler` → the cache test fails;
  - phone search `==` → `StartsWith` → `Build_PartialPhone_DoesNotMatch` (and the email test) fail;
  - last-admin `<= 1` → `< 1` → `UserTests` and `SuspendUserHandlerTests` last-admin tests fail;
  - web: `grantablePlan` Free → null, `setPasswordSchema` refine removed, `hasActiveFilters` ignoring `q` → 5 tests fail.
  - Not mutated (security code, per the PROGRESS rule): the self-suspension check and `OnTokenValidated`. Verified by reading: `Suspend_ByItself_*`, `Handle_Self_*`, `Suspend_Self_Returns400` assert the exact code and unchanged status; `ActiveUserTokenValidationTests` assert the exact failure message and that no query is sent without a claim; integration test 107 asserts 401 for the old access token and the old refresh cookie after suspension.

## Notes for review

- **Suspend transaction and audit.** `UserManager.UpdateSecurityStampAsync` saves through the scoped `AppDbContext` inside the advisory-lock transaction; the audit row is written by the pipeline after the handler returns (committed). Suspend of an Admin re-counts inside the lock; non-admins skip the count.
- **Execution strategy retry.** `ExecuteInAdminRosterLockAsync` runs the callback inside `CreateExecutionStrategy().ExecuteAsync`; a transient retry re-runs `FindByIdAsync` on the same context, which returns the tracked user. This matches the plan's code verbatim.
- **Per-request check cost.** `CheckUserActiveQuery` goes through the MediatR pipeline (request metrics included) once per authenticated request per 30 s per user. `UserActivityBehaviour` does not fire there, because `HttpContext.User` is not set yet during `OnTokenValidated`.
- **Existing tests that suspend users directly in the DB** are unaffected (they suspend before signing in, so nothing is cached).
- **Postman order.** `AcceptInvitation` sits in Auth, as the plan asks; its description says to run it after Users > Invite teacher plus SendEmailOtp/VerifyOtp for `invitedEmail` (the code is manual, like the existing email-code flow). A top-to-bottom collection run reaches it before the invite exists, so it returns 404 there.
- **Admin-users bundle** is 33 KB above the dashboard: react-hook-form + zod resolver (14 KB), the radix dialog (10 KB) and both `users` locale files (7 KB). Documented in performance.md §4.
- **Invitation email with the fake provider** returns `emailSent: false`, so dev and CI always show "share the link yourself". That is honest (nothing was delivered).
- `User.CreatedBy` is not set on invited users (the factory takes no creator); the audit row records the admin.
- `docs/backlog.json` untouched (story checklist only; incompleteness is not a docs violation).
- Line endings: some files were written with LF in the working copy; git (`autocrlf=true`) normalises them. A prettier run had touched unrelated session/route files with EOL-only changes; those 88 files were restored with `git checkout` and are not part of the diff.

## Rebase onto origin/main (#119, #110) — conflict resolution

The approved work was stashed, the branch fast-forwarded to origin/main, and `git stash pop` left 9 conflicts. Resolution, no logic changes:

| File | Resolution |
|---|---|
| `Messages.ar.resx`, `Messages.en.resx` | Kept both: upstream `TRAINING_EXPORT_*` then #106 `USER_*`/`STUDENT_*`/`COMPLIMENTARY_*`/invitation codes |
| `web/src/shared/i18n/ar.json`, `en.json` | Kept both error-code blocks (comma added); valid JSON, no duplicate keys |
| `web/orval.config.ts` | Kept `GetTrainingExports` plus `GetUsers`, `GetStudentSessionHistory` zod overrides |
| `postman/elmanhg.postman_collection.json` | Kept `trainingExportId` plus the #106 variables; valid JSON, request order untouched |
| `docs/audit-log.md` | Kept both command-table rows; merged the "Not audited" bullets (training-export download exception + `CheckUserActive` / `AcceptInvitation`) |
| `api/openapi/v1.json`, `web/src/shared/api/generated/model/index.ts` | Took upstream, then regenerated via `dotnet build api/ -c Release` and `npm --prefix web run gen:api` |

Verification (no `api/Elmanhg.Api/appsettings.json` present, so CI parity by default):
- `dotnet build api/ -c Release`: 0 errors.
- `dotnet test api/ -c Release`: total 4136, succeeded 4136, failed 0.
- `npm --prefix web run lint`: clean. `typecheck`: clean.
- `npm --prefix web test -- --run`: 232 files, 1320 tests passed.
- `npm --prefix web run build`: ok. `perf:budget`: all chunks within budget (admin-users 256/270 KB).
- `format:check` reports 992 files in this Windows worktree because `core.autocrlf=true` checks them out with CRLF line endings. This is local only; CI on Linux gets LF.

Stash entry dropped. Nothing committed.
