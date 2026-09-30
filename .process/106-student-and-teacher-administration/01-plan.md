# Plan — [E11.S3] Student and teacher administration (#106)

## Goal
An admin opens `/admin/users` and manages every account. The page has three tabs: Students, Teachers and Admins, each with server-side search and paging. An admin can:
- open any student's profile and progress, suspend or reactivate the student, and grant a complimentary Base or Ask a Teacher plan;
- invite a teacher or an admin, assign or unassign a teacher's subjects, and deactivate or reactivate teachers and admins.

The last active admin can never be deactivated, and an admin can never deactivate their own account. Deactivation takes effect at once: the refresh token dies (security stamp rotated), and the access token is refused on its next request. An invitee finishes the invitation at `/accept-invite`: they prove the email with a one-time code and set their password. Contact data is masked everywhere on these screens. Every mutation is audited.

## Scope
**In:**
- API
  - `GET /api/users` (role, search, status, paged): masked contact, plan tier for students, subject ids for teachers, invitation-pending flag, and `canSuspend`.
  - `POST /api/users/invitations` (Teacher/Admin).
  - `POST /api/users/{id}/suspend`: self and last-admin guards, advisory lock, security-stamp rotation, cache eviction.
  - `POST /api/users/{id}/reactivate`.
  - Student profile, progress and history: `GET /api/students/{id}`, `/progress`, `/sessions`.
  - `POST /api/students/{id}/complimentary-subscriptions`.
  - `POST /api/auth/invitations/accept` (anonymous; email OTP plus new password).
  - The active-user check on every validated JWT (`OnTokenValidated`, cached, evicted on suspend and reactivate).
- Web
  - `/admin/users` (replaces the placeholder), `/admin/student/$studentId` and `/accept-invite`.
  - A new lazy `users` i18n namespace, registered in the page modules as #105 did for the dashboard.
  - A new `admin-users` bundle budget.
- Docs and tooling: the docs listed under Existing code touched, Postman, OpenAPI and Orval regeneration.

