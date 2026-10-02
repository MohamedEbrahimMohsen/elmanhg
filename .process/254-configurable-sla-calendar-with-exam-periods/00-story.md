# [E18.S2] Configurable SLA calendar with exam periods

Issue: #254

Epic: #252

As an admin I configure how the Ask a Teacher SLA clock counts time. On normal days it skips weekends; during exam periods every day counts.

Dev decisions on #222:
- (a) "this should be configurable, because normal days we skip the weekend, but in the exams time, this will definitely count."
- (b) A breach gives no refund or credit. Unchanged.
- (c) A breach fires an alert only. Unchanged.

**Rules**
- **Settings** (on the E18.S1 Configuration page):
  - skip weekends, on or off (default on);
  - weekend days (default Friday and Saturday);
  - time zone for day boundaries (default Africa/Cairo);
  - exam periods, each with a start date, end date and name. Weekends count during them.
- **Deadline maths:** the SLA deadline and the reminder times count hours over this calendar. A weekend day outside an exam period does not use up SLA hours.
- **Calendar changes:** they apply to open threads on the next sweep, because deadlines are recomputed rather than fixed when the thread is created. If the plan picks another approach, it documents it.
- **Displayed deadlines:** the student copy ("teacher replies within N hours") and the teacher inbox countdown show the calendar-aware deadline.

### Sub-tasks
- [ ] SLA calendar settings and exam periods: stored through the E18.S1 settings store, with exam-period CRUD for admins
- [ ] Calendar-aware deadline and reminder times, used by the SLA sweep, the due-thread query and the inbox and student views
- [ ] Admin UI for the calendar and exam periods inside the Configuration page
- [ ] Tests across weekend, exam-period and time-zone boundaries
- [ ] PRD: update the Ask a Teacher SLA section and close #222 a–c

