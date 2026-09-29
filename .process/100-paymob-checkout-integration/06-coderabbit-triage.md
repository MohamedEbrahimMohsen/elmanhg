# CodeRabbit triage — PR #188

- RC1 (settle without eligibility re-check): **fix in the fake flow**. Verified that `CompleteFakePaymentHandler` settles any Pending payment. For real webhooks the money has already been captured, so the eligibility policy (refund, credit, or extend) belongs to #101. Recorded on #187.
- RC2 ("Check again" never restarts polling): **fix**. Verified that `openedAt` is fixed at mount.
