VERDICT: CHANGES_REQUESTED

# Review — Terms and privacy notice with training-use line at sign-up (E18.S9, round 1)

## Blocking

### 1. `docs/implementation-report.md` still says training notice/consent "stays open"
**Where:** `docs/implementation-report.md:179` (section "Awaiting a dev decision", row `#215 (also #229, #235)`) vs `docs/training-data.md:144` and `api/Elmanhg.Application/Auth/RegisterWithEmail/RegisterWithEmailHandler.cs:22` / `RegisterWithPhoneHandler.cs:32` (`user.AcceptTerms(...)`)
**Rule:** `.claude/rules/docs-sync.md` (divergence; ownership map: `docs/implementation-report.md` owns run results and open decisions)
**Problem:** This change builds the training-use notice. It adds the sign-up terms line and `/privacy`, stores `TermsVersion`/`TermsAcceptedAt`, and ticks the checklist item in `docs/training-data.md:144` ("decided on #273 ... (#301)"). The implementation report row still ends "Notice or consent for training use stays open; the privacy checklist is in `docs/training-data.md`". The plan did not list this file, so it was missed. There is precedent for updating this row: it already records that per-chat erasure was "built in #271".
**Failure:** Ask "is the training-use notice decided and built?" `docs/training-data.md:144` and PRD §13 "Notice." say yes. `docs/implementation-report.md:179` says it is still open.
**Fix:** In that row, replace "Notice or consent for training use stays open" with "notice for training use decided on #273 and built in #301 (a terms line at sign-up, the public `/privacy` page, `TermsVersion`/`TermsAcceptedAt` stored)". Leave the rest of the row unchanged.

## Non-blocking
- `.claude/design-system.md:101` (BrandBar) and `docs/claude-design-prompt.md:111` list the logo-only app bar screens as landing, login, sign-up, accept-invite and onboarding. `/privacy` now uses `BrandBar` too (`web/src/features/legal/pages/PrivacyPage.tsx:18`). The §4 `#/privacy` bullet already says "app bar with the logo only", so this does not contradict the code. Adding `/privacy` to both lists would keep them complete.
- `web/src/features/shell/pages/MorePage.tsx:31-38`: the standalone caption link has no minimum touch height (`min-h-11`), unlike the list rows above it. The same is true of the existing `NotFound` link, so it is consistent with the repo.
- `api/Elmanhg.Tests/Integration/Auth/PhoneAuthEndpointTests.cs:677`: I3 only checks `TermsAcceptedAt` is not null, while I1 checks a before/after window. This matches the plan, and the stronger check is in I1.
- `RegisterWithPhoneValidatorTests` has no cascade-stop test, while the email validator has V2. The plan also omits it, and the rule chain is identical.

## Verified
- **Intent:**
  - Both student self-sign-up commands carry `TermsVersion`.
  - Both validators return `TERMS_VERSION_REQUIRED`, then (cascade stop) `TERMS_VERSION_UNKNOWN`.
  - Both handlers call `AcceptTerms(request.TermsVersion, timeProvider.GetUtcNow())` before `CreateAsync`.
  - `AcceptInvitation` is untouched (plan Decision 1).
- **422 path:**
  - The validators run in the pipeline before the handler.
  - I2 confirms that an unknown email version gives 422 and creates no user.
  - I4 confirms that the phone OTP is not consumed by a 422 and a retry gives 200.
  - The domain guard (`User.Terms.cs:13-16`) is defence in depth with the same code, and D2 tests it.
- **Migration:**
  - `20261005111208_AddUserTermsAcceptance.cs` only adds nullable `TermsAcceptedAt timestamptz` and `TermsVersion varchar(20)` to `AspNetUsers`.
  - Down drops exactly those two columns. There is no SQL, no backfill and no NOT NULL column.
  - The snapshot diff contains only the two properties.
  - `AppDbContext` has `TermsVersionMaxLength = 20` with a WHY comment, and `AppDbContextTests` ends with `_AddUserTermsAcceptance`.
