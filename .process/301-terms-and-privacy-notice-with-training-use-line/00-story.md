# [E18.S9] Terms and privacy notice with training-use line at sign-up

Issue: #301

As a student (or parent) I am told, in the terms I accept at sign-up, that my chats with the platform may be used, without personal data, to improve the AI assistant.

**Dev decision on #273 (2026-10-05):** a terms line, no pop-up and no opt-in checkbox. The lightest notice that still informs students and parents; legal review before go-live is the dev's.

**Scope**
- **Privacy & terms page** at a public route (e.g. `/privacy`), ar (Egyptian-friendly, plain) and en, RTL. Short sections: what we collect, how it is used, **training use** (chats with the assistant, Ask a Teacher threads, answers and grades may be used without names, phone numbers or emails to improve the AI assistant and grading; see `docs/training-data.md` for what is stripped), deleting your chats (#271), minors (if you are under 18, a parent or guardian must agree), contact. Text lives in i18n; a `termsVersion` constant (e.g. `2026-10-05`) shown on the page.
- **Sign-up line** under the sign-up submit button: «بالتسجيل، أنت موافق على الشروط وسياسة الخصوصية، ومنها استخدام المحادثات بدون بياناتك الشخصية لتحسين المساعد الذكي. لو سنك أقل من 18 سنة، لازم ولي أمرك يكون موافق.» with a link to the page. Not a checkbox, not a mint button (the page keeps one primary).
- **Record acceptance:** the sign-up command stores `TermsVersion` and `TermsAcceptedAt` on the user (migration; existing users stay null). The web sends the version it displayed; the API rejects an unknown version (`422`). No re-acceptance flow yet.
- **Links:** landing footer and the student settings/profile area link to the page.
- **Docs:** PRD (privacy row / §13 training data), `docs/training-data.md` checklist item «Notice or consent» ticked with the decision, `docs/claude-design-prompt.md` §4–§6 and `docs/prototype.md` for the new page and the sign-up line, `docs/backlog.json` (E18).

**Out of scope:** re-prompting existing users when the version changes; parental verification; an opt-out switch.

### Sub-tasks
- [ ] API: `TermsVersion` / `TermsAcceptedAt` on sign-up, migration, validation, tests
- [ ] Web: privacy & terms page, sign-up line, footer/settings links, ar/en, tests
- [ ] Docs

Related: #273

