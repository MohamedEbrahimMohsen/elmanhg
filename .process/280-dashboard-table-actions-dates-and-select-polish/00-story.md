# [E19.S2] Dashboard, table actions, dates and select polish

Issue: #280

Epic: #275

As an admin, teacher or student, I want readable dashboards, tables and controls, and buttons that feel clickable, with polish that matches the Indigo calm theme.

The dev reviewed the demo after #276 (2026-10-03) and reported five problems. Screenshots were shared in chat; the observations are below.

1. **The admin dashboard (`/admin`) looks bad.**
   - Every KPI card is a wall of "label: value" text lines (Students, Subscribers, Content, Ask a Teacher, Payments, Signup funnel). Card heights are uneven and there is no visual hierarchy.
   - The daily charts (attempts, revenue, review decisions) are grey bars with almost no axis. Two days of data look like a single bar pinned to one edge.
   - The "pass rate by curriculum" table is fine but plain.
2. **Table action buttons look bad and show no pointer cursor.**
   - In admin Users, «إيقاف» is a red outline and «منح اسأل معلّم» is a mismatched pill, and their sizes don't match.
   - Hovering any button shows the normal arrow, not the hand. Tailwind v4 dropped `cursor: pointer` on buttons, so this is global.
3. **Dates are hard to read.**
   - Session history shows `٢٠٢٦/١٠/٠٣، ٢:٣٧ م`: Arabic-Indic digits crammed with a comma and time.
   - Admin Users shows `2026/10/01` in Latin digits.
   - The formatting is inconsistent across pages.
4. **Every dropdown's down arrow sits too far toward the edge.** The native `<select>` arrow in RTL is misplaced or crowded. It needs a consistent chevron placed at inline-end with proper padding, in RTL and LTR.
5. **Indigo looks blue on one screen and purple on another.** This is display calibration, not a bug. No change needed unless the dev asks for a hue shift.

**Rules**
- **Dashboard:** redesign it with a dataviz approach.
  - A KPI tile row: one big number, a short label, and the period delta where we have it.
  - Secondary metrics as aligned two-column rows with the numbers at inline-end, or as small bar breakdowns. For example, questions by type and the signup funnel would be horizontal bars with percentages.
  - Charts use the theme accent and have readable axes and labels. Days with no data still show on the axis.
  - Equal-height cards in a sensible grid, with readable empty states.
  - No new heavy charting dependency. The admin chunk has its own budget, and the student and quiz budgets must not grow.
- **Buttons:** global `cursor: pointer` on enabled buttons and button-like links. `not-allowed` on disabled ones.
- **Table row actions:** one compact, consistent style, the same height in every table. A destructive action is a danger ghost or outline variant at the same size. A long action may become an overflow menu if needed.
- **Dates:** one shared date/time formatter used everywhere.
  - Readable Arabic format, for example «٣ أكتوبر ٢٠٢٦، ٢:٣٧ م» or relative «منذ ساعتين» for recent items, with the full date on hover/title.
  - Pick one digit policy (Arabic-Indic vs Latin) app-wide, document it in the design system, and apply it consistently, including in tables.
- **Select:** a shared Select style with `appearance: none`, a chevron icon at inline-end, and matching padding. It applies to every `<select>` in the app.

### Sub-tasks
- [ ] Admin dashboard redesign: KPI tiles, aligned metric rows and bar breakdowns, themed charts with axes and empty days
- [ ] Global pointer cursor; one table-action button style across admin, teacher and student tables
- [ ] Shared date/time formatter with the digit policy, used everywhere; documented in the design system
- [ ] Shared Select with a correctly placed chevron in RTL and LTR, applied to every select
- [ ] Design-system docs (`.claude/design-system.md` and `docs/design-system.md`), `docs/claude-design-prompt.md`; tests; `perf:budget` green

