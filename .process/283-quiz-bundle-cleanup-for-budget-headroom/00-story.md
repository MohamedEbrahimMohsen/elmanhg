# [E19.S3] Quiz bundle cleanup for budget headroom

Issue: #283

Epic: #275

As the team, we want the quiz page (and the other student entry pages) to have real headroom under their bundle budgets, so student-facing changes stop failing CI on a few bytes.

Dev request (2026-10-03): "do the quiz cleanup so we have budget headroom". Today quiz is at 261093 of 261120 B brotli (27 B spare). Lesson has about 297 B spare. The global CSS and the entry chunk count toward every page.

**Rules**
- **Target:** at least 8 KB brotli of headroom on quiz, without raising any budget. Lesson, entry and landing must also gain headroom or stay where they are. If 8 KB isn't reachable without behaviour changes, report the best achievable figure with evidence. Stop at a clean, justified point rather than chasing bytes with hacks.
- **Cleanup only:**
  - find what is in the quiz path that doesn't need to be there (per-chunk breakdown from the `perf:budget` manifest walk);
  - move code that isn't needed on first render behind lazy imports, for example heavy editors, KaTeX parts, dialogs, the avatar panel, admin-only code, unused locales and big i18n bundles;
  - remove dead code and duplicate helpers;
  - drop unused or duplicated CSS utilities;
  - check that the vendor split is effective.
  - No behaviour or visual change, except that the disabled-button hover fix deferred from #280 (#282) now goes in, since this story makes room for it.
- **Guardrail:**
  - add a CI check, or a perf script option, that prints the per-chunk breakdown for quiz when a budget fails, so the next failure is easy to diagnose;
  - document how to use it in `docs/performance.md`.
- **Tests:** existing tests stay green. Lazy-loaded pieces get loading and fallback handling, with tests.

### Sub-tasks
- [ ] Per-chunk analysis of the quiz/lesson/entry/landing paths (before and after table in the report)
- [ ] Cleanup: lazy boundaries, dead code, deduplicated helpers, CSS trimming
- [ ] The deferred disabled-button hover fix (#282)
- [ ] Budget diagnostics output, plus `docs/performance.md`
- [ ] All web checks green; no budget raised