**Out:**
- Role changes: no promotion or demotion endpoint. With no role change, "no self-demotion" reduces to "no self-deactivation".
- Revoking or ending a complimentary grant (not in the story; the admin can wait for it to lapse).
- Unmasking contacts in the existing admin payment log (#102). It is untouched.
- Deleting accounts.
- Closing open SignalR connections of a suspended user: the connection keeps its already-validated token until it reconnects, and then fails the check. This is documented as a known limit.
- Releasing Ask a Teacher threads claimed by a deactivated teacher. Admins can already reply to any thread (PRD §16), and SLA breach alerts reach admins.

**Deferred:**
- **Automatic invitation email.** Resend is wired only as an OTP channel (`ResendEmailOtpChannel`). A transactional "you are invited" email needs a new sender and template, and real delivery cannot be verified without the Resend keys (go-live #185). The invite flow is complete without it: the admin shares the `/accept-invite` link, and the invitee receives an email code through the existing OTP channel. The orchestrator opens the follow-up issue.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Suspend (student) vs deactivate (teacher/admin) | One state, `UserStatus.Suspended`, and one endpoint `POST /api/users/{id}/suspend`. The UI label is role-dependent: students «إيقاف»/«موقوف»; teachers and admins «تعطيل»/«معطّل». | This is how the prototype toggles (`A.toggleUser`). `UserStatus` already has exactly Active and Suspended, and every login handler already refuses a non-active user. |
| 2 | Is suspension reversible? | Yes: `POST /api/users/{id}/reactivate` («تفعيل»). | The prototype toggles both ways. Without it a mistaken suspension is permanent. |
| 3 | Last-admin rule | `User.Suspend(suspendedBy, activeAdminCount)` refuses an active Admin target when `activeAdminCount <= 1` (400 `LAST_ACTIVE_ADMIN`). The count is taken inside `IUserRepository.ExecuteInAdminRosterLockAsync`, a transaction holding `pg_advisory_xact_lock(106001)`. | Two admins deactivating each other at the same time would otherwise both pass a count of 2. |
| 4 | Self | `User.Suspend` refuses `Id == suspendedBy` (400 `USER_CANNOT_SUSPEND_SELF`). This check comes before the last-admin check. | Security requirement. |
| 5 | Revocation | Suspend calls `userManager.UpdateSecurityStampAsync(user)` in the same save, which invalidates every refresh token (`RefreshTokenService.ValidateTokenAsync` checks the stamp). Access tokens (JWT, `CoreJwt:ExpirationHours`) are checked on every request by `OnTokenValidated`, which sends `CheckUserActiveQuery`. That query caches `user-active:{id:N}` in `IMemoryCache` for `Users:ActiveStatusCacheSeconds` (default 30), and suspend and reactivate evict the key. On a multi-instance deployment, other instances notice within that window. | JWTs carry no stamp. A per-request check with a short cache is the cheapest fail-closed option, needs no schema change, and does not change the logout semantics. |
| 6 | Invite mechanism | Invite creates the Teacher or Admin with `userManager.CreateAsync(user)` and **no password**, so `IsInvitationPending => Role != Student && PasswordHash is null`. The invitee opens `/accept-invite`, requests an email code (existing `POST /api/auth/otp/send`), verifies it (existing `/otp/verify`) and posts `{ verificationId, password }` to `POST /api/auth/invitations/accept`, which calls `AddPasswordAsync` and signs them in. | This reuses Core.OTP and the email channel end to end (the Morabh `UserForgotPasswordHandler` pattern: OTP-verified identity, then an Identity password write). No secret travels in the link, and there is no token table. PRD §7.1 says teachers and admins sign in with a password, and they still do afterwards. |
| 7 | Invitation expiry | None: the account exists until accepted. An admin can deactivate a pending invitee. | No product rule; keeps the model stateless. |
| 8 | Invitation link | The dialog's success view shows the absolute `/accept-invite` URL (built from `router.buildLocation`) with a «نسخ الرابط» button. The email is **not** put in the URL. | Keeps PII out of URLs and logs. |
| 9 | Complimentary grant rules | Only a Student (404 `STUDENT_NOT_FOUND`). Plan and period must be sold by the catalogue (`SubscriptionsOptions.PriceFor`), otherwise 400 `COMPLIMENTARY_PERIOD_UNAVAILABLE`; the months come from it. Ask a Teacher needs an entitled Base (400 `COMPLIMENTARY_REQUIRES_BASE`). A plan the student already holds (entitled) is refused (400 `COMPLIMENTARY_PLAN_ALREADY_ACTIVE`). It starts now: `Subscription.Start(..., paymobReference: null, createdBy: adminId)`. | This is what `docs/subscriptions.md` already promises for #106. Refusing a held plan keeps paid and free time separate for refunds (`RevokePaidPeriod`). The prototype only offers grant when the plan is not held. |
| 10 | "Complimentary" marker | `PaymobReference is null`. No new column and no migration. | Every paid path sets a reference (the transaction id). MRR already counts such a subscription as 0 (`docs/dashboard.md`). |
| 11 | PRD rule 12 vs complimentary grants | Amend PRD §11.2 and §17 rule 12: entitlement changes only through Paymob-verified events **or an admin complimentary grant (§10.4)**. | Otherwise the doc and the code diverge (docs-sync). |
| 12 | PII | List and profile return `maskedPhone` (`010*****678`: first 3 and last 3 characters) and `maskedEmail` (`m***@example.test`). Full values are never returned by these endpoints. Search matches the display name (case-insensitive contains), the **exact** phone number or the **exact** email (normalised). | Minimal PII. Exact-match search on contacts cannot be used to enumerate them. |
| 13 | Route shape | `api/users` (new `UsersController`) for the cross-role list and state changes; `api/students/{studentId}/…` (existing `StudentsController`) for student-only reads and the grant; `api/auth/invitations/accept` on `AuthController`. The existing `GET /api/teachers` and the assign and unassign endpoints are reused unchanged. | Keeps resource-oriented routes and reuses #57. |
| 14 | Policies | List, invite, suspend, reactivate, profile and grant use `DefaultCodes.UsersManage`. Student progress and history use `DefaultCodes.ProgressViewAny` (PRD §16 "View any student's progress"). Both are Admin only. No new policy. | They already exist and are tested in `PermissionMatrixPolicyTests`. |
| 15 | Reusing progress logic | Move the bodies of the three existing progress handlers into static loaders in `Progress/Shared` (`SubjectProgressLoader`, `WeakSpotsLoader`, `SessionHistoryLoader`), the `StudentEntitlementLoader` pattern. The existing handlers call them with the current user; the new admin handlers call them with the route's student id after `StudentLookup.GetAsync` (404 for a non-student). | No duplicated queries; the existing handler tests keep passing unchanged. |
| 16 | Admin progress payload | `GET /api/students/{id}/progress` returns `StudentProgressResult(Subjects, WeakSpots)`. History is paged separately. The headline counter and streak are not shown. | Two requests on the page instead of four. The prototype admin view is the progress page minus student actions. |
| 17 | Web reuse of progress components | Not reused: the student components link to student routes (`/student/lesson/...`, quiz results). New admin components live in `features/users` and reuse only `MasteryBar` (mastery barrel), `ContentErrorState`/`ContentListSkeleton` (content barrel), `Pagination` and `formatDate`. | Avoids admins navigating into student-only routes. |
| 18 | Where grant appears | In the students list row (as in the prototype: «منح الأساسية» when the tier is Free, «منح اسأل معلّم» when Base without Ask) **and** on the student page. Both open the same `GrantPlanDialog`, with the plan preselected and the period chosen from `GET /api/plans`. | Follows the prototype, and the detail page needs it too. |
| 19 | Audit | `User.Invite` (id from result), `User.Suspend`, `User.Reactivate` (id from command), and `Subscription.GrantComplimentary` (id from result, with a diff because `Subscription` is an `IAuditedEntity`). `User` stays non-audited-entity (docs/audit-log.md "PII entities"), so the user rows carry no diff. `AcceptInvitation` is auth and is not audited, like login and register. | Existing audit policy. |
| 20 | Last-admin integration test | Unit tests only. The integration DB is shared by every parallel test class and always holds many active admins, so "exactly one active admin" cannot be arranged. Self-suspension is integration-tested. | Determinism. |
| 21 | Admin i18n budget | A new `users` namespace, not in `app/i18n.ts`, registered with `addResourceBundle` at module top of `UsersPage.tsx` and `StudentDetailPage.tsx` (both lazy route chunks). A new `admin-users` entry goes in `web/scripts/perf/budgets.json`. The accept-invite strings live in the global `session` namespace, because that page is public. | The #105 pattern (`docs/performance.md` §4). |
| 22 | Result on suspend and reactivate | `200` with no body. The web invalidates the list and profile queries. | Mirrors `UnassignTeacherSubject`. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Identity/User.cs` | `public class User` → `public partial class User`. Nothing else. |
| `api/Elmanhg.Domain/Identity/IUserRepository.cs` | Add `Task ExecuteInAdminRosterLockAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);` |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// USERS` add `UserCannotSuspendSelf`, `LastActiveAdmin`, `UserNotSuspended`. Under `// SUBSCRIPTIONS` add `ComplimentaryRequiresBase`, `ComplimentaryPlanAlreadyActive` (values in Error codes). |
| `api/Elmanhg.Domain/Subscriptions/StudentEntitlement.cs` | Add `EnsureCanGrant` (see Domain behaviour). |
| `api/Elmanhg.Domain/Subscriptions/SubscriptionEntitlementSpecification.cs` | Add `EntitledForStudents` (see Domain behaviour). |
| `api/Elmanhg.Infrastructure/Identity/UserRepository.cs` | Implement `ExecuteInAdminRosterLockAsync` (see Domain behaviour). |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add a `// USERS` group with the Application codes from Error codes, and `StudentIdRequired`/`StudentNotFound` under `// STUDENTS`, `Complimentary*` under `// SUBSCRIPTIONS`, `InvitationNotFound`/`PasswordRejected` under `// AUTH`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<UsersOptions>().BindConfiguration(UsersOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` after the `StudentsOptions` line. |
| `api/Elmanhg.Application/Progress/GetSubjectProgress/GetSubjectProgressHandler.cs` | Body after the user guard becomes `return await SubjectProgressLoader.LoadAsync(questionMasteryRepository, sessionRepository, subjectRepository, unitRepository, currentUserService.UserId.Value, cancellationToken).ConfigureAwait(false);` |
| `api/Elmanhg.Application/Progress/GetWeakSpots/GetWeakSpotsHandler.cs` | Body after the guard becomes `return await WeakSpotsLoader.LoadAsync(questionMasteryRepository, lessonRepository, subjectRepository, progressOptions.Value, currentUserService.UserId.Value, cancellationToken).ConfigureAwait(false);` |
| `api/Elmanhg.Application/Progress/GetSessionHistory/GetSessionHistoryHandler.cs` | Body after the guard becomes `return await SessionHistoryLoader.LoadAsync(sessionRepository, lessonRepository, unitRepository, currentUserService.UserId.Value, request.Kind, request.PageNumber, request.PageSize, cancellationToken).ConfigureAwait(false);` |
| `api/Elmanhg.Api/Controllers/Students/StudentsController.cs` | Add four actions (API surface). |
| `api/Elmanhg.Api/Controllers/Students/Requests.cs` | Add `public sealed record GrantComplimentarySubscriptionRequest(SubscriptionPlan Plan, BillingPeriod Period);` |
| `api/Elmanhg.Api/Controllers/Auth/AuthController.cs` | Add the `AcceptInvitation` action (API surface). |
| `api/Elmanhg.Api/Program.cs` | After `builder.Services.AddElmanhgRealtime();` add `builder.Services.AddActiveUserTokenValidation();` |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | Add a key for every new code (Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | Add `"Users": { "ListMaxPageSize": 100, "SearchMaxLength": 256, "ActiveStatusCacheSeconds": 30 },` after `"Students"`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["Users:ListMaxPageSize"] = "100"`, `["Users:SearchMaxLength"] = "256"`, `["Users:ActiveStatusCacheSeconds"] = "30"` to the in-memory collection. |
| `api/Elmanhg.Tests/Domain/Identity/UserTests.cs` | Add the tests listed (modify: add only). |
| `api/Elmanhg.Tests/Domain/Subscriptions/StudentEntitlementTests.cs` | Add the tests listed (add only). |
| `api/Elmanhg.Tests/Domain/Subscriptions/SubscriptionEntitlementSpecificationTests.cs` | Add the tests listed (add only). |
| `web/src/routes/admin/users.tsx` | Replace the placeholder: `createFileRoute('/admin/users')({ validateSearch: usersSearchSchema, component: UsersPage })`, imported from `@/features/users`. |
| `web/src/routeTree.gen.ts` | Regenerated (new routes). |
| `web/src/shared/api/generated/**` | `npm --prefix web run gen:api`. |
| `web/src/features/session/index.ts` | Export `AcceptInvitePage`. |
| `web/src/features/session/i18n/en.json`, `ar.json` | Add the `acceptInvite.*`, `fields.newPassword`, `fields.confirmPassword`, `actions.setPassword` and `validation.passwordMismatch` keys (Web copy). |
| `web/src/shared/i18n/en.json`, `ar.json` | Add `errors.<CODE>` for every new code (same text as the resx). |
| `web/scripts/perf/budgets.json` | Add `{ "name": "admin-users", "entries": ["index.html", "src/routes/admin/route.tsx?tsr-split=component", "src/routes/admin/users.tsx?tsr-split=component"], "maxKb": <measured × 1.05 rounded up to the next 5> }`. |
| `postman/elmanhg.postman_collection.json` | New folder "Users" in order: List students, Invite teacher, List teachers, Assign subject (existing request moved or duplicated), Suspend user, Reactivate user. Under "Students": Get student profile, Get student progress, Get student sessions, Grant complimentary subscription. Under "Auth": Accept invitation, after the OTP send and verify requests. |
| `docs/PRD.md` | §7.1 sign-in paragraph: add «An invited teacher or admin sets their first password at `/accept-invite` after proving the email with a one-time code.» §10.4: rewrite the bullets (see Definition of done). §11.2 bullet 2 and §17 rule 12: add "or an admin's complimentary grant (§10.4)". |
| `docs/subscriptions.md` | Replace the line "**#106** grants complimentary plans…" with "**#106** (done): complimentary grants (see Complimentary grants)". Add a section `## Complimentary grants` stating rules 9 and 10 from Decisions, the endpoint and the audit action. Add a row to the API table. |
| `docs/audit-log.md` | Add four rows to "Audited commands" (Decision 19). Under "Not audited" add `AcceptInvitation` to Auth and `CheckUserActive` (a read). |
| `docs/progress.md` | Add an `## Admin view` section: the two admin endpoints, their policy `Progress.ViewAny`, 404 `STUDENT_NOT_FOUND`, and that they use the same loaders. |
| `docs/performance.md` | §3 table: add an `admin-users` row. §4 "Admin-only strings on demand": add that the `users` namespace is registered the same way by `UsersPage` and `StudentDetailPage`. |
| `docs/claude-design-prompt.md` | §4 Admin: replace the `#/admin/users` bullet with the full description (Web layout). Under **Landing** add an `/accept-invite` bullet. |
| `docs/prototype.md` | Admin bullet "Users page": add "The product also has search, invitations (link plus email code), reactivation, and no self-deactivation; the prototype does not simulate them." |

## Files to create

### API — Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Identity/User.Administration.cs` | partial class | `namespace Elmanhg.Domain.Identity; public partial class User` with `IsInvitationPending`, `CanBeSuspendedBy`, `Suspend(Guid, int)`, `Reactivate(Guid)`. Bodies in Domain behaviour. |

### API — Application
| # | Path | Type | Contract |
|---|------|------|----------|
| 2 | `api/Elmanhg.Application/Shared/Options/UsersOptions.cs` | options | `public sealed class UsersOptions { public const string SectionName = "Users"; [Range(1, 100)] public int ListMaxPageSize { get; set; } = 100; [Range(1, 256)] public int SearchMaxLength { get; set; } = 256; [Range(0, 300)] public int ActiveStatusCacheSeconds { get; set; } = 30; }`. 0 disables the cache. |
| 3 | `api/Elmanhg.Application/Users/Shared/ContactMask.cs` | static | `namespace Elmanhg.Application.Users.Shared; public static class ContactMask`. Constants `private const int VisiblePhoneCharacters = 3;` (comment: "privacy rule: operators recognise a number by its prefix and last digits") and `private const char MaskCharacter = '*';`. `public static string? MaskPhone(string? phone)`: null or whitespace → null; `phone.Length <= VisiblePhoneCharacters * 2` → `new string('*', phone.Length)`; else `phone[..3] + new string('*', phone.Length - 6) + phone[^3..]`. `public static string? MaskEmail(string? email)`: null or whitespace → null; `var at = email.IndexOf('@', StringComparison.Ordinal)`; `at <= 0` → `"***"`; else `email[0] + "***" + email[at..]`. |
| 4 | `api/Elmanhg.Application/Users/Shared/UserActiveCacheKey.cs` | static | `public static class UserActiveCacheKey { public static string For(Guid userId) => string.Create(CultureInfo.InvariantCulture, $"user-active:{userId:N}"); }` |
| 5 | `api/Elmanhg.Application/Users/Shared/UserSummaryResult.cs` | result (admin) | `public sealed record UserSummaryResult(Guid Id, string DisplayName, UserRole Role, UserStatus Status, string? MaskedPhone, string? MaskedEmail, bool InvitationPending, bool CanSuspend, DateTimeOffset CreationDate, PlanTier? Tier, bool HasAskTeacher, List<Guid> SubjectIds);` `Tier` is non-null for students only; `SubjectIds` is empty except for teachers. |
| 6 | `api/Elmanhg.Application/Users/Shared/UserSummaryResultGenerator.cs` | static | `public static UserSummaryResult Generate(User user, Guid actorId, int activeAdminCount, StudentEntitlement? entitlement, List<Guid> subjectIds)` → `new(user.Id, user.DisplayName, user.Role, user.Status, ContactMask.MaskPhone(user.PhoneNumber), ContactMask.MaskEmail(user.Email), user.IsInvitationPending, user.CanBeSuspendedBy(actorId, activeAdminCount), user.CreationDate, user.Role == UserRole.Student ? (entitlement ?? StudentEntitlement.Free).Tier : null, user.Role == UserRole.Student && (entitlement?.HasAskTeacher ?? false), subjectIds)`. |
| 7 | `api/Elmanhg.Application/Users/GetUsers/GetUsersQuery.cs` | query | `public sealed record GetUsersQuery(UserRole Role, string? Search, UserStatus? Status, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<UserSummaryResult>>;` |
| 8 | `api/Elmanhg.Application/Users/GetUsers/GetUsersFilter.cs` | static | `public static Expression<Func<User, bool>> Build(GetUsersQuery query)`. Locals: `role = query.Role`, `status = query.Status`, `term = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim()`, `lowered = term?.ToLowerInvariant()`, `normalizedEmail = term?.ToUpperInvariant()`. Returns `x => x.Role == role && (status == null || x.Status == status) && (term == null || x.DisplayName.ToLower().Contains(lowered!) || x.PhoneNumber == term || x.NormalizedEmail == normalizedEmail)`. |
| 9 | `api/Elmanhg.Application/Users/GetUsers/GetUsersValidator.cs` | validator | ctor `(IOptions<UsersOptions> usersOptions)`. `PageNumber`: `ValidateMin(1, ErrorCodes.UserListPageNumberInvalid)`. `PageSize`: `ValidateRange(1, options.ListMaxPageSize, ErrorCodes.UserListPageSizeInvalid)`. `Role`: `.IsInEnum().WithErrorCode(ErrorCodes.UserListRoleInvalid)`. `Status`: `.IsInEnum().WithErrorCode(ErrorCodes.UserListStatusInvalid)`. `Search`: `ValidateMaxLength(options.SearchMaxLength, ErrorCodes.UserListSearchTooLong)`. |
| 10 | `api/Elmanhg.Application/Users/GetUsers/GetUsersHandler.cs` | handler | ctor `(IUserRepository userRepository, ISubscriptionRepository subscriptionRepository, ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider)`. Steps are listed below this table. |
| 11 | `api/Elmanhg.Application/Users/InviteUser/InviteUserCommand.cs` | command | `public sealed record InviteUserCommand(UserRole Role, string DisplayName, string Email) : IRequest<InviteUserResult>, IAuditableCommand { AuditAction => "User.Invite"; AuditResourceType => "User"; AuditResourceId => null; }` |
| 12 | `api/Elmanhg.Application/Users/InviteUser/InviteUserResult.cs` | result (admin) | `public sealed record InviteUserResult(Guid UserId) : IAuditableResult { Guid? IAuditableResult.AuditResourceId => UserId; }` |
| 13 | `api/Elmanhg.Application/Users/InviteUser/InviteUserValidator.cs` | validator | ctor `(IOptions<AuthOptions> authOptions)`. `Role`: `.Must(x => x is UserRole.Teacher or UserRole.Admin).WithErrorCode(ErrorCodes.UserInviteRoleInvalid)`. `DisplayName`: `ValidateRequired(ErrorCodes.DisplayNameRequired)`, `.ValidateMaxLength(options.DisplayNameMaxLength, ErrorCodes.DisplayNameTooLong)`. `Email`: `ValidateRequired(ErrorCodes.EmailRequired)`, `.ValidateEmail(ErrorCodes.EmailInvalid)`, `.ValidateMaxLength(options.EmailMaxLength, ErrorCodes.EmailTooLong)`. |
| 14 | `api/Elmanhg.Application/Users/InviteUser/InviteUserHandler.cs` | handler | ctor `(UserManager<User> userManager, ICurrentUserService currentUserService)`. Steps: (1) user guard → 401 `UserNotAuthenticated`. (2) `var email = request.Email.Trim(); var displayName = request.DisplayName.Trim();` (3) `FindByEmailAsync(email)` not null → `ConflictCoreException(ErrorCodes.EmailAlreadyRegistered)`. (4) `var user = request.Role == UserRole.Admin ? User.CreateAdmin(displayName, email) : User.CreateTeacher(displayName, email);` (5) `var result = await userManager.CreateAsync(user)`; `!Succeeded` → `BadRequestCoreException(ErrorCodes.UserCreationFailed, innerException: new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code))))`. (6) `return new InviteUserResult(user.Id);` |
| 15 | `api/Elmanhg.Application/Users/SuspendUser/SuspendUserCommand.cs` | command | `public sealed record SuspendUserCommand(Guid UserId) : IRequest, IAuditableCommand { AuditAction => "User.Suspend"; AuditResourceType => "User"; AuditResourceId => UserId; }` |
| 16 | `api/Elmanhg.Application/Users/SuspendUser/SuspendUserValidator.cs` | validator | `UserId`: `ValidateRequired(ErrorCodes.UserIdRequired)`. |
| 17 | `api/Elmanhg.Application/Users/SuspendUser/SuspendUserHandler.cs` | handler | ctor `(UserManager<User> userManager, IUserRepository userRepository, ICurrentUserService currentUserService, IMemoryCache memoryCache)`. Steps: (1) user guard → 401. (2) `var actorId = currentUserService.UserId.Value;` (3) `await userRepository.ExecuteInAdminRosterLockAsync(async token => { … }, cancellationToken).ConfigureAwait(false);`, where the callback does: (a) `var user = await userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.UserNotFound);` (b) `var activeAdminCount = user.Role == UserRole.Admin ? await userRepository.CountAsync(token, x => x.Role == UserRole.Admin && x.Status == UserStatus.Active).ConfigureAwait(false) : 0;` (c) `user.Suspend(actorId, activeAdminCount);` (d) `var result = await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false); if (!result.Succeeded) { throw new ConflictCoreException(ErrorCodes.UserModifiedConcurrently); }`. (4) `memoryCache.Remove(UserActiveCacheKey.For(request.UserId));` |
| 18 | `api/Elmanhg.Application/Users/ReactivateUser/ReactivateUserCommand.cs` | command | `public sealed record ReactivateUserCommand(Guid UserId) : IRequest, IAuditableCommand { AuditAction => "User.Reactivate"; AuditResourceType => "User"; AuditResourceId => UserId; }` |
| 19 | `api/Elmanhg.Application/Users/ReactivateUser/ReactivateUserValidator.cs` | validator | `UserId`: `ValidateRequired(ErrorCodes.UserIdRequired)`. |
| 20 | `api/Elmanhg.Application/Users/ReactivateUser/ReactivateUserHandler.cs` | handler | ctor `(UserManager<User> userManager, ICurrentUserService currentUserService, IMemoryCache memoryCache)`. Steps: (1) guard → 401. (2) `FindByIdAsync` null → `NotFoundCoreException(ErrorCodes.UserNotFound)`. (3) `user.Reactivate(currentUserService.UserId.Value);` (4) `var result = await userManager.UpdateAsync(user)`; `!Succeeded` → `ConflictCoreException(ErrorCodes.UserModifiedConcurrently)`. (5) `memoryCache.Remove(UserActiveCacheKey.For(user.Id));` |
| 21 | `api/Elmanhg.Application/Users/CheckUserActive/CheckUserActiveQuery.cs` | query | `public sealed record CheckUserActiveQuery(Guid UserId) : IRequest<bool>;` |
| 22 | `api/Elmanhg.Application/Users/CheckUserActive/CheckUserActiveHandler.cs` | handler | ctor `(IUserRepository userRepository, IMemoryCache memoryCache, IOptions<UsersOptions> usersOptions)`. Steps: (1) `var key = UserActiveCacheKey.For(request.UserId); if (memoryCache.TryGetValue(key, out bool cached)) { return cached; }` (2) `var user = await userRepository.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken, asNoTracking: true).ConfigureAwait(false);` (3) `var active = user is { IsActive: true };` (4) `var seconds = usersOptions.Value.ActiveStatusCacheSeconds; if (seconds > 0) { memoryCache.Set(key, active, TimeSpan.FromSeconds(seconds)); }` (5) `return active;` |
| 23 | `api/Elmanhg.Application/Auth/AcceptInvitation/AcceptInvitationCommand.cs` | command | `public sealed record AcceptInvitationCommand(Guid VerificationId, string Password) : IRequest<AuthResult>;` (not auditable: auth). |
| 24 | `api/Elmanhg.Application/Auth/AcceptInvitation/AcceptInvitationValidator.cs` | validator | ctor `(IOptions<IdentityOptions> identityOptions)`. `VerificationId`: `ValidateRequired(ErrorCodes.OtpVerificationIdInvalidFormat)`. `Password`: `ValidateRequired(ErrorCodes.PasswordIsRequired)`, `.ValidateMinLength(identity.Password.RequiredLength, ErrorCodes.PasswordTooShort)`, `.ValidateHasNumber(ErrorCodes.PasswordMustContainDigit)` (mirrors `RegisterWithEmailValidator`). |
| 25 | `api/Elmanhg.Application/Auth/AcceptInvitation/AcceptInvitationHandler.cs` | handler | ctor `(UserManager<User> userManager, IOtpRepository otpRepository, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService)`. Steps: (1) `var otp = await otpRepository.FindByVerificationId(request.VerificationId, cancellationToken)`; `otp is null \|\| otp.RecipientType != OtpRecipientType.Email` → `BadRequestCoreException(ErrorCodes.OtpInvalid)`. (2) `otp.MarkUsed();` (Core throws `OTP_NOT_VERIFIED`/`OTP_ALREADY_USED`/`OTP_EXPIRED`, 400). (3) `var user = await userManager.FindByEmailAsync(otp.Recipient)`; `user is null \|\| !user.IsInvitationPending` → `NotFoundCoreException(ErrorCodes.InvitationNotFound)`. (4) `!user.IsActive` → `ForbiddenCoreException(ErrorCodes.UserSuspended)`. (5) `var result = await userManager.AddPasswordAsync(user, request.Password)`; `!Succeeded` → `BadRequestCoreException(ErrorCodes.PasswordRejected, innerException: new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code))))`. (6) `await otpRepository.SaveChangesAsync(cancellationToken)`. (7) access token `tokenService.GenerateTokenAsync(user.GetUserClaims())`, refresh `await refreshTokenService.GenerateTokenAsync(user, cancellationToken)`, `return AuthResultGenerator.Generate(user, accessToken, refreshToken);` Morabh source: `Morabh.Application/Users/Auth/UserForgotPassword/UserForgotPasswordHandler.cs` (OTP-verified identity, then an Identity password write, then tokens). |
| 26 | `api/Elmanhg.Application/Students/Shared/StudentLookup.cs` | static | `public static async Task<User> GetAsync(IUserRepository userRepository, Guid studentId, CancellationToken cancellationToken)` → `FirstOrDefaultAsync(x => x.Id == studentId && x.Role == UserRole.Student, cancellationToken, asNoTracking: true)` `?? throw new NotFoundCoreException(ErrorCodes.StudentNotFound)`. |
| 27 | `api/Elmanhg.Application/Students/Shared/StudentProfileResult.cs` | result (admin) | `public sealed record StudentProfileResult(Guid Id, string DisplayName, string? MaskedPhone, string? MaskedEmail, UserStatus Status, bool CanSuspend, DateTimeOffset CreationDate, DateTimeOffset? OnboardedAt, List<string> SubjectInterests, PlanTier Tier, bool HasAskTeacher, List<AdminSubscriptionResult> Subscriptions);` |
| 28 | `api/Elmanhg.Application/Students/Shared/StudentProfileResultGenerator.cs` | static | `public static StudentProfileResult Generate(User student, IReadOnlyList<Subject> interests, IReadOnlyList<Subscription> subscriptions, StudentEntitlement entitlement, TimeSpan gracePeriod)`. `SubjectInterests` = `interests.OrderBy(x => x.Order).Select(x => x.Name).ToList()`. `CanSuspend` = `student.IsActive`. `Subscriptions` = `subscriptions.Select(x => AdminSubscriptionResultGenerator.Generate(x, gracePeriod)).ToList()`. |
| 29 | `api/Elmanhg.Application/Students/GetStudentProfile/GetStudentProfileQuery.cs` | query | `public sealed record GetStudentProfileQuery(Guid StudentId) : IRequest<StudentProfileResult>;` |
| 30 | `api/Elmanhg.Application/Students/GetStudentProfile/GetStudentProfileValidator.cs` | validator | `StudentId`: `ValidateRequired(ErrorCodes.StudentIdRequired)`. |
| 31 | `api/Elmanhg.Application/Students/GetStudentProfile/GetStudentProfileHandler.cs` | handler | ctor `(IUserRepository userRepository, ISubscriptionRepository subscriptionRepository, ISubjectRepository subjectRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider)`. Steps: (1) `var student = await StudentLookup.GetAsync(userRepository, request.StudentId, cancellationToken)`. (2) `var subscriptions = await subscriptionRepository.FindAsync(x => x.StudentId == student.Id, cancellationToken, orderBy: q => q.OrderByDescending(x => x.CurrentPeriodStart).ThenByDescending(x => x.Id), asNoTracking: true)`. (3) `var interestIds = student.SubjectInterestIds; List<Subject> interests = interestIds.Count == 0 ? [] : await subjectRepository.FindAsync(x => interestIds.Contains(x.Id), cancellationToken, asNoTracking: true)`. (4) `var options = subscriptionsOptions.Value; var entitlement = StudentEntitlement.Resolve(subscriptions, timeProvider.GetUtcNow(), options.GracePeriod);` (5) `return StudentProfileResultGenerator.Generate(student, interests, subscriptions, entitlement, options.GracePeriod);` |
| 32 | `api/Elmanhg.Application/Subscriptions/Shared/AdminSubscriptionResult.cs` | result (admin) | `public sealed record AdminSubscriptionResult(Guid Id, SubscriptionPlan Plan, BillingPeriod Period, SubscriptionStatus Status, DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd, DateTimeOffset? EntitledUntil, bool IsComplimentary) : IAuditableResult { Guid? IAuditableResult.AuditResourceId => Id; }` |
| 33 | `api/Elmanhg.Application/Subscriptions/Shared/AdminSubscriptionResultGenerator.cs` | static | `public static AdminSubscriptionResult Generate(Subscription subscription, TimeSpan gracePeriod) => new(subscription.Id, subscription.Plan, subscription.Period, subscription.Status, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.EntitledUntil(gracePeriod), subscription.PaymobReference is null);` |
| 34 | `api/Elmanhg.Application/Subscriptions/GrantComplimentarySubscription/GrantComplimentarySubscriptionCommand.cs` | command | `public sealed record GrantComplimentarySubscriptionCommand(Guid StudentId, SubscriptionPlan Plan, BillingPeriod Period) : IRequest<AdminSubscriptionResult>, IAuditableCommand { AuditAction => "Subscription.GrantComplimentary"; AuditResourceType => "Subscription"; AuditResourceId => null; }` |
| 35 | `api/Elmanhg.Application/Subscriptions/GrantComplimentarySubscription/GrantComplimentarySubscriptionValidator.cs` | validator | `StudentId`: `ValidateRequired(ErrorCodes.StudentIdRequired)`. `Plan`: `.IsInEnum().WithErrorCode(ErrorCodes.ComplimentaryPlanInvalid)`. `Period`: `.IsInEnum().WithErrorCode(ErrorCodes.ComplimentaryPeriodInvalid)`. |
| 36 | `api/Elmanhg.Application/Subscriptions/GrantComplimentarySubscription/GrantComplimentarySubscriptionHandler.cs` | handler | ctor `(IUserRepository userRepository, ISubscriptionRepository subscriptionRepository, ICurrentUserService currentUserService, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider)`. Steps: (1) guard → 401. (2) `var student = await StudentLookup.GetAsync(userRepository, request.StudentId, cancellationToken)`. (3) `var options = subscriptionsOptions.Value; var price = options.PriceFor(request.Plan, request.Period) ?? throw new BadRequestCoreException(ErrorCodes.ComplimentaryPeriodUnavailable);` (4) `var now = timeProvider.GetUtcNow();` (5) `var held = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledFor(student.Id, now, options.GracePeriod), cancellationToken, asNoTracking: true)`. (6) `StudentEntitlement.Resolve(held, now, options.GracePeriod).EnsureCanGrant(request.Plan);` (7) `var subscription = Subscription.Start(student.Id, request.Plan, request.Period, price.Months, now, paymobReference: null, createdBy: currentUserService.UserId.Value);` (8) `AddAsync`, then `SaveChangesAsync` once. (9) `return AdminSubscriptionResultGenerator.Generate(subscription, options.GracePeriod);` |
| 37 | `api/Elmanhg.Application/Progress/Shared/SubjectProgressLoader.cs` | static | `public static async Task<List<SubjectProgressResult>> LoadAsync(IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, Guid studentId, CancellationToken cancellationToken)`. The body is moved verbatim from `GetSubjectProgressHandler` (lines after the guard, `userId` → `studentId`). |
| 38 | `api/Elmanhg.Application/Progress/Shared/WeakSpotsLoader.cs` | static | `public static async Task<WeakSpotsResult> LoadAsync(IQuestionMasteryRepository questionMasteryRepository, ILessonRepository lessonRepository, ISubjectRepository subjectRepository, ProgressOptions options, Guid studentId, CancellationToken cancellationToken)`. The body is moved verbatim from `GetWeakSpotsHandler`. |
| 39 | `api/Elmanhg.Application/Progress/Shared/SessionHistoryLoader.cs` | static | `public static async Task<PageData<SessionHistoryItemResult>> LoadAsync(ISessionRepository sessionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, Guid studentId, SessionHistoryKind? kind, int pageNumber, int pageSize, CancellationToken cancellationToken)`. The body is moved verbatim from `GetSessionHistoryHandler` and still uses `GetSessionHistoryFilter.Build(studentId, kind)`. |
| 40 | `api/Elmanhg.Application/Progress/Shared/StudentProgressResult.cs` | result (admin) | `public sealed record StudentProgressResult(List<SubjectProgressResult> Subjects, WeakSpotsResult WeakSpots);` |
| 41 | `api/Elmanhg.Application/Progress/GetStudentProgress/GetStudentProgressQuery.cs` | query | `public sealed record GetStudentProgressQuery(Guid StudentId) : IRequest<StudentProgressResult>;` |
| 42 | `api/Elmanhg.Application/Progress/GetStudentProgress/GetStudentProgressValidator.cs` | validator | `StudentId`: `ValidateRequired(ErrorCodes.StudentIdRequired)`. |
| 43 | `api/Elmanhg.Application/Progress/GetStudentProgress/GetStudentProgressHandler.cs` | handler | ctor `(IUserRepository userRepository, IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, IOptions<ProgressOptions> progressOptions)`. Steps: (1) `var student = await StudentLookup.GetAsync(...)`. (2) `var subjects = await SubjectProgressLoader.LoadAsync(..., student.Id, ...)`. (3) `var weakSpots = await WeakSpotsLoader.LoadAsync(..., progressOptions.Value, student.Id, ...)`. (4) `return new StudentProgressResult(subjects, weakSpots);` Awaits are sequential (one DbContext). |
| 44 | `api/Elmanhg.Application/Progress/GetStudentSessionHistory/GetStudentSessionHistoryQuery.cs` | query | `public sealed record GetStudentSessionHistoryQuery(Guid StudentId, SessionHistoryKind? Kind, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<SessionHistoryItemResult>>;` |
| 45 | `api/Elmanhg.Application/Progress/GetStudentSessionHistory/GetStudentSessionHistoryValidator.cs` | validator | ctor `(IOptions<ProgressOptions> progressOptions)`. `StudentId`: `ValidateRequired(ErrorCodes.StudentIdRequired)`. `PageNumber`: `ValidateMin(1, ErrorCodes.SessionHistoryPageNumberInvalid)`. `PageSize`: `ValidateRange(1, options.HistoryMaxPageSize, ErrorCodes.SessionHistoryPageSizeInvalid)`. `Kind`: `.IsInEnum().WithErrorCode(ErrorCodes.SessionHistoryKindInvalid)`. |
| 46 | `api/Elmanhg.Application/Progress/GetStudentSessionHistory/GetStudentSessionHistoryHandler.cs` | handler | ctor `(IUserRepository userRepository, ISessionRepository sessionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository)`. Steps: (1) `StudentLookup.GetAsync`. (2) `return await SessionHistoryLoader.LoadAsync(sessionRepository, lessonRepository, unitRepository, student.Id, request.Kind, request.PageNumber, request.PageSize, cancellationToken)`. |

`GetUsersHandler.Handle` steps (row 10):
1. Guard the current user → 401 `UserNotAuthenticated`. `var actorId = currentUserService.UserId.Value;`
2. `var page = await userRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: GetUsersFilter.Build(request), orderBy: q => q.OrderByDescending(x => x.CreationDate).ThenByDescending(x => x.Id), asNoTracking: true)`.
3. `var ids = page.Items.Select(x => x.Id).ToList();`
4. `var activeAdminCount = request.Role == UserRole.Admin ? await userRepository.CountAsync(cancellationToken, x => x.Role == UserRole.Admin && x.Status == UserStatus.Active) : 0;`
5. Students, when `request.Role == UserRole.Student && ids.Count > 0`:
   - `now = timeProvider.GetUtcNow()` and `grace = subscriptionsOptions.Value.GracePeriod`;
   - `var subscriptions = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledForStudents(ids, now, grace), cancellationToken, asNoTracking: true)`;
   - `entitlements = subscriptions.GroupBy(x => x.StudentId).ToDictionary(g => g.Key, g => StudentEntitlement.Resolve(g, now, grace))`. Otherwise use an empty dictionary.
6. Teachers, when `request.Role == UserRole.Teacher && ids.Count > 0`: `var assignments = await teacherSubjectRepository.FindAsync(x => ids.Contains(x.TeacherId), cancellationToken, asNoTracking: true)`, then `subjectsByTeacher = assignments.GroupBy(x => x.TeacherId).ToDictionary(g => g.Key, g => g.Select(x => x.SubjectId).ToList())`. Otherwise use an empty dictionary.
7. Return `new PageData<UserSummaryResult> { Items = page.Items.Select(x => UserSummaryResultGenerator.Generate(x, actorId, activeAdminCount, entitlements.GetValueOrDefault(x.Id), subjectsByTeacher.GetValueOrDefault(x.Id) ?? [])).ToList(), PageNumber = page.PageNumber, PageSize = page.PageSize, TotalItems = page.TotalItems, TotalPages = page.TotalPages }`.
8. Keep the file under about 100 lines. If it is longer, move steps 5–6 into `private static` methods in the same file.

### API — Api layer
| # | Path | Type | Contract |
|---|------|------|----------|
| 47 | `api/Elmanhg.Api/Controllers/Users/UsersController.cs` | controller | `[ApiController] [Route("api/users")] [Authorize] public class UsersController(IMediator mediator) : ControllerBase`. Actions are in API surface. New; the Morabh `Morabh.APIs/Controllers` style of thin controllers. |
| 48 | `api/Elmanhg.Api/Authorization/ActiveUserTokenValidation.cs` | static | `public static class ActiveUserTokenValidation`. (a) `public static IServiceCollection AddActiveUserTokenValidation(this IServiceCollection services)`: `services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => { options.Events ??= new JwtBearerEvents(); options.Events.OnTokenValidated = OnTokenValidated; }); return services;` (b) `public static async Task OnTokenValidated(TokenValidatedContext context)`: `var claim = context.Principal?.FindFirst(CurrentUserService.Constants.UserIdClaimType)?.Value;` → `if (!Guid.TryParse(claim, out var userId)) { context.Fail(ErrorCodes.UserNotAuthenticated); return; }` → `var sender = context.HttpContext.RequestServices.GetRequiredService<ISender>();` → `if (!await sender.Send(new CheckUserActiveQuery(userId), context.HttpContext.RequestAborted).ConfigureAwait(false)) { context.Fail(ErrorCodes.UserSuspended); }`. It needs a one-line WHY comment: "JWTs outlive a suspension; this refuses a suspended or deleted user's access token on its next request." New, no Morabh equivalent. |

### Docs
| # | Path | Type | Contract |
|---|------|------|----------|
| 49 | `docs/user-administration.md` | doc | Sections listed in Definition of done (endpoints, rules, revocation, invitations, masking/search, complimentary pointer, options, error codes, audit). |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.UserListPageNumberInvalid` (App) | `USER_LIST_PAGE_NUMBER_INVALID` | GetUsersValidator | validation | 422 |
| `UserListPageSizeInvalid` | `USER_LIST_PAGE_SIZE_INVALID` | GetUsersValidator | validation | 422 |
| `UserListRoleInvalid` | `USER_LIST_ROLE_INVALID` | GetUsersValidator | validation | 422 |
| `UserListStatusInvalid` | `USER_LIST_STATUS_INVALID` | GetUsersValidator | validation | 422 |
| `UserListSearchTooLong` | `USER_LIST_SEARCH_TOO_LONG` | GetUsersValidator | validation | 422 |
| `UserIdRequired` | `USER_ID_REQUIRED` | Suspend/Reactivate validators | validation | 422 |
| `UserInviteRoleInvalid` | `USER_INVITE_ROLE_INVALID` | InviteUserValidator | validation | 422 |
| `UserModifiedConcurrently` | `USER_MODIFIED_CONCURRENTLY` | Suspend/Reactivate handlers | `ConflictCoreException` | 409 |
| `StudentIdRequired` | `STUDENT_ID_REQUIRED` | profile/progress/history/grant validators | validation | 422 |
| `StudentNotFound` | `STUDENT_NOT_FOUND` | `StudentLookup` | `NotFoundCoreException` | 404 |
| `ComplimentaryPlanInvalid` | `COMPLIMENTARY_PLAN_INVALID` | Grant validator | validation | 422 |
| `ComplimentaryPeriodInvalid` | `COMPLIMENTARY_PERIOD_INVALID` | Grant validator | validation | 422 |
| `ComplimentaryPeriodUnavailable` | `COMPLIMENTARY_PERIOD_UNAVAILABLE` | Grant handler | `BadRequestCoreException` | 400 |
| `InvitationNotFound` | `INVITATION_NOT_FOUND` | AcceptInvitation handler | `NotFoundCoreException` | 404 |
| `PasswordRejected` | `PASSWORD_REJECTED` | AcceptInvitation handler | `BadRequestCoreException` | 400 |
| Domain `UserCannotSuspendSelf` | `USER_CANNOT_SUSPEND_SELF` | `User.Suspend(Guid,int)` | `BusinessRuleViolationCoreException` | 400 |
| Domain `LastActiveAdmin` | `LAST_ACTIVE_ADMIN` | `User.Suspend(Guid,int)` | `BusinessRuleViolationCoreException` | 400 |
| Domain `UserNotSuspended` | `USER_NOT_SUSPENDED` | `User.Reactivate` | `BusinessRuleViolationCoreException` | 400 |
| Domain `ComplimentaryRequiresBase` | `COMPLIMENTARY_REQUIRES_BASE` | `StudentEntitlement.EnsureCanGrant` | `BusinessRuleViolationCoreException` | 400 |
| Domain `ComplimentaryPlanAlreadyActive` | `COMPLIMENTARY_PLAN_ALREADY_ACTIVE` | `StudentEntitlement.EnsureCanGrant` | `BusinessRuleViolationCoreException` | 400 |

The following existing codes are reused, not re-added: `USER_NOT_AUTHENTICATED`, `USER_NOT_FOUND`, `USER_ALREADY_SUSPENDED`, `USER_SUSPENDED`, `EMAIL_ALREADY_REGISTERED`, `USER_CREATION_FAILED`, `DISPLAY_NAME_*`, `EMAIL_*`, `PASSWORD_*`, `OTP_INVALID`, `OTP_VERIFICATION_ID_INVALID_FORMAT`, `SESSION_HISTORY_*`.

Resource strings (resx and web `common:errors`; Arabic without tashkeel):
| Code | en | ar |
|---|---|---|
| USER_LIST_PAGE_NUMBER_INVALID | Page number must be 1 or more. | رقم الصفحة يجب أن يكون 1 أو أكثر. |
| USER_LIST_PAGE_SIZE_INVALID | Page size is out of range. | حجم الصفحة خارج النطاق المسموح. |
| USER_LIST_ROLE_INVALID | Unknown user type. | نوع المستخدم غير معروف. |
| USER_LIST_STATUS_INVALID | Unknown account status. | حالة الحساب غير معروفة. |
| USER_LIST_SEARCH_TOO_LONG | Search text is too long. | نص البحث طويل جدا. |
| USER_ID_REQUIRED | User id is required. | معرف المستخدم مطلوب. |
| USER_INVITE_ROLE_INVALID | Only teachers and admins can be invited. | يمكن دعوة المعلمين والمديرين فقط. |
| USER_MODIFIED_CONCURRENTLY | This account was changed by someone else. Reload and try again. | تم تعديل هذا الحساب من شخص آخر. أعد التحميل وحاول مرة أخرى. |
| STUDENT_ID_REQUIRED | Student id is required. | معرف الطالب مطلوب. |
| STUDENT_NOT_FOUND | Student not found. | الطالب غير موجود. |
| COMPLIMENTARY_PLAN_INVALID | Choose a valid plan. | اختر باقة صحيحة. |
| COMPLIMENTARY_PERIOD_INVALID | Choose a valid period. | اختر مدة صحيحة. |
| COMPLIMENTARY_PERIOD_UNAVAILABLE | This plan is not offered for that period. | هذه الباقة غير متاحة لهذه المدة. |
| INVITATION_NOT_FOUND | There is no pending invitation for this email. | لا توجد دعوة معلقة لهذا البريد. |
| PASSWORD_REJECTED | This password was not accepted. Choose another one. | لم تقبل كلمة المرور هذه. اختر كلمة أخرى. |
| USER_CANNOT_SUSPEND_SELF | You cannot deactivate your own account. | لا يمكنك إيقاف حسابك. |
| LAST_ACTIVE_ADMIN | At least one active admin must remain. | يجب أن يبقى مدير نشط واحد على الأقل. |
| USER_NOT_SUSPENDED | This account is already active. | هذا الحساب نشط بالفعل. |
| COMPLIMENTARY_REQUIRES_BASE | Ask a Teacher needs an active Base plan first. | اسأل معلم يتطلب باقة أساسية نشطة أولا. |
| COMPLIMENTARY_PLAN_ALREADY_ACTIVE | The student already has this plan. | لدى الطالب هذه الباقة بالفعل. |

## Domain behaviour
`User.Administration.cs` (the partial class `User`):
```csharp
public bool IsInvitationPending => Role != UserRole.Student && PasswordHash is null;

public bool CanBeSuspendedBy(Guid actorId, int activeAdminCount) => Id != actorId && IsActive && !(Role == UserRole.Admin && activeAdminCount <= 1);

public void Suspend(Guid suspendedBy, int activeAdminCount)
{
    if (Id == suspendedBy)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.UserCannotSuspendSelf);
    }

    if (Role == UserRole.Admin && IsActive && activeAdminCount <= 1)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.LastActiveAdmin);
    }

    Suspend();
    UpdatedBy = suspendedBy;
}

