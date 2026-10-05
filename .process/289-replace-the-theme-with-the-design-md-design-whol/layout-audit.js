/* Layout audit (#289). Paste into the console of the running web app (or pass to preview_eval / javascript_tool).
   await __layoutAudit.run({ lang: 'ar', routes: ['/student', '/student/progress'] })  -> findings ([] = clean)
   __layoutAudit.links('/student/subject/') -> in-app hrefs on the current page. Navigation uses history.pushState,
   which TanStack Router's browser history patches, so the SPA routes without a reload. */
(() => {
  const TOL = 1;
  const css = (el) => getComputedStyle(el);
  const rect = (el) => el.getBoundingClientRect();
  const rtl = () => document.documentElement.dir === 'rtl';
  const start = (r) => (rtl() ? r.right : r.left);
  const contentStart = (el) => {
    const r = rect(el), s = css(el);
    return rtl() ? r.right - parseFloat(s.paddingRight) - parseFloat(s.borderRightWidth) : r.left + parseFloat(s.paddingLeft) + parseFloat(s.borderLeftWidth);
  };
  const visible = (el) => { const r = rect(el), s = css(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && !el.closest('.sr-only'); };
  const ancestors = (el) => { const list = []; for (let p = el.parentElement; p && p !== document.documentElement; p = p.parentElement) list.push(p); return list; };
  const inFixed = (el) => [el, ...ancestors(el)].some((p) => css(p).position === 'fixed');
  const clipped = (el) => ancestors(el).some((p) => ['auto', 'scroll', 'hidden', 'clip'].includes(css(p).overflowX));
  const isFlexRow = (el) => css(el).display.endsWith('flex') && !css(el).flexDirection.startsWith('column');
  const isFlexCol = (el) => css(el).display.endsWith('flex') && css(el).flexDirection.startsWith('column');
  const name = (el) => `${el.tagName.toLowerCase()}${el.id ? `#${el.id}` : ''} "${(el.getAttribute('aria-label') ?? el.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 40)}"`;
  const finding = (check, el, detail) => ({ check, target: el ? name(el) : '', detail });
  const align = (el) => {
    const a = css(el).textAlign, ltr = css(el).direction === 'ltr';
    if (a === 'start' || a === (ltr ? 'left' : 'right')) return 'start';
    if (a === 'end' || a === (ltr ? 'right' : 'left')) return 'end';
    return 'center';
  };
  const CONTROL = 'button, a[data-slot="button"], input:not([type="checkbox"]):not([type="radio"]):not([type="hidden"]), select';

  const checks = {
    overflow() {
      if (document.documentElement.scrollWidth <= innerWidth + TOL) return [];
      const hits = [];
      for (const el of document.body.querySelectorAll('*')) {
        if (!visible(el) || inFixed(el) || clipped(el) || hits.some((h) => h.contains(el))) continue;
        const r = rect(el);
        if (r.right > innerWidth + TOL || r.left < -TOL) hits.push(el);
      }
      return hits.map((el) => finding('overflow', el, `left ${Math.round(rect(el).left)} right ${Math.round(rect(el).right)} viewport ${innerWidth}`));
    },
    edges() {
      const main = document.querySelector('main');
      if (!main) return [finding('edges', null, 'no <main>')];
      const ref = contentStart(main), out = [];
      const row = document.querySelector('body header > div');
      if (row && Math.abs(rect(row).width - rect(main).width) <= TOL) {
        if (Math.abs(contentStart(row) - ref) > TOL) out.push(finding('edges', row, `header row content ${Math.round(contentStart(row))} vs main ${Math.round(ref)}`));
        const logo = row.querySelector('a');
        if (logo && Math.abs(start(rect(logo)) - ref) > TOL) out.push(finding('edges', logo, `logo ${Math.round(start(rect(logo)))} vs main ${Math.round(ref)}`));
      }
      const root = main.firstElementChild;
      const blocks = new Set([main.querySelector('h1'), root, ...(root ? [...root.children] : [])]);
      for (const el of blocks) {
        if (!el || !visible(el) || inFixed(el) || rect(el).width < 24) continue;
        if (Math.abs(start(rect(el)) - ref) > TOL) out.push(finding('edges', el, `starts ${Math.round(start(rect(el)) - ref)}px from the page content edge`));
      }
      return out;
    },
    rows() {
      const out = [];
      for (const el of document.body.querySelectorAll('*')) {
        if (!isFlexRow(el) || !visible(el)) continue;
        const controls = [...el.children]
          .flatMap((c) => (c.matches(CONTROL) ? [c] : isFlexCol(c) ? [...c.children].filter((g) => g.matches(CONTROL)) : []))
          .filter(visible);
        const lines = [];
        for (const c of controls) {
          const r = rect(c), line = lines.find((l) => r.top < l.bottom && r.bottom > l.top);
          if (line) { line.items.push(c); line.top = Math.min(line.top, r.top); line.bottom = Math.max(line.bottom, r.bottom); }
          else lines.push({ top: r.top, bottom: r.bottom, items: [c] });
        }
        for (const { items } of lines) {
          if (items.length < 2) continue;
          const hs = items.map((c) => Math.round(rect(c).height));
          const cs = items.map((c) => rect(c).top + rect(c).height / 2);
          if (Math.max(...hs) - Math.min(...hs) > TOL) out.push(finding('row-heights', el, `control heights ${hs.join('/')}`));
          else if (Math.max(...cs) - Math.min(...cs) > TOL) out.push(finding('row-centres', el, `centres differ by ${(Math.max(...cs) - Math.min(...cs)).toFixed(1)}px`));
        }
      }
      return out;
    },
    icons() {
      const out = [];
      for (const svg of document.querySelectorAll('svg.lucide')) {
        if (!visible(svg) || rect(svg).height > 32) continue;
        const parent = svg.parentElement;
        if (isFlexCol(parent) || (isFlexRow(parent) && css(parent).alignItems === 'center')) continue;
        const sibling = [...parent.children].find((c) => c !== svg && visible(c) && c.textContent.trim());
        const textEl = sibling ?? ([...parent.childNodes].some((n) => n.nodeType === 3 && n.textContent.trim()) ? parent : null);
        if (!textEl) continue;
        const ts = css(textEl), lh = parseFloat(ts.lineHeight) || parseFloat(ts.fontSize) * 1.5;
        const lineCentre = rect(textEl).top + parseFloat(ts.paddingTop) + parseFloat(ts.borderTopWidth) + lh / 2;
        const dy = rect(svg).top + rect(svg).height / 2 - lineCentre;
        if (Math.abs(dy) > 2) out.push(finding('icon-centre', parent, `icon ${dy.toFixed(1)}px from the first text line centre`));
      }
      return out;
    },
    tables() {
      const out = [];
      for (const table of document.querySelectorAll('table')) {
        if (!visible(table)) continue;
        if (!['auto', 'scroll'].includes(css(table.parentElement).overflowX)) out.push(finding('table-scroll', table, 'not inside an overflow-x-auto wrapper'));
        const head = table.querySelector('thead tr'), body = table.querySelector('tbody tr');
        if (!head || !body) continue;
        [...head.children].forEach((th, i) => {
          const td = body.children[i];
          if (!td || td.colSpan > 1) return;
          if (align(th) !== align(td)) out.push(finding('table-align', th, `header ${align(th)} vs cell ${align(td)}`));
          else if (align(th) === 'start' && Math.abs(contentStart(th) - contentStart(td)) > TOL) out.push(finding('table-align', th, `header text ${Math.round(contentStart(th) - contentStart(td))}px from cell text`));
        });
      }
      return out;
    },
    header() {
      const banner = document.querySelector('body header');
      if (!banner || !banner.firstElementChild) return [];
      const out = [], row = banner.firstElementChild, mid = rect(row).top + rect(row).height / 2;
      if (banner.children.length !== 1) out.push(finding('header-rows', banner, `${banner.children.length} rows`));
      if (rect(banner).height > 56 + TOL) out.push(finding('header-height', banner, `${Math.round(rect(banner).height)}px (single row is 56)`));
      for (const el of row.querySelectorAll(':scope > *, nav a, nav > div > button')) {
        if (!visible(el) || css(el).display === 'none') continue;
        const c = rect(el).top + rect(el).height / 2;
        if (Math.abs(c - mid) > TOL) out.push(finding('header-centre', el, `${(c - mid).toFixed(1)}px off the row centre`));
      }
      return out;
    },
    tabBar() {
      const nav = [...document.querySelectorAll('nav')].find((n) => css(n).position === 'fixed' && visible(n));
      if (!nav) return [];
      const out = [], links = [...nav.querySelectorAll('a')], hs = links.map((a) => Math.round(rect(a).height));
      if (Math.max(...hs) - Math.min(...hs) > TOL) out.push(finding('tabbar-heights', nav, hs.join('/')));
      for (const a of links) {
        const mid = rect(a).left + rect(a).width / 2;
        for (const part of a.querySelectorAll(':scope > span')) {
          const c = rect(part).left + rect(part).width / 2;
          if (Math.abs(c - mid) > TOL) out.push(finding('tabbar-centre', a, `${(c - mid).toFixed(1)}px off centre`));
        }
      }
      return out;
    },
    targets() {
      const out = [];
      for (const el of document.querySelectorAll('a, button, input:not([type="hidden"]), select, textarea')) {
        if (!visible(el) || (el.tagName === 'A' && css(el).display === 'inline') || (el.matches('input[type="checkbox"], input[type="radio"]') && el.closest('label'))) continue;
        const r = rect(el);
        if (r.width < 24 || r.height < 24) out.push(finding('target-size', el, `${Math.round(r.width)}x${Math.round(r.height)}`));
      }
      return out;
    },
    primary() {
      const scope = [...document.querySelectorAll('[role="dialog"], [role="alertdialog"]')].filter(visible).at(-1) ?? document;
      const mint = [...scope.querySelectorAll('[data-slot="button"]')].filter((el) => visible(el) && css(el).backgroundColor === 'rgb(17, 238, 146)');
      return mint.length > 1 ? mint.map((el) => finding('primary-count', el, `${mint.length} mint buttons on the ${scope === document ? 'page' : 'dialog'}`)) : [];
    },
    hero() {
      const out = [];
      for (const el of document.querySelectorAll('.bg-hero')) {
        if (!visible(el)) continue;
        const s = css(el), padEnd = parseFloat(rtl() ? s.paddingLeft : s.paddingRight);
        if (!s.backgroundImage.startsWith(`linear-gradient(${rtl() ? 270 : 90}deg`)) out.push(finding('hero-direction', el, s.backgroundImage.slice(0, 40)));
        if (padEnd < 20 - TOL) out.push(finding('hero-padding', el, `inline-end padding ${padEnd}px < 20`));
      }
      return out;
    },
  };

  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const settle = async () => { await sleep(300); for (let i = 0; i < 40 && document.querySelector('[aria-busy="true"]'); i += 1) await sleep(250); await sleep(300); };
  const go = async (path) => { if (location.pathname + location.search !== path) history.pushState(null, '', path); await settle(); };
  const setLang = async (lng) => { const { i18n } = await import('/src/app/i18n.ts'); await i18n.changeLanguage(lng); await settle(); };
  const audit = () => Object.values(checks).flatMap((check) => check().map((f) => ({ route: location.pathname, width: innerWidth, dir: document.documentElement.dir, ...f })));
  const run = async ({ routes = [location.pathname], lang } = {}) => { if (lang) await setLang(lang); const all = []; for (const path of routes) { await go(path); all.push(...audit()); } return all; };
  const fonts = () => [...document.fonts].filter((f) => f.status === 'loaded').map((f) => `${f.family} ${f.weight}`);
  const links = (prefix) => [...new Set([...document.querySelectorAll(`a[href^="${prefix}"]`)].map((a) => a.getAttribute('href')))];
  window.__layoutAudit = { run, audit, go, setLang, links, checks, fonts };
})();
