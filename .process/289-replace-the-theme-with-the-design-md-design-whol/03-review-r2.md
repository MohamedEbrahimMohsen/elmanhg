VERDICT: APPROVED

# Review r2 — Replace the theme with the DESIGN.md design, whole app (#289, E19.S5)

## Blocking
None.

## Round-1 findings
- #1 (two mints per screen): resolved. Apply is `SubmitButton variant="secondary"` in all six filter forms (`UserFiltersForm.tsx:55`, `AuditLogFilters.tsx:43`, `PaymentLogFilters.tsx:60`, `QuestionListFilters.tsx:69`, `AvatarConversationFilters.tsx:46`, `ValidationQueueFilters.tsx:81`). `UserListEmptyState.tsx:18` Clear filters is secondary, so Invite (`UsersPage.tsx:80-81`) is the single mint on Teachers/Admins. The three extra screens: `NameForm.tsx:19,29,48` defaults the submit to secondary and only `ContentPage.tsx:59` passes `primary` (add subject), so the expanded tree has one mint; `QuestionImportForm.tsx:78` Check is secondary, leaving the report's Import (`QuestionImportReport.tsx:52`) as the mint; `PlanSummaryLine.tsx:25` is secondary, leaving `NextLessonCard.tsx:23` as Home's mint. My own sweep of every `variant="primary"` / default-variant `Button`/`SubmitButton` under `web/src/features` found no other co-rendered pair (ExamStartActions and ExamResultPage primaries are mutually exclusive branches; BulkApproveBar's second primary is inside its dialog; validation queue's no-results Clear is never shown with BulkApproveBar).
- #2 (hero on `/student/progress`): resolved by option (b). Listed in `.claude/design-system.md:35,96,108,124,221`, `docs/design-system.md:76,212,264,294`, `docs/claude-design-prompt.md:53,59,109,123,151,181,232`. No doc still says "three surfaces"; line 221 correctly says three components, four routes.
- #3 (tailwind-merge regression test): resolved. `utils.test.ts:9-12` would fail if `label` left the `text` list (cn would drop `text-label`); `utils.test.ts:14-16` would fail if `shadow` were emptied (unknown `shadow-1` would survive alongside `shadow-none`).

## Non-blocking
- `web/src/test/mintButtons.ts:1-4` counts `bg-action` on `[data-slot=button]` across the whole document. It is sound (every mint goes through `Button`; `buttonVariants` has no other caller) and catches the shell too. It is stricter than the dialog-is-its-own-screen rule if a test ever asserts with a dialog open over a page mint; none do today.
- The three extra screens (content tree, question import, Free-student Home) have no `mintButtons` assertion; only read-checked, as the report admits.
- Round-1 non-blocking items (BlueprintEditor Cancel weight, generateTokensCss grep exclusion, change-log wording, tatweel initial) remain; none gate.

## Verified
- Dialog rule present and consistent: `.claude/design-system.md:122` (rule 1) and `:116` (EmptyState), `docs/design-system.md:291`, `docs/claude-design-prompt.md:57`.
- From `web/`: typecheck exit 0; lint exit 0; vitest 306 files / 1817 tests passed, no timeouts; build ok (298 files precompressed); perf:budget entry 189/210, landing 199/220, lesson 220/240, quiz 236/255, admin-dashboard 213/233, admin-users 245/270, teacher-home 245/265, assistant 244/260 — matches the report. `budgets.json` and `routeTree.gen.ts` unchanged (no budget raised).
- Both CI greps from `web-ci.yml:39-40` return no match on `web/src`.
- Only new file is `web/src/test/mintButtons.ts`, as declared.

## Test quality
- Page tests (Audit, Payments, QuestionList, AvatarConversations, ValidationQueue: length 1; Users Students: length 0; Users Teachers: `['Invite teacher']`) would each fail if Apply or Clear filters reverted to primary. They constrain the fix.
- `utils.test.ts` new cases constrain the tailwind-merge config.
