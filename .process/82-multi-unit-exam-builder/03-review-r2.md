# Review r2 (orchestrator), #82

Blocking #1 (docs/sessions.md ScopeKey row) is fixed. The row now names both formats, `unit:<guid>` and `units:<size>:<sorted ids>`, and they match `MultiUnitExamScope.ToKey()`. The change is doc-only, so no code or tests changed.

VERDICT: APPROVED
