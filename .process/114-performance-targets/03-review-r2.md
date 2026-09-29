# Review r2 (orchestrator), #114

All three findings are fixed:
1. The lesson budget is now advisory: a `::warning::` from handleSummary, and the k6 smoke exits 0.
2. The seed-failure test is added.
3. Tests #33 and #41 are made falsifiable; the mutations are caught.

main (#96, #117) is merged. api 3380/3380, web 1056. VERDICT: APPROVED