public void Reactivate(Guid reactivatedBy)
{
    if (Status == UserStatus.Active)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.UserNotSuspended);
    }

    Status = UserStatus.Active;
    UpdatedBy = reactivatedBy;
    UpdationDate = DateTimeOffset.UtcNow;
}
```
- The existing parameterless `Suspend()` stays unchanged: it guards `USER_ALREADY_SUSPENDED` and stamps `UpdationDate`, and existing tests and helpers call it.
- `activeAdminCount` counts the active admins **including** the target.

`StudentEntitlement.EnsureCanGrant`:
```csharp
public void EnsureCanGrant(SubscriptionPlan plan)
{
    if (plan == SubscriptionPlan.AskTeacher && BaseSubscription is null)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.ComplimentaryRequiresBase);
    }

    if (Held(plan) is not null)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.ComplimentaryPlanAlreadyActive);
    }
}
```

`SubscriptionEntitlementSpecification.EntitledForStudents(List<Guid> studentIds, DateTimeOffset now, TimeSpan gracePeriod)`: the same predicate as `EntitledFor`, with `studentIds.Contains(x.StudentId)` in place of the equality.

`UserRepository.ExecuteInAdminRosterLockAsync`: this is the only raw SQL, and it is parameterised.
```csharp
// Serialises admin deactivations so two admins cannot deactivate each other past the last-admin rule; any fixed key unique to this lock.
private const long AdminRosterLockKey = 106001;

public async Task ExecuteInAdminRosterLockAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
{
    var strategy = context.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () =>
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdminRosterLockKey})", cancellationToken).ConfigureAwait(false);
        await operation(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }).ConfigureAwait(false);
}
```
`UserManager` shares the scoped `AppDbContext`, so `UpdateSecurityStampAsync` saves inside this transaction.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/users` (Name `GetUsers`) | `DefaultCodes.UsersManage` | `[FromQuery] UserRole role = UserRole.Student, string? search, UserStatus? status, int pageNumber = 1, int pageSize = 20` → `GetUsersQuery` | 200 `PageData<UserSummaryResult>`; 422 `USER_LIST_*` |
| POST | `/api/users/invitations` (`InviteUser`) | `UsersManage` | `[FromBody] InviteUserCommand` | 200 `InviteUserResult`; 409 `EMAIL_ALREADY_REGISTERED`; 422 |
| POST | `/api/users/{userId:guid}/suspend` (`SuspendUser`) | `UsersManage` | route | 200 empty; 400 `USER_CANNOT_SUSPEND_SELF` / `LAST_ACTIVE_ADMIN` / `USER_ALREADY_SUSPENDED`; 404 `USER_NOT_FOUND`; 409 |
| POST | `/api/users/{userId:guid}/reactivate` (`ReactivateUser`) | `UsersManage` | route | 200 empty; 400 `USER_NOT_SUSPENDED`; 404; 409 |
| GET | `/api/students/{studentId:guid}` (`GetStudentProfile`) | `UsersManage` | route | 200 `StudentProfileResult`; 404 `STUDENT_NOT_FOUND` |
| GET | `/api/students/{studentId:guid}/progress` (`GetStudentProgress`) | `ProgressViewAny` | route | 200 `StudentProgressResult`; 404 |
| GET | `/api/students/{studentId:guid}/sessions` (`GetStudentSessionHistory`) | `ProgressViewAny` | `[FromQuery] SessionHistoryKind? kind, int pageNumber = 1, int pageSize = 20` | 200 `PageData<SessionHistoryItemResult>`; 404; 422 |
| POST | `/api/students/{studentId:guid}/complimentary-subscriptions` (`GrantComplimentarySubscription`) | `UsersManage` | `[FromBody] GrantComplimentarySubscriptionRequest` → `new GrantComplimentarySubscriptionCommand(studentId, request.Plan, request.Period)` | 200 `AdminSubscriptionResult`; 400 `COMPLIMENTARY_*`; 404; 422 |
| POST | `/api/auth/invitations/accept` (`AcceptInvitation`) | `[AllowAnonymous]` + `[EnableRateLimiting(AuthRateLimitPolicies.Credentials)]` | `[FromBody] AcceptInvitationCommand` | 200 `AuthResult` via `SendWithRefreshCookie`; 400 `OTP_*` / `PASSWORD_REJECTED`; 403 `USER_SUSPENDED`; 404 `INVITATION_NOT_FOUND`; 422 |

