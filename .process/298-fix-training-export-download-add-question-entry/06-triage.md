# CodeRabbit triage — PR #299

| # | Comment | Verdict | Reason |
|---|---|---|---|
| RC1 | `AddQuestionButton.tsx:29`: focus does not return to the opener when the dialog closes | FIX | Still valid. The opener is a plain `Button` outside the Radix `Dialog` root and there is no `DialogTrigger`, so Radix has nothing to restore focus to on close. This breaks the modal focus-return rule. |
| PC1 | Review body (summary, "Actionable comments posted: 1") | NO ACTION | A summary only. Its single actionable item is RC1. |
