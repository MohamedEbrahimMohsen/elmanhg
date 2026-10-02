# User administration

Admins manage every account from `/admin/users` (PRD §10.4): students, teachers and admins in three tabs, each with server-side search and paging. A student's profile, subscriptions, progress and session history are on `/admin/student/$studentId`. Invitees finish their invitation at `/accept-invite`.

## Endpoints

| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/users` | `Users.Manage` | query `role` (`Student` default, `Teacher`, `Admin`), `search?`, `status?` (`Active`, `Suspended`), `pageNumber` = 1, `pageSize` = 20 | 200 `PageData<UserSummaryResult>`; 422 `USER_LIST_*` |
| POST | `/api/users/invitations` | `Users.Manage` | `{ role: Teacher \| Admin, displayName, email, phoneNumber? }` (`phoneNumber` teachers only) | 200 `InviteUserResult { userId, emailSent }`; 409 `EMAIL_ALREADY_REGISTERED`; 422 (including `PHONE_NUMBER_TEACHERS_ONLY` / `VALIDATION_PHONE_NUMBER_*`) |
| POST | `/api/users/{userId}/suspend` | `Users.Manage` | — | 200; 400 `USER_CANNOT_SUSPEND_SELF` / `LAST_ACTIVE_ADMIN` / `USER_ALREADY_SUSPENDED`; 404 `USER_NOT_FOUND`; 409 `USER_MODIFIED_CONCURRENTLY` |
| POST | `/api/users/{userId}/reactivate` | `Users.Manage` | — | 200; 400 `USER_NOT_SUSPENDED`; 404; 409 |
| GET | `/api/students/{studentId}` | `Users.Manage` | — | 200 `StudentProfileResult`; 404 `STUDENT_NOT_FOUND` |
| GET | `/api/students/{studentId}/progress` | `Progress.ViewAny` | — | 200 `StudentProgressResult` ([progress.md](progress.md), Admin view) |
| GET | `/api/students/{studentId}/sessions` | `Progress.ViewAny` | `kind?`, `pageNumber`, `pageSize` | 200 `PageData<SessionHistoryItemResult>` |
| POST | `/api/students/{studentId}/complimentary-subscriptions` | `Users.Manage` | `{ plan, period }` | 200 `AdminSubscriptionResult`; see [subscriptions.md](subscriptions.md), Complimentary grants |
| PUT | `/api/teachers/{teacherId}/phone-number` | `Users.Manage` | `{ phoneNumber: string \| null }` (null removes it) | 200; 400 `PHONE_NUMBER_TEACHERS_ONLY`; 404 `USER_NOT_FOUND`; 409 `USER_MODIFIED_CONCURRENTLY`; 422 `VALIDATION_PHONE_NUMBER_*` / `TEACHER_ID_REQUIRED` |
| POST | `/api/auth/invitations/accept` | anonymous, credential rate limit | `{ verificationId, password }` | 200 `AuthResult` plus the refresh cookie; 400 `OTP_*` / `PASSWORD_REJECTED`; 403 `USER_SUSPENDED`; 404 `INVITATION_NOT_FOUND`; 422 |

Both policies are Admin only (PRD §16). Teacher subject assignment reuses `POST`/`DELETE /api/teachers/{teacherId}/subjects/{subjectId}`.

`UserSummaryResult { id, displayName, role, status, maskedPhone?, maskedEmail?, invitationPending, canSuspend, creationDate, tier?, hasAskTeacher, subjectIds[] }`: `tier` is set for students only, `subjectIds` for teachers only.
`StudentProfileResult { id, displayName, maskedPhone?, maskedEmail?, status, canSuspend, creationDate, onboardedAt?, subjectInterests[], tier, hasAskTeacher, subscriptions[] }`, the subscriptions newest first with `isComplimentary`.

## Rules

- **One state.** Suspending a student and deactivating a teacher or admin are the same state, `UserStatus.Suspended`, set by `User.Suspend(suspendedBy, activeAdminCount)`. The web labels it «إيقاف» / «موقوف» for students and «تعطيل» / «معطّل» for staff.
- **Never yourself.** `Suspend` refuses the acting admin's own account (`USER_CANNOT_SUSPEND_SELF`), checked first.
- **Last admin.** `Suspend` refuses an active admin when the count of active admins, including the target, is 1 or less (`LAST_ACTIVE_ADMIN`). The handler counts inside `IUserRepository.ExecuteInAdminRosterLockAsync`: one transaction holding `pg_advisory_xact_lock(106001)`, so two admins deactivating each other at the same moment are serialised and the second one sees the first one's change.
- **Reversible.** `User.Reactivate(reactivatedBy)` returns a suspended account to Active; an active account returns `USER_NOT_SUSPENDED`.
- `canSuspend` in the list and profile applies the same three checks, so the web disables the button and shows «هذا حسابك» or «آخر مدير نشط».
- No role changes: there is no promotion or demotion endpoint.

## Revocation

- Suspend rotates the security stamp (`UserManager.UpdateSecurityStampAsync`) in the same transaction, so every refresh token dies at once: `POST /api/auth/refresh` returns 401.
- Access tokens (JWT, `CoreJwt:ExpirationHours`) carry a SHA-256 fingerprint of the security stamp in the `security_stamp_hash` claim, never the stamp itself (Identity derives codes from it) (#240). `ActiveUserTokenValidation` hooks `JwtBearerEvents.OnTokenValidated` and sends `CheckUserActiveQuery` for every authenticated request; a suspended or deleted user, a token without the claim, or a token whose fingerprint no longer matches fails authentication with 401. Reactivation does not rotate the stamp back, so a token issued before a suspension stays refused after a quick reactivation; the user signs in again. Sign-out rotates the stamp too, so it also ends the user's access tokens on every device.
- The check is cached in memory per user (`user-active:{id}`, the active flag and the stamp) for `Users:ActiveStatusCacheSeconds` (30 s by default; 0 turns the cache off). Suspend, reactivate and sign-out evict the key on the instance that handled them, so on one instance the next request is refused. Another API instance notices within the cache window.
- Known limit: an open SignalR connection keeps the token it was validated with until it reconnects; the reconnect fails the check.
- Known limit: Ask a Teacher threads claimed by a deactivated teacher are not released. Admins can reply to any thread, and SLA alerts reach them.

## Invitations

1. The admin invites a teacher or admin by name and email. The account is created with no password (`IsInvitationPending` = not a student and no password hash) and `EmailConfirmed`. An email already registered returns 409.
2. The API emails the invitee through the Resend email channel when `OtpDelivery:Email` is enabled with the Resend provider. The email is Arabic, right to left, and carries the name, the role and the `InvitationEmail:AcceptInviteUrl` link. With the fake provider nothing is sent (`emailSent: false`); a Resend failure is logged as a warning without the address or the link and also returns `emailSent: false`, so the invitation still stands. The API never logs the recipient or the link.
3. The dialog shows the absolute `/accept-invite` link with «نسخ الرابط» either way. The link carries no email, token or other secret.
4. The invitee enters the email at `/accept-invite`, receives a one-time code through the normal email OTP (`/api/auth/otp/send` and `/otp/verify`), then chooses a password. `POST /api/auth/invitations/accept` marks the code used, checks the account is a pending invitation (otherwise 404 `INVITATION_NOT_FOUND`) and active (otherwise 403 `USER_SUSPENDED`), sets the password (`AddPasswordAsync`; the Identity password rules apply, 400 `PASSWORD_REJECTED`) and signs the invitee in.
5. Afterwards the teacher or admin signs in with email and password like everyone else (PRD §7.1).

An invitation never expires; an admin can deactivate a pending invitee, which also blocks acceptance.

## Masking and search

- Contacts are masked in every list and profile: a phone keeps its first three and last three characters (`010*****678`; six characters or fewer are fully masked), an email keeps its first character and the domain (`m***@example.test`). Full values are never returned by these endpoints.
- Search is server-side. It matches the display name (case-insensitive, any part), the **exact** mobile number, or the **exact** email (case-insensitive, through the normalised email). A partial number or email matches nothing, so the search cannot be used to enumerate contacts.
- Results are ordered newest account first. The existing admin payment log (#102) is unchanged.

## Teacher WhatsApp number

- Optional. An admin sets it in the invite dialog (teachers only) or sets, changes or removes it from the teacher row on `/admin/users` («رقم واتساب»). There is no teacher self-service: teachers have no profile or settings page.
- Same format as the student phone sign-in: `CoreOtp:PhoneCodes` and `CoreOtp:PhoneLength` (11 digits starting with 010, 011, 012 or 015); the value is stored trimmed in the user's phone number column (`User.SetContactPhoneNumber`).
- Not verified (`PhoneNumberConfirmed` stays false) and not unique: a teacher may share a number with their own student account.
- Never used for sign-in: phone sign-in and registration look users up by user name, and a teacher's user name is the email, so phone sign-in with a teacher's number still returns 404 `PHONE_NUMBER_NOT_REGISTERED`.
- Masked in every list (`maskedPhone`); the full number is never returned. Audited as `Teacher.SetPhoneNumber` without a diff, so the number never reaches the audit log; no log line carries it.
- Used by the one out-of-app reminder per Ask a Teacher question ([ask-teacher.md](ask-teacher.md) → SLA); a teacher without a number gets the email reminder only.

## Complimentary grants

See [subscriptions.md](subscriptions.md), Complimentary grants. The web offers «منح الأساسية» to a Free student and «منح اسأل معلّم» to a Base student without the add-on, both from the list row and the student page.

## Options

| Key | Default | Meaning |
|---|---|---|
| `Users:ListMaxPageSize` | 100 | Largest `pageSize` of `GET /api/users` (1–100). |
| `Users:SearchMaxLength` | 256 | Longest `search` (1–256). |
| `Users:ActiveStatusCacheSeconds` | 30 | How long the per-request active check is cached (0–300; 0 = no cache). |
| `InvitationEmail:AcceptInviteUrl` | empty | Absolute `https` link to `/accept-invite`; required when the email channel uses Resend. |
| `InvitationEmail:Subject` | «دعوة للانضمام إلى المنهج» | Invitation email subject. |

The invitation email reuses `OtpDelivery:Email` (`Enabled`, `Provider`, `BaseUrl`, `ApiKey`, `FromAddress`) and its timeouts ([otp-delivery.md](otp-delivery.md)). Deployment variables: [deployment.md](deployment.md).

## Error codes

| Code | HTTP | When |
|---|---|---|
| `USER_LIST_PAGE_NUMBER_INVALID` / `USER_LIST_PAGE_SIZE_INVALID` | 422 | Paging out of range. |
| `USER_LIST_ROLE_INVALID` / `USER_LIST_STATUS_INVALID` | 422 | Unknown role or status. |
| `USER_LIST_SEARCH_TOO_LONG` | 422 | `search` longer than `Users:SearchMaxLength`. |
| `USER_ID_REQUIRED` / `STUDENT_ID_REQUIRED` | 422 | Empty id. |
| `USER_INVITE_ROLE_INVALID` | 422 | Invite role other than Teacher or Admin. |
| `PHONE_NUMBER_TEACHERS_ONLY` | 400 / 422 | A WhatsApp number set for a non-teacher (400 from `PUT …/phone-number`, 422 on an admin invite). |
| `USER_NOT_FOUND` / `STUDENT_NOT_FOUND` | 404 | Unknown user, or an id that is not a student. |
| `USER_CANNOT_SUSPEND_SELF` | 400 | An admin suspends their own account. |
| `LAST_ACTIVE_ADMIN` | 400 | The last active admin would be deactivated. |
| `USER_ALREADY_SUSPENDED` / `USER_NOT_SUSPENDED` | 400 | Suspend twice / reactivate an active account. |
| `USER_MODIFIED_CONCURRENTLY` | 409 | Identity could not save the change. |
| `INVITATION_NOT_FOUND` | 404 | Accepting for an email with no pending invitation. |
| `PASSWORD_REJECTED` | 400 | Identity refused the new password. |
| `COMPLIMENTARY_*` | 400 / 422 | See subscriptions.md. |

## Audit

`User.Invite` (resource id from the result), `User.Suspend` and `User.Reactivate` (from the command), `Teacher.SetPhoneNumber` (resource id = the teacher, from the command) and `Subscription.GrantComplimentary` (from the result, with a diff) are audited. `User` is not an audited entity, so the user rows carry no diff. Accepting an invitation is auth and is not audited. See [audit-log.md](audit-log.md).
