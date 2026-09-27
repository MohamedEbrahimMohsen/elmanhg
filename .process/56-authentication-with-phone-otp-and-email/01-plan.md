# Plan — Authentication with phone OTP and email (#56, E1.S3)

## Goal
After this ships, a student can create an account with a mobile number plus a one-time SMS code, or with an email and password, and sign in again the same way. Teachers and admins sign in with email and password; the first admin is seeded from configuration. The API issues a short-lived JWT with the role claim and keeps a rotating refresh token in an HttpOnly cookie. The web app replaces the stubbed dev session with real sign-up, OTP verification, sign-in, silent session restore on reload, 401 refresh-and-retry, and sign-out. OTP requests and credential attempts are rate limited. The SMS gateway is a config-selected `FakeSmsSender` that logs the code.

## Scope
**In:**
- All 5 sub-tasks. User aggregate (role, phone, email, display name, status). Register and login commands behind an `ISmsSender` port. JWT with role claims and rotating refresh tokens. Rate limiting through the OTP entity cooldown, Identity lockout and per-IP fixed windows. Sign-up, OTP verify, sign-in and sign-out in `web/`.
- Admin seed from `AdminSeed__*` configuration.
- Fixes to the vendored `Core.*` code that this story sits on:
  - `Core.OTP` threw plain `BaseException`, which surfaces as HTTP 500.
  - `GenerateOTPResult` returned the OTP code to the caller.
  - `ValidatePhoneNumber` set the length error as a message, not an error code.
  - `IRefreshTokenService` had no way to validate a token without the caller's claims.
- Postman folder `Auth`, README local-run notes.

**Out:**
- Admin creating Teachers or Admins, suspend or reactivate endpoints, "choose subjects" after sign-up. These belong to the PRD §10.4 user-management and onboarding stories. `User.Suspend()` ships only as the domain rule that the login guards test.
- Email confirmation and forgot or reset password. The PRD does not require them for v1 sign-up, and they need an email sender.
- Linking a phone and an email on one account.
- Landing page.
- Role-based API policies beyond `AuthenticatedUser`. No endpoint needs them yet.

