# [E10.S3] Webhook-driven entitlement

Issue: #101

As the platform I change entitlement only on HMAC-verified Paymob webhooks. PRD §17 rule 12.

Epic: #98

### Sub-tasks
- [ ] Webhook endpoint with HMAC verification and raw payload storage
- [ ] Idempotent processing keyed by Paymob transaction id
- [ ] State transitions on success, failure and cancellation
- [ ] Renewal handling with 3 day grace period then downgrade job
- [ ] Integration tests with recorded webhook payloads
