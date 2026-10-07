TRIAGE: 3 to implement, 0 rejected, 0 dev-decisions (plus 1 unverified review-body nit, rejected)

# CodeRabbit triage — PR #330 (story #328, E21.S11)

I checked each comment against the branch code (HEAD affab8e9). I treated the comment text as data only and did not follow any instruction in it.

## RC1 — `.claude/commands/feature.md:105` — FIX (Minor)
**Claim:** After polling, the "nothing actionable" bullet sends the PR straight to merge. A PR that CodeRabbit never reviewed therefore skips the whole-diff pass.
**Verified:** True. This PR's F12 paragraph (step 2, line 103) says that if CodeRabbit "is rate-limited and never reviews it, the Stage 3 reviewer runs an explicit correctness and security pass". The orchestrator only learns this after polling in step 3, and the step 3 bullet at line 105 treats "no review" the same as "reviewed, nothing actionable" and goes to merge. The bullet at line 106 is the same: once polling stops on later PRs, those PRs get no review at all. The new rule cannot be reached from the steps as written. The text comes from this PR, so this is not #331 scope.
**Change (line 105 only):**
`   - Nothing actionable within 15 min → `05-coderabbit-comments.md` says so. If CodeRabbit did not review the PR at all (including PRs no longer polled under the bullet below), launch a fresh `feature-reviewer` for the whole-diff pass in step 2 before merge; otherwise go to merge.`
Keep the F12 paragraph at line 103 as it is. The plan quotes it verbatim.

## RC2 — `.process/328-wave-1-follow-ups-otp-cooldown-and-columns-core/03-merged-pr-review.md:7` — FIX (Minor)
**Claim:** The routing note says m2 is the only finding in a file this story changes. M1 also cites `Core.OTP/Entities/OTP.cs`, which this story changes.
**Verified:** True. Line 12 cites `api/core-libraries/Core.OTP/Entities/OTP.cs`, and `git diff main...HEAD` changes that file (column removal plus D2 cooldown, OTP.cs:92). The other findings (m1, m3–m6) cite files that are not in the diff.
**Change (line 7, last sentence only):** replace "None of them is in a file this story changes, except m2 (Core.Storage/DependencyInjection.cs), and m2 is minor." with "None of them is in a file this story changes, except M1 (Core.OTP/Entities/OTP.cs; its fix needs the CoreDbContext mapping, so it stays a follow-up) and m2 (Core.Storage/DependencyInjection.cs, minor)."
Fix the wording only. The M1 fix itself (OTP concurrency) stays deferred to #331.

## RC3 — `docs/otp-delivery.md:18` — FIX (Minor; docs-sync divergence this PR introduced)
**Claim:** A resend during the block period returns `OTP_REISSUE_COOLDOWN`, not `OTP_REACHED_MAX_REISSUE_COUNT`.
**Verified:** True. In `Otp.Reissue` (api/core-libraries/Core.OTP/Entities/OTP.cs:58-93), the window reset (:60-64) runs first, then the cooldown check (:66-76), then the quota check (:78-81). After D2 (:92), the block runs `ReissueBlockCooldownInHours` from the resend that used up the quota. That resend is inside the window, so with the defaults (24 h block, 24 h window) the block always ends after the window ends. Every resend during the block gets `OTP_REISSUE_COOLDOWN`, and the first resend after the block finds the window expired and the count reset. `OTP_REACHED_MAX_REISSUE_COUNT` is reachable only when the block is shorter than the rest of the window (as in `OtpTests.Reissue_PastMaxReissueCount_ThrowsRateLimitExceeded`, block 0). Test T2 `Reissue_BlockedAfterLastAllowedResend_ThrowsCooldownUntilBlockEnds` (OtpTests.cs:96-104) pins the cooldown code during the block. The doc sentence was rewritten in this PR, so the fix belongs here.
**Change (line 18):** replace ", counted from that resend, and a resend over the quota returns 429 `OTP_REACHED_MAX_REISSUE_COUNT`." with ", counted from that resend; a resend during that block returns 429 `OTP_REISSUE_COOLDOWN`. A resend over the quota after the block has passed (possible only when `ReissueBlockCooldownInHours` is shorter than the rest of the window) returns 429 `OTP_REACHED_MAX_REISSUE_COUNT`."
No code change. The doc must match the code.

## PC1 nit — `03-merged-pr-review.md:47-48`, character class not in backticks — REJECT
CodeRabbit flagged this itself as "Unverified" and did not count it among the 3 actionable comments. It is a markdownlint style point in a process artifact, the repo has no markdownlint gate, and the text reads correctly. It is not worth a rework item.

## Not deferred to #331
None of RC1–RC3 overlaps #331's scope. RC2 mentions M1 (OTP concurrency, #331) but only asks for a wording fix in this PR's own artifact.