**Deferred** (the orchestrator opens an issue for each):
1. **Real SMS provider adapter.** There are no gateway credentials or provider choice in this repo. `ISmsSender` plus `SmsProvider` enum is the seam: add an enum value and an adapter.
2. **Playwright E2E for the sign-in journey.** It needs a full-stack CI job (API, PostgreSQL and Vite together) plus Playwright browsers, and none exists. The journey is covered by MSW component tests and by API integration tests.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | What does "phone or email" mean for credentials? | Phone is passwordless: send OTP → verify → register or login with the `verificationId`. Email uses email + password. Teachers and admins use email + password only. | PRD §7.1 says "phone + OTP, or email". Morabh's `CreateUserHandler` consumes a verified OTP by `VerificationId`. |
| 2 | OTP flow endpoints | Reuse `Core.OTP` `GenerateOTPCommand` and `VerifyOTPCommand` unchanged in shape. `AuthController` sends them directly. `MapCoreOTPEndpoints` is **not** mapped. | Reuse-first. Keeps every auth route under `api/auth` with rate-limit attributes. |
| 3 | Where does the SMS port live? | `Core.OTP/Sms/ISmsSender.SendOtpAsync(phoneNumber, code, cancellationToken)`. `GenerateOTPHandler` calls it after `SaveChangesAsync`. `Code` is removed from `GenerateOTPResult`. | The handler that creates the code must deliver it. The Morabh result carries `// CODE HAS TO BE DELETED`. |
| 4 | Fake selection and dev visibility | `Sms:Provider` (`SmsProvider` enum, only `Fake`) is validated on start. A DI factory switches on it. `FakeSmsSender` logs the code at Information outside Production. In Production it logs a warning without the code. No dev endpoint. | Config-selected fake (constitution §0.4). Logging is the smallest dev path. A dev-only endpoint is extra attack surface. Integration tests swap in `RecordingSmsSender`. |
| 5 | Order in phone login and register | `otp.MarkUsed()` runs **first** (in memory), then the user lookup. A thrown error discards the tracked change, so the OTP stays usable. | Checking the user before verification would let anyone who requested an OTP for a number learn whether that number is registered. Running `MarkUsed` first also lets login's `PHONE_NUMBER_NOT_REGISTERED` hand the same `verificationId` to register. |
| 6 | Persistence without `SaveChangesAsync` | Register handlers persist through `UserManager.CreateAsync`. Its store auto-saves the shared scoped `AppDbContext`, which also writes the `MarkUsed` OTP change in the same transaction. Login-by-phone calls `otpRepository.SaveChangesAsync` once. Email login writes through `UserManager.ResetAccessFailedCountAsync` / `AccessFailedAsync`. Tests assert `CreateAsync` / `SaveChangesAsync` `Received(1)` or `DidNotReceive()`. | Mirrors Morabh `CreateUserHandler` and `LoginUserHandler`. `UserManager` is the Identity write path. |
| 7 | Username and lookups | Phone accounts: `UserName = PhoneNumber`, found with `FindByNameAsync`. Email accounts: `UserName = Email`, found with `FindByEmailAsync`. `IdentityOptions.User.RequireUniqueEmail = false`; email uniqueness is enforced by the handler (409). | Mirrors Morabh (UserName = phone). `RequireUniqueEmail = true` would reject phone accounts that have no email. |
| 8 | Role storage | `User.Role` (`UserRole` enum: Student, Teacher, Admin) is a column. The JWT `ClaimTypes.Role` comes from it. ASP.NET `AspNetRoles` / `AddToRoleAsync` are not used. | One source of truth. Morabh takes role claims from `user.Type`. |
| 9 | Status | `UserStatus { Active, Suspended }`, `User.IsActive`. Login (both), refresh and phone login throw 403 `USER_SUSPENDED`. Email login checks status **after** the password. | PRD §15 status. Checking after the password does not reveal suspension without the password. |
| 10 | Refresh token transport | The refresh token goes only in an `HttpOnly; Secure; SameSite=Strict; Path=/api/auth` cookie, expiring after `CoreJwt.RefreshTokenExpirationDays`. `AuthResult.RefreshToken` is `[JsonIgnore]`. The access token is in the body and kept in memory by web. | react-feature §13 / §6.19: no tokens in storage, refresh in an HttpOnly cookie. |
| 11 | Anonymous refresh | Add `ValidateTokenAsync(refreshToken, cancellationToken)` to `Core.Identity` `IRefreshTokenService`. The body is lifted from `JwtTokenService.GenerateTokenAsync(string, List<Claim>)`: unprotect, expiry check, security-stamp check, return the user. Every refresh rotates the refresh token. | Morabh's refresh needs a valid access token (`[Authorize]`), so an expired session could never refresh. Existing Core code does exactly this check. |
| 12 | Logout semantics | `POST api/auth/logout` requires the `AuthenticatedUser` policy. It calls `UpdateSecurityStampAsync` (invalidates every refresh token for the user) and deletes the cookie. The access token lives until expiry. | Stateless refresh tickets are revoked by the security stamp (the existing Core design). The trade-off is that sign-out signs out every device. |
| 13 | Access token lifetime | `CoreJwt:ExpirationHours` changes from 72 to **1** in `appsettings.example.json`. | Logout cannot revoke access tokens. `JwtOptions` is in whole hours. |
| 14 | Rate limiting | Three layers: (a) `Otp.Reissue` cooldown and daily cap, now 429; (b) Identity lockout (`MaxFailedAccessAttempts` 5, 15 min) for email login, 429 `USER_LOCKED_OUT`; (c) ASP.NET `AddRateLimiter` fixed windows per remote IP. Policy `auth-otp-requests` covers `otp/send`. Policy `auth-credentials` covers verify, login and register. | Covers per-phone, per-account and per-IP abuse with no new package. |
| 15 | Rate-limit rejection body | `RateLimiterOptions.OnRejected` throws `RateLimitExceededCoreException(ErrorCodes.TooManyRequests)`. `app.UseRateLimiter()` sits right after `CoreExceptionMiddleware`, so the exception is formatted there (camelCase `code`, 429). `RejectionStatusCode = 429`. | One JSON error contract (skill §8.6). No second serializer. |
| 16 | Where do the new options live? | `AuthOptions` and `AdminSeedOptions` in `Elmanhg.Application/Shared/Options/`, bound in `AddApplication`. `SmsOptions` in Infrastructure, bound in `AddInfrastructure`. Identity options are bound from section `IdentityOptions` in `Program.cs`, inside the existing `AddCoreIdentity(identityOptions:)` hook. | Constitution §2: registration only in the layer DI files. The Identity hook already exists in Program. |
| 17 | Domain exception type | `User.Suspend()` throws `Core.Errors.BusinessRuleViolationCoreException` (400) with `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes.UserAlreadySuspended`. | The skill's `BusinessRuleViolationException` does not exist in `core-libraries`. No new exception types. Morabh keeps domain codes in `Morabh.Domain/Shared/Exceptions/ErrorCodes.cs`. |
| 18 | `DisplayName` column | PostgreSQL `text`, not null. The length cap lives only in `AuthOptions.DisplayNameMaxLength` (validator). Enum columns use `varchar(50)` through a named constant. | Skill §8.1: caps are config. `text` has no cost in PostgreSQL. |
| 19 | Admin seed trigger | `Program.cs` sends `SeedAdminCommand` once after `Build()`. The handler no-ops when `AdminSeed:Email` is empty or the admin already exists. A create failure throws 500 `ADMIN_SEED_FAILED` and fails startup. | A local run gets an admin from `.env`. Tests and the build-time OpenAPI run have no `AdminSeed` and never touch the DB. |
| 20 | `[AllowAnonymous]` on auth actions | Every action except `logout` is `[AllowAnonymous]`. The class is `[Authorize]`. | Sign-in cannot require sign-in. Skill §10 "missing auth" applies to protected resources. |
| 21 | OpenAPI operation ids | Each action sets its route `Name` (`SendOtp`, `VerifyOtp`, `RegisterWithPhone`, `RegisterWithEmail`, `LoginWithPhone`, `LoginWithEmail`, `RefreshAccessToken`, `Logout`). Each action declares `[ProducesResponseType<T>(StatusCodes.Status200OK)]`. | Orval function and hook names come from `operationId`. Response types give typed models. |
| 22 | Web session boot | `main.tsx` installs auth handlers, `await restoreSession()` (POST refresh with the cookie), then renders. Guards stay synchronous on `sessionStore`. | Guards (`requireRole`) already read the store synchronously. |
| 23 | Web 401 handling | `http` attaches `Authorization: Bearer` from the in-memory token. On 401 for any URL except `/api/auth/refresh` it runs a single-flight `refreshOnce()` and retries once. On failure it calls `notifyExpired()`, which clears the session; the router guard redirects to `/login`. | react-feature §13 "single-flight refresh on 401, retry once, then redirect to login". |
| 24 | Web shared-to-feature coupling | `src/shared/lib/authToken.ts` holds the token and a registered `{ refresh, onExpired }` pair. The session feature registers them. `shared` never imports `features`. | Layering: shared has no dependency on features. |
| 25 | Sign-in navigation | After `startSession`, no explicit `navigate`. `sessionStore.set` triggers `router.invalidate()`. The `/login` `beforeLoad` (`redirectSignedIn(session, search.redirect)`) and `/signup` `beforeLoad` redirect. | Existing guard wiring (the #55 dev sign-in worked the same way). |
| 26 | OTP step placement | The OTP step is a second step inside `/login` and `/signup` (component state), not a separate route. | The `verificationId` stays out of the URL and history. Screen count matches the story (sign-up, OTP verify, login, logout). |
| 27 | Retry after a verified OTP | `OtpForm` remembers `verified = true` after the first successful `verifyOtp`. A later submit skips verify and only retries the register/login call. After a resend it stores the new `verificationId` and resets `verified`. | `Otp.Verify` on an already-verified OTP returns `OTP_ALREADY_VERIFIED`. `Reissue` issues a new `VerificationId`. |
| 28 | Mutation toasts | Sign-in and sign-up errors show inline or in the form alert (`Form` + `applyServerErrors`). A resend success shows a toast. No success toast on sign-in, because the page navigates away. | react-feature §12: blocking form errors are inline. |
| 29 | Dev session stub | Delete `DevSignInPage`, `devSessions` and `VITE_DEV_SESSION_ROLE`. Move the fixture to `src/test/sessions.ts` as `testSessions` (same values). | The story replaces the stub. The shell tests still need a session fixture. |
| 30 | Client phone rule | `^01[0125][0-9]{8}$` as named constant `egyptianMobilePattern`. The server (`CoreOtp:PhoneCodes` + `PhoneLength`) is the authority. | Instant feedback. The server rejects anything else with 422 anyway. |
| 31 | `CoreOtp:PhoneCodes` | The example config lists `["010","011","012","015"]`. The binder appends these to the class default list, so duplicates are harmless (`Any(StartsWith)`). | 015 (WE) is missing from the Core defaults. |
| 32 | Integration-test cookies | The tests use `factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false })`. They read `Set-Cookie` and send the `Cookie` header by hand. | `CookieContainer` does not send `Secure` cookies over `http://localhost`. Manual handling also asserts the cookie attributes. |
| 33 | Docs | README gains the OTP-in-logs and admin-seed notes. No `/docs` divergence: PRD §7.1 and §15 already describe phone+OTP or email and the User fields. | `.claude/rules/docs-sync.md`. |

## Existing code touched
| File | Change |
|------|--------|
| `api/core-libraries/Core.OTP/Entities/OTP.cs` | `Reissue`: cooldown throws `new RateLimitExceededCoreException(ErrorCodes.OTPReissueCooldown, context: <same dictionary>)`, max reissue throws `new RateLimitExceededCoreException(ErrorCodes.OTPReachedMaxReissueCount)`. `MarkUsed`: its three throws become `new BadRequestCoreException(ErrorCodes.X)` (same codes). Nothing else changes. |
| `api/core-libraries/Core.OTP/GenerateOTP/GenerateOTPHandler.cs` | Constructor adds `ISmsSender smsSender` (last parameter). After `SaveChangesAsync`: `await smsSender.SendOtpAsync(request.PhoneNumber, code, cancellationToken).ConfigureAwait(false);`. The result is built without `Code`. |
| `api/core-libraries/Core.OTP/GenerateOTP/GenerateOTPResult.cs` | Remove the `string Code` parameter and its comment. |
| `api/core-libraries/Core.OTP/VerifyOTP/VerifyOTPHandler.cs` | Null OTP throws `new BadRequestCoreException(ErrorCodes.OtpInvalid)`. After save: `if (errorCode == ErrorCodes.OTPReachedMaxAttempts) { throw new RateLimitExceededCoreException(errorCode); }` then `if (!string.IsNullOrEmpty(errorCode)) { throw new BadRequestCoreException(errorCode); }`. |
| `api/core-libraries/Core.Identity/Tokens/RefreshToken/IRefreshTokenService.cs` | Add `Task<TUser> ValidateTokenAsync(string refreshToken, CancellationToken cancellationToken);` |
| `api/core-libraries/Core.Identity/Tokens/RefreshToken/RefreshTokenService.cs` | Implement `ValidateTokenAsync`: (1) `if (string.IsNullOrEmpty(refreshToken)) throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenIsRequired)`; (2) `var ticket = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector.Unprotect(refreshToken)`; (3) `if (ticket?.Properties?.ExpiresUtc is null \|\| ticket.Properties.ExpiresUtc < DateTimeOffset.UtcNow) throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenIsExpired)`; (4) `var user = await signInManager.ValidateSecurityStampAsync(ticket.Principal).ConfigureAwait(false)`; (5) `if (user is null) throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenUserNotFound)`; (6) `return user`. Braces on every `if`. `ErrorCodes` = `Core.Identity.Exceptions.ErrorCodes`. |
| `api/core-libraries/Core.Validation/Extensions/StringValidationExtensions.cs` | In `ValidatePhoneNumber`: `.Length(phoneLength).WithMessage(ValidationErrors.ValidationPhoneNumberMustBeXDigits)` becomes `.Length(phoneLength).WithErrorCode(ValidationErrors.ValidationPhoneNumberMustBeXDigits)`. |
| `api/Elmanhg.Domain/Identity/User.cs` | See Domain behaviour. |
| `api/Elmanhg.Application/Elmanhg.Application.csproj` | Add `<ProjectReference Include="..\core-libraries\Core.OTP\Core.OTP.csproj" />`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | After the validators: `services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` and `services.AddOptions<AdminSeedOptions>().BindConfiguration(AdminSeedOptions.SectionName);` |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddOptions<SmsOptions>().BindConfiguration(SmsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` `services.AddScoped<FakeSmsSender>();` `services.AddScoped<ISmsSender>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SmsOptions>>().Value.Provider switch { SmsProvider.Fake => serviceProvider.GetRequiredService<FakeSmsSender>(), _ => throw new InvalidOperationException("Unsupported Sms:Provider."), });` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add `ConfigureUsers(modelBuilder)` after `base.OnModelCreating` (contract in the `AppDbContext.ConfigureUsers` row under Infrastructure). |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by the migration. |
| `api/Elmanhg.Api/Elmanhg.Api.csproj` | Add `<ProjectReference Include="..\core-libraries\Core.OTP\Core.OTP.csproj" />`. |
| `api/Elmanhg.Api/Program.cs` | (1) `AddCoreIdentity(..., identityOptions: options => builder.Configuration.GetSection(nameof(IdentityOptions)).Bind(options))`. (2) Right after it in `#region IDENTITY`: `builder.Services.AddAuthorizationBuilder().AddPolicy(DefaultCodes.AuthenticatedUser, policy => policy.RequireAuthenticatedUser());` (3) After `AddCoreCQRS()`: `builder.Services.AddCoreOtp(builder.Configuration);` (4) After `AddInfrastructure()`: `builder.Services.AddAuthRateLimiting();` (5) After `var app = builder.Build();`: `#region SEED` with `await using (var scope = app.Services.CreateAsyncScope()) { await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SeedAdminCommand()); }` `#endregion`. (6) `app.UseRateLimiter();` directly after `app.UseMiddleware<CoreExceptionMiddleware>();`. No other line moves. |
| `api/Elmanhg.Api/appsettings.example.json` | `CoreJwt.ExpirationHours` 72 → 1. Add sections: `"CoreOtp": { "Secret": "", "OtpLength": 6, "PhoneCodes": ["010","011","012","015"], "PhoneLength": 11, "ExpirationMinutes": 5, "MaxVerificationAttempts": 3, "ReissueCooldownSeconds": 60, "MaxReissueCount": 5, "ReissueBlockCooldownInHours": 24 }`, `"Sms": { "Provider": "Fake" }`, `"Auth": { "DisplayNameMaxLength": 100, "EmailMaxLength": 256, "RefreshTokenCookieName": "elmanhg_refresh", "RefreshTokenCookiePath": "/api/auth", "RefreshTokenCookieSecure": true, "OtpRequestPermitLimit": 5, "OtpRequestWindowSeconds": 600, "CredentialPermitLimit": 10, "CredentialWindowSeconds": 60 }`, `"AdminSeed": { "Email": "", "Password": "", "DisplayName": "" }`, `"IdentityOptions": { "User": { "RequireUniqueEmail": false }, "Password": { "RequiredLength": 8, "RequireDigit": true, "RequireLowercase": false, "RequireUppercase": false, "RequireNonAlphanumeric": false, "RequiredUniqueChars": 1 }, "Lockout": { "AllowedForNewUsers": true, "MaxFailedAccessAttempts": 5, "DefaultLockoutTimeSpan": "00:15:00" } }`. Tell the dev to mirror this into the local gitignored `appsettings.json`. |
| `.env.example` (repo root) | Append `CoreOtp__Secret=change-me-local-otp-hmac-secret`, `AdminSeed__Email=admin@elmanhg.local`, `AdminSeed__Password=change-me-Admin1`, `AdminSeed__DisplayName=Admin`. |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | Add every key in the Error codes section (both files). |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. Must contain the 8 `operationId`s from Decision 21. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add a `public RecordingSmsSender Sms { get; } = new();` property. Add in-memory keys `CoreOtp:Secret`="elmanhg-tests-otp-secret" (with a WHY comment like the JWT key), `Sms:Provider`="Fake", every `Auth:*` key with the example values except `OtpRequestPermitLimit`="1000" and `CredentialPermitLimit`="1000", and every `IdentityOptions:*` key from the example. Add `builder.ConfigureTestServices(services => { services.RemoveAll<ISmsSender>(); services.AddSingleton<ISmsSender>(Sms); });`. |
| `postman/elmanhg.postman_collection.json` | Add collection variables `phoneNumber`, `verificationId`, `otpCode`. Add folder `Auth` with 8 requests (API surface table). Each request has `auth: noauth` except Logout. Test scripts: SendOtp sets `verificationId` from `pm.response.json().verificationId`. The four login/register requests and Refresh set `accessToken` from `pm.response.json().accessToken`. Each asserts status 200. |
| `postman/local.postman_environment.json` | Add `phoneNumber` ("01012345678"), `verificationId`, `otpCode` (empty). |
| `README.md` | Under "Run the backend locally", add two lines: OTP codes appear in the API console (`FakeSmsSender`) while `Sms__Provider=Fake`; the admin account is seeded on start from `AdminSeed__*` in `.env`. |
| `web/src/shared/lib/http.ts` | Split into `send()` (adds `Authorization: Bearer ${getAccessToken()}` when non-null) and `parse<T>()` (the current `!ok` / 204 / json logic). `http()`: `send`. If `response.status === 401 && !url.startsWith(refreshPath)`: if `await refreshOnce()`, re-`send` once; if the retry is 401 too, call `notifyExpired()`. If the refresh fails, call `notifyExpired()`. Then `parse`. `const refreshPath = '/api/auth/refresh'` (named constant). |
| `web/src/app/env.ts`, `web/src/vite-env.d.ts`, `web/.env.example` | Remove `VITE_DEV_SESSION_ROLE`, and the `roles` import in env.ts. |
| `web/src/main.tsx` | `const sessionStore = createSessionStore(null); installAuthHandlers({ sessionStore, queryClient });` then `void restoreSession(sessionStore).then(() => { createRoot(rootElement).render(...) })`. Keep the `rootElement` null check before it. Remove the `devSessions` import. |
| `web/src/routes/login.tsx` | `component: LoginPage` (imported from `@/features/session`). |
| `web/src/features/session/index.ts` | Remove `devSessions`, `DevSignInPage`. Export `LoginPage`, `SignUpPage`, `restoreSession`, `installAuthHandlers`, `startSession`, `clearSession`, `toSession`. |
| `web/src/features/session/hooks/useSignOut.ts` | Returns `() => void`. Uses generated `useLogout({ mutation: { onSettled: () => { clearSession(store, queryClient); } } })` and calls `mutate()`. `AppBar.tsx` is untouched. |
| `web/src/features/session/i18n/en.json`, `ar.json` | Replace the content with the keys in the Web copy section. |
| `web/src/shared/i18n/en.json`, `ar.json` | Add `errors.*` keys (Web copy section). |
| `web/src/test/setup.ts` | In `afterEach`: `setAccessToken(null); registerAuthHandlers(null);` |
| `web/src/routeTree.gen.ts` | Regenerated (adds `/signup`). |
| `web/src/shared/api/generated/**` | Regenerated by `npm run gen:api`: `auth/auth.ts`, `auth/auth.msw.ts`, `zod/auth/auth.zod.ts`, `model/*`. Never hand-edited. |

## Files to create

### api/ — Core
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/core-libraries/Core.OTP/Sms/ISmsSender.cs` | interface | `namespace Core.OTP.Sms; public interface ISmsSender { Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken); }`. New, no Morabh equivalent. |

### api/ — Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| 2 | `api/Elmanhg.Domain/Identity/UserRole.cs` | enum | `namespace Elmanhg.Domain.Identity; public enum UserRole { Student, Teacher, Admin }`. Adapted from Morabh `Morabh.Domain/Users/UserType.cs`. |
| 3 | `api/Elmanhg.Domain/Identity/UserStatus.cs` | enum | `public enum UserStatus { Active, Suspended }`. New. |
| 4 | `api/Elmanhg.Domain/SharedKernel/DefaultCodes.cs` | static class | `namespace Elmanhg.Domain.SharedKernel; public static class DefaultCodes { // AUTH  public const string AuthenticatedUser = "AuthenticatedUser"; }` |
| 5 | `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | static class | `namespace Elmanhg.Domain.SharedKernel.Exceptions; public static class ErrorCodes { // USERS  public const string UserAlreadySuspended = "USER_ALREADY_SUSPENDED"; }`. Mirrors `Morabh.Domain/Shared/Exceptions/ErrorCodes.cs`. |

### api/ — Application
Namespaces mirror folders (`Elmanhg.Application.Auth.LoginWithEmail`, …). Every handler is a one-line `sealed class`, uses `.ConfigureAwait(false)` everywhere, has no try/catch, and puts braces on every `if`.

| # | Path | Type | Contract |
|---|------|------|----------|
| 6 | `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | static class | `namespace Elmanhg.Application.Exceptions;` Flat constants, grouped `// AUTH` / `// VALIDATION` / `// PLATFORM`. Values in the Error codes section. Mirrors `Morabh.Application/Exceptions/ErrorCodes.cs`. |
| 7 | `api/Elmanhg.Application/Shared/Options/AuthOptions.cs` | sealed class | `namespace Elmanhg.Application.Shared.Options; public const string SectionName = "Auth";` Properties with `{ get; set; }`: `[Range(1, int.MaxValue)] int DisplayNameMaxLength`, `[Range(1, int.MaxValue)] int EmailMaxLength`, `[Required] string RefreshTokenCookieName = default!`, `[Required] string RefreshTokenCookiePath = default!`, `bool RefreshTokenCookieSecure`, `[Range(1, int.MaxValue)] int OtpRequestPermitLimit`, `[Range(1, int.MaxValue)] int OtpRequestWindowSeconds`, `[Range(1, int.MaxValue)] int CredentialPermitLimit`, `[Range(1, int.MaxValue)] int CredentialWindowSeconds`. |
| 8 | `api/Elmanhg.Application/Shared/Options/AdminSeedOptions.cs` | sealed class | `SectionName = "AdminSeed"`. Properties: `string Email { get; set; } = string.Empty; string Password { get; set; } = string.Empty; string DisplayName { get; set; } = string.Empty;` |
| 9 | `api/Elmanhg.Application/Auth/Shared/AuthUserResult.cs` | sealed record | `public sealed record AuthUserResult(Guid Id, string DisplayName, string Role, string? PhoneNumber, string? Email);`. Client-facing, no `LocalizedText`. |
| 10 | `api/Elmanhg.Application/Auth/Shared/AuthResult.cs` | sealed record | `public sealed record AuthResult(string AccessToken, AuthUserResult User, [property: JsonIgnore] string RefreshToken);`. Adapted from Morabh `LoginUserResult` / `CreateUserResult`. |
| 11 | `api/Elmanhg.Application/Auth/Shared/AuthResultGenerator.cs` | static class | `public static AuthResult Generate(User user, string accessToken, string refreshToken)` → `new AuthResult(accessToken, new AuthUserResult(user.Id, user.DisplayName, user.Role.ToString(), user.PhoneNumber, user.Email), refreshToken)`. |
| 12 | `api/Elmanhg.Application/Auth/Shared/UserClaimsExtensions.cs` | static class | Adapted from Morabh `Morabh.Application/Extensions/UserExtensions.cs`. `public static List<Claim> GetUserClaims(this User user)`: always `UserIdClaimType` = `user.Id.ToString("D")`, `UserNameClaimType` = `user.UserName ?? string.Empty`, `CreatedAtUnixTimeSecondsClaimType` = `user.CreationDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)`, `ClaimTypes.Role` = `user.Role.ToString()`. Adds `PhoneNumberClaimType` only when `PhoneNumber` is not null or whitespace, and `ClaimTypes.Email` only when `Email` is not null or whitespace. Constants come from `CurrentUserService.Constants`. |
| 13 | `.../Auth/RegisterWithPhone/RegisterWithPhoneCommand.cs` | sealed record | `(Guid VerificationId, string DisplayName) : IRequest<AuthResult>` |
| 14 | `.../Auth/RegisterWithPhone/RegisterWithPhoneValidator.cs` | sealed class | Constructor `(IOptions<AuthOptions> authOptions)`. `VerificationId`: `ValidateRequired(ErrorCodes.OtpVerificationIdInvalidFormat)`. `DisplayName`: `ValidateRequired(ErrorCodes.DisplayNameRequired).ValidateMaxLength(options.DisplayNameMaxLength, ErrorCodes.DisplayNameTooLong)`. |
| 15 | `.../Auth/RegisterWithPhone/RegisterWithPhoneHandler.cs` | sealed class | Adapted from Morabh `Users/Auth/CreateUser/CreateUserHandler.cs`. Dependencies: `(UserManager<User> userManager, IOtpRepository otpRepository, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService)`. Steps: (1) `otp = await otpRepository.FindByVerificationId(request.VerificationId, cancellationToken)`; null → `BadRequestCoreException(ErrorCodes.OtpInvalid)`. (2) `otp.MarkUsed()`. (3) `if (await userManager.FindByNameAsync(otp.PhoneNumber) is not null)` → `ConflictCoreException(ErrorCodes.PhoneNumberAlreadyRegistered)`. (4) `user = User.CreateStudentWithPhone(request.DisplayName, otp.PhoneNumber)`. (5) `result = await userManager.CreateAsync(user)`; `!result.Succeeded` → `BadRequestCoreException(ErrorCodes.UserCreationFailed, innerException: new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code))))`. (6) `accessToken = tokenService.GenerateTokenAsync(user.GetUserClaims())`. (7) `refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken)`. (8) `return AuthResultGenerator.Generate(user, accessToken, refreshToken)`. |
| 16 | `.../Auth/RegisterWithEmail/RegisterWithEmailCommand.cs` | sealed record | `(string DisplayName, string Email, string Password) : IRequest<AuthResult>` |
| 17 | `.../Auth/RegisterWithEmail/RegisterWithEmailValidator.cs` | sealed class | Constructor `(IOptions<AuthOptions> authOptions, IOptions<IdentityOptions> identityOptions)`. `DisplayName`: as #14. `Email`: `ValidateRequired(ErrorCodes.EmailRequired).ValidateEmail(ErrorCodes.EmailInvalid).ValidateMaxLength(options.EmailMaxLength, ErrorCodes.EmailTooLong)`. `Password`: `ValidateRequired(ErrorCodes.PasswordIsRequired).ValidateMinLength(identity.Password.RequiredLength, ErrorCodes.PasswordTooShort).ValidateHasNumber(ErrorCodes.PasswordMustContainDigit)`. |
| 18 | `.../Auth/RegisterWithEmail/RegisterWithEmailHandler.cs` | sealed class | New shape of Morabh `CreateUserHandler`. Dependencies: `(UserManager<User> userManager, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService)`. Steps: (1) `FindByEmailAsync(request.Email)` not null → `ConflictCoreException(ErrorCodes.EmailAlreadyRegistered)`. (2) `user = User.CreateStudentWithEmail(request.DisplayName, request.Email)`. (3) `CreateAsync(user, request.Password)`; failure → as #15 step 5. (4)–(6) as #15 steps 6–8. |
| 19 | `.../Auth/LoginWithPhone/LoginWithPhoneCommand.cs` | sealed record | `(Guid VerificationId) : IRequest<AuthResult>` |
| 20 | `.../Auth/LoginWithPhone/LoginWithPhoneValidator.cs` | sealed class | `VerificationId`: `ValidateRequired(ErrorCodes.OtpVerificationIdInvalidFormat)`. |
| 21 | `.../Auth/LoginWithPhone/LoginWithPhoneHandler.cs` | sealed class | Adapted from Morabh `Users/Auth/LoginUser/LoginUserHandler.cs` (OTP instead of PIN). Dependencies: `(UserManager<User> userManager, IOtpRepository otpRepository, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService)`. Steps: (1) OTP lookup, null → `BadRequestCoreException(ErrorCodes.OtpInvalid)`. (2) `otp.MarkUsed()`. (3) `user = await userManager.FindByNameAsync(otp.PhoneNumber)`; null → `NotFoundCoreException(ErrorCodes.PhoneNumberNotRegistered)`. (4) `!user.IsActive` → `ForbiddenCoreException(ErrorCodes.UserSuspended)`. (5) `await otpRepository.SaveChangesAsync(cancellationToken)`. (6) tokens, then return (as #15 steps 6–8). |
| 22 | `.../Auth/LoginWithEmail/LoginWithEmailCommand.cs` | sealed record | `(string Email, string Password) : IRequest<AuthResult>` |
| 23 | `.../Auth/LoginWithEmail/LoginWithEmailValidator.cs` | sealed class | `Email`: `ValidateRequired(ErrorCodes.EmailRequired).ValidateEmail(ErrorCodes.EmailInvalid)`. `Password`: `ValidateRequired(ErrorCodes.PasswordIsRequired)`. |
| 24 | `.../Auth/LoginWithEmail/LoginWithEmailHandler.cs` | sealed class | Adapted from Morabh `LoginUserHandler.cs`. Dependencies: `(UserManager<User> userManager, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService)`. Steps: (1) `user = await userManager.FindByEmailAsync(request.Email)`; null → `BadRequestCoreException(ErrorCodes.UserInvalidLogin)`. (2) `await userManager.IsLockedOutAsync(user)` → `RateLimitExceededCoreException(ErrorCodes.UserLockedOut)`. (3) `!await userManager.CheckPasswordAsync(user, request.Password)` → `await userManager.AccessFailedAsync(user)` then `BadRequestCoreException(ErrorCodes.UserInvalidLogin)`. (4) `!user.IsActive` → `ForbiddenCoreException(ErrorCodes.UserSuspended)`. (5) `await userManager.ResetAccessFailedCountAsync(user)`. (6) tokens, then return. |
| 25 | `.../Auth/RefreshAccessToken/RefreshAccessTokenCommand.cs` | sealed record | `(string RefreshToken) : IRequest<AuthResult>`. Adapted from Morabh `Tokens/UserRefreshAccessToken/UserRefreshAccessTokenCommand.cs`. |
| 26 | `.../Auth/RefreshAccessToken/RefreshAccessTokenValidator.cs` | sealed class | `RefreshToken`: `ValidateRequired(ErrorCodes.RefreshTokenIsRequired)`. Adapted from Morabh `UserRefreshAccessTokenValidator.cs`. |
| 27 | `.../Auth/RefreshAccessToken/RefreshAccessTokenHandler.cs` | sealed class | Dependencies: `(ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService)`. Steps: (1) `user = await refreshTokenService.ValidateTokenAsync(request.RefreshToken, cancellationToken)`. (2) `!user.IsActive` → `ForbiddenCoreException(ErrorCodes.UserSuspended)`. (3) `accessToken = tokenService.GenerateTokenAsync(user.GetUserClaims())`. (4) `refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken)` (rotation). (5) return the generated result. |
| 28 | `.../Auth/Logout/LogoutCommand.cs` | sealed record | `public sealed record LogoutCommand : IRequest;` |
| 29 | `.../Auth/Logout/LogoutHandler.cs` | sealed class | Dependencies: `(UserManager<User> userManager, ICurrentUserService currentUserService) : IRequestHandler<LogoutCommand>`. Steps: (1) `currentUserService.UserId` null or default → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`. (2) `user = await userManager.FindByIdAsync(currentUserService.UserId.Value.ToString())`; null → `UnauthorizedCoreException(ErrorCodes.UserNotFound)`. (3) `await userManager.UpdateSecurityStampAsync(user)`. |
| 30 | `.../Auth/SeedAdmin/SeedAdminCommand.cs` | sealed record | `public sealed record SeedAdminCommand : IRequest;` |
| 31 | `.../Auth/SeedAdmin/SeedAdminHandler.cs` | sealed class | New, no Morabh equivalent (Morabh `RoleSeed.cs` is commented out). Dependencies: `(UserManager<User> userManager, IOptions<AdminSeedOptions> adminSeedOptions) : IRequestHandler<SeedAdminCommand>`. Steps: (1) `options.Email` is null or whitespace → return. (2) `FindByEmailAsync(options.Email)` not null → return. (3) `user = User.CreateAdmin(options.DisplayName, options.Email)`. (4) `CreateAsync(user, options.Password)`; failure → `InternalServerErrorCoreException(ErrorCodes.AdminSeedFailed, innerException: new InvalidOperationException(<joined error codes>))`. |

### api/ — Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| 32 | `api/Elmanhg.Infrastructure/Sms/SmsProvider.cs` | enum | `namespace Elmanhg.Infrastructure.Sms; public enum SmsProvider { Fake }` |
| 33 | `api/Elmanhg.Infrastructure/Sms/SmsOptions.cs` | sealed class | `SectionName = "Sms"; [Required] public SmsProvider? Provider { get; set; }` |
| 34 | `api/Elmanhg.Infrastructure/Sms/FakeSmsSender.cs` | sealed class | New, no Morabh equivalent. `public sealed class FakeSmsSender(ILogger<FakeSmsSender> logger, IHostEnvironment hostEnvironment) : ISmsSender`. `SendOtpAsync`: if `hostEnvironment.IsProduction()`, call `logger.LogWarning("FakeSmsSender is active in Production; the OTP was not delivered.")`. Otherwise call `logger.LogInformation("FakeSmsSender OTP for {PhoneNumber}: {Code}", phoneNumber, code)`. Return `Task.CompletedTask`. Not unit-tested (infrastructure adapter; integration tests swap it out). |
| 35 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddUserProfileFields.cs` (+ `.Designer.cs`) | migration | `dotnet ef migrations add AddUserProfileFields --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api`. Expected ops: only `AddColumn` on `AspNetUsers`: `DisplayName text NOT NULL default ''`, `Role varchar(50) NOT NULL default ''`, `Status varchar(50) NOT NULL default ''` (EF-generated defaults are accepted; no rows exist). No Drop or Rename. |
| — | `AppDbContext.ConfigureUsers` (in existing file) | method | `private const int EnumColumnMaxLength = 50;` with WHY comment ("enum names are short identifiers; width is a schema invariant"). `private static void ConfigureUsers(ModelBuilder modelBuilder) { modelBuilder.Entity<User>(builder => { builder.Property(x => x.DisplayName).IsRequired(); builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(EnumColumnMaxLength); builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength); }); }` |

### api/ — API
| # | Path | Type | Contract |
|---|------|------|----------|
| 36 | `api/Elmanhg.Api/Controllers/Auth/AuthController.cs` | controller | Adapted from Morabh `Morabh.APIs/Controllers/Auth/UserAuthController.cs`. `namespace Elmanhg.Api.Controllers.Auth; [ApiController] [Route("api/auth")] [Authorize] public class AuthController(IMediator mediator, IOptions<AuthOptions> authOptions, IOptions<JwtOptions> jwtOptions) : ControllerBase`. Eight actions, per the API surface table. Actions that return `AuthResult`: `var result = await mediator.Send(command, cancellationToken); Response.AppendRefreshToken(result.RefreshToken, authOptions.Value, jwtOptions.Value.RefreshTokenExpirationDays); return Ok(result);`. Refresh builds `new RefreshAccessTokenCommand(Request.Cookies[authOptions.Value.RefreshTokenCookieName] ?? string.Empty)`. Logout: `await mediator.Send(new LogoutCommand(), cancellationToken); Response.DeleteRefreshToken(authOptions.Value); return Ok();`. No `await` without a following statement; no `ConfigureAwait` (controller). |
| 37 | `api/Elmanhg.Api/Controllers/Auth/RefreshTokenCookieExtensions.cs` | static class | `public static void AppendRefreshToken(this HttpResponse response, string refreshToken, AuthOptions options, int expirationDays)` → `response.Cookies.Append(options.RefreshTokenCookieName, refreshToken, Build(options, DateTimeOffset.UtcNow.AddDays(expirationDays)))`. `public static void DeleteRefreshToken(this HttpResponse response, AuthOptions options)` → `response.Cookies.Delete(options.RefreshTokenCookieName, Build(options, null))`. `private static CookieOptions Build(AuthOptions options, DateTimeOffset? expires)` → `new CookieOptions { HttpOnly = true, Secure = options.RefreshTokenCookieSecure, SameSite = SameSiteMode.Strict, Path = options.RefreshTokenCookiePath, Expires = expires, IsEssential = true }`. |
| 38 | `api/Elmanhg.Api/RateLimiting/AuthRateLimitPolicies.cs` | static class | `public const string OtpRequests = "auth-otp-requests"; public const string Credentials = "auth-credentials";` |
| 39 | `api/Elmanhg.Api/RateLimiting/AuthRateLimiting.cs` | static class | `private const string UnknownClientPartition = "unknown";` `public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)`: `services.AddRateLimiter(options => { options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; options.OnRejected = (context, cancellationToken) => throw new RateLimitExceededCoreException(ErrorCodes.TooManyRequests); });` plus `services.AddOptions<RateLimiterOptions>().Configure<IOptions<AuthOptions>>((rateLimiter, authOptions) => { rateLimiter.AddPolicy(AuthRateLimitPolicies.OtpRequests, httpContext => CreateFixedWindow(httpContext, auth.OtpRequestPermitLimit, auth.OtpRequestWindowSeconds)); rateLimiter.AddPolicy(AuthRateLimitPolicies.Credentials, httpContext => CreateFixedWindow(httpContext, auth.CredentialPermitLimit, auth.CredentialWindowSeconds)); });` `private static RateLimitPartition<string> CreateFixedWindow(HttpContext httpContext, int permitLimit, int windowSeconds)` → `RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClientPartition, _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromSeconds(windowSeconds), QueueLimit = 0 })`. |

### api/ — Tests
| # | Path | Contract |
|---|------|----------|
| 40 | `api/Elmanhg.Tests/Builders/OtpBuilder.cs` | `public sealed class OtpBuilder`. Private fields: `_phoneNumber = "01012345678"`, `_verified = false`, `_reissueCooldownSeconds = 60`, `_maxReissueCount = 5`, `_maxVerificationAttempts = 3`. Fluent methods: `ForPhone(string)`, `Verified()`, `WithReissueCooldownSeconds(int)`, `WithMaxReissueCount(int)`, `WithMaxVerificationAttempts(int)`. `public const string CodeHash = "code-hash";`. `Build()` → `Otp.Create(_phoneNumber, CodeHash, 5, _maxVerificationAttempts, _reissueCooldownSeconds, _maxReissueCount, 24)`, then `otp.Verify(CodeHash)` when verified. Builds only through the factory. |
| 41 | `api/Elmanhg.Tests/Application/Features/Auth/UserManagerSubstitute.cs` | `public static class UserManagerSubstitute { public static UserManager<User> Create() { return Substitute.For<UserManager<User>>(Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null); } }` (null-forgiving as needed). |
| 42 | `api/Elmanhg.Tests/Integration/Infrastructure/RecordingSmsSender.cs` | `public sealed class RecordingSmsSender : ISmsSender` backed by `ConcurrentDictionary<string, string>`. `SendOtpAsync` stores the latest code per phone. `public string LatestCodeFor(string phoneNumber)` returns the stored code or throws `InvalidOperationException`. |
| 43 | `api/Elmanhg.Tests/Integration/Auth/AuthTestClient.cs` | `public static class AuthTestClient`. `HttpClient Create(ApiFactory factory)` → `factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false })`. `string NewPhoneNumber()` → `"010" + (Math.Abs(Guid.NewGuid().GetHashCode()) % 100_000_000).ToString("D8", CultureInfo.InvariantCulture)`. `string NewEmail()` → `$"{Guid.NewGuid():N}@elmanhg.test"`. `Task<Guid> SendOtpAsync(HttpClient, string phone, CancellationToken)`. `Task<Guid> SendAndVerifyOtpAsync(HttpClient, ApiFactory, string phone, CancellationToken)`. `Task<HttpResponseMessage> RegisterByPhoneAsync(HttpClient, ApiFactory, string phone, CancellationToken)`. `string ReadRefreshCookie(HttpResponseMessage)` returns `"elmanhg_refresh=<value>"` from `Set-Cookie`. `Task<User> SeedUserAsync(ApiFactory, User user, string? password, bool suspended, CancellationToken)` uses a scope with `UserManager<User>`: create, then `user.Suspend()` + `UpdateAsync` when suspended. |
| 44–63 | Test classes | See Test plan. |

### web/
| # | Path | Type | Contract |
|---|------|------|----------|
| 64 | `web/src/shared/lib/authToken.ts` | module | `let accessToken: string \| null = null; let handlers: AuthHandlers \| null = null; let inFlight: Promise<boolean> \| null = null;` `export interface AuthHandlers { refresh: () => Promise<boolean>; onExpired: () => void }` `export function getAccessToken(): string \| null` `export function setAccessToken(token: string \| null): void` `export function registerAuthHandlers(next: AuthHandlers \| null): void` `export function refreshOnce(): Promise<boolean>` (no handlers → `Promise.resolve(false)`; otherwise `inFlight ??= handlers.refresh().finally(() => { inFlight = null; })`, return `inFlight`). `export function notifyExpired(): void` (calls `handlers?.onExpired()`). |
| 65 | `web/src/features/session/authSession.ts` | module | `const apiRoles = { Student: 'student', Teacher: 'teacher', Admin: 'admin' } as const satisfies Record<string, Role>;` `export function toSession(user: AuthUserResult): Session` → `{ userId: user.id, displayName: user.displayName, role }`; an unknown role throws `Error('Unknown role')`. `export function startSession(store: SessionStore, result: AuthResult): void` → `setAccessToken(result.accessToken); store.set(toSession(result.user))`. `export function clearSession(store: SessionStore, queryClient: QueryClient): void` → `setAccessToken(null); queryClient.clear(); store.set(null)`. `export async function restoreSession(store: SessionStore): Promise<boolean>` → try generated `refreshAccessToken()` → `startSession` → `true`; catch an `ApiError` → `setAccessToken(null); store.set(null); return false`; rethrow anything else. `export function installAuthHandlers({ sessionStore, queryClient }): void` → `registerAuthHandlers({ refresh: () => restoreSession(sessionStore), onExpired: () => { clearSession(sessionStore, queryClient); } })`. |
| 66 | `web/src/features/session/hooks/useStartSession.ts` | hook | `export function useStartSession(): (result: AuthResult) => void`. Uses `useSessionStore()` and calls `startSession(store, result)`. |
| 67 | `web/src/features/session/schemas/fields.ts` | zod | `export const egyptianMobilePattern = /^01[0125][0-9]{8}$/;` `export const otpCodePattern = /^[0-9]{6}$/;` `displayNameField = z.string().trim().min(1, { error: 'validation.required' }).max(100, { error: 'session:validation.displayNameLength' })` (100 mirrors `Auth:DisplayNameMaxLength`, in a named const `displayNameMaxLength`). `phoneNumberField = z.string().regex(egyptianMobilePattern, { error: 'session:validation.phone' })`. `emailField = z.email({ error: 'session:validation.email' })`. `newPasswordField = z.string().min(8, { error: 'session:validation.passwordLength' }).regex(/[0-9]/, { error: 'session:validation.passwordDigit' })` (8 in named const `passwordMinLength`). |
| 68 | `web/src/features/session/schemas/phoneStartSchema.ts` | zod | `phoneSignInSchema = z.object({ phoneNumber: phoneNumberField })`. `phoneSignUpSchema = phoneSignInSchema.extend({ displayName: displayNameField })`. Types `PhoneSignInValues`, `PhoneSignUpValues`. |
| 69 | `web/src/features/session/schemas/otpSchema.ts` | zod | `otpSchema = z.object({ code: z.string().regex(otpCodePattern, { error: 'session:validation.code' }) })`. |
| 70 | `web/src/features/session/schemas/emailSignInSchema.ts` | zod | `{ email: emailField, password: z.string().min(1, { error: 'validation.required' }) }` |
| 71 | `web/src/features/session/schemas/emailSignUpSchema.ts` | zod | `{ displayName: displayNameField, email: emailField, password: newPasswordField }` |
| 72 | `web/src/features/session/components/AuthLayout.tsx` | component | `AuthLayoutProps { title: string; children: ReactNode; footer: ReactNode }`. `<main id="main" className="mx-auto flex min-h-dvh max-w-layout flex-col justify-center gap-6 px-4">`, `<h1 className="font-display text-h1 font-bold lg:text-h1-desktop">`, card `<section className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">{children}</section>`, footer `<p className="text-caption text-text-muted">`. |
| 73 | `web/src/features/session/components/MethodSwitch.tsx` | component | `MethodSwitchProps { value: 'phone' \| 'email'; onChange: (method: 'phone' \| 'email') => void }`. `<div role="group" aria-label={t('signIn.methodLabel')} className="flex gap-2">` with two `<Button type="button" variant={active ? 'primary' : 'secondary'} aria-pressed={active}>` labelled `signIn.phoneTab` / `signIn.emailTab`. |
| 74 | `web/src/features/session/components/PhoneStartForm.tsx` | component | `export interface PhoneStart { phoneNumber: string; verificationId: string; displayName: string }`. `PhoneStartFormProps { withDisplayName: boolean; onCodeSent: (start: PhoneStart) => void }`. RHF with `phoneSignUpSchema` when `withDisplayName`, else `phoneSignInSchema`. Fields: optional `displayName` (`fields.displayName`, autoComplete `name`), `phoneNumber` (`type="tel"`, autoComplete `tel`, description `fields.phoneHint`). Submit via generated `useSendOtp().mutateAsync({ data: { phoneNumber } })`, then `onCodeSent({ phoneNumber, verificationId: result.verificationId, displayName: values.displayName ?? '' })`. `serverErrorFields`: `VALIDATION_PHONE_NUMBER_INVALID_CELLULAR_CODE`, `VALIDATION_PHONE_NUMBER_MUST_BE_X_DIGITS`, `VALIDATION_PHONE_NUMBER_MUST_BE_ONLY_DIGITS` → `phoneNumber`. `FormRootError`; submit `actions.sendCode`. |
| 75 | `web/src/features/session/components/OtpForm.tsx` | component | `OtpFormProps { start: PhoneStart; onVerified: (verificationId: string) => Promise<void>; onResent: (verificationId: string) => void; onChangeNumber: () => void }`. State `verified` (boolean). Shows `t('otp.sentTo', { phoneNumber })`. Field `code` (`fields.code`, autoComplete `one-time-code`). Submit: if not verified, `await verifyOtp.mutateAsync({ data: { verificationId, code } })` and set `verified = true`; then `await onVerified(verificationId)`. `serverErrorFields`: `OTP_NOT_MATCHED`, `OTP_EXPIRED`, `OTP_INVALID_FORMAT` → `code`. Secondary buttons (`type="button"`): `actions.resendCode` calls `sendOtp.mutateAsync({ data: { phoneNumber } })`. On success: `setVerified(false)`, `onResent(result.verificationId)`, `toast(t('otp.resent'))`. On error: `form.setError('root.server', { message: 'errors.<code>' })`. `actions.changeNumber` calls `onChangeNumber`. |
| 76 | `web/src/features/session/components/PhoneSignIn.tsx` | component | State `start: PhoneStart \| null`. When null, render `<PhoneStartForm withDisplayName={false} onCodeSent={setStart} />`. Otherwise render `<OtpForm ... onVerified={async (id) => { startSession(await loginWithPhone.mutateAsync({ data: { verificationId: id } })) }} onResent={(id) => { setStart({ ...start, verificationId: id }) }} onChangeNumber={() => { setStart(null) }} />`. Here `startSession` comes from `useStartSession()`. |
| 77 | `web/src/features/session/components/PhoneSignUp.tsx` | component | Same as #76 with `withDisplayName` and `registerWithPhone.mutateAsync({ data: { verificationId: id, displayName: start.displayName } })`. |
| 78 | `web/src/features/session/components/EmailSignInForm.tsx` | component | `emailSignInSchema`. Fields `email` (`type="email"`, `email`) and `password` (`type="password"`, `current-password`). Submit: `startSession(await loginWithEmail.mutateAsync({ data: values }))`. No field map: `USER_INVALID_LOGIN` / `USER_LOCKED_OUT` / `USER_SUSPENDED` show in `FormRootError`. Submit `actions.signIn`. |
| 79 | `web/src/features/session/components/EmailSignUpForm.tsx` | component | `emailSignUpSchema`. Fields `displayName`, `email`, `password` (`new-password`, description `fields.passwordHint`). Submit via `registerWithEmail`, then `startSession`. `serverErrorFields`: `EMAIL_ALREADY_REGISTERED` / `EMAIL_INVALID` / `EMAIL_TOO_LONG` → `email`; `PASSWORD_TOO_SHORT` / `PASSWORD_MUST_CONTAIN_DIGIT` → `password`; `DISPLAY_NAME_TOO_LONG` → `displayName`. Submit `actions.createAccount`. |
| 80 | `web/src/features/session/pages/LoginPage.tsx` | page | `const [method, setMethod] = useState<'phone' \| 'email'>('phone')`. `<AuthLayout title={t('signIn.title')} footer={<>{t('signIn.noAccount')} <Link to="/signup">{t('signIn.createAccount')}</Link></>}>` containing `<MethodSwitch>` then `PhoneSignIn` or `EmailSignInForm`. |
| 81 | `web/src/features/session/pages/SignUpPage.tsx` | page | Same shape: title `signUp.title`; footer `signUp.haveAccount` plus a `Link to="/login"` labelled `signUp.signIn`; renders `PhoneSignUp` or `EmailSignUpForm`. |
| 82 | `web/src/routes/signup.tsx` | route | `createFileRoute('/signup')({ beforeLoad: ({ context }) => { redirectSignedIn(context.sessionStore.get(), undefined); }, component: SignUpPage })` |
| 83 | `web/src/test/sessions.ts` | fixture | `export const testSessions: Record<Role, Session>`, with the exact values of the deleted `devSessions.ts`. |
| 84–92 | Test files | See Test plan. |

**Delete:** `web/src/features/session/devSessions.ts`, `web/src/features/session/pages/DevSignInPage.tsx`, `web/src/features/session/pages/DevSignInPage.test.tsx`.

## Error codes
`E` = `Elmanhg.Application.Exceptions.ErrorCodes`. `D` = domain `ErrorCodes`. `Core.OTP`, `Core.Identity` and `Core.Validation` codes already exist; they only get resx strings.

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| E.UserNotAuthenticated | USER_NOT_AUTHENTICATED | LogoutHandler | UnauthorizedCoreException | 401 |
| E.UserNotFound | USER_NOT_FOUND | LogoutHandler | UnauthorizedCoreException | 401 |
| E.UserInvalidLogin | USER_INVALID_LOGIN | LoginWithEmailHandler | BadRequestCoreException | 400 |
| E.UserLockedOut | USER_LOCKED_OUT | LoginWithEmailHandler | RateLimitExceededCoreException | 429 |
| E.UserSuspended | USER_SUSPENDED | LoginWithEmail, LoginWithPhone, RefreshAccessToken | ForbiddenCoreException | 403 |
| E.UserCreationFailed | USER_CREATION_FAILED | RegisterWithPhone, RegisterWithEmail | BadRequestCoreException | 400 |
| E.PhoneNumberAlreadyRegistered | PHONE_NUMBER_ALREADY_REGISTERED | RegisterWithPhone | ConflictCoreException | 409 |
| E.PhoneNumberNotRegistered | PHONE_NUMBER_NOT_REGISTERED | LoginWithPhone | NotFoundCoreException | 404 |
| E.EmailAlreadyRegistered | EMAIL_ALREADY_REGISTERED | RegisterWithEmail | ConflictCoreException | 409 |
| E.OtpInvalid | OTP_INVALID | RegisterWithPhone, LoginWithPhone (and Core VerifyOTP) | BadRequestCoreException | 400 |
| E.OtpVerificationIdInvalidFormat | OTP_VERIFICATION_ID_INVALID_FORMAT | RegisterWithPhone/LoginWithPhone validators | validation | 422 |
| E.RefreshTokenIsRequired | REFRESH_TOKEN_IS_REQUIRED | RefreshAccessTokenValidator | validation | 422 |
| E.DisplayNameRequired | DISPLAY_NAME_REQUIRED | Register validators | validation | 422 |
| E.DisplayNameTooLong | DISPLAY_NAME_TOO_LONG | Register validators | validation | 422 |
| E.EmailRequired | EMAIL_REQUIRED | RegisterWithEmail, LoginWithEmail validators | validation | 422 |
| E.EmailInvalid | EMAIL_INVALID | same | validation | 422 |
| E.EmailTooLong | EMAIL_TOO_LONG | RegisterWithEmailValidator | validation | 422 |
| E.PasswordIsRequired | PASSWORD_IS_REQUIRED | RegisterWithEmail, LoginWithEmail validators | validation | 422 |
| E.PasswordTooShort | PASSWORD_TOO_SHORT | RegisterWithEmailValidator | validation | 422 |
| E.PasswordMustContainDigit | PASSWORD_MUST_CONTAIN_DIGIT | RegisterWithEmailValidator | validation | 422 |
| E.TooManyRequests | TOO_MANY_REQUESTS | AuthRateLimiting.OnRejected | RateLimitExceededCoreException | 429 |
| E.AdminSeedFailed | ADMIN_SEED_FAILED | SeedAdminHandler | InternalServerErrorCoreException | 500 |
| D.UserAlreadySuspended | USER_ALREADY_SUSPENDED | User.Suspend | BusinessRuleViolationCoreException | 400 |
| Core.OTP | OTP_REISSUE_COOLDOWN | Otp.Reissue | RateLimitExceededCoreException | 429 |
| Core.OTP | OTP_REACHED_MAX_REISSUE_COUNT | Otp.Reissue | RateLimitExceededCoreException | 429 |
| Core.OTP | OTP_REACHED_MAX_ATTEMPTS | VerifyOTPHandler | RateLimitExceededCoreException | 429 |
| Core.OTP | OTP_NOT_MATCHED / OTP_EXPIRED / OTP_ALREADY_VERIFIED | VerifyOTPHandler | BadRequestCoreException | 400 |
| Core.OTP | OTP_NOT_VERIFIED / OTP_ALREADY_USED / OTP_EXPIRED | Otp.MarkUsed | BadRequestCoreException | 400 |
| Core.OTP | OTP_INVALID_FORMAT | VerifyOTPValidator | validation | 422 |
| Core.Identity | REFRESH_TOKEN_IS_EXPIRED / REFRESH_TOKEN_USER_NOT_FOUND | RefreshTokenService.ValidateTokenAsync | UnauthorizedCoreException | 401 |
| Core.Validation | VALIDATION_PHONE_NUMBER_IS_REQUIRED / _MUST_BE_ONLY_DIGITS / _MUST_BE_X_DIGITS / _INVALID_CELLULAR_CODE | GenerateOTPValidator | validation | 422 |

**Resx strings** (key = value; Arabic without tashkeel):

| Key | English | Arabic |
|-----|---------|--------|
| USER_NOT_AUTHENTICATED | You need to sign in first. | يجب تسجيل الدخول اولا. |
| USER_NOT_FOUND | Your account could not be found. | تعذر العثور على حسابك. |
| USER_INVALID_LOGIN | Email or password is incorrect. | البريد الالكتروني او كلمة المرور غير صحيحة. |
| USER_LOCKED_OUT | Too many failed attempts. Try again in a few minutes. | محاولات فاشلة كثيرة. حاول مرة اخرى بعد دقائق. |
| USER_SUSPENDED | This account is suspended. Contact support. | هذا الحساب موقوف. تواصل مع الدعم. |
| USER_ALREADY_SUSPENDED | This account is already suspended. | هذا الحساب موقوف بالفعل. |
| USER_CREATION_FAILED | We could not create your account. Try again. | تعذر انشاء حسابك. حاول مرة اخرى. |
| PHONE_NUMBER_ALREADY_REGISTERED | An account already uses this mobile number. Sign in instead. | يوجد حساب مسجل بهذا الرقم. سجل الدخول بدلا من ذلك. |
| PHONE_NUMBER_NOT_REGISTERED | No account uses this mobile number. Create an account first. | لا يوجد حساب بهذا الرقم. انشئ حسابا اولا. |
| EMAIL_ALREADY_REGISTERED | An account already uses this email. | يوجد حساب مسجل بهذا البريد الالكتروني. |
| OTP_INVALID | This verification request is not valid. Request a new code. | طلب التحقق غير صالح. اطلب رمزا جديدا. |
| OTP_VERIFICATION_ID_INVALID_FORMAT | The verification request is missing. | طلب التحقق غير موجود. |
| OTP_INVALID_FORMAT | Enter the 6-digit code. | ادخل الرمز المكون من 6 ارقام. |
| OTP_EXPIRED | This code has expired. Request a new one. | انتهت صلاحية الرمز. اطلب رمزا جديدا. |
| OTP_NOT_MATCHED | The code is incorrect. | الرمز غير صحيح. |
| OTP_ALREADY_VERIFIED | This code was already verified. | تم التحقق من هذا الرمز بالفعل. |
| OTP_NOT_VERIFIED | Verify the code first. | تحقق من الرمز اولا. |
| OTP_ALREADY_USED | This code was already used. Request a new one. | تم استخدام هذا الرمز. اطلب رمزا جديدا. |
| OTP_REACHED_MAX_ATTEMPTS | Too many wrong codes. Request a new one. | محاولات خاطئة كثيرة. اطلب رمزا جديدا. |
| OTP_REISSUE_COOLDOWN | Wait {minutes} min {seconds} sec before requesting a new code. | انتظر {minutes} دقيقة و{seconds} ثانية قبل طلب رمز جديد. |
| OTP_REACHED_MAX_REISSUE_COUNT | You reached the code request limit. Try again later. | وصلت للحد الاقصى لطلب الرموز. حاول لاحقا. |
| REFRESH_TOKEN_IS_REQUIRED | Your session has ended. Sign in again. | انتهت جلستك. سجل الدخول مرة اخرى. |
| REFRESH_TOKEN_IS_EXPIRED | Your session has ended. Sign in again. | انتهت جلستك. سجل الدخول مرة اخرى. |
| REFRESH_TOKEN_USER_NOT_FOUND | Your session has ended. Sign in again. | انتهت جلستك. سجل الدخول مرة اخرى. |
| DISPLAY_NAME_REQUIRED | Enter your name. | ادخل اسمك. |
| DISPLAY_NAME_TOO_LONG | Name is too long. | الاسم طويل جدا. |
| EMAIL_REQUIRED | Enter your email. | ادخل بريدك الالكتروني. |
| EMAIL_INVALID | Enter a valid email address. | ادخل بريدا الكترونيا صحيحا. |
| EMAIL_TOO_LONG | Email is too long. | البريد الالكتروني طويل جدا. |
| PASSWORD_IS_REQUIRED | Enter your password. | ادخل كلمة المرور. |
| PASSWORD_TOO_SHORT | Password is too short. | كلمة المرور قصيرة جدا. |
| PASSWORD_MUST_CONTAIN_DIGIT | Password must include a number. | يجب ان تحتوي كلمة المرور على رقم. |
| TOO_MANY_REQUESTS | Too many requests. Wait a moment and try again. | طلبات كثيرة. انتظر قليلا وحاول مرة اخرى. |
| ADMIN_SEED_FAILED | The admin account could not be seeded. | تعذر انشاء حساب المدير. |
| VALIDATION_PHONE_NUMBER_IS_REQUIRED | Enter your mobile number. | ادخل رقم الموبايل. |
| VALIDATION_PHONE_NUMBER_MUST_BE_ONLY_DIGITS | Mobile number must contain digits only. | رقم الموبايل يجب ان يحتوي على ارقام فقط. |
| VALIDATION_PHONE_NUMBER_MUST_BE_X_DIGITS | Mobile number must be 11 digits. | رقم الموبايل يجب ان يكون 11 رقما. |
| VALIDATION_PHONE_NUMBER_INVALID_CELLULAR_CODE | Mobile number must start with 010, 011, 012 or 015. | رقم الموبايل يجب ان يبدأ بـ 010 او 011 او 012 او 015. |

## Domain behaviour
`api/Elmanhg.Domain/Identity/User.cs` keeps the existing audit and soft-delete members and its implicit public parameterless constructor (required by Identity's `TUser : new()`). It adds:
```csharp
public string DisplayName { get; private set; } = string.Empty;
public UserRole Role { get; private set; }
public UserStatus Status { get; private set; }
public bool IsActive => Status == UserStatus.Active;

public static User CreateStudentWithPhone(string displayName, string phoneNumber)
{
    var user = Create(displayName, UserRole.Student, userName: phoneNumber);
    user.PhoneNumber = phoneNumber;
    user.PhoneNumberConfirmed = true;
    return user;
}

public static User CreateStudentWithEmail(string displayName, string email)
{
    var user = Create(displayName, UserRole.Student, userName: email);
    user.Email = email;
    user.EmailConfirmed = false;
    return user;
}

public static User CreateAdmin(string displayName, string email)
{
    var user = Create(displayName, UserRole.Admin, userName: email);
    user.Email = email;
    user.EmailConfirmed = true;
    return user;
}

public void Suspend()
{
    if (Status == UserStatus.Suspended)
    {
        throw new BusinessRuleViolationCoreException(ErrorCodes.UserAlreadySuspended);
    }

    Status = UserStatus.Suspended;
    UpdationDate = DateTimeOffset.UtcNow;
}

private static User Create(string displayName, UserRole role, string userName)
{
    var now = DateTimeOffset.UtcNow;
    return new User
    {
        Id = Guid.NewGuid(),
        UserName = userName,
        DisplayName = displayName,
        Role = role,
        Status = UserStatus.Active,
        CreationDate = now,
        UpdationDate = now,
    };
}
```
`ErrorCodes` here is `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`. `BusinessRuleViolationCoreException` comes from `Core.Errors` (reachable through Core.DDD). No Teacher factory: Teachers are created by the user-management story.

## API surface
Base `api/auth`. The class has `[Authorize]`. Responses are `200` with the listed body. Every error uses the Core error shape `{ code, message, data }`.

| Method | Route (Name) | Auth | Rate limit | Request body | Response |
|---|---|---|---|---|---|
| POST | `otp/send` (`SendOtp`) | `[AllowAnonymous]` | `OtpRequests` | `GenerateOTPCommand { phoneNumber }` | `GenerateOTPResult { verificationId, expiresAt, nextAllowedReissueAt, verificationAttempts, reissueCount, maxVerificationAttempts, maxReissueCount }` |
| POST | `otp/verify` (`VerifyOtp`) | `[AllowAnonymous]` | `Credentials` | `VerifyOTPCommand { code, verificationId }` | `VerifyOTPResult {}` |
| POST | `register/phone` (`RegisterWithPhone`) | `[AllowAnonymous]` | `Credentials` | `RegisterWithPhoneCommand { verificationId, displayName }` | `AuthResult { accessToken, user }` + `Set-Cookie` |
| POST | `register/email` (`RegisterWithEmail`) | `[AllowAnonymous]` | `Credentials` | `RegisterWithEmailCommand { displayName, email, password }` | `AuthResult` + `Set-Cookie` |
| POST | `login/phone` (`LoginWithPhone`) | `[AllowAnonymous]` | `Credentials` | `LoginWithPhoneCommand { verificationId }` | `AuthResult` + `Set-Cookie` |
| POST | `login/email` (`LoginWithEmail`) | `[AllowAnonymous]` | `Credentials` | `LoginWithEmailCommand { email, password }` | `AuthResult` + `Set-Cookie` |
| POST | `refresh` (`RefreshAccessToken`) | `[AllowAnonymous]` | — | none (cookie `elmanhg_refresh`) | `AuthResult` + rotated `Set-Cookie` |
| POST | `logout` (`Logout`) | `[Authorize(Policy = DefaultCodes.AuthenticatedUser)]` | — | none | `200` empty + cookie deleted |

Commands are bound directly with `[FromBody]` (no `Requests.cs`; the HTTP shape equals the command). Each action declares `[ProducesResponseType<T>(StatusCodes.Status200OK)]`, with `T` = `GenerateOTPResult`, `VerifyOTPResult` or `AuthResult`. Logout declares `[ProducesResponseType(StatusCodes.Status200OK)]`.

## Web copy
**`features/session/i18n/en.json`** (the `ar.json` values follow in the same order):
- `signIn`:
  - `title` "Sign in" / "تسجيل الدخول"
  - `methodLabel` "Sign-in method" / "طريقة الدخول"
  - `phoneTab` "Mobile" / "رقم الموبايل"
  - `emailTab` "Email" / "البريد الإلكتروني"
  - `noAccount` "New to Elmanhg?" / "جديد على المنهج؟"
  - `createAccount` "Create an account" / "إنشاء حساب"
- `signUp`:
  - `title` "Create account" / "إنشاء حساب"
  - `haveAccount` "Already have an account?" / "لديك حساب بالفعل؟"
  - `signIn` "Sign in" / "تسجيل الدخول"
- `fields`:
  - `displayName` "Your name" / "اسمك"
  - `phoneNumber` "Mobile number" / "رقم الموبايل"
  - `phoneHint` "11 digits, starting with 010, 011, 012 or 015" / "11 رقمًا تبدأ بـ 010 أو 011 أو 012 أو 015"
  - `email` "Email" / "البريد الإلكتروني"
  - `password` "Password" / "كلمة المرور"
  - `passwordHint` "At least 8 characters, including a number" / "8 أحرف على الأقل وتتضمن رقمًا"
  - `code` "Verification code" / "رمز التحقق"
- `actions`:
  - `sendCode` "Send code" / "إرسال الرمز"
  - `verify` "Verify" / "تحقق"
  - `resendCode` "Resend code" / "إعادة إرسال الرمز"
  - `changeNumber` "Change number" / "تغيير الرقم"
  - `signIn` "Continue" / "متابعة"
  - `createAccount` "Create account" / "إنشاء الحساب"
- `otp`:
  - `sentTo` "We sent a 6-digit code to {phoneNumber}." / "أرسلنا رمزًا من 6 أرقام إلى {phoneNumber}."
  - `resent` "A new code is on its way." / "تم إرسال رمز جديد."
- `validation`:
  - `phone` "Enter an 11-digit mobile number starting with 010, 011, 012 or 015." / "أدخل رقم موبايل من 11 رقمًا يبدأ بـ 010 أو 011 أو 012 أو 015."
  - `email` "Enter a valid email address." / "أدخل بريدًا إلكترونيًا صحيحًا."
  - `passwordLength` "Password must be at least 8 characters." / "كلمة المرور 8 أحرف على الأقل."
  - `passwordDigit` "Password must include a number." / "يجب أن تتضمن كلمة المرور رقمًا."
  - `code` "Enter the 6-digit code." / "أدخل الرمز المكوّن من 6 أرقام."
  - `displayNameLength` "Name must be 100 characters or fewer." / "الاسم 100 حرف كحد أقصى."

The sign-in submit button reads "Continue", which keeps it distinct from the "Sign in" heading and link.

**`shared/i18n/{en,ar}.json` → `errors`:** add every code the web can receive. Use the English and Arabic texts of the resx table (Arabic may use normal orthography with hamza). Codes: `USER_INVALID_LOGIN`, `USER_LOCKED_OUT`, `USER_SUSPENDED`, `USER_CREATION_FAILED`, `PHONE_NUMBER_ALREADY_REGISTERED`, `PHONE_NUMBER_NOT_REGISTERED`, `EMAIL_ALREADY_REGISTERED`, `EMAIL_INVALID`, `EMAIL_TOO_LONG`, `PASSWORD_TOO_SHORT`, `PASSWORD_MUST_CONTAIN_DIGIT`, `DISPLAY_NAME_TOO_LONG`, `OTP_INVALID`, `OTP_INVALID_FORMAT`, `OTP_EXPIRED`, `OTP_NOT_MATCHED`, `OTP_ALREADY_USED`, `OTP_NOT_VERIFIED`, `OTP_REACHED_MAX_ATTEMPTS`, `OTP_REACHED_MAX_REISSUE_COUNT`, `TOO_MANY_REQUESTS`, `VALIDATION_PHONE_NUMBER_INVALID_CELLULAR_CODE`, `VALIDATION_PHONE_NUMBER_MUST_BE_X_DIGITS`, `VALIDATION_PHONE_NUMBER_MUST_BE_ONLY_DIGITS`. One exception: `OTP_REISSUE_COOLDOWN` becomes "Please wait before requesting a new code." / "انتظر قليلًا قبل طلب رمز جديد." (no placeholders client-side).

## Test plan
### api — unit (xUnit v3, NSubstitute, FluentAssertions 7; `TestContext.Current.CancellationToken`)
Every handler success asserts the result and the persisting call `Received(1)`. Every throwing branch asserts the exception type, `ErrorCode`, and that the persisting call `DidNotReceive()`. The persisting call is `SaveChangesAsync` / `CreateAsync` / `UpdateSecurityStampAsync`, per Decision 6.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `Tests/Domain/Identity/UserTests` | `CreateStudentWithPhone_Always_CreatesActiveStudentWithConfirmedPhone` | Role Student, Status Active, UserName == PhoneNumber, PhoneNumberConfirmed, DisplayName, non-empty Id |
| 2 | 〃 | `CreateStudentWithEmail_Always_CreatesActiveStudentWithEmailUserName` | Role Student, UserName == Email, EmailConfirmed false, PhoneNumber null |
| 3 | 〃 | `CreateAdmin_Always_CreatesActiveAdminWithConfirmedEmail` | Role Admin, EmailConfirmed true |
| 4 | 〃 | `Suspend_ActiveUser_SetsSuspendedAndStampsUpdationDate` | Status Suspended, IsActive false, UpdationDate ≥ time captured before the call |
| 5 | 〃 | `Suspend_AlreadySuspended_ThrowsBusinessRuleViolation` | `BusinessRuleViolationCoreException`, `USER_ALREADY_SUSPENDED` |
| 6 | `Tests/Core/Otp/OtpTests` | `Reissue_BeforeCooldown_ThrowsRateLimitExceeded` | `RateLimitExceededCoreException`, `OTP_REISSUE_COOLDOWN`, context has `minutes` |
| 7 | 〃 | `Reissue_PastMaxReissueCount_ThrowsRateLimitExceeded` | builder cooldown 0, max 0; the first reissue succeeds and the second throws `OTP_REACHED_MAX_REISSUE_COUNT` |
| 8 | 〃 | `MarkUsed_NotVerified_ThrowsBadRequest` | `BadRequestCoreException`, `OTP_NOT_VERIFIED` |
| 9 | 〃 | `MarkUsed_AlreadyUsed_ThrowsBadRequest` | verified, marked used once; the second call throws `OTP_ALREADY_USED` |
| 10 | 〃 | `MarkUsed_Verified_SetsIsUsed` | IsUsed true |
| 11 | `Tests/Core/Otp/GenerateOTPHandlerTests` | `Handle_NewPhone_CreatesOtpAndSendsGeneratedCode` | `AddAsync` Received(1); `SaveChangesAsync` Received(1); `SendOtpAsync(phone, <generator output>)` Received(1); `VerificationId` non-empty |
| 12 | 〃 | `Handle_ExistingOtpInCooldown_ThrowsRateLimitAndSendsNothing` | `OTP_REISSUE_COOLDOWN`; `SaveChangesAsync` and `SendOtpAsync` DidNotReceive |
| 13 | `Tests/Core/Otp/VerifyOTPHandlerTests` | `Handle_UnknownVerificationId_ThrowsBadRequestOtpInvalid` | `BadRequestCoreException`, `OTP_INVALID`, save DidNotReceive |
| 14 | 〃 | `Handle_WrongCode_SavesAttemptAndThrowsOtpNotMatched` | `BadRequestCoreException`, `OTP_NOT_MATCHED`, save Received(1) (the attempt is persisted by design) |
| 15 | 〃 | `Handle_AttemptsExhausted_ThrowsRateLimitExceeded` | builder max attempts 0; `RateLimitExceededCoreException`, `OTP_REACHED_MAX_ATTEMPTS` |
| 16 | 〃 | `Handle_CorrectCode_VerifiesAndSaves` | otp.IsVerified true, save Received(1) |
| 17 | `Tests/Core/Otp/GenerateOTPValidatorTests` | `Validate_ValidMobileNumber_Passes` | "01512345678" is valid |
| 18 | 〃 | `Validate_Empty_FailsWithPhoneRequired` | `VALIDATION_PHONE_NUMBER_IS_REQUIRED` |
| 19 | 〃 | `Validate_NonDigits_FailsWithOnlyDigits` | `VALIDATION_PHONE_NUMBER_MUST_BE_ONLY_DIGITS` |
| 20 | 〃 | `Validate_WrongLength_FailsWithMustBeXDigits` | `VALIDATION_PHONE_NUMBER_MUST_BE_X_DIGITS` (proves the Core fix) |
| 21 | 〃 | `Validate_UnknownPrefix_FailsWithInvalidCellularCode` | "01312345678" gives `VALIDATION_PHONE_NUMBER_INVALID_CELLULAR_CODE` |
| 22 | `Tests/Application/Features/Auth/RegisterWithPhone/RegisterWithPhoneHandlerTests` | `Handle_VerifiedOtpNewPhone_CreatesStudentAndReturnsTokens` | `CreateAsync(Arg.Is<User>(role Student, phone))` Received(1); otp.IsUsed; result.User.Role "Student"; result.RefreshToken is the refresh service's value |
| 23 | 〃 | `Handle_UnknownVerificationId_ThrowsOtpInvalid` | BadRequest `OTP_INVALID`; CreateAsync DidNotReceive |
| 24 | 〃 | `Handle_UnverifiedOtp_ThrowsOtpNotVerified` | BadRequest `OTP_NOT_VERIFIED`; CreateAsync DidNotReceive |
| 25 | 〃 | `Handle_PhoneAlreadyRegistered_ThrowsConflict` | Conflict `PHONE_NUMBER_ALREADY_REGISTERED`; CreateAsync DidNotReceive |
| 26 | 〃 | `Handle_IdentityRejectsUser_ThrowsUserCreationFailed` | `CreateAsync` returns `IdentityResult.Failed(...)`; BadRequest `USER_CREATION_FAILED`; refresh `GenerateTokenAsync` DidNotReceive |
| 27 | `…/RegisterWithPhone/RegisterWithPhoneValidatorTests` | `Validate_ValidCommand_Passes` | no errors |
| 28 | 〃 | `Validate_EmptyVerificationId_FailsWithVerificationIdInvalidFormat` | code |
| 29 | 〃 | `Validate_EmptyDisplayName_FailsWithDisplayNameRequired` | code |
| 30 | 〃 | `Validate_DisplayNameOverMax_FailsWithDisplayNameTooLong` | 101 chars with max 100 |
| 31 | `…/RegisterWithEmail/RegisterWithEmailHandlerTests` | `Handle_NewEmail_CreatesStudentWithPasswordAndReturnsTokens` | `CreateAsync(user, "Password1")` Received(1); result.User.Email |
| 32 | 〃 | `Handle_EmailAlreadyRegistered_ThrowsConflict` | Conflict `EMAIL_ALREADY_REGISTERED`; CreateAsync DidNotReceive |
| 33 | 〃 | `Handle_IdentityRejectsUser_ThrowsUserCreationFailed` | BadRequest `USER_CREATION_FAILED` |
| 34 | `…/RegisterWithEmail/RegisterWithEmailValidatorTests` | `Validate_ValidCommand_Passes` | — |
| 35 | 〃 | `Validate_EmptyDisplayName_FailsWithDisplayNameRequired` | — |
| 36 | 〃 | `Validate_DisplayNameOverMax_FailsWithDisplayNameTooLong` | — |
| 37 | 〃 | `Validate_EmptyEmail_FailsWithEmailRequired` | — |
| 38 | 〃 | `Validate_MalformedEmail_FailsWithEmailInvalid` | — |
| 39 | 〃 | `Validate_EmailOverMax_FailsWithEmailTooLong` | — |
| 40 | 〃 | `Validate_EmptyPassword_FailsWithPasswordRequired` | — |
| 41 | 〃 | `Validate_ShortPassword_FailsWithPasswordTooShort` | RequiredLength 8, "Pass1" |
| 42 | 〃 | `Validate_PasswordWithoutDigit_FailsWithPasswordMustContainDigit` | "Password" |
| 43 | `…/LoginWithPhone/LoginWithPhoneHandlerTests` | `Handle_RegisteredActiveUser_MarksOtpUsedAndReturnsTokens` | otp.IsUsed; `SaveChangesAsync` Received(1); result.User.Id |
| 44 | 〃 | `Handle_UnknownVerificationId_ThrowsOtpInvalid` | save DidNotReceive |
| 45 | 〃 | `Handle_UnverifiedOtp_ThrowsOtpNotVerified` | `FindByNameAsync` DidNotReceive (no enumeration), save DidNotReceive |
| 46 | 〃 | `Handle_UnregisteredPhone_ThrowsNotFound` | NotFound `PHONE_NUMBER_NOT_REGISTERED`; save DidNotReceive |
| 47 | 〃 | `Handle_SuspendedUser_ThrowsForbidden` | Forbidden `USER_SUSPENDED`; save DidNotReceive |
| 48 | `…/LoginWithPhone/LoginWithPhoneValidatorTests` | `Validate_ValidCommand_Passes` / `Validate_EmptyVerificationId_FailsWithVerificationIdInvalidFormat` | 2 tests |
| 49 | `…/LoginWithEmail/LoginWithEmailHandlerTests` | `Handle_ValidCredentials_ResetsFailedCountAndReturnsTokens` | `ResetAccessFailedCountAsync` Received(1); result.User.Role |
| 50 | 〃 | `Handle_UnknownEmail_ThrowsInvalidLogin` | BadRequest `USER_INVALID_LOGIN`; `CheckPasswordAsync` DidNotReceive |
| 51 | 〃 | `Handle_LockedOut_ThrowsUserLockedOut` | RateLimit `USER_LOCKED_OUT`; `CheckPasswordAsync` DidNotReceive |
| 52 | 〃 | `Handle_WrongPassword_RecordsFailureAndThrowsInvalidLogin` | `AccessFailedAsync` Received(1); `ResetAccessFailedCountAsync` DidNotReceive |
| 53 | 〃 | `Handle_SuspendedUser_ThrowsForbidden` | Forbidden `USER_SUSPENDED`; `ResetAccessFailedCountAsync` DidNotReceive |
| 54 | `…/LoginWithEmail/LoginWithEmailValidatorTests` | `Validate_ValidCommand_Passes`, `Validate_EmptyEmail_FailsWithEmailRequired`, `Validate_MalformedEmail_FailsWithEmailInvalid`, `Validate_EmptyPassword_FailsWithPasswordRequired` | 4 tests |
| 55 | `…/RefreshAccessToken/RefreshAccessTokenHandlerTests` | `Handle_ValidToken_IssuesAccessTokenAndRotatesRefreshToken` | refresh `GenerateTokenAsync(user)` Received(1); result.RefreshToken is the new value (not the request's) |
| 56 | 〃 | `Handle_SuspendedUser_ThrowsForbidden` | Forbidden `USER_SUSPENDED`; `GenerateTokenAsync` DidNotReceive |
| 57 | `…/RefreshAccessToken/RefreshAccessTokenValidatorTests` | `Validate_Token_Passes` / `Validate_EmptyToken_FailsWithRefreshTokenRequired` | 2 tests |
| 58 | `…/Logout/LogoutHandlerTests` | `Handle_AuthenticatedUser_RotatesSecurityStamp` | `UpdateSecurityStampAsync(user)` Received(1) |
| 59 | 〃 | `Handle_NoCurrentUser_ThrowsUnauthorized` | `USER_NOT_AUTHENTICATED`; stamp DidNotReceive |
| 60 | 〃 | `Handle_UserMissing_ThrowsUnauthorizedUserNotFound` | `USER_NOT_FOUND`; stamp DidNotReceive |
| 61 | `…/SeedAdmin/SeedAdminHandlerTests` | `Handle_EmailNotConfigured_DoesNothing` | `FindByEmailAsync` and `CreateAsync` DidNotReceive |
| 62 | 〃 | `Handle_AdminAlreadyExists_DoesNotCreate` | CreateAsync DidNotReceive |
| 63 | 〃 | `Handle_NewAdmin_CreatesAdminWithConfiguredPassword` | `CreateAsync(Arg.Is<User>(Role Admin, Email), password)` Received(1) |
| 64 | 〃 | `Handle_IdentityRejectsAdmin_ThrowsAdminSeedFailed` | `InternalServerErrorCoreException`, `ADMIN_SEED_FAILED` |
| 65 | `…/Shared/UserClaimsExtensionsTests` | `GetUserClaims_PhoneStudent_IncludesIdRoleAndPhone` | NameIdentifier == Id, Role "Student", MobilePhone present, no Email claim |
| 66 | 〃 | `GetUserClaims_EmailAdmin_IncludesEmailAndOmitsPhone` | Role "Admin", Email present, no MobilePhone claim |

### api — integration (`Tests/Integration/Auth/`, real PostgreSQL through `ApiFactory`, `AuthTestClient.Create`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 67 | `OtpEndpointTests` | `SendOtp_ValidPhone_Returns200AndDeliversCode` | 200; body has `verificationId` and **no** `code` property; `factory.Sms.LatestCodeFor(phone)` has 6 digits |
| 68 | 〃 | `SendOtp_UnknownPrefix_Returns422` | 422; `code` contains `VALIDATION_PHONE_NUMBER_INVALID_CELLULAR_CODE` |
| 69 | 〃 | `SendOtp_WithinCooldown_Returns429` | second send for the same phone gives 429 `OTP_REISSUE_COOLDOWN` |
| 70 | 〃 | `VerifyOtp_CorrectCode_Returns200` | 200; DB `Otps` row `IsVerified` true (fresh scope) |
| 71 | 〃 | `VerifyOtp_WrongCode_Returns400` | 400 `OTP_NOT_MATCHED` |
| 72 | `PhoneAuthEndpointTests` | `RegisterWithPhone_VerifiedOtp_Returns200AndSetsRefreshCookie` | 200; `user.role` "Student"; `Set-Cookie` has `elmanhg_refresh=`, `httponly`, `secure`, `samesite=strict`, `path=/api/auth`; DB user with PhoneNumberConfirmed and DisplayName; Otp IsUsed |
| 73 | 〃 | `RegisterWithPhone_UnverifiedOtp_Returns400` | 400 `OTP_NOT_VERIFIED`; no user row |
| 74 | 〃 | `RegisterWithPhone_PhoneAlreadyRegistered_Returns409` | 409 `PHONE_NUMBER_ALREADY_REGISTERED` |
| 75 | 〃 | `LoginWithPhone_RegisteredPhone_Returns200WithStudentRoleClaim` | 200; `JwtSecurityTokenHandler().ReadJwtToken(accessToken)` has claim `role` = "Student" |
| 76 | 〃 | `LoginWithPhone_UnregisteredPhone_Returns404AndOtpStaysUsable` | 404 `PHONE_NUMBER_NOT_REGISTERED`; then `register/phone` with the same `verificationId` gives 200 |
| 77 | 〃 | `LoginWithPhone_SuspendedUser_Returns403` | seeded suspended phone user gives 403 `USER_SUSPENDED` |
| 78 | `EmailAuthEndpointTests` | `RegisterWithEmail_NewEmail_Returns200AndPersistsStudent` | 200; DB user Role Student, Email; cookie set |
| 79 | 〃 | `RegisterWithEmail_DuplicateEmail_Returns409` | 409 `EMAIL_ALREADY_REGISTERED` |
| 80 | 〃 | `RegisterWithEmail_ShortPassword_Returns422` | 422 `code` contains `PASSWORD_TOO_SHORT` |
| 81 | 〃 | `LoginWithEmail_Admin_Returns200WithAdminRoleClaim` | seeded `User.CreateAdmin`; 200; JWT `role` = "Admin"; `user.role` "Admin" |
| 82 | 〃 | `LoginWithEmail_WrongPassword_Returns400` | 400 `USER_INVALID_LOGIN` |
| 83 | 〃 | `LoginWithEmail_AfterMaxFailedAttempts_Returns429` | 5 wrong attempts, then the correct password gives 429 `USER_LOCKED_OUT` |
| 84 | `SessionEndpointTests` | `Refresh_ValidCookie_Returns200AndRotatesCookie` | 200; new `accessToken`; `Set-Cookie` value ≠ the sent value |
| 85 | 〃 | `Refresh_NoCookie_Returns422` | 422 `REFRESH_TOKEN_IS_REQUIRED` |
| 86 | 〃 | `Refresh_TamperedCookie_Returns401` | 401 `REFRESH_TOKEN_IS_EXPIRED` |
| 87 | 〃 | `Logout_Authenticated_Returns200AndRevokesRefreshToken` | 200; `Set-Cookie` deletes `elmanhg_refresh` (expires in the past); refresh with the old cookie gives 401 `REFRESH_TOKEN_USER_NOT_FOUND` |
| 88 | 〃 | `Logout_Anonymous_Returns401` | 401 |
| 89 | `AuthRateLimitTests` | `SendOtp_OverIpLimit_Returns429TooManyRequests` | `factory.WithWebHostBuilder` adds in-memory `Auth:OtpRequestPermitLimit`="1"; first send (phone A) 200; second (phone B) 429 with `code` `TOO_MANY_REQUESTS` |
| 90 | 〃 | `LoginWithEmail_OverIpLimit_Returns429TooManyRequests` | `Auth:CredentialPermitLimit`="1"; second login attempt gives 429 `TOO_MANY_REQUESTS` |

### web (Vitest + Testing Library + MSW, `renderApp`/`renderWithProviders`, `userEvent.setup()`)
| # | Test file | Test | Asserts |
|---|-----------|------|---------|
| 91 | `shared/lib/authToken.test.ts` (new) | `returns false when no handlers are registered` | `refreshOnce()` resolves false |
| 92 | 〃 | `shares one refresh between concurrent callers` | two concurrent calls both resolve true; the registered refresh ran once |
| 93 | 〃 | `calls onExpired when notified` | handler called |
| 94 | `shared/lib/http.test.ts` (**modify: add 4**) | `sends the access token as a bearer header` | MSW echoes `authorization` → `Bearer t1` |
| 95 | 〃 | `refreshes once and retries a request that returned 401` | the first call 401, refresh handler sets the token, the retry returns 200 body |
| 96 | 〃 | `calls onExpired and throws when refresh fails` | ApiError status 401; onExpired called |
| 97 | 〃 | `does not refresh when the refresh endpoint itself returns 401` | `/api/auth/refresh` 401 throws; the refresh handler is not called |
| 98 | `features/session/authSession.test.ts` (new) | `restores the session from the refresh cookie` | MSW refresh returns admin; `restoreSession` true; store role 'admin'; `getAccessToken()` set |
| 99 | 〃 | `stays signed out when refresh fails` | MSW 422; false; store null; token null |
| 100 | 〃 | `maps every API role to a session role` | `toSession` Student/Teacher/Admin → student/teacher/admin |
| 101 | `features/session/schemas/phoneStartSchema.test.ts` | valid phone; wrong prefix → `session:validation.phone`; 10 digits → same; empty display name → `validation.required`; 101-char display name → `session:validation.displayNameLength` | one `it` each |
| 102 | `features/session/schemas/otpSchema.test.ts` | six digits valid; five digits fails; letters fail | `session:validation.code` |
| 103 | `features/session/schemas/emailSignInSchema.test.ts` | valid; bad email → `session:validation.email`; empty password → `validation.required` | — |
| 104 | `features/session/schemas/emailSignUpSchema.test.ts` | valid; short password → `passwordLength`; no digit → `passwordDigit`; bad email; empty display name | — |
| 105 | `features/session/pages/LoginPage.test.tsx` | `signs an admin in with email and lands on the admin home` | MSW `login/email` → admin; heading "Dashboard" |
| 106 | 〃 | `returns to the originally requested page after signing in` | `renderApp('/admin/users')` → sign in → heading "Users" |
| 107 | 〃 | `shows the invalid-login message on wrong credentials` | 400 `USER_INVALID_LOGIN` shows the alert text "Email or password is incorrect." |
| 108 | 〃 | `signs a student in with a mobile code` | send → code step shows "We sent a 6-digit code to 01012345678." → verify + `login/phone` → heading "Home" |
| 109 | 〃 | `offers sign-up when the mobile number has no account` | `login/phone` 404 → alert "No account uses this mobile number…"; link "Create an account" is present |
| 110 | 〃 | `shows the wrong-code error on the code field` | verify 400 `OTP_NOT_MATCHED` → the code field `aria-invalid` + "The code is incorrect." |
| 111 | 〃 | `sends a new code and uses its verification id` | click "Resend code" → toast "A new code is on its way."; the MSW verify handler returns 200 only for the new id (400 `OTP_INVALID` otherwise) → heading "Home" |
| 112 | 〃 | `does not verify twice when sign-in fails after verification` | the MSW verify handler returns 200 the first time and 400 `OTP_ALREADY_VERIFIED` after that; `login/phone` returns 500 then 200; submit, see the generic alert, submit again → heading "Home" |
| 113 | 〃 | `sends a signed-in visitor away from sign in to their home` | `session: testSessions.student` → "Home" |
| 114 | 〃 | `renders right-to-left in Arabic` | `lng: 'ar'` → heading "تسجيل الدخول", `document.documentElement.dir` 'rtl' |
| 115 | 〃 | `has no axe violations` | axe violations [] |
| 116 | `features/session/pages/SignUpPage.test.tsx` | `creates a student account with email and lands on the student home` | MSW `register/email` → "Home" |
| 117 | 〃 | `shows the duplicate-email error on the email field` | 409 `EMAIL_ALREADY_REGISTERED` → the email field is invalid with text |
| 118 | 〃 | `shows required errors and focuses the first field when submitted empty` | name field focused; required text |
| 119 | 〃 | `creates a student account with a mobile code` | name + phone → code → verify + `register/phone` (body has `displayName`) → "Home" |
| 120 | 〃 | `offers sign-in when the mobile number already has an account` | `register/phone` 409 → alert text |
| 121 | 〃 | `has no axe violations` | — |
| 122 | `features/shell/components/AppShell.test.tsx` (**modify**) | `signs out to the sign-in page` | add `server.use(http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 200 })))`; import `testSessions` from `@/test/sessions` in place of `devSessions` (every test in the file) |
| 123 | 〃 (new test) | `signs out locally even when the server logout fails` | logout 500 → heading "Sign in" |
| 124 | `app/router.test.tsx`, `features/shell/pages/MorePage.test.tsx`, `features/session/sessionStore.test.ts` (**modify: import only**) | — | `devSessions` → `testSessions` from `@/test/sessions`; no assertion changes |
| 125 | `app/env.test.ts` (**modify: delete 3**) | delete `accepts a known dev session role`, `treats an empty dev session role as unset`, `rejects an unknown dev session role` | the feature is removed (Decision 29) |
| 126 | `features/session/pages/DevSignInPage.test.tsx` (**delete**) | — | page removed; its behaviours are re-covered by #105, #106, #113, #115 |

## Definition of done
- [ ] Every sub-task is covered: User aggregate fields; register/login commands behind `ISmsSender`; JWT with role claim plus rotating refresh cookie; OTP, lockout and per-IP rate limits; sign-up, OTP, sign-in and sign-out screens.
- [ ] Exactly the files listed are created, modified or deleted. Every reused piece names its Morabh source in this plan.
- [ ] `GenerateOTPResult` has no `Code`. `grep -rn "new BaseException(" api/core-libraries/Core.OTP` returns nothing.
- [ ] `ValidatePhoneNumber` length rule uses `WithErrorCode`.
- [ ] `IRefreshTokenService.ValidateTokenAsync` exists; the refresh endpoint is anonymous and rotates the cookie.
- [ ] `AuthResult` JSON has no `refreshToken`. The cookie is `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`.
- [ ] `Program.cs`: identity options bound from `IdentityOptions`; `AuthenticatedUser` policy; `AddCoreOtp`; `AddAuthRateLimiting`; admin seed after `Build()`; `UseRateLimiter` directly after `CoreExceptionMiddleware`. No other middleware reordered.
- [ ] `FakeSmsSender` is selected through `Sms:Provider`. `AuthOptions`, `SmsOptions` use `ValidateOnStart`.
- [ ] `appsettings.example.json` and `.env.example` hold only shapes and non-secret defaults. `CoreJwt:ExpirationHours` is 1.
- [ ] Migration `AddUserProfileFields` only adds 3 columns. The snapshot is updated.
- [ ] Every new error code has an entry in both `Messages.en.resx` and `Messages.ar.resx`, and web `errors.*` covers the codes listed.
- [ ] `api/openapi/v1.json` is regenerated with the 8 operationIds. `npm run gen:api` gives no diff after commit.
- [ ] Postman `Auth` folder has 8 requests with token and verificationId capture scripts.
- [ ] README has the OTP-log and admin-seed notes. No `/docs` divergence.
- [ ] Every row of the Test plan exists with that name. Existing tests change only as rows 122–126 state.
- [ ] `dotnet build api/ -c Release` has zero new warnings. `dotnet test api/` is green (Docker). `dotnet format --verify-no-changes` exits 0.
- [ ] The web checks exit 0: `npx tsc -b --noEmit`, `npm run lint`, `npx prettier --check .`, `npx vitest run --coverage` (features ≥ 80% lines / 70% branches), `npx vite build`. `routeTree.gen.ts` has no drift.
- [ ] No `localStorage`, `sessionStorage` or `document.cookie` in `web/src`. The access token lives only in `authToken.ts`.
- [ ] Guard grep is empty: `git diff origin/main -U0 -- '*.cs' | grep -E '^\+.*(DateTime\.(Now|UtcNow)|\.Result\b|\.Wait\(\)|new HttpClient\(|FromSqlRaw|async void)'`.
- [ ] No OTP, token or password is logged, except `FakeSmsSender` outside Production (Decision 4).
