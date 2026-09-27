VERDICT: CHANGES_REQUESTED

# Review: Authentication with phone OTP and email (#56, E1.S3)

## Blocking

### 1. FakeSmsSender writes OTP codes and phone numbers to the logs in every environment except Production
**Where:** `api/Elmanhg.Infrastructure/Sms/FakeSmsSender.cs:11-18`
**Rule:** skill §10, "Logging: never log tokens, passwords, OTPs ... or PII" (every §10 row is blocking). Also the caller's security criterion: no OTP may appear in logs outside Development. Plan Decision 4 allowed "outside Production". That conflicts with a skill non-negotiable, so the plan cannot waive it.
**Problem:** The guard is `hostEnvironment.IsProduction()`, so the code falls through to `LogInformation("FakeSmsSender OTP for {PhoneNumber}: {Code}", ...)` in Staging, Testing, and any other environment name.
**Failure:** A deployed host with `ASPNETCORE_ENVIRONMENT=Staging` and `Sms__Provider=Fake` writes every OTP and its phone number at Information level to every Serilog sink, including AppInsights when that sink is enabled. Anyone who can read those logs can complete `otp/verify` and then `login/phone` as any phone user.
**Fix:** Log the code only when `hostEnvironment.IsDevelopment()`. Everywhere else, log the existing warning without the code or the phone number. Update Decision 4 and the README note to say "Development". Integration tests are not affected because `ApiFactory` swaps in `RecordingSmsSender`.

### 2. The Postman capture scripts write to collection variables that the `local` environment's empty keys override
**Where:** `postman/elmanhg.postman_collection.json:54,115,146,177,208,239` (`pm.collectionVariables.set(...)`) against `postman/local.postman_environment.json:5,7`
**Rule:** Review order #7. The Postman surface must work as shipped. Plan DoD: "Postman Auth folder has 8 requests with token and verificationId capture scripts."
**Problem:** Postman resolves `{{name}}` from the narrowest scope first, and environment is narrower than collection. The environment defines `verificationId` and `accessToken` as `""`, so values the scripts store in the collection scope are never read while the `local` environment is selected.
**Failure:** Select `local`, run SendOtp, paste the code into `otpCode`, then run VerifyOtp. The body sends `"verificationId": ""` and the API returns 422 `OTP_VERIFICATION_ID_INVALID_FORMAT`. Likewise, Logout sends `Authorization: Bearer ` with an empty token after LoginWithEmail and gets 401.
**Fix:** Use `pm.environment.set(...)` in the six scripts, or remove `verificationId` and `accessToken` from the environment file. Use one scope, not both.

## Non-blocking
- `api/core-libraries/Core.Identity/Tokens/RefreshToken/RefreshTokenService.cs:29-51` and `api/Elmanhg.Application/Auth/RefreshAccessToken/RefreshAccessTokenHandler.cs:22`: "rotation" only issues a new cookie. The old refresh ticket stays valid until it expires (7 days) or the security stamp changes at logout, and there is no reuse detection, so a stolen cookie can be replayed after the owner refreshes. Decisions 11 and 12 chose stateless tickets, so this does not block. Open a follow-up issue for one-time refresh tokens (a stored ticket id, revoked on use).
- `api/Elmanhg.Api/RateLimiting/AuthRateLimiting.cs:34`: requests are partitioned by `Connection.RemoteIpAddress`, and nothing calls `UseForwardedHeaders`. Behind a reverse proxy or Cloudflare (the request logger already reads `CF-Connecting-IP`), every client shares one bucket, which means 5 OTP requests per 10 minutes for the whole platform. Configure forwarded headers before any deployment.
- `api/core-libraries/Core.OTP/OtpHasher/HmacOtpHasher.cs:9` and `OtpOptions`: `CoreOtp:Secret` is not validated at startup. An empty secret silently produces an unkeyed HMAC. Add `ValidateOnStart` with `[Required]`.
- `api/Elmanhg.Application/Auth/LoginWithEmail/LoginWithEmailHandler.cs:22`: the lockout check runs before the password check, so after 5 failures an existing email returns 429 while an unknown email always returns 400. This adds nothing beyond what `register/email` 409 already reveals. Noted only.
- `api/Elmanhg.Application/Auth/SeedAdmin/SeedAdminHandler.cs:21`: if the configured admin email already belongs to a Student, the seed returns silently and no admin exists. Consider failing with `ADMIN_SEED_FAILED` when the existing user is not an Admin.
- `web/src/features/session/components/PhoneSignIn.tsx:19-21` and `pages/LoginPage.tsx`: after a 404 `PHONE_NUMBER_NOT_REGISTERED`, the verified `verificationId` is not carried to `/signup`, even though Decision 5 keeps the OTP usable for exactly that hand-off. The user must request a new code and hits the 60 s cooldown (429).
- `web/src/features/session/components/OtpForm.tsx:52`: the phone number is not wrapped in `direction: ltr; unicode-bidi: isolate` as `docs/claude-design-prompt.md` (line 70) requires for phone numbers in RTL.
- `web/src/main.tsx:29`: if `restoreSession` rethrows a non-`ApiError` (for example the `Unknown role` error in `authSession.ts:16`), the app never renders and the page stays blank.
- Coverage: the empty-token and time-expired branches of `RefreshTokenService.ValidateTokenAsync` and the `OTP_EXPIRED` branch of `Otp.MarkUsed` (`OTP.cs:131-134`) have no test. The first is unreachable through the API because the validator catches it first.
- `postman/elmanhg.postman_collection.json` (RefreshAccessToken): the cookie is `Secure`, and `baseUrl` is `http://localhost:5080`, so Postman will not send the cookie back. Document `Auth__RefreshTokenCookieSecure=false` for local http, or use https.
- `api/Elmanhg.Api/Program.cs:38`: the inline literal `"GetDocument.Insider"` should be a named constant (constitution §0.3). Program.cs is now 113 lines.
- `dotnet format --verify-no-changes` exits 2. I confirmed that every finding is whitespace on untouched lines of vendored `core-libraries`, with none in Elmanhg.* files. The plan's DoD says exit 0.
- I did not independently verify `npm run gen:api` drift, because running it would write to the tree.

