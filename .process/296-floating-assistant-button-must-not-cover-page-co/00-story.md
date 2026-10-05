# [E19.S6] Floating assistant button must not cover page content on mobile

Issue: #296

Epic: #275

As a student on a phone, I can reach every button on a page. The floating «المساعد» button must not cover page content when I scroll to the end.

The dev saw this in the demo after #289 (2026-10-05). At 375-ish px on `/student`, the floating assistant pill (`AvatarDock`) sits on top of the mint «درّب الآن» button in the next-lesson card. The page content ends behind the fixed pill and the mobile tab bar.

**Rules**
- Reserve bottom space in the student page container on pages where the dock shows. That space is the dock height plus its offset plus a gap, on top of the tab-bar space already reserved, so the last element can always scroll clear of the pill. Use safe-area insets.
- No change to the dock's look or position, or to the panel. The full-page assistant and exam pages, where the dock is hidden, get no extra space.
- Check at 375, 768 and 1280 px with DOM measurement: the bottom of the last focusable element on each student page can scroll above the top of the dock rect.
- Add a test. Keep the budgets green.

### Sub-tasks
- [ ] Bottom spacing for the student shell where the dock is visible
- [ ] Test, plus a layout check at the 3 widths