Every action carries `[ProducesResponseType<T>(StatusCodes.Status200OK)]` (or the bare `StatusCodes.Status200OK` for the empty ones), mirroring `TeachersController`. The existing endpoints reused unchanged are `GET /api/teachers`, `POST`/`DELETE /api/teachers/{teacherId}/subjects/{subjectId}`, `GET /api/subjects`, `GET /api/plans`, and `POST /api/auth/otp/send` and `/otp/verify`.

## Web
**Files to create** (`web/src/…`):
| # | Path | Contract |
|---|------|----------|
| W1 | `routes/admin/student.$studentId.tsx` | `createFileRoute('/admin/student/$studentId')({ validateSearch: studentDetailSearchSchema, component: StudentDetailPage })` |
| W2 | `routes/accept-invite.tsx` | `createFileRoute('/accept-invite')({ beforeLoad: ({ context }) => { redirectSignedIn(context.sessionStore.get(), undefined); }, component: AcceptInvitePage })` |
| W3 | `features/users/index.ts` | exports `UsersPage`, `StudentDetailPage`, `usersSearchSchema`, `studentDetailSearchSchema` and their types. |
| W4 | `features/users/locales.ts` | `export function registerUsersLocales(): void`: `getI18n().addResourceBundle('ar'\|'en', 'users', …, true, true)`. The copy of `dashboard/locales.ts`. |
| W5 | `features/users/i18n/en.json`, `ar.json` | keys in Web copy. |
| W6 | `features/users/schemas/usersSearchSchema.ts` | `userListTabs = ['students','teachers','admins'] as const`; `// mirrors Users:SearchMaxLength` `userSearchMaxLength = 256`; `z.object({ tab: z.enum(userListTabs).optional().catch(undefined), q: z.string().trim().min(1).max(userSearchMaxLength).optional().catch(undefined), status: z.enum(['Active','Suspended']).optional().catch(undefined), page: z.coerce.number().int().min(1).optional().catch(undefined) })`; types `UsersSearch` and `UserListTab`. |
| W7 | `features/users/schemas/userFiltersSchema.ts` | `z.object({ q: z.string().trim().max(userSearchMaxLength, { error: 'users:validation.searchTooLong' }), status: z.enum(['', 'Active', 'Suspended']) })`; type `UserFiltersValues`. |
| W8 | `features/users/schemas/inviteUserSchema.ts` | `// mirrors Auth:DisplayNameMaxLength / Auth:EmailMaxLength`; `z.object({ displayName: z.string().trim().min(1, { error: 'validation.required' }).max(100, { error: 'users:validation.displayNameLength' }), email: z.email({ error: 'users:validation.email' }).max(256, { error: 'users:validation.emailLength' }) })`; type `InviteUserValues`. |
| W9 | `features/users/schemas/grantPlanSchema.ts` | `z.object({ plan: z.enum(['Base','AskTeacher'], { error: 'users:validation.planRequired' }), period: z.enum(['Monthly','Termly','Yearly'], { error: 'users:validation.periodRequired' }) })`; type `GrantPlanValues`. |
| W10 | `features/users/schemas/studentDetailSearchSchema.ts` | `z.object({ kind: z.enum(['Quiz','Exam']).optional().catch(undefined), page: z.coerce.number().int().min(1).optional().catch(undefined) })`. |
| W11 | `features/users/api/userListParams.ts` | `tabRole: Record<UserListTab, UserRole> = { students: 'Student', teachers: 'Teacher', admins: 'Admin' }`; `toGetUsersParams(search: UsersSearch): GetUsersParams` → `{ role: tabRole[search.tab ?? 'students'], pageNumber: search.page ?? 1, pageSize: 20, ...(search.q ? { search: search.q } : {}), ...(search.status ? { status: search.status } : {}) }`; `hasActiveFilters(search) => search.q !== undefined \|\| search.status !== undefined`. |
| W12 | `features/users/hooks/useUsersSearch.ts` | `getRouteApi('/admin/users')`; returns `{ search, tab, setTab(tab) /* navigate({ search: { tab } }) */, applyFilters(values: UserFiltersValues) /* page 1, keep tab */, clearFilters() /* keep tab only */, setPage(page) }`. |
| W13 | `features/users/hooks/useUserList.ts` | `useGetUsers(toGetUsersParams(search), { query: { placeholderData: keepPreviousData } })`. |
| W14 | `features/users/hooks/useUserStatus.ts` | Wraps `useSuspendUser` and `useReactivateUser`. `onSuccess`: invalidate `[getGetUsersQueryKey()[0]]` and `getGetStudentProfileQueryKey(userId)`, then `toast.success(t('status.suspendedToast'\|'status.reactivatedToast'))`. `onError`: `toast.error(t([\`common:errors.${code}\`, 'common:errors.UNHANDLED_EXCEPTION']))`. Returns `{ suspend(userId): Promise<void>, reactivate(userId): Promise<void>, isPending }`. |
| W15 | `features/users/hooks/useInviteUser.ts` | Wraps `useInviteUser`. `onSuccess` invalidates the users list and shows `toast.success(t('invite.createdToast'))`. Returns `{ invite(values & { role }): Promise<InviteUserResult>, isPending }`. Errors are handled by the dialog form (`applyServerErrors` through `Form`). |
| W16 | `features/users/hooks/useTeacherSubjects.ts` | `toggle(teacherId, subjectId, assign: boolean)` → `useAssignTeacherSubject` / `useUnassignTeacherSubject`. `onSuccess`: invalidate the users list and `toast.success(t('subjects.savedToast'))`. `onError`: an error toast from the code. Returns `{ toggle, isPending }`. |
| W17 | `features/users/hooks/useGrantPlan.ts` | Wraps `useGrantComplimentarySubscription`. `onSuccess`: invalidate `getGetStudentProfileQueryKey(studentId)` and the users list, then `toast.success(t('grant.doneToast'))`. Returns `{ grant(studentId, values): Promise<AdminSubscriptionResult>, isPending }`. |
| W18 | `features/users/hooks/useStudentHistorySearch.ts` | `getRouteApi('/admin/student/$studentId')`; returns `{ studentId, search, setKind(kind?), setPage(page), clearFilter() }`. |
| W19 | `features/users/pages/UsersPage.tsx` | `registerUsersLocales()` at module top. Layout: H1 `page.title` · `UserRoleTabs` · a header row with `tabs.<tab>` as H2, plus «دعوة معلّم»/«دعوة مدير» (primary `Button`) on the teachers and admins tabs · `UserFiltersForm` (keyed by `[q,status]`) · content (skeleton / error with retry / `UserListEmptyState` / `UserListTable` plus `Pagination` when `totalPages > 1`) · `UserStatusDialog`, `InviteUserDialog`, `GrantPlanDialog` driven by local target state. ≤120 lines; the error block mirrors `PaymentLogPage`. |
| W20 | `features/users/pages/StudentDetailPage.tsx` | `registerUsersLocales()` at module top. A back `Link` to `/admin/users` («المستخدمون»), H1 = `profile.displayName` (`student.title` while loading), `StudentProfileCard`, `StudentSubscriptionsCard`, `StudentProgressSection`, `StudentHistorySection`, `UserStatusDialog`, `GrantPlanDialog`. Profile loading → `ContentListSkeleton`; error → `ContentErrorState` (404 shows `common:errors.STUDENT_NOT_FOUND`). |
| W21 | `features/users/components/UserRoleTabs.tsx` | Props `{ value: UserListTab; onChange(tab) }`. Pill sub-tabs (`role="tablist"`, buttons `role="tab"` with `aria-selected`), like `PaymentLogTabs`. |
| W22 | `features/users/components/UserFiltersForm.tsx` | Props `{ search: UsersSearch; onApply(values); onClear() }`. RHF + `userFiltersSchema`. `TextField` «بحث» with hint `filters.searchHint`, a native `select` for status (الكل/نشط/موقوف), «تطبيق» and «مسح الفلاتر». |
| W23 | `features/users/components/UserListTable.tsx` | Props `{ tab: UserListTab; items: UserSummaryResult[]; currentUserId: string; onSuspend(u); onReactivate(u); onGrant(u, plan) }`. A card-wrapped `table` with an sr-only caption and headers by tab: students name/contact/plan/status/joined/actions; teachers name/contact/subjects/status/actions; admins name/contact/status/actions. |
| W24 | `features/users/components/UserListRow.tsx` | Props are the same, for one item. Contents: the name (students: `Link` to `/admin/student/$studentId`), `UserStatusBadge`, an «دعوة معلقة» pending badge when `invitationPending`, and masked contact in `type.mono dir="ltr"`. The status button reads «إيقاف»/«تعطيل» when active and «تفعيل» when suspended. When active and `!canSuspend`, the button is disabled with a visible hint: `status.selfHint` if `item.id === currentUserId`, else `status.lastAdminHint`. Students: «منح الأساسية» when `tier === 'Free'`, «منح اسأل معلّم» when `tier === 'Base' && !hasAskTeacher`. Teachers: `TeacherSubjectsCell`. Split out cells if the file passes 120 lines. |
| W25 | `features/users/components/UserStatusBadge.tsx` | Props `{ status: UserStatus; role: UserRole }`. Active → `bg-success text-surface` «نشط»; Suspended → `bg-danger text-surface` «موقوف» for students or «معطّل» otherwise. Pill `text-micro`. |
| W26 | `features/users/components/TeacherSubjectsCell.tsx` | Props `{ teacherId: string; subjectIds: string[] }`. `useGetSubjects()` renders one labelled checkbox per subject, checked when assigned. `onChange` → `toggle(teacherId, subject.id, checked)`. Inputs are disabled while pending. While subjects load, show caption `subjects.loading`. |
| W27 | `features/users/components/UserStatusDialog.tsx` | Props `{ target: { id; displayName; role; status } \| null; onOpenChange(open) }`. A confirm `Dialog`: title `status.suspendTitle`/`deactivateTitle`/`reactivateTitle` with `{ name }`, and a body that says sign-in stops at once and open sessions end. «تراجع» plus a confirm button (`danger` for suspend or deactivate, `primary` for reactivate). It uses `useUserStatus` and closes on success. |
| W28 | `features/users/components/InviteUserDialog.tsx` | Props `{ role: 'Teacher' \| 'Admin' \| null; onOpenChange(open) }`. Step 1: a form with `displayName` and `email`, and `serverErrorFields = { EMAIL_ALREADY_REGISTERED: 'email', EMAIL_INVALID: 'email', EMAIL_TOO_LONG: 'email', DISPLAY_NAME_REQUIRED: 'displayName', DISPLAY_NAME_TOO_LONG: 'displayName' }`. Step 2 (after success): `invite.doneBody` with `{ name, email }`, a read-only `dir="ltr"` link box built from `new URL(router.buildLocation({ to: '/accept-invite' }).href, document.baseURI).href`, «نسخ الرابط» (`navigator.clipboard.writeText` plus `toast(t('invite.copied'))`) and «تم». |
| W29 | `features/users/components/GrantPlanDialog.tsx` | Props `{ target: { studentId; displayName; plan: 'Base' \| 'AskTeacher' } \| null; onOpenChange(open) }`. Uses `useGetPlanCatalogue()` for the period options: Base → `catalogue.base.prices` («شهري»/«ترم»/«سنوي» with months); AskTeacher → Monthly only. RHF + `grantPlanSchema`, with plan prefilled from the target and shown as text. `serverErrorFields = { COMPLIMENTARY_PERIOD_UNAVAILABLE: 'period', COMPLIMENTARY_PERIOD_INVALID: 'period' }`; other codes show as `FormRootError`. Submit «منح مجانًا» → `useGrantPlan`, then close. |
| W30 | `features/users/components/UserListEmptyState.tsx` | Props `{ variant: 'no-data' \| 'no-results'; tab; onClear() }`. «لا يوجد مستخدمون بعد.» / «لا يوجد مستخدمون يطابقون البحث.» with «مسح الفلاتر». |
| W31 | `features/users/components/UserListSkeleton.tsx` | `role="status"`, `aria-label={t('list.loading')}`, 5 soft rows. |
| W32 | `features/users/components/StudentProfileCard.tsx` | Props `{ profile: StudentProfileResult; onSuspend(); onReactivate(); onGrant(plan) }`. A card with a `dl`: contact (masked, mono ltr), status badge, joined date and onboarded date (`formatDate`, latin digits for admin), interests (a comma list or «لم يختر مواد بعد»), plan line (`plan.Free`/`plan.Base` plus the «اسأل معلّم» badge). Actions: suspend or reactivate, and the grant buttons under the same rules as W24. |
| W33 | `features/users/components/StudentSubscriptionsCard.tsx` | Props `{ subscriptions: AdminSubscriptionResult[] }`. A table of plan, period, status, start, end, and a «مجاني» neutral badge when `isComplimentary`. Empty → «لا توجد اشتراكات.» |
| W34 | `features/users/components/StudentProgressSection.tsx` | Props `{ studentId }`. `useGetStudentProgress(studentId)`. Loading/error/empty like `SubjectProgressSection`. One card per subject: name, `MasteryBar`, `progress.mastery` {percent}, «أتقن X من Y · شاهد Z», and a unit table (name, mastery %, best exam score or «—»). Weak spots: two lists (lesson name + subject + %, objective text + lesson), no links; empty → «لا توجد نقاط ضعف بعد.» |
| W35 | `features/users/components/StudentHistorySection.tsx` | Props `{ studentId }`. `useStudentHistorySearch` plus `useGetStudentSessionHistory(studentId, { kind, pageNumber, pageSize: 20 }, { query: { placeholderData: keepPreviousData } })`. Kind pills الكل/تدريب/امتحانات; a table of date, kind, scope, score (with a «الأفضل» badge) or «جارية»; no links. Empty no-data / no-results with «مسح الفلتر»; `Pagination`. |
| W36 | `features/session/pages/AcceptInvitePage.tsx` | `AuthLayout` titled `acceptInvite.title` with intro `acceptInvite.intro`. The state machine is `start → code → password`: `EmailCodeStartForm` (reused) → `OtpForm` (reused, `request={{ phoneNumber: null, email }}`, `onVerified={(id) => setVerificationId(id)}`, `changeLabel=t('actions.changeEmail')`) → `SetPasswordForm`. The footer links to `/login` («لديك كلمة مرور؟ سجّل الدخول»). |
| W37 | `features/session/components/SetPasswordForm.tsx` | Props `{ verificationId: string }`. RHF + `setPasswordSchema`. Fields `password` and `confirmPassword` (`type="password"`, `autoComplete="new-password"`, hint `fields.passwordHint`). Submit «حفظ كلمة المرور والدخول» → `useAcceptInvitation().mutateAsync({ data: { verificationId, password } })` → `useStartSession()(result)`. The router redirect happens through `redirectSignedIn`. `serverErrorFields = { PASSWORD_TOO_SHORT: 'password', PASSWORD_MUST_CONTAIN_DIGIT: 'password', PASSWORD_REJECTED: 'password' }`; `INVITATION_NOT_FOUND` and `USER_SUSPENDED` show as the root error. |
| W38 | `features/session/schemas/setPasswordSchema.ts` | `z.object({ password: newPasswordField, confirmPassword: z.string() }).refine((v) => v.password === v.confirmPassword, { path: ['confirmPassword'], error: 'session:validation.passwordMismatch' })`; type `SetPasswordValues`. |
| W39 | `test/userFixtures.ts` | `userSummary(overrides)`, `usersPage(items, pageNumber=1, totalPages=1)`, `studentProfile(overrides)`, `adminSubscription(overrides)`, `studentProgress(overrides)`, `studentHistoryPage(items)`, and constants `listStudentId`, `listTeacherId`, `listAdminId` (fixed UUIDs). The default student has `maskedPhone: '010*****678'`, `tier: 'Free'` and `status: 'Active'`. |

