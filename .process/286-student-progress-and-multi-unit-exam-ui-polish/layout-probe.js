/* Layout probe (#286). Inject after .process/276-indigo-calm-theme-and-header-alignment-fixes/layout-audit.js.
   await __probe286.run() -> findings ([] = clean). Includes __layoutAudit overflow + targets checks. */
(() => {
  const TOL = 1;
  const css = (el) => getComputedStyle(el);
  const rect = (el) => el.getBoundingClientRect();
  const visible = (el) => { const r = rect(el); return r.width > 0 && r.height > 0 && !el.closest('.sr-only'); };
  const on = (suffix) => location.pathname.endsWith(suffix);
  const f = (check, detail) => ({ check, route: location.pathname + location.search, width: innerWidth, dir: document.documentElement.dir, detail });
  const cols = (el) => css(el).gridTemplateColumns.split(' ').filter(Boolean).length;
  const overlapY = (a, b) => rect(a).top < rect(b).bottom && rect(a).bottom > rect(b).top;
  const checks = {
    legends() {
      return [...document.querySelectorAll('fieldset > legend')].filter(visible).flatMap((legend) => {
        const fs = legend.parentElement, s = css(fs);
        const top = rect(fs).top + parseFloat(s.borderTopWidth) + parseFloat(s.paddingTop);
        return rect(legend).top < top - TOL ? [f('legend-on-border', `${legend.textContent.trim()}: ${rect(legend).top.toFixed(1)} < ${top.toFixed(1)}`)] : [];
      });
    },
    optionCards() {
      if (!on('/multi-exam')) return [];
      const boxes = [...document.querySelectorAll('main fieldset input[type="checkbox"]')];
      const grid = boxes[0]?.closest('label')?.parentElement;
      if (!grid) return [f('option-grid', 'no unit option cards')];
      const out = [], want = innerWidth >= 900 ? Math.min(3, boxes.length) : innerWidth >= 700 ? Math.min(2, boxes.length) : 1;
      if (cols(grid) !== want) out.push(f('option-grid', `${cols(grid)} columns, expected ${want}`));
      for (const box of document.querySelectorAll('main fieldset input[type="checkbox"], main fieldset input[type="radio"]')) {
        const card = box.closest('label');
        if (!card || rect(card).height < 48 - TOL) out.push(f('option-height', `${card?.textContent.trim()}: ${Math.round(card ? rect(card).height : 0)}px`));
        const caption = document.getElementById(box.getAttribute('aria-describedby') ?? '');
        if (box.type === 'checkbox' && !(caption && card?.contains(caption) && visible(caption))) out.push(f('option-caption', `${card?.textContent.trim()}: count not on its card`));
      }
      return out;
    },
    startButton() {
      if (!on('/multi-exam')) return [];
      const start = [...document.querySelectorAll('main button')].find((b) => /ابدأ الامتحان|Start exam/.test(b.textContent));
      if (!start || !visible(start)) return [f('start-visible', 'start button missing or hidden')];
      const out = [];
      if (rect(start).height < 44 - TOL) out.push(f('start-height', `${Math.round(rect(start).height)}px`));
      if (start.disabled) {
        const reason = document.getElementById(start.getAttribute('aria-describedby') ?? '');
        if (!reason || !visible(reason) || !reason.textContent.trim()) out.push(f('start-reason', 'disabled without a visible reason'));
        else if (innerWidth >= 700 && !overlapY(start, reason.parentElement)) out.push(f('start-beside-summary', 'button not beside the summary'));
      }
      return out;
    },
    subjectSelect() {
      if (!on('/multi-exam')) return [];
      const select = document.querySelector('main select');
      if (!select) return [];
      const w = rect(select).width;
      return innerWidth >= 700 && w > 320 + TOL ? [f('select-width', `${Math.round(w)}px, max 320 from 700px`)] : [];
    },
    subjectCards() {
      if (!on('/progress')) return [];
      return [...document.querySelectorAll('main article')].flatMap((card) => {
        const list = card.closest('ul');
        return list && Math.abs(rect(card).width - rect(list).width) > TOL ? [f('subject-width', `${Math.round(rect(card).width)} of ${Math.round(rect(list).width)}px`)] : [];
      });
    },
    unitLinks() {
      return [...document.querySelectorAll('main table a[href^="/student/unit/"]')].flatMap((a) =>
        parseFloat(css(a).borderTopWidth) > 0 || css(a).display !== 'inline' ? [f('unit-link-pill', `${a.textContent.trim()}: border ${css(a).borderTopWidth}, display ${css(a).display}`)] : []);
    },
    headline() {
      const card = [...document.querySelectorAll('main section[aria-label]')].find((s) => s.querySelector('dl') && s.querySelector('progress'));
      if (!on('/student') && !on('/progress')) return [];
      if (!card) return [f('headline', 'no headline card with stats and bar')];
      const out = [], p = card.firstElementChild, next = p.nextElementSibling;
      const lines = Math.round(rect(p).height / parseFloat(css(p).lineHeight));
      if (lines > (innerWidth >= 900 ? 1 : 2)) out.push(f('headline-lines', `${lines} lines`));
      if (rect(next).top - rect(p).bottom < 12 - TOL) out.push(f('headline-gap', `${(rect(next).top - rect(p).bottom).toFixed(1)}px`));
      if (card.querySelectorAll('dl dt').length !== 3) out.push(f('headline-stats', 'expected 3 stat chips'));
      return out;
    },
    weakRows() {
      if (!on('/progress')) return [];
      const out = [];
      for (const li of document.querySelectorAll('main li')) {
        const link = li.querySelector('a[href$="/practice"]');
        const title = li.querySelector('p');
        if (!link || !title) continue;
        if (innerWidth >= 700 && !overlapY(link, title.parentElement)) out.push(f('weak-row', `${title.textContent.trim()}: action not on the title row`));
        if (innerWidth < 700 && rect(li).height > 120 + TOL) out.push(f('weak-row-height', `${title.textContent.trim()}: ${Math.round(rect(li).height)}px`));
      }
      for (const ul of document.querySelectorAll('main ul[id]')) {
        const more = document.querySelector(`button[aria-controls="${ul.id}"]`);
        if (more && more.getAttribute('aria-expanded') === 'false' && ul.children.length > 3) out.push(f('weak-cap', `${ul.children.length} rows before Show all`));
      }
      return out;
    },
  };
  const audit = () => [
    ...Object.values(checks).flatMap((c) => c()),
    ...(window.__layoutAudit ? ['overflow', 'targets', 'tables'].flatMap((k) => window.__layoutAudit.checks[k]().map((x) => ({ route: location.pathname, width: innerWidth, dir: document.documentElement.dir, ...x }))) : [f('setup', '__layoutAudit not loaded')]),
  ];
  const run = async ({ routes = [location.pathname + location.search], lang } = {}) => {
    if (lang && window.__layoutAudit) await window.__layoutAudit.setLang(lang);
    const all = [];
    for (const path of routes) { if (window.__layoutAudit) await window.__layoutAudit.go(path); all.push(...audit()); }
    return all;
  };
  window.__probe286 = { run, audit, checks };
})();
