# Review r2 (orchestrator), #101

Blocking #1 (docs/paymob.md §8, malformed JSON) is fixed. The doc now says malformed JSON gets the MVC default 400 with no code, and `PAYMOB_WEBHOOK_PAYLOAD_INVALID` covers the structural problems. This was a doc-only change.

VERDICT: APPROVED