**Web layout and copy** (namespace `users`; the en value is shown, the ar value is in parentheses). The keys are fixed; the implementer writes both files.
- `page.title` Users (المستخدمون). `tabs.students` Students (الطلاب), `tabs.teachers` Teachers (المعلّمون), `tabs.admins` Admins (المديرون).
- `filters.search` Search (بحث), `filters.searchHint` Name, full mobile number or full email (الاسم أو رقم الموبايل كاملا أو البريد كاملا), `filters.status` Status (الحالة), `filters.all` All (الكل), `filters.apply` Apply (تطبيق), `filters.clear` Clear filters (مسح الفلاتر).
- `table.caption` Users list (قائمة المستخدمين); columns `table.name` Name (الاسم), `table.contact` Contact (التواصل), `table.plan` Plan (الباقة), `table.subjects` Assigned subjects (المواد المسندة), `table.status` Status (الحالة), `table.joined` Joined (تاريخ الانضمام), `table.actions` Actions (إجراءات).
- `status.Active` Active (نشط), `status.SuspendedStudent` Suspended (موقوف), `status.SuspendedStaff` Deactivated (معطّل), `status.pendingInvite` Invitation pending (دعوة معلقة).
- `status.suspend` Suspend (إيقاف), `status.deactivate` Deactivate (تعطيل), `status.reactivate` Activate (تفعيل).
- `status.suspendTitle` Suspend {name}? (إيقاف {name}؟), `status.deactivateTitle` Deactivate {name}? (تعطيل {name}؟), `status.reactivateTitle` Activate {name}? (تفعيل {name}؟).
- `status.suspendBody` They are signed out everywhere and cannot sign in until activated. (سيخرج من كل الأجهزة ولن يستطيع الدخول حتى يعاد تفعيله.), `status.reactivateBody` They can sign in again. (سيتمكن من الدخول مرة أخرى.).
- `status.cancel` Cancel (تراجع), `status.suspendedToast` Account suspended. (تم إيقاف الحساب.), `status.reactivatedToast` Account activated. (تم تفعيل الحساب.).
- `status.selfHint` This is your account (هذا حسابك), `status.lastAdminHint` Last active admin (آخر مدير نشط).
- `plan.Free` Free (مجانية), `plan.Base` Base (الأساسية), `plan.askTeacher` Ask a Teacher (اسأل معلّم).
- `grant.base` Grant Base (منح الأساسية), `grant.askTeacher` Grant Ask a Teacher (منح اسأل معلّم), `grant.title` Grant {plan} to {name} (منح {plan} إلى {name}), `grant.body` Free of charge; starts now and ends after the chosen period. (بدون مقابل، تبدأ الآن وتنتهي بعد المدة المختارة.), `grant.period` Period (المدة), `grant.Monthly` Monthly ({months} month) (شهري ({months} شهر)), `grant.Termly` Term ({months} months) (ترم ({months} أشهر)), `grant.Yearly` Yearly ({months} months) (سنوي ({months} شهرا)), `grant.confirm` Grant free (منح مجانا), `grant.doneToast` Plan granted. (تم منح الباقة.).
- `invite.teacher` Invite teacher (دعوة معلّم), `invite.admin` Invite admin (دعوة مدير), `invite.titleTeacher` / `invite.titleAdmin` (same text as the buttons), `invite.name` Name (الاسم), `invite.email` Email (البريد الإلكتروني), `invite.submit` Create invitation (إنشاء الدعوة).
- `invite.doneTitle` Invitation created (تم إنشاء الدعوة), `invite.doneBody` Send this link to {name}. They confirm {email} with a code and choose a password. (أرسل هذا الرابط إلى {name}. سيؤكد {email} برمز ثم يختار كلمة المرور.), `invite.link` Invitation link (رابط الدعوة), `invite.copy` Copy link (نسخ الرابط), `invite.copied` Link copied. (تم نسخ الرابط.), `invite.close` Done (تم), `invite.createdToast` Invitation created. (تم إنشاء الدعوة.).
- `subjects.loading` Loading subjects… (جار تحميل المواد…), `subjects.savedToast` Subjects updated. (تم تحديث المواد.).
- `list.loading` Loading users… (جار تحميل المستخدمين…), `list.errorTitle` Could not load users (تعذر تحميل المستخدمين), `empty.noData` No users yet. (لا يوجد مستخدمون بعد.), `empty.noResults` No users match the search. (لا يوجد مستخدمون يطابقون البحث.).
- `student.title` Student progress (تقدّم الطالب), `student.back` Users (المستخدمون), `student.contact` Contact (التواصل), `student.joined` Joined (انضم), `student.onboarded` Chose subjects (اختار المواد), `student.interests` Subjects of interest (المواد المفضلة), `student.noInterests` No subjects chosen yet (لم يختر مواد بعد), `student.plan` Plan (الباقة), `student.errorTitle` Could not load the student (تعذر تحميل بيانات الطالب).
- `subscriptions.title` Subscriptions (الاشتراكات), `subscriptions.empty` No subscriptions. (لا توجد اشتراكات.), `subscriptions.complimentary` Free grant (مجاني), `subscriptions.plan`/`period`/`status`/`start`/`end` (الباقة/المدة/الحالة/البداية/النهاية).
- `progress.title` Mastery by subject (الإتقان حسب المادة), `progress.mastery` Mastery {percent}% (الإتقان {percent}٪), `progress.counts` Mastered {mastered} of {servable} · seen {seen} (أتقن {mastered} من {servable} · شاهد {seen}), `progress.bestExam` Best unit exam (أفضل امتحان وحدة), `progress.empty` No subjects yet. (لا توجد مواد بعد.), `progress.loading` Loading progress… (جار تحميل التقدم…), `progress.errorTitle` Could not load progress (تعذر تحميل التقدم).
- `weakSpots.title` Weak spots (نقاط الضعف), `weakSpots.lessons` Lessons (الدروس), `weakSpots.objectives` Objectives (الأهداف), `weakSpots.empty` No weak spots yet. (لا توجد نقاط ضعف بعد.).
- `history.title` Session history (سجل الجلسات), `history.all` All (الكل), `history.quizzes` Quizzes (تدريب), `history.exams` Exams (امتحانات), `history.inProgress` In progress (جارية), `history.best` Best (الأفضل), `history.empty` No sessions yet. (لا توجد جلسات بعد.), `history.noResults` No sessions of this kind. (لا توجد جلسات من هذا النوع.), `history.clear` Clear filter (مسح الفلتر), `history.loading` Loading history… (جار تحميل السجل…), `history.errorTitle` Could not load history (تعذر تحميل السجل).
- `validation.searchTooLong` Search is too long. (نص البحث طويل جدا.), `validation.displayNameLength` Name must be 100 characters or fewer. (الاسم يجب ألا يزيد على 100 حرف.), `validation.email` Enter a valid email address. (أدخل بريدا إلكترونيا صحيحا.), `validation.emailLength` Email is too long. (البريد طويل جدا.), `validation.planRequired` Choose a plan. (اختر الباقة.), `validation.periodRequired` Choose a period. (اختر المدة.).
- Session additions:
  - `acceptInvite.title` Accept your invitation (قبول الدعوة);
  - `acceptInvite.intro` Enter the email the admin invited, confirm it with a code, then choose your password. (أدخل البريد الذي دعاك به المدير، أكده بالرمز، ثم اختر كلمة المرور.);
  - `acceptInvite.passwordIntro` Choose the password you will sign in with. (اختر كلمة المرور التي ستدخل بها.);
  - `acceptInvite.haveAccount` Already set a password? (ضبطت كلمة المرور من قبل؟) and `acceptInvite.signIn` Sign in (تسجيل الدخول);
  - `fields.newPassword` New password (كلمة المرور الجديدة), `fields.confirmPassword` Confirm password (تأكيد كلمة المرور), `actions.setPassword` Save password and sign in (حفظ كلمة المرور والدخول), `validation.passwordMismatch` Passwords do not match. (كلمتا المرور غير متطابقتين.).
- Admin numbers and dates use latin digits (`formatDate(..., 'latin')`, as the dashboard does). Every table sits inside `overflow-x-auto rounded-lg border border-border bg-surface shadow-1`. Logical properties only, no literals.