## Deviations (all 5 judged)
1. `AppDbContextTests` assertion: accepted. The behaviour changed on purpose (a second migration), and `SatisfyRespectively` still pins both migrations, so the test is not weakened.
2. Build-time OpenAPI guard in `Program.cs:35-43,73`: accepted. It loads only the committed, secret-free example and skips the seed. The WHY comment is valid. The literal is flagged above.
3. `web/orval.config.ts` drops `useQuery: true`: accepted. Without the change Orval would generate query hooks for POSTs. GET operations still get query hooks by default, and `signal: true` is kept.
4. Role claim asserted as `ClaimTypes.Role`: accepted. The token is built directly from the claims list, so the claim type is the full URI, and JwtBearer reads it correctly.
5. `SendWithRefreshCookie` helper in `AuthController.cs:86-91`: accepted. Behaviour is identical and the controller stays thin.

## Verified
- Build and tests, run myself: `dotnet build api/` succeeded with 0 warnings and 0 errors. `dotnet test api/` (Docker) passed 109 of 109. `npm --prefix web run build` succeeded. `npm --prefix web test -- --run` passed 22 files and 107 tests.
- Every file in *Files to create* exists. Three files were deleted as planned. The only additions beyond the plan are generated (`web/src/shared/api/generated/index.ts`) plus the declared deviations.
- Every test method in plan rows 1–90 exists with its exact name, and every web row (91–126) exists.
- `GenerateOTPResult` has no `Code`, and `v1.json` `GenerateOTPResult` has no `code`. Integration test 67 asserts that the response body has no `code`. No `new BaseException(` remains in Core.OTP.
- `AuthResult.RefreshToken` is `[JsonIgnore]`. `v1.json` `AuthResult` has only `accessToken` and `user`. The cookie is `HttpOnly; Secure; SameSite=Strict; Path=/api/auth` and expires with `RefreshTokenExpirationDays` (`RefreshTokenCookieExtensions.cs`), and integration test 72 asserts every attribute.
- OTP enumeration: `otp/send` answers the same way whether or not the number is registered. Login and register call `MarkUsed()` before looking up the user (`LoginWithPhoneHandler.cs:23-25`, `RegisterWithPhoneHandler.cs:23-25`). Unit test 45 asserts `FindByNameAsync` DidNotReceive on an unverified OTP. On 404, the OTP stays usable because the throw happens before `SaveChangesAsync` (integration test 76).
- Lockout: Identity lockout is bound from `IdentityOptions` with `AllowedForNewUsers`. Integration test 83 proves 5 failures produce 429 `USER_LOCKED_OUT`. Suspension is checked after the password (`LoginWithEmailHandler.cs:27-36`).
- Rate limits: `EnableRateLimiting` covers send (OtpRequests) and verify, register x2, and login x2 (Credentials). `UseRateLimiter` sits directly after `CoreExceptionMiddleware`, so `OnRejected` returns the camelCase `TOO_MANY_REQUESTS` 429 (tests 89 and 90). The OTP cooldown and daily cap now return 429.
- Refresh is anonymous. It validates the ticket signature and expiry, then the security stamp, and rejects suspended users. Logout calls `UpdateSecurityStampAsync`, and integration test 87 proves the old cookie then returns 401 `REFRESH_TOKEN_USER_NOT_FOUND`.
- Authorization is enforced server-side. The class has `[Authorize]`, and `logout` uses the `AuthenticatedUser` policy (anonymous gets 401, test 88). Role comes only from the `User.Role` column. No command carries a role, so there is no overposting.
- No secrets in the diff. `appsettings.example.json` holds shapes only, and `ExpirationHours` is 1. Error bodies include `Data` (message and stack) only in Development (`ErrorResponseHandler`). No request body is logged (`CoreRequestLoggingMiddleware`). Auth commands are not `IAuditableCommand`, so passwords never reach the audit log.
- Migration adds only 3 `AddColumn`s. Every new error code has an entry in both resx files, and every code the plan lists has a web `errors.*` key in both languages. The 8 operationIds are in `v1.json`.
- Guard grep is empty. The API has no `localStorage`, `sessionStorage` or `document.cookie`. The access token exists only in `authToken.ts`.
- Docs: no divergence. PRD §7.1, §15 and §16 and the backlog E1.S3 entry agree with the code.

## Test quality
- Handler tests (RegisterWithPhone, RegisterWithEmail, LoginWithPhone, LoginWithEmail, RefreshAccessToken, Logout, SeedAdmin) constrain real behaviour: they assert error codes, the persisting call as Received or DidNotReceive, and the ordering guard (test 45). The refresh "rotation" unit test is weak on its own, since it checks that the result carries the substitute's value, but integration test 84 backs it.
- Core OTP tests use real entities via `OtpBuilder` and would catch a regression in exception types.
- Integration tests go through HTTP and a fresh DbContext, and they assert cookie attributes. They constrain the implementation well.
- Web: the LoginPage and SignUpPage tests use MSW with request-dependent handlers (tests 111 and 112), so they would fail if the verification id or the single-verify logic were wrong. The http and authToken tests constrain single-flight refresh and the refresh-path exclusion.
