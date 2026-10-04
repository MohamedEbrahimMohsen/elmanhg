# [E19.S4] Student Progress and Multi-unit exam UI polish

Issue: #286

Epic: #275

As a student I want the Progress and Multi-unit exam pages to look tidy, readable and consistent with the Indigo calm design.

The dev reviewed the demo on 2026-10-04 and reported UI problems on `/student/progress` and `/student/multi-exam`. Screenshots were shared in chat. The orchestrator observed the following.

**Multi-unit exam (`/student/multi-exam`)**
- The `<fieldset>` legends («اختر وحدتين أو أكثر:», «عدد الأسئلة») sit on the card's top border. This is a shared fieldset/legend styling bug; fix it at the shared component level and check every other fieldset in the app.
- Each unit option is a checkbox, with its "(150 سؤال متاح)" line underneath and loosely spaced. The card leaves half its width empty. Make the unit options selectable option cards in a responsive grid, with the count on the same card, and a clear selected state.
- The question-count radios should match the same option-card style, or a segmented control.
- No start button is visible. Only the helper text «اختر وحدتين على الأقل.» shows, outside the cards. Always show the primary "start" button, disabled with the reason until valid, next to the selection summary.
- The subject select spans the full width. Give it a sensible max width.

**Progress (`/student/progress`), and the same headline on `/student`**
- The headline card «متبقّي لك 450 سؤال من 450»: the huge number line is cramped against the stats subtitle «شاهدت 34 سؤالًا · أتقنت 0 · سلسلة الأيام: 1 يوم». Fix the spacing and line-height. Consider showing the three stats as small labelled stat chips, and a progress bar.
- The subject card takes only half the width and leaves empty space. Use the full width or a proper grid.
- Unit names in the units table look like buttons (bordered pills). Make them plain text or links, consistent with the other tables. Keep row actions in the shared `sm` style.
- "Weakest lessons" and "weakest objectives" are long stacks of tall cards, each with a full-width bar and a button. Make them compact list rows: name, subject/lesson meta, mastery %, a small bar and «درّب الآن» on one row. Use a sensible cap with "show more".
- Check percent and number formatting with Latin digits (e.g. «إتقان 0%»).

**Rules**
- Keep the Indigo calm tokens and the shared components (`Select`, `Button`, table actions).
- No API change.
- Keep the student budgets green. Quiz has about 19.7 KB of headroom; don't spend it needlessly.
- Use DOM-measurement layout checks at 375, 768 and 1280 px. Screenshots often time out.

### Sub-tasks
- [ ] Shared fieldset/legend fix, applied app-wide
- [ ] Multi-unit exam: option-card grid, count control, always-visible start button with its reason
- [ ] Progress: headline spacing and stats, full-width subject section, plain unit names, compact weak-lessons and weak-objectives lists
- [ ] Tests; design-prompt and design-system docs updated where they describe these screens