## Test plan
### API unit
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `Tests/Domain/Identity/UserTests` (add) | `Suspend_ByAnotherActorWithTwoActiveAdmins_SuspendsAdminAndStampsUpdatedBy` | Status Suspended, `UpdatedBy` = actor, `UpdationDate` advanced |
| 2 | same | `Suspend_ByItself_ThrowsUserCannotSuspendSelf` | BRV + code; status unchanged |
| 3 | same | `Suspend_LastActiveAdmin_ThrowsLastActiveAdmin` | BRV + `LAST_ACTIVE_ADMIN`; still Active |
| 4 | same | `Suspend_StudentWithOneActiveAdminCount_Suspends` | a non-admin ignores the count |
| 5 | same | `Suspend_AlreadySuspendedWithActor_ThrowsUserAlreadySuspended` | code |
| 6 | same | `Reactivate_SuspendedUser_ActivatesAndStampsUpdatedBy` | Active, `IsActive`, `UpdatedBy` |
| 7 | same | `Reactivate_ActiveUser_ThrowsUserNotSuspended` | code |
| 8 | same | `IsInvitationPending_TeacherWithoutPassword_IsTrue` | true |
| 9 | same | `IsInvitationPending_TeacherWithPasswordHash_IsFalse` | set `PasswordHash = "hash"` → false |
| 10 | same | `IsInvitationPending_StudentWithoutPassword_IsFalse` | false |
| 11 | same | `CanBeSuspendedBy_OtherActiveUser_IsTrue` | true |
| 12 | same | `CanBeSuspendedBy_Self_IsFalse` | false |
| 13 | same | `CanBeSuspendedBy_LastActiveAdmin_IsFalse` | false |
| 14 | same | `CanBeSuspendedBy_SuspendedUser_IsFalse` | false |
| 15 | `Tests/Domain/Subscriptions/StudentEntitlementTests` (add) | `EnsureCanGrant_BaseWhenFree_DoesNotThrow` | no throw |
| 16 | same | `EnsureCanGrant_AskTeacherWithoutBase_ThrowsComplimentaryRequiresBase` | code |
| 17 | same | `EnsureCanGrant_HeldBase_ThrowsComplimentaryPlanAlreadyActive` | code |
| 18 | same | `EnsureCanGrant_AskTeacherWithBase_DoesNotThrow` | no throw |
| 19 | `Tests/Domain/Subscriptions/SubscriptionEntitlementSpecificationTests` (add) | `EntitledForStudents_ListedStudentEntitled_IsSatisfied` | compiled predicate true |
| 20 | same | `EntitledForStudents_UnlistedStudent_IsNotSatisfied` | false |
| 21 | `Tests/Application/Features/Users/Shared/ContactMaskTests` | `MaskPhone_ElevenDigits_KeepsFirstAndLastThree` | `"01012345678"` → `"010*****678"` |
| 22 | same | `MaskPhone_Null_ReturnsNull` | null |
| 23 | same | `MaskPhone_SixCharactersOrFewer_MasksEverything` | `"12345"` → `"*****"` |
| 24 | same | `MaskEmail_Address_KeepsFirstCharacterAndDomain` | `"mona@example.test"` → `"m***@example.test"` |
| 25 | same | `MaskEmail_Null_ReturnsNull` | null |
| 26 | same | `MaskEmail_WithoutAtSign_ReturnsMaskOnly` | `"***"` |
| 27 | `Tests/Application/Features/Users/GetUsers/GetUsersFilterTests` | `Build_SearchFragment_MatchesDisplayNameIgnoringCase` | compiled against users |
| 28 | same | `Build_ExactPhone_Matches` | true |
| 29 | same | `Build_PartialPhone_DoesNotMatch` | false |
| 30 | same | `Build_EmailInOtherCase_Matches` | requires `NormalizedEmail` set (create via factory, then set `NormalizedEmail = email.ToUpperInvariant()`) |
| 31 | same | `Build_Role_ExcludesOtherRoles` | false for a teacher when the role is Student |
| 32 | same | `Build_Status_ExcludesOtherStatus` | false |
| 33 | `…/GetUsers/GetUsersValidatorTests` | `Validate_Defaults_Passes` | valid |
| 34 | same | `Validate_PageNumberZero_FailsWithPageNumberInvalid` | code |
| 35 | same | `Validate_PageSizeAboveMax_FailsWithPageSizeInvalid` | code |
| 36 | same | `Validate_UndefinedRole_FailsWithRoleInvalid` | `(UserRole)9` |
| 37 | same | `Validate_UndefinedStatus_FailsWithStatusInvalid` | code |
| 38 | same | `Validate_SearchTooLong_FailsWithSearchTooLong` | code |
| 39 | `…/GetUsers/GetUsersHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401 code |
| 40 | same | `Handle_Students_ReturnsMaskedContactAndEntitledTier` | a stubbed page with one phone student plus `SubscriptionRepositoryStub` EntitledBase → `Tier` Base, `MaskedPhone` masked, `SubjectIds` empty |
| 41 | same | `Handle_Teachers_ReturnsAssignedSubjectIdsAndInvitationPending` | `SubjectIds` = assigned, `InvitationPending` true, `Tier` null |
| 42 | same | `Handle_AdminsWithOneActiveAdmin_MarksItNotSuspendable` | `CountAsync` → 1 → `CanSuspend` false |
| 43 | same | `Handle_AdminsWithTwoActive_CurrentAdminNotSuspendableOtherIs` | self false, other true |
| 44 | `…/Users/InviteUser/InviteUserValidatorTests` | `Validate_ValidTeacher_Passes`; `Validate_StudentRole_FailsWithInviteRoleInvalid`; `Validate_EmptyDisplayName_FailsWithDisplayNameRequired`; `Validate_LongDisplayName_FailsWithDisplayNameTooLong`; `Validate_EmptyEmail_FailsWithEmailRequired`; `Validate_InvalidEmail_FailsWithEmailInvalid`; `Validate_LongEmail_FailsWithEmailTooLong` | one code each (7 tests) |
| 45 | `…/InviteUser/InviteUserHandlerTests` | `Handle_Teacher_CreatesTeacherWithoutPassword` | `CreateAsync(Arg.Is<User>(role Teacher, email))` Received(1) with the single-arg overload; result id = user id |
| 46 | same | `Handle_Admin_CreatesAdmin` | role Admin |
| 47 | same | `Handle_EmailRegistered_ThrowsConflict` | 409 `EMAIL_ALREADY_REGISTERED`; `CreateAsync` DidNotReceive |
| 48 | same | `Handle_CreateFails_ThrowsUserCreationFailed` | 400 code |
| 49 | same | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401; `CreateAsync` DidNotReceive |
| 50 | `…/Users/SuspendUser/SuspendUserValidatorTests` | `Validate_UserId_Passes`; `Validate_EmptyUserId_FailsWithUserIdRequired` | 2 tests |
| 51 | `…/SuspendUser/SuspendUserHandlerTests` (the lock stub invokes the callback) | `Handle_Student_SuspendsRotatesStampAndEvictsCache` | status Suspended; `UpdateSecurityStampAsync` Received(1); a pre-seeded `user-active:` key is gone from a real `MemoryCache`; `ExecuteInAdminRosterLockAsync` Received(1) |
| 52 | same | `Handle_AdminWithTwoActiveAdmins_Suspends` | `CountAsync` → 2 → suspended |
| 53 | same | `Handle_LastActiveAdmin_ThrowsLastActiveAdmin` | BRV code; stamp DidNotReceive; cache key still present |
| 54 | same | `Handle_Self_ThrowsUserCannotSuspendSelf` | code; stamp DidNotReceive |
| 55 | same | `Handle_UnknownUser_ThrowsUserNotFound` | 404 |
| 56 | same | `Handle_StampUpdateFails_ThrowsUserModifiedConcurrently` | `IdentityResult.Failed()` → 409 |
| 57 | same | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401; lock DidNotReceive |
| 58 | `…/Users/ReactivateUser/ReactivateUserValidatorTests` | `Validate_UserId_Passes`; `Validate_EmptyUserId_FailsWithUserIdRequired` | 2 tests |
| 59 | `…/ReactivateUser/ReactivateUserHandlerTests` | `Handle_SuspendedUser_ReactivatesAndEvictsCache` | Active; `UpdateAsync` Received(1); key removed |
| 60 | same | `Handle_ActiveUser_ThrowsUserNotSuspended` | code; `UpdateAsync` DidNotReceive |
| 61 | same | `Handle_UnknownUser_ThrowsUserNotFound` | 404 |
| 62 | same | `Handle_UpdateFails_ThrowsUserModifiedConcurrently` | 409 |
| 63 | same | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401 |
| 64 | `…/Users/CheckUserActive/CheckUserActiveHandlerTests` | `Handle_ActiveUser_ReturnsTrueAndServesRepeatFromCache` | true twice; `FirstOrDefaultAsync` Received(1) (the cache is the behaviour) |
| 65 | same | `Handle_SuspendedUser_ReturnsFalse` | false |
| 66 | same | `Handle_UnknownUser_ReturnsFalse` | false |
| 67 | same | `Handle_CacheDisabled_QueriesEveryTime` | seconds 0 → Received(2) |
| 68 | `…/Auth/AcceptInvitation/AcceptInvitationValidatorTests` | `Validate_Valid_Passes`; `Validate_EmptyVerificationId_FailsWithInvalidFormat`; `Validate_EmptyPassword_FailsWithPasswordRequired`; `Validate_ShortPassword_FailsWithPasswordTooShort`; `Validate_PasswordWithoutDigit_FailsWithMustContainDigit` | 5 tests |
| 69 | `…/AcceptInvitation/AcceptInvitationHandlerTests` | `Handle_PendingInvitation_AddsPasswordAndReturnsTokens` | `AddPasswordAsync` Received(1); `otp.IsUsed`; `otpRepository.SaveChangesAsync` Received(1); result role Teacher and tokens |
| 70 | same | `Handle_UnknownVerification_ThrowsOtpInvalid` | 400; save DidNotReceive |
| 71 | same | `Handle_PhoneOtp_ThrowsOtpInvalid` | 400 |
| 72 | same | `Handle_UnverifiedOtp_ThrowsOtpNotVerified` | the Core.OTP code `OTP_NOT_VERIFIED` |
| 73 | same | `Handle_NoUserForEmail_ThrowsInvitationNotFound` | 404 |
| 74 | same | `Handle_UserAlreadyHasPassword_ThrowsInvitationNotFound` | 404; `AddPasswordAsync` DidNotReceive |
| 75 | same | `Handle_StudentEmail_ThrowsInvitationNotFound` | 404 |
| 76 | same | `Handle_SuspendedInvitee_ThrowsUserSuspended` | 403 |
| 77 | same | `Handle_PasswordRejectedByIdentity_ThrowsPasswordRejected` | 400; save DidNotReceive |
| 78 | `…/Students/GetStudentProfile/GetStudentProfileValidatorTests` | `Validate_StudentId_Passes`; `Validate_EmptyStudentId_FailsWithStudentIdRequired` | 2 tests |
| 79 | `…/GetStudentProfile/GetStudentProfileHandlerTests` | `Handle_Student_ReturnsMaskedProfileEntitlementAndInterests` | masked email, Tier Base, interest names ordered, subscriptions mapped |
| 80 | same | `Handle_ComplimentarySubscription_IsFlaggedComplimentary` | `IsComplimentary` true for a null reference, false for a paid one |
| 81 | same | `Handle_NonStudent_ThrowsStudentNotFound` | the repo stub compiles the predicate against a teacher → 404 |
| 82 | `…/Subscriptions/GrantComplimentarySubscription/GrantComplimentarySubscriptionValidatorTests` | `Validate_Valid_Passes`; `Validate_EmptyStudentId_FailsWithStudentIdRequired`; `Validate_UndefinedPlan_FailsWithPlanInvalid`; `Validate_UndefinedPeriod_FailsWithPeriodInvalid` | 4 tests |
| 83 | `…/GrantComplimentarySubscription/GrantComplimentarySubscriptionHandlerTests` | `Handle_TermlyBaseForFreeStudent_StartsComplimentarySubscription` | `AddAsync` with a null reference, `CreatedBy` = admin, end = start + 4 months (FakeTimeProvider); `SaveChangesAsync` Received(1); `IsComplimentary` true |
| 84 | same | `Handle_AskTeacherWithoutBase_ThrowsComplimentaryRequiresBase` | code; save DidNotReceive |
| 85 | same | `Handle_BaseAlreadyHeld_ThrowsComplimentaryPlanAlreadyActive` | code |
| 86 | same | `Handle_AskTeacherTermly_ThrowsComplimentaryPeriodUnavailable` | 400 code |
| 87 | same | `Handle_UnknownStudent_ThrowsStudentNotFound` | 404 |
| 88 | same | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401 |
| 89 | `…/Progress/GetStudentProgress/GetStudentProgressValidatorTests` | `Validate_StudentId_Passes`; `Validate_EmptyStudentId_FailsWithStudentIdRequired` | 2 tests |
| 90 | `…/GetStudentProgress/GetStudentProgressHandlerTests` | `Handle_Student_ReturnsThatStudentsSubjectsAndWeakSpots` | the result has the subject built from the stubbed counts; `GetLessonCountsAsync(studentId, …)` received with the route student id (the scoping is the behaviour) |
| 91 | same | `Handle_UnknownStudent_ThrowsStudentNotFound` | 404; mastery repo DidNotReceive |
| 92 | `…/Progress/GetStudentSessionHistory/GetStudentSessionHistoryValidatorTests` | `Validate_Valid_Passes`; `Validate_EmptyStudentId_FailsWithStudentIdRequired`; `Validate_PageNumberZero_FailsWithPageNumberInvalid`; `Validate_PageSizeAboveMax_FailsWithPageSizeInvalid`; `Validate_UndefinedKind_FailsWithKindInvalid` | 5 tests |
| 93 | `…/GetStudentSessionHistory/GetStudentSessionHistoryHandlerTests` | `Handle_Student_ReturnsOnlyThatStudentsSessions` | the paginated stub compiles the filter over two students' sessions → only the target's |
| 94 | same | `Handle_UnknownStudent_ThrowsStudentNotFound` | 404 |
| 95 | `Tests/Api/Authorization/ActiveUserTokenValidationTests` | `OnTokenValidated_ActiveUser_LeavesPrincipal` | `context.Result` null (not failed) with the sender substitute returning true |
| 96 | same | `OnTokenValidated_InactiveUser_Fails` | `context.Result.Failure` not null, message `USER_SUSPENDED` |
| 97 | same | `OnTokenValidated_MissingUserIdClaim_Fails` | failure; sender DidNotReceive |

### API integration (Testcontainers, through HTTP)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 98 | `Tests/Integration/Users/UsersEndpointTests` | `Get_StudentsSearchByExactPhone_ReturnsMaskedStudent` | 200; one item, `maskedPhone` = `010*****` + last 3; the raw body does not contain the full phone; `tier` "Free" |
| 99 | same | `Get_SearchByNameFragment_ReturnsMatchingUserOnly` | unique name fragment → exactly the seeded user |
| 100 | same | `Get_TeachersTab_ReturnsSubjectIdsAndPendingInvitation` | a teacher invited through the API with an assigned subject → `invitationPending` true, `subjectIds` contains the subject |
| 101 | same | `Get_PageSizeAboveMax_Returns422` | `USER_LIST_PAGE_SIZE_INVALID` |
| 102 | same | `Get_Teacher_Returns403` | 403 |
| 103 | same | `Get_Anonymous_Returns401` | 401 |
| 104 | same | `Invite_Teacher_CreatesPendingTeacherAndAudits` | 200 `userId`; DB role Teacher, `PasswordHash` null; an `AuditLogs` row `User.Invite` Success with `ResourceId` = userId |
| 105 | same | `Invite_RegisteredEmail_Returns409` | `EMAIL_ALREADY_REGISTERED` |
| 106 | same | `Invite_StudentRole_Returns422` | `USER_INVITE_ROLE_INVALID` |
| 107 | same | `Suspend_SignedInStudent_RevokesAccessAndRefreshTokens` | the student logs in (keep token + refresh cookie) and GETs `/api/progress/subjects` 200 (warms the cache); the admin suspends (200); the same token → 401; `POST /api/auth/refresh` with the old cookie → 401; DB status Suspended; audit `User.Suspend` Success |
| 108 | same | `Suspend_Self_Returns400` | `USER_CANNOT_SUSPEND_SELF` |
| 109 | same | `Suspend_UnknownUser_Returns404` | `USER_NOT_FOUND` |
| 110 | same | `Suspend_TeacherCaller_Returns403` | 403 |
| 111 | same | `Reactivate_SuspendedStudent_AllowsSignInAgain` | 200; then `login/email` 200 |
| 112 | same | `Reactivate_ActiveUser_Returns400` | `USER_NOT_SUSPENDED` |
| 113 | `Tests/Integration/Auth/AcceptInvitationEndpointTests` | `Accept_InvitedTeacher_SetsPasswordAndSignsIn` | the admin invites; `SendAndVerifyEmailOtpAsync`; accept → 200, `user.role` "Teacher", refresh cookie set; then `login/email` with the password → 200 |
| 114 | same | `Accept_AfterAcceptance_Returns404` | a second OTP plus accept → `INVITATION_NOT_FOUND` |
| 115 | same | `Accept_StudentEmail_Returns404` | `INVITATION_NOT_FOUND` |
| 116 | same | `Accept_UnverifiedOtp_Returns400` | `OTP_NOT_VERIFIED` |
| 117 | same | `Accept_ShortPassword_Returns422` | `PASSWORD_TOO_SHORT` |
| 118 | `Tests/Integration/Students/StudentAdministrationEndpointTests` | `GetProfile_Admin_ReturnsMaskedProfile` | 200; `maskedEmail` masked; the raw body does not contain the email; `tier` Free |
| 119 | same | `GetProfile_TeacherId_Returns404` | `STUDENT_NOT_FOUND` |
| 120 | same | `GetProfile_StudentCaller_Returns403` | 403 |
| 121 | same | `GetProgress_Admin_ReturnsSubjectsForStudent` | a seeded subject is present with `masteredCount` 0; `weakSpots.lessons` empty |
| 122 | same | `GetProgress_StudentCaller_Returns403` | 403 |
| 123 | same | `GetSessions_Admin_ReturnsEmptyPageForNewStudent` | 200, `totalItems` 0 |
| 124 | same | `GetSessions_PageSizeAboveMax_Returns422` | `SESSION_HISTORY_PAGE_SIZE_INVALID` |
| 125 | same | `GetSessions_Anonymous_Returns401` | 401 |
| 126 | `Tests/Integration/Subscriptions/ComplimentarySubscriptionEndpointTests` | `Grant_BaseToFreeStudent_EntitlesStudentAndAudits` | 200 `isComplimentary` true; the student's `GET /api/subscriptions/entitlement` has `tier` Base; DB `PaymobReference` null, `CreatedBy` admin; audit `Subscription.GrantComplimentary` with a non-null `Diff` |
| 127 | same | `Grant_AskTeacherWithoutBase_Returns400` | `COMPLIMENTARY_REQUIRES_BASE` |
| 128 | same | `Grant_HeldBase_Returns400` | `COMPLIMENTARY_PLAN_ALREADY_ACTIVE` |
| 129 | same | `Grant_AskTeacherTermly_Returns400` | `COMPLIMENTARY_PERIOD_UNAVAILABLE` |
| 130 | same | `Grant_UnknownStudent_Returns404` | `STUDENT_NOT_FOUND` |
| 131 | same | `Grant_TeacherCaller_Returns403` | 403 |

### Web (Vitest + Testing Library + MSW)
| # | File | `it(...)` | Asserts |
|---|------|-----------|---------|
| 132 | `features/users/schemas/usersSearchSchema.test.ts` | parses a valid tab, q, status and page · drops an unknown tab, status and page · trims q · drops q longer than 256 | parsed output |
| 133 | `features/users/schemas/userFiltersSchema.test.ts` | accepts empty filters · rejects a search over 256 with `users:validation.searchTooLong` | issues |
| 134 | `features/users/schemas/inviteUserSchema.test.ts` | accepts valid · requires a name · rejects a name over 100 · rejects an invalid email | error keys |
| 135 | `features/users/schemas/grantPlanSchema.test.ts` | accepts Base Termly · requires a plan · requires a period | error keys |
| 136 | `features/users/schemas/studentDetailSearchSchema.test.ts` | parses kind and page · drops invalid values | output |
| 137 | `features/users/api/userListParams.test.ts` | maps the teachers tab to the Teacher role with search and status · defaults to Student page 1 · `hasActiveFilters` true only with q or status | objects |
| 138 | `features/users/locales.test.ts` | does not ship the users namespace until registered · resolves `users:page.title` after registering | `i18n.hasResourceBundle('en','users')` false then true |
| 139 | `features/users/pages/UsersPage.test.tsx` | shows students after loading (loading status, then a row with the name, the masked phone and «Free») · shows the empty state when there are no users · shows no-results with Clear filters that clears the URL · shows retry on error and recovers · switching to Teachers requests `role=Teacher` and shows subject checkboxes · applying a search puts `q` in the request · renders rtl in Arabic · has no axe violations | DOM and request params |
| 140 | `features/users/pages/UsersPage.actions.test.tsx` | suspends a student after confirming and shows the toast · activates a suspended user · disables deactivate for the last active admin with the hint · disables deactivate on the current admin's own row · assigning a subject posts to the assign endpoint · unchecking a subject calls unassign · the invite teacher dialog shows the invitation link after success · the invite shows `EMAIL_ALREADY_REGISTERED` under the email field · a `LAST_ACTIVE_ADMIN` server error shows an error toast · grants Base from a Free student's row | UI outcomes and request bodies (MSW) |
| 141 | `features/users/pages/StudentDetailPage.test.tsx` | shows the profile with the masked contact, plan and complimentary badge · shows subject mastery, weak spots and history · filters history to exams · shows the not-found message for `STUDENT_NOT_FOUND` · shows retry on error · grants Ask a Teacher to a Base student and shows the toast · suspends from the profile · renders rtl in Arabic | DOM |
| 142 | `features/session/schemas/setPasswordSchema.test.ts` | accepts matching valid passwords · rejects short · rejects without a digit · rejects a mismatch on `confirmPassword` | error keys and path |
| 143 | `features/session/pages/AcceptInvitePage.test.tsx` | email → code → password signs the teacher in and lands on `/teacher` · shows `INVITATION_NOT_FOUND` as a form error · shows a mismatch inline · redirects a signed-in visitor to their home · has no axe violations | URL, DOM, request bodies |

## Definition of done
- [ ] Every checkbox of #106 is delivered: student search, profile, progress, suspend and grant; teacher invite, subject assignment and deactivate; admin invite and deactivate with last-admin protection; the admin users pages.
- [ ] Every new endpoint carries `[Authorize(Policy = DefaultCodes.UsersManage | ProgressViewAny)]` or `[AllowAnonymous]` (accept only), and `EndpointAuthorizationTests` passes.
- [ ] `User.Suspend(Guid, int)` refuses self (`USER_CANNOT_SUSPEND_SELF`) and the last active admin (`LAST_ACTIVE_ADMIN`). The count runs inside `ExecuteInAdminRosterLockAsync` (`pg_advisory_xact_lock`, parameterised `ExecuteSqlAsync`).
- [ ] Suspend rotates the security stamp in the same save (refresh tokens rejected) and evicts `user-active:{id:N}`. `OnTokenValidated` rejects the old access token. Integration test 107 proves both.
- [ ] Reactivate exists and evicts the cache.
- [ ] Invite creates a password-less Teacher or Admin. Accept uses the verified email OTP, `AddPasswordAsync` and a signed-in `AuthResult` with the refresh cookie, and refuses non-pending accounts with 404 `INVITATION_NOT_FOUND`.
- [ ] The complimentary grant follows Decision 9. `PaymobReference` is null, `CreatedBy` is the admin, and `IsComplimentary` is exposed.
- [ ] List and profile never return a full phone or email (tests 98 and 118 assert the raw body). Search is server-side, paged, capped by `Users:ListMaxPageSize`/`SearchMaxLength`, and matches the exact phone or email only.
- [ ] Audited: `User.Invite`, `User.Suspend`, `User.Reactivate`, `Subscription.GrantComplimentary`, plus the `docs/audit-log.md` rows.
- [ ] Every new error code is in `ErrorCodes` (Domain or Application), both resx files and both web `common` locale files.
- [ ] `UsersOptions` has safe code defaults, `ValidateOnStart`, and entries in `appsettings.example.json` and `ApiFactory`. `dotnet test api/ -c Release` is green with `appsettings.json` moved aside.
- [ ] The existing progress handlers delegate to the loaders, and their existing tests are unchanged and green.
- [ ] No migration is added (no schema change), and the `AppDbContextTests` migration list is untouched.
- [ ] `api/openapi/v1.json`, the Orval client and `postman/elmanhg.postman_collection.json` are regenerated or updated and ordered.
- [ ] The web `users` namespace is absent from `app/i18n.ts` and registered in both page modules; test 138 passes. The `admin-users` budget is added, `npm run build && npm run perf:budget` passes, and `docs/performance.md` §3/§4 are updated.
- [ ] `/admin/users`, `/admin/student/$studentId` and `/accept-invite` have loading, empty, error-with-retry and ar/rtl states, token classes only, and logical properties only.
- [ ] Web checks pass: `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`, `npx vitest run --coverage` (thresholds met) and `npm run gen:api` with no diff.
- [ ] Docs agree in the same change:
  - PRD §7.1, §10.4 (search, masking, reactivate, invite via link plus email code, no self-deactivation, last admin, complimentary rules), §11.2 and §17 rule 12;
  - `docs/subscriptions.md` Complimentary grants;
  - `docs/progress.md` Admin view;
  - `docs/claude-design-prompt.md` §4;
  - `docs/prototype.md`.
- [ ] A new `docs/user-administration.md` exists (file #49). Its sections:
  - endpoints table;
  - rules: suspend/deactivate, self, last admin with lock, reactivate;
  - revocation (stamp plus `OnTokenValidated` plus the cache window, SignalR known limit);
  - invitations (flow, no expiry, link without PII);
  - masking and search;
  - complimentary grants (pointer to `docs/subscriptions.md`);
  - options (`Users:*`);
  - error codes;
  - audit actions.
- [ ] The guard grep is clean: `git diff origin/main -U0 -- '*.cs' | grep -E '^\+.*(DateTime\.(Now|UtcNow)|\.Result\b|\.Wait\(\)|new HttpClient\(|FromSqlRaw|async void)'` prints nothing.

File count: API about 49 new plus 24 modified; tests 40 new classes plus 3 extended; web 39 new plus 9 modified; docs 1 new plus 9 modified; Postman 1.
