# Triage — CodeRabbit on PR #261

Triaged by the orchestrator, verified against the code.

- RC1 (settings.py:64, one price pair for all pipelines): REJECT for this PR and DEFER. All three pipelines default to the same model, so one pair is correct today. A per-pipeline price is only needed if a grading model is pointed at a different model. The planner already listed it as a known risk; it is tracked in the follow-up issue.
- RC2 (docs/ai-service.md:174, 227): FIX. Verified: 900 x 0.20/1M + 150 x 1.20/1M = 0.00036. Both examples are updated.
- PC1 (review body): summary only; no action needed.