- **Contract fidelity:**
  - Every file in *Files to create* (A1–A4, W1–W11) exists with the planned signature and markup. Nothing extra was created.
  - The copy is verbatim from the plan: session, legal ar/en, the landing footer, shell `more.privacy`, common errors and resx.
  - `termsVersion` (`web/src/shared/lib/terms.ts:2`) equals `TermsVersions.Current` (`TermsVersions.cs:6`). Both are `"2026-10-05"`.
  - `Deviations: None.` is confirmed against the diff.
- **Skill compliance (.NET):**
  - File-scoped namespaces, sealed records and handlers, one-line primary constructors, `DateTimeOffset`.
  - `ConfigureAwait(false)` is used on every new await outside tests.
  - The only comments are the two WHY invariants.
  - Each validator rule chain has one operator per line.
  - §8.1: `TermsVersions` is a release-bound invariant with a WHY comment (Decision 2), so it is not tunable config.
  - There is no other production caller of the register commands (checked across the repo).
- **Web:**
  - Every visual value is a design token. There are no hex values, arbitrary values or physical-direction classes.
  - The sign-up page keeps one mint primary. `TermsNotice` is a caption plus a text link, with no checkbox and no button. `/privacy` has no primary button.
  - Digits are Latin (`18`, `2026-10-05`).
  - There is no `#<number>` in any new code comment.
- **Bundle:**
  - The legal text is only in `web/dist/assets/privacy-*.js`, the lazy route chunk via `autoCodeSplitting`.
  - `registerLegalLocales()` runs at the module top of `PrivacyPage.tsx`, and `app/i18n.ts` is not edited.
  - `budgets.json` is unchanged.
- **Postman:** both register bodies include `"termsVersion": "2026-10-05"`, with the URLs, methods and `noauth` unchanged. No endpoint was added or removed.
- **OpenAPI and generated code:** `api/openapi/v1.json` and the Orval model/zod files mark `termsVersion` as required. `routeTree.gen.ts` adds only `/privacy`.
- **Docs:** these edits are present and agree with the code:
  - PRD §7.1, §13 "Notice.", the §14 Privacy row and the §15 User line
  - the `training-data.md` checklist
  - `claude-design-prompt.md` §4 (signup, landing footer, privacy), §5 rule 15 and §6 Legal page
  - `prototype.md` item 1
  - the `backlog.json` E18 story
- **Tests run independently:**
  - The 4 touched web test files: 34/34 pass.
  - API unit tests `UserTermsTests` and `RegisterWithEmail*`/`RegisterWithPhone*`: 37/37 pass.
  - API integration tests `EmailAuthEndpointTests`, `PhoneAuthEndpointTests`, `EmailCodeAuthEndpointTests` and `AppDbContextTests` (Testcontainers): 27/27 pass.
- **Test plan:** every row is present with the exact planned name: D1–D4, V1–V5, H1–H2, I1–I4, M1, S1–S6, P1–P6, L1, R1–R2.

## Test quality
- **`UserTermsTests`:**
  - D1 constrains all three assignments, including `UpdationDate`.
  - D2 constrains the guard and the "no mutation before the guard" ordering.
  - D4 pins ordinal, exact-match comparison, including a trailing space and null.
  - D3 is weak (it only shows the default is null) but harmless.
- **Handler tests (email and phone):** H1 and H2 use `Arg.Is` on the user passed to `CreateAsync` with `TermsAcceptedAt == Now`. They would fail if the handler skipped `AcceptTerms`, passed a different version or used another clock. They are not vacuous.
- **Validator tests:**
  - V1, V3, V4 and V5 each fail if their rule is removed.
  - V2 fails if `Cascade(Stop)` is removed.
  - The existing `Validate_ValidCommand_Passes` covers the accepting branch.
- **Integration tests:**
  - I1 and I3 read the persisted row from a fresh scope, so they prove the EF mapping and migration.
  - I2 and I4 prove the HTTP status, the code and that there are no side effects.
- **Web:**
  - S4 and S5 capture the real request body and compare it to the shared constant.
  - S6 drives the error-code-to-common-string mapping.
  - S2 proves the line stays on the OTP step.
  - P1 pins the section order and the version.
  - P5 pins RTL and Latin digits.
  - L1 and R2 navigate for real.
  - All of these constrain behaviour.
