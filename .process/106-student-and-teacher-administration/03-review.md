VERDICT: APPROVED

# Review — [E11.S3] Student and teacher administration (#106)

Reviewed against 01-plan.md, the gate conditions in 00-acceptance.md, 02-implementation.md, the dotnet and react skills, both testing conventions and .claude/rules/docs-sync.md. I read every changed and new API file end to end, plus the security-relevant web files (invite dialog, accept-invite page, set-password form, action buttons, status hook).

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Application/Users/SuspendUser/SuspendUserHandler.cs:25` and `api/Elmanhg.Domain/Identity/User.Administration.cs:10,19`: the active-admin count includes pending admin invitees (no password yet). Suppose real admins A and B plus a pending admin invite C suspend each other at the same moment. The lock serialises the two requests, but the second one counts B + C = 2 and passes, so the only remaining "active admin" has never signed in. The acting admin is also not re-checked as Active inside the lock. This needs a race plus a pending admin invite, and it can be recovered in the DB. Consider counting only admins with a password hash, or re-checking the actor inside the lock.
- `api/Elmanhg.Application/Users/CheckUserActive/CheckUserActiveHandler.cs:20-26`: stale-cache race. A request that reads "active" just before the suspend commits, then calls `Set` after the eviction at `SuspendUserHandler.cs:34`, caches `true` for up to `ActiveStatusCacheSeconds` (30 s). The window is bounded, and the same bound is already documented for other instances.
- `api/Elmanhg.Api/Authorization/ActiveUserTokenValidation.cs:32`: the JWT carries no stamp. After suspend and then reactivate within `CoreJwt:ExpirationHours` (1 h), a pre-suspension access token works again until it expires. Refresh tokens stay dead because the stamp was rotated. Worth one line in `docs/user-administration.md` Revocation.
- `docs/PRD.md` 10.4 says suspension "signs the user out everywhere ... refused on its next request". `docs/user-administration.md` Revocation gives the precise caveats: the per-instance 30 s window and open SignalR connections. They do not contradict each other, but the PRD line is stronger than the behaviour.
- `api/Elmanhg.Application/Users/InviteUser/InviteUserHandler.cs:34`: when Resend is on and the request is aborted, the cancellation exception is rethrown after the account was created. The invite is then audited as Failure while the user exists. A retry returns 409, and the admin sees the pending row. This is minor.
- `api/Elmanhg.Infrastructure/Identity/UserRepository.cs:14-20`: when the execution strategy retries, the callback runs again on the same context. `FindByIdAsync` then returns the tracked, already-suspended user, and the retry fails with `USER_ALREADY_SUSPENDED`. The plan code is followed verbatim. Transient-only.
- The branch is behind `origin/main` (#232 and #234 merged since). Per the PROGRESS lanes rule, merge it and regenerate OpenAPI, Orval and Postman before the PR.

## Verified
- **Policies.** Every new endpoint has a policy. `UsersController.cs:21,30,39,48` and the StudentsController profile and grant actions use `UsersManage`; progress and sessions use `ProgressViewAny`. Only `AuthController.AcceptInvitation` is `[AllowAnonymous]`, and it has `[EnableRateLimiting(AuthRateLimitPolicies.Credentials)]`, a per-IP fixed window.
- **Accept invitation.**
  - It needs a verified, unexpired, unused **email** OTP (`AcceptInvitationHandler.cs:18-24`). Core.OTP stores `CodeHash`, and `MarkUsed` enforces verified, not used and not expired.
  - There is no link token: the link carries no secret or PII (Decision 8).
  - Only a non-student with a null `PasswordHash` qualifies, so an active account gets 404. A suspended invitee gets 403.
  - Enumeration is only possible after proving control of the email.
  - Email-code login is Student-only, so an invitee cannot bypass accept.
  - A double accept is guarded by the Identity concurrency stamp.
- **Suspend.**
  - The self check comes first, then the last-admin check.
  - Both run inside `pg_advisory_xact_lock` with parameterised `ExecuteSqlAsync`.
  - The stamp is rotated in the same transaction, and the cache is evicted after commit.
- **Mutation checks I ran** in a scratch copy, not the worktree:
  - (a) Removing the self check fails `UserTests.Suspend_ByItself_*`, `SuspendUserHandlerTests.Handle_Self_*` and `UsersEndpointTests.Suspend_Self_Returns400`.
  - (b) Removing `AddActiveUserTokenValidation()` fails test 107, because the old access token gets 200 instead of 401.
  - (c) Replacing `UpdateSecurityStampAsync` with `UpdateAsync` fails test 107, because refresh gets 403 instead of 401. So stamp rotation, not just the IsActive check, is what kills the refresh token.
- **Token check is fail-closed.** A missing claim fails the request. A DB or cancellation exception propagates out of `OnTokenValidated`, so the JwtBearer handler rethrows and the request never authenticates. SignalR goes through the same JwtBearer scheme, so its connect and reconnect are checked too; open connections are the documented limit.
- **Complimentary grants.** Student only. Period comes from the catalogue. Ask a Teacher requires Base. A plan already held (including grace) is refused, so a grant cannot stack on or extend a held plan. It starts now, with a null `PaymobReference` and `CreatedBy` set to the admin. It is audited with a diff, which integration test 126 asserts.
- **Masking and PII.**
  - List and profile return masked values only; integration tests 98 and 118 assert the raw body.
  - Search matches the exact phone or normalised email only, plus a name fragment. Test 29 covers a partial phone.
  - Audit rows carry no command payload (`AuditBehaviour`), and `User` is not an audited entity.
  - Request logs redact query values (`RequestLogScrubber.Query`).
  - The invitation email senders log the role and status only, never the address or link (`FakeInvitationEmailSender.cs:10`, `ResendInvitationEmailSender.cs:37,42`). The display name is HTML-encoded. POST retries use a fixed Idempotency-Key.
- **Config.** `Users:*` and `InvitationEmail:*` are in `UsersOptions` (`ValidateOnStart`), `appsettings.example.json`, `ApiFactory`, `deploy/api.env.example`, `docs/deployment.md` and `docs/user-administration.md` Options. Startup refuses Resend without an https accept URL.
- **Test names.** All 135 API test names from the plan exist, and every web `it(...)` from rows 132–143 exists.
- **Error codes.** All 20 new codes are in both resx files and both web `common` locales.
- **Runs:**
  - `dotnet test api/ -c Release` with no `appsettings.json`: 3944/3944 passed.
  - Web: `tsc -b` clean, eslint clean, prettier clean, `vitest run` 1261/1261 (a first run under CPU load had worker timeouts; the clean re-run passed), `npm run build` ok, `perf:budget` all ok (admin-users 254/270).
  - OpenAPI: a fresh build matches the committed `v1.json`. Orval: `gen:api` gives no diff.
  - Guard grep: only test-code hits, as claimed.
- **Postman.** The Users folder has List students, Invite teacher (sets `invitedUserId`), List teachers, Assign subject, Suspend and Reactivate. The Students folder has profile, progress, sessions and grant. Auth has AcceptInvitation with noauth. Variables are added.
- **Docs.** They agree with the code:
  - PRD 7.1, 10.4, 11.2 and 17 rule 12 (gate 2, flagged as a deviation);
  - `docs/subscriptions.md` Complimentary grants;
  - `docs/audit-log.md` rows;
  - `docs/progress.md` Admin view;
  - `docs/performance.md` 3/4;
  - `docs/claude-design-prompt.md`;
  - `docs/prototype.md`;
  - the new `docs/user-administration.md`.
- **Deviations table** is accurate:
  - gate-1 email sender added;
  - `Action act` compile fix in `UserTests`;
  - Orval `query: false` entries;
  - `internal` helpers;
  - extra split-out files;
  - `GetUsers` parameter order.

## Test quality
- `SuspendUserHandlerTests` constrain the implementation: a real `MemoryCache` is used, and eviction and non-eviction are asserted, as are stamp Received/DidNotReceive and that the lock was used.
- `CheckUserActiveHandlerTests` assert the repository call count, which is the cache behaviour.
- `ActiveUserTokenValidationTests` assert the exact failure message and that the sender is not called without a claim. The registration itself is covered by integration test 107, as the mutation showed.
- `UsersEndpointTests.Suspend_SignedInStudent_RevokesAccessAndRefreshTokens` is the key test. It distinguishes stamp rotation (401) from the IsActive fallback (403), and it checks cache eviction because the cache is warmed first.
- `GetUsersFilterTests` compile the expression against real users, so the partial-phone and case tests are meaningful.
- No vacuous tests found.
